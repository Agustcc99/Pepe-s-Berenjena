using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // El PortalManager en el juego real (D-03): lo crea el Bootstrapper y le avisa al portal cuándo el juego empieza y cuándo se corta.
    public class PortalIntegrationTests
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

        [UnityTest]
        public IEnumerator ThePortalSignals_FollowTheGameState_OnTheRealGame()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            PortalManager portal = PortalManager.Instance;
            Assert.IsNotNull(portal, "El Bootstrapper crea el PortalManager.");
            Assert.IsFalse(portal.Signals.Playing, "En el menú todavía no hay gameplay.");
            Assert.AreEqual(0, portal.Signals.SentCount, "Y no sale ni un stop antes del primer start.");

            Assert.IsTrue(GameManager.Instance.StartPlaying());
            Assert.IsTrue(portal.Signals.Playing);

            Assert.IsTrue(GameManager.Instance.BeginAd());
            Assert.IsFalse(portal.Signals.Playing, "Un anuncio corta el gameplay.");
            Assert.IsTrue(GameManager.Instance.EndAd());
            Assert.IsTrue(portal.Signals.Playing);

            Assert.IsTrue(GameManager.Instance.Pause());
            Assert.IsFalse(portal.Signals.Playing, "La pausa corta el gameplay.");
            Assert.IsTrue(GameManager.Instance.Resume());
            Assert.IsTrue(portal.Signals.Playing);
            Assert.AreEqual(5, portal.Signals.SentCount);
        }
    }
}
