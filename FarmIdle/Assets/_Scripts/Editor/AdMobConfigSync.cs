using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Copia el ID de la APP de AdMob de Assets/Resources/AdMobConfig (el único lugar donde se cambian los IDs) al archivo de ajustes del
    // plugin (GoogleMobileAdsSettings), que es de donde el plugin lo toma para el manifiesto de Android. Se hace solo antes de cada build
    // de Android, así nadie tiene que acordarse de cambiar un segundo lugar. Si hay IDs reales cargados pero con formato inválido, el
    // build FALLA (mejor eso que publicar con un ID roto); con los de prueba avisa.
    //   Menú: FarmIdle > Fase 9 > AdMob > Sincronizar IDs ahora
    public sealed class AdMobConfigSync : IPreprocessBuildWithReport
    {
        public const string ConfigPath = "Assets/Resources/AdMobConfig.asset";
        private const string PluginSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            Sync();
        }

        [MenuItem("FarmIdle/Fase 9/AdMob/Sincronizar IDs ahora")]
        public static void Sync()
        {
            AdMobConfig config = LoadOrCreateConfig();

            if (!config.UsesTestIds)
            {
                if (!AdMobConfig.LooksLikeAppId(config.AndroidAppId))
                {
                    throw new BuildFailedException("El ID de app de AdMob no tiene el formato 'ca-app-pub-XXXXXXXXXXXXXXXX~NNNNNNNNNN': " + config.AndroidAppId);
                }

                if (!AdMobConfig.LooksLikeUnitId(config.RewardedUnitId))
                {
                    throw new BuildFailedException("El ID del bloque de anuncios con recompensa no tiene el formato 'ca-app-pub-XXXXXXXXXXXXXXXX/NNNNNNNNNN': " + config.RewardedUnitId);
                }
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PluginSettingsPath);
            if (settings == null) throw new BuildFailedException("Falta " + PluginSettingsPath + " (el archivo de ajustes del plugin de AdMob).");

            var serialized = new SerializedObject(settings);
            SerializedProperty appId = serialized.FindProperty("adMobAndroidAppId");
            if (appId == null) throw new BuildFailedException("El archivo de ajustes del plugin no tiene el campo adMobAndroidAppId (¿cambió la versión del plugin?).");

            if (appId.stringValue != config.AndroidAppId)
            {
                appId.stringValue = config.AndroidAppId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[FarmIdle] AdMob: ID de app sincronizado (" + (config.UsesTestIds ? "IDs de PRUEBA de Google" : "IDs reales") + ").");
        }

        // El asset de configuración (se crea con los IDs de prueba si no existe).
        public static AdMobConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AdMobConfig>(ConfigPath);
            if (config != null) return config;

            Phase1SceneBuilder.EnsureFolder("Assets/Resources");
            config = ScriptableObject.CreateInstance<AdMobConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        // ¿Los IDs son los de prueba? (para los avisos del release).
        public static bool UsesTestIds()
        {
            return LoadOrCreateConfig().UsesTestIds;
        }
    }
}
