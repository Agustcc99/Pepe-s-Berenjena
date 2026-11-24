using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // IN-02: el cartel de la plaza que anuncia el cultivo del día ("HOY +50 %" y el nombre). Se reescribe al cambiar el cultivo
    // (DailyCropManager.Changed) o el idioma. Sin cultivo del día queda vacío. No se orienta a la cámara: el tablón es fijo.
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class DailyCropSign : MonoBehaviour
    {
        private TextMeshPro _text;
        private DailyCropManager _daily;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
        }

        private void OnEnable()
        {
            _daily = DailyCropManager.Instance;
            if (_daily != null) _daily.Changed += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_daily != null)
            {
                _daily.Changed -= Refresh;
                _daily = null;
            }

            Loc.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_text == null) return;

            CropData crop = _daily != null ? _daily.Crop : null;
            _text.text = crop != null
                ? Loc.Format("daily.sign", Mathf.RoundToInt(_daily.Bonus * 100f), crop.DisplayName)
                : Loc.Get("daily.sign_none");
        }
    }
}
