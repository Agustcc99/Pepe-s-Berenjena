using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // El progreso offline (B-01) en el MainLevel real, arrancando desde Bootstrap como el juego: una partida guardada hace una hora deja
    // monedas pendientes, la tarjeta de bienvenida aparece recién al jugar, Reclamar y Duplicar (video) acreditan, y lo no reclamado
    // sobrevive a cerrar el juego.
    public class OfflineIntegrationTests
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

        private static T[] All<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 6f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static Button ButtonNamed(string name)
        {
            return All<Button>().First(button => button.name == name);
        }

        private static GameObject Card => All<Transform>().First(t => t.name == "OfflineBackdrop").gameObject;

        // Una partida guardada hace 'away', con 'pendingCoins' sin reclamar.
        private static MemorySaveStorage StorageAwayFor(TimeSpan away, long pendingCoins = 0L)
        {
            string mark = (DateTime.UtcNow - away).ToString("o", CultureInfo.InvariantCulture);
            var data = new SaveData();
            data.Set("offline", "{\"version\":1,\"lastSeenUtc\":\"" + mark + "\",\"pendingCoins\":" + pendingCoins + ",\"pendingSeconds\":0}");

            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));
            return storage;
        }

        private static IEnumerator StartPlaying()
        {
            GameManager.Instance.StartPlaying();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnOneHourAbsence_ShowsTheCard_OnlyOncePlaying_WithTheRightNumbers()
        {
            yield return GameBoot.BootToReady(StorageAwayFor(TimeSpan.FromHours(1)));
            yield return null;

            Assert.IsFalse(Card.activeSelf, "Detrás del 'Tocá para jugar' no se muestra.");
            Assert.IsFalse(OfflineManager.Instance.HasPending, "Los campos todavía no estaban activos para medir: se resuelve al jugar.");

            yield return StartPlaying();

            OfflineManager offline = OfflineManager.Instance;
            Assert.IsTrue(offline.HasPending);
            Assert.IsTrue(Card.activeSelf);
            Assert.Greater(offline.BaseCoinsPerSecond, 0f, "El campo inicial está registrado como fuente.");

            // 1 h x lo que rinde el campo x 25% de eficiencia (sin mejoras todavía), con un par de segundos de margen del arranque.
            double expected = 3600d * offline.BaseCoinsPerSecond * 0.15d;   // B-11: eficiencia 15 % (antes 25 %)
            Assert.AreEqual(expected, offline.PendingCoins, expected * 0.01d + 2d);

            string body = Card.transform.Find("Card/Body").GetComponent<TMP_Text>().text;
            StringAssert.Contains("1 h 0 min", body);
            StringAssert.Contains(offline.PendingCoins.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.'), body);
        }

        [UnityTest]
        public IEnumerator Claiming_CreditsThePendingCoins_AndClosesTheCard()
        {
            yield return GameBoot.BootToReady(StorageAwayFor(TimeSpan.FromHours(1)));
            yield return null;
            yield return StartPlaying();

            long pending = OfflineManager.Instance.PendingCoins;
            long start = EconomyManager.Instance.Balance;
            Assert.Greater(pending, 0L);

            ButtonNamed("OfflineClaimButton").onClick.Invoke();

            Assert.AreEqual(start + pending, EconomyManager.Instance.Balance);
            Assert.IsFalse(OfflineManager.Instance.HasPending);
            Assert.IsFalse(Card.activeSelf);
        }

        [UnityTest]
        public IEnumerator Doubling_WatchesTheVideo_ThenCreditsTwice()
        {
            yield return GameBoot.BootToReady(StorageAwayFor(TimeSpan.FromHours(1)));
            yield return null;
            ((MockAdService)AdsManager.Instance.Service).DurationSeconds = 0.3f;
            yield return StartPlaying();

            long pending = OfflineManager.Instance.PendingCoins;
            long start = EconomyManager.Instance.Balance;
            Button doubleButton = ButtonNamed("OfflineDoubleButton");
            yield return WaitFor(() => doubleButton.interactable);
            Assert.IsTrue(doubleButton.interactable, "Con el anuncio simulado listo, se puede pedir el video.");

            doubleButton.onClick.Invoke();
            Assert.IsTrue(AdsManager.Instance.IsShowing, "El video empezó por la acción del jugador.");
            Assert.AreEqual(start, EconomyManager.Instance.Balance, "Hasta que no termina el video no se acredita nada.");

            yield return WaitFor(() => !OfflineManager.Instance.HasPending);

            Assert.AreEqual(start + pending * 2L, EconomyManager.Instance.Balance, "El doble de lo pendiente.");
            Assert.IsFalse(Card.activeSelf);
        }

        [UnityTest]
        public IEnumerator ASaveWithoutTheSection_OrAClockGoneBack_ShowsNoCard()
        {
            yield return GameBoot.BootToReady();   // partida nueva: sin sección
            yield return null;
            yield return StartPlaying();
            Assert.IsFalse(OfflineManager.Instance.HasPending);
            Assert.IsFalse(Card.activeSelf);

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(StorageAwayFor(TimeSpan.FromHours(-4)));   // la hora guardada es del futuro
            yield return null;
            yield return StartPlaying();
            Assert.IsFalse(OfflineManager.Instance.HasPending);
            Assert.IsFalse(Card.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheStorehousePad_IsInThePlaza_AndBuyingItRaisesTheRealCap()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            yield return StartPlaying();

            UpgradePad pad = All<UpgradePad>().First(p => p.Upgrade.Kind == UpgradeKind.OfflineCap);
            Assert.AreEqual(3.1f, pad.transform.position.x, 1e-3f, "Sector Almacén de la plaza (este de la avenida).");
            Assert.AreEqual(-25f, pad.transform.position.z, 1e-3f, "Sector Almacén de la plaza, al fondo.");
            StringAssert.Contains(pad.Upgrade.CostOfLevel(1).ToString(), pad.Label.text);
            StringAssert.Contains(pad.Upgrade.DisplayName, pad.Label.text);

            OfflineManager offline = OfflineManager.Instance;
            float baseCap = offline.CapSeconds;
            Assert.AreEqual(7200f, baseCap);

            // Se paga por la misma puerta que las demás mejoras.
            EconomyManager.Instance.AddCoins(pad.Upgrade.CostOfLevel(1));
            Assert.Greater(ProgressionManager.Instance.TryPay(pad.Upgrade, pad.Upgrade.CostOfLevel(1)), 0L);
            Assert.AreEqual(1, ProgressionManager.Instance.LevelOf(pad.Upgrade));
            Assert.AreEqual(baseCap + 3600f, offline.CapSeconds);
        }

        [UnityTest]
        public IEnumerator AnUnclaimedReward_SurvivesClosingTheGame()
        {
            yield return GameBoot.BootToReady(StorageAwayFor(TimeSpan.FromHours(1)));
            yield return null;
            yield return StartPlaying();

            long pending = OfflineManager.Instance.PendingCoins;
            Assert.Greater(pending, 0L);
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            MemorySaveStorage storage = GameBoot.Storage;

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;
            yield return StartPlaying();

            Assert.AreEqual(pending, OfflineManager.Instance.PendingCoins, "Cerrar sin reclamar no pierde las monedas ni las duplica.");
            Assert.IsTrue(Card.activeSelf);
        }
    }
}
