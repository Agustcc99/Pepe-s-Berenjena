using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FarmIdle.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmIdle.Tests
{
    // "Un build final NO trae cosas de desarrollo": ni el panel de debug, ni el contador de FPS, ni el anuncio simulado que regala
    // recompensas. Se vigila por tres lados: las clases de desarrollo solo se compilan en el Editor y en Development Build (define
    // DEVELOPMENT_BUILD), el objeto del panel se saca de las escenas al construir un release, y el servicio de anuncios de un build final
    // sin SDK nunca regala nada. Además, ReleaseBuilder verifica el APK real (ver docs/RELEASE.md: revisión de nombres de tipos).
    public class ReleaseBuildTests
    {
        private static readonly string[] DevelopmentOnlyTypes = { "MockAdService", "DebugPanel", "FpsCounter" };

        private static string ScriptsFolder => Path.Combine(Application.dataPath, "_Scripts");

        // ---------- Las clases de desarrollo solo se compilan en desarrollo ----------

        // El código sin comentarios, línea por línea, con la condición (#if / #elif / #else) de la rama en que está cada una.
        private static IEnumerable<(int line, string code, List<string> conditions)> CodeWithConditions(string file)
        {
            var stack = new List<string>();
            string[] lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                string code = Regex.Replace(lines[i], @"//.*$", "").Trim();

                if (code.StartsWith("#if")) stack.Add(code.Substring(3).Trim());
                else if (code.StartsWith("#elif") && stack.Count > 0) stack[stack.Count - 1] = code.Substring(5).Trim();
                else if (code.StartsWith("#else") && stack.Count > 0) stack[stack.Count - 1] = "#else";
                else if (code.StartsWith("#endif") && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                else yield return (i + 1, code, new List<string>(stack));
            }
        }

        // Una rama "de desarrollo": se compila solo con DEVELOPMENT_BUILD o solo en el Editor (UNITY_EDITOR sin negar).
        private static bool IsDevelopmentBranch(string condition)
        {
            if (condition.Contains("DEVELOPMENT_BUILD")) return true;

            string withoutNegated = condition.Replace("!UNITY_EDITOR", "");
            return withoutNegated.Contains("UNITY_EDITOR");
        }

        [Test]
        public void TheDevelopmentOnlyTypes_AreOnlyCompiledInTheEditorAndDevelopmentBuilds()
        {
            string[] runtimeFiles = Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Replace('\\', '/').Contains("/Editor/")).ToArray();
            Assert.Greater(runtimeFiles.Length, 20);

            foreach (string file in runtimeFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                bool declaresIt = DevelopmentOnlyTypes.Contains(fileName);

                foreach ((int line, string code, List<string> conditions) in CodeWithConditions(file))
                {
                    foreach (string type in DevelopmentOnlyTypes)
                    {
                        if (!Regex.IsMatch(code, @"\b" + type + @"\b")) continue;

                        // Una cadena de texto que solo menciona el nombre (un log) no es una referencia al tipo.
                        if (Regex.IsMatch(Regex.Replace(code, "\"[^\"]*\"", ""), @"\b" + type + @"\b") == false) continue;

                        bool guarded = conditions.Any(IsDevelopmentBranch);
                        Assert.IsTrue(guarded, Path.GetFileName(file) + ":" + line + " usa " + type + " fuera de #if UNITY_EDITOR || DEVELOPMENT_BUILD: un build final lo compilaría.");
                    }
                }

                if (declaresIt)
                {
                    // Cada archivo que declara uno de esos tipos está entero dentro del #if (y el #endif es lo último).
                    string[] lines = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//")).ToArray();
                    Assert.AreEqual("#if UNITY_EDITOR || DEVELOPMENT_BUILD", lines.First(), fileName + ".cs tiene que empezar con #if UNITY_EDITOR || DEVELOPMENT_BUILD.");
                    Assert.AreEqual("#endif", lines.Last(), fileName + ".cs tiene que terminar con el #endif de ese #if.");
                }
            }
        }

        [Test]
        public void TheWebBridge_OnlyAllowsTheSimulatedAd_InDevelopmentOrWhenThePageAsksForIt()
        {
            string jslib = File.ReadAllText(Path.Combine(Application.dataPath, "Plugins", "WebGL", "FarmIdleWeb.jslib"));

            StringAssert.Contains("FI_ConfigureSimulatedAds", jslib);
            StringAssert.Contains("window.FarmIdleAllowSimAds === true", jslib, "La página tiene que poder pedir la simulación a propósito.");

            // Sin permiso, el anuncio simulado devuelve 'nofill' antes de dibujar el overlay.
            int guard = jslib.IndexOf("done('nofill');", StringComparison.Ordinal);
            int overlay = jslib.IndexOf("document.createElement('div')", StringComparison.Ordinal);
            Assert.Greater(guard, 0, "Falta el corte 'nofill' sin permiso.");
            Assert.Less(guard, overlay, "El corte tiene que ir antes de armar el overlay simulado.");

            string adsManager = File.ReadAllText(Path.Combine(ScriptsFolder, "Managers", "Rewards", "AdsManager.cs"));
            StringAssert.Contains("allowSimulatedAds: Debug.isDebugBuild", adsManager);
        }

        // ---------- El objeto del panel se saca de las escenas ----------

        [Test]
        public void TheSceneStripper_RemovesTheDebugPanel_AndKeepsTheRestOfTheHud()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + SceneNames.MainLevel + ".unity", OpenSceneMode.Additive);
            try
            {
                Assert.IsTrue(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DebugPanel>(true)).Any(), "El nivel generado trae el panel de debug.");

                int removed = ReleaseSceneStripper.StripDevelopmentObjects(scene);

                Assert.AreEqual(1, removed);
                Assert.IsFalse(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DebugPanel>(true)).Any(), "Ya no queda ningún panel.");

                // El resto del HUD sigue (contador de monedas, barra de nivel, tarjeta de sonido...).
                Assert.IsTrue(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoinCounterView>(true)).Any());
                Assert.IsTrue(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SettingsPanelView>(true)).Any());
                Assert.IsTrue(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TapToPlayOverlay>(true)).Any());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);   // sin guardar: el cambio es solo de la prueba
            }
        }

        // ---------- Un build final sin SDK no regala nada ----------

        [Test]
        public void TheNoAdService_IsNeverReady_AndAnswersNoFillWithoutRewarding()
        {
            var service = new NoAdService();
            var results = new List<AdResult>();

            Assert.IsFalse(service.IsReady("reward_turbo"));
            service.ShowRewarded("reward_turbo", results.Add);
            service.Tick(1f);

            CollectionAssert.AreEqual(new[] { AdResult.NoFill }, results);
            Assert.Throws<ArgumentNullException>(() => service.ShowRewarded("x", null));
        }

        [Test]
        public void TheRelease_HasAVersion_AndNeverStoresSigningSecretsInTheProject()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion));
            Assert.Greater(PlayerSettings.Android.bundleVersionCode, 0);

            // La firma sale de variables de entorno: el proyecto no guarda contraseñas ni la ruta de un almacén.
            string settings = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "ProjectSettings.asset"));
            Assert.IsFalse(Regex.IsMatch(settings, @"AndroidKeystorePass:[ \t]*\S"), "Hay una contraseña de almacén guardada en ProjectSettings.");
            Assert.IsFalse(Regex.IsMatch(settings, @"AndroidKeyaliasPass:[ \t]*\S"), "Hay una contraseña de clave guardada en ProjectSettings.");
            // Un nombre vacío Unity lo guarda como '{inproject}: ' (el prefijo que usa para rutas dentro del proyecto, sin ruta).
            Match keystore = Regex.Match(settings, @"AndroidKeystoreName:[ \t]*(.*)");
            Assert.IsTrue(keystore.Success);
            Assert.IsTrue(keystore.Groups[1].Value.Trim() == "" || keystore.Groups[1].Value.Trim() == "'{inproject}: '", "Hay una ruta de almacén guardada en ProjectSettings: " + keystore.Groups[1].Value);
            Assert.IsFalse(Regex.IsMatch(settings, @"AndroidKeyaliasName:[ \t]*\S"), "Hay un alias de clave guardado en ProjectSettings.");
        }
    }
}
