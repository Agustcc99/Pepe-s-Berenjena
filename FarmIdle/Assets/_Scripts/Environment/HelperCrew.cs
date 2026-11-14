using UnityEngine;

namespace FarmIdle
{
    // Los ayudantes que se ven (F-19), ya armados en la escena y apagados: prende uno por cada nivel comprado y apaga el resto.
    // Escucha a HelperManager.Changed, que avisa al comprar, al cargar la partida y al empezar una temporada nueva, así que los
    // tres casos usan el mismo camino. Sin Instantiate: solo SetActive sobre objetos que ya existen (como DecorationSet).
    public sealed class HelperCrew : MonoBehaviour
    {
        [SerializeField, Tooltip("Los peones, en el orden de los niveles de GameConfig.Helpers.")]
        private GameObject[] walkers = new GameObject[0];

        private HelperManager _helpers;

        // Para pruebas y depuración.
        public int Count => walkers.Length;

        public GameObject WalkerAt(int index)
        {
            return walkers[index];
        }

        private void OnEnable()
        {
            _helpers = HelperManager.Instance;
            if (_helpers == null) return;

            _helpers.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_helpers != null)
            {
                _helpers.Changed -= Refresh;
                _helpers = null;
            }
        }

        private void Refresh()
        {
            int level = _helpers.Level;
            for (int i = 0; i < walkers.Length; i++)
            {
                if (walkers[i] == null) continue;

                bool show = i < level;
                if (walkers[i].activeSelf != show) walkers[i].SetActive(show);
            }
        }
    }
}
