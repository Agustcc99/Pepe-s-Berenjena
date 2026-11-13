using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // La plaza por sectores: las vallas tienen colisión y solo se entra a un sector por su puerta.
    public class PlazaWallsTests
    {
        [SetUp]
        public void SetUp()
        {
            GameBoot.KeepPlazaWalls = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBoot.KeepPlazaWalls = false;
            yield return GameBoot.CleanUp();
        }

        private static IEnumerator Place(GameObject player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(position.x, player.transform.position.y, position.z);
            controller.enabled = true;
            yield return null;
        }

        // Camina en línea recta con el mismo CharacterController del jugador, en pasos cortos y con tope de cuadros.
        private static IEnumerator Walk(GameObject player, Vector3 direction, float distance)
        {
            var controller = player.GetComponent<CharacterController>();
            int steps = Mathf.CeilToInt(distance / 0.15f);
            for (int i = 0; i < steps; i++)
            {
                controller.Move(direction * 0.15f);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheFences_BlockTheSectors_ExceptThroughTheirDoors()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            GameObject player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(GameObject.Find("PlazaWalls"), "La plaza tiene sus vallas con colisión.");

            // Contra la valla de la avenida, lejos de una puerta: no pasa al sector de Atributos ni al de Puntos.
            yield return Place(player, new Vector3(0f, 0f, -14.5f));
            yield return Walk(player, Vector3.left, 2.5f);
            Assert.Greater(player.transform.position.x, -1.2f, "La valla del oeste frena al jugador.");

            yield return Place(player, new Vector3(0f, 0f, -14.5f));
            yield return Walk(player, Vector3.right, 2.5f);
            Assert.Less(player.transform.position.x, 1.2f, "La valla del este frena al jugador.");

            // Por la puerta de Videos (z = -11,4): pasa y llega a la columna de pads.
            yield return Place(player, new Vector3(0f, 0f, -11.4f));
            yield return Walk(player, Vector3.left, 6.5f);
            Assert.Less(player.transform.position.x, -5f, "La puerta deja pasar al sector Videos.");
        }
    }
}
