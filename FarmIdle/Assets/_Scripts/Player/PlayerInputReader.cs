using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmIdle
{
    // Lee la acción "Move" y la expone como un Vector2 de magnitud 0..1.
    // Recibe teclado (WASD y flechas), mando y el joystick en pantalla
    // (que escribe en <Gamepad>/leftStick): todo entra por la misma acción.
    // Solo entrega movimiento mientras el juego está en Playing (no en el menú ni en pausa).
    public class PlayerInputReader : MonoBehaviour
    {
        private InputAction _move;
        private GameManager _game;
        private bool _inputAllowed = true;

        public Vector2 Move
        {
            get
            {
                if (_move == null || !_inputAllowed) return Vector2.zero;
                return Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            }
        }

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _move.AddBinding("<Gamepad>/leftStick");
        }

        private void OnEnable()
        {
            _move.Enable();

            // Sin GameManager (por ejemplo, una escena de prueba suelta) el input queda siempre habilitado.
            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.StateChanged += OnGameStateChanged;
                _inputAllowed = _game.IsPlaying;
            }
        }

        private void OnDisable()
        {
            _move.Disable();

            if (_game != null)
            {
                _game.StateChanged -= OnGameStateChanged;
                _game = null;
            }
        }

        private void OnDestroy()
        {
            _move.Dispose();
        }

        private void OnGameStateChanged(GameState previous, GameState current)
        {
            _inputAllowed = current == GameState.Playing;
        }
    }
}
