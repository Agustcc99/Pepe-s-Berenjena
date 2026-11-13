using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Las reglas de AdsManager con servicios reales (economía, buffs, fertilizante) y el MockAdService, sin cargar el nivel: ver un
    // anuncio otorga la recompensa SOLO si se completa; saltado, error y sin anuncio no dan nada y devuelven el juego a Playing; el
    // tiempo y los buffs se congelan en AdShowing y Paused; enfriamientos; corte por tiempo; resultados tardíos.
    public class AdsRulesTests
    {
        private FieldTestWorld _world;
        private AdsRig _rig;
        private readonly List<string> _log = new List<string>();

        private sealed class ThrowingAdService : IAdService
        {
            public string Name => "Throwing";
            public bool IsReady(string placement) { return true; }
            public void ShowRewarded(string placement, Action<AdResult> onFinished) { throw new InvalidOperationException("el SDK explotó"); }
            public void Tick(float unscaledDeltaSeconds) { }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = AdsRig.Create(_world);
            _log.Clear();
            _rig.Ads.AdFinished += (reward, result, granted) => _log.Add(reward.Id + ":" + result + ":" + granted);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 4f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private IEnumerator WaitForAdToEnd()
        {
            yield return WaitFor(() => _world.Game.State == GameState.Playing);
        }

        // ---------- Resultados ----------

        [UnityTest]
        public IEnumerator ACompletedAd_GrantsTheFertilizer_AndTheGameComesBackToPlaying()
        {
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Fert));

            Assert.AreEqual(GameState.AdShowing, _world.Game.State, "Mientras se muestra, el juego está en AdShowing...");
            Assert.AreEqual(0f, Time.timeScale, "...con el tiempo congelado.");
            Assert.IsTrue(_rig.Ads.IsShowing);
            Assert.AreSame(_rig.Fert, _rig.Ads.CurrentReward);
            Assert.AreEqual(0, _rig.Fertilizer.Stock, "Todavía no se otorgó nada.");

            yield return WaitForAdToEnd();

            Assert.AreEqual(GameState.Playing, _world.Game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(1, _rig.Fertilizer.Stock);
            CollectionAssert.AreEqual(new[] { "fert:Rewarded:True" }, _log);
            Assert.IsFalse(_rig.Ads.IsShowing);
        }

        [UnityTest]
        public IEnumerator ACompletedAd_ActivatesTheTurbo_WithTheSpeedCap()
        {
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Turbo));
            yield return WaitForAdToEnd();

            Assert.IsTrue(_rig.Buffs.IsActive(_rig.Turbo));
            Assert.AreEqual(3f, _rig.Buffs.MoveSpeedMultiplier);
            Assert.AreEqual(11f, _rig.Buffs.FinalMoveSpeed(5f, 1f), 1e-4f, "x3 sobre 5 u/s serían 15: el tope final lo deja en 11.");
            Assert.AreEqual(11f, _rig.Buffs.FinalMoveSpeed(5f, 1.5f), 1e-4f, "Y sobre 7,5 serían 22,5: también 11.");
        }

        [UnityTest]
        public IEnumerator ASkippedAd_GrantsNothing_AndTheGameComesBackToPlaying()
        {
            _rig.Mock.NextResult = AdResult.Closed;

            _rig.Ads.TryRequest(_rig.Fert);
            yield return WaitForAdToEnd();

            Assert.AreEqual(GameState.Playing, _world.Game.State);
            Assert.AreEqual(0, _rig.Fertilizer.Stock);
            CollectionAssert.AreEqual(new[] { "fert:Closed:False" }, _log);
            Assert.AreEqual(0f, _rig.Ads.RewardCooldownRemaining(_rig.Fert), "Sin recompensa no hay enfriamiento de la recompensa.");
        }

        [UnityTest]
        public IEnumerator AnAdError_GrantsNothing_AndTheGameComesBackToPlaying()
        {
            _rig.Mock.NextResult = AdResult.Failed;

            _rig.Ads.TryRequest(_rig.Turbo);
            yield return WaitForAdToEnd();

            Assert.AreEqual(GameState.Playing, _world.Game.State);
            Assert.IsFalse(_rig.Buffs.HasAnyBuff);
            CollectionAssert.AreEqual(new[] { "turbo:Failed:False" }, _log);
        }

        [UnityTest]
        public IEnumerator WithNoAdAvailable_NothingStarts_AndTheGameStaysInPlaying()
        {
            _rig.Mock.Available = false;
            var rejected = new List<AdRequestStatus>();
            _rig.Ads.RequestRejected += (reward, status) => rejected.Add(status);

            Assert.AreEqual(AdRequestStatus.NoAd, _rig.Ads.TryRequest(_rig.Fert));

            Assert.AreEqual(GameState.Playing, _world.Game.State, "Ni siquiera pasó a AdShowing.");
            Assert.AreEqual(1f, Time.timeScale);
            CollectionAssert.AreEqual(new[] { AdRequestStatus.NoAd }, rejected);
            Assert.AreEqual(0, _rig.Fertilizer.Stock);
            yield break;
        }

        [UnityTest]
        public IEnumerator ATrueFailureInsideTheService_IsCaught_AndTheGameComesBackToPlaying()
        {
            _rig.Destroy();
            _world.Game.Resume();   // por las dudas: sigue en Playing
            _rig = AdsRig.Create(_world, new ThrowingAdService());
            _rig.Ads.AdFinished += (reward, result, granted) => _log.Add(reward.Id + ":" + result + ":" + granted);
            LogAssert.Expect(LogType.Error, new Regex("el servicio falló"));

            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Fert));

            Assert.AreEqual(GameState.Playing, _world.Game.State, "El fallo del SDK no deja el juego trabado en AdShowing.");
            Assert.AreEqual(0, _rig.Fertilizer.Stock);
            CollectionAssert.AreEqual(new[] { "fert:Failed:False" }, _log);
            yield break;
        }

        // ---------- Tiempo congelado ----------

        [UnityTest]
        public IEnumerator Buffs_AgeWhilePlaying_ButFreezeInPauseAndWhileAnAdIsShowing()
        {
            _rig.Buffs.Activate(_rig.Turbo);
            float start = _rig.Buffs.RemainingOf(_rig.Turbo);

            yield return new WaitForSecondsRealtime(0.3f);
            float afterPlaying = _rig.Buffs.RemainingOf(_rig.Turbo);
            Assert.Less(afterPlaying, start - 0.05f, "Jugando, el buff envejece.");

            // Pausa.
            _world.Game.Pause();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(afterPlaying, _rig.Buffs.RemainingOf(_rig.Turbo), 1e-4f, "En pausa se congela.");
            _world.Game.Resume();

            // Anuncio en pantalla (colgado a propósito para medir).
            _rig.Mock.HoldOpen = true;
            float beforeAd = _rig.Buffs.RemainingOf(_rig.Turbo);
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Fert));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(GameState.AdShowing, _world.Game.State);
            Assert.AreEqual(beforeAd, _rig.Buffs.RemainingOf(_rig.Turbo), 1e-3f, "Con un anuncio en pantalla se congela.");

            Assert.IsTrue(_rig.Mock.CompleteNow(AdResult.Closed));
            Assert.AreEqual(GameState.Playing, _world.Game.State);
        }

        [UnityTest]
        public IEnumerator ATurbo_ExpiresByItself_AndTheSpeedGoesBack()
        {
            _rig.Buffs.Activate(_rig.Turbo);   // dura 2 s de juego
            string expired = null;
            _rig.Buffs.BuffExpired += id => expired = id;
            Assert.AreEqual(11f, _rig.Buffs.FinalMoveSpeed(5f, 1f), 1e-4f);

            yield return WaitFor(() => !_rig.Buffs.HasAnyBuff, 6f);

            Assert.IsFalse(_rig.Buffs.IsActive(_rig.Turbo));
            Assert.AreEqual("turbo", expired);
            Assert.AreEqual(5f, _rig.Buffs.FinalMoveSpeed(5f, 1f), 1e-4f, "Vuelve a la velocidad normal.");
        }

        [UnityTest]
        public IEnumerator TheRewardCooldown_RunsInGameTime_NotInPause()
        {
            _rig.Ads.TryRequest(_rig.Fert);
            yield return WaitForAdToEnd();
            Assert.Greater(_rig.Ads.RewardCooldownRemaining(_rig.Fert), 1.2f, "Recién ganada: enfriándose (1,5 s de juego).");

            _world.Game.Pause();
            float paused = _rig.Ads.RewardCooldownRemaining(_rig.Fert);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(paused, _rig.Ads.RewardCooldownRemaining(_rig.Fert), 1e-4f, "En pausa no corre.");
            _world.Game.Resume();

            yield return WaitFor(() => _rig.Ads.RewardCooldownRemaining(_rig.Fert) <= 0f, 6f);
            Assert.AreEqual(0f, _rig.Ads.RewardCooldownRemaining(_rig.Fert));
        }

        // ---------- Quién puede pedir y cuándo ----------

        [UnityTest]
        public IEnumerator AnAd_CannotBeRequested_OutsidePlaying_OrWhileAnotherIsShowing()
        {
            _world.Game.Pause();
            Assert.AreEqual(AdRequestStatus.NotPlaying, _rig.Ads.TryRequest(_rig.Fert));
            _world.Game.Resume();

            _rig.Mock.HoldOpen = true;
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Fert));
            Assert.AreEqual(AdRequestStatus.Busy, _rig.Ads.TryRequest(_rig.Turbo), "Un solo anuncio a la vez.");

            _rig.Mock.CompleteNow(AdResult.Closed);
            yield break;
        }

        [UnityTest]
        public IEnumerator ThereIsAMinimumGapBetweenAds_AndEachRewardHasItsOwnCooldown()
        {
            _rig.Ads.TryRequest(_rig.Fert);
            yield return WaitForAdToEnd();
            Assert.AreEqual(1, _rig.Fertilizer.Stock);

            Assert.AreEqual(AdRequestStatus.Cooldown, _rig.Ads.TryRequest(_rig.Turbo), "Pasó muy poco desde el anuncio anterior.");
            Assert.AreEqual(AdRequestStatus.RewardCooldown, _rig.Ads.TryRequest(_rig.Fert), "Y esa recompensa todavía se está enfriando.");
            Assert.Greater(_rig.Ads.AdCooldownRemaining(), 0f);

            yield return WaitFor(() => _rig.Ads.AdCooldownRemaining() <= 0f, 4f);
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Turbo), "Pasado el mínimo, otra recompensa sí se puede.");
            yield return WaitForAdToEnd();
            Assert.IsTrue(_rig.Buffs.IsActive(_rig.Turbo));
        }

        [UnityTest]
        public IEnumerator AReward_ThatWouldBeUseless_IsNotOffered()
        {
            _rig.Fertilizer.DebugGive(2);   // stock lleno (máximo 2)
            Assert.AreEqual(AdRequestStatus.NotUseful, _rig.Ads.TryRequest(_rig.Fert));

            _rig.Buffs.Activate(_rig.Turbo);
            Assert.AreEqual(AdRequestStatus.NotUseful, _rig.Ads.TryRequest(_rig.Turbo), "Con el buff ya activo no se pide otro video.");

            Assert.AreEqual(GameState.Playing, _world.Game.State);
            yield break;
        }

        // ---------- Resultados que no cuentan ----------

        [UnityTest]
        public IEnumerator AnAdThatNeverAnswers_IsCutOffByTime_NothingIsGranted_AndALateResultIsIgnored()
        {
            _rig.Mock.HoldOpen = true;
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Turbo));
            Assert.AreEqual(GameState.AdShowing, _world.Game.State);

            // El corte por tiempo es de 0,8 s de reloj sin escala (aunque el tiempo del juego esté congelado).
            LogAssert.Expect(LogType.Warning, new Regex("no respondió"));
            yield return WaitFor(() => _world.Game.State == GameState.Playing, 4f);

            Assert.AreEqual(GameState.Playing, _world.Game.State, "Un anuncio colgado no deja el juego trabado.");
            Assert.IsFalse(_rig.Buffs.HasAnyBuff);
            CollectionAssert.AreEqual(new[] { "turbo:Failed:False" }, _log);

            // El SDK contesta tarde con "recompensa ganada": se ignora (no se otorga ni se avisa dos veces).
            LogAssert.Expect(LogType.Warning, new Regex("ignorado"));
            Assert.IsTrue(_rig.Mock.CompleteNow(AdResult.Rewarded));

            Assert.IsFalse(_rig.Buffs.HasAnyBuff, "Un resultado tardío no otorga nada.");
            Assert.AreEqual(1, _log.Count);
            Assert.AreEqual(GameState.Playing, _world.Game.State);
        }

        [UnityTest]
        public IEnumerator TheSameResult_CannotGrantTwice()
        {
            _rig.Mock.HoldOpen = true;
            _rig.Ads.TryRequest(_rig.Fert);

            Assert.IsTrue(_rig.Mock.CompleteNow(AdResult.Rewarded));
            Assert.IsFalse(_rig.Mock.CompleteNow(AdResult.Rewarded), "El servicio ya no tiene nada en pantalla.");

            Assert.AreEqual(1, _rig.Fertilizer.Stock);
            Assert.AreEqual(1, _log.Count);
            yield break;
        }

        // ---------- Fertilizante: comprar ----------

        [UnityTest]
        public IEnumerator BuyingFertilizer_SpendsCoins_UpToTheMaxStock_AndOnlyWhilePlaying()
        {
            Assert.AreEqual(100L, _rig.Fertilizer.Price);
            Assert.IsTrue(_rig.Fertilizer.TryBuy());
            Assert.AreEqual(900L, _rig.Economy.Balance);
            Assert.AreEqual(1, _rig.Fertilizer.Stock);

            _world.Game.Pause();
            Assert.IsFalse(_rig.Fertilizer.TryBuy(), "En pausa no se compra.");
            _world.Game.Resume();
            Assert.AreEqual(900L, _rig.Economy.Balance);

            Assert.IsTrue(_rig.Fertilizer.TryBuy());
            Assert.IsTrue(_rig.Fertilizer.IsFull);
            Assert.IsFalse(_rig.Fertilizer.TryBuy(), "Con el stock lleno no cobra.");
            Assert.AreEqual(800L, _rig.Economy.Balance);

            _rig.Economy.TrySpend(_rig.Economy.Balance);
            _rig.Fertilizer.TryUse();   // sin plantas creciendo no gasta
            Assert.AreEqual(2, _rig.Fertilizer.Stock);
            yield break;
        }

        [UnityTest]
        public IEnumerator BuyingFertilizer_HasACooldown_AndABuyCapBelowTheVideoCap()
        {
            TestReflection.Set(_rig.Config, "fertilizerBuyCooldownSeconds", 60f);

            Assert.IsTrue(_rig.Fertilizer.TryBuy());
            Assert.Greater(_rig.Fertilizer.BuyCooldownRemaining, 50f);
            Assert.IsFalse(_rig.Fertilizer.CanBuy, "En enfriamiento no se compra.");
            Assert.IsFalse(_rig.Fertilizer.TryBuy());
            Assert.AreEqual(900L, _rig.Economy.Balance, "Rechazada la compra no cobra.");

            TestReflection.Set(_rig.Config, "fertilizerBuyMaxStock", 1);
            TestReflection.Set(_rig.Config, "fertilizerBuyCooldownSeconds", 0f);
            Assert.IsTrue(_rig.Fertilizer.AtBuyLimit);
            Assert.IsFalse(_rig.Fertilizer.IsFull);
            Assert.IsFalse(_rig.Fertilizer.TryBuy(), "Con el tope de compra alcanzado no se compra más con monedas.");

            Assert.AreEqual(1, _rig.Fertilizer.Grant(1), "El video sigue sumando hasta el máximo.");
            Assert.AreEqual(2, _rig.Fertilizer.Stock);
            yield break;
        }

        [UnityTest]
        public IEnumerator YouCannotBuyFertilizer_WithoutTheCoins()
        {
            _rig.Economy.TrySpend(_rig.Economy.Balance - 99);

            Assert.IsFalse(_rig.Fertilizer.CanBuy);
            Assert.IsFalse(_rig.Fertilizer.TryBuy());
            Assert.AreEqual(99L, _rig.Economy.Balance);
            Assert.AreEqual(0, _rig.Fertilizer.Stock);
            yield break;
        }

        [UnityTest]
        public IEnumerator TheFertilizerFromAVideo_StopsAtTheMaxStock()
        {
            _rig.Fertilizer.DebugGive(1);

            _rig.Ads.TryRequest(_rig.Fert);
            yield return WaitForAdToEnd();

            Assert.AreEqual(2, _rig.Fertilizer.Stock);
            Assert.AreEqual(1, _log.Count);
        }
    }
}
