using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // U-10: además de apuntar, la guía DICE qué hacer: un cartel corto ("Llevá la cosecha al puesto") que va junto a la flecha del
    // borde o, con el lugar a la vista, encima de él.
    //
    // La flecha de la guía de los primeros minutos en el borde de la pantalla: cuando el lugar al que hay que ir (el campo, el pad de
    // venta, la primera mejora) queda FUERA de la vista, una flecha pegada al borde apunta hacia él. Cuando está a la vista se apaga
    // (la flecha en el mundo, de Environment, ya lo marca). Lee el lugar del TutorialManager (ITutorialTarget, de Core): no conoce al
    // entorno. Todo es cosmético y usa el reloj sin escala.
    //   - Va dentro del HUD (el área segura) y se posiciona con coordenadas de pantalla convertidas al rect del HUD, así que sirve con
    //     cualquier tamaño de pantalla. Sin asignaciones por frame (solo estructuras).
    public sealed class TutorialEdgeArrow : MonoBehaviour
    {
        [SerializeField, Tooltip("El rect del HUD dentro del cual se mueve la flecha (el área segura).")]
        private RectTransform area;

        [SerializeField, Tooltip("La flecha en sí: apunta hacia arriba (+Y) sin rotar.")]
        private RectTransform arrow;

        [SerializeField, Min(0f), Tooltip("Margen al borde, en unidades del Canvas.")]
        private float edgeMargin = 90f;

        [SerializeField, Min(0f)] private float bobAmount = 14f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 5f;

        [SerializeField, Min(0f), Tooltip("Segundos que espera la flecha de 'conviene ir' desde que hay un lugar (0 = al instante).")]
        private float guideDelaySeconds = 3f;
        [SerializeField, Min(0.05f)] private float guideRefreshSeconds = 0.5f;

        [Header("Cartel (qué hacer)")]
        [SerializeField, Tooltip("El cartel que dice qué hacer: va junto a la flecha o, con el lugar a la vista, encima de él. Puede faltar.")]
        private RectTransform labelRoot;

        [SerializeField] private TMP_Text label;

        [SerializeField, Min(0f), Tooltip("Distancia de la flecha al cartel, hacia el centro de la pantalla (unidades del Canvas).")]
        private float labelGap = 170f;

        [SerializeField, Min(0f), Tooltip("Altura sobre el lugar (en el mundo) a la que se pone el cartel cuando está a la vista.")]
        private float labelWorldHeight = 4.2f;

        [SerializeField, Min(0f)] private float labelMargin = 30f;

        private TutorialManager _tutorial;
        private Camera _camera;
        private IGuidePoint _guide;
        private float _guideSince;
        private float _guideRefreshAt;
        private string _hintKey;       // la clave del texto que muestra el cartel ahora (null = ninguno escrito)

        // Para pruebas y depuración: qué dice el cartel y si se ve.
        public string CurrentHintKey => _hintKey;
        public bool IsLabelShowing => labelRoot != null && labelRoot.gameObject.activeSelf;
        public Vector2 LabelPosition => labelRoot != null ? labelRoot.anchoredPosition : Vector2.zero;

        // Qué hay que hacer en cada paso de la guía inicial y en cada lugar de "conviene ir". Claves de Localization.csv.
        public static string TutorialHintKey(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Sow: return "guide.sow";
                case TutorialStep.Harvest: return "guide.harvest";
                case TutorialStep.Sell: return "guide.sell";
                case TutorialStep.Buy: return "guide.buy";
                default: return null;
            }
        }

        public static string GuideHintKey(IGuidePoint point)
        {
            if (point == null) return null;
            if (point.GuidePriority >= GuidePoints.NewItemPriority) return "guide.new_item";
            return point.GuidePriority >= GuidePoints.SellPriority ? "guide.sell_full" : "guide.can_buy";
        }

        // Para pruebas y depuración: el lugar de "conviene ir" elegido (null con la guía inicial activa o si no hay ninguno).
        public IGuidePoint CurrentGuide => _guide;

        // Para pruebas y depuración.
        public bool IsShowing => arrow != null && arrow.gameObject.activeSelf;
        public Vector2 ArrowPosition => arrow != null ? arrow.anchoredPosition : Vector2.zero;
        public Rect Area => area != null ? area.rect : default;

        private void OnEnable()
        {
            _tutorial = TutorialManager.Instance;
            Loc.Changed += OnLanguageChanged;
            SetShown(false);
            SetLabelShown(false);
        }

        private void OnDisable()
        {
            Loc.Changed -= OnLanguageChanged;
            _tutorial = null;
            _guide = null;
            _guideRefreshAt = 0f;
            SetShown(false);
            SetLabelShown(false);
        }

        // Con otro idioma el cartel se vuelve a escribir en el próximo cuadro.
        private void OnLanguageChanged()
        {
            _hintKey = null;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                SetShown(false);
                SetLabelShown(false);
                return;
            }

            // Primero la guía de los primeros minutos; terminada, el lugar al que conviene ir ahora (vender, una compra que alcanza).
            ITutorialTarget target = _tutorial != null ? _tutorial.CurrentTarget : null;
            Vector3 worldTarget;
            string hintKey;
            if (target != null)
            {
                worldTarget = target.Position;
                _guide = null;
                hintKey = TutorialHintKey(_tutorial.Step);
            }
            else if (!TryGetGuide(out worldTarget))
            {
                SetShown(false);
                SetLabelShown(false);
                return;
            }
            else
            {
                hintKey = GuideHintKey(_guide);
            }

            Vector3 screen = _camera.WorldToScreenPoint(worldTarget);
            float width = Screen.width;
            float height = Screen.height;
            bool behind = screen.z < 0f;
            bool inView = !behind && screen.x >= 0f && screen.x <= width && screen.y >= 0f && screen.y <= height;
            if (inView)
            {
                // A la vista no hace falta la flecha del borde, pero el cartel sigue diciendo qué hacer, encima del lugar.
                SetShown(false);
                Vector3 above = _camera.WorldToScreenPoint(worldTarget + new Vector3(0f, labelWorldHeight, 0f));
                if (area != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, new Vector2(above.x, above.y), null, out Vector2 local)) PlaceLabel(hintKey, local);
                else SetLabelShown(false);
                return;
            }

            // Detrás de la cámara la proyección se da vuelta: se invierte para que apunte hacia el lado correcto.
            Vector2 fromCenter = new Vector2(screen.x - width * 0.5f, screen.y - height * 0.5f);
            if (behind) fromCenter = -fromCenter;
            if (fromCenter.sqrMagnitude < 0.0001f) fromCenter = Vector2.up;

            // Del punto de pantalla al rect del HUD (en unidades del Canvas), pegado al borde con un margen.
            Rect rect = area.rect;
            float halfW = rect.width * 0.5f - edgeMargin;
            float halfH = rect.height * 0.5f - edgeMargin;
            Vector2 dir = fromCenter.normalized;
            float scale = Mathf.Min(dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue, dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            Vector2 position = rect.center + dir * scale;

            // El cartel, quieto, del lado de adentro de la flecha.
            PlaceLabel(hintKey, position - dir * labelGap);

            // Un vaivén hacia afuera para llamar la atención.
            position += dir * (bobAmount * Mathf.Sin(Time.unscaledTime * bobSpeed));

            arrow.anchoredPosition = position;
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            SetShown(true);
        }

        // El lugar de GuidePoints (Core) al que conviene ir. Se elige cada 'guideRefreshSeconds' (no en cada cuadro) y la flecha espera
        // 'guideDelaySeconds' desde que hay uno, para no saltar apenas alcanza la plata. Solo jugando y con la guía inicial terminada.
        private bool TryGetGuide(out Vector3 position)
        {
            position = default;
            if (_tutorial != null && _tutorial.IsActive)
            {
                _guide = null;
                return false;
            }

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return false;   // en pausa o con un anuncio no aparece ni se recalcula

            float now = Time.unscaledTime;
            if (now >= _guideRefreshAt)
            {
                _guideRefreshAt = now + guideRefreshSeconds;

                // Desde dónde se mide "el más cercano": el punto del suelo al que mira la cámara (más o menos, el jugador).
                Transform cam = _camera.transform;
                Vector3 forward = cam.forward;
                Vector3 focus = cam.position;
                if (forward.y < -0.01f) focus += forward * (cam.position.y / -forward.y);

                IGuidePoint best = GuidePoints.Best(focus);
                if (best != null && _guide == null) _guideSince = now;
                _guide = best;
            }

            if (_guide == null || now - _guideSince < guideDelaySeconds) return false;

            position = _guide.Position;
            return true;
        }

        private void SetShown(bool shown)
        {
            if (arrow != null && arrow.gameObject.activeSelf != shown) arrow.gameObject.SetActive(shown);
        }

        // Escribe el cartel (solo si cambió el texto: sin cadenas nuevas por cuadro) y lo pone en 'position', sin salirse del área.
        private void PlaceLabel(string hintKey, Vector2 position)
        {
            // Solo jugando: sobre la pantalla de "Tocá para jugar", en pausa o con un anuncio, el cartel molesta.
            GameManager game = GameManager.Instance;
            if (labelRoot == null || label == null || area == null || hintKey == null || (game != null && !game.IsPlaying))
            {
                SetLabelShown(false);
                return;
            }

            if (hintKey != _hintKey)
            {
                _hintKey = hintKey;
                label.text = Loc.Get(hintKey);
            }

            Rect rect = area.rect;
            Vector2 half = labelRoot.rect.size * 0.5f;
            float minX = rect.xMin + half.x + labelMargin, maxX = rect.xMax - half.x - labelMargin;
            float minY = rect.yMin + half.y + labelMargin, maxY = rect.yMax - half.y - labelMargin;
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : rect.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : rect.center.y;

            labelRoot.anchoredPosition = position;
            SetLabelShown(true);
        }

        private void SetLabelShown(bool shown)
        {
            if (labelRoot != null && labelRoot.gameObject.activeSelf != shown) labelRoot.gameObject.SetActive(shown);
        }
    }
}
