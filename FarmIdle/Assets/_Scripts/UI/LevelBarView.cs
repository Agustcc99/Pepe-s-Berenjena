using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Barra de experiencia y nivel del HUD. Solo escucha: ProgressionManager ya tiene la XP, el nivel y los puntos finales y esta
    // vista muestra lo que dice. La UI no conoce al jugador ni al entorno, solo a Managers.
    //   - Muestra "Nv N", la barra del nivel actual y, cuando hay puntos de perk sin gastar, un aviso con cuántos son y dónde
    //     gastarlos (los pads de la plaza).
    //   - Al subir de nivel (LevelChanged, jugando; no al cargar una partida) la barra late y aparece un cartel "¡Nivel N!" unos segundos.
    //   Las animaciones usan tiempo sin escala y se detienen solas al desactivarse; nada de esto cambia ningún valor del juego.
    public sealed class LevelBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField, Tooltip("Barra que se llena (escala en X, de 0 a 1) con la XP del nivel actual.")]
        private RectTransform fill;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField, Tooltip("Aviso de puntos sin gastar. Se muestra solo si hay puntos.")]
        private TMP_Text pointsLabel;
        [SerializeField, Tooltip("Cartel temporal \"¡Nivel N!\" al subir de nivel.")]
        private TMP_Text levelUpLabel;
        [SerializeField, Tooltip("Lo que \"late\" al subir de nivel.")]
        private RectTransform pulseTarget;

        [SerializeField, Min(0.1f)] private float levelUpSeconds = 2f;
        [SerializeField, Min(1f)] private float pulseScale = 1.15f;

        private ProgressionManager _progression;
        private readonly StringBuilder _text = new StringBuilder(48);
        private Coroutine _levelUpRoutine;

        private void OnEnable()
        {
            _progression = ProgressionManager.Instance;
            if (_progression == null)
            {
                Debug.LogError("[FarmIdle] LevelBarView: no hay ProgressionManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            _progression.Changed += OnProgressChanged;
            _progression.LevelChanged += OnLevelChanged;
            Loc.Changed += Refresh;
            if (levelUpLabel != null) levelUpLabel.gameObject.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                Loc.Changed -= Refresh;
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
                _progression = null;
            }

            // Al desactivarse Unity detiene las corrutinas; hay que dejar todo en su estado de reposo.
            _levelUpRoutine = null;
            if (pulseTarget != null) pulseTarget.localScale = Vector3.one;
            if (levelUpLabel != null) levelUpLabel.gameObject.SetActive(false);
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.PaymentChanged || change.Kind == ProgressChangeKind.ZoneUnlocked || change.Kind == ProgressChangeKind.UpgradeLevelChanged) return;

            Refresh();
        }

        private void OnLevelChanged(int level)
        {
            Refresh();

            if (_levelUpRoutine != null) StopCoroutine(_levelUpRoutine);
            _levelUpRoutine = StartCoroutine(LevelUp(level));
        }

        private void Refresh()
        {
            if (_progression == null) return;

            int level = _progression.Level;
            _progression.GetLevelProgress(out long into, out long needed);

            if (levelLabel != null)
            {
                _text.Clear();
                _text.Append(Loc.Get("hud.level_short")).Append(' ').Append(level);
                if (_progression.IsMaxLevel) _text.Append(' ').Append(Loc.Get("pad.max"));
                levelLabel.text = _text.ToString();
            }

            float fraction = needed > 0L ? Mathf.Clamp01(into / (float)needed) : 1f;
            if (fill != null) fill.localScale = new Vector3(Mathf.Max(0.001f, fraction), 1f, 1f);

            if (xpLabel != null)
            {
                _text.Clear();
                if (needed > 0L) _text.Append(into).Append(" / ").Append(needed).Append(' ').Append(Loc.Get("hud.xp"));
                xpLabel.text = _text.ToString();   // cambia con cada venta; son unos pocos caracteres
            }

            if (pointsLabel != null)
            {
                int points = _progression.PerkPoints;
                pointsLabel.gameObject.SetActive(points > 0);
                if (points > 0)
                {
                    pointsLabel.text = Loc.Format(points == 1 ? "hud.points_one" : "hud.points_many", points);
                }
            }
        }

        private IEnumerator LevelUp(int level)
        {
            if (levelUpLabel != null)
            {
                levelUpLabel.text = Loc.Format("hud.level_up", level);
                levelUpLabel.gameObject.SetActive(true);
            }

            float elapsed = 0f;
            while (elapsed < levelUpSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                if (pulseTarget != null)
                {
                    float k = Mathf.Clamp01(elapsed / 0.35f);
                    float scale = 1f + (pulseScale - 1f) * Mathf.Sin(k * Mathf.PI);   // sube y vuelve a 1 en el primer tercio de segundo
                    pulseTarget.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (pulseTarget != null) pulseTarget.localScale = Vector3.one;
            if (levelUpLabel != null) levelUpLabel.gameObject.SetActive(false);
            _levelUpRoutine = null;
        }
    }
}
