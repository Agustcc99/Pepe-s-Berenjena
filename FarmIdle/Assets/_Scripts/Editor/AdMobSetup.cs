using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Enciende o apaga AdMob en el build de Android definiendo FARMIDLE_ADMOB (solo para Android: la web usa su propio puente).
    // Con el símbolo, AdsManager usa AdMobAdService (Managers/Rewards/AdMobAdService.cs) y se compila AdMobGradleDependencies.
    //   Menú:    FarmIdle > Fase 8 > AdMob > Activar / Desactivar
    //   Consola: Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.AdMobSetup.Enable
    internal static class AdMobSetup
    {
        private const string Symbol = "FARMIDLE_ADMOB";

        [MenuItem("FarmIdle/Fase 8/AdMob/Activar (Android)")]
        public static void Enable()
        {
            Set(true);
        }

        [MenuItem("FarmIdle/Fase 8/AdMob/Desactivar (Android)")]
        public static void Disable()
        {
            Set(false);
        }

        private static void Set(bool enabled)
        {
            NamedBuildTarget target = NamedBuildTarget.Android;
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => s.Length > 0).ToList();

            symbols.Remove(Symbol);
            if (enabled) symbols.Add(Symbol);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] AdMob " + (enabled ? "activado" : "desactivado") + " para Android (" + Symbol + ").");
        }
    }
}
