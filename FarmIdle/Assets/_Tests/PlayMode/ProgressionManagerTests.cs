using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // ProgressionManager con servicios reales (economía, mochila, GameManager en Playing): comprar es una transacción
    // autoritativa por EconomyManager.TrySpend y cada efecto llega a la capa que corresponde.
    // Zonas de prueba: 0 cuesta 300 y 1 cuesta 500. Mejoras: capacidad (+4 por 100, +8 por 200), precio (x1,5 por 150),
    // velocidad (x1,2 por 80) y crecimiento (x2 por 60).
    public class ProgressionManagerTests
    {
        private FieldTestWorld _world;
        private ProgressionRig _rig;
        private UpgradeData _capacity;
        private UpgradeData _price;
        private UpgradeData _speed;
        private UpgradeData _growth;
        private CropData _crop;
        private readonly List<ProgressChange> _changes = new List<ProgressChange>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _capacity = TestReflection.CreateUpgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f), (200L, 8f));
            _price = TestReflection.CreateUpgrade("price", UpgradeKind.SellPrice, (150L, 1.5f));
            _speed = TestReflection.CreateUpgrade("speed", UpgradeKind.MoveSpeed, (80L, 1.2f));
            _growth = TestReflection.CreateUpgrade("growth", UpgradeKind.GrowthSpeed, (60L, 2f));
            _rig = ProgressionRig.AddTo(_world, 0, 10, new long[] { 300, 500 }, _capacity, _price, _speed, _growth);

            _crop = TestReflection.CreateCrop("wheat", 5f, _world.PlantTemplate);
            TestReflection.Set(_crop, "sellPrice", 4);

            _changes.Clear();
            _rig.Progression.Changed += _changes.Add;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_crop);
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TryPay_WithoutMoney_BuysNothing_AndChargesNothing()
        {
            ZoneData zone = _rig.Zones[0];

            Assert.AreEqual(0L, _rig.Progression.TryPay(zone, 100), "Con la billetera vacía no hay con qué pagar.");
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(zone));
            Assert.AreEqual(0L, _rig.Economy.Balance);
            Assert.AreEqual(0L, _rig.Progression.PaidFor(zone));
            Assert.AreEqual(0, _changes.Count, "No cambió nada: no hay eventos.");
            yield break;
        }

        [UnityTest]
        public IEnumerator TryPay_ReachingTheCost_UnlocksTheZone_AndChargesExactlyTheCost()
        {
            ZoneData zone = _rig.Zones[0];
            _rig.Economy.AddCoins(320);

            long spent = _rig.Progression.TryPay(zone, 1000);   // se ofrece de más: solo se cobra lo que falta

            Assert.AreEqual(300L, spent);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(zone));
            Assert.AreEqual(20L, _rig.Economy.Balance, "Se descontó el costo, ni una moneda más.");
            Assert.AreEqual(0L, _rig.Progression.PaidFor(zone), "Completada la compra no queda nada pendiente.");
            Assert.IsTrue(_changes.Exists(c => c.Kind == ProgressChangeKind.ZoneUnlocked && c.Id == zone.Id));
            Assert.AreEqual(0L, _rig.Progression.TryPay(zone, 50), "Una zona abierta no cobra de nuevo.");
            Assert.AreEqual(20L, _rig.Economy.Balance);
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(_rig.Zones[1]), "La otra zona sigue cerrada.");
            yield break;
        }

        [UnityTest]
        public IEnumerator TryPay_InInstallments_KeepsWhatWasPaid_UntilItAddsUpToTheCost()
        {
            ZoneData zone = _rig.Zones[0];
            _rig.Economy.AddCoins(100);

            Assert.AreEqual(100L, _rig.Progression.TryPay(zone, 100));
            Assert.AreEqual(0L, _rig.Economy.Balance);
            Assert.AreEqual(100L, _rig.Progression.PaidFor(zone));
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(zone));
            Assert.AreEqual(0L, _rig.Progression.TryPay(zone, 50), "Sin saldo no se cobra (TrySpend falla) y no cambia nada.");
            Assert.AreEqual(100L, _rig.Progression.PaidFor(zone));

            _rig.Economy.AddCoins(250);
            Assert.AreEqual(200L, _rig.Progression.TryPay(zone, 250), "Solo lo que faltaba para llegar a 300.");
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(zone));
            Assert.AreEqual(50L, _rig.Economy.Balance);
            yield break;
        }

        [UnityTest]
        public IEnumerator CapacityUpgrade_GrowsTheBackpack_KeepingWhatItCarries()
        {
            Assert.AreEqual(10, _rig.Backpack.Backpack.Capacity);
            for (int i = 0; i < 10; i++) Assert.IsTrue(_rig.Backpack.TryAdd(_crop));
            Assert.IsTrue(_rig.Backpack.Backpack.IsFull);

            var capacities = new List<int>();
            _rig.Backpack.Backpack.CapacityChanged += capacities.Add;

            _rig.Economy.AddCoins(1000);
            Assert.AreEqual(100L, _rig.Progression.TryPay(_capacity, 100));

            Assert.AreEqual(1, _rig.Progression.LevelOf(_capacity));
            Assert.AreEqual(14, _rig.Backpack.Backpack.Capacity, "10 de base + 4 del nivel 1.");
            Assert.AreEqual(10, _rig.Backpack.Backpack.Count, "Lo que llevaba se conserva.");
            Assert.IsFalse(_rig.Backpack.Backpack.IsFull, "Ahora hay lugar.");
            CollectionAssert.AreEqual(new[] { 14 }, capacities);

            Assert.AreEqual(200L, _rig.Progression.TryPay(_capacity, 1000));
            Assert.AreEqual(18, _rig.Backpack.Backpack.Capacity, "El nivel 2 da +8 en total (no +4 +8).");
            Assert.AreEqual(10, _rig.Backpack.Backpack.Count);

            Assert.IsTrue(_rig.Progression.IsMaxed(_capacity));
            Assert.AreEqual(0L, _rig.Progression.TryPay(_capacity, 1000), "Al máximo no cobra.");
            Assert.AreEqual(0L, _rig.Progression.CostOfNextLevel(_capacity));
            yield break;
        }

        [UnityTest]
        public IEnumerator PriceUpgrade_ChangesWhatASaleCredits()
        {
            long before = _rig.Economy.SellProduct(_crop);
            Assert.AreEqual(4L, before);
            Assert.AreEqual(1f, _rig.Economy.Pricing.UpgradeMultiplier);

            long balance = _rig.Economy.Balance;
            _rig.Economy.AddCoins(150);
            Assert.AreEqual(150L, _rig.Progression.TryPay(_price, 150));
            Assert.AreEqual(1.5f, _rig.Economy.Pricing.UpgradeMultiplier, 1e-5f, "El multiplicador llega al PriceCalculator.");

            long after = _rig.Economy.SellProduct(_crop);
            Assert.AreEqual(6L, after, "4 x 1,5 = 6.");
            Assert.AreEqual(balance + 6L, _rig.Economy.Balance, "La venta ya paga con la mejora y la compra descontó lo suyo.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SpeedAndGrowthUpgrades_ReachTheModifiers_WithTheirEvent()
        {
            Assert.AreEqual(1f, _rig.Progression.Modifiers.MoveSpeed);
            Assert.AreEqual(1f, _rig.Progression.Modifiers.GrowthSpeed);

            int events = 0;
            _rig.Progression.ModifiersChanged += () => events++;

            _rig.Economy.AddCoins(1000);
            _rig.Progression.TryPay(_speed, 80);
            Assert.AreEqual(1.2f, _rig.Progression.Modifiers.MoveSpeed, 1e-5f);
            Assert.AreEqual(1, events);

            _rig.Progression.TryPay(_growth, 60);
            Assert.AreEqual(2f, _rig.Progression.Modifiers.GrowthSpeed, 1e-5f);
            Assert.AreEqual(2, events);

            // Un pago a medias no cambia ningún efecto ni avisa de efectos.
            _rig.Progression.TryPay(_capacity, 40);
            Assert.AreEqual(2, events);
            Assert.AreEqual(0, _rig.Progression.Modifiers.BackpackBonus);
            yield break;
        }

        [UnityTest]
        public IEnumerator WhilePausedOrShowingAnAd_NothingIsBought()
        {
            _rig.Economy.AddCoins(1000);
            ZoneData zone = _rig.Zones[0];

            Assert.IsTrue(_world.Game.Pause());
            Assert.AreEqual(0L, _rig.Progression.TryPay(zone, 300), "En pausa no se compra.");
            Assert.AreEqual(1000L, _rig.Economy.Balance);
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(zone));

            Assert.IsTrue(_world.Game.Resume());
            Assert.IsTrue(_world.Game.BeginAd());
            Assert.AreEqual(0L, _rig.Progression.TryPay(_price, 150), "Con un anuncio en pantalla tampoco.");
            Assert.AreEqual(1000L, _rig.Economy.Balance);
            Assert.AreEqual(0, _rig.Progression.LevelOf(_price));

            Assert.IsTrue(_world.Game.EndAd());
            Assert.AreEqual(300L, _rig.Progression.TryPay(zone, 300), "Al volver a Playing se compra normal.");
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(zone));
            yield break;
        }

        [UnityTest]
        public IEnumerator GrowthUpgrade_MakesThePlantsGrowFaster()
        {
            PlotView plot = _world.Plots[0];   // este cultivo tarda 5 s en crecer

            _rig.Economy.AddCoins(100);
            Assert.AreEqual(60L, _rig.Progression.TryPay(_growth, 60));   // x2

            Assert.IsTrue(plot.TrySow());
            float sownAt = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(PlotState.Growing, plot.State, "5 s a doble velocidad son 2,5 s: todavía no está.");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (plot.State != PlotState.Ready && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(PlotState.Ready, plot.State, "Con la mejora la planta madura en la mitad de tiempo.");
            Assert.Less(Time.realtimeSinceStartup - sownAt, 4.2f, "Sin la mejora habría tardado 5 s.");
        }

        [UnityTest]
        public IEnumerator Restore_ReappliesTheEffects_OfASavedProgress()
        {
            // Lo que la Fase 7 va a hacer: guardar el progreso y devolverlo en otra sesión.
            _rig.Economy.AddCoins(10000);
            _rig.Progression.TryPay(_capacity, 100);
            _rig.Progression.TryPay(_price, 150);
            _rig.Progression.TryPay(_rig.Zones[1], 500);
            _rig.Progression.TryPay(_rig.Zones[0], 120);   // una compra a medias
            ProgressData saved = JsonUtility.FromJson<ProgressData>(JsonUtility.ToJson(_rig.Progression.CreateSnapshot()));

            // Sesión nueva: servicios nuevos sobre el mismo mundo de prueba.
            _rig.Destroy();
            _world.Destroy();
            _world = FieldTestWorld.Create(1, 5f);
            _capacity = TestReflection.CreateUpgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f), (200L, 8f));
            _price = TestReflection.CreateUpgrade("price", UpgradeKind.SellPrice, (150L, 1.5f));
            _rig = ProgressionRig.AddTo(_world, 0, 10, new long[] { 300, 500 }, _capacity, _price);
            Assert.AreEqual(10, _rig.Backpack.Backpack.Capacity);

            // Los ids de los datos de prueba son los mismos, así que el progreso guardado les corresponde.
            var changes = new List<ProgressChange>();
            _rig.Progression.Changed += changes.Add;
            _rig.Progression.Restore(saved);

            Assert.AreEqual(14, _rig.Backpack.Backpack.Capacity);
            Assert.AreEqual(1.5f, _rig.Economy.Pricing.UpgradeMultiplier, 1e-5f);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(_rig.Zones[1]));
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(_rig.Zones[0]));
            Assert.AreEqual(120L, _rig.Progression.PaidFor(_rig.Zones[0]), "Lo pagado de una compra en curso también se guarda.");
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(ProgressChangeKind.Restored, changes[0].Kind);
            yield break;
        }
    }
}
