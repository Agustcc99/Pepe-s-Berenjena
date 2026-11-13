using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Una mochila que crece (mejora de capacidad) con servicios reales y un granjero armado por código (BackpackRig): la pila
    // visible acompaña sin perder nada ni hacer Instantiate (su Prewarm ya cuenta con la capacidad máxima), y el aviso de
    // "mochila llena" aparece y desaparece con ella.
    public class BackpackGrowthTests
    {
        private const int Capacity = 6;
        private const int CropCount = 2;
        private const int Bonus = 24;   // máximo: 30 lugares
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private BackpackRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(0, 0.6f);
            _rig = BackpackRig.AddTo(_world, FarAway, Capacity, CropCount, Bonus);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        private void Fill(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(_rig.Actor.TryAccept(_rig.Crops[i % CropCount], FarAway + Vector3.up), "La mochila tendría que tener lugar (" + i + ").");
            }
        }

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(0.7f);   // terminan los vuelos de la pila (0,35 s)
        }

        [UnityTest]
        public IEnumerator ThePoolIsSizedForTheMaximumCapacity_NotForTheCurrentOne()
        {
            Assert.AreEqual(Capacity, _rig.Backpack.Capacity);
            Assert.AreEqual(Capacity + Bonus, _rig.Manager.MaxCapacity);
            Assert.AreEqual(Capacity + Bonus + _rig.View.DepartingHeadroom, _rig.ProductPrewarm);

            for (int i = 0; i < CropCount; i++)
            {
                Assert.IsTrue(_world.Pools.TryGetStats(_rig.ProductTemplates[i], out PoolStats stats));
                Assert.AreEqual(_rig.ProductPrewarm, stats.Created, "La pila prewarmea la mochila entera en su máximo.");
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator GrowingTheBackpack_KeepsWhatItCarries_TheStackFollows_AndNeverInstantiates()
        {
            Fill(Capacity);
            yield return Settle();
            Assert.IsTrue(_rig.Backpack.IsFull);
            Assert.AreEqual(Capacity, _rig.View.ItemCount);

            var contents = new System.Collections.Generic.List<CropData>(_rig.Backpack.Items);
            int awakes = AwakeCounter.Awakes;
            float heightBefore = _rig.View.ItemHeight;

            _rig.Manager.SetCapacityBonus(Bonus);

            Assert.AreEqual(Capacity + Bonus, _rig.Backpack.Capacity);
            Assert.IsFalse(_rig.Backpack.IsFull);
            CollectionAssert.AreEqual(contents, new System.Collections.Generic.List<CropData>(_rig.Backpack.Items), "La mochila conserva lo que llevaba, en el mismo orden.");
            Assert.AreEqual(Capacity, _rig.View.ItemCount, "La pila sigue mostrando lo mismo.");

            // Una columna de 30 productos no entra en la altura máxima: la pila pasa a DOS columnas (par a la izquierda, impar a la
            // derecha) de 15 filas, que se aprietan un poco, y cada producto sigue en el suyo.
            float height = _rig.View.ItemHeight;
            Assert.AreEqual(2, _rig.View.Columns, "Con más de 13 lugares la pila usa dos columnas.");
            Assert.Less(height, heightBefore, "Con más capacidad la pila se apretuja para no pasar de su altura máxima.");
            Assert.AreEqual(3.4f / Mathf.Ceil((Capacity + Bonus) / 2f), height, 1e-4f);
            for (int i = 0; i < Capacity; i++)
            {
                GameObject item = _rig.View.ItemObject(i);
                Assert.IsNotNull(item);
                float x = (i % 2 == 0 ? -0.5f : 0.5f) * _rig.View.ColumnSpacing;
                Assert.AreEqual(0f, Vector3.Distance(new Vector3(x, (i / 2 + 0.5f) * height, 0f), item.transform.localPosition), 1e-4f, "El producto " + i + " se reacomodó a su lugar nuevo.");
            }

            // Se llena hasta el nuevo tope, con la pila siguiéndolo, sin crear nada de más.
            Fill(Bonus);
            yield return Settle();
            Assert.AreEqual(Capacity + Bonus, _rig.Backpack.Count);
            Assert.IsTrue(_rig.Backpack.IsFull);
            Assert.AreEqual(Capacity + Bonus, _rig.View.ItemCount);
            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Crecer la mochila no hizo ningún Instantiate.");

            for (int i = 0; i < CropCount; i++)
            {
                Assert.IsTrue(_world.Pools.TryGetStats(_rig.ProductTemplates[i], out PoolStats stats));
                Assert.AreEqual(0, stats.Misses);
                Assert.AreEqual(_rig.ProductPrewarm, stats.Created);
            }

            // Y vaciarla de arriba hacia abajo sigue devolviendo todo al pool.
            while (_rig.Manager.TryPop(out _)) { }
            yield return Settle();
            Assert.AreEqual(0, _rig.View.ItemCount);
            for (int i = 0; i < CropCount; i++)
            {
                _world.Pools.TryGetStats(_rig.ProductTemplates[i], out PoolStats stats);
                Assert.AreEqual(0, stats.Active);
            }
        }

        [UnityTest]
        public IEnumerator ASmallGrowth_KeepsTheSlotHeight_WhileTheColumnStillFits()
        {
            Fill(3);
            yield return Settle();
            float height = _rig.View.ItemHeight;

            // 6 + 4 = 10 productos de 0,26 miden 2,6: entran en los 3,4 de altura máxima, así que nada se aprieta.
            _rig.Manager.SetCapacityBonus(4);

            Assert.AreEqual(10, _rig.Backpack.Capacity);
            Assert.AreEqual(height, _rig.View.ItemHeight, 1e-6f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 2.5f * height, 0f), _rig.View.ItemObject(2).transform.localPosition), 1e-4f);
        }

        [UnityTest]
        public IEnumerator TheFullIndicator_ShowsWhenTheBackpackIsFull_AndHidesWhenThereIsRoom()
        {
            GameObject host = _rig.Farmer;
            host.SetActive(false);   // se arma apagado y se prende al final: el OnEnable ya ve su texto
            var labelObject = new GameObject("FullLabel", typeof(TextMeshPro));
            labelObject.transform.SetParent(host.transform, false);
            var label = labelObject.GetComponent<TextMeshPro>();
            label.enabled = false;

            var indicator = host.AddComponent<BackpackFullIndicator>();
            TestReflection.Set(indicator, "label", label);
            host.SetActive(true);
            yield return null;

            Assert.IsFalse(indicator.IsShowing, "Con la mochila vacía no hay aviso.");

            Fill(Capacity - 1);
            Assert.IsFalse(indicator.IsShowing, "Con un lugar libre tampoco.");

            Fill(1);
            Assert.IsTrue(_rig.Backpack.IsFull);
            Assert.IsTrue(indicator.IsShowing, "Llena: aparece el MAX.");

            Assert.IsTrue(_rig.Manager.TryPop(out _));
            Assert.IsFalse(indicator.IsShowing, "Se liberó un lugar: se va.");

            Fill(1);
            Assert.IsTrue(indicator.IsShowing);
            _rig.Manager.SetCapacityBonus(4);
            Assert.IsFalse(indicator.IsShowing, "Comprar capacidad también lo apaga.");

            host.SetActive(false);
            Assert.IsFalse(label.enabled, "Al apagarse deja el texto oculto.");
        }
    }
}
