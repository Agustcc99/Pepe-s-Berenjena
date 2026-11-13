using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // El botón "¡Nueva temporada!" del HUD y su confirmación (F-03). El botón aparece cuando ya no queda nada por comprar
    // (PrestigeManager.IsReady) y abre una tarjeta que explica qué se reinicia, qué se conserva y cuánto pasa a pagar la venta.
    // Nada se reinicia hasta tocar "¡Empezar!". Solo habla con PrestigeManager (y escucha al progreso para saber cuándo mostrarse):
    // no conoce el mundo.
    //   - Este componente vive en un contenedor siempre activo; solo el botón y la tarjeta se prenden y apagan.
    //   - Los textos fijos los pone LocalizedText; acá se arma el del cuerpo (lleva los multiplicadores).
    public sealed class SeasonPanelView : MonoBehaviour
    {
        [SerializeField, Tooltip("El botón del HUD. Se ve solo cuando hay una temporada para cerrar.")]
        private Button openButton;

        [SerializeField, Tooltip("El fondo oscuro con la tarjeta de confirmación (arranca apagado).")]
        private GameObject panel;

        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private PrestigeManager _prestige;
        private ProgressionManager _progression;

        public bool IsButtonVisible => openButton != null && openButton.gameObject.activeSelf;
        public bool IsPanelOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            _prestige = PrestigeManager.Instance;
            _progression = ProgressionManager.Instance;
            if (_prestige == null || _progression == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                if (panel != null) panel.SetActive(false);
                _prestige = null;
                _progression = null;
                return;
            }

            openButton.onClick.AddListener(Open);
            yesButton.onClick.AddListener(Confirm);
            noButton.onClick.AddListener(Close);
            _prestige.Changed += Refresh;
            _progression.Changed += OnProgressChanged;
            Loc.Changed += Refresh;

            panel.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_prestige == null) return;

            openButton.onClick.RemoveListener(Open);
            yesButton.onClick.RemoveListener(Confirm);
            noButton.onClick.RemoveListener(Close);
            _prestige.Changed -= Refresh;
            _progression.Changed -= OnProgressChanged;
            Loc.Changed -= Refresh;
            _prestige = null;
            _progression = null;
        }

        public void Open()
        {
            if (_prestige == null || !_prestige.IsReady) return;

            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        // Solo el "¡Empezar!" llega acá. Si el reinicio no se pudo (por ejemplo, en pausa) la tarjeta queda abierta.
        private void Confirm()
        {
            if (_prestige != null && _prestige.TryStartSeason()) Close();
        }

        // Los pagos a medias y la XP no cambian lo que hay por comprar: no hace falta releer nada.
        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.PaymentChanged || change.Kind == ProgressChangeKind.XpChanged) return;

            Refresh();
        }

        private void Refresh()
        {
            if (_prestige == null) return;

            bool ready = _prestige.IsReady;
            openButton.gameObject.SetActive(ready);
            if (!ready && panel.activeSelf) panel.SetActive(false);

            if (ready && bodyLabel != null && panel.activeSelf)
            {
                bodyLabel.text = Loc.Format("season.body", FormatMultiplier(_prestige.NextMultiplier), FormatMultiplier(_prestige.Multiplier), _prestige.NextSeeds);
            }
        }

        // "1,5" en español y "1.5" en inglés, sin ceros de más.
        private static string FormatMultiplier(float multiplier)
        {
            return multiplier.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', Loc.DecimalSeparator);
        }
    }
}
