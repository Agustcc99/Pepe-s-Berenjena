using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // F-18 (tanda 2): el pad de entrega de un pedido y la tarjeta del HUD, con los servicios mínimos (sin escena).
    public class OrderPadTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameConfig _config;
        private CropData _crop;
        private EconomyManager _economy;
        private BackpackManager _backpack;
        private OrdersManager _orders;
        private OrderPad _pad;
        private TextMeshPro _label;

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
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

            _economy = Create<EconomyManager>();
            _economy.Initialize(_config);
            _backpack = Create<BackpackManager>();
            _backpack.Initialize(_config);
            _orders = Create<OrdersManager>();
            _orders.Initialize(_config);
            _orders.Roll = max => 0;

            var padObject = new GameObject("OrderPad");
            _objects.Add(padObject);
            padObject.SetActive(false);
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(padObject.transform, false);
            _label = labelObject.AddComponent<TextMeshPro>();
            _pad = padObject.AddComponent<OrderPad>();
            TestReflection.Set(_pad, "label", _label);
            TestReflection.Set(_pad, "dwellSeconds", 0f);
            TestReflection.Set(_pad, "slot", 0);
            padObject.SetActive(true);
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
        public IEnumerator StandingOnThePad_DeliversOncePerVisit_AndTheSignFollows()
        {
            _orders.Tick(0.1f);
            int wanted = _orders.QuantityOf(0);
            Assert.Greater(wanted, 3);

            for (int i = 0; i < 3; i++) _backpack.TryAdd(_crop);

            _pad.Interact(null);
            Assert.AreEqual(3, _orders.DeliveredOf(0), "Entrega todo lo que lleva de ese cultivo.");
            Assert.AreEqual(0, _backpack.Backpack.CountOf(_crop));

            // Sin bajarse del pad no vuelve a actuar aunque la mochila se llene otra vez.
            for (int i = 0; i < 2; i++) _backpack.TryAdd(_crop);
            _pad.Interact(null);
            Assert.AreEqual(3, _orders.DeliveredOf(0));

            yield return new WaitForSecondsRealtime(0.3f);   // el cartel se revisa unas 5 veces por segundo
            StringAssert.Contains("3/" + wanted, _label.text);

            _pad.ActorLeft(null);
            _pad.Interact(null);
            Assert.AreEqual(5, _orders.DeliveredOf(0));
        }

        // IN-05: la tarjeta de pedidos arranca plegada (solo "Pedidos del pueblo (N) +") y al tocarla muestra el detalle.
        [UnityTest]
        public IEnumerator TheHudCard_StartsCollapsed_AndTappingItShowsTheDetail()
        {
            var hudObject = new GameObject("OrdersHud");
            _objects.Add(hudObject);
            hudObject.SetActive(false);
            var card = new GameObject("Card");
            card.transform.SetParent(hudObject.transform, false);
            var headerObject = new GameObject("Header");
            headerObject.transform.SetParent(card.transform, false);
            var header = headerObject.AddComponent<TextMeshPro>();
            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(card.transform, false);
            var body = bodyObject.AddComponent<TextMeshPro>();
            var view = hudObject.AddComponent<OrdersHudView>();
            TestReflection.Set(view, "card", card);
            TestReflection.Set(view, "text", body);
            TestReflection.Set(view, "header", header);
            hudObject.SetActive(true);
            yield return null;

            _orders.Tick(0.1f);
            Assert.IsFalse(view.IsExpanded);
            StringAssert.Contains("(3)", header.text, "El encabezado cuenta los pedidos.");
            StringAssert.EndsWith("+", header.text);
            Assert.IsFalse(bodyObject.activeSelf, "Plegada no muestra el detalle.");

            view.Toggle();
            Assert.IsTrue(bodyObject.activeSelf);
            StringAssert.EndsWith("-", header.text);
            StringAssert.Contains("0/" + _orders.QuantityOf(0), body.text);

            view.Toggle();
            Assert.IsFalse(bodyObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CompletingTheOrder_Pays_AndTheHudCardListsOnlyActiveOrders()
        {
            var hudObject = new GameObject("OrdersHud");
            _objects.Add(hudObject);
            hudObject.SetActive(false);
            var card = new GameObject("Card");
            card.transform.SetParent(hudObject.transform, false);
            var text = card.AddComponent<TextMeshPro>();
            var view = hudObject.AddComponent<OrdersHudView>();
            TestReflection.Set(view, "card", card);
            TestReflection.Set(view, "text", text);
            var done = new GameObject("Done");
            done.transform.SetParent(hudObject.transform, false);
            var doneText = done.AddComponent<TextMeshPro>();
            TestReflection.Set(view, "completedLabel", doneText);
            hudObject.SetActive(true);
            yield return null;

            Assert.IsFalse(card.activeSelf, "Sin pedidos la tarjeta queda apagada.");
            Assert.IsFalse(view.IsNoticeShowing, "El aviso de pedido completado arranca apagado.");

            _orders.Tick(0.1f);
            Assert.IsTrue(card.activeSelf);
            int wanted = _orders.QuantityOf(0);
            StringAssert.Contains("0/" + wanted, text.text);

            long reward = _orders.RewardCoins(0);
            Assert.Greater(reward, 0L);
            string rewardText = "+" + reward.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            StringAssert.Contains(rewardText, text.text, "Cada pedido muestra su premio en la tarjeta del HUD.");
            for (int i = 0; i < wanted; i++) _backpack.TryAdd(_crop);

            _pad.Interact(null);
            Assert.IsFalse(_orders.IsActive(0), "El pedido completo deja el lugar vacío.");
            Assert.AreEqual(reward, _economy.Balance);
            Assert.IsTrue(card.activeSelf, "Quedan los otros dos pedidos.");
            Assert.IsTrue(view.IsNoticeShowing, "Al completar un pedido sale el aviso.");
            StringAssert.Contains(rewardText, view.NoticeText, "Y dice cuánto se ganó.");
        }
    }
}
