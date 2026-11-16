using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // La localización ES/EN (ROADMAP U-03): el lector del CSV, que la tabla esté completa y que no queden textos "duros" en el código.
    public class LocalizationTests
    {
        private static string ScriptsFolder => Path.Combine(Application.dataPath, "_Scripts");

        [SetUp]
        public void SetUp()
        {
            Loc.Unload();
            Loc.SetLanguage(GameLanguage.Spanish);
        }

        [TearDown]
        public void TearDown()
        {
            Loc.Unload();
            Loc.SetLanguage(GameLanguage.Spanish);
        }

        // ---------- El lector del CSV ----------

        [Test]
        public void Parse_ReadsQuotedCellsCommasEscapedQuotesAndLineBreaks()
        {
            const string csv = "key,es,en\r\n" +
                               "# un comentario\r\n" +
                               "a.simple,Hola,Hello\r\n" +
                               "b.comma,\"Uno, dos\",\"One, two\"\r\n" +
                               "c.quote,\"Dijo \"\"hola\"\"\",Said hi\r\n" +
                               "d.break,Linea1\\nLinea2,Line1\\nLine2\r\n" +
                               "\r\n" +
                               "e.onlyspanish,Solo\r\n";

            Dictionary<string, string[]> table = Loc.Parse(csv);

            Assert.AreEqual(5, table.Count, "El encabezado, el comentario y la fila vacía no cuentan.");
            Assert.AreEqual("Hello", table["a.simple"][1]);
            Assert.AreEqual("Uno, dos", table["b.comma"][0]);
            Assert.AreEqual("Dijo \"hola\"", table["c.quote"][0]);
            Assert.AreEqual("Linea1\nLinea2", table["d.break"][0]);
            Assert.AreEqual(string.Empty, table["e.onlyspanish"][1]);
        }

        [Test]
        public void Get_FollowsTheLanguage_FallsBackToSpanish_AndShowsTheKeyWhenMissing()
        {
            Loc.Load("key,es,en\nx.both,Hola,Hello\nx.spanish,Solo español,\n");

            Assert.AreEqual("Hola", Loc.Get("x.both"));
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("Hello", Loc.Get("x.both"));
            Assert.AreEqual("Solo español", Loc.Get("x.spanish"), "Sin traducción al inglés se usa el español.");
            Assert.AreEqual("x.missing", Loc.Get("x.missing"), "Sin clave se ve la clave.");
            Assert.AreEqual("respaldo", Loc.GetOr("x.missing", "respaldo"));
            Assert.AreEqual("Hello", Loc.GetOr("x.both", "respaldo"));
        }

        [Test]
        public void SetLanguage_NotifiesOnlyWhenTheLanguageReallyChanges()
        {
            int changes = 0;
            void Count() => changes++;
            Loc.Changed += Count;

            Loc.SetLanguage(GameLanguage.Spanish);
            Assert.AreEqual(0, changes);
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual(1, changes);

            Loc.Changed -= Count;
        }

        [Test]
        public void ResolveAuto_IsSpanishOnlyForASpanishSystem()
        {
            Loc.SystemLanguageOverride = SystemLanguage.Spanish;
            Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto());
            Loc.SystemLanguageOverride = SystemLanguage.German;
            Assert.AreEqual(GameLanguage.English, Loc.ResolveAuto());
            Loc.SystemLanguageOverride = null;
        }

        // ---------- La tabla real ----------

        [Test]
        public void TheTable_HasASpanishAndAnEnglishTextForEveryKey_WithTheSamePlaceholders()
        {
            Dictionary<string, string[]> table = Loc.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "Localization.csv")));
            Assert.Greater(table.Count, 50);

            var placeholders = new Regex(@"\{\d\}");
            foreach (KeyValuePair<string, string[]> row in table)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.Value[0]), "Falta el español de " + row.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.Value[1]), "Falta el inglés de " + row.Key);

                string[] inSpanish = placeholders.Matches(row.Value[0]).Select(m => m.Value).OrderBy(v => v).ToArray();
                string[] inEnglish = placeholders.Matches(row.Value[1]).Select(m => m.Value).OrderBy(v => v).ToArray();
                CollectionAssert.AreEqual(inSpanish, inEnglish, "Los {0} de " + row.Key + " no coinciden entre idiomas.");
            }
        }

        [Test]
        public void TheTable_IsFoundByResources_AndWorksWithoutAScene()
        {
            Assert.AreEqual("Ajustes", Loc.Get("settings.title"));
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("Settings", Loc.Get("settings.title"));
            Assert.AreEqual("Level 7!", Loc.Format("hud.level_up", 7));
        }

        [Test]
        public void EveryKeyUsedInTheCode_ExistsInTheTable()
        {
            var keyPatterns = new[]
            {
                new Regex(@"Loc\.(?:Get|GetOr|Format|Has)\(\s*""([^""]+)"""),
                new Regex(@"""@([a-z_]+\.[a-z_]+)"""),
                new Regex(@"AddLocalizedKey\([^,]+,\s*""([^""]+)"""),
                new Regex(@"""((?:menu|settings|hud|pad|ad|fmt|guide)\.[a-z_]+)""")   // también las claves dentro de un ternario
            };
            var used = new List<string>();
            foreach (string file in Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Regex pattern in keyPatterns)
                {
                    foreach (Match match in pattern.Matches(text)) used.Add(Path.GetFileName(file) + ": " + match.Groups[1].Value);
                }
            }

            Assert.Greater(used.Count, 40, "El escaneo tiene que encontrar las claves usadas.");
            string[] missing = used.Where(entry => !Loc.Has(entry.Substring(entry.IndexOf(": ", System.StringComparison.Ordinal) + 2))).ToArray();
            Assert.IsEmpty(missing, "Claves usadas en el código que no están en la tabla: " + string.Join(", ", missing));
        }

        private sealed class FakeGuidePoint : IGuidePoint
        {
            public int Priority;
            public Vector3 Position => Vector3.zero;
            public int GuidePriority => Priority;
            public bool WantsGuide => true;
        }

        // U-10: cada paso de la guía y cada lugar de "conviene ir" tiene su cartel, en los dos idiomas.
        [Test]
        public void EveryGuideStepAndPoint_HasAHintInTheTable()
        {
            var keys = new List<string>();
            foreach (TutorialStep step in new[] { TutorialStep.Sow, TutorialStep.Harvest, TutorialStep.Sell, TutorialStep.Buy })
            {
                string key = TutorialEdgeArrow.TutorialHintKey(step);
                Assert.IsNotNull(key, "El paso " + step + " no tiene cartel.");
                keys.Add(key);
            }

            Assert.IsNull(TutorialEdgeArrow.TutorialHintKey(TutorialStep.Done), "Terminada la guía no hay cartel.");
            Assert.IsNull(TutorialEdgeArrow.GuideHintKey(null));

            string sell = TutorialEdgeArrow.GuideHintKey(new FakeGuidePoint { Priority = GuidePoints.SellPriority });
            string buy = TutorialEdgeArrow.GuideHintKey(new FakeGuidePoint { Priority = GuidePoints.BuyPriority });
            Assert.AreNotEqual(sell, buy, "Vender y comprar dicen cosas distintas.");
            keys.Add(sell);
            keys.Add(buy);

            string fresh = TutorialEdgeArrow.GuideHintKey(new FakeGuidePoint { Priority = GuidePoints.NewItemPriority });
            Assert.AreNotEqual(sell, fresh, "Lo recién comprado tiene su propio cartel.");
            keys.Add(fresh);

            foreach (string key in keys)
            {
                Assert.IsTrue(Loc.Has(key), "Falta en la tabla: " + key);
                Loc.SetLanguage(GameLanguage.Spanish);
                string spanish = Loc.Get(key);
                Loc.SetLanguage(GameLanguage.English);
                Assert.AreNotEqual(spanish, Loc.Get(key), "El cartel '" + key + "' tiene que estar en los dos idiomas.");
            }
        }

        [Test]
        public void EveryGameDataAsset_HasItsNameInTheTable()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDataAsset");
            Assert.Greater(guids.Length, 10);

            var missing = new List<string>();
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !Loc.Has("data." + asset.Id)) missing.Add("data." + asset.Id);
            }

            Assert.IsEmpty(missing, "Datos sin nombre en la tabla: " + string.Join(", ", missing));
        }

        [Test]
        public void TheDataNames_FollowTheLanguage()
        {
            string[] guids = AssetDatabase.FindAssets("t:CropData");
            CropData wheat = guids.Select(guid => AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid))).First(crop => crop.Id == "wheat");

            Assert.AreEqual("Trigo", wheat.DisplayName);
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("Wheat", wheat.DisplayName);
        }

        [Test]
        public void TheUpgradeAndPerkDescriptions_FollowTheLanguage()
        {
            var perk = ScriptableObject.CreateInstance<PerkData>();
            SetField(perk, "kind", PerkKind.BackpackCapacity);
            SetField(perk, "valuePerPick", 1f);
            Assert.AreEqual("+1 lugar", perk.DescribePick());
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("+1 slot", perk.DescribePick());

            SetField(perk, "kind", PerkKind.MoveSpeed);
            SetField(perk, "valuePerPick", 0.015f);
            Assert.AreEqual("+1.5%", perk.DescribePick(), "En inglés, punto decimal y sin espacio.");
            Loc.SetLanguage(GameLanguage.Spanish);
            Assert.AreEqual("+1,5 %", perk.DescribePick());

            Object.DestroyImmediate(perk);
        }

        private static void SetField(Object target, string name, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (value is float f) property.floatValue = f;
            else property.enumValueIndex = (int)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Cero textos "duros" ----------

        // Un literal cuenta como texto visible si, sin etiquetas de TextMeshPro ni {0}, le quedan dos letras o más.
        private static bool LooksLikeText(string literal)
        {
            if (Regex.IsMatch(literal, @"^[a-z_]+(\.[a-z_]+)+$")) return false;   // una clave de la tabla, no un texto

            string stripped = Regex.Replace(literal, @"<[^>]*>|\{\d\}|\\[nrt]", string.Empty);
            return Regex.Matches(stripped, @"[A-Za-zÁÉÍÓÚÜÑáéíóúüñ¡¿]").Count >= 2;
        }

        // Saca el comentario "// ..." del final de una línea (sin confundir un "//" dentro de un texto).
        private static string WithoutComment(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length - 1; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
                if (!inString && line[i] == '/' && line[i + 1] == '/') return line.Substring(0, i);
            }

            return line;
        }

        [Test]
        public void TheGameCode_HasNoHardCodedPlayerFacingText()
        {
            // Dónde se arma texto que ve el jugador. DebugPanel es solo de desarrollo (no se traduce).
            string[] folders = { "UI", "Environment", "Player", "Data" };
            var patterns = new[]
            {
                new Regex(@"\.Append\(\s*""((?:[^""\\]|\\.)*)"""),
                new Regex(@"\.text\s*=[^;]*?""((?:[^""\\]|\\.)*)"""),
                new Regex(@"\breturn\s+""((?:[^""\\]|\\.)*)"""),
                new Regex(@"\.SetText\(\s*""((?:[^""\\]|\\.)*)""")
            };

            var offenders = new List<string>();
            foreach (string folder in folders)
            {
                foreach (string file in Directory.GetFiles(Path.Combine(ScriptsFolder, folder), "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    if (name == "DebugPanel.cs") continue;

                    int number = 0;
                    foreach (string rawLine in File.ReadAllLines(file))
                    {
                        number++;
                        string line = WithoutComment(rawLine);
                        if (line.Contains("Debug.Log") || line.Contains("Tooltip") || line.Contains("throw ")) continue;

                        foreach (Regex pattern in patterns)
                        {
                            foreach (Match match in pattern.Matches(line))
                            {
                                if (LooksLikeText(match.Groups[1].Value)) offenders.Add(name + ":" + number + " \"" + match.Groups[1].Value + "\"");
                            }
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders, "Textos escritos a mano (usar Loc con una clave de la tabla): " + string.Join(" | ", offenders));
        }

        [Test]
        public void TheSceneBuilders_CreateEveryVisibleLabelFromAKey()
        {
            // CreateText(nombre, padre, TEXTO, ...) y CreateButton(nombre, padre, TEXTO, ...): el texto es "@clave" o no tiene letras.
            // AddCardButton(padre, nombre, TEXTO, ...) y AddSliderRow(padre, TEXTO, ...) igual. Los botones de debug no se traducen.
            var patterns = new[]
            {
                new Regex(@"EditorUi\.Create(?:Text|Button)\(\s*[^,]+,\s*[^,]+,\s*""((?:[^""\\]|\\.)*)"""),
                new Regex(@"AddCardButton\(\s*[^,]+,\s*[^,]+,\s*""((?:[^""\\]|\\.)*)"""),
                new Regex(@"AddSliderRow\(\s*[^,]+,\s*""((?:[^""\\]|\\.)*)""")
            };

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(ScriptsFolder, "Editor"), "*.cs"))
            {
                int number = 0;
                foreach (string rawLine in File.ReadAllLines(file))
                {
                    number++;
                    string line = WithoutComment(rawLine);
                    foreach (Regex pattern in patterns)
                    {
                        foreach (Match match in pattern.Matches(line))
                        {
                            string text = match.Groups[1].Value;
                            if (!text.StartsWith("@") && LooksLikeText(text)) offenders.Add(Path.GetFileName(file) + ":" + number + " \"" + text + "\"");
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders, "Textos del builder sin clave (usar \"@clave\"): " + string.Join(" | ", offenders));
        }
    }
}
