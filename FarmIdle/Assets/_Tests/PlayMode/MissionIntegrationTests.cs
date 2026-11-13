using System;
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
    // Las misiones diarias (F-05) en el MainLevel real, arrancando desde Bootstrap como el juego: al empezar a jugar tocan tres, la
    // tarjeta las muestra con su avance, completarlas y reclamarlas acredita el premio una sola vez, y lo reclamado sobrevive a cerrar.
    public class MissionIntegrationTests
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

        private static GameObject Backdrop => All<Transform>().First(t => t.name == "MissionBackdrop").gameObject;
        private static Button MissionButton => All<Button>().First(b => b.name == "MissionButton");
        private static TMP_Text ButtonLabel => MissionButton.GetComponentInChildren<TMP_Text>();

        private static IEnumerator StartPlaying()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();
            yield return null;
        }

        // Completa a mano las misiones de cosechar y vender (con los mismos eventos del juego: la mochila y la economía).
        private static void Harvest(int times)
        {
            CropData crop = Object.FindAnyObjectByType<PlotView>().Crop;
            for (int i = 0; i < times; i++)
            {
                Assert.IsTrue(BackpackManager.Instance.TryAdd(crop));
                BackpackManager.Instance.TryPop(out _);
            }
        }

        private static void Sell(int times)
        {
            CropData crop = Object.FindAnyObjectByType<PlotView>().Crop;
            for (int i = 0; i < times; i++) EconomyManager.Instance.SellProduct(crop);
        }

        [UnityTest]
        public IEnumerator AtTheStartOfPlaying_ANewPlayerGetsThreeMissions_AndTheButtonSaysSo()
        {
            yield return StartPlaying();

            MissionManager missions = MissionManager.Instance;
            Assert.IsNotNull(missions);
            Assert.AreEqual(3, missions.Count);
            Assert.AreEqual(MissionRules.DayKey(DateTime.Now), missions.Day);
            Assert.AreEqual("Misiones", ButtonLabel.text, "Nada para reclamar todavía.");
            Assert.IsFalse(Backdrop.activeSelf);

            MissionButton.onClick.Invoke();
            Assert.IsTrue(Backdrop.activeSelf);

            var rows = All<Transform>().Where(t => t.name.StartsWith("Row") && t.IsChildOf(Backdrop.transform) && t.gameObject.activeSelf).ToArray();
            Assert.AreEqual(3, rows.Length);
            for (int i = 0; i < 3; i++)
            {
                StringAssert.Contains(missions.DataAt(i).Target.ToString(), Backdrop.transform.Find("Card/Row" + i + "/Title").GetComponent<TMP_Text>().text);
                Assert.AreEqual("0 / " + missions.DataAt(i).Target, Backdrop.transform.Find("Card/Row" + i + "/Progress").GetComponent<TMP_Text>().text);
                Assert.IsFalse(Backdrop.transform.Find("Card/Row" + i + "/ClaimButton").gameObject.activeSelf, "Sin completar no hay botón.");
            }

            Backdrop.transform.Find("Card/MissionCloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(Backdrop.activeSelf);
        }

        [UnityTest]
        public IEnumerator CompletingAndClaiming_PaysOnce_AndTheButtonCountsWhatIsReady()
        {
            yield return StartPlaying();

            MissionManager missions = MissionManager.Instance;
            int harvest = Enumerable.Range(0, 3).First(i => missions.DataAt(i).Kind == MissionKind.Harvest);
            Harvest(missions.DataAt(harvest).Target);

            Assert.IsTrue(missions.IsCompleteAt(harvest));
            Assert.AreEqual("Misiones (1)", ButtonLabel.text, "El botón avisa que hay un premio listo.");

            MissionButton.onClick.Invoke();
            Button claim = Backdrop.transform.Find("Card/Row" + harvest + "/ClaimButton").GetComponent<Button>();
            Assert.IsTrue(claim.gameObject.activeSelf);
            Assert.IsTrue(claim.interactable);
            StringAssert.Contains("Premio", Backdrop.transform.Find("Card/Row" + harvest + "/Reward").GetComponent<TMP_Text>().text);

            long reward = missions.RewardCoinsAt(harvest) > 0 ? missions.RewardCoinsAt(harvest) : 0L;
            int fertilizerReward = missions.RewardFertilizerAt(harvest);
            long before = EconomyManager.Instance.Balance;
            int stockBefore = FertilizerManager.Instance.Stock;

            claim.onClick.Invoke();

            Assert.AreEqual(before + reward, EconomyManager.Instance.Balance);
            Assert.AreEqual(stockBefore + fertilizerReward, FertilizerManager.Instance.Stock);
            Assert.IsTrue(missions.IsClaimedAt(harvest));
            Assert.IsFalse(claim.interactable, "Ya reclamada.");
            Assert.AreEqual("Reclamada", Backdrop.transform.Find("Card/Row" + harvest + "/ClaimButton/Label").GetComponent<TMP_Text>().text);
            Assert.AreEqual("Misiones", ButtonLabel.text);

            claim.onClick.Invoke();
            Assert.AreEqual(before + reward, EconomyManager.Instance.Balance, "No se cobra dos veces.");
        }

        [UnityTest]
        public IEnumerator TheProgressAndTheClaim_SurviveClosingTheGame_OnTheSameDay()
        {
            yield return StartPlaying();

            MissionManager missions = MissionManager.Instance;
            int harvest = Enumerable.Range(0, 3).First(i => missions.DataAt(i).Kind == MissionKind.Harvest);
            string[] ids = Enumerable.Range(0, 3).Select(i => missions.DataAt(i).Id).ToArray();
            Harvest(missions.DataAt(harvest).Target);
            Assert.IsTrue(missions.TryClaim(harvest));

            Sell(1);
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            MemorySaveStorage storage = GameBoot.Storage;

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;
            GameManager.Instance.StartPlaying();
            yield return null;

            missions = MissionManager.Instance;
            CollectionAssert.AreEqual(ids, Enumerable.Range(0, 3).Select(i => missions.DataAt(i).Id).ToArray(), "El mismo día, las mismas misiones.");
            Assert.IsTrue(missions.IsClaimedAt(harvest), "Lo reclamado no se vuelve a ofrecer.");
            Assert.IsFalse(missions.TryClaim(harvest));
        }

        [UnityTest]
        public IEnumerator NothingCounts_WhileThePlayerIsNotPlaying()
        {
            yield return StartPlaying();

            MissionManager missions = MissionManager.Instance;
            int harvest = Enumerable.Range(0, 3).First(i => missions.DataAt(i).Kind == MissionKind.Harvest);

            GameManager.Instance.Pause();
            CropData crop = Object.FindAnyObjectByType<PlotView>().Crop;
            Assert.IsTrue(BackpackManager.Instance.TryAdd(crop));
            BackpackManager.Instance.TryPop(out _);

            Assert.AreEqual(0, missions.ProgressAt(harvest), "En pausa no se suma.");
            Assert.IsFalse(missions.TryClaim(harvest));

            GameManager.Instance.Resume();
            Assert.IsTrue(BackpackManager.Instance.TryAdd(crop));
            BackpackManager.Instance.TryPop(out _);
            Assert.AreEqual(1, missions.ProgressAt(harvest));
        }
    }
}
