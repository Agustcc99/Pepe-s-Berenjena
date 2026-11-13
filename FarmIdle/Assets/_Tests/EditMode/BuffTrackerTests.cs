using System.Collections.Generic;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // Los buffs temporales (lógica pura): activar, renovar, vencer, congelarse sin tiempo, multiplicadores y la restauración.
    public class BuffTrackerTests
    {
        private BuffTracker _tracker;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _tracker = new BuffTracker();
            _events = new List<string>();
            _tracker.Activated += buff => _events.Add("start:" + buff.Id);
            _tracker.Expired += buff => _events.Add("end:" + buff.Id);
            _tracker.Changed += () => _events.Add("changed");
        }

        [Test]
        public void Activate_AddsTheBuff_WithItsFullDuration_AndAnnouncesIt()
        {
            Assert.IsTrue(_tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 30f));

            Assert.AreEqual(1, _tracker.Count);
            Assert.IsTrue(_tracker.IsActive("turbo"));
            Assert.AreEqual(30f, _tracker.RemainingOf("turbo"), 1e-5f);
            CollectionAssert.AreEqual(new[] { "start:turbo", "changed" }, _events);
        }

        [Test]
        public void Activate_RejectsBadData_AndChangesNothing()
        {
            Assert.IsFalse(_tracker.Activate("", BuffKind.MoveSpeed, 3f, 30f));
            Assert.IsFalse(_tracker.Activate("a", BuffKind.MoveSpeed, 3f, 0f));
            Assert.IsFalse(_tracker.Activate("a", BuffKind.MoveSpeed, 3f, -5f));
            Assert.IsFalse(_tracker.Activate("a", BuffKind.MoveSpeed, float.NaN, 30f));
            Assert.IsFalse(_tracker.Activate("a", BuffKind.MoveSpeed, -1f, 30f));
            Assert.IsFalse(_tracker.Activate("a", BuffKind.MoveSpeed, 3f, float.PositiveInfinity));

            Assert.AreEqual(0, _tracker.Count);
            Assert.IsEmpty(_events);
        }

        [Test]
        public void Tick_AgesTheBuff_AndItExpiresByItself()
        {
            _tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 10f);
            _events.Clear();

            _tracker.Tick(4f);
            Assert.AreEqual(6f, _tracker.RemainingOf("turbo"), 1e-5f);
            Assert.IsEmpty(_events, "Mientras queda tiempo no pasa nada.");

            _tracker.Tick(6f);
            Assert.IsFalse(_tracker.IsActive("turbo"));
            Assert.AreEqual(0, _tracker.Count);
            CollectionAssert.AreEqual(new[] { "end:turbo", "changed" }, _events);
            Assert.AreEqual(1f, _tracker.MultiplierOf(BuffKind.MoveSpeed), "Sin buff vuelve a x1.");
        }

        [Test]
        public void Tick_WithNoGameTime_FreezesTheBuff()
        {
            _tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 10f);

            // En pausa y con un anuncio en pantalla el tiempo del juego vale 0 (y por seguridad se ignora lo negativo y NaN).
            _tracker.Tick(0f);
            _tracker.Tick(-3f);
            _tracker.Tick(float.NaN);

            Assert.AreEqual(10f, _tracker.RemainingOf("turbo"), 1e-5f);
            Assert.IsTrue(_tracker.IsActive("turbo"));
        }

        [Test]
        public void Activate_AnActiveBuff_RenewsItInsteadOfStacking()
        {
            _tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 30f);
            _tracker.Tick(20f);
            Assert.AreEqual(10f, _tracker.RemainingOf("turbo"), 1e-5f);

            _tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 30f);

            Assert.AreEqual(1, _tracker.Count, "Sigue siendo uno solo.");
            Assert.AreEqual(30f, _tracker.RemainingOf("turbo"), 1e-5f, "Vuelve a la duración completa (no 40 s): renovar, no sumar.");
            Assert.AreEqual(3f, _tracker.MultiplierOf(BuffKind.MoveSpeed), "Y el multiplicador no se acumula.");
        }

        [Test]
        public void MultiplierOf_MultipliesBuffsOfTheSameKind_AndSeparatesKinds()
        {
            _tracker.Activate("a", BuffKind.MoveSpeed, 2f, 10f);
            _tracker.Activate("b", BuffKind.MoveSpeed, 1.5f, 10f);
            _tracker.Activate("c", BuffKind.SellPrice, 2f, 10f);

            Assert.AreEqual(3f, _tracker.MultiplierOf(BuffKind.MoveSpeed), 1e-5f);
            Assert.AreEqual(2f, _tracker.MultiplierOf(BuffKind.SellPrice), 1e-5f);
        }

        [Test]
        public void Tick_ExpiresOnlyTheBuffsThatRanOut()
        {
            _tracker.Activate("short", BuffKind.MoveSpeed, 2f, 5f);
            _tracker.Activate("long", BuffKind.SellPrice, 2f, 20f);

            _tracker.Tick(6f);

            Assert.IsFalse(_tracker.IsActive("short"));
            Assert.IsTrue(_tracker.IsActive("long"));
            Assert.AreEqual(14f, _tracker.RemainingOf("long"), 1e-5f);
            Assert.AreEqual(1f, _tracker.MultiplierOf(BuffKind.MoveSpeed));
            Assert.AreEqual(2f, _tracker.MultiplierOf(BuffKind.SellPrice));
        }

        [Test]
        public void Restore_PutsTheListBack_SilentlyAndClampsTheTime()
        {
            var saved = new List<ActiveBuff>
            {
                new ActiveBuff { Id = "turbo", Kind = BuffKind.MoveSpeed, Magnitude = 3f, Duration = 30f, Remaining = 12.5f },
                new ActiveBuff { Id = "cheat", Kind = BuffKind.SellPrice, Magnitude = 2f, Duration = 30f, Remaining = 9999f },   // archivo editado
                new ActiveBuff { Id = "dead", Kind = BuffKind.MoveSpeed, Magnitude = 2f, Duration = 30f, Remaining = 0f },        // ya vencido
                new ActiveBuff { Id = "", Kind = BuffKind.MoveSpeed, Magnitude = 2f, Duration = 30f, Remaining = 5f },            // sin id
                new ActiveBuff { Id = "turbo", Kind = BuffKind.MoveSpeed, Magnitude = 3f, Duration = 30f, Remaining = 20f },       // repetido
            };

            _tracker.Restore(saved);

            Assert.AreEqual(2, _tracker.Count);
            Assert.AreEqual(12.5f, _tracker.RemainingOf("turbo"), 1e-5f);
            Assert.AreEqual(30f, _tracker.RemainingOf("cheat"), 1e-5f, "No puede tener más de lo que dura el buff.");
            CollectionAssert.AreEqual(new[] { "changed" }, _events, "Restaurar no dispara 'recién activado': solo Changed.");
        }

        [Test]
        public void Restore_Null_ClearsEverything()
        {
            _tracker.Activate("turbo", BuffKind.MoveSpeed, 3f, 10f);

            _tracker.Restore(null);

            Assert.AreEqual(0, _tracker.Count);
        }

        [Test]
        public void ARestoredBuff_KeepsAgingAndExpiresNormally()
        {
            _tracker.Restore(new[] { new ActiveBuff { Id = "turbo", Kind = BuffKind.MoveSpeed, Magnitude = 3f, Duration = 30f, Remaining = 2f } });
            _events.Clear();

            _tracker.Tick(2.5f);

            Assert.IsFalse(_tracker.IsActive("turbo"));
            CollectionAssert.AreEqual(new[] { "end:turbo", "changed" }, _events);
        }
    }
}
