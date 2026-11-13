using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El formato de la partida guardada (Fase 7): el sobre versionado (SaveData), su ida y vuelta a texto (SaveSerializer) y la
    // migración entre versiones (SaveMigrator). Todo sin archivos ni escena.
    public class SaveFormatTests
    {
        private static SaveData Sample()
        {
            var data = new SaveData { savedAtUtc = "2026-10-16T12:00:00.0000000Z" };
            data.Set("economy", "{\"version\":1,\"coins\":1234}");
            data.Set("backpack", "{\"version\":1,\"crops\":[\"wheat\",\"carrot\"]}");
            return data;
        }

        // ---------- SaveData ----------

        [Test]
        public void SaveData_Set_AddsNewSections_AndReplacesTheOnesThatExist()
        {
            var data = new SaveData();
            Assert.IsNull(data.Find("economy"));

            data.Set("economy", "A");
            data.Set("backpack", "B");
            data.Set("economy", "C");

            Assert.AreEqual(2, data.sections.Length);
            Assert.AreEqual("C", data.Find("economy").json);
            Assert.AreEqual("B", data.Find("backpack").json);
            Assert.IsNull(data.Find("nothing"));
            Assert.IsNull(data.Find(null));
            Assert.Throws<ArgumentException>(() => data.Set("", "x"));
        }

        // ---------- Ida y vuelta ----------

        [Test]
        public void Serialize_Then_Parse_KeepsEveryByteOfEverySection()
        {
            SaveData original = Sample();
            original.Set("tricky", "{\"name\":\"ñandú \\\"x\\\"\",\"line\":\"a\\nb\",\"emoji\":\"\\u00e9\"}");

            string text = SaveSerializer.Serialize(original);
            SaveParseResult result = SaveSerializer.Parse(text, SaveMigrator.Default);

            Assert.AreEqual(SaveParseStatus.Ok, result.Status);
            Assert.IsTrue(result.IsUsable);
            Assert.AreEqual(SaveData.CurrentVersion, result.Data.version);
            Assert.AreEqual(original.savedAtUtc, result.Data.savedAtUtc);
            Assert.AreEqual(3, result.Data.sections.Length);
            foreach (SaveSection section in original.sections)
            {
                Assert.AreEqual(section.json, result.Data.Find(section.id).json, "La sección '" + section.id + "' tiene que volver idéntica.");
            }

            Assert.AreEqual(text, SaveSerializer.Serialize(result.Data), "Guardar lo que se cargó da el mismo texto: el formato es estable.");
        }

        [Test]
        public void Parse_AnEmptySaveIsFine_AndUnknownExtraFieldsAreIgnored()
        {
            SaveParseResult empty = SaveSerializer.Parse("{\"version\":1,\"sections\":[]}", SaveMigrator.Default);
            Assert.AreEqual(SaveParseStatus.Ok, empty.Status);
            Assert.AreEqual(0, empty.Data.sections.Length);

            SaveParseResult extra = SaveSerializer.Parse("{\"version\":1,\"sections\":[],\"somethingNew\":5,\"other\":{\"a\":1}}", SaveMigrator.Default);
            Assert.AreEqual(SaveParseStatus.Ok, extra.Status);
        }

        // ---------- Datos dañados o vacíos ----------

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n\t ")]
        public void Parse_BlankText_IsEmpty(string text)
        {
            SaveParseResult result = SaveSerializer.Parse(text, SaveMigrator.Default);

            Assert.AreEqual(SaveParseStatus.Empty, result.Status);
            Assert.IsFalse(result.IsUsable);
            Assert.IsNull(result.Data);
        }

        [TestCase("not json at all")]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("[1,2,3]")]
        [TestCase("null")]
        [TestCase("42")]
        [TestCase("{}")]
        [TestCase("{\"hello\":\"world\"}")]
        [TestCase("{\"version\":0,\"sections\":[]}")]
        [TestCase("{\"version\":-3,\"sections\":[]}")]
        [TestCase("{\"version\":\"abc\",\"sections\":[]}")]
        [TestCase("{\"version\":1,\"sections\":\"nope\"}")]
        [TestCase("<html><body>404</body></html>")]
        public void Parse_DamagedOrForeignText_IsCorrupt_AndNeverThrows(string text)
        {
            SaveParseResult result = null;
            Assert.DoesNotThrow(() => result = SaveSerializer.Parse(text, SaveMigrator.Default));

            Assert.AreEqual(SaveParseStatus.Corrupt, result.Status, "Texto: " + text);
            Assert.IsFalse(result.IsUsable);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message), "Tiene que decir qué pasó.");
        }

        [Test]
        public void Parse_ATruncatedFile_IsNotTakenAsAGoodSave()
        {
            string text = SaveSerializer.Serialize(Sample());

            // Cortado en varios puntos, como si el celular se hubiera quedado sin batería a mitad de la escritura.
            for (int cut = 1; cut < text.Length; cut += Math.Max(1, text.Length / 17))
            {
                string truncated = text.Substring(0, cut);
                SaveParseResult result = SaveSerializer.Parse(truncated, SaveMigrator.Default);

                Assert.IsFalse(result.IsUsable, "Un archivo cortado en el byte " + cut + " no puede cargarse como partida válida: " + truncated);
            }
        }

        [Test]
        public void Parse_ASaveFromAFutureVersion_IsReportedAsSuch_NotAsDamaged()
        {
            SaveParseResult result = SaveSerializer.Parse("{\"version\":" + (SaveData.CurrentVersion + 5) + ",\"sections\":[{\"id\":\"x\",\"json\":\"{}\"}]}", SaveMigrator.Default);

            Assert.AreEqual(SaveParseStatus.Future, result.Status);
            Assert.IsFalse(result.IsUsable);
            StringAssert.Contains((SaveData.CurrentVersion + 5).ToString(), result.Message);
        }

        [Test]
        public void Parse_CleansNullEmptyAndDuplicatedSections()
        {
            const string text = "{\"version\":1,\"sections\":[{\"id\":\"a\",\"json\":\"1\"},{\"id\":\"\",\"json\":\"2\"},{\"id\":\"b\"},{\"id\":\"a\",\"json\":\"3\"}]}";
            SaveParseResult result = SaveSerializer.Parse(text, SaveMigrator.Default);

            Assert.AreEqual(SaveParseStatus.Ok, result.Status);
            Assert.AreEqual(2, result.Data.sections.Length, "Sin la de id vacío y con una sola 'a'.");
            Assert.AreEqual("3", result.Data.Find("a").json, "Si un id se repite se queda con la última.");
            Assert.AreEqual(string.Empty, result.Data.Find("b").json, "Una sección sin json queda vacía, no nula.");
        }

        // ---------- Migración ----------

        private static string TextOfVersion(int version)
        {
            var data = new SaveData { version = version };
            data.Set("economy", "{\"version\":1,\"coins\":5}");
            return SaveSerializer.Serialize(data);
        }

        [Test]
        public void Migrator_RunsOneStepPerVersion_InOrder_AndEndsOnTheCurrentVersion()
        {
            var order = new List<int>();
            var steps = new Dictionary<int, SaveMigrator.Step>
            {
                { 1, d => { order.Add(1); d.Set("added-in-2", "x"); return d; } },
                { 2, d => { order.Add(2); d.Set("added-in-3", "y"); return d; } }
            };
            var migrator = new SaveMigrator(3, steps);

            SaveParseResult result = SaveSerializer.Parse(TextOfVersion(1), migrator);

            Assert.AreEqual(SaveParseStatus.Migrated, result.Status);
            Assert.AreEqual(3, result.Data.version);
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
            Assert.IsNotNull(result.Data.Find("added-in-2"));
            Assert.IsNotNull(result.Data.Find("added-in-3"));
            Assert.AreEqual("{\"version\":1,\"coins\":5}", result.Data.Find("economy").json, "Lo que los pasos no tocan queda igual.");
        }

        [Test]
        public void Migrator_ASaveAlreadyOnTheCurrentVersion_IsNotMigrated()
        {
            var migrator = new SaveMigrator(3, new Dictionary<int, SaveMigrator.Step>
            {
                { 1, d => throw new InvalidOperationException("no tendría que correr") },
                { 2, d => throw new InvalidOperationException("no tendría que correr") }
            });

            SaveParseResult result = SaveSerializer.Parse(TextOfVersion(3), migrator);

            Assert.AreEqual(SaveParseStatus.Ok, result.Status);
        }

        [Test]
        public void Migrator_StartingFromTheMiddle_OnlyRunsTheRemainingSteps()
        {
            var order = new List<int>();
            var migrator = new SaveMigrator(3, new Dictionary<int, SaveMigrator.Step>
            {
                { 1, d => { order.Add(1); return d; } },
                { 2, d => { order.Add(2); return d; } }
            });

            SaveParseResult result = SaveSerializer.Parse(TextOfVersion(2), migrator);

            Assert.AreEqual(SaveParseStatus.Migrated, result.Status);
            CollectionAssert.AreEqual(new[] { 2 }, order);
        }

        [Test]
        public void Migrator_AStepMayReturnAnotherObject_AndTheMigratorSetsTheVersion()
        {
            var migrator = new SaveMigrator(2, new Dictionary<int, SaveMigrator.Step>
            {
                { 1, d => { var next = new SaveData { version = 1 }; next.Set("fresh", "z"); return next; } }   // se "olvida" de subir la versión
            });

            SaveParseResult result = SaveSerializer.Parse(TextOfVersion(1), migrator);

            Assert.AreEqual(SaveParseStatus.Migrated, result.Status);
            Assert.AreEqual(2, result.Data.version);
            Assert.IsNotNull(result.Data.Find("fresh"));
            Assert.IsNull(result.Data.Find("economy"), "Es el objeto nuevo el que sigue.");
        }

        [Test]
        public void Migrator_AMissingStep_IsReportedAsCorrupt_WithTheReason()
        {
            var migrator = new SaveMigrator(3, new Dictionary<int, SaveMigrator.Step> { { 2, d => d } });   // falta el paso 1 -> 2

            SaveParseResult result = SaveSerializer.Parse(TextOfVersion(1), migrator);

            Assert.AreEqual(SaveParseStatus.Corrupt, result.Status);
            StringAssert.Contains("1", result.Message);
        }

        [Test]
        public void Migrator_AStepThatFailsOrReturnsNothing_IsReportedAsCorrupt_NotAsACrash()
        {
            var throwing = new SaveMigrator(2, new Dictionary<int, SaveMigrator.Step> { { 1, d => throw new InvalidOperationException("boom") } });
            var nothing = new SaveMigrator(2, new Dictionary<int, SaveMigrator.Step> { { 1, d => null } });

            SaveParseResult first = null;
            SaveParseResult second = null;
            Assert.DoesNotThrow(() => first = SaveSerializer.Parse(TextOfVersion(1), throwing));
            Assert.DoesNotThrow(() => second = SaveSerializer.Parse(TextOfVersion(1), nothing));

            Assert.AreEqual(SaveParseStatus.Corrupt, first.Status);
            StringAssert.Contains("boom", first.Message);
            Assert.AreEqual(SaveParseStatus.Corrupt, second.Status);
        }

        [Test]
        public void Migrator_TryMigrate_LeavesTheDataUntouched_WhenItFails()
        {
            var migrator = new SaveMigrator(3, new Dictionary<int, SaveMigrator.Step> { { 1, d => d } });   // falta el 2 -> 3
            SaveData data = new SaveData { version = 1 };
            SaveData same = data;

            Assert.IsFalse(migrator.TryMigrate(ref data, out string error));
            Assert.AreSame(same, data);
            Assert.IsFalse(string.IsNullOrEmpty(error));

            SaveData future = new SaveData { version = 9 };
            Assert.IsFalse(migrator.TryMigrate(ref future, out error));

            SaveData nothing = null;
            Assert.IsFalse(migrator.TryMigrate(ref nothing, out error));
        }

        [Test]
        public void Migrator_RejectsANonsenseCurrentVersion()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveMigrator(0));
        }
    }
}
