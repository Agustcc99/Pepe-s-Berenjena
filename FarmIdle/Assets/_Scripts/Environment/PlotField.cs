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
    public sealed class PlotField : MonoBehaviour, IFertilizable, IProductionSource
    {
        [SerializeField] private PlotView[] plots;

        private ProgressionManager _progression;
        private FertilizerManager _fertilizer;
        private OfflineManager _offline;

        public IReadOnlyList<PlotView> Plots => plots;

        private void OnEnable()
        {
            // Opcional: sin FertilizerManager (una escena suelta) el campo simplemente no se puede fertilizar.
            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Register(this);

            _offline = OfflineManager.Instance;
            if (_offline != null) _offline.Register(this);
        }

        private void OnDisable()
        {
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
