using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FarmIdle
{
    // Menú inicial: una capa a pantalla completa con "Tap to Play". Se ve mientras el juego está en Boot o Ready
    // (y tapa el joystick, así que no se puede mover antes de empezar) y desaparece al pasar a Playing.
    // En PC (fuera de celulares y tablets) muestra además la pista de teclado y deja empezar con Espacio o Enter.
    // Usa un CanvasGroup en vez de desactivar el objeto para seguir suscrita al GameManager (si se volviera
    // a Ready, reaparece). Va en el mismo objeto que la Image oscura que recibe el toque.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TapToPlayOverlay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text prompt;
        [SerializeField, Tooltip("Pista de teclado; solo se ve fuera de celulares y tablets. Puede faltar.")] private TMP_Text keyboardHint;
        [SerializeField, Min(0f)] private float pulseSpeed = 3f;

        private CanvasGroup _group;
        private GameManager _game;
        private Coroutine _pulseRoutine;
        private Coroutine _autoRoutine;
        private bool _awaitingGesture;

        private static bool? _autoStartOverride;

        // En la web el juego arranca solo al quedar listo: CrazyGames mide la carga hasta el primer gameplayStart y el toque lo atrasaba
        // lo que tardara el jugador. En celulares nativos y en el editor se sigue pidiendo el toque. Las pruebas lo fuerzan con el setter
        // (null vuelve al valor por defecto).
        public static bool AutoStart
        {
            get => _autoStartOverride ?? Application.platform == RuntimePlatform.WebGLPlayer;
            set => _autoStartOverride = value;
        }

        public static void ResetAutoStart() { _autoStartOverride = null; }

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            if (_game == null)
            {
                Debug.LogError("[FarmIdle] TapToPlayOverlay: no hay GameManager. Arrancá el juego desde la escena Bootstrap.", this);
                SetVisible(false);
                return;
            }

            _game.StateChanged += OnStateChanged;
            SetVisible(ShouldShow(_game.State));
            QueueAutoStart(_game.State);
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.StateChanged -= OnStateChanged;
                _game = null;
            }

            _pulseRoutine = null;
            _autoRoutine = null;
        }

        private void Update()
        {
            if (_awaitingGesture) PollFirstGesture();
            if (!IsVisible || _game == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                _game.StartPlaying();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Solo vale cuando el nivel ya está listo (en Boot el overlay se ve pero todavía no hace nada).
            if (_game != null) _game.StartPlaying();
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            SetVisible(ShouldShow(current));
            QueueAutoStart(current);
        }

        private void QueueAutoStart(GameState state)
        {
            if (state == GameState.Ready && AutoStart && _autoRoutine == null && isActiveAndEnabled)
            {
                _autoRoutine = StartCoroutine(StartWhenReady());
            }
        }

        // Con arranque automático el overlay solo cubre el Boot (sin pedir nada) y en Ready no se muestra.
        private static bool ShouldShow(GameState state)
        {
            return state == GameState.Boot || (state == GameState.Ready && !AutoStart);
        }

        // Un cuadro de espera: así quienes escuchan el paso a Ready terminan antes de que llegue Playing (no se anida el evento).
        private IEnumerator StartWhenReady()
        {
            yield return null;
            if (_game != null && AutoStart && _game.StartPlaying()) _awaitingGesture = true;
            _autoRoutine = null;
        }

        // Primer toque o tecla tras el arranque automático: el navegador recién ahí deja sonar el audio (ver AudioManager).
        private void PollFirstGesture()
        {
            Pointer pointer = Pointer.current;
            Keyboard keyboard = Keyboard.current;
            bool touched = (pointer != null && pointer.press.wasPressedThisFrame) || (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
            if (!touched) return;

            _awaitingGesture = false;
            if (AudioManager.Instance != null) AudioManager.Instance.RestartMusicAfterGesture();
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;

            // Touchscreen.current no sirve de criterio: en WebGL el Input System lo crea siempre, también en PC.
            if (keyboardHint != null) keyboardHint.gameObject.SetActive(!Application.isMobilePlatform && !AutoStart);
            if (prompt != null) prompt.gameObject.SetActive(!AutoStart);   // si arranca solo, no pide un toque que no hace falta

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (visible && prompt != null && isActiveAndEnabled)
            {
                _pulseRoutine = StartCoroutine(PulsePrompt());
            }
        }

        private IEnumerator PulsePrompt()
        {
            while (true)
            {
                prompt.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
                yield return null;
            }
        }
    }
}
