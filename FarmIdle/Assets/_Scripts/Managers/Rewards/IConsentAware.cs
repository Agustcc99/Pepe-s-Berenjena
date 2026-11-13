using System;

namespace FarmIdle
{
    // Un servicio de anuncios que además maneja el consentimiento del jugador (AdMob con UMP). AdsManager lo expone a la UI (el botón
    // "Privacidad" de los ajustes aparece solo si hace falta); los demás servicios (web, simulado, sin anuncios) no lo implementan.
    public interface IConsentAware
    {
        // true si hay que ofrecer una forma de revisar el consentimiento (por ejemplo, el jugador está en la Unión Europea).
        bool PrivacyOptionsRequired { get; }

        // Muestra el formulario de privacidad. Avisa al terminar (con un mensaje de error, o null si salió bien).
        void ShowPrivacyOptions(Action<string> onDone);
    }
}
