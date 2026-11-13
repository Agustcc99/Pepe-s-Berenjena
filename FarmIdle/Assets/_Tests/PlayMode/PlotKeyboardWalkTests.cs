using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // De punta a punta con el teclado: el juego arranca desde Bootstrap, empieza a jugar (el "Tap to Play") y se
    // camina hacia adelante con la W desde el punto de aparición hasta pisar la primera fila de parcelas. Pasa por
    // todo lo real: PlayerInputReader, PlayerMovement, el CharacterController, el trigger de la parcela, el
    // InteractionTrigger y el PlayerActor.
    //
    // InputTestFixture aísla el Input System y simula un teclado, pero se usa por composición (no se hereda de él):
    // el joystick en pantalla del nivel (un OnScreenControl) crea un gamepad virtual y, al descargarse, lo quita.
    // Si ese nivel sobreviviera a la restauración del aislamiento, quitaría un dispositivo que ya no existe. Por eso
    // el nivel se descarga siempre con el Input System todavía aislado (GameBoot.CleanUp), y el de una prueba
    // anterior se descarga antes de aislar.
    public class PlotKeyboardWalkTests
    {
        private readonly InputTestFixture _input = new InputTestFixture();
        private Keyboard _keyboard;

        [UnityTest]
        public IEnumerator HoldingW_FromTheSpawnPoint_ReachesTheFirstRow_AndSowsIt()
        {
            yield return GameBoot.CleanUp();
            _input.Setup();

            // Se corre el escenario atrapando el fallo para poder limpiar (con yields) antes de reportarlo.
            Exception failure = null;
            IEnumerator scenario = Scenario();
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

        private IEnumerator Scenario()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();

            yield return GameBoot.BootToReady();
            yield return null;

            var field = UnityEngine.Object.FindAnyObjectByType<PlotField>();
            Assert.IsNotNull(field, "MainLevel no tiene el campo de parcelas.");
            GameObject player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(player);
            Vector3 spawn = player.transform.position;

            // Antes de empezar (menú) el jugador no se mueve aunque se apriete la W.
            _input.Press(_keyboard.wKey);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(spawn.z, player.transform.position.z, 0.01f, "En el menú el jugador no tiene que moverse.");
            Assert.AreEqual(9, PlotCensus.Instance.Empty);

            GameManager.Instance.StartPlaying();

            // Con la W todavía apretada, camina hasta que alguna parcela se siembre (con un tope de tiempo).
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && PlotCensus.Instance.Growing == 0)
            {
                yield return null;
            }

            _input.Release(_keyboard.wKey);

            Assert.Greater(player.transform.position.z, spawn.z + 1f, "El jugador tendría que haber caminado hacia adelante.");
            Assert.AreEqual(1, PlotCensus.Instance.Growing, "Caminando hacia adelante tiene que sembrar la primera parcela que pisa.");
            Assert.AreEqual(8, PlotCensus.Instance.Empty);

            // Camina por x = 0, así que la primera que pisa es la del medio de la fila más cercana (índice 1 de las 9).
            Assert.AreEqual(PlotState.Growing, field.Plots[1].State, "Tendría que haber sembrado la parcela del medio de la primera fila.");

            // Y al soltar la tecla el jugador se frena.
            yield return new WaitForSeconds(0.3f);
            float stoppedAt = player.transform.position.z;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(stoppedAt, player.transform.position.z, 0.01f, "Sin tecla apretada el jugador se queda quieto.");
        }
    }
}
