using System;
using UnityEngine;

namespace FarmIdle
{
    // Decide cuándo avisarle al portal que el juego empezó o se cortó (gameplayStart / gameplayStop). Es C# puro para poder probarlo sin
    // escena: la salida es un Action<bool> (en el juego, WebPortal.SetGameplay).
    //   - Solo cuenta como "jugando" el estado Playing: Boot, Ready (el "Tocá para jugar"), Paused y AdShowing son cortes.
    //   - Nunca repite una señal (Playing -> Playing no manda nada) y no manda un stop si todavía no hubo un start.
    public sealed class GameplaySignals
    {
        private readonly Action<bool> _sink;

        public GameplaySignals(Action<bool> sink)
        {
            _sink = sink;
        }

        // Lo último que se le avisó al portal.
        public bool Playing { get; private set; }

        // Cuántas señales salieron en total (para pruebas y depuración).
        public int SentCount { get; private set; }

        public void OnStateChanged(GameState previous, GameState current)
        {
            Report(current == GameState.Playing);
        }

        public void Report(bool playing)
        {
            if (playing == Playing) return;

            Playing = playing;
            SentCount++;
            _sink?.Invoke(playing);
        }
    }

    // Servicio del portal web (D-03). Escucha al GameManager y le pasa a GameplaySignals los cambios de estado; no hace nada por cuadro.
    // Por eso no conoce la UI ni el gameplay: solo el evento del estado del juego.
    public sealed class PortalManager : MonoBehaviour
    {
        public static PortalManager Instance { get; private set; }

        private GameManager _game;

        public GameplaySignals Signals { get; private set; }

        // Lo llama el Bootstrapper, después de la analítica; hasta entonces Instance es null. Con 'sink' (pruebas) la salida es esa.
        public void Initialize(Action<bool> sink = null)
        {
            Unsubscribe();
            Signals = new GameplaySignals(sink ?? WebPortal.SetGameplay);
            Instance = this;

            _game = GameManager.Instance;
            if (_game == null) return;

            _game.StateChanged += Signals.OnStateChanged;
            Signals.Report(_game.State == GameState.Playing);
        }

        // Lleva el idioma que informa el portal a la tabla de textos. Lo llama el Bootstrapper ANTES de crear los ajustes, que resuelven el
        // idioma "Automático" al iniciar. Sin portal (o si todavía no inició) queda vacío y manda el idioma del sistema o del navegador.
        public static void ApplyPortalLocale()
        {
            string locale = WebPortal.ReadLocale();
            if (!string.IsNullOrEmpty(locale)) Loc.PortalLocale = locale;
        }

        private void Unsubscribe()
        {
            if (_game != null && Signals != null) _game.StateChanged -= Signals.OnStateChanged;
            _game = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }
    }
}
