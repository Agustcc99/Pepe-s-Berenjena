using System;
using UnityEngine;

namespace FarmIdle
{
    // Máquina de estados del juego (FSM por enum): Boot -> Ready -> Playing <-> Paused / AdShowing.
    // Guarda el estado actual, valida las transiciones y avisa con StateChanged. No conoce la UI ni el gameplay:
    // quien necesite reaccionar (overlay, input, audio...) se suscribe al evento.
    // Lo único que hace por su cuenta es congelar el tiempo mientras el estado es Paused o AdShowing.
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;

        public bool IsPlaying => State == GameState.Playing;

        // (estado anterior, estado nuevo). Se dispara después de cambiar el estado.
        public event Action<GameState, GameState> StateChanged;

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize()
        {
            Instance = this;
            State = GameState.Boot;
            ApplyTimeScale(State);
        }

        // Atajos con nombre para los cambios habituales. Devuelven false si no se pudo hacer.
        public bool MarkReady() { return TrySetState(GameState.Ready); }
        public bool StartPlaying() { return State == GameState.Ready && TrySetState(GameState.Playing); }
        public bool Pause() { return TrySetState(GameState.Paused); }
        public bool Resume() { return State == GameState.Paused && TrySetState(GameState.Playing); }
        public bool BeginAd() { return TrySetState(GameState.AdShowing); }
        public bool EndAd() { return State == GameState.AdShowing && TrySetState(GameState.Playing); }

        public bool TrySetState(GameState next)
        {
            if (next == State) return false;

            if (!IsTransitionAllowed(State, next))
            {
                Debug.LogWarning("[FarmIdle] Transición de estado no permitida: " + State + " -> " + next);
                return false;
            }

            GameState previous = State;
            State = next;
            ApplyTimeScale(next);
            StateChanged?.Invoke(previous, next);
            return true;
        }

        private static bool IsTransitionAllowed(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.Boot: return to == GameState.Ready;
                case GameState.Ready: return to == GameState.Playing;
                case GameState.Playing: return to == GameState.Paused || to == GameState.AdShowing;
                case GameState.Paused: return to == GameState.Playing;
                case GameState.AdShowing: return to == GameState.Playing;
                default: return false;
            }
        }

        private static void ApplyTimeScale(GameState state)
        {
            Time.timeScale = state == GameState.Paused || state == GameState.AdShowing ? 0f : 1f;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            Instance = null;
            Time.timeScale = 1f;
        }
    }
}
