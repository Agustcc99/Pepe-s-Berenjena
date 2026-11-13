using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // El flujo de consentimiento (UMP / GDPR) contra un cliente simulado: el orden en que se pide el estado, se muestra el formulario y
    // recién ahí se avisa que se puede pedir anuncios; los errores de red y los reintentos; los avisos viejos.
    public class ConsentFlowTests
    {
        private sealed class FakeClient : IConsentClient
        {
            public readonly List<string> Calls = new List<string>();
            public Action<string> PendingUpdate;
            public Action<string> PendingForm;
            public bool CanRequest { get; set; }
            public bool PrivacyRequired { get; set; }
            public Action<string> PendingPrivacy;

            // Qué pasa cuando se toca "Aceptar" en el formulario.
            public bool ConsentGivenWhenFormCloses;

            public bool CanRequestAds => CanRequest;
            public bool PrivacyOptionsRequired => PrivacyRequired;

            public void UpdateInfo(Action<string> onDone)
            {
                Calls.Add("update");
                PendingUpdate = onDone;
            }

            public void ShowFormIfRequired(Action<string> onDone)
            {
                Calls.Add("form");
                PendingForm = onDone;
            }

            public void ShowPrivacyOptions(Action<string> onDone)
            {
                Calls.Add("privacy");
                PendingPrivacy = onDone;
            }

            public void FinishUpdate(string error = null)
            {
                Action<string> callback = PendingUpdate;
                PendingUpdate = null;
                callback(error);
            }

            public void FinishForm(string error = null)
            {
                Action<string> callback = PendingForm;
                PendingForm = null;
                if (ConsentGivenWhenFormCloses) CanRequest = true;
                callback(error);
            }
        }

        [Test]
        public void FirstLaunch_AsksUMP_ThenShowsTheForm_AndOnlyThenAllowsAds()
        {
            var client = new FakeClient { ConsentGivenWhenFormCloses = true };
            var flow = new ConsentFlow(client);
            int ready = 0;
            flow.BecameReady += () => ready++;

            flow.Start();
            CollectionAssert.AreEqual(new[] { "update" }, client.Calls);
            Assert.AreEqual(ConsentState.UpdatingInfo, flow.State);
            Assert.AreEqual(0, ready, "Sin consentimiento no se puede pedir anuncios.");

            client.FinishUpdate();
            CollectionAssert.AreEqual(new[] { "update", "form" }, client.Calls);
            Assert.AreEqual(ConsentState.ShowingForm, flow.State);
            Assert.AreEqual(0, ready, "Mientras el formulario está en pantalla, todavía no.");

            client.FinishForm();
            Assert.AreEqual(ConsentState.Ready, flow.State);
            Assert.AreEqual(1, ready);
            Assert.IsTrue(flow.CanRequestAds);
        }

        [Test]
        public void APlayerWhoDeclines_NeverGetsAds()
        {
            var client = new FakeClient { ConsentGivenWhenFormCloses = false };
            var flow = new ConsentFlow(client);
            int ready = 0;
            flow.BecameReady += () => ready++;

            flow.Start();
            client.FinishUpdate();
            client.FinishForm();

            Assert.AreEqual(ConsentState.Unavailable, flow.State);
            Assert.AreEqual(0, ready);
            Assert.IsFalse(flow.CanRequestAds);
        }

        [Test]
        public void APreviousConsent_AllowsAdsRightAway_WithoutWaitingForTheNetwork_AndOnlyOnce()
        {
            var client = new FakeClient { CanRequest = true };
            var flow = new ConsentFlow(client);
            int ready = 0;
            flow.BecameReady += () => ready++;

            flow.Start();
            Assert.AreEqual(1, ready, "Ya consintió antes: se puede sin esperar a UMP.");
            Assert.AreEqual(ConsentState.Ready, flow.State);

            client.FinishUpdate();
            client.FinishForm();
            Assert.AreEqual(1, ready, "El aviso es una sola vez.");
            Assert.AreEqual(ConsentState.Ready, flow.State, "El resto del flujo no lo saca de Ready.");
        }

        [Test]
        public void ARegionWithoutTheRequirement_ReachesReadyWithoutAForm()
        {
            var client = new FakeClient();
            var flow = new ConsentFlow(client);
            flow.Start();

            client.FinishUpdate();
            client.CanRequest = true;   // UMP: "no hace falta preguntar" en esa región
            client.FinishForm();

            Assert.AreEqual(ConsentState.Ready, flow.State);
        }

        [Test]
        public void ANetworkError_IsRetriedLater_UntilItWorks()
        {
            var client = new FakeClient { ConsentGivenWhenFormCloses = true };
            var flow = new ConsentFlow(client, maxRetries: 3, retrySeconds: 30f);
            int ready = 0;
            flow.BecameReady += () => ready++;

            flow.Start();
            client.FinishUpdate("sin conexión");
            Assert.AreEqual("sin conexión", flow.LastError);
            Assert.AreEqual(ConsentState.UpdatingInfo, flow.State, "Sigue en curso: va a reintentar.");

            flow.Tick(29f);
            CollectionAssert.AreEqual(new[] { "update" }, client.Calls, "Todavía no pasó la espera.");

            flow.Tick(1.5f);
            CollectionAssert.AreEqual(new[] { "update", "update" }, client.Calls);
            Assert.AreEqual(1, flow.RetriesUsed);

            client.FinishUpdate();   // ahora sí hay red
            client.FinishForm();
            Assert.AreEqual(ConsentState.Ready, flow.State);
            Assert.AreEqual(1, ready);
        }

        [Test]
        public void WhenTheRetriesRunOut_TheFlowGivesUp_WithoutAds_AndWithoutLooping()
        {
            var client = new FakeClient();
            var flow = new ConsentFlow(client, maxRetries: 2, retrySeconds: 10f);
            flow.Start();

            for (int attempt = 0; attempt < 3; attempt++)
            {
                client.FinishUpdate("sin conexión");
                flow.Tick(10f);
            }

            Assert.AreEqual(3, client.Calls.FindAll(c => c == "update").Count, "El primer intento y dos reintentos.");
            Assert.AreEqual(ConsentState.Unavailable, flow.State);

            flow.Tick(1000f);
            Assert.AreEqual(3, client.Calls.FindAll(c => c == "update").Count, "No sigue reintentando para siempre.");
        }

        [Test]
        public void ALateAnswerFromAnOldRequest_IsIgnored()
        {
            var client = new FakeClient { ConsentGivenWhenFormCloses = true };
            var flow = new ConsentFlow(client, maxRetries: 3, retrySeconds: 5f);
            flow.Start();

            Action<string> oldCallback = client.PendingUpdate;
            client.FinishUpdate("tiempo agotado");
            flow.Tick(5f);   // reintento: el pedido nuevo reemplaza al viejo

            oldCallback(null);   // la respuesta del pedido viejo llega tarde
            CollectionAssert.AreEqual(new[] { "update", "update" }, client.Calls, "La respuesta vieja no abre el formulario.");

            client.FinishUpdate();
            Assert.AreEqual(ConsentState.ShowingForm, flow.State);
        }

        [Test]
        public void AFormError_WithoutConsent_EndsUnavailable_ButKeepsTheMessage()
        {
            var client = new FakeClient();
            var flow = new ConsentFlow(client);
            flow.Start();
            client.FinishUpdate();
            client.FinishForm("el formulario no cargó");

            Assert.AreEqual(ConsentState.Unavailable, flow.State);
            Assert.AreEqual("el formulario no cargó", flow.LastError);
        }

        [Test]
        public void PrivacyOptions_AreOfferedOnlyWhenRequired_AndWithdrawingConsentStopsTheAds()
        {
            var client = new FakeClient { CanRequest = true, PrivacyRequired = true };
            var flow = new ConsentFlow(client);
            flow.Start();
            Assert.IsTrue(flow.PrivacyOptionsRequired);

            string result = "sin llamar";
            flow.ShowPrivacyOptions(error => result = error ?? "ok");
            CollectionAssert.Contains(client.Calls, "privacy");

            client.CanRequest = false;   // el jugador retiró el consentimiento en el formulario
            client.PendingPrivacy(null);

            Assert.AreEqual("ok", result);
            Assert.IsFalse(flow.CanRequestAds);
            Assert.AreEqual(ConsentState.Unavailable, flow.State);

            Assert.IsFalse(new ConsentFlow(new FakeClient { PrivacyRequired = false }).PrivacyOptionsRequired);
        }

        [Test]
        public void StartingTwice_DoesNotAskUMPTwice()
        {
            var client = new FakeClient();
            var flow = new ConsentFlow(client);

            flow.Start();
            flow.Start();

            Assert.AreEqual(1, client.Calls.Count);
        }
    }

    public class AdMobConfigTests
    {
        private static AdMobConfig Create(bool useTest, string appId, string unitId)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<AdMobConfig>();
            var serialized = new UnityEditor.SerializedObject(config);
            serialized.FindProperty("useTestAds").boolValue = useTest;
            serialized.FindProperty("realAndroidAppId").stringValue = appId;
            serialized.FindProperty("realRewardedUnitId").stringValue = unitId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void ByDefault_TheTestIdsOfGoogleAreUsed()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<AdMobConfig>();

            Assert.IsTrue(config.UsesTestIds);
            Assert.AreEqual(AdMobConfig.TestAndroidAppId, config.AndroidAppId);
            Assert.AreEqual(AdMobConfig.TestRewardedUnitId, config.RewardedUnitId);
            Assert.IsTrue(AdMobConfig.LooksLikeAppId(config.AndroidAppId));
            Assert.IsTrue(AdMobConfig.LooksLikeUnitId(config.RewardedUnitId));
        }

        [Test]
        public void RealIds_AreUsedOnlyWhenBothAreLoaded_AndTheTestSwitchIsOff()
        {
            const string app = "ca-app-pub-1234567890123456~1234567890";
            const string unit = "ca-app-pub-1234567890123456/0987654321";

            Assert.AreEqual(AdMobConfig.TestAndroidAppId, Create(true, app, unit).AndroidAppId, "Con el interruptor de prueba, siempre los de prueba.");
            Assert.AreEqual(AdMobConfig.TestRewardedUnitId, Create(false, app, "").RewardedUnitId, "Falta el del bloque: se siguen usando los de prueba.");
            Assert.AreEqual(AdMobConfig.TestAndroidAppId, Create(false, "", unit).AndroidAppId, "Falta el de la app.");

            AdMobConfig real = Create(false, " " + app + " ", unit);
            Assert.IsFalse(real.UsesTestIds);
            Assert.AreEqual(app, real.AndroidAppId);
            Assert.AreEqual(unit, real.RewardedUnitId);
        }

        [Test]
        public void TheIdFormat_IsCheckedBeforePublishing()
        {
            Assert.IsTrue(AdMobConfig.LooksLikeAppId("ca-app-pub-1234567890123456~1234567890"));
            Assert.IsFalse(AdMobConfig.LooksLikeAppId("ca-app-pub-1234567890123456/1234567890"), "Un ID de bloque no es de app.");
            Assert.IsFalse(AdMobConfig.LooksLikeAppId("ca-app-pub-123~456"), "Corto.");
            Assert.IsFalse(AdMobConfig.LooksLikeAppId("ca-app-pub-12345678901234xx~1234567890"));
            Assert.IsFalse(AdMobConfig.LooksLikeAppId(""));
            Assert.IsFalse(AdMobConfig.LooksLikeAppId(null));

            Assert.IsTrue(AdMobConfig.LooksLikeUnitId("ca-app-pub-1234567890123456/1234567890"));
            Assert.IsFalse(AdMobConfig.LooksLikeUnitId("ca-app-pub-1234567890123456~1234567890"));
            Assert.IsFalse(AdMobConfig.LooksLikeUnitId("ca-app-pub-1234567890123456/"));
        }

        [Test]
        public void TheProject_NeverShipsRealAdMobIds()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<AdMobConfig>(FarmIdle.EditorTools.AdMobConfigSync.ConfigPath);
            Assert.IsNotNull(config, "Falta Assets/Resources/AdMobConfig.");
            Assert.IsTrue(config.UsesTestIds, "El repositorio solo trae los IDs de prueba de Google: los reales los carga el dueño del proyecto.");
        }
    }
}
