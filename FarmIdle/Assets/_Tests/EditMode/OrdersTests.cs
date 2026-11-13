using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // F-18 (tanda 1): pedidos del pueblo. La lógica pura del cartel (OrderBoard), el OrdersManager con servicios reales sobre un
    // GameConfig de prueba (dos cultivos, uno con su zona sin abrir), la mochila (TryTake) y los datos del GameConfig real.
    public class OrdersTests
    {
        private static void Set(object target, string fieldName, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        // ---------- OrderBoard (lógica pura) ----------

        [Test]
        public void QuantityFor_FollowsTheBackpack_WithinLimits_AndBigIsDouble()
        {
            Assert.AreEqual(8, OrderBoard.QuantityFor(10, false, 0.8f, 4, 24, 2f, 40));
            Assert.AreEqual(16, OrderBoard.QuantityFor(10, true, 0.8f, 4, 24, 2f, 40));
            Assert.AreEqual(4, OrderBoard.QuantityFor(2, false, 0.8f, 4, 24, 2f, 40), "Nunca baja del mínimo.");
            Assert.AreEqual(24, OrderBoard.QuantityFor(100, false, 0.8f, 4, 24, 2f, 40), "Tope del pedido normal.");
            Assert.AreEqual(40, OrderBoard.QuantityFor(100, true, 0.8f, 4, 24, 2f, 40), "Tope del pedido grande.");
            Assert.GreaterOrEqual(OrderBoard.QuantityFor(2, true, 0.8f, 4, 24, 2f, 40), OrderBoard.QuantityFor(2, false, 0.8f, 4, 24, 2f, 40), "El grande nunca pide menos que el normal.");
        }

        [Test]
        public void PickIndex_FollowsTheWeights_AndIgnoresEmptyOnes()
        {
            int[] weights = { 1, 0, 3 };
            Assert.AreEqual(0, OrderBoard.PickIndex(weights, 3, 0));
            Assert.AreEqual(2, OrderBoard.PickIndex(weights, 3, 1));
            Assert.AreEqual(2, OrderBoard.PickIndex(weights, 3, 3));
            Assert.AreEqual(2, OrderBoard.PickIndex(weights, 3, 99), "Un sorteo fuera de rango se acota.");
            Assert.AreEqual(-1, OrderBoard.PickIndex(new[] { 0, 0 }, 2, 0));
            Assert.AreEqual(4, OrderBoard.WeightTotal(weights, 3));
        }

        [Test]
        public void Deliver_AcceptsOnlyWhatIsMissing_AndCompletesTheOrder()
        {
            var board = new OrderBoard();
            board.Fill(0, "wheat", 8);

            Assert.AreEqual(5, board.Deliver(0, 5));
            Assert.IsFalse(board.IsComplete(0));
            Assert.AreEqual(3, board[0].Remaining);

            Assert.AreEqual(3, board.Deliver(0, 10), "Solo toma lo que falta.");
            Assert.IsTrue(board.IsComplete(0));
            Assert.AreEqual(0, board.Deliver(0, 4), "Un pedido completo no recibe más.");
            Assert.AreEqual(0, board.Deliver(1, 4), "Un lugar vacío no recibe nada.");
        }

        [Test]
        public void ACompletedSlot_WaitsItsCooldown_BeforeNeedingANewOrder()
        {
            var board = new OrderBoard();
            Assert.IsTrue(board.NeedsFill(0), "Un lugar nuevo pide pedido de una vez.");

            board.Fill(0, "wheat", 4);
            Assert.IsFalse(board.NeedsFill(0));

            board.Deliver(0, 4);
            board.Complete(0, 60f);
            Assert.IsFalse(board[0].IsActive);
            Assert.IsFalse(board.NeedsFill(0));

            board.Tick(59f);
            Assert.IsFalse(board.NeedsFill(0));
            board.Tick(2f);
            Assert.IsTrue(board.NeedsFill(0));
        }

        [Test]
        public void Clear_EmptiesEverything_AndResetsTheSeeds()
        {
            var board = new OrderBoard();
            board.Fill(1, "carrot", 5);
            board.Complete(2, 100f);
            board.SeedsThisSeason = 2;

            board.Clear();

            Assert.IsTrue(board.NeedsFill(0));
            Assert.IsTrue(board.NeedsFill(1));
            Assert.IsTrue(board.NeedsFill(2));
            Assert.AreEqual(0, board.SeedsThisSeason);
        }

        [Test]
        public void Set_ClampsWhatWasDelivered_AndTurnsBrokenSlotsIntoEmptyOnes()
        {
            var board = new OrderBoard();
            board.Set(0, "wheat", 5, 99, 0f);
            Assert.AreEqual(5, board[0].Delivered);

            board.Set(1, null, 5, 2, 30f);
            Assert.IsFalse(board[1].IsActive);
            Assert.AreEqual(30f, board[1].Cooldown);

            board.Set(2, "wheat", 0, 0, 0f);
            Assert.IsFalse(board[2].IsActive);
        }

        // ---------- La mochila ----------

        [Test]
        public void TryTake_RemovesOnlyThatCrop_AndAnnouncesOnce()
        {
            CropData wheat = ScriptableObject.CreateInstance<CropData>();
            CropData carrot = ScriptableObject.CreateInstance<CropData>();
            var inventory = new Inventory(10);
            inventory.TryAdd(wheat);
            inventory.TryAdd(carrot);
            inventory.TryAdd(wheat);
            inventory.TryAdd(wheat);

            int changes = 0;
            inventory.Changed += change => changes++;

            Assert.AreEqual(2, inventory.TryTake(wheat, 2));
            Assert.AreEqual(1, changes, "Un solo aviso por entrega.");
            Assert.AreEqual(1, inventory.CountOf(wheat));
            Assert.AreEqual(1, inventory.CountOf(carrot));

            Assert.AreEqual(1, inventory.TryTake(wheat, 5), "Saca lo que haya.");
            Assert.AreEqual(0, inventory.TryTake(wheat, 1));
            Assert.AreEqual(2, changes, "Sin nada que sacar no avisa.");

            UnityEngine.Object.DestroyImmediate(wheat);
            UnityEngine.Object.DestroyImmediate(carrot);
        }

        // ---------- OrdersManager ----------

        private GameConfig _config;
        private CropData _cropA;
        private CropData _cropB;
        private CropData _cropClosed;
        private EconomyManager _economy;
        private BackpackManager _backpack;
        private PrestigeManager _prestige;
        private OrdersManager _orders;
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private CropData MakeCrop(string id, int price)
        {
            CropData crop = ScriptableObject.CreateInstance<CropData>();
            crop.name = id;
            Set(crop, "id", id);
            Set(crop, "displayName", id);
            Set(crop, "sellPrice", price);
            _objects.Add(crop);
            return crop;
        }

        [SetUp]
        public void SetUp()
        {
            _cropA = MakeCrop("crop_a", 10);
            _cropB = MakeCrop("crop_b", 20);
            _cropClosed = MakeCrop("crop_closed", 99);

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _objects.Add(_config);
            Set(_config, "crops", new[] { _cropA, _cropB, _cropClosed });
            Set(_config, "ordersMinZones", 0);
            Set(_config, "orderCrops", new[]
            {
                new OrderCropRule("crop_a", "", 1),
                new OrderCropRule("crop_b", "", 1),
                new OrderCropRule("crop_closed", "zone_never", 5)
            });

            _economy = Create<EconomyManager>();
            _economy.Initialize(_config);
            _backpack = Create<BackpackManager>();
            _backpack.Initialize(_config);
            _prestige = Create<PrestigeManager>();
            _prestige.Initialize(_config);
            _orders = Create<OrdersManager>();
            _orders.Initialize(_config);
            _orders.Roll = max => 0;   // siempre el primer candidato (crop_a)
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in _objects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }

            _objects.Clear();
        }

        private void FillBackpack(CropData crop, int count)
        {
            for (int i = 0; i < count; i++) _backpack.TryAdd(crop);
        }

        [Test]
        public void Tick_PutsThreeOrders_TheLastOneBig()
        {
            _orders.Tick(0.1f);

            for (int i = 0; i < _orders.SlotCount; i++)
            {
                Assert.IsTrue(_orders.IsActive(i), "Lugar " + i);
                Assert.AreEqual("crop_a", _orders.CropOf(i).Id);
            }

            Assert.AreEqual(8, _orders.QuantityOf(0), "80 % de la mochila de 10.");
            Assert.AreEqual(16, _orders.QuantityOf(2), "El grande, el doble.");
            Assert.IsTrue(_orders.IsBig(2));
        }

        [Test]
        public void WithTheBoardStillLocked_NoOrdersAppear()
        {
            Set(_config, "ordersMinZones", 2);   // sin ProgressionManager no hay zonas compradas

            Assert.IsFalse(_orders.IsUnlocked);
            _orders.Tick(1f);

            for (int i = 0; i < _orders.SlotCount; i++) Assert.IsFalse(_orders.IsActive(i));
        }

        [Test]
        public void ACropWhoseZoneIsClosed_NeverShowsUp()
        {
            for (int roll = 0; roll < 6; roll++)
            {
                int captured = roll;
                _orders.Roll = max => Math.Min(captured, max - 1);
                _orders.ResetSeason();
                _orders.Tick(0.1f);

                for (int i = 0; i < _orders.SlotCount; i++)
                {
                    Assert.AreNotEqual("crop_closed", _orders.CropOf(i).Id, "Con el sorteo " + roll);
                }
            }
        }

        [Test]
        public void Deliver_InTwoTrips_PaysQuarterExtra_AndKeepsTheOtherCrops()
        {
            _orders.Tick(0.1f);
            FillBackpack(_cropA, 5);
            FillBackpack(_cropB, 2);

            Assert.IsTrue(_orders.CanDeliver(0));
            Assert.AreEqual(5, _orders.TryDeliver(0));
            Assert.AreEqual(5, _orders.DeliveredOf(0));
            Assert.AreEqual(0, _economy.Balance, "Todavía no cobró.");
            Assert.AreEqual(2, _backpack.Backpack.CountOf(_cropB), "Lo de otro cultivo no se toca.");
            Assert.IsFalse(_orders.CanDeliver(0), "Sin más del cultivo pedido no hay qué entregar.");

            long paid = 0;
            _orders.Completed += (slot, coins, seeds) => paid = coins;

            FillBackpack(_cropA, 3);
            Assert.AreEqual(3, _orders.TryDeliver(0));

            Assert.AreEqual(100L, paid, "8 x 10 x 1,25.");
            Assert.AreEqual(100L, _economy.Balance);
            Assert.IsFalse(_orders.IsActive(0), "El lugar queda esperando el próximo pedido.");
            Assert.AreEqual(60f, _orders.CooldownOf(0));
            Assert.AreEqual(0, _economy.SoldUnits, "Entregar no es vender.");
        }

        [Test]
        public void ACompletedSlot_GetsANewOrderAfterItsWait()
        {
            _orders.Tick(0.1f);
            FillBackpack(_cropA, 8);
            _orders.TryDeliver(0);

            _orders.Tick(59f);
            Assert.IsFalse(_orders.IsActive(0));
            _orders.Tick(2f);
            Assert.IsTrue(_orders.IsActive(0));
            Assert.AreEqual(0, _orders.DeliveredOf(0));
        }

        private void CompleteBigOrder()
        {
            int guard = 0;
            while (_orders.IsActive(2) && guard++ < 10)
            {
                FillBackpack(_cropA, _backpack.Backpack.Capacity);
                _orders.TryDeliver(2);
            }
        }

        [Test]
        public void TheBigOrder_GivesASeed_UpToTheSeasonCap()
        {
            _orders.Tick(0.1f);

            CompleteBigOrder();
            Assert.AreEqual(1, _prestige.Seeds);
            Assert.AreEqual(224L, _economy.Balance, "16 x 10 x 1,4.");

            _orders.Tick(601f);
            CompleteBigOrder();
            Assert.AreEqual(2, _prestige.Seeds);

            _orders.Tick(601f);
            CompleteBigOrder();
            Assert.AreEqual(2, _prestige.Seeds, "Con el tope de la temporada cumplido ya no da semillas.");
            Assert.AreEqual(0, _orders.SeedsLeft);
        }

        [Test]
        public void ANormalOrder_NeverGivesSeeds()
        {
            _orders.Tick(0.1f);
            FillBackpack(_cropA, 8);
            _orders.TryDeliver(0);

            Assert.AreEqual(0, _prestige.Seeds);
        }

        [Test]
        public void ANewSeason_ClearsTheBoardAndTheSeedCount()
        {
            _orders.Tick(0.1f);
            CompleteBigOrder();
            Assert.AreEqual(1, _orders.SeedsThisSeason);

            _orders.ResetSeason();

            Assert.AreEqual(0, _orders.SeedsThisSeason);
            for (int i = 0; i < _orders.SlotCount; i++) Assert.IsFalse(_orders.IsActive(i));
        }

        [Test]
        public void Capture_Then_Restore_KeepsOrdersProgressAndWaits()
        {
            _orders.Tick(0.1f);
            FillBackpack(_cropA, 3);
            _orders.TryDeliver(1);
            CompleteBigOrder();   // el lugar grande queda esperando 600 s y con 1 semilla contada

            string json = _orders.Capture();

            var other = Create<OrdersManager>();
            other.Initialize(_config);
            other.Restore(json);

            Assert.AreEqual(3, other.DeliveredOf(1));
            Assert.AreEqual(8, other.QuantityOf(1));
            Assert.AreEqual("crop_a", other.CropOf(1).Id);
            Assert.IsFalse(other.IsActive(2));
            Assert.AreEqual(600f, other.CooldownOf(2));
            Assert.AreEqual(1, other.SeedsThisSeason);
        }

        [Test]
        public void Restore_WithNothing_OrWithAnUnknownCrop_StartsClean()
        {
            _orders.Restore(null);
            for (int i = 0; i < _orders.SlotCount; i++) Assert.IsFalse(_orders.IsActive(i));

            _orders.Restore("esto no es json");
            for (int i = 0; i < _orders.SlotCount; i++) Assert.IsFalse(_orders.IsActive(i));

            _orders.Restore("{\"version\":1,\"cropIds\":[\"ya_no_existe\",\"crop_a\",\"\"],\"quantities\":[5,6,0],\"delivered\":[1,2,0],\"cooldowns\":[0,0,0],\"seedsThisSeason\":-4}");
            Assert.IsFalse(_orders.IsActive(0), "El cultivo que ya no existe se descarta.");
            Assert.IsTrue(_orders.IsActive(1));
            Assert.AreEqual(6, _orders.QuantityOf(1));
            Assert.AreEqual(2, _orders.DeliveredOf(1));
            Assert.AreEqual(0, _orders.SeedsThisSeason, "Un contador negativo se acota.");
        }

        // ---------- Datos reales ----------

        [Test]
        public void TheRealConfig_HasOrderRules_ForCropsAndZonesThatExist()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length);
            var real = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            Assert.AreEqual(real.Crops.Count, real.OrderCrops.Count, "Un cultivo por regla.");
            foreach (OrderCropRule rule in real.OrderCrops)
            {
                Assert.IsNotNull(real.FindCrop(rule.CropId), "Cultivo inexistente: " + rule.CropId);

                if (string.IsNullOrEmpty(rule.ZoneId)) continue;

                bool found = false;
                foreach (ZoneData zone in real.Zones)
                {
                    if (zone != null && zone.Id == rule.ZoneId) found = true;
                }

                Assert.IsTrue(found, "Zona inexistente: " + rule.ZoneId);
            }

            Assert.Greater(real.OrderBigBonus, real.OrderBonus, "El pedido grande paga más.");
            Assert.GreaterOrEqual(real.OrderBigMaxQuantity, real.OrderMaxQuantity);
        }
    }
}
