using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Builds de RELEASE (Fase 9): lo que se sube a Google Play y a un portal web. A diferencia de los builds de prueba de
    // ProjectConfigurator (Development), acá NO hay Development Build: no existe el MockAdService (regala recompensas), el DebugPanel
    // se saca de la escena (ReleaseSceneStripper) y el contador de FPS no se crea.
    //   Menú:    FarmIdle > Release > Build Android (APK + AAB) / Build Web
    //   Consola: Unity.exe -batchmode -projectPath <ruta> -buildTarget Android -executeMethod FarmIdle.EditorTools.ReleaseBuilder.BuildAndroid
    //            Unity.exe -batchmode -projectPath <ruta> -buildTarget WebGL   -executeMethod FarmIdle.EditorTools.ReleaseBuilder.BuildWeb
    //
    // FIRMA (SECRETA, NO está en el repo): la clave de firma de Android se conecta por variables de entorno, nunca por archivos del proyecto.
    //   FARMIDLE_KEYSTORE_PATH   ruta al .keystore / .jks
    //   FARMIDLE_KEYSTORE_PASS   contraseña del almacén
    //   FARMIDLE_KEY_ALIAS       alias de la clave
    //   FARMIDLE_KEY_PASS        contraseña de la clave (si falta, se usa la del almacén)
    // Sin ellas el build sale firmado con la clave de DEBUG (sirve para probar en un celular, NO se puede subir a Play) y se avisa.
    // Versión: FARMIDLE_VERSION_NAME (por defecto ReleaseVersion.Name) y FARMIDLE_VERSION_CODE (por defecto ReleaseVersion.Code; en cada
    // subida a Play tiene que ser mayor que la anterior).
    public static class ReleaseBuilder
    {
        private const string ReleaseFolder = "Builds/Release";

        [MenuItem("FarmIdle/Release/Build Android (APK + AAB)")]
        public static void BuildAndroid()
        {
            RunBuild(() =>
            {
                ApplyCommonSettings();
                ApplyAndroidSettings(out string signingNote);
                string version = ReleaseVersion.Name + "-" + ReleaseVersion.Code;
                string folder = ReleaseFolder + "/Android";
                Directory.CreateDirectory(folder);

                // Primero el APK (para instalar a mano en un celular) y después el AAB (lo que pide Google Play).
                var results = new List<string>();
                foreach (bool bundle in new[] { false, true })
                {
                    EditorUserBuildSettings.buildAppBundle = bundle;
                    string path = folder + "/FarmIdle-" + version + (bundle ? ".aab" : ".apk");
                    results.Add(BuildPlayer(BuildTarget.Android, path));
                }

                EditorUserBuildSettings.buildAppBundle = false;   // los builds de prueba vuelven a generar APK

                string verified = VerifyNoDevelopmentTypes(folder + "/FarmIdle-" + version + "_BackUpThisFolder_ButDontShipItWithYourGame/Managed");
                Debug.Log("[FarmIdle] Release Android: " + string.Join(" | ", results) + " | " + signingNote + " | " + verified);
            }, restoreSigning: true);
        }

        [MenuItem("FarmIdle/Release/Build Web")]
        public static void BuildWeb()
        {
            RunBuild(() =>
            {
                ApplyCommonSettings();
                ApplyWebSettings();

                string path = ReleaseFolder + (WantsGzip() ? "/Web-Gzip" : "/Web");
                string result = BuildPlayer(BuildTarget.WebGL, path);
                Debug.Log("[FarmIdle] Release Web: " + result + " | " + DescribeFolderSizes(Path.Combine(path, "Build")));

                // La variante lista para CrazyGames (SDK + adaptador en el index.html) sale al lado, en <carpeta>-CrazyGames.
                PortalPackager.Pack(path);
            }, restoreSigning: false);
        }

        // ---------- Ajustes ----------

        // Lo que vale para los dos destinos: nombre, versión, ícono, splash y que la partida no lleve nada de desarrollo.
        private static void ApplyCommonSettings()
        {
            PlayerSettings.companyName = "FarmIdle";
            PlayerSettings.productName = "FarmIdle";
            PlayerSettings.bundleVersion = ReleaseVersion.Name;

            ReleaseArt.Apply();   // ícono y splash
            AssetDatabase.SaveAssets();
        }

        private static void ApplyAndroidSettings(out string signingNote)
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(android, "com.farmidle.prototype");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = ReleaseVersion.Code;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;   // el más alto instalado: Play exige uno reciente

            // Tamaño: código IL2CPP optimizado para tamaño y recorte administrado alto (ver Assets/link.xml: lo propio no se recorta).
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;

            // Los símbolos de IL2CPP salen aparte (un .zip junto al AAB): sirven para entender un cierre inesperado en Play Console.
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;

            // AdMob se enciende solo para Android (ver AdMobSetup). Los IDs salen de Assets/Resources/AdMobConfig (AdMobConfigSync los copia al
            // manifiesto antes del build).
            AdMobSetup.Enable();
            if (AdMobConfigSync.UsesTestIds())
            {
                Debug.LogWarning("[FarmIdle] Release Android con IDs de PRUEBA de AdMob: sirve para probar, pero para publicar hay que cargar los IDs reales en Assets/Resources/AdMobConfig y apagar 'Use Test Ads'.");
            }

            signingNote = ApplyAndroidSigning();
        }

        // La firma sale de variables de entorno; nada de esto se guarda en el proyecto (se limpia al terminar, ver RunBuild).
        private static string ApplyAndroidSigning()
        {
            string path = Environment.GetEnvironmentVariable("FARMIDLE_KEYSTORE_PATH");
            string storePass = Environment.GetEnvironmentVariable("FARMIDLE_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("FARMIDLE_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("FARMIDLE_KEY_PASS");

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(alias))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[FarmIdle] Release Android SIN clave de firma (faltan FARMIDLE_KEYSTORE_PATH / _PASS / FARMIDLE_KEY_ALIAS): se firma con la clave de DEBUG. Sirve para probar en un celular, pero Google Play NO lo acepta.");
                return "firmado con la clave de DEBUG (no se puede subir a Play)";
            }

            if (!File.Exists(path)) throw new FileNotFoundException("FARMIDLE_KEYSTORE_PATH apunta a un archivo que no existe.", path);

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? storePass : keyPass;
            return "firmado con la clave de release (alias " + alias + ")";
        }

        private static void ApplyWebSettings()
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;

            // El título de la pestaña y del portal (DEC-2): tiene que coincidir con el del menú (`menu.title`). Android conserva "FarmIdle"
            // hasta probar que el apóstrofo no rompe la etiqueta de la app.
            PlayerSettings.productName = "Pepe's Berenjena";
            PlayerSettings.defaultWebScreenWidth = 450;
            PlayerSettings.defaultWebScreenHeight = 800;
            PlayerSettings.WebGL.template = "PROJECT:FarmIdle";   // marco 9:16 a todo el alto de la ventana (la plantilla por defecto fija 450x800 y se corta)

            // Tamaño: Brotli, con el respaldo de descompresión en JS para hostings que no mandan Content-Encoding (se ve en RELEASE.md).
            // Para hostings sin Brotli: FARMIDLE_WEB_COMPRESSION=gzip (sale en Builds/Release/Web-Gzip).
            PlayerSettings.WebGL.compressionFormat = WantsGzip() ? WebGLCompressionFormat.Gzip : WebGLCompressionFormat.Brotli;
            // CrazyGames rechaza los `*.unityweb` ("Found .unityweb files... Decompression fallback"): su servidor ya manda Content-Encoding.
            // Por defecto sin respaldo (archivos `.br` / `.gz`); FARMIDLE_WEB_FALLBACK=on lo vuelve a activar para un hosting sin headers.
            PlayerSettings.WebGL.decompressionFallback = string.Equals(Environment.GetEnvironmentVariable("FARMIDLE_WEB_FALLBACK"), "on", StringComparison.OrdinalIgnoreCase);
            // Anuncios en la web: apagados en el Basic Launch (AdPolicy). FARMIDLE_WEB_ADS=on define FARMIDLE_WEB_ADS para el Full Launch.
            var webSymbols = PlayerSettings.GetScriptingDefineSymbols(web).Split(';').Where(s => s.Length > 0 && s != "FARMIDLE_WEB_ADS").ToList();
            if (string.Equals(Environment.GetEnvironmentVariable("FARMIDLE_WEB_ADS"), "on", StringComparison.OrdinalIgnoreCase)) webSymbols.Add("FARMIDLE_WEB_ADS");
            PlayerSettings.SetScriptingDefineSymbols(web, string.Join(";", webSymbols));
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            // Informe de tamaño del wasm (D-15): FARMIDLE_WEB_ANALYZE=on lo escribe en el log del build.
            PlayerSettings.WebGL.analyzeBuildSize = string.Equals(Environment.GetEnvironmentVariable("FARMIDLE_WEB_ANALYZE"), "on", StringComparison.OrdinalIgnoreCase);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;   // el guardado y los anuncios usan try/catch

            PlayerSettings.SetIl2CppCodeGeneration(web, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
        }

        private static bool WantsGzip()
        {
            return string.Equals(Environment.GetEnvironmentVariable("FARMIDLE_WEB_COMPRESSION"), "gzip", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- Construir ----------

        private static void RunBuild(Action build, bool restoreSigning)
        {
            int exitCode = 0;
            try
            {
                if (!ScenesExist()) throw new InvalidOperationException("Faltan las escenas. Ejecutá FarmIdle > Fase 2 > Construir assets y escenas.");
                build();
            }
            catch (Exception e)
            {
                Debug.LogError("[FarmIdle] Release: el build falló (" + e.Message + ").");
                exitCode = 1;
            }
            finally
            {
                if (restoreSigning)
                {
                    // Nada de la firma se queda en el proyecto.
                    PlayerSettings.Android.useCustomKeystore = false;
                    PlayerSettings.Android.keystoreName = "";
                    PlayerSettings.Android.keystorePass = "";
                    PlayerSettings.Android.keyaliasName = "";
                    PlayerSettings.Android.keyaliasPass = "";
                    EditorUserBuildSettings.buildAppBundle = false;
                }
            }

            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static string BuildPlayer(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths.All,   // Bootstrap primero
                locationPathName = path,
                target = target,
                options = BuildOptions.None   // SIN Development Build
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("BuildPlayer " + target + " terminó con " + summary.result + " (" + summary.totalErrors + " errores).");
            }

            long bytes = File.Exists(path) ? new FileInfo(path).Length : DirectorySize(path);
            return Path.GetFileName(path) + " " + (bytes / 1048576f).ToString("0.0") + " MB";
        }

        // Los tipos que son SOLO de desarrollo no pueden estar en las bibliotecas que quedaron en el build (las de FarmIdle, ya recortadas por
        // el linker, que Unity deja en la carpeta "BackUpThisFolder"). Si alguno aparece el release falla: el build no se puede subir.
        private static readonly string[] DevelopmentOnlyTypes = { "MockAdService", "DebugPanel", "FpsCounter" };

        private static string VerifyNoDevelopmentTypes(string managedFolder)
        {
            if (!Directory.Exists(managedFolder)) return "ADVERTENCIA: no se pudo verificar que no haya tipos de desarrollo (falta " + managedFolder + ")";

            var found = new List<string>();
            foreach (string dll in Directory.GetFiles(managedFolder, "FarmIdle.*.dll"))
            {
                string text = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(dll));
                foreach (string type in DevelopmentOnlyTypes)
                {
                    if (text.Contains(type)) found.Add(type + " en " + Path.GetFileName(dll));
                }
            }

            if (found.Count > 0) throw new InvalidOperationException("El release trae tipos de desarrollo: " + string.Join(", ", found));
            return "verificado: sin " + string.Join(", ", DevelopmentOnlyTypes) + " en las bibliotecas de FarmIdle";
        }

        private static bool ScenesExist()
        {
            return ScenePaths.All.All(File.Exists);
        }

        private static long DirectorySize(string folder)
        {
            return Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0L;
        }

        private static string DescribeFolderSizes(string folder)
        {
            if (!Directory.Exists(folder)) return "(sin carpeta Build)";

            return string.Join(", ", new DirectoryInfo(folder).GetFiles().Select(f => f.Name + " " + (f.Length / 1048576f).ToString("0.00") + " MB"));
        }
    }
}
