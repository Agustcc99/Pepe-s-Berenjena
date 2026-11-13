using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // El botón "Logros" del HUD y su tarjeta (F-06): la lista de logros con su avance (con scroll) y el botón para reclamar cada premio.
    // Solo habla con AchievementManager (leer y reclamar): no conoce el mundo ni los sistemas que cuentan el avance.
    //   - El botón dice cuántos logros tienen el premio listo ("Logros (2)"); eso se reescribe solo cuando el número cambia.
    //   - Las filas se arman una vez en el builder (una por logro) y se releen mientras la tarjeta está abierta.
    //   - Este componente vive en un contenedor siempre activo; solo la tarjeta se prende y apaga.
    public sealed class AchievementPanelView : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public TMP_Text title;
            public TMP_Text progress;
            public TMP_Text reward;
            [Tooltip("La barra de avance: se escala en X de 0 a 1.")]
            public RectTransform fill;
            public Button claim;
            public TMP_Text claimLabel;
        }

        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text openLabel;
        [SerializeField, Tooltip("El fondo oscuro con la tarjeta (arranca apagado).")]
        private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField, Tooltip("La lista con scroll: se vuelve al principio cada vez que se abre.")]
        private ScrollRect scroll;
        [SerializeField] private Row[] rows = new Row[0];

        private AchievementManager _achievements;
        private readonly StringBuilder _text = new StringBuilder(48);
        private int _shownClaimable = -1;

        public bool IsOpen => panel != null && panel.activeSelf;
        public int RowCount => rows.Length;

        private void OnEnable()
        {
            _achievements = AchievementManager.Instance;
            if (_achievements == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                if (panel != null) panel.SetActive(false);
                return;
            }

            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;   // una vez por fila al activarse
                rows[i].claim.onClick.AddListener(() => _achievements.TryClaim(index));
            }

            _achievements.Changed += Refresh;
            Loc.Changed += OnLanguageChanged;

            panel.SetActive(false);
            _shownClaimable = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (_achievements == null) return;

            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
            for (int i = 0; i < rows.Length; i++) rows[i].claim.onClick.RemoveAllListeners();

            _achievements.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
            _achievements = null;
        }

        public void Open()
        {
            panel.SetActive(true);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        private void OnLanguageChanged()
        {
            _shownClaimable = -1;
            Refresh();
        }

        private void Refresh()
        {
            if (_achievements == null) return;

            int claimable = _achievements.ClaimableCount;
            if (claimable != _shownClaimable)
            {
                _shownClaimable = claimable;
                _text.Clear();
                _text.Append(Loc.Get("achievement.button"));
                if (claimable > 0) _text.Append(" (").Append(claimable).Append(')');
                openLabel.text = _text.ToString();
            }

            if (!panel.activeSelf) return;

            for (int i = 0; i < rows.Length; i++)
            {
                bool exists = i < _achievements.Count;
                rows[i].root.SetActive(exists);
                if (exists) RefreshRow(rows[i], i);
            }
        }

        private void RefreshRow(Row row, int index)
        {
            AchievementData data = _achievements.DataAt(index);
            long progress = _achievements.ProgressAt(index);
            bool complete = _achievements.IsCompleteAt(index);
            bool claimed = _achievements.IsClaimedAt(index);

            row.title.text = data.Describe();

            _text.Clear();
            _text.Append(AchievementData.FormatNumber(progress)).Append(" / ").Append(AchievementData.FormatNumber(data.Target));
            row.progress.text = _text.ToString();

            row.fill.localScale = new Vector3(Mathf.Max(0.001f, progress / (float)data.Target), 1f, 1f);

            int fertilizer = _achievements.RewardFertilizerAt(index);
            row.reward.text = fertilizer > 0
                ? Loc.Format("mission.reward_fert", fertilizer)
                : Loc.Format("mission.reward_coins", AchievementData.FormatNumber(_achievements.RewardCoinsAt(index)));

            row.claim.gameObject.SetActive(complete);
            row.claim.interactable = !claimed;
            row.claimLabel.text = Loc.Get(claimed ? "mission.claimed" : "mission.claim");
        }
    }
}
