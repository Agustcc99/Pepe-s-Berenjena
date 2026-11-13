using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmIdle.Tests
{
    // Arranque del juego completo para las pruebas de integración: carga la escena Bootstrap como lo hace el build
    // y espera a que el nivel quede listo (Ready).
    internal static class GameBoot
    {
        private const string HolderScene = "Empty (tests)";

        // Los servicios viven en DontDestroyOnLoad: hay que borrarlos a mano entre una prueba y otra.
        // Y el nivel no se descarga solo: si queda cargado, su joystick en pantalla (un OnScreenControl) conserva un
        // gamepad virtual que se vuelve inválido cuando otra prueba aísla el Input System (InputTestFixture), y al
        // descargarlo más tarde falla. Por eso cada prueba de integración deja el nivel descargado al terminar.
        public static IEnumerator CleanUp()
        {
            GameObject services = GameObject.Find("[Services]");
            if (services != null) Object.Destroy(services);

            Time.timeScale = 1f;
            yield return null;

            Scene level = SceneManager.GetSceneByName(SceneNames.MainLevel);
            if (!level.isLoaded) yield break;

            // Unity no deja descargar la última escena cargada (tras LoadScene(Bootstrap), MainLevel lo es): se
            // deja una escena vacía como "relevo" y recién ahí se descarga el nivel.
            Scene holder = SceneManager.GetSceneByName(HolderScene);
            if (!holder.isLoaded) holder = SceneManager.CreateScene(HolderScene);
            SceneManager.SetActiveScene(holder);

            yield return SceneManager.UnloadSceneAsync(level);
        }

        // El almacenamiento de partida del último arranque (en memoria): las pruebas lo leen para ver qué se guardó, y lo pueden
        // preparar antes de arrancar con BootToReady(storage) para simular una partida existente o dañada.
        public static MemorySaveStorage Storage { get; private set; }

        public static IEnumerator BootToReady()
        {
            return BootToReady(new MemorySaveStorage());
        }

        // Arranca con el almacenamiento dado: el juego NUNCA toca la partida real del dispositivo en las pruebas.
        public static IEnumerator BootToReady(MemorySaveStorage storage)
        {
            Storage = storage;
            SaveManager.StorageForNextBoot = storage;
            Loc.SystemLanguageOverride = SystemLanguage.Spanish;   // "Automático" no depende de la máquina donde corren las pruebas
            Loc.SetLanguage(GameLanguage.Spanish);
            SceneManager.LoadScene(SceneNames.Bootstrap);

            float start = Time.realtimeSinceStartup;
            do
            {
                yield return null;
            }
            while ((GameManager.Instance == null || GameManager.Instance.State != GameState.Ready) && Time.realtimeSinceStartup - start < 30f);

            Assert.IsNotNull(GameManager.Instance, "El Bootstrap no creó el GameManager.");
            Assert.AreEqual(GameState.Ready, GameManager.Instance.State, "El juego no llegó a Ready.");

            yield return null;   // deja que termine de descargarse la escena Bootstrap
            yield return null;

            // Las pruebas caminan en línea recta hasta los pads: sin esto las vallas de la plaza (con colisión) las frenarían. Solo
            // PlazaWallsTests las deja puestas.
            if (!KeepPlazaWalls)
            {
                GameObject walls = GameObject.Find("PlazaWalls");
                if (walls != null) walls.SetActive(false);
            }
        }

        public static bool KeepPlazaWalls { get; set; }
    }
}
