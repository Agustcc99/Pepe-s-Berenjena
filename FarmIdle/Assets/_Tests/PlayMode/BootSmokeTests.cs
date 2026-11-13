using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Prueba de humo del juego completo: arranca desde la escena Bootstrap como lo hace el build y comprueba
    // que servicios, escena, HUD y pool quedaron bien conectados (referencias del Inspector incluidas).
    public class BootSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return CleanUp();
        }

        // El arranque y la limpieza son los mismos para todas las pruebas de integración (ver GameBoot).
        private static IEnumerator CleanUp()
        {
            return GameBoot.CleanUp();
        }

        private static IEnumerator BootToReady()
        {
            return GameBoot.BootToReady();
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesServices_LoadsTheLevel_AndReachesReady()
        {
            yield return BootToReady();

            Assert.IsNotNull(EconomyManager.Instance, "Falta el EconomyManager.");
            Assert.IsNotNull(PoolManager.Instance, "Falta el PoolManager.");
            Assert.IsNotNull(BackpackManager.Instance, "Falta el BackpackManager.");
            Assert.AreEqual(0, BackpackManager.Instance.Backpack.Count, "La mochila arranca vacía.");
            Assert.AreEqual(0L, EconomyManager.Instance.Balance);

            Assert.AreEqual(SceneNames.MainLevel, SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneManager.GetSceneByName(SceneNames.Bootstrap).isLoaded, "La escena Bootstrap debería haberse descargado.");
            Assert.IsNotNull(GameObject.FindWithTag("Player"), "No está el jugador en MainLevel.");
        }

        [UnityTest]
        public IEnumerator TapToPlay_StartsTheGame_AndTheHudReactsToWalletEvents()
        {
            yield return BootToReady();

            var overlay = Object.FindAnyObjectByType<TapToPlayOverlay>();
            Assert.IsNotNull(overlay, "No está el overlay Tap to Play.");
            Assert.IsTrue(overlay.IsVisible, "El overlay tiene que verse en Ready.");

            overlay.OnPointerClick(new PointerEventData(EventSystem.current));

            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Assert.IsFalse(overlay.IsVisible, "El overlay tiene que ocultarse al empezar.");

            var counter = Object.FindAnyObjectByType<CoinCounterView>();
            Assert.IsNotNull(counter, "No está el contador de monedas.");
            TMP_Text label = counter.GetComponentInChildren<TMP_Text>();
            Assert.AreEqual("0", label.text);

            // El HUD solo escucha el evento de la billetera.
            EconomyManager.Instance.AddCoins(10);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual("10", label.text);

            Assert.IsTrue(EconomyManager.Instance.TrySpend(4));
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual("6", label.text);

            EconomyManager.Instance.AddCoins(1500);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual("1,506", label.text);
        }

        [UnityTest]
        public IEnumerator DebugPanel_PrewarmsTheProbePool_AndDeliversWithoutInstantiating()
        {
            yield return BootToReady();
            yield return null;   // el Start() del panel hace el Prewarm

            var panel = Object.FindAnyObjectByType<DebugPanel>();
            Assert.IsNotNull(panel, "No está el panel de debug.");

            PoolManager pools = PoolManager.Instance;
            Assert.IsTrue(pools.TryGetStats(panel.ProbePrefab, out PoolStats afterPrewarm), "El panel no hizo el Prewarm.");
            Assert.AreEqual(panel.ProbePrewarm, afterPrewarm.Created);
            Assert.AreEqual(panel.ProbePrewarm, afterPrewarm.Inactive);
            Assert.AreEqual(0, afterPrewarm.Active);

            panel.SpawnProbes();

            Assert.IsTrue(pools.TryGetStats(panel.ProbePrefab, out PoolStats whileInUse));
            Assert.AreEqual(afterPrewarm.Created, whileInUse.Created, "Pedir objetos creó instancias nuevas.");
            Assert.AreEqual(panel.ProbesPerTap, whileInUse.Active);
            Assert.AreEqual(0, whileInUse.Misses);

            yield return new WaitForSeconds(panel.ProbeSeconds + 0.3f);

            Assert.IsTrue(pools.TryGetStats(panel.ProbePrefab, out PoolStats afterRelease));
            Assert.AreEqual(afterPrewarm.Created, afterRelease.Created);
            Assert.AreEqual(0, afterRelease.Active, "Los objetos de prueba tenían que volver al pool.");
            Assert.AreEqual(panel.ProbePrewarm, afterRelease.Inactive);
        }
    }
}
