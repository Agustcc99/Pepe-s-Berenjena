using System;

namespace FarmIdle
{
    // Cómo terminó un anuncio con recompensa. Solo Rewarded da la recompensa.
    public enum AdResult
    {
        Rewarded,   // el jugador completó el anuncio: recompensa ganada
        Closed,     // lo cerró / lo saltó antes de completarlo: nada
        Failed,     // error al cargar o mostrar: nada
        NoFill      // no había anuncio disponible: nada
    }

    // La puerta con quien muestra los anuncios (un SDK, una página web, o un simulador). AdsManager es el ÚNICO que la usa: el
    // resto del juego pide la recompensa a AdsManager y se entera del resultado por sus eventos. Hay tres implementaciones:
    //   - MockAdService:  simulado, configurable (Editor, pruebas y builds de desarrollo sin SDK).
    //   - WebAdService:   WebGL; llama al puente JS (Assets/Plugins/WebGL/FarmIdleWeb.jslib), que muestra un anuncio simulado
    //                     o delega en un adaptador de portal (window.FarmIdleAdAdapter) cuando exista.
    //   - AdMobAdService: Android; necesita el plugin oficial de Google Mobile Ads (define FARMIDLE_ADMOB).
    public interface IAdService
    {
        string Name { get; }

        // true si hay un anuncio listo para ese placement. Nunca muestra nada.
        bool IsReady(string placement);

        // Muestra un anuncio con recompensa y avisa el resultado UNA sola vez con 'onFinished' (puede llamarse dentro de la
        // misma llamada, por ejemplo con NoFill). Si ya hay uno en pantalla, avisa Failed. Nunca tira excepciones por un fallo del SDK.
        void ShowRewarded(string placement, Action<AdResult> onFinished);

        // AdsManager lo llama en cada frame con el tiempo sin escala. Los servicios que simulan una duración lo usan; los SDK
        // reales lo ignoran.
        void Tick(float unscaledDeltaSeconds);
    }
}
