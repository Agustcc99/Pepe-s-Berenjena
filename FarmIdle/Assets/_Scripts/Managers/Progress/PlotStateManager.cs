using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de lo sembrado entre sesiones (B-11): al volver al juego las parcelas no aparecen vacías.
    //   - Qué guarda: por cada campo activo (IPlotStateHolder, registrado por PlotField), el estado de cada parcela y los segundos de
    //     crecimiento, más la hora (UTC) en que se guardó.
    //   - Al volver: lo sembrado sigue creciendo con el tiempo que se estuvo fuera (casi siempre vuelve listo para cosechar). Si
    //     pasaron GameConfig.OfflineRotSeconds (24 h) o más, se pudre: las parcelas vuelven vacías. Las monedas del tiempo fuera
    //     (OfflineManager) no se tocan.
    //   - Cuándo se aplica: cada campo pide su foto en su Start (Apply), con las plantas ya en el pool. En el celular, al volver de
    //     segundo plano sin cerrar (OnApplicationPause), los campos crecen o se pudren en el momento.
    //   - Reloj local, sin red: si la hora retrocedió, lo sembrado vuelve tal como estaba.
    //   - Partida guardada: ISaveable "plots". Es volátil (las parcelas cambian sin avisar): se escribe al pausar o cerrar. Una partida
    //     anterior a B-11 no trae la sección y arranca con las parcelas vacías, como antes.
    //   - La UI lee GrownPlots / RottedPlots para el aviso de la tarjeta de bienvenida y los borra con ClearNotice.
    public sealed class PlotStateManager : MonoBehaviour, ISaveable, ISaveableVolatile
    {
        public static PlotStateManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class FieldSave
        {
            public string id = string.Empty;
            public int[] states = new int[0];
            public float[] elapsed = new float[0];
        }

        [Serializable]
        private sealed class PlotsSave
        {
            public int version = SaveVersion;
            public string savedAtUtc = string.Empty;   // ISO 8601
            public FieldSave[] fields = new FieldSave[0];
        }

        private GameConfig _config;
        private readonly List<IPlotStateHolder> _holders = new List<IPlotStateHolder>(8);
        private readonly List<FieldSave> _loaded = new List<FieldSave>(8);   // fotos de la partida que todavía no pidió ningún campo
        private string _loadedJson;      // la sección tal como vino, mientras el nivel no arrancó (ningún campo pidió su foto)
        private double _awaySeconds;     // tiempo fuera medido al cargar
        private bool _rotted;            // pasó el plazo: lo cargado no se restaura
        private DateTime _pausedAtUtc;
        private bool _paused;

        // La hora actual en UTC; las pruebas la reemplazan.
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        // Para el aviso al volver: parcelas que volvieron sembradas tras un rato fuera, y parcelas que se pudrieron.
        public int GrownPlots { get; private set; }
        public int RottedPlots { get; private set; }

        // Horas enteras del plazo, para el texto del aviso.
        public int RotHours => _config != null ? Mathf.RoundToInt(_config.OfflineRotSeconds / 3600f) : 0;

        public event Action NoticeChanged;

        public void ClearNotice()
        {
            if (GrownPlots == 0 && RottedPlots == 0) return;

            GrownPlots = 0;
            RottedPlots = 0;
            NoticeChanged?.Invoke();
        }

        private GameManager _game;

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;
            Instance = this;
        }

        // Mientras no se empezó a jugar (pantalla de "Tocá para jugar") la partida cargada se vuelve a escribir tal cual, con su hora:
        // cerrar ahí no pierde ni el tiempo fuera ni lo sembrado. Al pasar a Playing se empieza a guardar lo que hay en los campos.
        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) _loadedJson = null;
        }

        // ---------- Campos ----------

        public void Register(IPlotStateHolder holder)
        {
            if (holder != null && !_holders.Contains(holder)) _holders.Add(holder);
        }

        public void Unregister(IPlotStateHolder holder)
        {
            _holders.Remove(holder);
        }

        // El campo pide su foto de la partida (una sola vez por carga): lo sembrado vuelve crecido, o no vuelve si se pudrió.
        public void Apply(IPlotStateHolder holder)
        {
            if (holder == null) return;

            if (_game == null) _loadedJson = null;   // sin GameManager (pruebas sueltas) el nivel "arranca" con el primer campo

            string id = holder.StateId;
            for (int i = 0; i < _loaded.Count; i++)
            {
                FieldSave save = _loaded[i];
                if (save.id != id) continue;

                _loaded.RemoveAt(i);

                if (_rotted)
                {
                    int lost = CountPlanted(save);
                    if (lost > 0)
                    {
                        RottedPlots += lost;
                        NoticeChanged?.Invoke();
                    }

                    return;
                }

                int planted = holder.RestoreState(save.states, save.elapsed, _awaySeconds);
                if (planted > 0 && _awaySeconds >= _config.OfflineMinSeconds)
                {
                    GrownPlots += planted;
                    NoticeChanged?.Invoke();
                }

                return;
            }
        }

        private static int CountPlanted(FieldSave save)
        {
            int planted = 0;
            for (int i = 0; i < save.states.Length; i++)
            {
                if (save.states[i] == (int)PlotState.Growing || save.states[i] == (int)PlotState.Ready) planted++;
            }

            return planted;
        }

        // Android/iOS (y el navegador al ocultar la pestaña): el juego pasó a segundo plano y volvió sin cerrarse. Lo sembrado crece ese
        // tiempo, o se pudre si fue más que el plazo. Mensaje de Unity; es público para poder probarlo sin escena.
        public void OnApplicationPause(bool paused)
        {
            if (_config == null) return;

            if (paused)
            {
                _pausedAtUtc = Clock();
                _paused = true;
                return;
            }

            if (!_paused) return;
            _paused = false;

            double away = OfflineMath.ElapsedSeconds(_pausedAtUtc, Clock());
            if (!(away > 0d)) return;

            if (away >= _config.OfflineRotSeconds)
            {
                int lost = 0;
                for (int i = 0; i < _holders.Count; i++) lost += _holders[i].ClearAll();
                if (lost > 0)
                {
                    RottedPlots += lost;
                    NoticeChanged?.Invoke();
                }

                return;
            }

            for (int i = 0; i < _holders.Count; i++) _holders[i].AdvanceAway(away);
        }

        // ---------- Partida guardada ----------

        public string SaveId => "plots";

