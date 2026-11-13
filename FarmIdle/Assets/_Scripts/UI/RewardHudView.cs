using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Los buffs activos y el stock de fertilizante en el HUD: una fila por buff (nombre, segundos que le quedan y una barra que se
    // vacía) y una fila con el fertilizante si hay stock. Solo lee: BuffManager y FertilizerManager tienen los datos; la UI no conoce
    // al jugador ni al entorno. Las filas están armadas en la escena (no se crea nada jugando) y las que sobran quedan apagadas.
    //   - Los segundos son tiempo de juego, así que se congelan en pausa y con un anuncio en pantalla, igual que el buff.
    //   - Se revisa unas 10 veces por segundo con tiempo sin escala y solo se reescribe el texto cuando cambia el segundo mostrado.
    public sealed class RewardHudView : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public TMP_Text text;
            [Tooltip("Barra que se vacía (escala en X, de 0 a 1). Puede faltar.")]
            public RectTransform fill;
        }

        [SerializeField, Tooltip("Filas para buffs (en orden). Hay tantas como buffs a la vez.")]
        private Row[] buffRows = new Row[0];

        [SerializeField, Tooltip("La fila del fertilizante: se muestra solo con stock.")]
        private Row fertilizerRow;

        private BuffManager _buffs;
        private FertilizerManager _fertilizer;
        private readonly StringBuilder _text = new StringBuilder(40);
        private int[] _shownSeconds;
        private int _shownStock = -1;
        private float _nextCheck;

        private void OnEnable()
        {
            _shownSeconds = new int[buffRows.Length];
            for (int i = 0; i < _shownSeconds.Length; i++) _shownSeconds[i] = -1;
            _shownStock = -1;
            _nextCheck = 0f;

            _buffs = BuffManager.Instance;
            _fertilizer = FertilizerManager.Instance;
            if (_buffs != null) _buffs.Changed += OnChanged;
            if (_fertilizer != null) _fertilizer.Changed += OnChanged;
            Loc.Changed += OnChanged;

            Refresh(force: true);
        }

        private void OnDisable()
        {
            Loc.Changed -= OnChanged;
            if (_buffs != null)
            {
                _buffs.Changed -= OnChanged;
                _buffs = null;
            }

            if (_fertilizer != null)
            {
                _fertilizer.Changed -= OnChanged;
                _fertilizer = null;
            }
        }

        private void OnChanged()
        {
            Refresh(force: true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;

            _nextCheck = Time.unscaledTime + 0.1f;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            int active = _buffs != null ? _buffs.Active.Count : 0;

            for (int i = 0; i < buffRows.Length; i++)
            {
                Row row = buffRows[i];
                if (row == null || row.root == null) continue;

                bool show = i < active;
                if (row.root.activeSelf != show) row.root.SetActive(show);
                if (!show)
                {
                    _shownSeconds[i] = -1;
                    continue;
                }

                ActiveBuff buff = _buffs.Active[i];
                int seconds = Mathf.CeilToInt(buff.Remaining);
                if (force || seconds != _shownSeconds[i])
                {
                    _shownSeconds[i] = seconds;
                    RewardData reward = _buffs.RewardOf(buff);
                    _text.Clear();
                    _text.Append(reward != null ? reward.DisplayName : buff.Id).Append("  ").Append(seconds).Append(" s");
                    if (row.text != null) row.text.text = _text.ToString();
                }

                if (row.fill != null)
                {
                    float fraction = buff.Duration > 0f ? Mathf.Clamp01(buff.Remaining / buff.Duration) : 0f;
                    row.fill.localScale = new Vector3(Mathf.Max(0.001f, fraction), 1f, 1f);
                }
            }

            RefreshFertilizer(force);
        }

        private void RefreshFertilizer(bool force)
        {
            if (fertilizerRow == null || fertilizerRow.root == null) return;

            int stock = _fertilizer != null ? _fertilizer.Stock : 0;
            bool show = stock > 0;
            if (fertilizerRow.root.activeSelf != show) fertilizerRow.root.SetActive(show);
            if (!show || (!force && stock == _shownStock))
            {
                if (!show) _shownStock = -1;
                return;
            }

            _shownStock = stock;
            _text.Clear();
            _text.Append(Loc.Get("hud.fertilizer")).Append(" x").Append(stock);
            if (fertilizerRow.text != null) fertilizerRow.text.text = _text.ToString();
        }
    }
}
