using System;
using UnityEngine;

namespace FarmIdle
{
    // El ÚNICO lugar donde se cambian los IDs de AdMob, sin tocar código: Assets/Resources/AdMobConfig. Por defecto son los IDs de PRUEBA que
    // publica Google (los anuncios de prueba se pueden tocar sin riesgo). Para publicar:
    //   1) cargar en 'realAndroidAppId' el ID de la APP (ca-app-pub-XXXX~YYYY) y en 'realRewardedUnitId' el del bloque de anuncios
    //      con recompensa (ca-app-pub-XXXX/ZZZZ), que se crean en la consola de AdMob (los pone el dueño del proyecto, no el código);
    //   2) apagar 'useTestAds'.
    // Al construir para Android, el ID de la app se copia solo al manifiesto (AdMobConfigSync) y el del bloque lo lee AdMobAdService.
    // No hay ningún ID real en el repositorio. Son datos de solo lectura en runtime.
    [CreateAssetMenu(fileName = "AdMobConfig", menuName = "FarmIdle/Datos/Configuración de AdMob")]
    public sealed class AdMobConfig : ScriptableObject
    {
        // IDs de PRUEBA de Google (públicos): https://developers.google.com/admob/unity/test-ads
        public const string TestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";

        public enum ConsentDebugGeography
        {
            Off,      // el comportamiento real: UMP decide según dónde está el jugador
            EEA,      // simula estar en el Espacio Económico Europeo (muestra el formulario de consentimiento)
            NotEEA    // simula estar fuera
        }

        [Header("IDs")]
        [SerializeField, Tooltip("Mientras esté activo se usan SIEMPRE los IDs de prueba de Google, aunque haya IDs reales cargados. Apagar solo para publicar.")]
        private bool useTestAds = true;

        [SerializeField, Tooltip("ID de la APP de AdMob para Android (ca-app-pub-XXXX~YYYY). Lo carga el dueño del proyecto; vacío = no hay.")]
        private string realAndroidAppId = "";

        [SerializeField, Tooltip("ID del bloque de anuncios con recompensa (ca-app-pub-XXXX/ZZZZ). Lo carga el dueño del proyecto; vacío = no hay.")]
        private string realRewardedUnitId = "";

        [Header("Consentimiento (UMP / GDPR) — solo para probar")]
        [SerializeField, Tooltip("Simula una región para ver el formulario de consentimiento. Solo en el Editor y en builds de desarrollo; un build final lo ignora.")]
        private ConsentDebugGeography debugGeography = ConsentDebugGeography.Off;

        [SerializeField, Tooltip("IDs hasheados de los celulares de prueba (el SDK imprime el tuyo en logcat: 'Use ConsentDebugSettings ... addTestDeviceHashedId'). Hace falta para simular una región en un celular real.")]
        private string[] testDeviceHashedIds = new string[0];

        [SerializeField, Tooltip("Marcar si el juego se dirige a menores de edad (limita los anuncios). Por defecto no.")]
        private bool tagForUnderAgeOfConsent;

        // Si se usan los IDs de prueba: o porque está pedido, o porque faltan los reales.
        public bool UsesTestIds => useTestAds || string.IsNullOrWhiteSpace(realAndroidAppId) || string.IsNullOrWhiteSpace(realRewardedUnitId);

        public string AndroidAppId => UsesTestIds ? TestAndroidAppId : realAndroidAppId.Trim();
        public string RewardedUnitId => UsesTestIds ? TestRewardedUnitId : realRewardedUnitId.Trim();

        public ConsentDebugGeography DebugGeography => debugGeography;
        public string[] TestDeviceHashedIds => testDeviceHashedIds ?? new string[0];
        public bool TagForUnderAgeOfConsent => tagForUnderAgeOfConsent;

        // Un ID de app o de bloque con la forma de AdMob: "ca-app-pub-" + 16 dígitos + "~" o "/" + dígitos.
        public static bool LooksLikeAppId(string id)
        {
            return IsAdMobId(id, '~');
        }

        public static bool LooksLikeUnitId(string id)
        {
            return IsAdMobId(id, '/');
        }

        private static bool IsAdMobId(string id, char separator)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("ca-app-pub-", StringComparison.Ordinal)) return false;

            string rest = id.Substring("ca-app-pub-".Length);
            int split = rest.IndexOf(separator);
            if (split != 16 || split == rest.Length - 1) return false;

            for (int i = 0; i < rest.Length; i++)
            {
                if (i == split) continue;
                if (rest[i] < '0' || rest[i] > '9') return false;
            }

            return true;
        }
    }
}
