using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // El botón "Misiones" del HUD y su tarjeta (F-05): las misiones del día con su avance y el botón para reclamar cada premio.
    // Solo habla con MissionManager (leer y reclamar): no conoce el mundo ni los sistemas que cuentan el avance.
    //   - El botón dice cuántas misiones tienen el premio listo ("Misiones (2)"); eso se reescribe solo cuando el número cambia.
    //   - Las filas se releen mientras la tarjeta está abierta (avance, premio con la producción de hoy, estado del botón).
    //   - Este componente vive en un contenedor siempre activo; solo la tarjeta se prende y apaga.
    public sealed class MissionPanelView : MonoBehaviour
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
        [SerializeField] private Row[] rows = new Row[0];

        private MissionManager _missions;
        private readonly StringBuilder _text = new StringBuilder(48);
        private int _shownClaimable = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            _missions = MissionManager.Instance;
            if (_missions == null)
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
                rows[i].claim.onClick.AddListener(() => OnClaimClicked(index));
            }

            _missions.Changed += Refresh;
            Loc.Changed += OnLanguageChanged;

            panel.SetActive(false);
            _shownClaimable = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (_missions == null) return;

            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
            for (int i = 0; i < rows.Length; i++) rows[i].claim.onClick.RemoveAllListeners();

            _missions.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
            _missions = null;
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

        private void OnClaimClicked(int index)
        {
            _missions.TryClaim(index);
            if (AllClaimed()) Close();   // se reclamó la última: la tarjeta no tiene nada más para mostrar
        }

        // Todas las misiones de hoy ya reclamadas: el botón del HUD se esconde hasta el día siguiente (playtest Q-01: seguía tapando pantalla).
        private bool AllClaimed()
        {
            if (_missions.Count == 0) return false;

            for (int i = 0; i < _missions.Count; i++)
            {
                if (!_missions.IsClaimedAt(i)) return false;
            }

            return true;
        }

        private void OnLanguageChanged()
        {
            _shownClaimable = -1;
            Refresh();
        }

        private void Refresh()
        {
            if (_missions == null) return;

            int claimable = _missions.ClaimableCount;
            if (claimable != _shownClaimable)
            {
                _shownClaimable = claimable;
                _text.Clear();
                _text.Append(Loc.Get("mission.button"));
                if (claimable > 0) _text.Append(" (").Append(claimable).Append(')');
                openLabel.text = _text.ToString();
            }

            bool allClaimed = AllClaimed();
            if (openButton.gameObject.activeSelf == allClaimed) openButton.gameObject.SetActive(!allClaimed);

            if (!panel.activeSelf) return;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool exists = i < _missions.Count;
                row.root.SetActive(exists);
                if (exists) RefreshRow(row, i);
            }
        }

        private void RefreshRow(Row row, int index)
        {
            MissionData data = _missions.DataAt(index);
            int progress = _missions.ProgressAt(index);
            bool complete = _missions.IsCompleteAt(index);
            bool claimed = _missions.IsClaimedAt(index);

            row.title.text = data.Describe();

            _text.Clear();
            _text.Append(progress).Append(" / ").Append(data.Target);
            row.progress.text = _text.ToString();

            row.fill.localScale = new Vector3(Mathf.Max(0.001f, progress / (float)data.Target), 1f, 1f);

            int fertilizer = _missions.RewardFertilizerAt(index);
            row.reward.text = fertilizer > 0
                ? Loc.Format("mission.reward_fert", fertilizer)
                : Loc.Format("mission.reward_coins", FormatCoins(_missions.RewardCoinsAt(index)));

            row.claim.gameObject.SetActive(complete);
            row.claim.interactable = !claimed;
            row.claimLabel.text = Loc.Get(claimed ? "mission.claimed" : "mission.claim");
        }

        // 12.345 en español y 12,345 en inglés.
        private static string FormatCoins(long coins)
        {
            string text = coins.ToString("N0", CultureInfo.InvariantCulture);
            return Loc.DecimalSeparator == ',' ? text.Replace(',', '.') : text;
        }
    }
}
