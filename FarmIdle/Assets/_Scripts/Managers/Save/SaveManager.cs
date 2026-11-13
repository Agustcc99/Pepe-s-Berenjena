using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de la partida guardada. Los sistemas que se guardan (ISaveable) se registran acá, SaveManager lee el archivo
    // ANTES de que arranque el nivel y se lo reparte, y después escribe cuando algo cambia.
    //   - Cargar: Bootstrapper registra los sistemas y llama a Load() antes de cargar MainLevel, así cuando el nivel
    //     arranca la billetera, la mochila y el progreso ya tienen su estado. El orden de restauración es el del registro
    //     (la progresión antes que la mochila: es la que le agranda la capacidad antes de que se le devuelva el contenido).
    //     Un archivo dañado, vacío o de una versión desconocida NO rompe nada: se avisa en consola y se arranca de cero.
    //   - Guardar: un cambio importante (compra, venta...) marca la partida como "sucia" y recién unos segundos después se
    //     escribe (los cambios seguidos se juntan en un solo guardado: nada de escribir en cada frame). Además se guarda al
    //     pausar, al perder el foco y al cerrar; en la web, también cuando la pestaña se oculta o se cierra.
    //   - Es solo una puerta: no sabe qué hay adentro de cada sección ni toca el juego. Restaurar no dispara efectos de
    //     juego (cada sistema se restaura en silencio) y los avisos de cambio que llegan mientras se restaura se ignoran.
    //   - No guarda hasta haber cargado (Load): un arranque que falló a medias no pisa una partida buena con una vacía.
    //   - Usa el reloj sin escala: en pausa y con un anuncio en pantalla el guardado diferido sigue funcionando.
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        // Para pruebas: si se asigna antes de arrancar el Bootstrapper, el juego usa ESTE almacenamiento en vez de la
        // carpeta de datos del dispositivo (así las pruebas no leen ni pisan la partida real). Se consume al crear el servicio.
        public static ISaveStorage StorageForNextBoot { get; set; }

        private readonly List<ISaveable> _saveables = new List<ISaveable>();
        private readonly List<SaveSection> _unclaimed = new List<SaveSection>();   // secciones del archivo sin dueño: se reescriben tal cual

        private SaveFile _file;
        private float _saveDelay = 3f;
        private bool _loaded;
        private bool _restoring;
        private bool _dirty;
        private float _dueAt;

        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.NotLoaded;
        public string LoadMessage { get; private set; } = string.Empty;
        public int SavedVersion { get; private set; }                // versión del sobre del último archivo cargado o escrito
        public DateTime? LastSavedUtc { get; private set; }          // null hasta el primer guardado de esta sesión
        public int SaveCount { get; private set; }                   // guardados escritos en esta sesión
        public string LastError { get; private set; } = string.Empty;
        public bool IsDirty => _dirty;
        public bool IsLoaded => _loaded;
        public int SaveableCount => _saveables.Count;

        // Se dispara cuando cambia el estado del guardado (cargó, guardó, se borró...). Lo usa el panel de debug.
        public event Action StatusChanged;

        // Lo llama el Bootstrapper; hasta entonces Instance es null. Sin 'storage' usa la carpeta de datos del dispositivo.
        public void Initialize(GameConfig config, ISaveStorage storage = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            ISaveStorage chosen = storage ?? StorageForNextBoot ?? CreateDeviceStorage();
            StorageForNextBoot = null;

            _file = new SaveFile(chosen, SaveMigrator.Default);
#if UNITY_WEBGL && !UNITY_EDITOR
            _saveDelay = config.WebSaveDelaySeconds;   // en la web se guarda antes: cerrar la pestaña de golpe es lo más común
#else
            _saveDelay = config.SaveDelaySeconds;
#endif
            Instance = this;

            // La web avisa al ocultarse o cerrarse la pestaña (en los demás destinos esto no hace nada).
            WebPersistence.RegisterPageHide(gameObject.name, nameof(OnWebPageHidden));
        }

        // El almacén real del dispositivo: archivos en la carpeta de datos de la app. En la web, además, un espejo en localStorage (síncrono)
        // para no perder los últimos segundos si la pestaña se cierra de golpe (ver MirroredSaveStorage).
        private static ISaveStorage CreateDeviceStorage()
        {
            var files = new FileSaveStorage(Application.persistentDataPath);
#if UNITY_WEBGL && !UNITY_EDITOR
            return new MirroredSaveStorage(files, new WebLocalStore());
#else
            return files;
#endif
        }

        // Registra un sistema guardable. El orden de registro es el orden de restauración.
        public void Register(ISaveable saveable)
        {
            if (saveable == null) throw new ArgumentNullException(nameof(saveable));
            if (string.IsNullOrEmpty(saveable.SaveId)) throw new ArgumentException("El sistema necesita un SaveId.", nameof(saveable));

            foreach (ISaveable existing in _saveables)
            {
                if (existing.SaveId == saveable.SaveId)
                {
                    throw new InvalidOperationException("Ya hay un sistema guardable con el id '" + saveable.SaveId + "'.");
                }
            }

            _saveables.Add(saveable);
            saveable.SaveRequested += RequestSave;
        }

        // ---------- Cargar ----------

        // Lee el archivo y se lo reparte a los sistemas registrados. Devuelve true si se aplicó una partida guardada.
        // Nunca tira excepciones por un archivo malo.
        public bool Load()
        {
            SaveLoadResult result = _file.Load();
            LoadStatus = result.Status;
            LoadMessage = result.Message;

            if (result.NeedsWarning)
            {
                Debug.LogWarning("[FarmIdle] Partida: " + result.Message);
            }
            else if (!string.IsNullOrEmpty(result.Message))
            {
                Debug.Log("[FarmIdle] Partida: " + result.Message);
            }

            _unclaimed.Clear();
            bool applied = false;

            if (result.HasData)
            {
                SavedVersion = result.Data.version;
                ApplyToSaveables(result.Data);
                applied = true;
            }
            else
            {
                SavedVersion = 0;
            }

            _loaded = true;
            _dirty = false;
            StatusChanged?.Invoke();
            return applied;
        }

        private void ApplyToSaveables(SaveData data)
        {
            _restoring = true;
            try
            {
                foreach (ISaveable saveable in _saveables)
                {
                    SaveSection section = data.Find(saveable.SaveId);
                    if (section == null) continue;   // sistema nuevo que la partida vieja no conoce: queda en su estado inicial

                    try
                    {
                        saveable.Restore(section.json);
                    }
                    catch (Exception e)
                    {
                        // Una sección dañada no tumba las demás: ese sistema arranca de cero.
                        Debug.LogError("[FarmIdle] Partida: la sección '" + saveable.SaveId + "' no se pudo restaurar (" + e.Message + "); ese sistema arranca de cero.");
                        TryReset(saveable);
                    }
                }

                // Lo que el archivo trae y nadie reclama (un sistema que se sacó o que todavía no se registró) se conserva.
                if (data.sections != null)
                {
                    foreach (SaveSection section in data.sections)
                    {
                        if (!IsRegistered(section.id)) _unclaimed.Add(section);
                    }
                }
            }
            finally
            {
                _restoring = false;
            }
        }

        private bool IsRegistered(string id)
        {
            foreach (ISaveable saveable in _saveables)
            {
                if (saveable.SaveId == id) return true;
            }

            return false;
        }

        private static void TryReset(ISaveable saveable)
        {
            try
            {
                saveable.Restore(null);
            }
            catch (Exception e)
            {
                Debug.LogError("[FarmIdle] Partida: tampoco se pudo reiniciar '" + saveable.SaveId + "' (" + e.Message + ").");
            }
        }

        // ---------- Guardar ----------

        // Un sistema avisa que cambió algo importante. Se junta con los avisos siguientes: el guardado se hace una sola vez
        // 'saveDelaySeconds' después del primero (el contador no se reinicia con cada cambio, así una venta continua igual guarda).
        public void RequestSave()
        {
            if (_restoring || !_loaded) return;

            if (!_dirty)
            {
                _dirty = true;
                _dueAt = Time.unscaledTime + _saveDelay;
            }
        }

        // Escribe la partida ahora. Con 'force' false no hace nada si no cambió nada desde el último guardado.
        public bool SaveNow(bool force = false)
        {
            if (!_loaded || _file == null) return false;
            if (!force && !_dirty) return false;

            var data = new SaveData { savedAtUtc = DateTime.UtcNow.ToString("o") };

            foreach (ISaveable saveable in _saveables)
            {
                try
                {
                    data.Set(saveable.SaveId, saveable.Capture());
                }
                catch (Exception e)
                {
                    // Si un sistema no puede capturarse no se escribe NADA: mejor conservar la partida anterior entera que una a medias.
                    LastError = "Capture de '" + saveable.SaveId + "' falló: " + e.Message;
                    Debug.LogError("[FarmIdle] Partida: " + LastError + " No se guardó.");
                    StatusChanged?.Invoke();
                    return false;
                }
            }

            foreach (SaveSection section in _unclaimed)
            {
                if (data.Find(section.id) == null) data.Set(section.id, section.json);
            }

            string text = SaveSerializer.Serialize(data);
            if (!_file.Write(text))
            {
                LastError = "No se pudo escribir el archivo.";
                Debug.LogWarning("[FarmIdle] Partida: " + LastError + " Se reintenta en unos segundos.");
                _dirty = true;
                _dueAt = Time.unscaledTime + Mathf.Max(_saveDelay, 5f);
                StatusChanged?.Invoke();
                return false;
            }

            _dirty = false;
            LastError = string.Empty;
            SavedVersion = data.version;
            LastSavedUtc = DateTime.UtcNow;
            SaveCount++;
            StatusChanged?.Invoke();
            return true;
        }

        // Debug: borra la partida y deja todos los sistemas como en una partida nueva.
        public void DeleteSave()
        {
            if (_file == null) return;

            _restoring = true;
            try
            {
                foreach (ISaveable saveable in _saveables) TryReset(saveable);
            }
            finally
            {
                _restoring = false;
            }

            _unclaimed.Clear();
            _file.DeleteSave();
            _dirty = false;
            LastSavedUtc = null;
            SavedVersion = 0;
            LoadStatus = SaveLoadStatus.NewGame;
            LoadMessage = "Partida borrada.";
            StatusChanged?.Invoke();
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime >= _dueAt) SaveNow();
        }

        // Android e iOS: pasar a segundo plano es la última oportunidad confiable de guardar.
        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow(force: AnyVolatileState());
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SaveNow(force: AnyVolatileState());
        }

        private void OnApplicationQuit()
        {
            SaveNow(force: AnyVolatileState());
        }

        // La web avisa por SendMessage (ver WebPersistence): la pestaña se ocultó, se cerró o se recarga.
        public void OnWebPageHidden(string unused)
        {
            SaveNow(force: AnyVolatileState());
        }

        // true si algún sistema guarda algo que corre con el tiempo (los segundos de un buff): hay que escribir aunque no haya
        // habido ningún aviso de cambio.
        private bool AnyVolatileState()
        {
            foreach (ISaveable saveable in _saveables)
            {
                if (saveable is ISaveableVolatile volatileState && volatileState.HasVolatileState) return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            foreach (ISaveable saveable in _saveables) saveable.SaveRequested -= RequestSave;
            _saveables.Clear();

            if (Instance == this) Instance = null;
        }
    }
}
