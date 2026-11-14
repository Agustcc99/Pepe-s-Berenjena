using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // El aviso de "mochila llena": un "MAX" en el mundo, sobre la cabeza del granjero, que aparece cuando ya no entra ni un
    // producto más y se va apenas se libera un lugar (al vender, o al comprar más capacidad). Antes la mochila llena solo se
    // veía en el panel de debug.
    // Solo mira: lee la mochila como IReadOnlyInventory (BackpackManager), igual que la pila de la espalda, y no decide nada.
    // El texto mira a la cámara y "late" suave mientras está (tiempo sin escala, cosmético). Sin etiqueta o sin mochila no hace nada.
    public sealed class BackpackFullIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("Texto en el mundo, hijo del jugador. Arranca apagado.")]
        private TMP_Text label;

        [SerializeField, Min(0f)] private float pulseAmount = 0.12f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("J-08: cuánto dura el festejo \"¡Llena!\" al llenarse, antes de volver al MAX.")]
        private float cheerSeconds = 1.2f;
        [SerializeField, Min(1f), Tooltip("Cuánto crece el cartel al aparecer el festejo.")]
        private float cheerScale = 1.7f;

        private const string FullKey = "pad.max";
        private const string CheerKey = "backpack.full_cheer";

        private IReadOnlyInventory _inventory;
        private Camera _camera;
        private bool _cheering;
        private float _cheerStartedAt;

        // Para pruebas y depuración.
        public bool IsCheering => _cheering;
        public bool IsShowing => label != null && label.enabled;
        public TMP_Text Label => label;

        private void OnEnable()
        {
            if (label == null)
            {
                Debug.LogError("[FarmIdle] BackpackFullIndicator '" + name + "': falta el texto.", this);
                return;
            }

            BackpackManager backpacks = BackpackManager.Instance;
            if (backpacks == null)
            {
                Debug.LogError("[FarmIdle] BackpackFullIndicator: no hay BackpackManager. Arrancá el juego desde la escena Bootstrap.", this);
                label.enabled = false;
                return;
            }

            _inventory = backpacks.Backpack;
            _inventory.Changed += OnInventoryChanged;
            _inventory.CapacityChanged += OnCapacityChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.CapacityChanged -= OnCapacityChanged;
                _inventory = null;
            }

            if (label != null)
            {
                EndCheer();
                label.enabled = false;
            }
        }

        private void OnInventoryChanged(InventoryChange change)
        {
            bool wasShowing = label.enabled;
            Refresh();

            // J-08: llenarla cosechando es un logro. El cartel entra grande y dice "¡Llena!" un momento; después queda el MAX.
            // Una partida que se carga con la mochila llena no festeja.
            if (!wasShowing && label.enabled && change.Added && !change.IsReplacement && cheerSeconds > 0f)
            {
                _cheering = true;
                _cheerStartedAt = Time.unscaledTime;
                label.text = Loc.Get(CheerKey);
            }
        }

        private void EndCheer()
        {
            if (!_cheering) return;

            _cheering = false;
            label.text = Loc.Get(FullKey);
        }

        private void OnCapacityChanged(int capacity)
        {
            Refresh();
        }

        private void Refresh()
        {
            label.enabled = _inventory.IsFull;
            if (!label.enabled)
            {
                EndCheer();
                label.transform.localScale = Vector3.one;
            }
        }

        // Billboard (el jugador gira y el texto no tiene que girar con él) y un latido suave mientras está a la vista.
        private void LateUpdate()
        {
            if (_inventory == null || !label.enabled) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) label.transform.rotation = _camera.transform.rotation;

            if (_cheering)
            {
                float k = (Time.unscaledTime - _cheerStartedAt) / cheerSeconds;
                if (k < 1f)
                {
                    // Entra de golpe a 'cheerScale' y se asienta rápido en un tamaño algo mayor que el del MAX.
                    float settle = Mathf.Clamp01(k / 0.25f);
                    label.transform.localScale = Vector3.one * Mathf.Lerp(cheerScale, 1.25f, settle * (2f - settle));
                    return;
                }

                EndCheer();
            }

            label.transform.localScale = Vector3.one * (1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed));
        }
    }
}
