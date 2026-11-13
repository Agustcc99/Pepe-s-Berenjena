using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // El premio de login diario (F-07) en el MainLevel real, arrancando desde Bootstrap como el juego: no molesta durante la guía de los
    // primeros minutos, después aparece (botón y tarjeta) y reclamarlo de punta a punta.
    public class DailyRewardIntegrationTests
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

        [UnityTest]
        public IEnumerator DuringTheGuide_TheDailyRewardStaysQuiet_AfterwardsItShowsAndCanBeClaimed()
        {
            yield return StartPlaying();

            DailyRewardManager daily = DailyRewardManager.Instance;
            Assert.IsNotNull(daily, "El Bootstrapper tiene que crear el DailyRewardManager.");
            var view = Object.FindAnyObjectByType<DailyRewardView>();
            Assert.IsNotNull(view, "El HUD tiene el premio diario.");

            // Partida nueva: hay premio, pero la guía está corriendo: ni botón ni tarjeta.
            Assert.IsTrue(daily.IsClaimable);
            Assert.IsTrue(TutorialManager.Instance.IsActive);
            Assert.IsFalse(view.ButtonVisible, "Durante la guía no se muestra el botón.");
            Assert.IsFalse(view.IsOpen, "Ni se abre sola.");

            // Termina la guía: aparece el botón.
            TutorialManager.Instance.Skip();
            yield return null;
            Assert.IsTrue(view.ButtonVisible);

            Button open = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(button => button.name == "DailyRewardButton");
            open.onClick.Invoke();
            Assert.IsTrue(view.IsOpen);

            Button claim = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(button => button.name == "DailyClaimButton");
            Assert.IsTrue(claim.interactable);

            long balance = EconomyManager.Instance.Balance;
            claim.onClick.Invoke();
            yield return null;

            Assert.Greater(EconomyManager.Instance.Balance, balance, "El premio del día 1 entra a la billetera.");
            Assert.AreEqual(1, daily.Streak);
            Assert.IsFalse(daily.IsClaimable);
            Assert.IsFalse(claim.interactable, "Ya reclamado hoy.");
            Assert.IsFalse(view.ButtonVisible, "El botón del HUD se esconde.");
            Assert.IsTrue(view.IsOpen, "La tarjeta queda abierta mostrando el día cumplido.");

            Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(button => button.name == "DailyRewardCloseButton").onClick.Invoke();
            Assert.IsFalse(view.IsOpen);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        // Como la guía oculta la tarjeta en una partida nueva, el layout se mide: los 7 casilleros dentro de la tarjeta, sin pisarse, con
        // el premio, la racha y el botón de reclamar debajo, también dentro.
        [UnityTest]
        public IEnumerator TheCard_HasSevenSlots_InsideTheCard_WithoutOverlapping()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            TutorialManager.Instance.Skip();
            Assert.IsTrue(GameManager.Instance.StartPlaying());
            yield return null;

            var view = Object.FindAnyObjectByType<DailyRewardView>();
            Assert.IsTrue(view.IsOpen);

            Transform backdrop = Object.FindObjectsByType<Image>(FindObjectsInactive.Include).First(image => image.name == "DailyRewardBackdrop").transform;
            Rect card = WorldRect((RectTransform)backdrop.Find("Card"));
            var slots = Enumerable.Range(1, 7).Select(day => WorldRect((RectTransform)backdrop.Find("Card/Day" + day))).ToArray();

            for (int i = 0; i < slots.Length; i++)
            {
                Assert.IsTrue(card.Contains(slots[i].min) && card.Contains(slots[i].max), "El casillero " + (i + 1) + " se sale de la tarjeta.");
                for (int j = i + 1; j < slots.Length; j++) Assert.IsFalse(slots[i].Overlaps(slots[j]), "Los casilleros " + (i + 1) + " y " + (j + 1) + " se pisan.");
            }

            float lowestSlot = slots.Min(slot => slot.yMin);
            foreach (string name in new[] { "Reward", "Streak", "DailyClaimButton" })
            {
                Rect rect = WorldRect((RectTransform)backdrop.Find("Card/" + name));
                Assert.IsTrue(card.Contains(rect.min) && card.Contains(rect.max), name + " se sale de la tarjeta.");
                Assert.LessOrEqual(rect.yMax, lowestSlot + 0.01f, name + " tiene que ir debajo de los casilleros.");
            }
        }

        [UnityTest]
        public IEnumerator TheDailyRewardCard_OpensByItself_WhenItBecomesAvailable_AfterTheGuide()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            TutorialManager.Instance.Skip();

            Assert.IsTrue(GameManager.Instance.StartPlaying());   // al empezar a jugar, el premio queda listo y la tarjeta se abre
            yield return null;

            var view = Object.FindAnyObjectByType<DailyRewardView>();
            Assert.IsTrue(DailyRewardManager.Instance.IsClaimable);
            Assert.IsTrue(view.IsOpen, "Con la guía terminada la tarjeta se abre sola.");
        }
    }
}
