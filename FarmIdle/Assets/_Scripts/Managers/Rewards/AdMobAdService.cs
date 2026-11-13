// AdMob (Android) con consentimiento UMP. Solo se compila en Android con el plugin oficial "Google Mobile Ads Unity Plugin" (11.6.0) y el
// define FARMIDLE_ADMOB (AdMobSetup lo activa). Sin eso este archivo queda vacío y AdsManager usa el Mock (Editor / desarrollo) o ningún
// anuncio (build final).
//
// ESTE CÓDIGO NO SE PROBÓ EN UN CELULAR: se escribió contra la API documentada del plugin y compila en el build de Android, pero nunca corrió
// en un dispositivo. El orden del consentimiento (ConsentFlow) sí está probado con un cliente simulado.
//
// IDs: los carga el dueño del proyecto en UN solo lugar, Assets/Resources/AdMobConfig (ver Data/AdMobConfig.cs). Por defecto son los IDs
// de PRUEBA de Google; ningún ID real va al repositorio.
//
// Consentimiento (UMP / GDPR): antes de MobileAds.Initialize se pide el estado y, si hace falta, se muestra el formulario; no se
// inicializa AdMob ni se pide ningún anuncio hasta que ConsentInformation.CanRequestAds() sea true (ConsentFlow, Managers/Rewards/ConsentFlow.cs).
#if UNITY_ANDROID && FARMIDLE_ADMOB
using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace FarmIdle
{
    // El lado de UMP del flujo de consentimiento: traduce IConsentClient a las llamadas del plugin.
    internal sealed class UmpConsentClient : IConsentClient
    {
        private readonly AdMobConfig _config;

        public UmpConsentClient(AdMobConfig config)
        {
            _config = config;
        }

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void UpdateInfo(Action<string> onDone)
        {
            var parameters = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = _config != null && _config.TagForUnderAgeOfConsent
            };

            // Simular una región solo sirve para probar: un build final lo ignora.
            if (_config != null && Debug.isDebugBuild && _config.DebugGeography != AdMobConfig.ConsentDebugGeography.Off)
            {
                parameters.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = _config.DebugGeography == AdMobConfig.ConsentDebugGeography.EEA ? DebugGeography.EEA : DebugGeography.NotEEA,
                    TestDeviceHashedIds = new List<string>(_config.TestDeviceHashedIds)
                };
            }

            ConsentInformation.Update(parameters, error => onDone(error != null ? error.Message : null));
        }

        public void ShowFormIfRequired(Action<string> onDone)
        {
            ConsentForm.LoadAndShowConsentFormIfRequired(error => onDone(error != null ? error.Message : null));
        }

        public void ShowPrivacyOptions(Action<string> onDone)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => onDone(error != null ? error.Message : null));
        }
    }

    public sealed class AdMobAdService : IAdService, IConsentAware
    {
        private readonly AdMobConfig _config;
        private readonly ConsentFlow _consent;

        private RewardedAd _loaded;
        private bool _loading;
        private bool _initialized;
        private Action<AdResult> _callback;
        private bool _earned;

        public AdMobAdService()
        {
            _config = Resources.Load<AdMobConfig>("AdMobConfig");
            if (_config == null) Debug.LogWarning("[FarmIdle] AdMob: falta Assets/Resources/AdMobConfig; se usan los IDs de prueba.");

            // Los avisos del SDK (anuncio cerrado, recompensa, consentimiento) tocan el estado del juego: tienen que llegar por el hilo
            // principal de Unity, no por uno del SDK.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            // AdMob NO se inicializa hasta que el consentimiento lo permita.
            _consent = new ConsentFlow(new UmpConsentClient(_config));
            _consent.BecameReady += InitializeSdk;
            _consent.Start();
        }

        public string Name => _config == null || _config.UsesTestIds ? "AdMob (IDs de prueba)" : "AdMob";

        private string UnitId => _config != null ? _config.RewardedUnitId : AdMobConfig.TestRewardedUnitId;

        // ---------- Consentimiento ----------

        public bool PrivacyOptionsRequired => _consent.PrivacyOptionsRequired;

        public void ShowPrivacyOptions(Action<string> onDone)
        {
            _consent.ShowPrivacyOptions(onDone);
        }

        private void InitializeSdk()
        {
            if (_initialized) return;

            MobileAds.Initialize(status =>
            {
                _initialized = true;
                Load();
            });
        }

        // ---------- IAdService ----------

        // Solo hay anuncio si el SDK está inicializado Y el jugador permite pedirlos (si retira el consentimiento deja de haber).
        public bool IsReady(string placement)
        {
            return _initialized && _consent.CanRequestAds && _callback == null && _loaded != null && _loaded.CanShowAd();
        }

        public void ShowRewarded(string placement, Action<AdResult> onFinished)
        {
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));

            if (_callback != null)
            {
                onFinished(AdResult.Failed);
                return;
            }

            if (!_initialized || !_consent.CanRequestAds || _loaded == null || !_loaded.CanShowAd())
            {
                Load();
                onFinished(AdResult.NoFill);
                return;
            }

            _callback = onFinished;
            _earned = false;

            RewardedAd ad = _loaded;
            _loaded = null;

            ad.OnAdFullScreenContentClosed += () => Finish(_earned ? AdResult.Rewarded : AdResult.Closed);
            ad.OnAdFullScreenContentFailed += error => Finish(AdResult.Failed);
            ad.Show(reward => _earned = true);   // la recompensa se confirma acá, el resultado se entrega al CERRAR el anuncio
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            _consent.Tick(unscaledDeltaSeconds);   // reintenta el consentimiento si falló la red al abrir
        }

        private void Finish(AdResult result)
        {
            Action<AdResult> callback = _callback;
            _callback = null;
            Load();   // el siguiente
            if (callback != null) callback(result);
        }

        private void Load()
        {
            if (_loading || _loaded != null || !_initialized || !_consent.CanRequestAds) return;

            _loading = true;
            RewardedAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                _loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[FarmIdle] AdMob: no se pudo cargar el anuncio (" + (error != null ? error.GetMessage() : "sin respuesta") + ").");
                    return;
                }

                _loaded = ad;
            });
        }
    }
}
#endif
