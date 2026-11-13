using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Reglas del Basic Launch de CrazyGames: sin anuncios en la web y el inglés como idioma por defecto.
    public class WebLaunchPolicyTests
    {
        [Test]
        public void RewardedAds_AreOffTheWebUnlessTheFullLaunchBuildTurnsThemOn()
        {
            Assert.IsFalse(AdPolicy.Offered(true, false), "Web en Basic Launch: sin anuncios.");
            Assert.IsTrue(AdPolicy.Offered(true, true), "Web en Full Launch: con anuncios.");
            Assert.IsTrue(AdPolicy.Offered(false, false), "Android y el Editor siempre los ofrecen.");
            Assert.IsTrue(AdPolicy.RewardedAdsOffered, "En el Editor se ofrecen (si no, las pruebas de los pads no andarían).");
        }

        [Test]
        public void Auto_IsEnglishOnTheWeb_EvenWithASpanishSystemAndPortal_ButStillFollowsTheSystemElsewhere()
        {
            try
            {
                Loc.SystemLanguageOverride = SystemLanguage.Spanish;
                Loc.PortalLocale = "es-AR";

                Loc.AutoIsEnglishOnly = false;
                Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto());

                Loc.AutoIsEnglishOnly = true;
                Assert.AreEqual(GameLanguage.English, Loc.ResolveAuto(), "En la web, Automático es inglés.");
            }
            finally
            {
                Loc.AutoIsEnglishOnly = false;
                Loc.PortalLocale = null;
                Loc.SystemLanguageOverride = null;
            }
        }
    }
}
