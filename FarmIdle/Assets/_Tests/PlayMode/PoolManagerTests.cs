using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Cuenta los Instantiate reales de forma independiente al pool: el Awake de un componente corre
    // una sola vez por instancia creada, así que Awakes == cantidad de Instantiate.
    public sealed class AwakeCounter : MonoBehaviour
    {
        public static int Awakes;

        private void Awake()
        {
            Awakes++;
        }
    }

    public class PoolManagerTests
    {
        private GameObject _managerObject;
        private PoolManager _pools;
        private GameObject _template;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("PoolManager (test)");
            _pools = _managerObject.AddComponent<PoolManager>();
            _pools.Initialize();

            // Hace de prefab: lo que se clona en cada Instantiate.
            _template = new GameObject("Template");
            _template.AddComponent<AwakeCounter>();
            AwakeCounter.Awakes = 0;   // el propio molde ya corrió su Awake
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_template);
            Object.DestroyImmediate(_managerObject);
        }

        [Test]
        public void Prewarm_CreatesTheRequestedInstances_InactiveAndCounted()
        {
            _pools.Prewarm(_template, 5);

            Assert.IsTrue(_pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(5, stats.Created);
            Assert.AreEqual(5, stats.Inactive);
            Assert.AreEqual(0, stats.Active);
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(5, AwakeCounter.Awakes);   // 5 Instantiate reales, ni uno más
        }

        [Test]
        public void AfterPrewarm_GetAndRelease_NeverInstantiate()
        {
            _pools.Prewarm(_template, 5);
            int awakesAfterPrewarm = AwakeCounter.Awakes;

            var held = new GameObject[5];
            for (int round = 0; round < 100; round++)
            {
                for (int i = 0; i < held.Length; i++) held[i] = _pools.Get(_template);
                for (int i = 0; i < held.Length; i++) _pools.Release(held[i]);
            }

            Assert.IsTrue(_pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(awakesAfterPrewarm, AwakeCounter.Awakes, "Hubo Instantiate después del Prewarm.");
            Assert.AreEqual(5, stats.Created);
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(5, stats.Inactive);
            Assert.AreEqual(0, stats.Active);
        }

        [Test]
        public void GetBeyondPrewarm_Instantiates_CountsAMiss_AndWarns()
        {
            _pools.Prewarm(_template, 2);
            _pools.Get(_template);
            _pools.Get(_template);

            LogAssert.Expect(LogType.Warning, new Regex("fuera del Prewarm"));
            _pools.Get(_template);

            Assert.IsTrue(_pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(3, stats.Created);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(3, stats.Active);
        }

        [Test]
        public void GetWithoutPrewarm_WarnsButStillWorks()
        {
            LogAssert.Expect(LogType.Warning, new Regex("sin Prewarm previo"));
            LogAssert.Expect(LogType.Warning, new Regex("fuera del Prewarm"));

            GameObject instance = _pools.Get(_template);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.activeSelf);
        }

        [Test]
        public void Prewarm_IsIdempotent_AndOnlyCreatesWhatIsMissing()
        {
            _pools.Prewarm(_template, 3);
            _pools.Prewarm(_template, 3);
            _pools.Prewarm(_template, 5);

            Assert.IsTrue(_pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(5, stats.Created);
            Assert.AreEqual(5, AwakeCounter.Awakes);
        }

        [Test]
        public void Get_PlacesAndActivatesTheInstance_AndReleaseParksIt()
        {
            _pools.Prewarm(_template, 1);
            Transform parent = new GameObject("Parent").transform;
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject instance = _pools.Get(_template, new Vector3(1f, 2f, 3f), rotation, parent);

            Assert.IsTrue(instance.activeInHierarchy);
            Assert.AreEqual(1f, instance.transform.position.x, 0.001f);
            Assert.AreEqual(2f, instance.transform.position.y, 0.001f);
            Assert.AreEqual(3f, instance.transform.position.z, 0.001f);
            Assert.Less(Quaternion.Angle(rotation, instance.transform.rotation), 0.01f);
            Assert.AreSame(parent, instance.transform.parent);

            _pools.Release(instance);

            Assert.IsFalse(instance.activeSelf);
            Assert.AreNotSame(parent, instance.transform.parent);
            Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void ReleasedInstance_IsReusedByTheNextGet()
        {
            _pools.Prewarm(_template, 1);

            GameObject first = _pools.Get(_template);
            _pools.Release(first);
            GameObject second = _pools.Get(_template);

            Assert.AreSame(first, second);
        }

        [Test]
        public void Release_OfAnObjectThatIsNotPooled_Warns()
        {
            var stranger = new GameObject("Stranger");

            LogAssert.Expect(LogType.Warning, new Regex("no viene de ningún pool"));
            _pools.Release(stranger);
        }

        [Test]
        public void DoubleRelease_IsIgnoredWithAWarning_AndDoesNotCorruptThePool()
        {
            _pools.Prewarm(_template, 2);
            GameObject instance = _pools.Get(_template);
            _pools.Release(instance);

            LogAssert.Expect(LogType.Warning, new Regex("ya estaba libre"));
            _pools.Release(instance);

            Assert.IsTrue(_pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(2, stats.Inactive);

            // Si la instancia hubiera quedado duplicada en el pool, saldría dos veces.
            GameObject a = _pools.Get(_template);
            GameObject b = _pools.Get(_template);
            Assert.AreNotSame(a, b);
        }
    }
}
