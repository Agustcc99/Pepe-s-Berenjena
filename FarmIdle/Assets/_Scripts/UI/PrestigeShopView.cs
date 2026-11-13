using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // La tienda de semillas doradas (F-11): un botón del HUD (aparece cuando hay semillas o ya se hizo una temporada) y una tarjeta con
    // una fila por mejora. Tocar una fila compra su próximo nivel si alcanzan las semillas. Solo habla con PrestigeManager.
    //   - Vive en un contenedor siempre activo; solo el botón y la tarjeta se prenden y apagan.
    //   - Las filas son fijas (las crea el builder): una por mejora del catálogo, en el mismo orden.
    public sealed class PrestigeShopView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text seedsLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] rowButtons;
        [SerializeField] private TMP_Text[] rowLabels;

        private PrestigeManager _prestige;

        public bool IsButtonVisible => openButton != null && openButton.gameObject.activeSelf;
        public bool IsPanelOpen => panel != null && panel.activeSelf;

        private void OnEnable()
        {
            _prestige = PrestigeManager.Instance;
            if (_prestige == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                if (panel != null) panel.SetActive(false);
                return;
            }

            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < rowButtons.Length; i++)
            {
                int index = i;   // una captura por fila, solo al activarse
                rowButtons[i].onClick.AddListener(() => Buy(index));
            }

            _prestige.Changed += Refresh;
            Loc.Changed += Refresh;
            panel.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_prestige == null) return;

            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            for (int i = 0; i < rowButtons.Length; i++) rowButtons[i].onClick.RemoveAllListeners();

            _prestige.Changed -= Refresh;
            Loc.Changed -= Refresh;
            _prestige = null;
        }

        public void Open()
        {
            if (_prestige == null) return;

            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        private void Buy(int index)
        {
            if (_prestige != null) _prestige.TryBuyShop(index);   // si compra, Changed refresca
        }

        private void Refresh()
        {
            if (_prestige == null) return;

            // El botón se muestra desde la primera temporada: antes no hay nada que gastar ni se sabe qué es.
            bool visible = _prestige.Seasons > 0 || _prestige.Seeds > 0;
            openButton.gameObject.SetActive(visible);
            if (!visible && panel.activeSelf) panel.SetActive(false);
            if (!panel.activeSelf) return;

            seedsLabel.text = Loc.Format("shop.seeds", _prestige.Seeds);

            int count = Mathf.Min(_prestige.ShopCount, rowButtons.Length);
            for (int i = 0; i < rowButtons.Length; i++)
            {
                bool exists = i < count;
                rowButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                PrestigeShopItem item = _prestige.ShopItem(i);
                string nameKey = "shop.name." + item.Id;   // claves armadas con el id: ver LocalizationTests
                string descriptionKey = "shop.desc." + item.Id;
                string name = Loc.Get(nameKey);
                string description = Loc.Get(descriptionKey);
                int level = _prestige.ShopLevel(i);

                rowLabels[i].text = level >= item.MaxLevel
                    ? Loc.Format("shop.row_max", name, description)
                    : Loc.Format("shop.row", name, level, item.MaxLevel, description, _prestige.ShopNextCost(i));
                rowButtons[i].interactable = _prestige.CanBuyShop(i);
            }
        }
    }
}
