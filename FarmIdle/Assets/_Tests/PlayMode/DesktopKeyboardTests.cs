using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // ROADMAP U-05: el juego en PC se maneja solo con el teclado. WASD y flechas ya los cubre PlotKeyboardWalkTests; acá:
    // la pista de controles del menú (solo sin pantalla táctil), Espacio/Enter para empezar, y que Esc NO haga nada (la guía de CrazyGames
    // recomienda no usarla; los Ajustes se abren con su botón).
    // Mismo esquema que PlotKeyboardWalkTests: InputTestFixture por composición y el nivel se descarga con el Input System aislado.
    public class DesktopKeyboardTests
    {
        private readonly InputTestFixture _input = new InputTestFixture();
        private Keyboard _keyboard;

        [UnityTest]
        public IEnumerator Menu_ShowsTheKeyboardHint_AndSpaceStartsTheGame()
        {
            yield return Run(MenuScenario());
        }

        [UnityTest]
        public IEnumerator Escape_DoesNothing_NeitherOpeningNorClosingSettings()
        {
            yield return Run(EscapeScenario());
        }

        private IEnumerator Run(IEnumerator scenario)
        {
            yield return GameBoot.CleanUp();
            _input.Setup();

            Exception failure = null;
            while (true)
            {
                try
                {
                    if (!scenario.MoveNext()) break;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                yield return scenario.Current;
            }

            yield return GameBoot.CleanUp();
            _input.TearDown();

            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            _input.Press(key, queueEventOnly: true);
            yield return null;
            _input.Release(key, queueEventOnly: true);
            yield return null;
        }

        private IEnumerator MenuScenario()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return GameBoot.BootToReady();
            yield return null;

            var overlay = UnityEngine.Object.FindAnyObjectByType<TapToPlayOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.IsVisible);

            Transform hint = overlay.transform.Find("KeyboardHint");
            Assert.IsNotNull(hint, "Falta la pista de teclado en el menú.");
            Assert.IsTrue(hint.gameObject.activeSelf, "Fuera de celulares la pista de teclado tiene que verse.");

            yield return Tap(_keyboard.spaceKey);

            Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "Espacio tiene que empezar el juego.");
            Assert.IsFalse(overlay.IsVisible);
        }

        private IEnumerator EscapeScenario()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return GameBoot.BootToReady();
            yield return null;

            var settings = UnityEngine.Object.FindAnyObjectByType<SettingsPanelView>();
            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.IsOpen);

            yield return Tap(_keyboard.escapeKey);
            Assert.IsFalse(settings.IsOpen, "En el menú Esc no abre los Ajustes.");

            GameManager.Instance.StartPlaying();
            yield return null;

            yield return Tap(_keyboard.escapeKey);
            Assert.IsFalse(settings.IsOpen, "Jugando, Esc tampoco abre los Ajustes.");

            settings.Open();
            settings.OpenCredits();
            yield return Tap(_keyboard.escapeKey);
            Assert.IsTrue(settings.IsCreditsOpen, "Esc no cierra los créditos.");
            Assert.IsTrue(settings.IsOpen, "Esc no cierra la tarjeta.");
        }
    }
}
