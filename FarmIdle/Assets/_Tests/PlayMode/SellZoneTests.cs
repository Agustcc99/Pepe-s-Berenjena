using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La venta con un mundo armado por código: parcelas reales (3 cultivos con precio), el PlayerActor y la pila visible de
    // la espalda, el EconomyManager y un pad de venta con su SellFeedback. Todo con servicios reales y física real: el
    // granjero se "pisa" el pad moviendo su transform, como en HarvestStackTests.
    // Precios: el cultivo 0 paga 2, el 1 paga 5 y el 2 paga 11 (los de trigo, zanahoria y tomate).
    public class SellZoneTests
    {
        private const int PlotCount = 3;
        private const float GrowSeconds = 0.6f;
        private const int Capacity = 6;
        private const float Interval = 0.12f;
        private const float Flight = 0.25f;
        private static readonly int[] Prices = { 2, 5, 11 };
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);
        private static readonly Vector3 PadSpot = new Vector3(0f, 0f, -20f);

        private FieldTestWorld _world;
        private BackpackRig _rig;
        private SellRig _sell;
        private int _logs;
        private Application.LogCallback _logCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(PlotCount, GrowSeconds);
            _rig = BackpackRig.AddTo(_world, FarAway, Capacity, PlotCount);
            SellRig.SetPrices(_rig, Prices);

            for (int i = 0; i < PlotCount; i++)
            {
                TestReflection.Set(_world.Plots[i], "crop", _rig.Crops[i]);
            }

            _sell = SellRig.AddTo(_world, _rig.Config, PadSpot, Interval, Flight, withFeedback: true);

            _logCounter = (message, stackTrace, type) => _logs++;
            yield return null;   // el Start() del campo hace el Prewarm de plantas
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= _logCounter;
            _sell.Destroy();
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        private void StandOnThePad()
        {
            _rig.Farmer.transform.position = _sell.PadPosition + Vector3.up;
        }

        private void LeaveThePad()
        {
            _rig.Farmer.transform.position = FarAway;
        }

        private void MoveFarmerOnto(int plotIndex)
        {
            _rig.Farmer.transform.position = _world.Plots[plotIndex].transform.position + Vector3.up;
        }

        private void Load(params int[] crops)
        {
            foreach (int crop in crops)
            {
                Assert.IsTrue(_rig.Actor.TryAccept(_rig.Crops[crop], FarAway), "La mochila tendría que tener lugar.");
            }
        }

        private void MakeReady(PlotView plot)
        {
            plot.Interact(_rig.Actor);
            plot.Tick(GrowSeconds + 0.1f);
            Assert.AreEqual(PlotState.Ready, plot.State);
        }

        private static IEnumerator PhysicsSteps()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 4f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        // Que terminen los vuelos de la pila y los efectos del pad (duran menos de un segundo, con tiempo sin escala).
        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(1.3f);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private PoolStats ProductPool(int crop)
        {
            Assert.IsTrue(_world.Pools.TryGetStats(_rig.ProductTemplates[crop], out PoolStats stats), "No hay pool del producto " + crop);
            return stats;
        }

        // ---------- Vender ----------

        [UnityTest]
        public IEnumerator StandingOnThePad_SellsTopToBottom_AndTheWalletRisesByEachCropPrice()
        {
            int[] load = { 0, 1, 2, 0, 1 };   // de abajo hacia arriba
            Load(load);

            var sales = new List<(long delta, float time)>();
            _sell.Economy.Wallet.BalanceChanged += (balance, delta) => sales.Add((delta, Time.time));

            // Cada cultivo que sale de la mochila, en el orden en que sale.
            var order = new List<CropData>();
            _sell.Economy.ProductSold += (crop, coins) => order.Add(crop);

            StandOnThePad();
            yield return WaitFor(() => _rig.Backpack.Count == 0);
            Assert.AreEqual(0, _rig.Backpack.Count, "Se vació la mochila.");

            // De arriba hacia abajo (LIFO): el último que entró es el primero que se vende.
            var expected = new List<CropData>();
            for (int i = load.Length - 1; i >= 0; i--) expected.Add(_rig.Crops[load[i]]);
            CollectionAssert.AreEqual(expected, order);

            // La billetera sube por el precio de cada cultivo (mezcla incluida), una venta por vez.
            Assert.AreEqual(load.Length, sales.Count);
            long total = 0;
            for (int i = 0; i < load.Length; i++)
            {
                long price = Prices[load[load.Length - 1 - i]];
                Assert.AreEqual(price, sales[i].delta, "Precio de la venta " + i);
                total += price;
            }

            Assert.AreEqual(total, _sell.Economy.Balance);
            Assert.AreEqual(load.Length, _sell.Economy.SoldUnits);
            Assert.AreEqual(total, _sell.Economy.SoldCoins);

            // El ritmo: ni más rápido que el intervalo fijo, ni mucho más lento.
            float span = sales[sales.Count - 1].time - sales[0].time;
            Assert.GreaterOrEqual(span, (load.Length - 1) * Interval - 1e-3f, "Nunca más rápido que el intervalo.");
            Assert.LessOrEqual(span, (load.Length - 1) * Interval + 0.35f, "Y sin perder tiempo entre ventas.");
        }

        [UnityTest]
        public IEnumerator TheBackpackStackFollowsTheSale_TheProductFliesToThePadAndGoesBackToThePool()
        {
            Load(0, 1, 2);
            yield return new WaitForSecondsRealtime(0.5f);   // terminan los vuelos de entrada
            Assert.AreEqual(3, _rig.View.ItemCount);

            GameObject top = _rig.View.ItemObject(2);
            Assert.IsNotNull(top);

            // Lo que se ve justo al vender (se anota acá y se mira afuera: una falla dentro del evento no sería del test).
            bool checkedFlight = false;
            int backpackCount = -1, stackCount = -1, departing = -1;
            GameObject departingObject = null;
            bool topActive = false;
            _sell.Economy.ProductSold += (crop, coins) =>
            {
                if (checkedFlight) return;
                checkedFlight = true;

                backpackCount = _rig.Backpack.Count;
                stackCount = _rig.View.ItemCount;
                departing = _rig.View.DepartingCount;
                departingObject = departing > 0 ? _rig.View.DepartingObject(0) : null;
                topActive = top.activeInHierarchy;
            };

            StandOnThePad();
            yield return WaitFor(() => checkedFlight);
            Assert.IsTrue(checkedFlight, "No se vendió.");

            // Justo al vender: la pila ya coincide con la mochila (soltó su lugar) y el producto vuela aparte.
            Assert.AreEqual(2, backpackCount);
            Assert.AreEqual(2, stackCount, "La pila suelta el lugar en el acto.");
            Assert.AreEqual(1, departing, "El producto vendido vuela a la zona de venta.");
            Assert.AreSame(top, departingObject, "Es el mismo objeto que estaba arriba de la pila, no uno nuevo.");
            Assert.IsTrue(topActive);

            // A mitad del vuelo está entre la espalda y el pad; al llegar vuelve al pool.
            // (Se mide en el plano del suelo: el arco hace subir al producto a mitad de camino.)
            Vector3 target = _sell.Target.position;
            float startDistance = FlatDistance(top.transform.position, target);
            yield return new WaitForSecondsRealtime(Flight * 0.5f);
            if (_rig.View.DepartingCount > 0 && _rig.View.DepartingObject(0) == top)
            {
                Assert.Less(FlatDistance(top.transform.position, target), startDistance, "Se acerca al pad.");
            }

            yield return Settle();
            Assert.AreEqual(0, _rig.View.DepartingCount, "Todo lo que voló volvió al pool.");
            Assert.AreEqual(0, _rig.Backpack.Count);
            Assert.AreEqual(0, _rig.View.ItemCount);
            for (int i = 0; i < PlotCount; i++)
            {
                Assert.AreEqual(0, ProductPool(i).Active, "Los productos vuelven al pool: cultivo " + i);
            }
        }

        [UnityTest]
        public IEnumerator TheProductArrives_AtTheTarget_BeforeReturningToThePool()
        {
            Load(2);
            yield return new WaitForSecondsRealtime(0.5f);
            GameObject item = _rig.View.ItemObject(0);

            StandOnThePad();
            yield return WaitFor(() => _rig.View.DepartingCount > 0);
            Assert.AreEqual(1, _rig.View.DepartingCount);

            // Casi al final del vuelo está casi sobre el pad (y chico).
            float distance = float.MaxValue;
            float deadline = Time.realtimeSinceStartup + Flight * 2f;
            while (_rig.View.DepartingCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                distance = Mathf.Min(distance, Vector3.Distance(item.transform.position, _sell.Target.position));
                yield return null;
            }

            Assert.Less(distance, 0.6f, "El producto llega a la zona de venta antes de volver al pool.");
            Assert.AreEqual(0, _rig.View.DepartingCount);
        }

        [UnityTest]
        public IEnumerator WithAnEmptyBackpack_NothingHappens_AndTheConsoleStaysClean()
        {
            _logs = 0;
            Application.logMessageReceived += _logCounter;

            StandOnThePad();
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.4f);   // parado sobre el pad con la mochila vacía

            Application.logMessageReceived -= _logCounter;
            Assert.AreEqual(0, _logs, "Vender con la mochila vacía no tiene que escribir en la consola.");
            Assert.AreEqual(0, _sell.Economy.Balance);
            Assert.AreEqual(0, _sell.Economy.SoldUnits);
            Assert.AreEqual(0, _rig.View.DepartingCount);
        }

        [UnityTest]
        public IEnumerator TheFirstProductThatArrivesOnAnEmptyPad_SellsAtOnce_WithoutWaitingATurn()
        {
            StandOnThePad();
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.5f);   // un buen rato parado sin nada que vender

            float before = Time.time;
            Load(1);
            yield return WaitFor(() => _sell.Economy.SoldUnits == 1, 1f);

            Assert.AreEqual(1, _sell.Economy.SoldUnits);
            Assert.AreEqual(5L, _sell.Economy.Balance);
            Assert.Less(Time.time - before, 0.25f, "Salió en uno o dos frames, no esperó un intervalo entero.");
        }

        [UnityTest]
        public IEnumerator WhilePausedOrShowingAnAd_ItDoesNotSell_AndWhenItCanActItKeepsSelling()
        {
            Load(0, 1, 2, 1);
            StandOnThePad();
            yield return PhysicsSteps();
            yield return WaitFor(() => _sell.Economy.SoldUnits >= 1, 1f);   // la primera sale al instante
            Assert.GreaterOrEqual(_sell.Economy.SoldUnits, 1);

            // En pausa: congelado, aunque sigue parado sobre el pad (la física también está congelada: tiempo real).
            Assert.IsTrue(_world.Game.Pause());
            long frozenBalance = _sell.Economy.Balance;
            int frozenSold = _sell.Economy.SoldUnits;
            int frozenInBackpack = _rig.Backpack.Count;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(frozenSold, _sell.Economy.SoldUnits, "En pausa no vende.");
            Assert.AreEqual(frozenBalance, _sell.Economy.Balance);
            Assert.AreEqual(frozenInBackpack, _rig.Backpack.Count);
            Assert.Greater(frozenInBackpack, 0, "Quedaba algo por vender.");

            // Al volver, sigue sin tener que irse y volver.
            Assert.IsTrue(_world.Game.Resume());
            yield return WaitFor(() => _rig.Backpack.Count == 0);
            Assert.AreEqual(0, _rig.Backpack.Count, "Al reanudar sigue vendiendo.");
            Assert.AreEqual(4, _sell.Economy.SoldUnits);
            Assert.AreEqual(2L + 5L + 11L + 5L, _sell.Economy.Balance);

            // Lo mismo con un anuncio en pantalla.
            Load(2, 2, 0);
            yield return null;   // el jugador sigue sobre el pad: empieza a vender
            Assert.IsTrue(_world.Game.BeginAd());
            int soldAtAd = _sell.Economy.SoldUnits;
            int leftAtAd = _rig.Backpack.Count;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(soldAtAd, _sell.Economy.SoldUnits, "Con un anuncio no vende.");
            Assert.AreEqual(leftAtAd, _rig.Backpack.Count);

            Assert.IsTrue(_world.Game.EndAd());
            yield return WaitFor(() => _rig.Backpack.Count == 0);
            Assert.AreEqual(0, _rig.Backpack.Count);
            Assert.AreEqual(7, _sell.Economy.SoldUnits);
            Assert.AreEqual(2L + 5L + 11L + 5L + 2L + 11L + 11L, _sell.Economy.Balance);

            // Todo lo que voló (también en pausa y con el anuncio en medio) vuelve al pool.
            yield return Settle();
            Assert.AreEqual(0, _rig.View.DepartingCount);
            for (int i = 0; i < PlotCount; i++) Assert.AreEqual(0, ProductPool(i).Active);
        }

        [UnityTest]
        public IEnumerator LeavingThePad_StopsTheSale_AndComingBackResumesIt()
        {
            Load(0, 1, 2, 0, 1, 2);
            StandOnThePad();
            yield return WaitFor(() => _sell.Economy.SoldUnits >= 2);
            Assert.GreaterOrEqual(_sell.Economy.SoldUnits, 2);

            LeaveThePad();
            yield return PhysicsSteps();
            int soldWhenLeaving = _sell.Economy.SoldUnits;
            int inBackpack = _rig.Backpack.Count;
            Assert.Greater(inBackpack, 0, "Se fue a mitad de la venta: todavía quedaba mochila.");

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(soldWhenLeaving, _sell.Economy.SoldUnits, "Fuera del pad no se vende nada más.");
            Assert.AreEqual(inBackpack, _rig.Backpack.Count);

            // Lo que ya estaba volando termina su vuelo sin problema: la pila refleja la mochila.
            yield return Settle();
            Assert.AreEqual(0, _rig.View.DepartingCount);
            Assert.AreEqual(inBackpack, _rig.View.ItemCount);

            StandOnThePad();
            yield return WaitFor(() => _rig.Backpack.Count == 0);
            Assert.AreEqual(0, _rig.Backpack.Count, "Al volver sigue vendiendo.");
            Assert.AreEqual(6, _sell.Economy.SoldUnits);
            Assert.AreEqual(2L + 5L + 11L + 2L + 5L + 11L, _sell.Economy.Balance);
        }

        [UnityTest]
        public IEnumerator SellingFreesSpace_SoAReadyPlotHarvestsAgain_FullCycle()
        {
            // Mochila llena y una parcela lista: parado sobre la parcela no cosecha.
            Load(0, 1, 2, 0, 1, 2);
            Assert.IsTrue(_rig.Backpack.IsFull);
            MakeReady(_world.Plots[0]);

            MoveFarmerOnto(0);
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(PlotState.Ready, _world.Plots[0].State, "Llena: la parcela lista no cosecha.");
            Assert.AreEqual(Capacity, _rig.Backpack.Count);

            // Se vende todo en el pad.
            StandOnThePad();
            yield return WaitFor(() => _rig.Backpack.Count == 0);
            Assert.AreEqual(0, _rig.Backpack.Count);
            Assert.AreEqual(2 * (Prices[0] + Prices[1] + Prices[2]), _sell.Economy.Balance);

            // Con lugar de nuevo, la parcela lista vuelve a cosechar: cosechar -> llenar -> vender -> seguir cosechando.
            MoveFarmerOnto(0);
            yield return WaitFor(() => _rig.Backpack.Count == 1);
            Assert.AreEqual(1, _rig.Backpack.Count, "Cosecha otra vez apenas hay lugar.");
            Assert.AreSame(_rig.Crops[0], _rig.Backpack.Items[0]);

            // Se va de la parcela (que se resiembra y vuelve a madurar enseguida) y la pila coincide con la mochila.
            LeaveThePad();
            yield return PhysicsSteps();
            int inBackpack = _rig.Backpack.Count;
            yield return Settle();
            Assert.AreEqual(inBackpack, _rig.View.ItemCount);
            Assert.AreEqual(0, _rig.View.DepartingCount);
        }

        // ---------- Sin Instantiate ----------

        [UnityTest]
        public IEnumerator SellingAFullBackpack_Repeatedly_NeverInstantiates_NorMissesThePools()
        {
            int awakes = AwakeCounter.Awakes;

            StandOnThePad();
            yield return PhysicsSteps();
            for (int round = 0; round < 4; round++)
            {
                for (int i = 0; i < Capacity; i++) Load((i + round) % PlotCount);
                Assert.IsTrue(_rig.Backpack.IsFull);

                yield return WaitFor(() => _rig.Backpack.Count == 0);
                Assert.AreEqual(0, _rig.Backpack.Count, "Ronda " + round);
            }

            yield return Settle();

            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Hubo Instantiate durante la venta.");
            for (int i = 0; i < PlotCount; i++)
            {
                PoolStats stats = ProductPool(i);
                Assert.AreEqual(_rig.ProductPrewarm, stats.Created, "No se crearon productos nuevos: cultivo " + i);
                Assert.AreEqual(0, stats.Misses, "El Prewarm alcanzó: cultivo " + i);
                Assert.AreEqual(0, stats.Active);
            }

            Assert.IsTrue(_world.Pools.TryGetStats(_sell.CoinTemplate, out PoolStats coins));
            Assert.AreEqual(0, coins.Misses, "Las monedas salen del Prewarm.");
            Assert.AreEqual(0, coins.Active, "Las monedas vuelven al pool.");

            // Cada ronda lleva dos unidades de cada cultivo: 2 x (2 + 5 + 11) = 36, cuatro veces.
            Assert.AreEqual(24, _sell.Economy.SoldUnits);
            Assert.AreEqual(4L * 2L * (Prices[0] + Prices[1] + Prices[2]), _sell.Economy.Balance);
        }

        // ---------- Lo cosmético ----------

        [UnityTest]
        public IEnumerator WhenTheProductsArrive_ACoinPopsAndThePlusNAddsUp_ThenEverythingGoesBack()
        {
            Load(2, 1, 2);   // 11 + 5 + 11 = 27, de arriba hacia abajo: 11, 5, 11
            StandOnThePad();

            long peak = 0;
            int maxCoins = 0;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                peak = Math.Max(peak, _sell.Feedback.PopupAmount);
                maxCoins = Math.Max(maxCoins, _sell.Feedback.ActiveCoins);
                if (_rig.Backpack.Count == 0 && _sell.Feedback.PopupAmount == 0 && peak > 0) break;
                yield return null;
            }

            Assert.AreEqual(27L, peak, "El \"+N\" suma lo ganado mientras se sigue vendiendo.");
            Assert.GreaterOrEqual(maxCoins, 1, "Apareció al menos una moneda.");
            Assert.AreEqual(0L, _sell.Feedback.PopupAmount, "El \"+N\" se apaga solo.");
            Assert.IsFalse(_sell.Feedback.PopupLabel.enabled);
            Assert.AreEqual(0, _sell.Feedback.ActiveCoins);
            Assert.AreEqual(0, _sell.Feedback.PendingArrivals);
        }

        [UnityTest]
        public IEnumerator TheEffects_AppearOnlyWhenTheProductArrives_NotWhenItLeaves()
        {
            Load(1);
            StandOnThePad();
            yield return WaitFor(() => _sell.Economy.SoldUnits == 1, 1f);

            // La venta ya está hecha (la billetera tiene las monedas), pero todavía no llegó nada a la zona.
            Assert.AreEqual(5L, _sell.Economy.Balance);
            Assert.AreEqual(1, _sell.Feedback.PendingArrivals, "La moneda está en camino.");
            Assert.AreEqual(0L, _sell.Feedback.PopupAmount);

            yield return new WaitForSecondsRealtime(Flight + 0.1f);
            Assert.AreEqual(5L, _sell.Feedback.PopupAmount, "Al llegar aparece el \"+N\".");
            Assert.AreEqual(0, _sell.Feedback.PendingArrivals);
        }

        // ---------- La zona solo conoce interfaces ----------

        [UnityTest]
        public IEnumerator TheZoneOnlyNeedsAnIBackpackDispenser_NotThePlayer()
        {
            FakeFarmer farmer = FakeFarmer.Create(FarAway, 4);
            farmer.Backpack.TryAdd(_rig.Crops[0]);
            farmer.Backpack.TryAdd(_rig.Crops[2]);

            farmer.transform.position = _sell.PadPosition + Vector3.up;
            yield return WaitFor(() => farmer.Backpack.Count == 0);

            Assert.AreEqual(2, farmer.Dispenses);
            Assert.AreEqual(0, farmer.Backpack.Count);
            Assert.AreEqual(13L, _sell.Economy.Balance, "11 (arriba) + 2.");
            Assert.AreEqual(0f, Vector3.Distance(_sell.Target.position, farmer.LastTo), 1e-4f, "El actor sabe adónde va el producto.");
            Assert.AreEqual(Flight, farmer.LastFlightSeconds, 1e-5f, "Y cuánto tarda en llegar.");

            Object.DestroyImmediate(farmer.gameObject);
        }

        [UnityTest]
        public IEnumerator AnActorWithoutABackpack_DoesNothingOnThePad()
        {
            FakeActor actor = FakeActor.Create(FarAway);

            _logs = 0;
            Application.logMessageReceived += _logCounter;
            actor.transform.position = _sell.PadPosition + Vector3.up;
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.3f);
            Application.logMessageReceived -= _logCounter;

            Assert.AreEqual(0, _logs);
            Assert.AreEqual(0L, _sell.Economy.Balance);

            Object.DestroyImmediate(actor.gameObject);
        }

        [UnityTest]
        public IEnumerator AnActorThatCannotActRightNow_IsNotServed_UntilItCan()
        {
            FakeFarmer farmer = FakeFarmer.Create(FarAway, 4);
            farmer.Backpack.TryAdd(_rig.Crops[1]);
            farmer.CanAct = false;

            farmer.transform.position = _sell.PadPosition + Vector3.up;
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, farmer.DispenseCalls, "Ni siquiera se le pide el producto.");
            Assert.AreEqual(1, farmer.Backpack.Count);

            farmer.CanAct = true;
            yield return WaitFor(() => farmer.Backpack.Count == 0);
            Assert.AreEqual(5L, _sell.Economy.Balance);

            Object.DestroyImmediate(farmer.gameObject);
        }

        // ---------- Precio ----------

        [UnityTest]
        public IEnumerator TheEconomyPaysTheFinalPrice_UpgradesAndBuffsMultiplyTheBasePrice()
        {
            _sell.Economy.Pricing.SetUpgradeMultiplier(1.5f);
            _sell.Economy.Pricing.SetBuffMultiplier(2f);

            Load(1);   // base 5 -> 5 x 1,5 x 2 = 15
            StandOnThePad();
            yield return WaitFor(() => _rig.Backpack.Count == 0);

            Assert.AreEqual(15L, _sell.Economy.Balance);
            Assert.AreEqual(15L, _sell.Economy.SoldCoins);
        }

        [UnityTest]
        public IEnumerator AZoneWithoutConfig_DoesNotSell_AndComplainsOnlyOnce()
        {
            TestReflection.Set(_sell.Zone, "config", null);
            TestReflection.Set(_sell.Zone, "_pacer", null);

            Load(1);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no puede vender: falta el GameConfig"));
            StandOnThePad();
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.3f);   // muchos frames: un solo error

            Assert.AreEqual(0, _sell.Economy.SoldUnits);
            Assert.AreEqual(1, _rig.Backpack.Count, "No se pierde el producto.");
        }
    }
}
