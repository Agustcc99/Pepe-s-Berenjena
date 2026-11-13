using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // La pila visible de la espalda con servicios reales (GameManager, PoolManager, BackpackManager) y un granjero
    // armado por código (BackpackRig): 3 cultivos, cada uno con su producto "molde" con un AwakeCounter para contar
    // los Instantiate reales. Sin parcelas: acá importa solo cómo se refleja la mochila.
    public class BackpackStackViewTests
    {
        private const int Capacity = 6;
        private const int CropCount = 3;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private BackpackRig _rig;
        private CropData _naked;   // un cultivo sin producto, solo para una prueba; se destruye en TearDown

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(0, 0.6f);
            _rig = BackpackRig.AddTo(_world, FarAway, Capacity, CropCount);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            if (_naked != null) Object.DestroyImmediate(_naked);
            Time.timeScale = 1f;
        }

        private Vector3 OriginNearTheFarmer()
        {
            return _rig.Farmer.transform.position + new Vector3(4f, 0f, 5f);
        }

        // Lo mismo que hace una parcela al cosechar: le ofrece el cultivo al jugador, que lo guarda y avisa de dónde vino.
        private void Accept(int crop, Vector3 from)
        {
            Assert.IsTrue(_rig.Actor.TryAccept(_rig.Crops[crop], from), "La mochila tendría que tener lugar.");
        }

        private PoolStats ProductPool(int crop)
        {
            Assert.IsTrue(_world.Pools.TryGetStats(_rig.ProductTemplates[crop], out PoolStats stats), "No hay pool del producto " + crop);
            return stats;
        }

        private Vector3 SlotWorldPosition(int index)
        {
            return _rig.Anchor.TransformPoint(new Vector3(0f, (index + 0.5f) * _rig.View.ItemHeight, 0f));
        }

        // Deja que terminen los vuelos (duran 0,35 s con tiempo sin escala).
        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(0.7f);
        }

        [UnityTest]
        public IEnumerator Activating_PrewarmsTheBackpackCapacityOfEveryProduct_WithoutExtraInstances()
        {
            Assert.AreEqual(_rig.ProductPrewarm * CropCount, _rig.PrewarmAwakes, "Una mochila entera de cada producto, más los que pueden estar volando a la venta.");

            for (int i = 0; i < CropCount; i++)
            {
                PoolStats stats = ProductPool(i);
                Assert.AreEqual(_rig.ProductPrewarm, stats.Created);
                Assert.AreEqual(_rig.ProductPrewarm, stats.Inactive);
                Assert.AreEqual(0, stats.Active);
                Assert.AreEqual(0, stats.Misses);
            }

            Assert.AreEqual(0, _rig.View.ItemCount, "La mochila arranca vacía.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ProductsPileUpOnTheBack_InBackpackOrder_WithMixedCrops()
        {
            int[] order = { 0, 1, 0, 2, 1, 0 };
            foreach (int crop in order) Accept(crop, OriginNearTheFarmer());

            yield return Settle();

            Assert.AreEqual(order.Length, _rig.View.ItemCount);
            Assert.AreEqual(order.Length, _rig.Backpack.Count);

            var seen = new HashSet<GameObject>();
            for (int i = 0; i < order.Length; i++)
            {
                Assert.AreSame(_rig.Crops[order[i]], _rig.Backpack.Items[i], "El orden de la mochila.");

                GameObject item = _rig.View.ItemObject(i);
                Assert.IsNotNull(item, "Falta el producto " + i);
                Assert.IsTrue(seen.Add(item), "Dos lugares de la pila comparten el mismo objeto.");
                Assert.AreEqual(_rig.ProductTemplates[order[i]].name, item.name, "El producto tiene que ser el del cultivo (lugar " + i + ").");
                Assert.IsTrue(item.activeInHierarchy);
                Assert.AreSame(_rig.Anchor, item.transform.parent, "La pila cuelga del ancla de la espalda.");

                Vector3 expected = new Vector3(0f, (i + 0.5f) * _rig.View.ItemHeight, 0f);
                Assert.AreEqual(0f, Vector3.Distance(expected, item.transform.localPosition), 1e-4f, "Altura por ítem en el lugar " + i);
                Assert.AreEqual(Vector3.one, item.transform.localScale);
                Assert.IsFalse(_rig.View.IsFlying(i));
            }

            // Un producto más alto en la pila está más arriba, y todos están sobre el mismo eje.
            for (int i = 1; i < order.Length; i++)
            {
                Assert.Greater(_rig.View.ItemObject(i).transform.position.y, _rig.View.ItemObject(i - 1).transform.position.y);
            }

            int[] perCrop = { 3, 2, 1 };
            for (int crop = 0; crop < CropCount; crop++)
            {
                Assert.AreEqual(perCrop[crop], ProductPool(crop).Active);
                Assert.AreEqual(_rig.ProductPrewarm, ProductPool(crop).Created, "No se creó nada nuevo.");
            }
        }

        [UnityTest]
        public IEnumerator TheProduct_FliesFromTheGivenPoint_AndLandsOnItsSlot()
        {
            Vector3 from = OriginNearTheFarmer();
            Accept(0, from);

            Assert.IsTrue(_rig.View.IsFlying(0));
            GameObject item = _rig.View.ItemObject(0);
            Assert.AreEqual(0f, Vector3.Distance(item.transform.position, from), 0.01f, "Arranca en el punto de donde salió (la parcela).");
            Assert.Less(item.transform.localScale.x, 1f, "Sale chico y crece en el vuelo.");

            yield return new WaitForSecondsRealtime(0.12f);

            Assert.IsTrue(_rig.View.IsFlying(0), "A los 0,12 s todavía vuela.");
            Assert.Greater(Vector3.Distance(item.transform.position, from), 0.3f, "Ya salió del punto de origen.");
            Assert.Greater(Vector3.Distance(item.transform.position, SlotWorldPosition(0)), 0.3f, "Todavía no llegó.");

            yield return Settle();

            Assert.IsFalse(_rig.View.IsFlying(0));
            Assert.AreEqual(0f, Vector3.Distance(item.transform.position, SlotWorldPosition(0)), 1e-3f, "Aterrizó en su lugar de la pila.");
            Assert.AreEqual(Vector3.one, item.transform.localScale);
        }

        [UnityTest]
        public IEnumerator TheFlight_HeadsForWhereTheBackIsNow_NotWhereItWas()
        {
            Accept(0, OriginNearTheFarmer());
            GameObject item = _rig.View.ItemObject(0);
            Vector3 oldSlot = SlotWorldPosition(0);

            yield return new WaitForSecondsRealtime(0.05f);
            _rig.Farmer.transform.position += new Vector3(8f, 0f, 0f);   // el jugador sigue caminando
            Vector3 newSlot = SlotWorldPosition(0);
            Assert.AreEqual(8f, Vector3.Distance(oldSlot, newSlot), 1e-3f);

            yield return new WaitForSecondsRealtime(0.25f);   // casi al final del vuelo
            Assert.Less(Vector3.Distance(item.transform.position, newSlot), Vector3.Distance(item.transform.position, oldSlot),
                "Tiene que ir hacia la espalda que se mueve, no hacia el lugar viejo.");

            yield return Settle();
            Assert.AreEqual(0f, Vector3.Distance(item.transform.position, SlotWorldPosition(0)), 1e-3f);
        }

        [UnityTest]
        public IEnumerator WhenTheBackpackEmptiesFromTheTop_TheProductsGoBackToThePool_TopFirst()
        {
            int[] order = { 0, 1, 2, 0 };
            foreach (int crop in order) Accept(crop, OriginNearTheFarmer());
            yield return Settle();

            var items = new GameObject[order.Length];
            for (int i = 0; i < order.Length; i++) items[i] = _rig.View.ItemObject(i);

            Assert.IsTrue(_rig.Manager.TryPop(out CropData popped));
            Assert.AreSame(_rig.Crops[0], popped);

            Assert.AreEqual(3, _rig.View.ItemCount, "La pila perdió el de arriba.");
            Assert.IsFalse(items[3].activeSelf, "El de arriba volvió al pool.");
            Assert.AreNotSame(_rig.Anchor, items[3].transform.parent);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(items[i].activeInHierarchy, "Los de abajo no se tocan: " + i);
                Assert.AreSame(items[i], _rig.View.ItemObject(i));
            }

            // Se vacía del todo, de arriba hacia abajo (como lo hará la venta).
            Assert.IsTrue(_rig.Manager.TryPop(out _));
            Assert.AreEqual(2, _rig.View.ItemCount);
            Assert.IsFalse(items[2].activeSelf);
            Assert.IsTrue(_rig.Manager.TryPop(out _));
            Assert.IsTrue(_rig.Manager.TryPop(out _));

            Assert.AreEqual(0, _rig.View.ItemCount);
            for (int crop = 0; crop < CropCount; crop++)
            {
                Assert.AreEqual(0, ProductPool(crop).Active, "Todo vuelve al pool.");
                Assert.AreEqual(_rig.ProductPrewarm, ProductPool(crop).Inactive);
            }
        }

        [UnityTest]
        public IEnumerator PoppingWhileTheProductFlies_StopsTheFlight_AndReturnsItToThePool()
        {
            Accept(1, OriginNearTheFarmer());
            GameObject item = _rig.View.ItemObject(0);
            Assert.IsTrue(_rig.View.IsFlying(0));

            Assert.IsTrue(_rig.Manager.TryPop(out _));

            Assert.AreEqual(0, _rig.View.ItemCount);
            Assert.IsFalse(item.activeSelf);
            Vector3 parked = item.transform.position;

            yield return Settle();

            Assert.AreEqual(0f, Vector3.Distance(parked, item.transform.position), 1e-4f, "Una corrutina de vuelo quedó viva moviendo un objeto del pool.");
            Assert.IsFalse(item.activeSelf);
            Assert.AreEqual(0, ProductPool(1).Active);
        }

        [UnityTest]
        public IEnumerator WhilePaused_AFlyingProductStillLands_AndThePileStillFollowsThePops()
        {
            Accept(0, OriginNearTheFarmer());
            Accept(2, OriginNearTheFarmer());

            Assert.IsTrue(_world.Game.Pause());
            Assert.AreEqual(0f, Time.timeScale);

            yield return Settle();   // el vuelo usa tiempo sin escala: aterriza aunque el mundo esté congelado

            Assert.IsFalse(_rig.View.IsFlying(0));
            Assert.IsFalse(_rig.View.IsFlying(1));
            Assert.AreEqual(0f, Vector3.Distance(_rig.View.ItemObject(1).transform.position, SlotWorldPosition(1)), 1e-3f);

            Assert.IsTrue(_rig.Manager.TryPop(out _));
            Assert.AreEqual(1, _rig.View.ItemCount, "Vaciar la mochila en pausa también se refleja.");

            Assert.IsTrue(_world.Game.Resume());
            Accept(1, OriginNearTheFarmer());
            yield return Settle();
            Assert.AreEqual(2, _rig.View.ItemCount);
        }

        [UnityTest]
        public IEnumerator FillingAndEmptyingTheBackpackOverAndOver_NeverInstantiates()
        {
            int awakes = AwakeCounter.Awakes;

            for (int round = 0; round < 5; round++)
            {
                for (int i = 0; i < Capacity; i++) Accept((i + round) % CropCount, OriginNearTheFarmer());

                Assert.IsTrue(_rig.Backpack.IsFull);
                Assert.IsFalse(_rig.Actor.TryAccept(_rig.Crops[0], OriginNearTheFarmer()), "Llena: no entra nada más.");
                Assert.AreEqual(Capacity, _rig.View.ItemCount);

                while (_rig.Manager.TryPop(out _)) { }
                Assert.AreEqual(0, _rig.View.ItemCount);
                yield return null;
            }

            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Hubo Instantiate después del Prewarm.");
            for (int crop = 0; crop < CropCount; crop++)
            {
                PoolStats stats = ProductPool(crop);
                Assert.AreEqual(_rig.ProductPrewarm, stats.Created);
                Assert.AreEqual(0, stats.Misses);
                Assert.AreEqual(0, stats.Active);
            }
        }

        [UnityTest]
        public IEnumerator DisablingTheView_ReturnsEveryProduct_AndEnablingItRebuildsThePileFromTheBackpack()
        {
            foreach (int crop in new[] { 2, 0, 1 }) Accept(crop, OriginNearTheFarmer());
            yield return Settle();

            _rig.Farmer.SetActive(false);   // por ejemplo, al descargar la escena

            Assert.AreEqual(0, _rig.View.ItemCount);
            for (int crop = 0; crop < CropCount; crop++) Assert.AreEqual(0, ProductPool(crop).Active, "Los productos tienen que volver al pool antes de que la escena se vaya.");
            Assert.AreEqual(3, _rig.Backpack.Count, "La mochila no se toca.");

            _rig.Farmer.SetActive(true);

            Assert.AreEqual(3, _rig.View.ItemCount);
            Assert.AreEqual(_rig.ProductTemplates[2].name, _rig.View.ItemObject(0).name);
            Assert.AreEqual(_rig.ProductTemplates[0].name, _rig.View.ItemObject(1).name);
            Assert.AreEqual(_rig.ProductTemplates[1].name, _rig.View.ItemObject(2).name);
            Assert.AreEqual(0f, Vector3.Distance(_rig.View.ItemObject(2).transform.position, SlotWorldPosition(2)), 1e-3f);
        }

        [UnityTest]
        public IEnumerator ABackpackChangedByAnotherSystem_ShowsUpInThePile_WithoutFlying()
        {
            Assert.IsTrue(_rig.Manager.TryAdd(_rig.Crops[1]));   // no pasa por el jugador: nadie avisa de dónde vino

            Assert.AreEqual(1, _rig.View.ItemCount);
            Assert.IsFalse(_rig.View.IsFlying(0));
            Assert.AreEqual(0f, Vector3.Distance(_rig.View.ItemObject(0).transform.position, SlotWorldPosition(0)), 1e-3f);

            yield break;
        }

        // Ojo: nada de try/finally en una prueba iterador. Con este Unity (6000.6.6f1) un try/finally con un 'out _' en el
        // finally tumbó el proceso entero con un crash nativo; la limpieza va en TearDown.
        [UnityTest]
        public IEnumerator ACropWithoutAProductPrefab_IsCountedByTheBackpack_ButNotShown_AndLogsOnce()
        {
            _naked = TestReflection.CreateCrop("naked", 1f, _world.PlantTemplate);
            LogAssert.Expect(LogType.Error, new Regex("no tiene prefab de producto"));

            Assert.IsTrue(_rig.Actor.TryAccept(_naked, OriginNearTheFarmer()));
            Assert.IsTrue(_rig.Actor.TryAccept(_naked, OriginNearTheFarmer()));   // el segundo no vuelve a quejarse

            Assert.AreEqual(2, _rig.Backpack.Count);
            Assert.AreEqual(2, _rig.View.ItemCount);
            Assert.IsNull(_rig.View.ItemObject(0), "Sin prefab no hay nada que mostrar.");
            Assert.IsFalse(_rig.View.IsFlying(1));

            Assert.IsTrue(_rig.Manager.TryPop(out CropData popped));
            Assert.AreSame(_naked, popped);
            Assert.AreEqual(1, _rig.View.ItemCount, "Sacarlo de la mochila también lo saca de la pila.");

            yield break;
        }
    }
}
