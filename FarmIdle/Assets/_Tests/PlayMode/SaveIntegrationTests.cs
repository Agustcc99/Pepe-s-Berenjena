using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // La partida guardada con servicios REALES (economía, mochila, progresión y SaveManager, sobre un almacenamiento en memoria):
    // jugar, "cerrar" y volver a arrancar los servicios, y que todo quede igual. También el guardado diferido, la pausa y los anuncios,
    // los archivos dañados y los datos que ya no existen.
    public class SaveIntegrationTests
    {
        private FieldTestWorld _world;
        private SaveRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = SaveRig.Create(_world);
            _rig.Start();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        // Juega un rato: ventas, compras completas y a medias, mochila con cosas, nivel y un perk.
        private void PlayABit()
        {
            _rig.Economy.AddCoins(2000);
            Assert.AreEqual(300L, _rig.Progression.TryPay(_rig.Zones[0], 300));
            Assert.AreEqual(100L, _rig.Progression.TryPay(_rig.Capacity, 100));
            Assert.AreEqual(200L, _rig.Progression.TryPay(_rig.Zones[1], 200), "Una compra a medias.");
            Assert.AreEqual(50L, _rig.Progression.TryPay(_rig.Price, 50), "Otra a medias.");

            for (int i = 0; i < 30; i++) _rig.Economy.SellProduct(_rig.Carrot);   // 30 x $6 de XP base: pasa al nivel 2 (125 de XP)
            Assert.AreEqual(2, _rig.Progression.Level);
            Assert.IsTrue(_rig.Progression.TryChoosePerk(_rig.Strength));

            Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Wheat));
            Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Carrot));
            Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Tomato));
            Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Wheat));
        }

        // ---------- Cerrar y volver a abrir ----------

        [UnityTest]
        public IEnumerator CloseAndReopen_RestoresWalletBackpackZonesUpgradesPartialPaymentsLevelAndPerks()
        {
            PlayABit();
            string before = _rig.Describe();
            long coins = _rig.Economy.Balance;

            _rig.Restart();

            Assert.AreEqual(before, _rig.Describe(), "Todo igual después de cerrar y volver a abrir.");
            Assert.AreEqual(SaveLoadStatus.Loaded, _rig.Save.LoadStatus);

            // Y los valores concretos, no solo que "sea igual a sí mismo".
            Assert.AreEqual(coins, _rig.Economy.Balance);
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(_rig.Zones[0]));
            Assert.IsFalse(_rig.Progression.IsZoneUnlocked(_rig.Zones[1]));
            Assert.AreEqual(200L, _rig.Progression.PaidFor(_rig.Zones[1]), "Lo pagado a medias se conserva.");
            Assert.AreEqual(50L, _rig.Progression.PaidFor(_rig.Price));
            Assert.AreEqual(1, _rig.Progression.LevelOf(_rig.Capacity));
            Assert.AreEqual(6 + 4 + 1, _rig.Backpack.Backpack.Capacity, "La capacidad se reconstruye: base + mejora + un perk de Fuerza.");
            CollectionAssert.AreEqual(new[] { _rig.Wheat, _rig.Carrot, _rig.Tomato, _rig.Wheat }, _rig.Backpack.Backpack.Items, "La mochila vuelve en el mismo orden.");
            Assert.AreEqual(2, _rig.Progression.Level);
            Assert.AreEqual(180L, _rig.Progression.Xp);
            Assert.AreEqual(1, _rig.Progression.PicksOf(_rig.Strength));
            Assert.AreEqual(0, _rig.Progression.PerkPoints, "El punto del nivel 2 ya estaba gastado.");

            // Y se puede seguir: lo pagado a medias se completa y el siguiente nivel pide la XP que sigue.
            Assert.AreEqual(300L, _rig.Progression.TryPay(_rig.Zones[1], 1000), "Faltaban 300 de 500.");
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(_rig.Zones[1]));
            yield break;
        }

        [UnityTest]
        public IEnumerator CloseAndReopenTwice_KeepsTheSameGame_AndTheFileDoesNotGrow()
        {
            PlayABit();
            _rig.Restart();
            string first = _rig.Describe();
            _rig.Storage.TryRead(SaveFile.MainSlot, out string textAfterFirst);

            _rig.Restart();
            _rig.Restart();

            Assert.AreEqual(first, _rig.Describe());
            _rig.Storage.TryRead(SaveFile.MainSlot, out string textAfterThird);
            Assert.AreEqual(textAfterFirst.Length, textAfterThird.Length, "Volver a guardar lo mismo no hace crecer el archivo.");
            yield break;
        }

        [UnityTest]
        public IEnumerator ASaveFromAnEmptyGame_BringsTheGameBackAsANewOne()
        {
            string fresh = _rig.Describe();

            _rig.Restart();

            Assert.AreEqual(fresh, _rig.Describe());
            Assert.AreEqual(50L, _rig.Economy.Balance, "Las monedas iniciales de la partida nueva.");
            yield break;
        }

        // ---------- Restaurar no hace efectos ni crea nada ----------

        [UnityTest]
        public IEnumerator Restoring_FiresNoLevelUp_NoSale_AndOnlyOneRestoredNotice()
        {
            PlayABit();
            ProgressData rich = _rig.Progression.CreateSnapshot();
            rich.xp = 30000;   // nivel 16 de golpe
            rich.unlockedZones = new[] { _rig.Zones[0].Id, _rig.Zones[1].Id };

            int levelUps = 0;
            int sales = 0;
            var kinds = new System.Collections.Generic.List<ProgressChangeKind>();
            _rig.Progression.LevelChanged += _ => levelUps++;
            _rig.Economy.ProductSold += (_, __) => sales++;
            _rig.Progression.Changed += change => kinds.Add(change.Kind);

            _rig.Progression.Restore(rich);

            Assert.AreEqual(0, levelUps, "Cargar una partida con más nivel no es subir de nivel.");
            Assert.AreEqual(0, sales);
            CollectionAssert.AreEqual(new[] { ProgressChangeKind.Restored }, kinds, "Un solo aviso: releer todo.");
            Assert.AreEqual(16, _rig.Progression.Level);
            yield break;
        }

        [UnityTest]
        public IEnumerator RestoringAnOpenZone_ShowsItAtOnce_WithoutTheOpeningEffect()
        {
            GameObject locked = new GameObject("locked");
            GameObject unlocked = new GameObject("unlocked");
            unlocked.SetActive(false);
            var holder = new GameObject("zone");
            holder.SetActive(false);   // se arma apagado: su OnEnable ya ve las tres referencias
            var controller = holder.AddComponent<ZoneController>();
            TestReflection.Set(controller, "zone", _rig.Zones[0]);
            TestReflection.Set(controller, "lockedRoot", locked);
            TestReflection.Set(controller, "unlockedRoot", unlocked);
            holder.SetActive(true);
            yield return null;
            Assert.IsTrue(locked.activeSelf);
            Assert.IsFalse(unlocked.activeSelf);

            ProgressData data = _rig.Progression.CreateSnapshot();
            data.unlockedZones = new[] { _rig.Zones[0].Id };
            _rig.Progression.Restore(data);

            Assert.IsFalse(controller.IsRevealing, "Restaurar no corre el efecto de apertura.");
            Assert.IsTrue(unlocked.activeSelf, "El campo aparece de una.");
            Assert.IsFalse(locked.activeSelf);
            Assert.AreEqual(Vector3.one, unlocked.transform.localScale, "Sin quedarse a mitad del rebote.");

            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(locked);
            Object.DestroyImmediate(unlocked);
        }

        // ---------- Guardar cuando toca ----------

        [UnityTest]
        public IEnumerator ManyChangesInARow_MakeASingleDeferredWrite_AndIdlenessMakesNone()
        {
            Assert.AreEqual(0, _rig.Save.SaveCount);
            Assert.IsFalse(_rig.Save.IsDirty);

            for (int i = 0; i < 30; i++)
            {
                _rig.Economy.AddCoins(1);
                _rig.Economy.SellProduct(_rig.Wheat);
            }

            Assert.IsTrue(_rig.Save.IsDirty);
            Assert.AreEqual(0, _rig.Save.SaveCount, "Nada se escribe en el momento del cambio.");

            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(1, _rig.Save.SaveCount, "Todos los cambios seguidos se juntaron en un guardado.");
            Assert.IsFalse(_rig.Save.IsDirty);

            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(1, _rig.Save.SaveCount, "Sin cambios no se vuelve a escribir.");
        }

        [UnityTest]
        public IEnumerator ASteadyStreamOfChanges_StillSavesPeriodically_InsteadOfWaitingForever()
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.6f)
            {
                _rig.Economy.AddCoins(1);   // un cambio en cada frame, como una venta continua
                yield return null;
            }

            Assert.GreaterOrEqual(_rig.Save.SaveCount, 2, "El contador no se reinicia con cada cambio: guarda cada ~0,5 s aunque nunca pare.");
        }

        [UnityTest]
        public IEnumerator Purchases_AndSales_MarkTheGameForSaving()
        {
            _rig.Economy.AddCoins(1000);
            yield return new WaitForSecondsRealtime(0.8f);
            int saves = _rig.Save.SaveCount;

            _rig.Progression.TryPay(_rig.Growth, 60);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(saves + 1, _rig.Save.SaveCount, "Una compra se guarda.");

            _rig.Economy.SellProduct(_rig.Tomato);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(saves + 2, _rig.Save.SaveCount, "Una venta se guarda.");

            Assert.IsTrue(_rig.Backpack.TryAdd(_rig.Wheat));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(saves + 3, _rig.Save.SaveCount, "Cosechar (la mochila) se guarda.");
        }

        [UnityTest]
        public IEnumerator Pause_AndAnAdOnScreen_DoNotBreakTheSave()
        {
            _world.Game.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            _rig.Economy.AddCoins(5);
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(1, _rig.Save.SaveCount, "El guardado diferido usa tiempo real: en pausa también se escribe.");
            StringAssert.Contains("55", ReadSection("economy"));

            Assert.AreEqual(0L, _rig.Progression.TryPay(_rig.Growth, 60), "En pausa no se compra.");
            _world.Game.Resume();

            Assert.IsTrue(_world.Game.BeginAd());
            _rig.Economy.AddCoins(7);
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(2, _rig.Save.SaveCount, "Con un anuncio en pantalla también.");
            StringAssert.Contains("62", ReadSection("economy"));

            Assert.IsTrue(_world.Game.EndAd());
            string before = _rig.Describe();
            _rig.Restart();
            Assert.AreEqual(before, _rig.Describe());
            Assert.AreEqual(62L, _rig.Economy.Balance);
        }

        private string ReadSection(string id)
        {
            Assert.IsTrue(_rig.Storage.TryRead(SaveFile.MainSlot, out string text), "Hay un archivo de partida.");
            SaveParseResult parsed = SaveSerializer.Parse(text, SaveMigrator.Default);
            Assert.IsTrue(parsed.IsUsable);
            return parsed.Data.Find(id).json;
        }

        [UnityTest]
        public IEnumerator TheAppGoingToTheBackgroundOrQuitting_SavesAtOnce()
        {
            _rig.Economy.AddCoins(3);
            _rig.Save.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(1, _rig.Save.SaveCount, "Pasar a segundo plano guarda al instante (sin esperar el diferido).");

            _rig.Economy.AddCoins(1);
            _rig.Save.SendMessage("OnApplicationPause", false);
            Assert.AreEqual(1, _rig.Save.SaveCount, "Volver al primer plano no guarda.");

            _rig.Save.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual(2, _rig.Save.SaveCount, "Perder el foco guarda.");

            _rig.Economy.AddCoins(1);
            _rig.Save.OnWebPageHidden(string.Empty);
            Assert.AreEqual(3, _rig.Save.SaveCount, "Ocultar o cerrar la pestaña guarda.");

            _rig.Economy.AddCoins(1);
            _rig.Save.SendMessage("OnApplicationQuit");
            Assert.AreEqual(4, _rig.Save.SaveCount, "Cerrar guarda.");

            _rig.Save.SendMessage("OnApplicationQuit");
            Assert.AreEqual(4, _rig.Save.SaveCount, "Sin cambios pendientes no se reescribe.");
            Assert.GreaterOrEqual(_rig.Storage.FlushCount, 4, "Cada guardado pide bajar a almacenamiento persistente (IndexedDB en la web).");
            yield break;
        }

        [UnityTest]
        public IEnumerator NothingIsSavedBeforeTheGameIsLoaded()
        {
            var storage = new MemorySaveStorage();
            var go = new GameObject("EarlySaveManager");
            go.transform.SetParent(_world.Root.transform, false);
            var save = go.AddComponent<SaveManager>();
            save.Initialize(_rig.Config, storage);
            save.Register(_rig.Economy);

            _rig.Economy.AddCoins(10);   // el aviso llega antes de cargar: se ignora

            Assert.IsFalse(save.IsDirty);
            Assert.IsFalse(save.SaveNow(force: true), "Un arranque que no cargó no pisa una partida buena con una vacía.");
            Assert.AreEqual(0, storage.WriteCount);

            Assert.IsFalse(save.Load(), "No había partida.");
            _rig.Economy.AddCoins(10);
            Assert.IsTrue(save.IsDirty);
            Assert.IsTrue(save.SaveNow());
            Assert.IsTrue(storage.Has(SaveFile.MainSlot));
            yield break;
        }

        [UnityTest]
        public IEnumerator AFailingDisk_KeepsTheGameRunning_AndTheNextSaveSucceeds()
        {
            _rig.Storage.FailWrites = true;
            _rig.Economy.AddCoins(9);

            LogAssert.Expect(LogType.Warning, new Regex("Partida"));
            yield return new WaitForSecondsRealtime(0.9f);

            Assert.AreEqual(0, _rig.Save.SaveCount);
            Assert.IsTrue(_rig.Save.IsDirty, "Queda pendiente para reintentar.");
            Assert.IsFalse(string.IsNullOrEmpty(_rig.Save.LastError));
            Assert.AreEqual(59L, _rig.Economy.Balance, "El juego sigue andando.");

            _rig.Storage.FailWrites = false;
            Assert.IsTrue(_rig.Save.SaveNow());
            Assert.IsTrue(string.IsNullOrEmpty(_rig.Save.LastError));
            Assert.IsFalse(_rig.Save.IsDirty);
        }

        // ---------- Archivos dañados y datos que ya no existen ----------

        [UnityTest]
        public IEnumerator ADamagedSaveFile_StartsANewGame_WithAWarning_AndKeepsTheDamagedOne()
        {
            _rig.Stop();
            _rig.Storage.Put(SaveFile.MainSlot, "{ definitely not a save");
            LogAssert.Expect(LogType.Warning, new Regex("Partida"));

            _rig.Start();
            yield return null;

            Assert.AreEqual(SaveLoadStatus.CorruptStartedFresh, _rig.Save.LoadStatus);
            Assert.AreEqual(50L, _rig.Economy.Balance, "Arranca de cero: las monedas iniciales.");
            Assert.AreEqual(0, _rig.Backpack.Backpack.Count);
            Assert.AreEqual(1, _rig.Progression.Level);
            Assert.IsTrue(_rig.Storage.Has(SaveFile.CorruptSlot), "El archivo roto se conserva aparte.");

            // Y el juego guarda con normalidad de ahí en más.
            _rig.Economy.AddCoins(1);
            Assert.IsTrue(_rig.Save.SaveNow());
            Assert.IsTrue(SaveSerializer.Parse(ReadMain(), SaveMigrator.Default).IsUsable);
        }

        private string ReadMain()
        {
            _rig.Storage.TryRead(SaveFile.MainSlot, out string text);
            return text;
        }

        [UnityTest]
        public IEnumerator ADamagedMainFile_IsRecoveredFromTheBackup()
        {
            PlayABit();
            _rig.Save.SaveNow(force: true);
            string good = _rig.Describe();
            _rig.Economy.AddCoins(1);
            _rig.Save.SaveNow(force: true);   // ahora el respaldo es la partida anterior (la de 'good')
            _rig.Stop();

            _rig.Storage.Put(SaveFile.MainSlot, "damaged");
            LogAssert.Expect(LogType.Warning, new Regex("Partida"));
            _rig.Start();

            Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, _rig.Save.LoadStatus);
            Assert.AreEqual(good, _rig.Describe());
            yield break;
        }

        [UnityTest]
        public IEnumerator ADamagedSection_OnlyResetsThatSystem()
        {
            PlayABit();
            _rig.Save.SaveNow(force: true);
            string fullState = _rig.Describe();
            _rig.Stop();

            SaveData data = SaveSerializer.Parse(ReadMain(), SaveMigrator.Default).Data;
            data.Set("economy", "{{{ garbage");
            _rig.Storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            _rig.Start();

            Assert.AreEqual(50L, _rig.Economy.Balance, "La billetera ilegible arranca con las monedas iniciales.");
            Assert.AreEqual(4, _rig.Backpack.Backpack.Count, "La mochila se restauró.");
            Assert.AreEqual(2, _rig.Progression.Level, "Y el progreso también.");
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(_rig.Zones[0]));
            Assert.AreNotEqual(fullState, _rig.Describe());
            yield break;
        }

        [UnityTest]
        public IEnumerator IdsThatNoLongerExist_AreIgnored_AndSectionsOfOtherSystemsSurvive()
        {
            _rig.Stop();

            var progress = new ProgressData
            {
                unlockedZones = new[] { "ghost-zone", _rig.Zones[0].Id },
                upgradeLevels = new[] { new UpgradeLevelEntry { id = "ghost-upgrade", level = 3 }, new UpgradeLevelEntry { id = _rig.Price.Id, level = 1 } },
                payments = new[] { new PaymentEntry { key = "zone:ghost-zone", coins = 77 } },
                xp = 130,
                perkPicks = new[] { new PerkPickEntry { id = "ghost-perk", picks = 9 } }
            };
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":99}");
            data.Set("backpack", "{\"version\":1,\"crops\":[\"wheat\",\"ghost-crop\",\"carrot\"]}");
            data.Set("progress", JsonUtility.ToJson(progress));
            data.Set("ads", "{\"watched\":12}");   // un sistema que todavía no existe: su sección tiene que sobrevivir
            _rig.Storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            LogAssert.Expect(LogType.Warning, new Regex("mochila restaurada"));
            _rig.Start();

            Assert.AreEqual(99L, _rig.Economy.Balance);
            CollectionAssert.AreEqual(new[] { _rig.Wheat, _rig.Carrot }, _rig.Backpack.Backpack.Items, "El cultivo que ya no existe se saltea.");
            Assert.IsTrue(_rig.Progression.IsZoneUnlocked(_rig.Zones[0]));
            Assert.AreEqual(1, _rig.Progression.LevelOf(_rig.Price));
            Assert.AreEqual(2, _rig.Progression.Level, "130 de XP: nivel 2.");
            Assert.AreEqual(1, _rig.Progression.PerkPoints, "El perk fantasma no gasta puntos del nivel.");
            Assert.AreEqual(1.5f, _rig.Progression.Modifiers.SellPrice, 1e-5f);

            Assert.IsTrue(_rig.Save.SaveNow(force: true));
            SaveData rewritten = SaveSerializer.Parse(ReadMain(), SaveMigrator.Default).Data;
            Assert.AreEqual("{\"watched\":12}", rewritten.Find("ads").json, "Lo que nadie reclama se reescribe tal cual.");
            yield break;
        }

        [UnityTest]
        public IEnumerator ABackpackBiggerThanTheCapacity_IsTrimmedWithAWarning()
        {
            _rig.Stop();

            var data = new SaveData();
            data.Set("backpack", "{\"version\":1,\"crops\":[\"wheat\",\"wheat\",\"wheat\",\"wheat\",\"wheat\",\"wheat\",\"wheat\",\"wheat\",\"wheat\"]}");
            _rig.Storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            LogAssert.Expect(LogType.Warning, new Regex("mochila restaurada"));
            _rig.Start();

            Assert.AreEqual(6, _rig.Backpack.Backpack.Count, "Con capacidad 6 no entran los 9.");
            Assert.AreEqual(6, _rig.Backpack.Backpack.Capacity);
            yield break;
        }

        // ---------- Borrar la partida (debug) ----------

        [UnityTest]
        public IEnumerator DeleteSave_ReturnsEverythingToANewGame_AndRemovesTheFiles()
        {
            string fresh = _rig.Describe();
            PlayABit();
            _rig.Save.SaveNow(force: true);
            Assert.AreNotEqual(fresh, _rig.Describe());
            Assert.IsTrue(_rig.Storage.Has(SaveFile.MainSlot));

            _rig.Save.DeleteSave();

            Assert.AreEqual(fresh, _rig.Describe(), "Billetera, mochila, capacidad, zonas, mejoras, nivel y perks vuelven a empezar.");
            Assert.IsFalse(_rig.Storage.Has(SaveFile.MainSlot));
            Assert.IsFalse(_rig.Storage.Has(SaveFile.BackupSlot));
            Assert.AreEqual(SaveLoadStatus.NewGame, _rig.Save.LoadStatus);
            Assert.IsNull(_rig.Save.LastSavedUtc);
            Assert.IsFalse(_rig.Save.IsDirty, "Borrar no deja un guardado pendiente que lo reviva.");

            _rig.Economy.AddCoins(5);
            Assert.IsTrue(_rig.Save.SaveNow());
            yield break;
        }

        [UnityTest]
        public IEnumerator SaveStatus_ReportsVersionLoadResultAndLastSave()
        {
            Assert.AreEqual(SaveLoadStatus.NewGame, _rig.Save.LoadStatus);
            Assert.AreEqual(0, _rig.Save.SavedVersion);
            Assert.IsNull(_rig.Save.LastSavedUtc);

            int notices = 0;
            _rig.Save.StatusChanged += () => notices++;
            _rig.Economy.AddCoins(1);
            Assert.IsTrue(_rig.Save.SaveNow());

            Assert.AreEqual(SaveData.CurrentVersion, _rig.Save.SavedVersion);
            Assert.IsNotNull(_rig.Save.LastSavedUtc);
            Assert.AreEqual(1, notices, "Un aviso por guardado (el panel de debug lo usa).");
            yield break;
        }

        [Test]
        public void TwoSaveablesWithTheSameId_AreRefused()
        {
            Assert.Throws<System.InvalidOperationException>(() => _rig.Save.Register(_rig.Economy));
        }
    }
}
