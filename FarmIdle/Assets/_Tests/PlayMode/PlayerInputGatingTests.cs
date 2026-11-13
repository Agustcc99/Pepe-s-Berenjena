using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmIdle.Tests
{
    // El jugador solo recibe movimiento mientras el juego está en Playing: no en el menú ni en pausa.
    // InputTestFixture aísla el Input System y permite simular un teclado sin tocar el real.
    public class PlayerInputGatingTests : InputTestFixture
    {
        private GameObject _gameObject;
        private GameObject _playerObject;
        private GameManager _game;
        private PlayerInputReader _reader;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();

            _gameObject = new GameObject("GameManager (test)");
            _game = _gameObject.AddComponent<GameManager>();
            _game.Initialize();

            _keyboard = InputSystem.AddDevice<Keyboard>();

            _playerObject = new GameObject("Player (test)");
            _reader = _playerObject.AddComponent<PlayerInputReader>();
        }

        public override void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_gameObject != null) Object.DestroyImmediate(_gameObject);
            Time.timeScale = 1f;

            base.TearDown();
        }

        [Test]
        public void Move_IsZeroUntilPlaying_ThenFollowsTheKeyboard()
        {
            Press(_keyboard.wKey);

            Assert.AreEqual(Vector2.zero, _reader.Move, "En Boot no tiene que moverse.");

            _game.MarkReady();
            Assert.AreEqual(Vector2.zero, _reader.Move, "En Ready (menú) no tiene que moverse.");

            _game.StartPlaying();
            Assert.AreEqual(new Vector2(0f, 1f), _reader.Move, "En Playing tiene que moverse hacia adelante.");

            _game.Pause();
            Assert.AreEqual(Vector2.zero, _reader.Move, "En pausa no tiene que moverse.");

            _game.Resume();
            Assert.AreEqual(new Vector2(0f, 1f), _reader.Move, "Al reanudar vuelve a moverse.");
        }

        [Test]
        public void WithoutAGameManager_InputIsAlwaysAllowed()
        {
            // Una escena de prueba suelta (sin Bootstrap) no tiene GameManager: el input no se bloquea.
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_gameObject);
            _gameObject = null;
            Assert.IsNull(GameManager.Instance);

            _playerObject = new GameObject("Player (test, sin GameManager)");
            _reader = _playerObject.AddComponent<PlayerInputReader>();

            Press(_keyboard.dKey);

            Assert.AreEqual(new Vector2(1f, 0f), _reader.Move);
        }
    }
}
