using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Un campo: el grupo de parcelas de una zona. Hace dos cosas por todas ellas:
    //   - al cargar el nivel declara el Prewarm de plantas (una por parcela, agrupadas por prefab), así que sembrar
    //     nunca hace Instantiate durante el juego;
    //   - en cada frame le pasa a las parcelas el tiempo del juego (Time.deltaTime, que vale 0 en pausa y con un
    //     anuncio en pantalla: el crecimiento se congela solo), multiplicado por la velocidad de crecimiento que dan las
    //     mejoras (ProgressionManager.Modifiers). Un solo Update para todo el campo.
    //
    // Prewarm deja el pool con N instancias EN TOTAL, no suma. Con varios campos (una zona por campo, cada una apagada hasta que
    // se desbloquea) cada campo cuenta las plantas del NIVEL ENTERO: las parcelas de todos los PlotField de su escena, también
    // los que están desactivados. Así el primer campo que arranca deja listas las plantas de las zonas cerradas, y cuando una
    // zona se abre su Prewarm repite la misma cuenta y no crea nada (al abrir una zona no hay Instantiate).
    //
    // Fertilizante (Fase 8): el campo es un IFertilizable. Se registra en el FertilizerManager al activarse y se da de baja al apagarse,
    // así que una zona cerrada (campo apagado) queda fuera sin lógica extra.
    //
    // Progreso offline (B-01): el campo también es una IProductionSource, registrada en el OfflineManager con el mismo ciclo (activo =
    // zona abierta), que suma lo que rinde cada parcela según su cultivo.
    //
    // Lo sembrado se guarda (B-11): el campo es un IPlotStateHolder, registrado en el PlotStateManager con el mismo ciclo. Al arrancar
    // (Start, con las plantas ya en el pool) pide su foto de la partida: lo sembrado vuelve, crecido con el tiempo que se estuvo fuera.
    public sealed class PlotField : MonoBehaviour, IFertilizable, IProductionSource, IPlotStateHolder
    {
        [SerializeField] private PlotView[] plots;

        private ProgressionManager _progression;
        private FertilizerManager _fertilizer;
        private OfflineManager _offline;
        private PlotStateManager _plotStates;

        public IReadOnlyList<PlotView> Plots => plots;

        // ---------- IPlotStateHolder ----------

        // El nombre del campo y el de su padre (la zona): único dentro del nivel y estable entre sesiones.
        public string StateId => transform.parent != null ? transform.parent.name + "/" + name : name;

        public int PlotCount => plots.Length;

        public void CaptureState(int[] states, float[] elapsed)
        {
            for (int i = 0; i < plots.Length && i < states.Length && i < elapsed.Length; i++)
            {
                states[i] = (int)plots[i].State;
                elapsed[i] = plots[i].ElapsedSeconds;
            }
        }

        public int RestoreState(int[] states, float[] elapsed, double awaySeconds)
        {
            for (int i = 0; i < plots.Length && i < states.Length && i < elapsed.Length; i++)
            {
                int state = states[i];
                if (state != (int)PlotState.Growing && state != (int)PlotState.Ready) continue;

                plots[i].Restore((PlotState)state, Mathf.Max(0f, elapsed[i]));
            }

            AdvanceAway(awaySeconds);

            int planted = 0;
            for (int i = 0; i < plots.Length; i++)
            {
                if (plots[i].State != PlotState.Empty) planted++;
            }

            return planted;
        }

        public void AdvanceAway(double awaySeconds)
        {
            if (!(awaySeconds > 0d)) return;

            float seconds = (float)System.Math.Min(awaySeconds, 1e7) * GrowthFactor();
            for (int i = 0; i < plots.Length; i++) plots[i].Tick(seconds);
        }

        public int ClearAll()
        {
            int cleared = 0;
            for (int i = 0; i < plots.Length; i++)
            {
                if (plots[i].Rot()) cleared++;
            }

            return cleared;
        }

        // La velocidad de crecimiento de hoy: mejoras x tienda de prestigio (1 sin ninguna de las dos).
        private float GrowthFactor()
        {
            if (_progression == null) _progression = ProgressionManager.Instance;
            return (_progression != null ? _progression.Modifiers.GrowthSpeed : 1f) * PrestigeManager.GrowthFactor;
        }

        private void OnEnable()
        {
            // Opcional: sin FertilizerManager (una escena suelta) el campo simplemente no se puede fertilizar.
            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Register(this);

            _offline = OfflineManager.Instance;
            if (_offline != null) _offline.Register(this);

            _plotStates = PlotStateManager.Instance;
            if (_plotStates != null) _plotStates.Register(this);
        }

        private void OnDisable()
        {
            if (_plotStates != null)
            {
                _plotStates.Unregister(this);
                _plotStates = null;
            }

            if (_fertilizer != null)
            {
                _fertilizer.Unregister(this);
                _fertilizer = null;
            }

            if (_offline != null)
            {
                _offline.Unregister(this);
                _offline = null;
            }
        }

        // IProductionSource
        public float BaseCoinsPerSecond
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < plots.Length; i++)
                {
                    CropData crop = plots[i].Crop;
                    if (crop != null && crop.GrowSeconds > 0f) total += crop.SellPrice / crop.GrowSeconds;
                }

                return total;
            }
        }

        private void Start()
        {
            PrewarmPlants();

            // Con las plantas ya en el pool y todas las parcelas despiertas: lo sembrado en la partida guardada vuelve a su lugar.
            if (_plotStates != null) _plotStates.Apply(this);
        }

        // IFertilizable
        public int GrowingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < plots.Length; i++)
                {
                    if (plots[i].State == PlotState.Growing) count++;
                }

                return count;
            }
        }

        public int GrowAllNow()
        {
            int grown = 0;
            for (int i = 0; i < plots.Length; i++)
            {
                if (plots[i].FinishGrowing()) grown++;
            }

            return grown;
        }

        private void Update()
        {
            float deltaSeconds = Time.deltaTime;
            if (deltaSeconds <= 0f) return;   // pausa o anuncio: nada crece

            // Opcional: sin ProgressionManager (una escena suelta) las plantas crecen a velocidad normal. Se busca hasta
            // encontrarlo y después se guarda: no hay búsqueda por frame.
            if (_progression == null) _progression = ProgressionManager.Instance;
            if (_progression != null) deltaSeconds *= _progression.Modifiers.GrowthSpeed;
            deltaSeconds *= PrestigeManager.GrowthFactor;   // F-11: mejora permanente de la tienda de prestigio (1 si no hay)

            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].Tick(deltaSeconds);
            }
        }

        private void PrewarmPlants()
        {
            PoolManager pools = PoolManager.Instance;
            if (pools == null)
            {
                Debug.LogError("[FarmIdle] PlotField: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            // Una planta por parcela de todo el nivel, contadas por prefab (cultivos distintos pueden compartir prefab).
            var needed = new Dictionary<GameObject, int>();
            foreach (PlotField field in FieldsOfLevel())
            {
                field.CountPlants(needed);
            }

            foreach (KeyValuePair<GameObject, int> entry in needed)
            {
                pools.Prewarm(entry.Key, entry.Value);
            }
        }

        // Los campos de la escena de este (activos o no), y este mismo aunque viva fuera de ella.
        private List<PlotField> FieldsOfLevel()
        {
            var fields = new List<PlotField>();
            var found = new List<PlotField>();

            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(true, found);
                fields.AddRange(found);
            }

            if (!fields.Contains(this)) fields.Add(this);
            return fields;
        }

        private void CountPlants(Dictionary<GameObject, int> needed)
        {
            for (int i = 0; i < plots.Length; i++)
            {
                CropData crop = plots[i].Crop;
                if (crop == null || crop.PlantPrefab == null)
                {
                    Debug.LogError("[FarmIdle] La parcela '" + plots[i].name + "' no tiene cultivo con prefab de planta.", plots[i]);
                    continue;
                }

                needed.TryGetValue(crop.PlantPrefab, out int count);
                needed[crop.PlantPrefab] = count + 1;
            }
        }
    }
}
