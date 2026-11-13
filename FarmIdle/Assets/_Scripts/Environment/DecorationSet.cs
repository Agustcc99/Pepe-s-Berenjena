using UnityEngine;

namespace FarmIdle
{
    // Las decoraciones de la granja (F-17) ya armadas en la escena y apagadas: prende las primeras 'Owned' (en el orden del
    // GameConfig) y apaga el resto. Escucha a DecorationManager, así que cargar la partida y comprar usan el mismo camino.
    // Sin Instantiate: solo SetActive sobre objetos que ya existen.
    public sealed class DecorationSet : MonoBehaviour
    {
        [SerializeField, Tooltip("Los objetos en el mismo orden que GameConfig.Decorations.")]
        private GameObject[] items = new GameObject[0];

        private DecorationManager _decorations;

        private void OnEnable()
        {
            _decorations = DecorationManager.Instance;
            if (_decorations == null) return;

            _decorations.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_decorations != null)
            {
                _decorations.Changed -= Refresh;
                _decorations = null;
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) items[i].SetActive(_decorations.IsOwned(i));
            }
        }
    }
}
