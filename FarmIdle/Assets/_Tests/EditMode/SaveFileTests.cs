using System;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // El protocolo de archivos de la partida (SaveFile) sobre un almacenamiento en memoria: escritura con respaldo, y una carga que
    // se recupera de un archivo roto, vacío o de una versión desconocida sin tirar excepciones y sin perder el archivo apartado.
    public class SaveFileTests
    {
        private MemorySaveStorage _storage;
        private SaveFile _file;

        [SetUp]
        public void SetUp()
        {
            _storage = new MemorySaveStorage();
            _file = new SaveFile(_storage, SaveMigrator.Default);
        }

        private static string TextWithCoins(long coins)
        {
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":" + coins + "}");
            return SaveSerializer.Serialize(data);
        }

        private static long CoinsOf(SaveLoadResult result)
        {
            return UnityEngine.JsonUtility.FromJson<CoinsProbe>(result.Data.Find("economy").json).coins;
        }

        [Serializable]
        private sealed class CoinsProbe
        {
            public long coins;
        }

        // ---------- Partida nueva ----------

        [Test]
        public void Load_WithNothingSaved_IsANewGame()
        {
            SaveLoadResult result = _file.Load();

            Assert.AreEqual(SaveLoadStatus.NewGame, result.Status);
            Assert.IsFalse(result.HasData);
            Assert.IsFalse(result.NeedsWarning, "Una partida nueva no es un problema.");
        }

        // ---------- Escribir y leer ----------

        [Test]
        public void Write_Then_Load_ReturnsWhatWasSaved_AndFlushes()
        {
            Assert.IsTrue(_file.Write(TextWithCoins(500)));
            Assert.AreEqual(1, _storage.FlushCount, "Tras guardar se pide bajar a almacenamiento persistente (IndexedDB en la web).");

            SaveLoadResult result = new SaveFile(_storage, SaveMigrator.Default).Load();

            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(500L, CoinsOf(result));
        }

        [Test]
        public void EachWrite_KeepsThePreviousGoodSaveAsTheBackup()
        {
            _file.Write(TextWithCoins(1));
            Assert.IsFalse(_storage.Has(SaveFile.BackupSlot), "El primer guardado no tiene anterior.");

            _file.Write(TextWithCoins(2));
            _file.Write(TextWithCoins(3));

            _storage.TryRead(SaveFile.MainSlot, out string main);
            _storage.TryRead(SaveFile.BackupSlot, out string backup);
            Assert.AreEqual(TextWithCoins(3), main);
            Assert.AreEqual(TextWithCoins(2), backup);
        }

        [Test]
        public void AFailedWrite_ReturnsFalse_AndTheMainSaveStaysAsItWas()
        {
            _file.Write(TextWithCoins(10));

            _storage.FailWrites = true;
            Assert.IsFalse(_file.Write(TextWithCoins(99)));
            _storage.FailWrites = false;

            SaveLoadResult result = new SaveFile(_storage, SaveMigrator.Default).Load();
            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(10L, CoinsOf(result));
        }

        [Test]
        public void Write_WithoutText_Throws()
        {
            Assert.Throws<ArgumentException>(() => _file.Write(""));
            Assert.Throws<ArgumentException>(() => _file.Write(null));
        }

        // ---------- Archivo dañado ----------

        [Test]
        public void ADamagedMainFile_IsRecoveredFromTheBackup_AndTheDamagedOneIsKeptAside()
        {
            _file.Write(TextWithCoins(7));
            _file.Write(TextWithCoins(8));   // principal 8, respaldo 7
            _storage.Put(SaveFile.MainSlot, "{ this is not a save");

            SaveLoadResult result = new SaveFile(_storage, SaveMigrator.Default).Load();

            Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, result.Status);
            Assert.IsTrue(result.NeedsWarning, "Se avisa en consola.");
            Assert.AreEqual(7L, CoinsOf(result));
            _storage.TryRead(SaveFile.CorruptSlot, out string kept);
            Assert.AreEqual("{ this is not a save", kept, "El archivo roto se conserva aparte, no se pierde.");
        }

        [Test]
        public void AMissingMainFile_WithABackup_IsRecoveredFromTheBackup()
        {
            _file.Write(TextWithCoins(7));
            _file.Write(TextWithCoins(8));
            _storage.Delete(SaveFile.MainSlot);

            SaveLoadResult result = new SaveFile(_storage, SaveMigrator.Default).Load();

            Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, result.Status);
            Assert.AreEqual(7L, CoinsOf(result));
            Assert.IsFalse(_storage.Has(SaveFile.CorruptSlot), "No había nada roto que apartar.");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{}")]
        [TestCase("garbage")]
        public void AnEmptyOrUselessMainFile_WithoutBackup_StartsFresh_WithoutThrowing(string content)
        {
            _storage.Put(SaveFile.MainSlot, content);

            SaveLoadResult result = null;
            Assert.DoesNotThrow(() => result = _file.Load());

            Assert.AreEqual(SaveLoadStatus.CorruptStartedFresh, result.Status);
            Assert.IsFalse(result.HasData);
            Assert.IsTrue(result.NeedsWarning);
            Assert.IsTrue(_storage.Has(SaveFile.CorruptSlot));
        }

        [Test]
        public void MainAndBackupBothDamaged_StartsFresh_AndExplainsWhy()
        {
            _storage.Put(SaveFile.MainSlot, "xxx");
            _storage.Put(SaveFile.BackupSlot, "yyy");

            SaveLoadResult result = _file.Load();

            Assert.AreEqual(SaveLoadStatus.CorruptStartedFresh, result.Status);
            StringAssert.Contains("respaldo", result.Message);
        }

        [Test]
        public void WhenTheStorageCannotBeRead_TheGameStartsFresh_WithoutThrowing()
        {
            _file.Write(TextWithCoins(5));
            _storage.FailReads = true;

            SaveLoadResult result = null;
            Assert.DoesNotThrow(() => result = new SaveFile(_storage, SaveMigrator.Default).Load());

            Assert.AreEqual(SaveLoadStatus.NewGame, result.Status);
        }

        // ---------- Versión desconocida ----------

        [Test]
        public void ASaveFromAFutureVersion_StartsFresh_ButIsNeverOverwrittenOrLost()
        {
            string future = "{\"version\":" + (SaveData.CurrentVersion + 3) + ",\"sections\":[{\"id\":\"economy\",\"json\":\"{\\\"coins\\\":1}\"}]}";
            _storage.Put(SaveFile.MainSlot, future);
            _storage.Put(SaveFile.BackupSlot, TextWithCoins(1));   // aunque haya un respaldo, no se usa: es de otra época

            SaveFile file = new SaveFile(_storage, SaveMigrator.Default);
            SaveLoadResult result = file.Load();

            Assert.AreEqual(SaveLoadStatus.FutureVersionStartedFresh, result.Status);
            Assert.IsFalse(result.HasData);
            Assert.IsTrue(result.NeedsWarning);
            _storage.TryRead(SaveFile.FutureSlot, out string kept);
            Assert.AreEqual(future, kept, "Se conserva una copia exacta para poder volver a la versión nueva.");

            file.Write(TextWithCoins(2));
            _storage.TryRead(SaveFile.FutureSlot, out kept);
            Assert.AreEqual(future, kept, "Y guardar la partida nueva no toca esa copia.");
        }

        // ---------- Migración dentro de la carga ----------

        [Test]
        public void AnOldSave_IsMigratedOnLoad_AndSavedBackInTheCurrentFormat()
        {
            var migrator = new SaveMigrator(2, new System.Collections.Generic.Dictionary<int, SaveMigrator.Step>
            {
                { 1, d => { d.Set("migrated", "yes"); return d; } }
            });
            var data = new SaveData { version = 1 };
            data.Set("economy", "{\"version\":1,\"coins\":42}");
            _storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            var file = new SaveFile(_storage, migrator);
            SaveLoadResult result = file.Load();

            Assert.AreEqual(SaveLoadStatus.Migrated, result.Status);
            Assert.AreEqual(2, result.Data.version);
            Assert.AreEqual("yes", result.Data.Find("migrated").json);

            file.Write(SaveSerializer.Serialize(result.Data));
            _storage.TryRead(SaveFile.BackupSlot, out string backup);
            Assert.AreEqual(1, SaveSerializer.Parse(backup, new SaveMigrator(1)).Data.version, "El respaldo conserva el archivo ORIGINAL (por si la migración tuviera un error).");
        }

        // ---------- Borrar ----------

        [Test]
        public void DeleteSave_RemovesTheGameAndItsBackup_ButKeepsTheSetAsideFiles()
        {
            _file.Write(TextWithCoins(1));
            _file.Write(TextWithCoins(2));
            _storage.Put(SaveFile.CorruptSlot, "old damaged file");

            _file.DeleteSave();

            Assert.IsFalse(_storage.Has(SaveFile.MainSlot));
            Assert.IsFalse(_storage.Has(SaveFile.BackupSlot));
            Assert.IsTrue(_storage.Has(SaveFile.CorruptSlot));
            Assert.AreEqual(SaveLoadStatus.NewGame, new SaveFile(_storage, SaveMigrator.Default).Load().Status);
        }
    }
}
