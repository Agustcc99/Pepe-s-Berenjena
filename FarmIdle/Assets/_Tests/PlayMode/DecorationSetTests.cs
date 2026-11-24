using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // F-20: que se note la decoración comprada. Aparece con un salto y, por unos segundos, la guía (flecha y cartel) apunta a ella.
    // Cargar la partida prende las decoraciones sin salto ni guía.
    public class DecorationSetTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameConfig _config;
        private EconomyManager _economy;
        private DecorationManager _decorations;
        private DecorationSet _set;
        private GameObject[] _items;

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private DecorationData Data(string id, int cost)
        {
            var data = ScriptableObject.CreateInstance<DecorationData>();
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
            GuidePoints.Clear();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _objects.Add(_config);
            TestReflection.Set(_config, "decorations", new[] { Data("deco_a", 100), Data("deco_b", 200) });

            _economy = Create<EconomyManager>();
            _economy.Initialize(_config);
            _decorations = Create<DecorationManager>();
            _decorations.Initialize(_config);

            // El conjunto con dos objetos, armado apagado y prendido después (como en la escena: ya están los servicios).
            var root = new GameObject("Decorations");
            _objects.Add(root);
            root.SetActive(false);
            _items = new GameObject[2];
            for (int i = 0; i < _items.Length; i++)
            {
                _items[i] = new GameObject("Item" + i);
                _items[i].transform.SetParent(root.transform, false);
                _items[i].transform.position = new Vector3(10f + i * 5f, 0f, -12f);
                _items[i].transform.localScale = Vector3.one * 1.6f;
            }

            _set = root.AddComponent<DecorationSet>();
            TestReflection.Set(_set, "items", _items);
            TestReflection.Set(_set, "guideSeconds", 1.2f);
            TestReflection.Set(_set, "popSeconds", 0.5f);
            root.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DecorationSet.Placed -= OnPlaced;
            foreach (Object item in _objects)
            {
                if (item != null) Object.Destroy(item);
            }

            _objects.Clear();
            yield return null;
            GuidePoints.Clear();
        }

        [UnityTest]
        public IEnumerator BuyingADecoration_PopsItIn_AndTheGuidePointsAtIt_ForAWhile()
        {
            Assert.IsFalse(_items[0].activeSelf, "Sin comprar, apagada.");
            Assert.IsFalse(_set.WantsGuide);
            Assert.IsNull(GuidePoints.Best(Vector3.zero));

            _economy.AddCoins(1000);
            Assert.IsTrue(_decorations.TryBuyNext());

            Assert.IsTrue(_items[0].activeSelf, "La comprada se prende.");
            Assert.IsFalse(_items[1].activeSelf);
            Assert.AreEqual(0, _set.NewIndex);
            Assert.IsTrue(_set.WantsGuide, "La guía la señala.");
            Assert.AreSame(_set, GuidePoints.Best(Vector3.zero), "Y gana a cualquier otro lugar.");
            Assert.AreEqual(GuidePoints.NewItemPriority, _set.GuidePriority);
            Assert.AreEqual(_items[0].transform.position, _set.Position, "La flecha apunta a donde apareció.");

            yield return null;
            Assert.Less(_items[0].transform.localScale.x, 1.6f, "Aparece chica y crece (el salto).");

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(1.6f, _items[0].transform.localScale.x, 1e-4f, "Terminado el salto queda en su tamaño.");

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(_set.WantsGuide, "Pasado el rato, la guía la suelta.");
        }

        private int _placedCount;
        private int _placedPercent;
        private Vector3 _placedAt;

        private void OnPlaced(Vector3 position, int percent)
        {
            _placedCount++;
            _placedAt = position;
            _placedPercent = percent;
        }

        // F-22: la compra avisa para el festejo (estallido y cartel); cargar una partida no.
        [UnityTest]
        public IEnumerator BuyingADecoration_AnnouncesTheCelebration_ButLoadingASaveDoesNot()
        {
            DecorationSet.Placed += OnPlaced;

            _decorations.Restore("{\"version\":1,\"owned\":1}");
            yield return null;
            Assert.AreEqual(0, _placedCount, "Cargar la partida no festeja.");

            _economy.AddCoins(1000);
            Assert.IsTrue(_decorations.TryBuyNext());
            Assert.AreEqual(1, _placedCount);
            Assert.AreEqual(_items[1].transform.position, _placedAt, "El festejo sale donde apareció la segunda.");
            Assert.AreEqual(2, _placedPercent, "Lleva su encanto en % (2 por defecto).");
        }

        [UnityTest]
        public IEnumerator LoadingASave_ShowsTheOwnedDecorations_WithoutPopOrGuide()
        {
            _decorations.Restore("{\"version\":1,\"owned\":2}");
            yield return null;

            Assert.IsTrue(_items[0].activeSelf);
            Assert.IsTrue(_items[1].activeSelf);
            Assert.AreEqual(1.6f, _items[1].transform.localScale.x, 1e-4f);
            Assert.IsFalse(_set.WantsGuide, "Cargar la partida no señala nada.");
        }
    }
}
