using UnityEngine;

namespace FarmIdle
{
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

        private TutorialManager _tutorial;
        private Camera _camera;
        private IGuidePoint _guide;
        private float _guideSince;
        private float _guideRefreshAt;

        // Para pruebas y depuración: el lugar de "conviene ir" elegido (null con la guía inicial activa o si no hay ninguno).
        public IGuidePoint CurrentGuide => _guide;

        // Para pruebas y depuración.
        public bool IsShowing => arrow != null && arrow.gameObject.activeSelf;
        public Vector2 ArrowPosition => arrow != null ? arrow.anchoredPosition : Vector2.zero;
        public Rect Area => area != null ? area.rect : default;

        private void OnEnable()
        {
            _tutorial = TutorialManager.Instance;
            SetShown(false);
        }

        private void OnDisable()
        {
            _tutorial = null;
            _guide = null;
            _guideRefreshAt = 0f;
            SetShown(false);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                SetShown(false);
                return;
            }

            // Primero la guía de los primeros minutos; terminada, el lugar al que conviene ir ahora (vender, una compra que alcanza).
            ITutorialTarget target = _tutorial != null ? _tutorial.CurrentTarget : null;
            Vector3 worldTarget;
            if (target != null)
            {
                worldTarget = target.Position;
                _guide = null;
            }
            else if (!TryGetGuide(out worldTarget))
            {
                SetShown(false);
                return;
            }

            Vector3 screen = _camera.WorldToScreenPoint(worldTarget);
            float width = Screen.width;
            float height = Screen.height;
            bool behind = screen.z < 0f;
            bool inView = !behind && screen.x >= 0f && screen.x <= width && screen.y >= 0f && screen.y <= height;
            if (inView)
            {
                SetShown(false);
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
    }
}
