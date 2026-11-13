using UnityEngine;

namespace FarmIdle
{
    // Base de los datos de diseño (cultivos, zonas, mejoras, recompensas).
    // Son ScriptableObjects de solo lectura: en runtime nadie los modifica. El progreso del jugador vive aparte
    // (SaveData) y los referencia por Id, así que el Id es un contrato: una vez usado, no se cambia.
    // Renombrar o mover el asset sí es seguro; lo que se guarda es el Id, no el nombre del archivo.
    public abstract class GameDataAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Identificador estable: minúsculas, números, _ y -. Se guarda en la partida, no lo cambies.")]
        private string id;

        public string Id => id;

        private string _nameKey;

        // El nombre que ve el jugador: sale de la tabla de textos con la clave "data.<id>" (ROADMAP U-03). Si la tabla no la tiene,
        // queda el nombre guardado en el asset. La clave se arma una sola vez: pedir el nombre no asigna memoria.
        protected string LocalizedName(string fallback)
        {
            if (_nameKey == null) _nameKey = "data." + id;
            return Loc.GetOr(_nameKey, fallback);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Un asset nuevo arranca con el id sacado de su nombre; a partir de ahí queda fijo.
            if (string.IsNullOrWhiteSpace(id))
            {
                id = ToSlug(name);
            }
        }

        private static string ToSlug(string value)
        {
            var chars = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!valid) chars[i] = '_';
            }
            return new string(chars);
        }
#endif
    }
}
