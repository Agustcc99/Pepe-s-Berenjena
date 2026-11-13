using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // El premio de login diario del HUD (F-07): un botón "Premio diario" que solo se ve cuando hay premio listo y una tarjeta con los 7
    // días de la racha, el premio de hoy y el botón para reclamarlo. Solo habla con DailyRewardManager (leer y reclamar).
    //   - Se abre sola cuando el premio queda listo (al empezar a jugar, o con el día nuevo), pero NO durante la guía de los primeros minutos
    //     ni si hay una tarjeta de "Bienvenido de vuelta" pendiente: en esos casos queda el botón.
    //   - Reclamar deja la tarjeta abierta mostrando el día cumplido ("Volvé mañana"); el botón del HUD se esconde.
    //   - Este componente vive en un contenedor siempre activo; solo la tarjeta se prende y apaga.
    public sealed class DailyRewardView : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public Image back;
            public TMP_Text day;
            public TMP_Text sub;
        }

        private static readonly Color ClaimedColor = new Color(0.25f, 0.45f, 0.25f, 0.95f);
        private static readonly Color TodayColor = new Color(0.93f, 0.68f, 0.12f, 1f);
        private static readonly Color FutureColor = new Color(0.08f, 0.13f, 0.09f, 0.95f);

        [SerializeField] private Button openButton;
        [SerializeField, Tooltip("El fondo oscuro con la tarjeta (arranca apagado).")]
        private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private Slot[] slots = new Slot[0];

        private DailyRewardManager _daily;
        private TutorialManager _tutorial;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool ButtonVisible => openButton != null && openButton.gameObject.activeSelf;

        private void OnEnable()
        {
            _daily = DailyRewardManager.Instance;
            if (_daily == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                if (panel != null) panel.SetActive(false);
                return;
            }

            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            claimButton.onClick.AddListener(OnClaimClicked);

            _daily.Changed += Refresh;
            _daily.BecameAvailable += OnBecameAvailable;
            Loc.Changed += Refresh;

            _tutorial = TutorialManager.Instance;
            if (_tutorial != null) _tutorial.StepChanged += OnTutorialStep;

            panel.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_daily == null) return;

            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
            claimButton.onClick.RemoveListener(OnClaimClicked);

            _daily.Changed -= Refresh;
            _daily.BecameAvailable -= OnBecameAvailable;
            Loc.Changed -= Refresh;
            if (_tutorial != null) _tutorial.StepChanged -= OnTutorialStep;

            _tutorial = null;
            _daily = null;
        }

        public void Open()
        {
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        private void OnClaimClicked()
        {
            _daily.TryClaim();
        }

        private bool TutorialRunning => _tutorial != null && _tutorial.IsActive;

        private void OnTutorialStep(TutorialStep step)
        {
            Refresh();
        }

        // El premio quedó listo: se muestra la tarjeta, salvo durante la guía o con la tarjeta de "Bienvenido de vuelta" pendiente.
        private void OnBecameAvailable()
        {
            OfflineManager offline = OfflineManager.Instance;
            if (TutorialRunning || (offline != null && offline.HasPending)) return;

            Open();
        }

        private void Refresh()
        {
            if (_daily == null) return;

            bool claimable = _daily.IsClaimable;
            bool show = claimable && !TutorialRunning;
            if (openButton.gameObject.activeSelf != show) openButton.gameObject.SetActive(show);

            if (!panel.activeSelf) return;

            int day = _daily.Day;
            for (int i = 0; i < slots.Length; i++)
            {
                int number = i + 1;
                bool claimed = claimable ? number < day : number <= day;
                bool today = claimable && number == day;

                slots[i].back.color = today ? TodayColor : (claimed ? ClaimedColor : FutureColor);
                slots[i].day.text = Loc.Format("daily.day", number);
                slots[i].day.color = today ? new Color(0.20f, 0.12f, 0.02f) : Color.white;

                int fertilizer = _daily.FertilizerOfDay(number);
                slots[i].sub.text = fertilizer > 0 ? Loc.Format("daily.cell_fert", fertilizer) : Loc.Get("daily.cell_coins");
                slots[i].sub.color = today ? new Color(0.20f, 0.12f, 0.02f) : new Color(1f, 0.85f, 0.3f);
            }

            if (claimable)
            {
                int fertilizer = _daily.RewardFertilizerForDay(day);
                rewardText.text = fertilizer > 0
                    ? Loc.Format("mission.reward_fert", fertilizer)
                    : Loc.Format("mission.reward_coins", AchievementData.FormatNumber(_daily.RewardCoinsForDay(day)));
            }
            else
            {
                rewardText.text = Loc.Get("daily.come_back");
            }

            streakText.text = Loc.Format("daily.streak", _daily.Streak);
            claimButton.interactable = claimable;
            claimLabel.text = Loc.Get(claimable ? "mission.claim" : "mission.claimed");
        }
    }
}