#pragma warning disable 67   // las parcelas no piden guardar: la sección es volátil y viaja con cada escritura
        public event Action SaveRequested;
#pragma warning restore 67

        public bool HasVolatileState => true;

        public string Capture()
        {
            // Partida cargada pero el nivel todavía no arrancó: se conserva lo cargado con su hora (restamparlo haría perder el tiempo
            // fuera si se cierra sin llegar a jugar).
            if (_loadedJson != null) return _loadedJson;

            var fields = new List<FieldSave>(_holders.Count + _loaded.Count);
            for (int i = 0; i < _holders.Count; i++)
            {
                IPlotStateHolder holder = _holders[i];
                int count = holder.PlotCount;
                var field = new FieldSave { id = holder.StateId, states = new int[count], elapsed = new float[count] };
                holder.CaptureState(field.states, field.elapsed);
                fields.Add(field);
            }

            // Fotos que ningún campo pidió (un campo que hoy no está activo): se conservan.
            for (int i = 0; i < _loaded.Count; i++)
            {
                if (!fields.Exists(field => field.id == _loaded[i].id)) fields.Add(_loaded[i]);
            }

            return JsonUtility.ToJson(new PlotsSave
            {
                savedAtUtc = Clock().ToString("o", CultureInfo.InvariantCulture),
                fields = fields.ToArray()
            });
        }

        // Con null, vacío o ilegible: nada sembrado (partida nueva o anterior a B-11). Mide el tiempo fuera y decide si se pudrió. Si el
        // nivel ya está corriendo (cargar o borrar la partida a mitad del juego), los campos se actualizan en el momento.
        public void Restore(string json)
        {
            PlotsSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<PlotsSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _loaded.Clear();
            _loadedJson = null;
            _awaySeconds = 0d;
            _rotted = false;
            GrownPlots = 0;
            RottedPlots = 0;

            if (save != null && save.fields != null)
            {
                for (int i = 0; i < save.fields.Length; i++)
                {
                    FieldSave field = save.fields[i];
                    if (field == null || string.IsNullOrEmpty(field.id) || field.states == null || field.elapsed == null) continue;

                    _loaded.Add(field);
                }
            }

            if (_loaded.Count > 0)
            {
                _loadedJson = json;

                if (DateTime.TryParse(save.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime savedAt))
                {
                    _awaySeconds = Math.Max(0d, OfflineMath.ElapsedSeconds(savedAt, Clock()));
                    _rotted = _config != null && _awaySeconds >= _config.OfflineRotSeconds;
                }
            }

            // Con el nivel ya corriendo los campos no vuelven a pasar por Start: se vacían y se les aplica lo cargado acá.
            if (_holders.Count > 0)
            {
                for (int i = 0; i < _holders.Count; i++) _holders[i].ClearAll();
                for (int i = 0; i < _holders.Count; i++) Apply(_holders[i]);
                if (_game != null && _game.IsPlaying) _loadedJson = null;   // ya se está jugando: se guarda lo que quedó en los campos
            }

            NoticeChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }
    }
}
