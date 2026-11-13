using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // El espejo de la partida de la web (localStorage, síncrono) contra el almacén de siempre (IndexedDB, asíncrono): si el jugador cierra la
    // pestaña de golpe y IndexedDB perdió los últimos segundos, el espejo los devuelve. Todo con almacenes en memoria.
    public class MirroredSaveStorageTests
    {
        private sealed class FakeKeyValueStore : IKeyValueStore
        {
            public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
            public bool Broken;   // localStorage bloqueado (modo privado) o lleno

            public bool TryGet(string key, out string value)
            {
                value = null;
                return !Broken && Data.TryGetValue(key, out value);
            }

            public bool TrySet(string key, string value)
            {
                if (Broken) return false;

                Data[key] = value;
                return true;
            }

            public void Remove(string key)
            {
                if (!Broken) Data.Remove(key);
            }
        }

        // Una partida mínima válida escrita en una fecha dada.
        private static string SaveAt(DateTime utc, long coins)
        {
            var data = new SaveData { savedAtUtc = utc.ToString("o") };
            data.Set("economy", "{\"version\":1,\"coins\":" + coins + "}");
            return SaveSerializer.Serialize(data);
        }

        private static readonly DateTime T0 = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Writing_TheMainSlot_AlsoWritesTheMirror_AndOtherSlotsDoNot()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore();
            var storage = new MirroredSaveStorage(inner, mirror);

            Assert.IsTrue(storage.TryWrite(SaveFile.MainSlot, SaveAt(T0, 10)));
            Assert.IsTrue(storage.TryWrite(SaveFile.BackupSlot, SaveAt(T0, 5)));

            Assert.AreEqual(1, mirror.Data.Count, "Solo se espeja la partida principal.");
            Assert.AreEqual(SaveAt(T0, 10), mirror.Data[MirroredSaveStorage.MirrorKey]);
        }

        [Test]
        public void WhenIndexedDbLostTheLastSeconds_TheMirrorBringsThemBack()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore();
            inner.TryWrite(SaveFile.MainSlot, SaveAt(T0, 100));                                    // lo que alcanzó a quedar en IndexedDB
            mirror.Data[MirroredSaveStorage.MirrorKey] = SaveAt(T0.AddSeconds(3), 140);            // lo último, que sí quedó en localStorage

            var storage = new MirroredSaveStorage(inner, mirror);
            Assert.IsTrue(storage.TryRead(SaveFile.MainSlot, out string text));

            Assert.AreEqual(SaveAt(T0.AddSeconds(3), 140), text, "Gana la copia más nueva.");

            // Y la partida se carga de verdad con esos datos.
            SaveLoadResult loaded = new SaveFile(storage, SaveMigrator.Default).Load();
            Assert.AreEqual(SaveLoadStatus.Loaded, loaded.Status);
            StringAssert.Contains("\"coins\":140", loaded.Data.Find("economy").json);
        }

        [Test]
        public void WhenTheMirrorIsOlder_TheNormalStorageWins()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore();
            inner.TryWrite(SaveFile.MainSlot, SaveAt(T0.AddSeconds(10), 300));
            mirror.Data[MirroredSaveStorage.MirrorKey] = SaveAt(T0, 100);

            var storage = new MirroredSaveStorage(inner, mirror);
            storage.TryRead(SaveFile.MainSlot, out string text);

            Assert.AreEqual(SaveAt(T0.AddSeconds(10), 300), text);
        }

        [Test]
        public void WhenOnlyOneCopyExists_ThatOneIsUsed()
        {
            var mirror = new FakeKeyValueStore();
            mirror.Data[MirroredSaveStorage.MirrorKey] = SaveAt(T0, 77);
            var onlyMirror = new MirroredSaveStorage(new MemorySaveStorage(), mirror);
            Assert.IsTrue(onlyMirror.TryRead(SaveFile.MainSlot, out string fromMirror));
            Assert.AreEqual(SaveAt(T0, 77), fromMirror, "IndexedDB vacío (por ejemplo, se borró el sitio a medias) y el espejo sí está.");

            var inner = new MemorySaveStorage();
            inner.TryWrite(SaveFile.MainSlot, SaveAt(T0, 55));
            var onlyInner = new MirroredSaveStorage(inner, new FakeKeyValueStore());
            Assert.IsTrue(onlyInner.TryRead(SaveFile.MainSlot, out string fromInner));
            Assert.AreEqual(SaveAt(T0, 55), fromInner);

            var nothing = new MirroredSaveStorage(new MemorySaveStorage(), new FakeKeyValueStore());
            Assert.IsFalse(nothing.TryRead(SaveFile.MainSlot, out _), "Sin ninguna copia es una partida nueva.");
        }

        [Test]
        public void AGarbageMirror_IsIgnored_AndNeverHidesAGoodSave()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore();
            inner.TryWrite(SaveFile.MainSlot, SaveAt(T0, 9));
            mirror.Data[MirroredSaveStorage.MirrorKey] = "esto no es una partida";

            var storage = new MirroredSaveStorage(inner, mirror);
            storage.TryRead(SaveFile.MainSlot, out string text);
            Assert.AreEqual(SaveAt(T0, 9), text);

            var onlyGarbage = new MirroredSaveStorage(new MemorySaveStorage(), mirror);
            Assert.IsFalse(onlyGarbage.TryRead(SaveFile.MainSlot, out _), "Un espejo ilegible solo no cuenta como partida.");
        }

        [Test]
        public void ABrokenMirror_NeverBreaksTheNormalSave()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore { Broken = true };
            var storage = new MirroredSaveStorage(inner, mirror);

            Assert.IsTrue(storage.TryWrite(SaveFile.MainSlot, SaveAt(T0, 12)), "localStorage bloqueado: se guarda igual en el almacén de siempre.");
            Assert.IsTrue(storage.TryRead(SaveFile.MainSlot, out string text));
            Assert.AreEqual(SaveAt(T0, 12), text);
        }

        [Test]
        public void Deleting_TheMainSlot_AlsoClearsTheMirror_SoNothingComesBack()
        {
            var inner = new MemorySaveStorage();
            var mirror = new FakeKeyValueStore();
            var storage = new MirroredSaveStorage(inner, mirror);
            storage.TryWrite(SaveFile.MainSlot, SaveAt(T0, 1));

            Assert.IsTrue(storage.Delete(SaveFile.MainSlot));

            Assert.IsFalse(storage.TryRead(SaveFile.MainSlot, out _));
            Assert.AreEqual(0, mirror.Data.Count);
        }

        [Test]
        public void SavedAt_ReadsTheStamp_AndRejectsTextWithout()
        {
            Assert.AreEqual(T0, SaveSerializer.SavedAtOf(SaveAt(T0, 1)));
            Assert.AreEqual(DateTime.MinValue, SaveSerializer.SavedAtOf(null));
            Assert.AreEqual(DateTime.MinValue, SaveSerializer.SavedAtOf(""));
            Assert.AreEqual(DateTime.MinValue, SaveSerializer.SavedAtOf("{}"));
            Assert.AreEqual(DateTime.MinValue, SaveSerializer.SavedAtOf("basura"));
        }
    }
}
