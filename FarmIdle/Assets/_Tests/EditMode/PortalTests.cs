using System.Collections.Generic;
using FarmIdle.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Portal web (ROADMAP D-03): las señales de gameplay, el idioma que informa el portal y el index.html de la variante CrazyGames.
    public class PortalTests
    {
        private readonly List<bool> _sent = new List<bool>();
        private GameplaySignals _signals;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _signals = new GameplaySignals(playing => _sent.Add(playing));
            Loc.PortalLocale = null;
            Loc.SystemLanguageOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            Loc.PortalLocale = null;
            Loc.SystemLanguageOverride = null;
        }

        // ---------- Señales de gameplay ----------

        [Test]
        public void BeforePlaying_NothingIsSent_NotEvenAStop()
        {
            _signals.OnStateChanged(GameState.Boot, GameState.Ready);

            CollectionAssert.IsEmpty(_sent);
            Assert.IsFalse(_signals.Playing);
        }

        [Test]
        public void OnlyPlayingIsGameplay_AndEveryBreakIsAStop()
        {
            _signals.OnStateChanged(GameState.Ready, GameState.Playing);
            _signals.OnStateChanged(GameState.Playing, GameState.AdShowing);   // el anuncio corta
            _signals.OnStateChanged(GameState.AdShowing, GameState.Playing);
            _signals.OnStateChanged(GameState.Playing, GameState.Paused);      // la pausa corta
            _signals.OnStateChanged(GameState.Paused, GameState.Playing);

            CollectionAssert.AreEqual(new[] { true, false, true, false, true }, _sent);
            Assert.AreEqual(5, _signals.SentCount);
        }

        [Test]
        public void ASignalIsNeverRepeated()
        {
            _signals.OnStateChanged(GameState.Ready, GameState.Playing);
            _signals.Report(true);
            _signals.Report(true);
            _signals.Report(false);
            _signals.Report(false);

            CollectionAssert.AreEqual(new[] { true, false }, _sent);
        }

        [Test]
        public void WithoutASink_ReportingDoesNotFail()
        {
            var silent = new GameplaySignals(null);
            Assert.DoesNotThrow(() => silent.Report(true));
            Assert.IsTrue(silent.Playing);
        }

        // ---------- Idioma del portal ----------

        [Test]
        public void ThePortalLocale_BeatsTheSystemLanguage_WhenItIsSet()
        {
            Loc.SystemLanguageOverride = SystemLanguage.English;
            Loc.PortalLocale = "es-AR";
            Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto());

            Loc.SystemLanguageOverride = SystemLanguage.Spanish;
            Loc.PortalLocale = "en-US";
            Assert.AreEqual(GameLanguage.English, Loc.ResolveAuto());
        }

        [Test]
        public void ThePortalLocale_IsSpanishOnlyForSpanishCodes()
        {
            foreach (string spanish in new[] { "es", "ES", "es-AR", "es_MX", "es-419" })
            {
                Loc.PortalLocale = spanish;
                Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto(), spanish);
            }

            foreach (string other in new[] { "en", "pt-BR", "et-EE", "de-DE", "fr" })
            {
                Loc.PortalLocale = other;
                Assert.AreEqual(GameLanguage.English, Loc.ResolveAuto(), other);
            }
        }

        [Test]
        public void WithoutAPortalLocale_TheSystemLanguageDecides()
        {
            Loc.SystemLanguageOverride = SystemLanguage.Spanish;
            Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto());
            Loc.PortalLocale = "";
            Assert.AreEqual(GameLanguage.Spanish, Loc.ResolveAuto(), "Un texto vacío es 'no hay portal'.");
            Loc.SystemLanguageOverride = SystemLanguage.German;
            Assert.AreEqual(GameLanguage.English, Loc.ResolveAuto());
        }

        [Test]
        public void OutsideTheWeb_AskingThePortalForTheLocaleGivesNothing()
        {
            Assert.IsNull(WebPortal.ReadLocale());
            Assert.DoesNotThrow(() => WebPortal.SetGameplay(true));
            PortalManager.ApplyPortalLocale();
            Assert.IsNull(Loc.PortalLocale);
        }

        // ---------- index.html de la variante CrazyGames ----------

        private const string Page = "<!DOCTYPE html>\n<html>\n  <head>\n    <title>x</title>\n  </head>\n  <body><script src=\"Build/loader.js\"></script></body>\n</html>\n";

        [Test]
        public void PatchIndexHtml_PutsTheSdkAndTheAdapterInTheHead_BeforeTheUnityLoader()
        {
            string patched = PortalPackager.PatchIndexHtml(Page);

            int head = patched.IndexOf("</head>");
            int sdk = patched.IndexOf(PortalPackager.CrazyGamesSdkUrl);
            int adapter = patched.IndexOf(PortalPackager.CrazyGamesAdapterFile);
            int loader = patched.IndexOf("Build/loader.js");
            Assert.Greater(sdk, 0);
            Assert.Less(sdk, adapter, "El SDK va primero.");
            Assert.Less(adapter, head, "Los dos en el <head>.");
            Assert.Less(head, loader, "Antes del loader de Unity.");
            StringAssert.StartsWith("<!DOCTYPE html>", patched);
        }

        [Test]
        public void PatchIndexHtml_IsIdempotent_AndFailsWithoutAHead()
        {
            string once = PortalPackager.PatchIndexHtml(Page);
            Assert.AreEqual(once, PortalPackager.PatchIndexHtml(once), "Aplicarlo dos veces no repite los scripts.");
            Assert.Throws<System.InvalidOperationException>(() => PortalPackager.PatchIndexHtml("<html><body></body></html>"));
        }

        [Test]
        public void TheGameTemplate_CanBePatched()
        {
            string template = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "WebGLTemplates", "FarmIdle", "index.html"));
            string patched = PortalPackager.PatchIndexHtml(template);
            Assert.Less(patched.IndexOf(PortalPackager.CrazyGamesAdapterFile), patched.IndexOf("createUnityInstance"));
        }

        [Test]
        public void TheAdapterFileExists_AndMentionsTheSdkCallsTheGameNeeds()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "WebPortals", PortalPackager.CrazyGamesAdapterFile);
            string code = System.IO.File.ReadAllText(path);
            foreach (string call in new[] { "gameplayStart", "gameplayStop", "requestAd", "systemInfo", "FarmIdlePortal", "FarmIdleAdAdapter", "'disabled'" })
            {
                StringAssert.Contains(call, code);
            }
        }
    }
}
