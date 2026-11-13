using UnityEngine;

namespace FarmIdle
{
    // El color del jugador (F-17, tanda 3): pinta el cuerpo y la nariz con la skin que lleva puesta SkinManager. Usa un
    // MaterialPropertyBlock (el mismo material de siempre, sin materiales nuevos ni copias) y sin skin lo quita. Solo reacciona a
    // Changed: nada por cuadro.
    public sealed class PlayerSkinView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly string[] AccentNames = { "PieIzq", "PieDer", "ManoIzq", "ManoDer" };

        private Renderer _body;
        private Renderer _nose;
        private MaterialPropertyBlock _block;
        private SkinManager _skins;

        private void Awake()
        {
            Transform visual = transform.Find("Visual");
            if (visual == null) return;

            _body = visual.GetComponent<Renderer>();
            Transform nose = visual.Find("Nariz");
            if (nose != null) _nose = nose.GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _skins = SkinManager.Instance;
            if (_skins == null) return;

            _skins.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_skins != null)
            {
                _skins.Changed -= Apply;
                _skins = null;
            }
        }

        private void Apply()
        {
            if (_body == null || _skins == null) return;

            SkinData skin = _skins.EquippedSkin;
            Paint(_body, skin != null, skin != null ? skin.BodyColor : Color.white);
            if (_nose != null) Paint(_nose, skin != null, skin != null ? skin.NoseColor : Color.white);

            // Manos y pies (si el modelo los tiene) van con el color de la nariz.
            for (int i = 0; i < AccentNames.Length; i++)
            {
                Transform accent = transform.Find(AccentNames[i]);
                Renderer renderer = accent != null ? accent.GetComponent<Renderer>() : null;
                if (renderer != null) Paint(renderer, skin != null, skin != null ? skin.NoseColor : Color.white);
            }
        }

        private void Paint(Renderer target, bool custom, Color color)
        {
            if (!custom)
            {
                target.SetPropertyBlock(null);
                return;
            }

            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
