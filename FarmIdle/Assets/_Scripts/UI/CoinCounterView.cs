using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Contador de monedas del HUD. Solo escucha: la billetera avisa con BalanceChanged y esta vista anima lo que muestra.
    // La billetera ya tiene el saldo final desde el primer instante; el conteo y el "latido" son cosméticos y usan
    // tiempo sin escala para seguir funcionando aunque el juego esté en pausa.
    public sealed class CoinCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private RectTransform pulseTarget;   // lo que "late" cuando se ganan monedas

        [SerializeField, Min(0f)] private float countSeconds = 0.35f;
        [SerializeField, Min(1f)] private float pulseScale = 1.2f;
        [SerializeField, Min(0.01f)] private float pulseSeconds = 0.2f;

        private IReadOnlyWallet _wallet;
        private long _shown = -1;   // valor que muestra el texto ahora (-1: todavía no se escribió nada)
        private Coroutine _countRoutine;
        private Coroutine _pulseRoutine;

        private void OnEnable()
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy == null)
            {
                Debug.LogError("[FarmIdle] CoinCounterView: no hay EconomyManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            _wallet = economy.Wallet;
            _wallet.BalanceChanged += OnBalanceChanged;
            ShowAmount(_wallet.Balance);
        }

        private void OnDisable()
        {
            if (_wallet != null)
            {
                _wallet.BalanceChanged -= OnBalanceChanged;
                _wallet = null;
            }

            // Al desactivarse Unity detiene las corrutinas; hay que dejar todo en su estado de reposo.
            _countRoutine = null;
            _pulseRoutine = null;
            if (pulseTarget != null) pulseTarget.localScale = Vector3.one;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            if (_countRoutine != null) StopCoroutine(_countRoutine);
            _countRoutine = StartCoroutine(CountTo(balance));

            if (delta > 0 && pulseTarget != null)
            {
                if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
                _pulseRoutine = StartCoroutine(Pulse());
            }
        }

        private IEnumerator CountTo(long target)
        {
            long from = _shown < 0 ? 0 : _shown;
            float elapsed = 0f;

            while (elapsed < countSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / countSeconds);
                ShowAmount(from + (long)((target - from) * (double)t));
                yield return null;
            }

            ShowAmount(target);
            _countRoutine = null;
        }

        private IEnumerator Pulse()
        {
            float elapsed = 0f;

            while (elapsed < pulseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / pulseSeconds);
                float scale = 1f + (pulseScale - 1f) * Mathf.Sin(t * Mathf.PI);   // sube y vuelve a 1
                pulseTarget.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            pulseTarget.localScale = Vector3.one;
            _pulseRoutine = null;
        }

        private void ShowAmount(long value)
        {
            if (value == _shown) return;

            _shown = value;
            amountLabel.text = value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
