using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // ROADMAP U-04: "Texto grande" en el MainLevel real. Con la opción prendida los textos del Canvas crecen hasta ×1,3 sin salirse
    // de su caja, y al apagarla vuelven exactos a como estaban.
    public class TextSizeTests
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

        private static List<TMP_Text> CanvasTexts()
        {
            var applier = Object.FindAnyObjectByType<TextScaleApplier>();
            Assert.IsNotNull(applier, "El Canvas tiene que llevar el TextScaleApplier.");
            return applier.GetComponentsInChildren<TMP_Text>(false).Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text)).ToList();
        }

        private static float FittedWidth(TMP_Text text)
        {
            text.ForceMeshUpdate();
            return text.renderedWidth;
        }

        [UnityTest]
        public IEnumerator LargeText_GrowsTheCanvasTexts_WithoutCuttingThem_AndComesBackExactly()
        {
            yield return StartPlaying();
            SettingsPanelView view = Object.FindAnyObjectByType<SettingsPanelView>();
            view.Open();
            yield return null;

            var applier = Object.FindAnyObjectByType<TextScaleApplier>();
            Assert.AreEqual(1f, applier.AppliedScale, 1e-5f);

            // Foto de antes: tamaño y, para los que entran en su caja, su ancho.
            List<TMP_Text> texts = CanvasTexts();
            Assert.Greater(texts.Count, 10, "Tiene que haber textos de HUD y de la tarjeta de ajustes.");
            var sizeBefore = texts.ToDictionary(text => text, text => text.fontSize);
            var autoBefore = texts.ToDictionary(text => text, text => text.enableAutoSizing);
            var fitted = new HashSet<TMP_Text>(texts.Where(text => FittedWidth(text) <= text.rectTransform.rect.width + 1f));

            Button textSize = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(button => button.name == "TextSizeButton");
            textSize.onClick.Invoke();
            yield return null;

            Assert.IsTrue(SettingsManager.Instance.LargeText);
            Assert.AreEqual(SettingsManager.LargeTextScale, applier.AppliedScale, 1e-5f);
            Assert.AreEqual(Loc.Get("settings.text_size_large"), textSize.GetComponentInChildren<TMP_Text>().text);

            int grown = 0;
            foreach (TMP_Text text in texts)
            {
                text.ForceMeshUpdate();
                float limit = sizeBefore[text] * SettingsManager.LargeTextScale + 0.05f;
                Assert.LessOrEqual(text.fontSize, limit, text.name + " pasó de ×1,3.");
                if (!autoBefore[text])
                {
                    Assert.GreaterOrEqual(text.fontSize, sizeBefore[text] - 0.05f, text.name + " quedó más chico que sin la opción.");
                    if (text.fontSize > sizeBefore[text] + 0.5f) grown++;
                }

                if (fitted.Contains(text))
                {
                    Assert.LessOrEqual(text.renderedWidth, text.rectTransform.rect.width + 1f, text.name + " se sale de su caja con texto grande.");
                }
            }

            Assert.Greater(grown, 3, "Con texto grande tienen que crecer varios textos.");

            // Apagar: todo vuelve exacto.
            textSize.onClick.Invoke();
            yield return null;
            Assert.IsFalse(SettingsManager.Instance.LargeText);
            foreach (TMP_Text text in texts)
            {
                Assert.AreEqual(sizeBefore[text], text.fontSize, 1e-3f, text.name);
                Assert.AreEqual(autoBefore[text], text.enableAutoSizing, text.name);
            }
        }

        [UnityTest]
        public IEnumerator LargeText_IsRestoredFromTheSave_OnTheNextBoot()
        {
            yield return StartPlaying();
            SettingsManager.Instance.SetLargeText(true);
            string json = SettingsManager.Instance.Capture();
            StringAssert.Contains("\"largeText\":true", json);

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady();
            yield return null;

            SettingsManager.Instance.Restore(json);
            yield return null;
            Assert.IsTrue(SettingsManager.Instance.LargeText);
            Assert.AreEqual(SettingsManager.LargeTextScale, Object.FindAnyObjectByType<TextScaleApplier>().AppliedScale, 1e-5f, "El Canvas aplica lo restaurado.");
        }
    }
}
