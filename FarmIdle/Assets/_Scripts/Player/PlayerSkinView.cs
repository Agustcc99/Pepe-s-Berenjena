using UnityEngine;

namespace FarmIdle
{
    // El aspecto del jugador (F-17, tanda 3 + IN-04): pinta el cuerpo y la nariz con la skin que lleva puesta SkinManager, cambia el
    // sombrero (gorra, gorro, brote, galera, flor, corona) y prende un diseño en el cuerpo (franja, bufanda, jardinero, lunares).
    // Los sombreros y diseños son grupos que el builder deja armados y apagados bajo el cuerpo ("Hat_<Nombre>", "Pat_<Nombre>"):
    // acá solo se prenden y se pintan, sin Instantiate. Usa un MaterialPropertyBlock (el mismo material de siempre, sin materiales
    // nuevos ni copias) y sin skin lo quita y vuelve al sombrero de paja. Solo reacciona a Changed: nada por cuadro.
    public sealed class PlayerSkinView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly string[] AccentNames = { "PieIzq", "PieDer", "ManoIzq", "ManoDer" };
        private const string AccentSuffix = "Acc";   // las piezas que terminan así llevan el color de detalle de la skin

        private Renderer _body;
        private Renderer _nose;
        private GameObject _strawBrim;
        private GameObject _strawCrown;
        private GameObject[] _hats;          // por SkinHat; Straw no tiene grupo propio
        private GameObject[] _patterns;      // por SkinPattern; None no tiene grupo
        private Renderer[][] _hatRenderers;
        private Renderer[][] _patternRenderers;
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

            Transform brim = visual.Find("SombreroAla");
            Transform crown = visual.Find("SombreroCopa");
            if (brim != null) _strawBrim = brim.gameObject;
            if (crown != null) _strawCrown = crown.gameObject;

            int hatCount = System.Enum.GetValues(typeof(SkinHat)).Length;
            _hats = new GameObject[hatCount];
            _hatRenderers = new Renderer[hatCount][];
            for (int i = 0; i < hatCount; i++) Find(visual, "Hat_" + (SkinHat)i, out _hats[i], out _hatRenderers[i]);

            int patternCount = System.Enum.GetValues(typeof(SkinPattern)).Length;
            _patterns = new GameObject[patternCount];
            _patternRenderers = new Renderer[patternCount][];
            for (int i = 0; i < patternCount; i++) Find(visual, "Pat_" + (SkinPattern)i, out _patterns[i], out _patternRenderers[i]);
        }

        private static void Find(Transform visual, string name, out GameObject group, out Renderer[] renderers)
        {
            Transform found = visual.Find(name);
            group = found != null ? found.gameObject : null;
            renderers = found != null ? found.GetComponentsInChildren<Renderer>(true) : null;
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

            ApplyLook(skin);
        }

        // Sombrero y diseño: prende el grupo de la skin, apaga los demás y pinta el prendido. Sin skin: solo el sombrero de paja.
        private void ApplyLook(SkinData skin)
        {
            SkinHat hat = skin != null ? skin.Hat : SkinHat.Straw;
            SkinPattern pattern = skin != null ? skin.Pattern : SkinPattern.None;
            Color hatColor = skin != null ? skin.HatColor : Color.white;
            Color accentColor = skin != null ? skin.AccentColor : Color.white;

            bool straw = hat == SkinHat.Straw || hat == SkinHat.Flower;
            if (_strawBrim != null) _strawBrim.SetActive(straw);
            if (_strawCrown != null) _strawCrown.SetActive(straw);

            for (int i = 0; i < _hats.Length; i++) Show(_hats[i], _hatRenderers[i], i == (int)hat, hatColor, accentColor);
            for (int i = 0; i < _patterns.Length; i++) Show(_patterns[i], _patternRenderers[i], i == (int)pattern, accentColor, accentColor);
        }

        private void Show(GameObject group, Renderer[] renderers, bool visible, Color main, Color accent)
        {
            if (group == null) return;

            group.SetActive(visible);
            if (!visible || renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                Paint(renderer, true, renderer.name.EndsWith(AccentSuffix, System.StringComparison.Ordinal) ? accent : main);
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
