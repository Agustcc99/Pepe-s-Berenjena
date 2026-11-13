using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmIdle.EditorTools
{
    // Saca de las escenas lo que es solo de desarrollo cuando el build NO es Development: el panel de debug (botones de monedas, de
    // pausa, de borrar la partida, del anuncio simulado...) y todo lo que cuelga de él. Así un build final no trae ni el objeto ni su
    // referencia al cubo de prueba del pool. (El contador de FPS no hace falta sacarlo: se crea solo y únicamente en desarrollo.)
    // La lógica está separada (StripDevelopmentObjects) para poder probarla sin construir nada.
    public sealed class ReleaseSceneStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Sin informe es entrar a Play en el Editor; un build de desarrollo conserva todo.
            if (report == null || (report.summary.options & BuildOptions.Development) != 0) return;

            int removed = StripDevelopmentObjects(scene);
            if (removed > 0) Debug.Log("[FarmIdle] Release: se sacó " + removed + " objeto(s) de desarrollo de la escena " + scene.name + ".");
        }

        // Destruye cada objeto que lleva un DebugPanel (con sus hijos). Devuelve cuántos.
        public static int StripDevelopmentObjects(Scene scene)
        {
            GameObject[] panels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DebugPanel>(true))
                .Select(panel => panel.gameObject)
                .Distinct()
                .ToArray();

            foreach (GameObject panel in panels) Object.DestroyImmediate(panel);
            return panels.Length;
        }
    }
}
