using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Un IAnalytics que anota lo que le llega: "nombre" o "nombre clave=valor".
    internal sealed class RecordingAnalytics : IAnalytics
    {
        public readonly List<string> Events = new List<string>();

        public string Name => "Recording";

        public void Log(string eventName) { Events.Add(eventName); }

        public void Log(string eventName, string key, string value) { Events.Add(eventName + " " + key + "=" + value); }

        public void Log(string eventName, string key, long value) { Events.Add(eventName + " " + key + "=" + value); }
    }

    // Analítica (N-01) con servicios reales armados por código: las compras, los niveles y la sesión.
    public class AnalyticsProgressTests
    {
        private FieldTestWorld _world;
        private SaveRig _rig;
        private RecordingAnalytics _log;
        private AnalyticsManager _analytics;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = SaveRig.Create(_world);
            _rig.Start();
            _log = new RecordingAnalytics();

            var go = new GameObject("AnalyticsManager");
            go.transform.SetParent(_world.Root.transform, false);
            _analytics = go.AddComponent<AnalyticsManager>();
            _analytics.Initialize(_log);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TheSession_StartsWhenTheGameIsPlaying_AndTheServiceIsTheOneGiven()
        {
            Assert.AreSame(_log, _analytics.Service);
            CollectionAssert.AreEqual(new[] { "sesion_inicio" }, _log.Events, "El mundo de prueba ya está en Playing.");
            Assert.IsTrue(_analytics.SessionOpen);
            yield break;
        }

        [UnityTest]
        public IEnumerator Buying_EmitsPadBought_OnlyWhenThePurchaseIsComplete_AndRemembersTheLastPad()
        {
            _log.Events.Clear();
            _rig.Economy.AddCoins(2000);

            _rig.Progression.TryPay(_rig.Zones[1], 100);   // a medias: todavía no es una compra
            CollectionAssert.IsEmpty(_log.Events);
            Assert.AreEqual(AnalyticsManager.NoPad, _analytics.LastPad);

            _rig.Progression.TryPay(_rig.Zones[0], 300);
            _rig.Progression.TryPay(_rig.Price, 150);

            CollectionAssert.AreEqual(new[] { "pad_comprado id=zone-a", "pad_comprado id=price" }, _log.Events);
            Assert.AreEqual("price", _analytics.LastPad);
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelingUp_EmitsTheNewLevel_OncePerLevel()
        {
            _log.Events.Clear();
            for (int i = 0; i < 13; i++) _rig.Economy.SellProduct(_rig.Tomato);   // 130 de XP: nivel 2 (125)
            CollectionAssert.AreEqual(new[] { "nivel_subido nivel=2" }, _log.Events);

            _rig.Economy.SellProduct(_rig.Tomato);
            Assert.AreEqual(1, _log.Events.Count, "Dentro del mismo nivel no vuelve a emitir.");
            yield break;
        }

        [UnityTest]
        public IEnumerator TheSession_EndsWithDurationAndLastPad_WhenTheAppIsPaused_AndStartsAgainOnResume()
        {
            _rig.Economy.AddCoins(500);
            _rig.Progression.TryPay(_rig.Zones[0], 300);
            _log.Events.Clear();

            _analytics.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(2, _log.Events.Count);
            StringAssert.StartsWith("sesion_fin duracion=", _log.Events[0]);
            Assert.GreaterOrEqual(long.Parse(_log.Events[0].Substring("sesion_fin duracion=".Length)), 0L);
            Assert.AreEqual("abandono ultimo_pad=zone-a", _log.Events[1]);
            Assert.IsFalse(_analytics.SessionOpen);

            _analytics.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(2, _log.Events.Count, "Una sesión cerrada no se cierra dos veces.");

            _analytics.SendMessage("OnApplicationPause", false);
            Assert.AreEqual("sesion_inicio", _log.Events[2]);
            Assert.IsTrue(_analytics.SessionOpen);
            yield break;
        }

        [UnityTest]
        public IEnumerator PausingTheGame_DoesNotEndTheSession()
        {
            _log.Events.Clear();
            _world.Game.Pause();
            _world.Game.Resume();

            CollectionAssert.IsEmpty(_log.Events, "La pausa del juego no es una sesión nueva.");
            Assert.IsTrue(_analytics.SessionOpen);
            yield break;
        }

        [UnityTest]
        public IEnumerator OtherSystems_CanLogTheirOwnEvents_ThroughTheManager()
        {
            _analytics.EndSession();
            _log.Events.Clear();

            _analytics.Log(AnalyticsEvents.Prestige);
            _analytics.Log(AnalyticsEvents.OfflineClaimed, AnalyticsEvents.DoubledKey, 1L);
            _analytics.Log(AnalyticsEvents.MissionCompleted, AnalyticsEvents.IdKey, "cosechar-50");

            CollectionAssert.AreEqual(new[] { "prestigio", "offline_reclamado duplicado=1", "mision_completada id=cosechar-50" }, _log.Events);
            yield break;
        }
    }

    // Analítica con anuncios reales (el simulado): pedir, ver y cobrar el Turbo.
    public class AnalyticsAdsTests
    {
        private FieldTestWorld _world;
        private AdsRig _rig;
        private RecordingAnalytics _log;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = AdsRig.Create(_world);
            _log = new RecordingAnalytics();

            var go = new GameObject("AnalyticsManager");
            go.transform.SetParent(_world.Root.transform, false);
            go.AddComponent<AnalyticsManager>().Initialize(_log);
            _log.Events.Clear();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        private IEnumerator WaitForAdToEnd()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_world.Game.State != GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator ATurboVideo_EmitsAccepted_TurboUsed_AndCompleted_InThatOrder()
        {
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Turbo));
            yield return WaitForAdToEnd();

            CollectionAssert.AreEqual(new[] { "rewarded_aceptado lugar=turbo", "turbo_usado", "rewarded_completado lugar=turbo" }, _log.Events);
        }

        [UnityTest]
        public IEnumerator AClosedVideo_IsAcceptedButNeverCompleted()
        {
            _rig.Mock.NextResult = AdResult.Closed;
            Assert.AreEqual(AdRequestStatus.Started, _rig.Ads.TryRequest(_rig.Fert));
            yield return WaitForAdToEnd();

            CollectionAssert.AreEqual(new[] { "rewarded_aceptado lugar=fert" }, _log.Events);
        }

        [UnityTest]
        public IEnumerator ARejectedRequest_EmitsNothing()
        {
            _rig.Mock.Available = false;
            Assert.AreEqual(AdRequestStatus.NoAd, _rig.Ads.TryRequest(_rig.Fert));

            CollectionAssert.IsEmpty(_log.Events);
            yield break;
        }
    }

    // N-06: pedidos, sumideros (decoración, ayudante, skin), tienda de semillas y cosecha rara, con los servicios mínimos (sin escena).
    public class AnalyticsNewSystemsTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameConfig _config;
        private CropData _crop;
        private EconomyManager _economy;
        private BackpackManager _backpack;
        private PrestigeManager _prestige;
        private OrdersManager _orders;
        private DecorationManager _decorations;
        private HelperManager _helpers;
        private SkinManager _skins;
        private RecordingAnalytics _log;

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private T Data<T>(string id, int cost) where T : GameDataAsset
        {
            T data = ScriptableObject.CreateInstance<T>();
            data.name = id;
            TestReflection.Set(data, "id", id);
            TestReflection.Set(data, "displayName", id);
            TestReflection.Set(data, "cost", cost);
            _objects.Add(data);
            return data;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _crop = ScriptableObject.CreateInstance<CropData>();
            _crop.name = "crop_a";
            TestReflection.Set(_crop, "id", "crop_a");
            TestReflection.Set(_crop, "displayName", "crop_a");
            TestReflection.Set(_crop, "sellPrice", 10);
            _objects.Add(_crop);

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _objects.Add(_config);
            TestReflection.Set(_config, "crops", new[] { _crop });
            TestReflection.Set(_config, "ordersMinZones", 0);
            TestReflection.Set(_config, "orderCrops", new[] { new OrderCropRule("crop_a", "", 1) });
            TestReflection.Set(_config, "decorations", new[] { Data<DecorationData>("deco_a", 100), Data<DecorationData>("deco_b", 200) });
            TestReflection.Set(_config, "helpers", new[] { Data<HelperData>("helper_a", 100) });
            TestReflection.Set(_config, "skins", new[] { Data<SkinData>("skin_a", 100) });

            _economy = Create<EconomyManager>();
            _economy.Initialize(_config);
            _backpack = Create<BackpackManager>();
            _backpack.Initialize(_config);
            _prestige = Create<PrestigeManager>();
            _prestige.Initialize(_config);
            _orders = Create<OrdersManager>();
            _orders.Initialize(_config);
            _orders.Roll = max => 0;
            _decorations = Create<DecorationManager>();
            _decorations.Initialize(_config);
            _helpers = Create<HelperManager>();
            _helpers.Initialize(_config);
            _skins = Create<SkinManager>();
            _skins.Initialize(_config);

            _log = new RecordingAnalytics();
            Create<AnalyticsManager>().Initialize(_log);
            _log.Events.Clear();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _objects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator AnOrder_EmitsOnlyWhenItIsCompleted_NotOnAPartialDelivery()
        {
            _orders.Tick(0.1f);
            int wanted = _orders.QuantityOf(0);
            Assert.Greater(wanted, 1);

            _backpack.TryAdd(_crop);
            Assert.AreEqual(1, _orders.TryDeliver(0));
            CollectionAssert.IsEmpty(_log.Events, "Una entrega a medias no es un pedido cumplido.");

            for (int i = 1; i < wanted; i++) _backpack.TryAdd(_crop);
            Assert.AreEqual(wanted - 1, _orders.TryDeliver(0));

            CollectionAssert.AreEqual(new[] { "pedido_completado grande=0" }, _log.Events);
            yield break;
        }

        [UnityTest]
        public IEnumerator TheSinks_EmitWhatWasBought_AndARefusedPurchaseEmitsNothing()
        {
            Assert.IsFalse(_decorations.TryBuyNext(), "Sin monedas no se compra.");
            CollectionAssert.IsEmpty(_log.Events);

            _economy.AddCoins(1000);
            Assert.IsTrue(_decorations.TryBuyNext());
            Assert.IsTrue(_decorations.TryBuyNext());
            Assert.IsTrue(_helpers.TryBuyNext());
            Assert.IsTrue(_skins.TryBuyNext());

            CollectionAssert.AreEqual(
                new[] { "decoracion_comprada id=deco_a", "decoracion_comprada id=deco_b", "ayudante_comprado nivel=1", "skin_comprada id=skin_a" },
                _log.Events);
            yield break;
        }

        [UnityTest]
        public IEnumerator TheSeedShop_EmitsTheUpgradeBought_AndGrantingSeedsEmitsNothing()
        {
            _prestige.GrantSeeds(5);
            CollectionAssert.IsEmpty(_log.Events);

            Assert.IsTrue(_prestige.TryBuyShop(0));

            CollectionAssert.AreEqual(new[] { "mejora_semillas id=" + _prestige.ShopItem(0).Id }, _log.Events);
            yield break;
        }

        [UnityTest]
        public IEnumerator ARareHarvest_EmitsTheCrop_AndACommonOneEmitsNothing()
        {
            _economy.RareRoll = () => 0.99f;
            Assert.AreEqual(0L, _economy.TryRareHarvest(_crop));
            CollectionAssert.IsEmpty(_log.Events);

            _economy.RareRoll = () => 0f;
            Assert.Greater(_economy.TryRareHarvest(_crop), 0L);

            CollectionAssert.AreEqual(new[] { "cosecha_rara id=crop_a" }, _log.Events);
            yield break;
        }

        [UnityTest]
        public IEnumerator RestoringAGame_EmitsNoPurchases()
        {
            _economy.AddCoins(1000);
            _decorations.TryBuyNext();
            _helpers.TryBuyNext();
            _skins.TryBuyNext();
            _prestige.GrantSeeds(5);
            _prestige.TryBuyShop(0);
            string decorations = _decorations.Capture(), helpers = _helpers.Capture(), skins = _skins.Capture(), prestige = _prestige.Capture();
            _log.Events.Clear();

            _decorations.Restore(decorations);
            _helpers.Restore(helpers);
            _skins.Restore(skins);
            _prestige.Restore(prestige);

            CollectionAssert.IsEmpty(_log.Events, "Cargar la partida no es comprar.");
            yield break;
        }
    }

    // El juego completo: el Bootstrapper crea el AnalyticsManager y la sesión arranca con el "Tap to Play".
    public class AnalyticsBootTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTest]
        public IEnumerator TheBootstrapper_CreatesTheAnalytics_AndTheSessionStartsWhenThePlayerPlays()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            AnalyticsManager analytics = AnalyticsManager.Instance;
            Assert.IsNotNull(analytics);
            Assert.AreEqual("Console", analytics.Service.Name, "En el Editor la analítica escribe en el log.");
            Assert.IsFalse(analytics.SessionOpen, "Antes del Tap to Play no hay sesión.");

            var log = new RecordingAnalytics();
            analytics.SetService(log);
            GameManager.Instance.StartPlaying();

            Assert.IsTrue(analytics.SessionOpen);
            CollectionAssert.Contains(log.Events, "sesion_inicio");
        }
    }
}
