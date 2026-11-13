using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    public class GameManagerTests
    {
        private GameObject _gameObject;
        private GameManager _game;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("GameManager (test)");
            _game = _gameObject.AddComponent<GameManager>();
            _game.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Time.timeScale = 1f;
        }

        [Test]
        public void StartsInBoot_WithTimeRunning()
        {
            Assert.AreEqual(GameState.Boot, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreSame(_game, GameManager.Instance);
        }

        [Test]
        public void NormalFlow_BootReadyPlaying()
        {
            Assert.IsTrue(_game.MarkReady());
            Assert.AreEqual(GameState.Ready, _game.State);

            Assert.IsTrue(_game.StartPlaying());
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.IsTrue(_game.IsPlaying);
        }

        [Test]
        public void InvalidTransition_IsRejectedWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no permitida"));

            Assert.IsFalse(_game.TrySetState(GameState.Playing));   // de Boot no se puede saltar a Playing

            Assert.AreEqual(GameState.Boot, _game.State);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndCurrent_OncePerChange()
        {
            int calls = 0;
            GameState previous = GameState.Playing;
            GameState current = GameState.Boot;
            _game.StateChanged += (oldState, newState) => { calls++; previous = oldState; current = newState; };

            _game.MarkReady();
            _game.MarkReady();   // mismo estado: se ignora

            Assert.AreEqual(1, calls);
            Assert.AreEqual(GameState.Boot, previous);
            Assert.AreEqual(GameState.Ready, current);
        }

        [Test]
        public void Pause_FreezesTime_AndResumeRestoresIt()
        {
            _game.MarkReady();
            _game.StartPlaying();

            Assert.IsTrue(_game.Pause());
            Assert.AreEqual(GameState.Paused, _game.State);
            Assert.AreEqual(0f, Time.timeScale);

            Assert.IsTrue(_game.Resume());
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Ad_FreezesTime_AndEndAdReturnsToPlaying()
        {
            _game.MarkReady();
            _game.StartPlaying();

            Assert.IsTrue(_game.BeginAd());
            Assert.AreEqual(GameState.AdShowing, _game.State);
            Assert.AreEqual(0f, Time.timeScale);

            Assert.IsTrue(_game.EndAd());
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Ad_CannotStartFromReady()
        {
            _game.MarkReady();

            LogAssert.Expect(LogType.Warning, new Regex("no permitida"));
            Assert.IsFalse(_game.BeginAd());
            Assert.AreEqual(GameState.Ready, _game.State);
        }

        [Test]
        public void ResumeAndEndAd_OutsideTheirState_DoNothing()
        {
            _game.MarkReady();
            _game.StartPlaying();

            Assert.IsFalse(_game.Resume());
            Assert.IsFalse(_game.EndAd());
            Assert.AreEqual(GameState.Playing, _game.State);
        }

        [Test]
        public void StartPlaying_OutsideReady_DoesNothing()
        {
            Assert.IsFalse(_game.StartPlaying());   // sigue en Boot
            Assert.AreEqual(GameState.Boot, _game.State);
        }
    }
}
