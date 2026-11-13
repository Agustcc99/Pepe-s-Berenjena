using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El progreso del jugador (C# puro): zonas abiertas, nivel de cada mejora y lo pagado de cada compra en curso, con su evento
    // de cambios y la conversión a ProgressData que la Fase 7 va a guardar.
    public class PlayerProgressTests
    {
        private PlayerProgress _progress;
        private List<ProgressChange> _changes;

        [SetUp]
        public void SetUp()
        {
            _progress = new PlayerProgress();
            _changes = new List<ProgressChange>();
            _progress.Changed += _changes.Add;
        }

        [Test]
        public void StartsEmpty()
        {
            Assert.IsFalse(_progress.IsZoneUnlocked("east"));
            Assert.AreEqual(0, _progress.LevelOf("capacity"));
            Assert.AreEqual(0L, _progress.PaidOf(PlayerProgress.ZoneKey("east")));
            Assert.AreEqual(0, _progress.UnlockedZoneCount);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void UnlockZone_OpensIt_Once_AndAnnouncesIt()
        {
            Assert.IsTrue(_progress.UnlockZone("east"));

            Assert.IsTrue(_progress.IsZoneUnlocked("east"));
            Assert.IsFalse(_progress.IsZoneUnlocked("west"));
            Assert.AreEqual(1, _progress.UnlockedZoneCount);
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual(ProgressChangeKind.ZoneUnlocked, _changes[0].Kind);
            Assert.AreEqual("east", _changes[0].Id);

            Assert.IsFalse(_progress.UnlockZone("east"), "Abrir una zona abierta no hace nada.");
            Assert.AreEqual(1, _changes.Count, "Y no avisa de nuevo.");
        }

        [Test]
        public void UnlockZone_WithoutAnId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _progress.UnlockZone(""));
            Assert.Throws<ArgumentException>(() => _progress.UnlockZone(null));
        }

        [Test]
        public void SetLevel_StoresTheLevel_AndAnnouncesOnlyRealChanges()
        {
            Assert.IsTrue(_progress.SetLevel("capacity", 2));
            Assert.AreEqual(2, _progress.LevelOf("capacity"));
            Assert.AreEqual(0, _progress.LevelOf("speed"), "Cada mejora tiene su nivel.");
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual(ProgressChangeKind.UpgradeLevelChanged, _changes[0].Kind);
            Assert.AreEqual("capacity", _changes[0].Id);

            Assert.IsFalse(_progress.SetLevel("capacity", 2), "El mismo nivel no es un cambio.");
            Assert.AreEqual(1, _changes.Count);

            Assert.IsTrue(_progress.SetLevel("capacity", 0), "Volver al nivel 0 es un cambio.");
            Assert.AreEqual(0, _progress.LevelOf("capacity"));
            Assert.AreEqual(2, _changes.Count);
        }

        [Test]
        public void SetLevel_RejectsNegativeLevelsAndMissingIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.SetLevel("capacity", -1));
            Assert.Throws<ArgumentException>(() => _progress.SetLevel("", 1));
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void AddPaid_Accumulates_PerPurchase_AndAnnounces()
        {
            string east = PlayerProgress.ZoneKey("east");
            string capacity = PlayerProgress.UpgradeKey("capacity");

            _progress.AddPaid(east, 100);
            _progress.AddPaid(east, 50);
            _progress.AddPaid(capacity, 7);

            Assert.AreEqual(150L, _progress.PaidOf(east));
            Assert.AreEqual(7L, _progress.PaidOf(capacity));
            Assert.AreEqual(3, _changes.Count);
            Assert.AreEqual(ProgressChangeKind.PaymentChanged, _changes[0].Kind);
            Assert.AreEqual(east, _changes[0].Id);
        }

        [Test]
        public void AddPaid_ZeroDoesNothing_NegativeThrows()
        {
            string east = PlayerProgress.ZoneKey("east");

            _progress.AddPaid(east, 0);
            Assert.AreEqual(0, _changes.Count);

            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.AddPaid(east, -1));
            Assert.Throws<ArgumentException>(() => _progress.AddPaid("", 5));
        }

        [Test]
        public void ClearPaid_ForgetsTheAmount_AndOnlyAnnouncesIfThereWasSomething()
        {
            string east = PlayerProgress.ZoneKey("east");

            _progress.ClearPaid(east);
            Assert.AreEqual(0, _changes.Count, "No había nada que borrar.");

            _progress.AddPaid(east, 80);
            _changes.Clear();
            _progress.ClearPaid(east);

            Assert.AreEqual(0L, _progress.PaidOf(east));
            Assert.AreEqual(1, _changes.Count);
        }

        [Test]
        public void ZoneAndUpgradeKeys_NeverCollide_EvenWithTheSameId()
        {
            Assert.AreNotEqual(PlayerProgress.ZoneKey("x"), PlayerProgress.UpgradeKey("x"));

            _progress.AddPaid(PlayerProgress.ZoneKey("x"), 10);
            Assert.AreEqual(0L, _progress.PaidOf(PlayerProgress.UpgradeKey("x")));
        }

        [Test]
        public void ToData_AndLoadFrom_RoundTrip_AlsoThroughJson()
        {
            _progress.UnlockZone("west");
            _progress.UnlockZone("east");
            _progress.SetLevel("speed", 3);
            _progress.SetLevel("capacity", 1);
            _progress.AddPaid(PlayerProgress.ZoneKey("north"), 120);

            ProgressData data = _progress.ToData();
            Assert.AreEqual(ProgressData.CurrentVersion, data.version);
            CollectionAssert.AreEqual(new[] { "east", "west" }, data.unlockedZones, "Siempre en el mismo orden: el archivo no cambia sin motivo.");

            // El formato lo entiende JsonUtility tal cual (es lo que va a escribir el SaveManager).
            ProgressData fromJson = JsonUtility.FromJson<ProgressData>(JsonUtility.ToJson(data));

            var restored = new PlayerProgress();
            restored.LoadFrom(fromJson);

            Assert.IsTrue(restored.IsZoneUnlocked("east"));
            Assert.IsTrue(restored.IsZoneUnlocked("west"));
            Assert.AreEqual(2, restored.UnlockedZoneCount);
            Assert.AreEqual(3, restored.LevelOf("speed"));
            Assert.AreEqual(1, restored.LevelOf("capacity"));
            Assert.AreEqual(120L, restored.PaidOf(PlayerProgress.ZoneKey("north")));
            Assert.AreEqual(JsonUtility.ToJson(data), JsonUtility.ToJson(restored.ToData()), "Guardar lo restaurado da lo mismo.");
        }

        [Test]
        public void LoadFrom_ReplacesEverything_AndAnnouncesOnce()
        {
            _progress.UnlockZone("old");
            _progress.SetLevel("old-upgrade", 2);
            _progress.AddPaid(PlayerProgress.ZoneKey("old"), 5);
            _changes.Clear();

            var data = new ProgressData { unlockedZones = new[] { "east" }, upgradeLevels = new[] { new UpgradeLevelEntry { id = "capacity", level = 2 } } };
            _progress.LoadFrom(data);

            Assert.IsFalse(_progress.IsZoneUnlocked("old"), "Lo anterior se descarta.");
            Assert.AreEqual(0, _progress.LevelOf("old-upgrade"));
            Assert.AreEqual(0L, _progress.PaidOf(PlayerProgress.ZoneKey("old")));
            Assert.IsTrue(_progress.IsZoneUnlocked("east"));
            Assert.AreEqual(2, _progress.LevelOf("capacity"));
            Assert.AreEqual(1, _changes.Count, "Un solo aviso para todo el cambio.");
            Assert.AreEqual(ProgressChangeKind.Restored, _changes[0].Kind);
        }

        [Test]
        public void LoadFrom_ToleratesNullAndDamagedData()
        {
            Assert.DoesNotThrow(() => _progress.LoadFrom(null));
            Assert.AreEqual(0, _progress.UnlockedZoneCount);

            var damaged = new ProgressData
            {
                unlockedZones = new[] { null, "", "east" },
                upgradeLevels = new[] { null, new UpgradeLevelEntry { id = "", level = 3 }, new UpgradeLevelEntry { id = "a", level = -2 }, new UpgradeLevelEntry { id = "b", level = 1 } },
                payments = new[] { null, new PaymentEntry { key = "k", coins = -9 }, new PaymentEntry { key = "j", coins = 40 } }
            };

            Assert.DoesNotThrow(() => _progress.LoadFrom(damaged));
            Assert.AreEqual(1, _progress.UnlockedZoneCount, "Solo queda la zona con id.");
            Assert.AreEqual(0, _progress.LevelOf("a"), "Un nivel negativo se ignora.");
            Assert.AreEqual(1, _progress.LevelOf("b"));
            Assert.AreEqual(0L, _progress.PaidOf("k"), "Un monto negativo se ignora.");
            Assert.AreEqual(40L, _progress.PaidOf("j"));

            Assert.DoesNotThrow(() => _progress.LoadFrom(new ProgressData { unlockedZones = null, upgradeLevels = null, payments = null }));
        }
    }
}
