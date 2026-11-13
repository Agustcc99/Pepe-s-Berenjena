using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La mochila es C# puro: se prueba sin escena. Los cultivos son ScriptableObjects de prueba; solo importa
    // cuál es cuál (se comparan por referencia).
    public class InventoryTests
    {
        private CropData _wheat;
        private CropData _carrot;
        private CropData _tomato;
        private Inventory _backpack;
        private List<InventoryChange> _changes;

        [SetUp]
        public void SetUp()
        {
            _wheat = ScriptableObject.CreateInstance<CropData>();
            _carrot = ScriptableObject.CreateInstance<CropData>();
            _tomato = ScriptableObject.CreateInstance<CropData>();

            _backpack = new Inventory(3);
            _changes = new List<InventoryChange>();
            _backpack.Changed += change => _changes.Add(change);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wheat);
            Object.DestroyImmediate(_carrot);
            Object.DestroyImmediate(_tomato);
        }

        [Test]
        public void NewBackpack_IsEmpty_WithTheGivenCapacity()
        {
            Assert.AreEqual(3, _backpack.Capacity);
            Assert.AreEqual(0, _backpack.Count);
            Assert.IsFalse(_backpack.IsFull);
            Assert.AreEqual(0, _backpack.Items.Count);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void TryGrowTo_RaisesTheCapacity_KeepingTheContents_AndAnnouncesIt()
        {
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_carrot);
            _backpack.TryAdd(_tomato);
            Assert.IsTrue(_backpack.IsFull);
            _changes.Clear();

            var capacities = new List<int>();
            _backpack.CapacityChanged += capacities.Add;

            Assert.IsTrue(_backpack.TryGrowTo(5));

            Assert.AreEqual(5, _backpack.Capacity);
            Assert.AreEqual(3, _backpack.Count, "Lo que llevaba se conserva.");
            CollectionAssert.AreEqual(new[] { _wheat, _carrot, _tomato }, _backpack.Items, "Y en el mismo orden.");
            Assert.IsFalse(_backpack.IsFull, "Ahora hay lugar.");
            CollectionAssert.AreEqual(new[] { 5 }, capacities);
            Assert.AreEqual(0, _changes.Count, "Crecer no es agregar ni sacar productos.");

            Assert.IsTrue(_backpack.TryAdd(_wheat));
            Assert.IsTrue(_backpack.TryAdd(_wheat));
            Assert.IsFalse(_backpack.TryAdd(_wheat), "El tope nuevo es 5.");
        }

        [TestCase(3)]
        [TestCase(2)]
        [TestCase(0)]
        public void TryGrowTo_NeverShrinks_AndDoesNotAnnounceNothing(int smallerOrEqual)
        {
            _backpack.TryAdd(_wheat);
            int announced = 0;
            _backpack.CapacityChanged += capacity => announced++;

            Assert.IsFalse(_backpack.TryGrowTo(smallerOrEqual));

            Assert.AreEqual(3, _backpack.Capacity);
            Assert.AreEqual(1, _backpack.Count);
            Assert.AreEqual(0, announced);
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void Constructor_WithoutRoomForAnything_Throws(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(capacity));
        }

        [Test]
        public void TryAdd_PutsTheProductOnTop_AndRaisesTheEventOnceWithTheNewCount()
        {
            Assert.IsTrue(_backpack.TryAdd(_wheat));

            Assert.AreEqual(1, _backpack.Count);
            Assert.AreSame(_wheat, _backpack.Items[0]);
            Assert.AreEqual(1, _changes.Count);
            Assert.AreSame(_wheat, _changes[0].Crop);
            Assert.IsTrue(_changes[0].Added);
            Assert.AreEqual(1, _changes[0].Count);
        }

        [Test]
        public void TryAdd_Null_Throws_AndChangesNothing()
        {
            Assert.Throws<ArgumentNullException>(() => _backpack.TryAdd(null));

            Assert.AreEqual(0, _backpack.Count);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void TryAdd_ReachingTheCapacity_MakesItFull_AndTheNextOneIsRejectedWithoutChangingAnything()
        {
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_carrot);
            Assert.IsFalse(_backpack.IsFull);

            Assert.IsTrue(_backpack.TryAdd(_tomato), "El último lugar sí se puede usar.");
            Assert.IsTrue(_backpack.IsFull);
            _changes.Clear();

            Assert.IsFalse(_backpack.TryAdd(_wheat), "Llena: rechaza.");

            Assert.AreEqual(3, _backpack.Count);
            Assert.AreSame(_tomato, _backpack.Items[2], "Lo de arriba sigue siendo lo último que entró.");
            Assert.AreEqual(0, _changes.Count, "Un intento rechazado no avisa.");
        }

        [Test]
        public void TryPop_TakesTheTopFirst_LIFO()
        {
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_carrot);
            _backpack.TryAdd(_tomato);

            Assert.IsTrue(_backpack.TryPop(out CropData first));
            Assert.IsTrue(_backpack.TryPop(out CropData second));
            Assert.IsTrue(_backpack.TryPop(out CropData third));

            Assert.AreSame(_tomato, first);
            Assert.AreSame(_carrot, second);
            Assert.AreSame(_wheat, third);
            Assert.AreEqual(0, _backpack.Count);
        }

        [Test]
        public void TryPop_OnAnEmptyBackpack_ReturnsFalse_AndRaisesNothing()
        {
            Assert.IsFalse(_backpack.TryPop(out CropData crop));

            Assert.IsNull(crop);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void MixedCrops_KeepTheirOrderFromBottomToTop_AndAreCountedPerCrop()
        {
            _backpack = new Inventory(5);
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_tomato);
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_carrot);
            _backpack.TryAdd(_wheat);

            CollectionAssert.AreEqual(new[] { _wheat, _tomato, _wheat, _carrot, _wheat }, _backpack.Items);
            Assert.AreEqual(3, _backpack.CountOf(_wheat));
            Assert.AreEqual(1, _backpack.CountOf(_tomato));
            Assert.AreEqual(1, _backpack.CountOf(_carrot));

            // Y sale en el orden inverso: la mezcla no cambia el LIFO.
            _backpack.TryPop(out CropData top);
            Assert.AreSame(_wheat, top);
            _backpack.TryPop(out top);
            Assert.AreSame(_carrot, top);
            Assert.AreEqual(2, _backpack.CountOf(_wheat));
            Assert.AreEqual(0, _backpack.CountOf(_carrot));
        }

        [Test]
        public void AfterTakingOneOut_ThereIsRoomAgain()
        {
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_wheat);
            Assert.IsFalse(_backpack.TryAdd(_carrot));

            _backpack.TryPop(out _);
            Assert.IsFalse(_backpack.IsFull);

            Assert.IsTrue(_backpack.TryAdd(_carrot));
            Assert.AreSame(_carrot, _backpack.Items[2]);
        }

        [Test]
        public void Events_ReportEachChange_WithTheCropTheDirectionAndTheCountAfterIt()
        {
            _backpack.TryAdd(_wheat);
            _backpack.TryAdd(_carrot);
            _backpack.TryPop(out _);
            _backpack.TryPop(out _);

            Assert.AreEqual(4, _changes.Count);
            AssertChange(_changes[0], _wheat, added: true, count: 1);
            AssertChange(_changes[1], _carrot, added: true, count: 2);
            AssertChange(_changes[2], _carrot, added: false, count: 1);
            AssertChange(_changes[3], _wheat, added: false, count: 0);
        }

        [Test]
        public void Events_ArriveAfterTheStateChanged()
        {
            // La vista lee la mochila dentro del evento: tiene que ver ya el valor final.
            int countSeenByTheListener = -1;
            _backpack.Changed += change => countSeenByTheListener = _backpack.Count;

            _backpack.TryAdd(_wheat);
            Assert.AreEqual(1, countSeenByTheListener);

            _backpack.TryPop(out _);
            Assert.AreEqual(0, countSeenByTheListener);
        }

        [Test]
        public void ItemsView_IsTheLiveContent_NotASnapshot()
        {
            IReadOnlyInventory view = _backpack;
            IReadOnlyList<CropData> items = view.Items;

            _backpack.TryAdd(_wheat);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(1, view.Count);
        }

        private static void AssertChange(InventoryChange change, CropData crop, bool added, int count)
        {
            Assert.AreSame(crop, change.Crop);
            Assert.AreEqual(added, change.Added);
            Assert.AreEqual(count, change.Count);
        }
    }
}
