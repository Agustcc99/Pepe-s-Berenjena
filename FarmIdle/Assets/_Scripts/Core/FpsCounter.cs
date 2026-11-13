// SOLO existe en el Editor y en los builds de desarrollo (define DEVELOPMENT_BUILD): un build final no compila esta clase.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Contador de FPS para pruebas. Se crea solo y únicamente existe en el Editor y en los builds de desarrollo
    // ("Development Build" marcado); en un build final ni siquiera se compila.
    // Además muestra la posición del jugador (objeto con tag "Player") para comprobar que se mueve.
    public class FpsCounter : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;

        private float _elapsed;
        private int _frames;
        private string _label = "FPS: --";
        private GUIStyle _style;
        private Transform _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Debug.isDebugBuild es true en el Editor y en los Development Build.
            if (!Debug.isDebugBuild) return;

            var go = new GameObject("FpsCounter");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsCounter>();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;

            if (_elapsed >= RefreshSeconds)
            {
                _label = "FPS: " + Mathf.RoundToInt(_frames / _elapsed) + PlayerPositionText();
                _elapsed = 0f;
                _frames = 0;
            }
        }

        private string PlayerPositionText()
        {
            // El jugador se busca por tag y se guarda; mientras no exista (escena Bootstrap) se reintenta cada medio segundo.
            if (_player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found == null) return "";
                _player = found.transform;
            }

            Vector3 p = _player.position;
            return "\nPos: " + p.x.ToString("F1", CultureInfo.InvariantCulture) + ", " + p.z.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.03f),
                    fontStyle = FontStyle.Bold
                };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(20f, Screen.height * 0.17f, 700f, 200f), _label, _style);   // debajo de la barra de nivel del HUD
        }
    }
}
#endif
