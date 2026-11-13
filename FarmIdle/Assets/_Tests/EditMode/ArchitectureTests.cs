using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Hace cumplir las capas acordadas leyendo los .asmdef: cada capa solo puede referenciar a las de abajo.
    //   Core y Data no dependen de nadie · Managers usa Core + Data · Player, UI y Environment usan Managers
    //   (+ Core + Data) y no se conocen entre sí: hablan por las interfaces de Core (IActor, IInteractable) y por
    //   los servicios de Managers (PlotCensus, PoolManager...). El código del Editor puede ver todo, pero nadie
    //   puede depender de él.
    // El compilador ya lo impide mientras el .asmdef esté bien; este test avisa si alguien agrega la referencia prohibida.
    public class ArchitectureTests
    {
        [Serializable]
        private class AsmdefFile
        {
            public string name;
            public string[] references;
        }

        // Para sumar una capa nueva hay que declararla acá.
        private static readonly Dictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>
        {
            { "FarmIdle.Core", new string[0] },
            { "FarmIdle.Data", new string[0] },
            { "FarmIdle.Managers", new[] { "FarmIdle.Core", "FarmIdle.Data" } },
            { "FarmIdle.Player", new[] { "FarmIdle.Core", "FarmIdle.Data", "FarmIdle.Managers" } },
            { "FarmIdle.UI", new[] { "FarmIdle.Core", "FarmIdle.Data", "FarmIdle.Managers" } },
            { "FarmIdle.Environment", new[] { "FarmIdle.Core", "FarmIdle.Data", "FarmIdle.Managers" } },
        };

        private static readonly string[] SiblingLayers = { "FarmIdle.Player", "FarmIdle.UI", "FarmIdle.Environment" };

        private static List<AsmdefFile> ReadRuntimeAsmdefs()
        {
            string scriptsFolder = Path.Combine(Application.dataPath, "_Scripts");
            string[] files = Directory.GetFiles(scriptsFolder, "*.asmdef", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 0, "No se encontraron .asmdef en Assets/_Scripts.");

            var asmdefs = new List<AsmdefFile>();
            foreach (string file in files)
            {
                var asmdef = JsonUtility.FromJson<AsmdefFile>(File.ReadAllText(file));
                if (asmdef.name == "FarmIdle.Editor") continue;

                asmdef.references = asmdef.references ?? new string[0];
                asmdefs.Add(asmdef);
            }

            return asmdefs;
        }

        [Test]
        public void EachRuntimeLayer_OnlyReferencesTheLayersBelowIt()
        {
            foreach (AsmdefFile asmdef in ReadRuntimeAsmdefs())
            {
                Assert.IsTrue(AllowedReferences.TryGetValue(asmdef.name, out string[] allowed),
                    asmdef.name + " no está en la tabla de capas de ArchitectureTests: declarale qué puede referenciar.");

                foreach (string reference in asmdef.references)
                {
                    Assert.IsFalse(reference.StartsWith("GUID:"), asmdef.name + " referencia por GUID; usá el nombre del ensamblado para que se pueda verificar.");

                    // Los paquetes de Unity (InputSystem, TextMeshPro...) no son capas del proyecto.
                    if (!reference.StartsWith("FarmIdle.")) continue;

                    Assert.Contains(reference, allowed, asmdef.name + " no puede depender de " + reference + ".");
                }
            }
        }

        // La regla de la Fase 3 dicha en voz alta (la tabla de arriba ya la cubre, pero así el mensaje es directo):
        // el jugador, la UI y el entorno son hermanos y no se referencian entre sí.
        [Test]
        public void PlayerUiAndEnvironment_NeverReferenceEachOther()
        {
            foreach (AsmdefFile asmdef in ReadRuntimeAsmdefs())
            {
                if (Array.IndexOf(SiblingLayers, asmdef.name) < 0) continue;

                foreach (string sibling in SiblingLayers)
                {
                    if (sibling == asmdef.name) continue;

                    CollectionAssert.DoesNotContain(asmdef.references, sibling,
                        asmdef.name + " no puede conocer a " + sibling + ": se hablan por Core (interfaces) y Managers (servicios).");
                }
            }
        }

        // La lógica "pura" (billetera, mochila, precios, progreso, ritmo de venta y de pago, reglas de la parcela) no usa UnityEngine: se prueba sin escena y no sabe
        // nada de vistas. El compilador no lo impide (el ensamblado igual referencia UnityEngine), así que se mira el código.
        [TestCase("Managers/Wallet.cs")]
        [TestCase("Managers/Inventory.cs")]
        [TestCase("Managers/PriceCalculator.cs")]
        [TestCase("Managers/PlayerProgress.cs")]
        [TestCase("Managers/ProgressionModifiers.cs")]
        [TestCase("Managers/LevelCurve.cs")]
        [TestCase("Managers/SaveData.cs")]
        [TestCase("Managers/SaveMigrator.cs")]
        [TestCase("Managers/SaveFile.cs")]
        [TestCase("Managers/BuffTracker.cs")]
        [TestCase("Managers/CooldownTracker.cs")]
        [TestCase("Managers/OfferTimer.cs")]
        [TestCase("Managers/AdSession.cs")]
        [TestCase("Managers/MoveSpeedRules.cs")]
        [TestCase("Managers/FertilizerStock.cs")]
        [TestCase("Managers/MockAdService.cs")]
        [TestCase("Managers/NoAdService.cs")]
        [TestCase("Managers/ConsentFlow.cs")]
        [TestCase("Managers/AudioSettings.cs")]
        [TestCase("Managers/TutorialProgress.cs")]
        [TestCase("Managers/SoundThrottle.cs")]
        [TestCase("Managers/PulseThrottle.cs")]
        [TestCase("Managers/IVibrator.cs")]
        [TestCase("Managers/NullAnalytics.cs")]
        [TestCase("Managers/AnalyticsEvents.cs")]
        [TestCase("Environment/SalePacer.cs")]
        [TestCase("Environment/PurchaseDrip.cs")]
        [TestCase("Environment/PlotLogic.cs")]
        public void PureLogicClasses_DoNotUseUnityEngine(string relativePath)
        {
            // Se busca por nombre de archivo: Managers está en subcarpetas por dominio y un archivo puede cambiar de carpeta.
            string[] found = Directory.GetFiles(Path.Combine(Application.dataPath, "_Scripts"), Path.GetFileName(relativePath), SearchOption.AllDirectories);
            Assert.AreEqual(1, found.Length, relativePath + ": se esperaba exactamente un archivo con ese nombre.");
            string source = File.ReadAllText(found[0]);

            // Se quitan los comentarios: los de arriba explican justamente que no usa UnityEngine.
            string code = Regex.Replace(source, @"//[^\r\n]*", "");
            Assert.IsFalse(Regex.IsMatch(code, @"\bUnityEngine\b"), relativePath + " es lógica pura y no puede usar UnityEngine.");
        }

        // AdsManager es la ÚNICA puerta para pedir un anuncio: ningún otro código (UI, jugador, entorno, otros servicios) le pide un
        // anuncio a un IAdService. Se mira el código fuente buscando la llamada.
        [Test]
        public void OnlyTheAdsManager_AsksAnAdServiceToShowAnAd()
        {
            string[] allowed = { "AdsManager.cs", "IAdService.cs", "MockAdService.cs", "WebAdService.cs", "AdMobAdService.cs" };
            string scripts = Path.Combine(Application.dataPath, "_Scripts");

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (Array.IndexOf(allowed, Path.GetFileName(file)) >= 0) continue;

                string code = Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", "");
                Assert.IsFalse(Regex.IsMatch(code, @"\.ShowRewarded\s*\("), Path.GetFileName(file) + " no puede mostrar un anuncio por su cuenta: se pide a AdsManager.TryRequest.");
            }
        }

        // B-03: ningún anuncio se dispara solo. Los únicos que piden uno a AdsManager son un gesto del jugador: tocar un botón de la
        // tarjeta (OfflineRewardView) o pararse en un pad de video (RewardPad). El símbolo de oferta y las misiones no pueden pedirlo.
        [Test]
        public void OnlyPlayerGestures_RequestARewardedAd()
        {
            string[] allowed = { "AdsManager.cs", "OfflineRewardView.cs", "RewardPad.cs" };
            string scripts = Path.Combine(Application.dataPath, "_Scripts");

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (Array.IndexOf(allowed, Path.GetFileName(file)) >= 0) continue;

                string code = Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", "");
                Assert.IsFalse(Regex.IsMatch(code, @"\.TryRequest\s*\("), Path.GetFileName(file) + " no puede pedir un anuncio: tiene que nacer de un gesto del jugador (botón o pad).");
            }
        }

        // AR-03. Código del juego = todo .cs de Assets/_Scripts fuera de la carpeta Editor. Se quitan los comentarios antes de mirar.
        private static IEnumerable<KeyValuePair<string, string>> RuntimeSources()
        {
            string scripts = Path.Combine(Application.dataPath, "_Scripts");
            string editorFolder = Path.Combine(scripts, "Editor") + Path.DirectorySeparatorChar;

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(editorFolder, StringComparison.OrdinalIgnoreCase)) continue;

                yield return new KeyValuePair<string, string>(Path.GetFileName(file), Regex.Replace(File.ReadAllText(file), @"//[^\r\n]*", ""));
            }
        }

        // Sin Instantiate en juego: todo objeto repetido sale del pool. PoolManager es el único que clona prefabs.
        [Test]
        public void OnlyThePoolManager_InstantiatesInGameCode()
        {
            foreach (KeyValuePair<string, string> source in RuntimeSources())
            {
                if (source.Key == "PoolManager.cs") continue;

                Assert.IsFalse(Regex.IsMatch(source.Value, @"\bInstantiate\s*[<(]"), source.Key + " llama a Instantiate: pedile el objeto al PoolManager.");
            }
        }

        // Buscar objetos por tipo recorre toda la escena; en el juego se resuelve por referencia serializada o por servicio.
        [Test]
        public void GameCode_NeverSearchesTheSceneByType()
        {
            foreach (KeyValuePair<string, string> source in RuntimeSources())
            {
                Assert.IsFalse(Regex.IsMatch(source.Value, @"\bFind(Object|FirstObject|AnyObject)(s)?(OfType|ByType)\b"),
                    source.Key + " busca objetos por tipo en la escena: usá una referencia serializada o un servicio.");
            }
        }

        // UnityEditor no existe en un build: solo el código de la carpeta Editor puede usarlo.
        [Test]
        public void UnityEditor_IsOnlyUsedInTheEditorFolder()
        {
            foreach (KeyValuePair<string, string> source in RuntimeSources())
            {
                Assert.IsFalse(Regex.IsMatch(source.Value, @"\bUnityEditor\b"), source.Key + " usa UnityEditor fuera de la carpeta Editor.");
            }
        }

        // Archivos de más de 500 líneas: no se agregan más. DebugPanel ya estaba pasado al fijar la regla y es solo de depuración
        // (los builders de las Fases 6 y 9 salieron al partirse por responsabilidad, AR-05). La lista no puede quedar con un archivo
        // que ya no la necesita.
        private const int MaxLines = 500;
        private static readonly string[] OversizedBeforeTheRule = { "DebugPanel.cs" };

        [Test]
        public void NoScriptGrowsPast500Lines_ExceptTheKnownBuilders()
        {
            string scripts = Path.Combine(Application.dataPath, "_Scripts");

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                int lines = File.ReadAllLines(file).Length;
                bool known = Array.IndexOf(OversizedBeforeTheRule, name) >= 0;

                if (known) Assert.Greater(lines, MaxLines, name + " ya baja de " + MaxLines + " líneas: sacalo de OversizedBeforeTheRule.");
                else Assert.LessOrEqual(lines, MaxLines, name + " tiene " + lines + " líneas (tope " + MaxLines + "): partilo por responsabilidad.");
            }
        }

        [Test]
        public void EveryAllowedLayer_HasItsAsmdef()
        {
            var found = new HashSet<string>();
            foreach (AsmdefFile asmdef in ReadRuntimeAsmdefs()) found.Add(asmdef.name);

            foreach (string layer in AllowedReferences.Keys)
            {
                Assert.IsTrue(found.Contains(layer), "La tabla declara " + layer + " pero no existe su .asmdef en Assets/_Scripts.");
            }
        }
    }
}
