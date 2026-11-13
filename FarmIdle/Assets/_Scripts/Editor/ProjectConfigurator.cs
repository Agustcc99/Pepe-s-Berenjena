using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Configuración del proyecto y builds de prueba.
    // Se usa desde el menú "FarmIdle" del Editor o por línea de comandos:
    //   Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.ProjectConfigurator.ConfigureProject
    public static class ProjectConfigurator
    {
        [MenuItem("FarmIdle/Configurar proyecto")]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "FarmIdle";
            PlayerSettings.productName = "FarmIdle";

            // Móvil: vertical, IL2CPP y ARM64.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.farmidle.prototype");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Web: lienzo vertical y sin compresión mientras se desarrolla.
            PlayerSettings.defaultWebScreenWidth = 450;
            PlayerSettings.defaultWebScreenHeight = 800;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] Proyecto configurado.");
        }

        [MenuItem("FarmIdle/Build Web (Development)")]
        public static void BuildWeb()
        {
            Build(BuildTarget.WebGL, "Builds/Web");
        }

        [MenuItem("FarmIdle/Build Android (Development)")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, "Builds/Android/FarmIdle.apk");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            foreach (string scene in ScenePaths.All)
            {
                if (File.Exists(scene)) continue;

                Debug.LogError("[FarmIdle] Falta la escena " + scene + ". Ejecutá FarmIdle > Fase 2 > Construir assets y escenas.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // Los builds de prueba no se comprimen (compilan más rápido y se sirven con cualquier servidor). La compresión es del release
            // (ReleaseBuilder) y deja su ajuste puesto: acá se vuelve a apagar.
            if (target == BuildTarget.WebGL)
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = true;
            }

            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths.All,   // Bootstrap primero
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[FarmIdle] Build " + target + ": " + summary.result + " (" + summary.totalErrors + " errores, " + summary.totalSize / (1024 * 1024) + " MB)");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
