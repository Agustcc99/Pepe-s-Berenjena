using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Niveles y perks con servicios reales: la XP sale de las ventas (precio BASE), el nivel se deduce de ella, cada nivel da un punto y
    // cada punto se gasta en un perk con efecto real en la mochila y en la velocidad.
    // Curva de prueba = la del juego: nivel 2 a los 125 de XP, el 3 a los 500, el 4 a los 1.125; tope en 20.
    public class LevelProgressionTests
    {
        private FieldTestWorld _world;
        private SaveRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = SaveRig.Create(_world);
            _rig.Start();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        // Da XP vendiendo tomates ($10 de XP base cada uno).
        private void GainXp(int xp)
        {
            for (int i = 0; i < xp / 10; i++) _rig.Economy.SellProduct(_rig.Tomato);
        }

        [UnityTest]
        public IEnumerator EverySale_GivesTheBasePriceAsXp_NotTheUpgradedOne()
        {
            Assert.AreEqual(1, _rig.Progression.Level);
            Assert.AreEqual(0L, _rig.Progression.Xp);

            _rig.Economy.SellProduct(_rig.Carrot);
            Assert.AreEqual(6L, _rig.Progression.Xp);

            _rig.Economy.AddCoins(1000);
            _rig.Progression.TryPay(_rig.Price, 150);   // el precio de venta pasa a x1,5
            long coins = _rig.Economy.SellProduct(_rig.Tomato);
            Assert.AreEqual(15L, coins, "Pagó $15 con la mejora de precio...");
            Assert.AreEqual(16L, _rig.Progression.Xp, "...pero la XP fue la del precio base ($10): comprar Precio no acelera los niveles.");
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelUp_FiresOnce_AtTheThreshold_WithTheNewLevel()
        {
            var levels = new List<int>();
            _rig.Progression.LevelChanged += levels.Add;

            GainXp(120);
            Assert.AreEqual(1, _rig.Progression.Level);
            Assert.AreEqual(0, levels.Count);
            Assert.AreEqual(0, _rig.Progression.PerkPoints);

            _rig.Economy.SellProduct(_rig.Tomato);   // 130 de XP
            Assert.AreEqual(2, _rig.Progression.Level);
            CollectionAssert.AreEqual(new[] { 2 }, levels);
            Assert.AreEqual(1, _rig.Progression.PerkPoints, "Un punto por nivel que se sube.");

            _rig.Economy.SellProduct(_rig.Tomato);
            Assert.AreEqual(1, levels.Count, "Dentro del mismo nivel no vuelve a avisar.");

            _rig.Progression.GetLevelProgress(out long into, out long needed);
            Assert.AreEqual(140L - 125L, into);
            Assert.AreEqual(375L, needed);
            yield break;
        }

        [UnityTest]
        public IEnumerator ABigSale_CanSkipSeveralLevels_WithASingleNotice_AndAllThePoints()
        {
            var levels = new List<int>();
            _rig.Progression.LevelChanged += levels.Add;

            GainXp(1130);   // pasa del nivel 1 al 4 (1.125)

            Assert.AreEqual(4, _rig.Progression.Level);
            Assert.AreEqual(3, _rig.Progression.PerkPoints);
            Assert.AreEqual(4, levels[levels.Count - 1]);
            yield break;
        }

        [UnityTest]
        public IEnumerator TheMaxLevel_IsACap_AndGivesNineteenPointsInTotal()
        {
            _rig.Progression.Restore(new ProgressData { xp = 9_999_999 });

            Assert.AreEqual(20, _rig.Progression.Level);
            Assert.IsTrue(_rig.Progression.IsMaxLevel);
            Assert.AreEqual(19, _rig.Progression.PerkPointsEarned);
            _rig.Progression.GetLevelProgress(out long into, out long needed);
            Assert.AreEqual(0L, needed, "En el tope ya no hay un nivel siguiente.");

            int levelUps = 0;
            _rig.Progression.LevelChanged += _ => levelUps++;
            GainXp(500);
            Assert.AreEqual(20, _rig.Progression.Level);
            Assert.AreEqual(0, levelUps);
            yield break;
        }

        // ---------- Elegir un perk ----------

        [UnityTest]
        public IEnumerator ChoosingAPerk_NeedsAPoint_SpendsIt_AndRecordsTheChoice()
        {
            Assert.IsFalse(_rig.Progression.CanChoose(_rig.Strength), "Sin puntos no se elige.");
            Assert.IsFalse(_rig.Progression.TryChoosePerk(_rig.Strength));
            Assert.AreEqual(0, _rig.Progression.PicksOf(_rig.Strength));

            GainXp(500);   // nivel 3: dos puntos
            Assert.AreEqual(2, _rig.Progression.PerkPoints);

            var kinds = new List<ProgressChangeKind>();
            _rig.Progression.Changed += change => kinds.Add(change.Kind);
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));

            Assert.AreEqual(1, _rig.Progression.PicksOf(_rig.Strength));
            Assert.AreEqual(1, _rig.Progression.PerkPoints);
            Assert.AreEqual(1, _rig.Progression.PerkPointsSpent);
            CollectionAssert.Contains(kinds, ProgressChangeKind.PerkChosen);

            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Quickness));
            Assert.AreEqual(0, _rig.Progression.PerkPoints);
            Assert.IsFalse(_rig.Progression.TryChoosePerk(_rig.Strength), "Se acabaron los puntos.");
            Assert.Throws<System.ArgumentNullException>(() => _rig.Progression.TryChoosePerk(null));
            yield break;
        }

        [UnityTest]
        public IEnumerator Strength_AddsOneBackpackSlotPerPick_KeepingWhatTheBackpackCarries()
        {
            GainXp(2000);   // nivel 5: cuatro puntos
            for (int i = 0; i < 6; i++) Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Wheat));
            Assert.IsTrue(_rig.Backpack.Backpack.IsFull);
            Assert.AreEqual(6, _rig.Backpack.Backpack.Capacity);

            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));
            Assert.AreEqual(7, _rig.Backpack.Backpack.Capacity, "Carga UN objeto más.");
            Assert.AreEqual(6, _rig.Backpack.Backpack.Count, "Lo que llevaba se conserva.");
            Assert.IsFalse(_rig.Backpack.Backpack.IsFull);

            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));
            Assert.AreEqual(9, _rig.Backpack.Backpack.Capacity);
            Assert.AreEqual(3, _rig.Progression.Modifiers.BackpackBonus);
            yield break;
        }

        [UnityTest]
        public IEnumerator Strength_AddsToTheCoinUpgrade_AndRaisesTheMaximumTheStackIsSizedFor()
        {
            Assert.AreEqual(6 + 8 + 5, _rig.Backpack.MaxCapacity, "El máximo cuenta la base, la mejora de monedas al tope y los 5 usos de Fuerza.");

            _rig.Economy.AddCoins(1000);
            _rig.Progression.TryPay(_rig.Capacity, 100);   // +4
            GainXp(1130);
            _rig.Progression.TryChoosePerk(_rig.Strength);
            _rig.Progression.TryChoosePerk(_rig.Strength);

            Assert.AreEqual(6 + 4 + 2, _rig.Backpack.Backpack.Capacity);
            yield break;
        }

        [UnityTest]
        public IEnumerator Quickness_AddsAPercentagePerPick_OnTopOfTheCoinUpgrade_AndAnnouncesIt()
        {
            GainXp(2000);
            int changes = 0;
            _rig.Progression.ModifiersChanged += () => changes++;

            Assert.AreEqual(1f, _rig.Progression.Modifiers.MoveSpeed);
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Quickness));
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Quickness));
            Assert.AreEqual(1.10f, _rig.Progression.Modifiers.MoveSpeed, 1e-5f, "Dos elecciones de +5 %.");
            Assert.AreEqual(2, changes);

            _rig.Economy.AddCoins(500);
            _rig.Progression.TryPay(_rig.Speed, 80);   // x1,2
            Assert.AreEqual(1.2f * 1.10f, _rig.Progression.Modifiers.MoveSpeed, 1e-5f, "La mejora de monedas y el perk se multiplican.");
            yield break;
        }

        [UnityTest]
        public IEnumerator ThePermanentSpeed_IsCapped_SoTheTemporaryBoostStillMatters()
        {
            TestReflection.Set(_rig.Config, "maxMoveSpeedMultiplier", 1.25f);
            GainXp(2000);
            _rig.Economy.AddCoins(500);
            _rig.Progression.TryPay(_rig.Speed, 80);                    // x1,2
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Quickness));   // x1,2 x 1,05 = 1,26 > 1,25
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Quickness));

            Assert.AreEqual(1.25f, _rig.Progression.Modifiers.MoveSpeed, 1e-5f, "El tope del GameConfig manda.");
            yield break;
        }

        [UnityTest]
        public IEnumerator APerk_SoldOutAtItsMaxPicks_StopsTakingPoints()
        {
            _rig.Progression.Restore(new ProgressData { xp = 45125 });   // nivel 20: 19 puntos
            for (int i = 0; i < 5; i++) Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));

            Assert.IsTrue(_rig.Progression.IsMaxed(_rig.Strength));
            Assert.IsFalse(_rig.Progression.CanChoose(_rig.Strength));
            Assert.IsFalse(_rig.Progression.TryChoosePerk(_rig.Strength), "Un perk agotado no gasta puntos.");
            Assert.AreEqual(14, _rig.Progression.PerkPoints, "19 puntos menos los 5 gastados.");
            Assert.AreEqual(5, _rig.Progression.PicksOf(_rig.Strength));
            yield break;
        }

        [UnityTest]
        public IEnumerator ChoosingAPerk_OnlyWorksWhilePlaying()
        {
            GainXp(500);
            _world.Game.Pause();
            Assert.IsFalse(_rig.Progression.TryChoosePerk(_rig.Strength), "En pausa no se elige.");
            _world.Game.Resume();

            Assert.IsTrue(_world.Game.BeginAd());
            Assert.IsFalse(_rig.Progression.TryChoosePerk(_rig.Strength), "Con un anuncio en pantalla tampoco.");
            Assert.IsTrue(_world.Game.EndAd());

            Assert.AreEqual(2, _rig.Progression.PerkPoints, "Ningún punto se gastó.");
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));
            yield break;
        }

        [UnityTest]
        public IEnumerator APerkRemovedFromTheGame_DoesNotEatThePlayersPoints()
        {
            GainXp(500);   // dos puntos
            var data = _rig.Progression.CreateSnapshot();
            data.perkPicks = new[] { new PerkPickEntry { id = "perk-that-was-removed", picks = 2 } };
            _rig.Progression.Restore(data);

            Assert.AreEqual(2, _rig.Progression.PerkPoints, "Los puntos gastados en un perk que ya no existe se devuelven (no cuentan como gastados).");
            yield break;
        }

        // ---------- El perk usado por el pad ----------

        [UnityTest]
        public IEnumerator PerksSurviveCloseAndReopen_AndTheirEffectsComeBack()
        {
            GainXp(2000);
            _rig.Progression.TryChoosePerk(_rig.Strength);
            _rig.Progression.TryChoosePerk(_rig.Strength);
            _rig.Progression.TryChoosePerk(_rig.Quickness);
            string before = _rig.Describe();

            _rig.Restart();

            Assert.AreEqual(before, _rig.Describe());
            Assert.AreEqual(8, _rig.Backpack.Backpack.Capacity);
            Assert.AreEqual(1.05f, _rig.Progression.Modifiers.MoveSpeed, 1e-5f);
            Assert.AreEqual(1, _rig.Progression.PerkPoints, "Un punto sin gastar también se guarda.");
            yield break;
        }
    }
}
