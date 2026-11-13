using System;

namespace FarmIdle
{
    // Lo que el flujo de consentimiento necesita del SDK (UMP de Google): cada pedido avisa con un mensaje de error (null = salió bien).
    // Detrás de una interfaz para probar el flujo sin celular ni SDK.
    public interface IConsentClient
    {
        // Pide a UMP el estado del consentimiento (qué región, si hace falta preguntar).
        void UpdateInfo(Action<string> onDone);

        // Si hace falta, carga y muestra el formulario de consentimiento; avisa cuando el jugador lo cierra (o enseguida si no hacía falta).
        void ShowFormIfRequired(Action<string> onDone);

        // true si ya se puede pedir anuncios (consentimiento obtenido, o que no hace falta en esa región).
        bool CanRequestAds { get; }

        // true si hay que ofrecer una forma de revisar el consentimiento (el botón "Privacidad" de los ajustes).
        bool PrivacyOptionsRequired { get; }

        void ShowPrivacyOptions(Action<string> onDone);
    }

    public enum ConsentState
    {
        Idle,          // todavía no empezó
        UpdatingInfo,  // pidiendo el estado a UMP
        ShowingForm,   // el formulario está en pantalla (o cargando)
        Ready,         // se puede pedir anuncios
        Unavailable    // terminó el flujo y NO se puede pedir anuncios (no aceptó, o falló y se acabaron los reintentos)
    }

    // El flujo de consentimiento (UMP / GDPR) que manda sobre cuándo se puede inicializar AdMob y pedir anuncios. Es C# puro, sin UnityEngine:
    // el tiempo se lo pasa quien lo usa con Tick (tiempo sin escala), así se prueba sin celular.
    //   - Es el orden que recomienda Google: en CADA arranque se pide el estado (Update) y, si hace falta, se muestra el formulario
    //     (LoadAndShowConsentFormIfRequired). Recién cuando CanRequestAds es true se avisa BecameReady (UNA sola vez): ahí se inicializa
    //     AdMob. Nunca se pide un anuncio antes.
    //   - Si en un arranque anterior el jugador ya consintió, CanRequestAds ya es true al empezar: se avisa Ready sin esperar la red.
    //   - Un error de red al pedir el estado se reintenta (hasta 'maxRetries' veces, 'retrySeconds' entre una y otra): un celular sin
    //     conexión al abrir no deja el juego sin videos para siempre.
    //   - Un aviso que llega de un pedido viejo (ya hubo un reintento) se ignora.
    public sealed class ConsentFlow
    {
        private readonly IConsentClient _client;
        private readonly int _maxRetries;
        private readonly float _retrySeconds;

        private int _generation;
        private float _retryIn = -1f;
        private int _retriesUsed;
        private bool _readyRaised;

        public ConsentFlow(IConsentClient client, int maxRetries = 3, float retrySeconds = 30f)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _maxRetries = Math.Max(0, maxRetries);
            _retrySeconds = Math.Max(0.1f, retrySeconds);
        }

        public ConsentState State { get; private set; } = ConsentState.Idle;
        public string LastError { get; private set; } = string.Empty;
        public int RetriesUsed => _retriesUsed;
        public bool CanRequestAds => _client.CanRequestAds;
        public bool PrivacyOptionsRequired => _client.PrivacyOptionsRequired;

        // Se dispara UNA vez, cuando por primera vez se puede pedir anuncios.
        public event Action BecameReady;

        public void Start()
        {
            if (State != ConsentState.Idle) return;

            BeginUpdate();
        }

        // Muestra el formulario para revisar o cambiar el consentimiento (el botón "Privacidad"). No cambia el estado del flujo salvo que
        // el jugador deje de consentir (CanRequestAds pasa a false: el estado vuelve a Unavailable).
        public void ShowPrivacyOptions(Action<string> onDone = null)
        {
            _client.ShowPrivacyOptions(error =>
            {
                if (!_client.CanRequestAds && State == ConsentState.Ready) State = ConsentState.Unavailable;
                onDone?.Invoke(error);
            });
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_retryIn < 0f || !(unscaledDeltaSeconds > 0f)) return;

            _retryIn -= unscaledDeltaSeconds;
            if (_retryIn > 0f) return;

            _retryIn = -1f;
            _retriesUsed++;
            BeginUpdate();
        }

        private void BeginUpdate()
        {
            int generation = ++_generation;
            if (!_readyRaised) State = ConsentState.UpdatingInfo;

            _client.UpdateInfo(error => OnUpdated(generation, error));

            // Si el jugador ya consintió en un arranque anterior (o no hace falta) no hay por qué esperar a la red.
            CheckReady();
        }

        private void OnUpdated(int generation, string error)
        {
            if (generation != _generation) return;   // un pedido viejo

            if (error != null)
            {
                LastError = error;
                CheckReady();
                if (!_readyRaised) ScheduleRetryOrGiveUp();
                return;
            }

            if (!_readyRaised) State = ConsentState.ShowingForm;
            _client.ShowFormIfRequired(formError => OnFormDone(generation, formError));
        }

        private void OnFormDone(int generation, string error)
        {
            if (generation != _generation) return;

            if (error != null) LastError = error;

            CheckReady();
            State = _readyRaised ? ConsentState.Ready : ConsentState.Unavailable;   // sin Ready: no aceptó, o el formulario falló
        }

        private void CheckReady()
        {
            if (_readyRaised || !_client.CanRequestAds) return;

            _readyRaised = true;
            State = ConsentState.Ready;
            _retryIn = -1f;
            BecameReady?.Invoke();
        }

        private void ScheduleRetryOrGiveUp()
        {
            if (_retriesUsed < _maxRetries)
            {
                _retryIn = _retrySeconds;
                State = ConsentState.UpdatingInfo;   // sigue "en curso": reintenta solo
                return;
            }

            State = ConsentState.Unavailable;   // se acabaron los reintentos
        }
    }
}
