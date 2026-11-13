using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // Los logros (F-06) en el MainLevel real, arrancando desde Bootstrap como el juego: el botón del HUD con su aviso, la tarjeta con la
    // lista completa y su scroll, y reclamar un premio de punta a punta.
    public class AchievementIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return GameBoot.CleanUp();
        }

        private static IEnumerator StartPlaying()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            Assert.IsTrue(GameManager.Instance.StartPlaying());
            yield return null;
        }

        private static Transform Backdrop()
        {
            GameObject backdrop = Object.FindObjectsByType<Image>(FindObjectsInactive.Include).First(image => image.name == "AchievementBackdrop").gameObject;
            return backdrop.transform;
        }

        [UnityTest]
        public IEnumerator TheHud_ShowsTheButton_TheListScrolls_AndAReadyAchievementCanBeClaimed()
        {
            yield return StartPlaying();

            AchievementManager achievements = AchievementManager.Instance;
            Assert.IsNotNull(achievements, "El Bootstrapper tiene que crear el AchievementManager.");
            Assert.AreEqual(18, achievements.Count);

            var view = Object.FindAnyObjectByType<AchievementPanelView>();
            Assert.IsNotNull(view, "El HUD tiene el botón de logros.");
            Assert.AreEqual(achievements.Count, view.RowCount, "Una fila por logro.");
            Assert.IsFalse(view.IsOpen);

            Button open = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(button => button.name == "AchievementButton");
            TMP_Text openLabel = open.GetComponentInChildren<TMP_Text>();
            Assert.AreEqual(Loc.Get("achievement.button"), openLabel.text);

            // Vender 100 productos cumple "Vendé 100 productos en total".
            CropData crop = AnyCrop();
            for (int i = 0; i < 100; i++) EconomyManager.Instance.SellProduct(crop);
            yield return null;

            Assert.AreEqual(1, achievements.ClaimableCount);
            Assert.AreEqual(Loc.Get("achievement.button") + " (1)", openLabel.text, "El botón avisa cuántos premios hay para reclamar.");

            open.onClick.Invoke();
            Assert.IsTrue(view.IsOpen);

            // La lista es más alta que el visor y arranca arriba.
            ScrollRect scroll = Backdrop().GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll);
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height, "Con 18 logros hace falta scroll.");
            Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 1e-3f);

            int rowsShown = Backdrop().GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Row") && t.gameObject.activeSelf);
            Assert.AreEqual(18, rowsShown);

            // Reclamar el de ventas (índice 3).
            Transform row = Backdrop().GetComponentsInChildren<Transform>(true).First(t => t.name == "Row3");
            Button claim = row.GetComponentsInChildren<Button>(true).First(button => button.name == "ClaimButton");
            Assert.IsTrue(claim.gameObject.activeSelf, "El cumplido muestra su botón de reclamar.");
            Assert.IsTrue(claim.interactable);

            long balance = EconomyManager.Instance.Balance;
            claim.onClick.Invoke();
            yield return null;

            Assert.Greater(EconomyManager.Instance.Balance, balance, "El premio entra a la billetera.");
            Assert.IsFalse(claim.interactable, "Ya reclamado.");
            Assert.AreEqual(0, achievements.ClaimableCount);
            Assert.AreEqual(Loc.Get("achievement.button"), openLabel.text);

            // Cerrar con la X.
            Backdrop().GetComponentsInChildren<Button>(true).First(button => button.name == "AchievementCloseButton").onClick.Invoke();
            Assert.IsFalse(view.IsOpen);
        }

        // El cultivo real más barato (los que referencia el GameConfig ya están cargados en memoria): con otro, vender 100 sube de nivel
        // y cumple más de un logro.
        private static CropData AnyCrop()
        {
            return Resources.FindObjectsOfTypeAll<CropData>().OrderBy(crop => crop.SellPrice).First();
        }

        [UnityTest]
        public IEnumerator Achievements_SurviveASaveRoundTrip_ThroughTheRealSaveManager()
        {
            yield return StartPlaying();

            AchievementManager achievements = AchievementManager.Instance;
            CropData crop = AnyCrop();
            for (int i = 0; i < 100; i++) EconomyManager.Instance.SellProduct(crop);
            Assert.IsTrue(achievements.TryClaim(3));

            string json = achievements.Capture();
            StringAssert.Contains("\"claimed\":[\"ach_sell_1\"]", json);

            achievements.Restore(null);
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.Sell));

            achievements.Restore(json);
            Assert.AreEqual(100L, achievements.CounterOf(AchievementKind.Sell));
            Assert.IsTrue(achievements.IsClaimedAt(3));
        }
    }
}
