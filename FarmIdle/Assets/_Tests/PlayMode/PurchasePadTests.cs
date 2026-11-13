using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Los pads de compra con un mundo armado por código y servicios reales (EconomyManager, ProgressionManager, GameManager en
    // Playing, PoolManager): un portón de zona con su ZoneController (y un campo apagado de 3 parcelas) y un pad de mejora.
    // El actor es un FakeActor al que se "mueve" encima del pad, con física real, igual que en SellZoneTests.
    // Zona de prueba: cuesta 300. Mejora de precio: un solo nivel por 150 (x1,5). El pago tarda 0,8 s con 0,1 s de espera
    // (ProgressionRig), así que cada prueba espera unos segundos de tiempo real.
    public class PurchasePadTests
    {
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);
        private static readonly Vector3 GateSpot = new Vector3(0f, 0f, -20f);
        private static readonly Vector3 UpgradeSpot = new Vector3(12f, 0f, -20f);
        private const int ZonePlots = 3;

        private FieldTestWorld _world;
        private ProgressionRig _rig;
        private UpgradeData _price;
        private FakeActor _actor;
        private GameObject _gate;
        private ZoneUnlockPad _gatePad;
        private GameObject _upgradeObject;
        private UpgradePad _upgradePad;
        private GameObject _zoneObject;
        private GameObject _locked;
        private GameObject _unlocked;
        private ZoneController _zoneController;
        private PlotView[] _zonePlots;
        private int _logs;
        private Application.LogCallback _logCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(2, 4f);
            _price = TestReflection.CreateUpgrade("price", UpgradeKind.SellPrice, (150L, 1.5f), (300L, 2f));
            _rig = ProgressionRig.AddTo(_world, 0, 10, new long[] { 300 }, _price);

            // Una zona cerrada: sus dos mitades y el controlador que las alterna.
            _unlocked = _world.AddZoneField(ZonePlots, 20f, out _zonePlots, out _);
            _zoneObject = new GameObject("Zone");
            _zoneObject.SetActive(false);
            _zoneObject.transform.SetParent(_world.Root.transform, false);
            _locked = new GameObject("Locked");
            _locked.transform.SetParent(_zoneObject.transform, false);
            _zoneController = _zoneObject.AddComponent<ZoneController>();
            TestReflection.Set(_zoneController, "zone", _rig.Zones[0]);
            TestReflection.Set(_zoneController, "lockedRoot", _locked);
            TestReflection.Set(_zoneController, "unlockedRoot", _unlocked);

            _gate = CreatePadObject("Gate", GateSpot, out TMP_Text gateLabel);
            _gatePad = _gate.AddComponent<ZoneUnlockPad>();
            _gate.AddComponent<InteractionTrigger>();
            TestReflection.Set(_gatePad, "config", _rig.Config);
            TestReflection.Set(_gatePad, "label", gateLabel);
            TestReflection.Set(_gatePad, "zone", _rig.Zones[0]);

            _upgradeObject = CreatePadObject("UpgradePad", UpgradeSpot, out TMP_Text upgradeLabel);
            _upgradePad = _upgradeObject.AddComponent<UpgradePad>();
            _upgradeObject.AddComponent<InteractionTrigger>();
            TestReflection.Set(_upgradePad, "config", _rig.Config);
            TestReflection.Set(_upgradePad, "label", upgradeLabel);
            TestReflection.Set(_upgradePad, "upgrade", _price);

            _actor = FakeActor.Create(FarAway);

            _logCounter = (message, stackTrace, type) => _logs++;
            _zoneObject.SetActive(true);
            _gate.SetActive(true);
            _upgradeObject.SetActive(true);
            yield return null;   // los Start() hacen el Prewarm de plantas y dejan armados los pads
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= _logCounter;
            if (_actor != null) UnityEngine.Object.DestroyImmediate(_actor.gameObject);
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        // Un pad en el suelo (apagado hasta que se terminen de cablear sus referencias) con trigger de 2,2 x 2 x 2,2 y un cartel.
        private GameObject CreatePadObject(string name, Vector3 position, out TMP_Text label)
        {
            var pad = new GameObject(name);
            pad.SetActive(false);
            pad.transform.SetParent(_world.Root.transform, false);
            pad.transform.position = position;

            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(2.2f, 2f, 2.2f);

            var labelObject = new GameObject("Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(pad.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 3f, 0f);
            label = labelObject.GetComponent<TMP_Text>();
            return pad;
        }

        private void StandOn(GameObject pad)
        {
            _actor.transform.position = pad.transform.position + Vector3.up;
        }

        private void Leave()
        {
            _actor.transform.position = FarAway;
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

        private ZoneData Zone => _rig.Zones[0];

        [UnityTest]
        public IEnumerator TheGate_ShowsTheZoneAndItsCost_AndTheZoneStartsClosed()
        {
            StringAssert.Contains("zone-0", _gatePad.Label.text);
            StringAssert.Contains("300", _gatePad.Label.text);
            Assert.IsTrue(_locked.activeSelf, "Cerrada: se ve el terreno vallado.");
            Assert.IsFalse(_unlocked.activeSelf, "Cerrada: el campo no está.");
            Assert.AreEqual(2, _world.Census.Total, "Las parcelas de la zona cerrada no cuentan en el censo.");
            yield break;
        }

        [UnityTest]
        public IEnumerator StandingOnTheGate_WithoutMoney_BuysNothing()
        {
            StandOn(_gate);
            yield return PhysicsSteps();
            yield return new WaitForSecondsRealtime(1.3f);

            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(Zone));
            Assert.AreEqual(0L, _rig.Economy.Balance);
            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone));
            Assert.IsTrue(_locked.activeSelf);
            Assert.IsFalse(_unlocked.activeSelf);
        }

        [UnityTest]
        public IEnumerator StandingOnTheGate_WithEnoughMoney_DripsThenUnlocks_ChargingExactlyTheCost()
        {
            _rig.Economy.AddCoins(350);
            StandOn(_gate);
            yield return PhysicsSteps();

            // A mitad del pago: ya salió plata de la billetera hacia la compra, pero todavía no alcanza.
            yield return WaitFor(() => _rig.Progression.PaidFor(Zone) > 60L, 2f);
            long paid = _rig.Progression.PaidFor(Zone);
            Assert.Greater(paid, 0L, "El pago gotea: se va juntando de a poco.");
            Assert.Less(paid, 300L);
            Assert.AreEqual(350L - paid, _rig.Economy.Balance, "Cada moneda que pasa a la compra sale de la billetera en el mismo instante.");
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(Zone));

            yield return WaitFor(() => _rig.Progression.IsZoneUnlocked(Zone), 3f);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(Zone), "Al llegar al costo la zona se abre.");
            Assert.AreEqual(50L, _rig.Economy.Balance, "Se cobró el costo, ni una moneda más.");
            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone));

            // Se ve: el campo aparece y el terreno cerrado se va (con el efecto, que termina en menos de un segundo).
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(_unlocked.activeSelf);
            Assert.IsFalse(_locked.activeSelf);
            Assert.AreEqual(Vector3.one, _unlocked.transform.localScale, "El efecto de apertura termina en el tamaño de siempre.");
            Assert.IsFalse(_zoneController.IsRevealing);
            Assert.IsFalse(_gate.activeSelf, "El portón desaparece cuando ya no hace falta.");
            Assert.AreEqual(2 + ZonePlots, _world.Census.Total, "Las parcelas nuevas se anotan en el censo al aparecer.");
        }

        [UnityTest]
        public IEnumerator LeavingTheGate_StopsThePayment_AndKeepsWhatWasPaid()
        {
            _rig.Economy.AddCoins(1000);
            StandOn(_gate);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.PaidFor(Zone) > 30L, 2f);

            Assert.IsTrue(_gatePad.Drip.IsActive, "Parado en el pad hay una visita en curso.");

            Leave();
            yield return PhysicsSteps();
            Assert.IsFalse(_gatePad.Drip.IsActive, "Al irse, el InteractionTrigger avisa (IInteractableExit) y el pad corta la visita.");
            long paid = _rig.Progression.PaidFor(Zone);
            long balance = _rig.Economy.Balance;
            Assert.Greater(paid, 0L);
            Assert.Less(paid, 300L, "Se fue antes de terminar de pagar.");

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(paid, _rig.Progression.PaidFor(Zone), "Sin el jugador encima el pago no avanza.");
            Assert.AreEqual(balance, _rig.Economy.Balance);
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(Zone));

            // Al volver sigue desde donde quedó: la zona termina costando 300 en total.
            StandOn(_gate);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.IsZoneUnlocked(Zone), 3f);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(Zone));
            Assert.AreEqual(700L, _rig.Economy.Balance, "Lo pagado antes cuenta: no se paga dos veces.");
        }

        [UnityTest]
        public IEnumerator WithoutEnoughMoney_ItDrainsWhatThereIs_AndResumesWhenComingBackWithMore()
        {
            _rig.Economy.AddCoins(100);
            StandOn(_gate);
            yield return PhysicsSteps();
            yield return new WaitForSecondsRealtime(1.5f);

            Assert.AreEqual(0L, _rig.Economy.Balance, "Gotea todo lo que tiene.");
            Assert.AreEqual(100L, _rig.Progression.PaidFor(Zone));
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(Zone));

            Leave();
            yield return PhysicsSteps();
            _rig.Economy.AddCoins(500);

            StandOn(_gate);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.IsZoneUnlocked(Zone), 3f);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(Zone));
            Assert.AreEqual(300L, _rig.Economy.Balance, "500 - (300 - 100 que ya estaban pagados).");
        }

        [UnityTest]
        public IEnumerator WhenTheActorCannotAct_OrTheGameIsPaused_OrAnAdIsShowing_NothingIsPaid()
        {
            _rig.Economy.AddCoins(1000);

            // 1) El actor no puede actuar (el jugador fuera de Playing): el pad ni lo atiende.
            _actor.CanAct = false;
            StandOn(_gate);
            yield return PhysicsSteps();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(1000L, _rig.Economy.Balance);
            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone));

            // 2) Pausa real (el jugador "podría" actuar): el tiempo del juego no corre y ProgressionManager no cobra.
            _actor.CanAct = true;
            Assert.IsTrue(_world.Game.Pause());
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(1000L, _rig.Economy.Balance, "En pausa no se paga.");
            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone));
            Assert.IsTrue(_world.Game.Resume());

            // 3) Anuncio en pantalla.
            Assert.IsTrue(_world.Game.BeginAd());
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(1000L, _rig.Economy.Balance, "Con un anuncio en pantalla tampoco.");
            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone));
            Assert.IsTrue(_world.Game.EndAd());

            // 4) De vuelta en Playing, parado en el mismo lugar, se paga.
            yield return WaitFor(() => _rig.Progression.IsZoneUnlocked(Zone), 3f);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(Zone));
            Assert.AreEqual(700L, _rig.Economy.Balance);
        }

        [UnityTest]
        public IEnumerator OpeningAZone_ActivatesObjectsThatAlreadyExisted_NeverInstantiating_AndThePoolWasSizedForTheWholeLevel()
        {
            // El primer campo en arrancar dejó prewarmeadas las plantas de TODO el nivel: sus 2 parcelas y las 3 de la zona cerrada.
            int expected = 2 + ZonePlots;
            Assert.IsTrue(_world.Pools.TryGetStats(_world.Crop.PlantPrefab, out PoolStats before));
            Assert.AreEqual(expected, before.Created, "Una planta por parcela del nivel entero, aunque la zona esté cerrada.");
            Assert.AreEqual(expected, AwakeCounter.Awakes, "Esos Awakes son todos los Instantiate que hubo.");

            _rig.Economy.AddCoins(300);
            Assert.AreEqual(300L, _rig.Progression.TryPay(Zone, 300));
            yield return null;
            yield return null;   // el Start() del campo de la zona repite el Prewarm

            Assert.IsTrue(_unlocked.activeSelf);
            Assert.AreEqual(expected, AwakeCounter.Awakes, "Abrir la zona no hizo ningún Instantiate.");
            Assert.IsTrue(_world.Pools.TryGetStats(_world.Crop.PlantPrefab, out PoolStats after));
            Assert.AreEqual(expected, after.Created);
            Assert.AreEqual(0, after.Misses);

            // Y las parcelas nuevas funcionan: siembran con plantas del pool.
            Assert.IsTrue(_zonePlots[0].TrySow());
            Assert.IsNotNull(_zonePlots[0].Plant, "La planta de la zona sale del pool.");
            Assert.AreEqual(expected, AwakeCounter.Awakes);
            Assert.AreEqual(0, _logs, "Sin errores ni advertencias.");
        }

        [UnityTest]
        public IEnumerator TheUpgradePad_BuysOneLevelPerVisit_ThenShowsMax_AndStopsCharging()
        {
            StringAssert.Contains("price", _upgradePad.Label.text);
            StringAssert.Contains("150", _upgradePad.Label.text);

            _rig.Economy.AddCoins(1000);
            StandOn(_upgradeObject);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.LevelOf(_price) == 1, 3f);

            Assert.AreEqual(1, _rig.Progression.LevelOf(_price));
            Assert.AreEqual(850L, _rig.Economy.Balance);
            Assert.AreEqual(1.5f, _rig.Economy.Pricing.UpgradeMultiplier, 1e-5f);
            StringAssert.Contains("300", _upgradePad.Label.text, "El cartel ya muestra el costo del nivel siguiente.");
            Assert.IsTrue(_upgradePad.WaitingForExit);

            // Quedarse parado no encadena el nivel siguiente ni vacía la billetera: hay que bajarse y volver a subir.
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(1, _rig.Progression.LevelOf(_price), "Una compra por visita.");
            Assert.AreEqual(850L, _rig.Economy.Balance);
            Assert.AreEqual(0L, _rig.Progression.PaidFor(_price));

            Leave();
            yield return PhysicsSteps();
            Assert.IsFalse(_upgradePad.WaitingForExit, "Al irse, el pad vuelve a estar listo.");

            StandOn(_upgradeObject);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.LevelOf(_price) == 2, 3f);
            Assert.AreEqual(2, _rig.Progression.LevelOf(_price));
            Assert.AreEqual(550L, _rig.Economy.Balance);
            Assert.AreEqual(2f, _rig.Economy.Pricing.UpgradeMultiplier, 1e-5f);
            StringAssert.Contains("MAX", _upgradePad.Label.text);

            Leave();
            yield return PhysicsSteps();
            StandOn(_upgradeObject);
            yield return PhysicsSteps();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(550L, _rig.Economy.Balance, "Al máximo, el pad no cobra más.");
            Assert.IsTrue(_upgradeObject.activeSelf, "El pad de una mejora se queda (dice MAX).");
        }

        [UnityTest]
        public IEnumerator PadsDoNotChargeAnyoneButTheOneStandingOnThem()
        {
            _rig.Economy.AddCoins(1000);
            StandOn(_upgradeObject);
            yield return PhysicsSteps();
            yield return WaitFor(() => _rig.Progression.LevelOf(_price) == 1, 3f);

            Assert.AreEqual(0L, _rig.Progression.PaidFor(Zone), "La zona no recibió nada: el jugador está en otro pad.");
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(Zone));
            Assert.AreEqual(850L, _rig.Economy.Balance);
        }
    }
}
