using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // La lógica pura de la Fase 8 que rodea a los buffs: enfriamientos, ritmo de la oferta, reglas de un anuncio a la vez, tope de
    // velocidad, stock de fertilizante, el anuncio simulado y el puente web.
    public class CooldownTrackerTests
    {
        [Test]
        public void ACooldown_RunsDown_WithGameTime_AndIsReadyAtZero()
        {
            var cooldown = new CooldownTracker();
            Assert.IsTrue(cooldown.IsReady("video"), "Sin enfriamiento está listo.");

            cooldown.Start("video", 10f);
            Assert.IsFalse(cooldown.IsReady("video"));
            Assert.AreEqual(10f, cooldown.Remaining("video"), 1e-5f);

            cooldown.Tick(4f);
            Assert.AreEqual(6f, cooldown.Remaining("video"), 1e-5f);

            cooldown.Tick(6f);
            Assert.IsTrue(cooldown.IsReady("video"));
            Assert.AreEqual(0f, cooldown.Remaining("video"));
        }

        [Test]
        public void ACooldown_FreezesWithoutGameTime()
        {
            var cooldown = new CooldownTracker();
            cooldown.Start("video", 10f);

            cooldown.Tick(0f);
            cooldown.Tick(-2f);
            cooldown.Tick(float.NaN);

            Assert.AreEqual(10f, cooldown.Remaining("video"), 1e-5f);
        }

        [Test]
        public void Cooldowns_AreIndependentPerKey_AndRestartable()
        {
            var cooldown = new CooldownTracker();
            cooldown.Start("a", 10f);
            cooldown.Start("b", 3f);

            cooldown.Tick(5f);
            Assert.IsTrue(cooldown.IsReady("b"));
            Assert.AreEqual(5f, cooldown.Remaining("a"), 1e-5f);

            cooldown.Start("a", 8f);   // reiniciar
            Assert.AreEqual(8f, cooldown.Remaining("a"), 1e-5f);

            cooldown.Start("a", 0f);   // sin espera
            Assert.IsTrue(cooldown.IsReady("a"));
        }

        [Test]
        public void Start_WithoutAKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CooldownTracker().Start("", 5f));
        }
    }

    public class OfferTimerTests
    {
        private static OfferTimer Create(out List<string> events, float random = 0.5f)
        {
            var timer = new OfferTimer(60f, 120f, 180f, 25f, () => random);
            var log = new List<string>();
            timer.Started += () => log.Add("start");
            timer.Ended += taken => log.Add(taken ? "taken" : "gone");
            events = log;
            return timer;
        }

        // D-04: la regla de rewarded de CrazyGames no admite una oferta que aparece sola con el juego activo. En la web no hay símbolo;
        // en Android y en el Editor sí.
        [Test]
        public void TheFloatingSymbol_IsOffInTheWeb_AndOnEverywhereElse()
        {
            Assert.IsFalse(OfferManager.SymbolsAllowedOn(UnityEngine.RuntimePlatform.WebGLPlayer));
            Assert.IsTrue(OfferManager.SymbolsAllowedOn(UnityEngine.RuntimePlatform.Android));
            Assert.IsTrue(OfferManager.SymbolsAllowedOn(UnityEngine.RuntimePlatform.WindowsEditor));
            Assert.IsTrue(OfferManager.SymbolsAllowedOn(UnityEngine.RuntimePlatform.WindowsPlayer));
        }

        [Test]
        public void TheFirstOffer_AppearsAfterTheFirstDelay_NotBefore()
        {
            OfferTimer timer = Create(out List<string> events);

            timer.Tick(59f, true);
            Assert.IsFalse(timer.IsOffering);
            Assert.IsEmpty(events);

            timer.Tick(1.5f, true);
            Assert.IsTrue(timer.IsOffering);
            CollectionAssert.AreEqual(new[] { "start" }, events);
            Assert.AreEqual(25f, timer.Remaining, 1e-5f, "El símbolo vive 'lifetime' segundos.");
        }

        [Test]
        public void TheCountdown_FreezesWhileAnOfferIsNotAllowed()
        {
            OfferTimer timer = Create(out _);

            timer.Tick(40f, true);
            timer.Tick(500f, false);   // buff activo, anuncio en pantalla, sin anuncio listo...
            Assert.IsFalse(timer.IsOffering, "No se acumula una oferta atrasada.");
            Assert.AreEqual(20f, timer.Remaining, 1e-5f);

            timer.Tick(20f, true);
            Assert.IsTrue(timer.IsOffering);
        }

        [Test]
        public void AnOffer_LeavesByItself_AndTheNextWaitIsDrawnBetweenMinAndMax()
        {
            OfferTimer timer = Create(out List<string> events, random: 0.5f);
            timer.Tick(61f, true);

            timer.Tick(26f, true);

            Assert.IsFalse(timer.IsOffering);
            CollectionAssert.AreEqual(new[] { "start", "gone" }, events);
            Assert.AreEqual(150f, timer.Remaining, 1e-4f, "Entre 120 y 180 con azar 0,5.");
        }

        [TestCase(0f, 120f)]
        [TestCase(1f, 180f)]
        public void TheNextWait_StaysInsideTheRange(float random, float expected)
        {
            OfferTimer timer = Create(out _, random);
            timer.Tick(61f, true);
            timer.Tick(30f, true);

            Assert.AreEqual(expected, timer.Remaining, 1e-4f);
        }

        [Test]
        public void TakingTheOffer_EndsItAsTaken()
        {
            OfferTimer timer = Create(out List<string> events);
            timer.Tick(61f, true);

            timer.Cancel(taken: true);

            Assert.IsFalse(timer.IsOffering);
            CollectionAssert.AreEqual(new[] { "start", "taken" }, events);
            timer.Cancel(taken: true);
            Assert.AreEqual(2, events.Count, "Cancelar sin oferta no hace nada.");
        }

        [Test]
        public void AnOfferInProgress_FreezesWithoutGameTime()
        {
            OfferTimer timer = Create(out _);
            timer.Tick(61f, true);

            timer.Tick(0f, true);   // pausa o anuncio en pantalla

            Assert.IsTrue(timer.IsOffering);
            Assert.AreEqual(25f, timer.Remaining, 1e-4f);
        }

        [Test]
        public void ForceNextOffer_MakesItAppearOnTheNextTick()
        {
            OfferTimer timer = Create(out _);

            timer.ForceNextOffer();
            timer.Tick(0.016f, true);

            Assert.IsTrue(timer.IsOffering);
        }

        [Test]
        public void BadTimes_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OfferTimer(10f, 20f, 10f, 25f, () => 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OfferTimer(10f, 20f, 30f, 0f, () => 0f));
        }
    }

    public class AdSessionTests
    {
        [Test]
        public void OnlyOneAdAtATime()
        {
            var session = new AdSession(20f, 120f);

            Assert.AreEqual(AdRejection.None, session.TryBegin(0f, out int first));
            Assert.IsTrue(session.IsShowing);
            Assert.AreEqual(first, session.CurrentId);

            Assert.AreEqual(AdRejection.Busy, session.TryBegin(1f, out int second));
            Assert.AreEqual(0, second);
        }

        [Test]
        public void TheResult_CountsOnlyOnce_ForTheCurrentRequest()
        {
            var session = new AdSession(20f, 120f);
            session.TryBegin(0f, out int id);

            Assert.IsFalse(session.TryComplete(id + 1, 1f, true), "Un pedido que no es el actual se ignora.");
            Assert.IsTrue(session.TryComplete(id, 1f, true));
            Assert.IsFalse(session.TryComplete(id, 2f, true), "El resultado repetido se ignora: no se otorga dos veces.");
            Assert.IsFalse(session.IsShowing);
        }

        [Test]
        public void ThereIsAMinimumGap_AfterAnAdThatWasShown()
        {
            var session = new AdSession(20f, 120f);
            session.TryBegin(0f, out int id);
            session.TryComplete(id, 30f, wasShown: true);

            Assert.AreEqual(AdRejection.Cooldown, session.TryBegin(40f, out _));
            Assert.AreEqual(10f, session.CooldownRemaining(40f), 1e-4f);
            Assert.AreEqual(AdRejection.None, session.TryBegin(50.5f, out _));
        }

        [Test]
        public void AnAdThatNeverShowed_DoesNotStartTheCooldown()
        {
            var session = new AdSession(20f, 120f);
            session.TryBegin(0f, out int id);
            session.TryComplete(id, 1f, wasShown: false);   // NoFill o error al cargar

            Assert.AreEqual(AdRejection.None, session.TryBegin(1.5f, out _));
        }

        [Test]
        public void ARequest_TimesOut_AndALateResultIsIgnored()
        {
            var session = new AdSession(0f, 60f);
            session.TryBegin(100f, out int id);

            Assert.IsFalse(session.HasTimedOut(159f));
            Assert.IsTrue(session.HasTimedOut(160f));

            Assert.AreEqual(id, session.Abort(160f));
            Assert.IsFalse(session.IsShowing);
            Assert.IsFalse(session.TryComplete(id, 200f, true), "El resultado tardío de un pedido cortado no cuenta.");

            session.TryBegin(210f, out int next);
            Assert.AreNotEqual(id, next);
            Assert.IsFalse(session.TryComplete(id, 211f, true), "Y tampoco cuenta contra el pedido nuevo.");
            Assert.AreEqual(0, new AdSession(0f, 60f).Abort(0f), "Abortar sin anuncio no hace nada.");
        }
    }

    public class MoveSpeedRulesTests
    {
        // Los números del juego: base 5 u/s, permanente máximo x1,5 (7,5 u/s), buff x3, tope final 11 u/s.
        private const float Base = 5f;
        private const float Cap = 11f;

        [Test]
        public void WithoutABuff_TheSpeedIsBaseTimesPermanent_AndTheCapDoesNotTouchIt()
        {
            Assert.AreEqual(5f, MoveSpeedRules.Final(Base, 1f, 1f, Cap), 1e-5f);
            Assert.AreEqual(7.5f, MoveSpeedRules.Final(Base, 1.5f, 1f, Cap), 1e-5f);
        }

        [Test]
        public void TheTripleBuff_IsCappedAtTheFinalSpeed()
        {
            Assert.AreEqual(11f, MoveSpeedRules.Final(Base, 1f, 3f, Cap), 1e-5f, "Jugador nuevo: de 5 a 11 (se nota x2,2) y no 15.");
            Assert.AreEqual(11f, MoveSpeedRules.Final(Base, 1.5f, 3f, Cap), 1e-5f, "Al máximo: de 7,5 a 11 (x1,47) y no 22,5.");
        }

        [Test]
        public void ASmallBuff_BelowTheCap_PassesThroughUntouched()
        {
            Assert.AreEqual(7.5f, MoveSpeedRules.Final(Base, 1f, 1.5f, Cap), 1e-5f);
        }

        [Test]
        public void TheCap_NeverLowersThePermanentSpeed()
        {
            Assert.AreEqual(7.5f, MoveSpeedRules.Final(Base, 1.5f, 1f, 6f), 1e-5f, "Aunque el tope sea menor que lo permanente.");
            Assert.AreEqual(7.5f, MoveSpeedRules.Final(Base, 1.5f, 3f, 6f), 1e-5f);
        }

        [Test]
        public void NoCap_MeansNoLimit()
        {
            Assert.AreEqual(22.5f, MoveSpeedRules.Final(Base, 1.5f, 3f, 0f), 1e-4f);
        }

        [Test]
        public void ANegativeValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MoveSpeedRules.Final(-1f, 1f, 1f, Cap));
            Assert.Throws<ArgumentOutOfRangeException>(() => MoveSpeedRules.Final(5f, -1f, 1f, Cap));
            Assert.Throws<ArgumentOutOfRangeException>(() => MoveSpeedRules.Final(5f, 1f, float.NaN, Cap));
        }
    }

    public class FertilizerStockTests
    {
        [Test]
        public void TheStock_FillsUpToTheMax_AndNoFurther()
        {
            var stock = new FertilizerStock(3);

            Assert.AreEqual(2, stock.Add(2));
            Assert.AreEqual(1, stock.Add(5), "Solo entra lo que cabe.");
            Assert.IsTrue(stock.IsFull);
            Assert.AreEqual(0, stock.Add(1));
            Assert.AreEqual(3, stock.Count);
        }

        [Test]
        public void TakingOne_Consumes_AndAnEmptyStockGivesNothing()
        {
            var stock = new FertilizerStock(3);
            int changes = 0;
            stock.Changed += () => changes++;

            Assert.IsFalse(stock.TryTake());
            Assert.AreEqual(0, changes, "Sin cambios no avisa.");

            stock.Add(1);
            Assert.IsTrue(stock.TryTake());
            Assert.AreEqual(0, stock.Count);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Set_ClampsToTheRange()
        {
            var stock = new FertilizerStock(3);

            stock.Set(99);
            Assert.AreEqual(3, stock.Count);

            stock.Set(-4);
            Assert.AreEqual(0, stock.Count);
        }

        [Test]
        public void AddingZeroOrLess_DoesNothing()
        {
            var stock = new FertilizerStock(3);

            Assert.AreEqual(0, stock.Add(0));
            Assert.AreEqual(0, stock.Add(-2));
            Assert.AreEqual(0, stock.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => new FertilizerStock(0));
        }
    }

    public class MockAdServiceTests
    {
        [Test]
        public void ItSimulatesTheDuration_ThenDeliversTheConfiguredResult()
        {
            var mock = new MockAdService { DurationSeconds = 2f, NextResult = AdResult.Rewarded };
            var results = new List<AdResult>();

            mock.ShowRewarded("p", results.Add);
            Assert.IsTrue(mock.IsShowing);
            Assert.IsFalse(mock.IsReady("p"), "Mientras muestra uno no hay otro listo.");

            mock.Tick(1.5f);
            Assert.IsEmpty(results);
            Assert.AreEqual(0.5f, mock.RemainingSeconds, 1e-5f);

            mock.Tick(1f);
            CollectionAssert.AreEqual(new[] { AdResult.Rewarded }, results);
            Assert.IsFalse(mock.IsShowing);
            Assert.IsTrue(mock.IsReady("p"));
            Assert.AreEqual(1, mock.ShowCount);
            Assert.AreEqual("p", mock.LastPlacement);
        }

        [TestCase(AdResult.Closed)]
        [TestCase(AdResult.Failed)]
        [TestCase(AdResult.Rewarded)]
        public void ItDeliversEachConfigurableResult(AdResult wanted)
        {
            var mock = new MockAdService { DurationSeconds = 0f, NextResult = wanted };
            var results = new List<AdResult>();

            mock.ShowRewarded("p", results.Add);
            mock.Tick(0.016f);

            CollectionAssert.AreEqual(new[] { wanted }, results);
        }

        [Test]
        public void WithoutAnAd_ItIsNotReady_AndAnswersNoFillRightAway()
        {
            var mock = new MockAdService { Available = false };
            var results = new List<AdResult>();

            Assert.IsFalse(mock.IsReady("p"));
            mock.ShowRewarded("p", results.Add);

            CollectionAssert.AreEqual(new[] { AdResult.NoFill }, results);
            Assert.AreEqual(0, mock.ShowCount);
        }

        [Test]
        public void ASecondShow_WhileOneIsOnScreen_Fails()
        {
            var mock = new MockAdService { DurationSeconds = 5f };
            var first = new List<AdResult>();
            var second = new List<AdResult>();

            mock.ShowRewarded("p", first.Add);
            mock.ShowRewarded("p", second.Add);

            CollectionAssert.AreEqual(new[] { AdResult.Failed }, second);
            Assert.IsEmpty(first, "El primero sigue en pantalla.");
        }

        [Test]
        public void HoldOpen_KeepsItOnScreen_UntilCompletedByHand()
        {
            var mock = new MockAdService { DurationSeconds = 0f, HoldOpen = true };
            var results = new List<AdResult>();
            mock.ShowRewarded("p", results.Add);

            mock.Tick(100f);
            Assert.IsEmpty(results);

            Assert.IsTrue(mock.CompleteNow(AdResult.Closed));
            CollectionAssert.AreEqual(new[] { AdResult.Closed }, results);
            Assert.IsFalse(mock.CompleteNow(AdResult.Rewarded), "Ya no hay nada que cerrar.");
        }
    }

    public class WebAdServiceTests
    {
        private sealed class FakeBridge : IWebAdBridge
        {
            public bool Ready = true;
            public int Shown;
            public int LastRequestId;
            public string LastObject, LastMethod;

            public bool IsReady(string placement) { return Ready; }

            public void Show(string placement, int requestId, string gameObjectName, string methodName)
            {
                Shown++;
                LastRequestId = requestId;
                LastObject = gameObjectName;
                LastMethod = methodName;
            }
        }

        [TestCase("rewarded", AdResult.Rewarded)]
        [TestCase("closed", AdResult.Closed)]
        [TestCase("nofill", AdResult.NoFill)]
        [TestCase("failed", AdResult.Failed)]
        [TestCase("something-odd", AdResult.Failed)]
        public void TheBridgeResult_IsTranslated(string text, AdResult expected)
        {
            var bridge = new FakeBridge();
            var service = new WebAdService(bridge, "AdsManager", "OnWebAdResult");
            var results = new List<AdResult>();

            service.ShowRewarded("p", results.Add);
            Assert.AreEqual("AdsManager", bridge.LastObject);
            Assert.AreEqual("OnWebAdResult", bridge.LastMethod);

            service.OnBridgeResult(bridge.LastRequestId + "|" + text);

            CollectionAssert.AreEqual(new[] { expected }, results);
        }

        [Test]
        public void AResultForAnotherRequest_ABadFormat_OrARepeat_IsIgnored()
        {
            var bridge = new FakeBridge();
            var service = new WebAdService(bridge, "AdsManager", "OnWebAdResult");
            var results = new List<AdResult>();
            service.ShowRewarded("p", results.Add);
            int id = bridge.LastRequestId;

            service.OnBridgeResult((id + 7) + "|rewarded");
            service.OnBridgeResult("rewarded");
            service.OnBridgeResult("x|rewarded");
            service.OnBridgeResult("");
            service.OnBridgeResult(null);
            Assert.IsEmpty(results);

            service.OnBridgeResult(id + "|rewarded");
            service.OnBridgeResult(id + "|rewarded");
            CollectionAssert.AreEqual(new[] { AdResult.Rewarded }, results, "Una sola vez.");
        }

        [Test]
        public void IsReady_FollowsTheBridge_AndASecondShowFails()
        {
            var bridge = new FakeBridge { Ready = false };
            var service = new WebAdService(bridge, "AdsManager", "OnWebAdResult");
            Assert.IsFalse(service.IsReady("p"));

            bridge.Ready = true;
            Assert.IsTrue(service.IsReady("p"));

            var first = new List<AdResult>();
            var second = new List<AdResult>();
            service.ShowRewarded("p", first.Add);
            service.ShowRewarded("p", second.Add);

            CollectionAssert.AreEqual(new[] { AdResult.Failed }, second);
            Assert.AreEqual(1, bridge.Shown);
        }
    }
}
