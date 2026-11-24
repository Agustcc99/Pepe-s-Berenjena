using System;
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
    // "Nueva temporada" (F-03) en el MainLevel real, arrancando desde Bootstrap como el juego: el botón aparece recién con todo comprado,
    // la tarjeta explica el cambio y no reinicia nada hasta confirmar, el reinicio vuelve a cerrar las zonas con su portón y se guarda.
    public class SeasonIntegrationTests
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

        private static IEnumerator BootAndPlay(MemorySaveStorage storage = null)
        {
            yield return storage != null ? GameBoot.BootToReady(storage) : GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();
        }

        private static Button ButtonNamed(string name)
        {
            return All<Button>().First(button => button.name == name);
        }

        private static GameObject Backdrop => All<Transform>().First(t => t.name == "SeasonBackdrop").gameObject;

        // Todas las zonas abiertas y las mejoras al máximo, como al final de una temporada.
        private static ProgressData EverythingBought()
        {
            ProgressionManager progression = ProgressionManager.Instance;
            var data = new ProgressData();
            var zones = new List<string>();
            foreach (ZoneData zone in progression.Zones) zones.Add(zone.Id);
            data.unlockedZones = zones.ToArray();

            var levels = new List<UpgradeLevelEntry>();
            foreach (UpgradeData upgrade in progression.Upgrades) levels.Add(new UpgradeLevelEntry { id = upgrade.Id, level = upgrade.MaxLevel });
            data.upgradeLevels = levels.ToArray();
            data.xp = 5000;
            return data;
        }

        [UnityTest]
        public IEnumerator TheSeasonButton_IsHidden_UntilEverythingIsBought()
        {
            yield return BootAndPlay();

            Assert.IsNotNull(PrestigeManager.Instance);
            Assert.AreEqual(0, PrestigeManager.Instance.Seasons);
            Assert.AreEqual(1, All<SeasonPanelView>().Length);
            Assert.IsFalse(ButtonNamed("SeasonButton").gameObject.activeSelf, "Al empezar no hay nada que cerrar.");
            Assert.IsFalse(Backdrop.activeSelf);

            ProgressionManager.Instance.Restore(EverythingBought());
            Assert.IsTrue(ButtonNamed("SeasonButton").gameObject.activeSelf, "Con todo comprado aparece.");

            // Si algo vuelve a faltar, se va (y no deja la tarjeta abierta).
            ButtonNamed("SeasonButton").onClick.Invoke();
            Assert.IsTrue(Backdrop.activeSelf);
            ProgressionManager.Instance.Restore(new ProgressData());
            Assert.IsFalse(ButtonNamed("SeasonButton").gameObject.activeSelf);
            Assert.IsFalse(Backdrop.activeSelf);
        }

        // F-11: la tienda de semillas en el MainLevel real: oculta al principio, aparece con la primera temporada, compra y se guarda.
        [UnityTest]
        public IEnumerator TheSeedShop_AppearsAfterTheFirstSeason_AndBuysAnUpgrade()
        {
            yield return BootAndPlay();

            PrestigeManager prestige = PrestigeManager.Instance;
            Assert.AreEqual(1, All<PrestigeShopView>().Length);
            Assert.IsFalse(ButtonNamed("ShopButton").gameObject.activeSelf, "Sin semillas ni temporadas no se muestra.");

            ProgressionManager.Instance.Restore(EverythingBought());
            Assert.IsTrue(prestige.TryStartSeason());
            Assert.Greater(prestige.Seeds, 0);
            Assert.IsTrue(ButtonNamed("ShopButton").gameObject.activeSelf, "Con semillas aparece.");

            ButtonNamed("ShopButton").onClick.Invoke();
            GameObject shop = All<Transform>().First(t => t.name == "ShopBackdrop").gameObject;
            Assert.IsTrue(shop.activeSelf);

            int seedsBefore = prestige.Seeds;
            Button row = ButtonNamed("ShopRow" + 0);
            Assert.IsTrue(row.interactable, "La primera mejora cuesta 1 semilla.");
            row.onClick.Invoke();
            Assert.AreEqual(1, prestige.ShopLevel(0));
            Assert.AreEqual(seedsBefore - prestige.ShopItem(0).CostOf(0), prestige.Seeds);

            ButtonNamed("ShopCloseButton").onClick.Invoke();
            Assert.IsFalse(shop.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheCard_ExplainsTheChange_AndNothingResetsUntilConfirmed()
        {
            yield return BootAndPlay();

            ProgressionManager.Instance.Restore(EverythingBought());
            EconomyManager.Instance.AddCoins(777);
            long coins = EconomyManager.Instance.Balance;

            ButtonNamed("SeasonButton").onClick.Invoke();
            Assert.IsTrue(Backdrop.activeSelf);

            string body = Backdrop.transform.Find("Card/Body").GetComponent<TMP_Text>().text;
            StringAssert.Contains("x1,25", body, "Lo que pasa a pagar la venta (en español, con coma).");
            StringAssert.Contains("ahora x1", body);

            ButtonNamed("SeasonNoButton").onClick.Invoke();
            Assert.IsFalse(Backdrop.activeSelf);
            Assert.AreEqual(0, PrestigeManager.Instance.Seasons);
            Assert.AreEqual(coins, EconomyManager.Instance.Balance, "Cancelar no reinicia nada.");
            Assert.AreEqual(ProgressionManager.Instance.Zones.Count, ProgressionManager.Instance.UnlockedZoneCount);
        }

        [UnityTest]
        public IEnumerator ConfirmingTheSeason_ClosesTheZonesAgain_WithTheirGate_AndSaves()
        {
            yield return BootAndPlay();

            // Una zona abierta jugando: al completarse el pago su portón se achica y se apaga (y apagado no escucharía el reinicio).
            ProgressionManager progression = ProgressionManager.Instance;
            ZoneController controller = All<ZoneController>().First(zone => zone.Zone.RequiredSeason == 0);   // F-24: una de las de siempre
            ZoneUnlockPad gate = controller.GetComponentsInChildren<ZoneUnlockPad>(true).First(pad => pad.Zone == controller.Zone);
            EconomyManager.Instance.AddCoins(progression.CostOf(controller.Zone));
            Assert.Greater(progression.TryPay(controller.Zone, progression.CostOf(controller.Zone)), 0L);
            yield return WaitFor(() => !gate.gameObject.activeSelf);
            Assert.IsFalse(gate.gameObject.activeSelf, "El portón se apagó al abrirse la zona.");
            Assert.IsTrue(controller.UnlockedRoot.activeSelf);

            progression.Restore(EverythingBought());
            Assert.IsTrue(PrestigeManager.Instance.IsReady);

            int writesBefore = GameBoot.Storage.WriteCount;
            ButtonNamed("SeasonButton").onClick.Invoke();
            ButtonNamed("SeasonYesButton").onClick.Invoke();

            Assert.AreEqual(1, PrestigeManager.Instance.Seasons);
            Assert.IsFalse(Backdrop.activeSelf, "La tarjeta se cierra.");
            Assert.IsFalse(ButtonNamed("SeasonButton").gameObject.activeSelf, "Recién empezada, no hay nada para cerrar.");
            Assert.AreEqual(0, progression.UnlockedZoneCount);
            Assert.Greater(GameBoot.Storage.WriteCount, writesBefore, "Se escribió la partida al reiniciar, sin esperar el guardado diferido.");

            foreach (ZoneController zone in All<ZoneController>())
            {
                // F-24: las granjas de temporada que todavía no aparecieron no se ven (en la temporada 1 solo se suma el Campo del Alba).
                Assert.AreEqual(progression.IsZoneAvailable(zone.Zone), zone.LockedRoot.activeSelf, zone.name + ": vuelve a verse cerrada (si existe esta temporada).");
                Assert.IsFalse(zone.UnlockedRoot.activeSelf, zone.name + ": sus parcelas se apagan.");
            }

            Assert.IsTrue(gate.gameObject.activeSelf, "El portón vuelve a estar para pagarlo de nuevo.");
            Assert.AreEqual(EconomyManager.Instance.Pricing.PrestigeMultiplier, PrestigeManager.Instance.Multiplier, 1e-6f);
            Assert.Greater(PrestigeManager.Instance.Multiplier, 1f);
        }

        [UnityTest]
        public IEnumerator TheSeasons_AreSaved_AndComeBackOnTheNextBoot()
        {
            yield return BootAndPlay();
            ProgressionManager.Instance.Restore(EverythingBought());
            Assert.IsTrue(PrestigeManager.Instance.TryStartSeason());

            float expected = PrestigeManager.Instance.Multiplier;
            MemorySaveStorage storage = GameBoot.Storage;

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(1, PrestigeManager.Instance.Seasons);
            Assert.AreEqual(expected, EconomyManager.Instance.Pricing.PrestigeMultiplier, 1e-6f);
            Assert.AreEqual(0, ProgressionManager.Instance.UnlockedZoneCount, "Y la temporada nueva sigue sin zonas.");
        }
    }
}
