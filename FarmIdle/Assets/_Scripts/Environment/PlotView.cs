using UnityEngine;

namespace FarmIdle
{
    // Una parcela en el mundo. Junta tres cosas y no agrega reglas propias:
    //   - la lógica (PlotLogic: Empty -> Growing -> Ready),
    //   - la planta visible (PlantView), que sale del PoolManager al sembrar y vuelve al quedar vacía,
    //   - la interacción: es un IInteractable, así que no sabe quién la pisa. Solo ve un IActor (hoy el jugador,
    //     mañana un ayudante). El que detecta al actor es el InteractionTrigger del mismo objeto.
    //     Pisar una parcela vacía la siembra; pisar una lista la cosecha si el actor tiene una mochila con lugar
    //     (IBackpackOwner): el producto entra a la mochila, la planta vuelve al pool y la parcela queda vacía, y si
    //     el actor sigue encima el siguiente Interact la vuelve a sembrar (ciclo continuo).
    // El tiempo se lo pasa el PlotField con Tick, usando el tiempo del juego.
    public sealed class PlotView : MonoBehaviour, IInteractable
    {
        // Desde qué altura sobre la planta sale el producto cosechado (solo para animarlo hasta la espalda).
        private const float HarvestHeight = 0.35f;

        [SerializeField, Tooltip("Cultivo de esta parcela: define cuánto tarda y qué planta se ve.")]
        private CropData crop;

        [SerializeField, Tooltip("Dónde nace la planta. Sin asignar, el origen de la parcela.")]
        private Transform plantAnchor;

        // Aviso cosmético de una cosecha lograda (cultivo y punto del mundo donde estaba la planta). Es estático para que un solo
        // HarvestFeedback en la escena escuche todas las parcelas; el que escucha tiene que desuscribirse al apagarse.
        public static event System.Action<CropData, Vector3> Harvested;

        // J-04: después de Harvested, cuando esa cosecha salió rara. Lleva las monedas del premio (ya acreditadas).
        public static event System.Action<CropData, Vector3, long> RareHarvested;

        private PlotLogic _logic;
        private PlantView _plant;
        private PlotCensus _census;
        private bool _configErrorLogged;

        public CropData Crop => crop;
        // Se pueden leer antes del Awake de la parcela (al cargar el nivel, otro objeto puede consultar al campo en su OnEnable): hasta
        // entonces está vacía.
        public PlotState State => _logic != null ? _logic.State : PlotState.Empty;
        public float Progress => _logic != null ? _logic.Progress : 0f;

        // La planta que se ve ahora (null mientras la parcela está vacía).
        public PlantView Plant => _plant;

        private void Awake()
        {
            _logic = new PlotLogic();
        }

        private void OnEnable()
        {
            _logic.StateChanged += OnLogicStateChanged;

            _census = PlotCensus.Instance;
            if (_census != null) _census.Register(_logic.State);

            // Si se reactiva con algo plantado, recupera su planta (la anterior volvió al pool al desactivarse).
            if (_logic.State != PlotState.Empty) ApplyVisual(animate: false);
        }

        private void OnDisable()
        {
            _logic.StateChanged -= OnLogicStateChanged;

            if (_census != null)
            {
                _census.Unregister(_logic.State);
                _census = null;
            }

            // La planta vive en el pool, no en esta escena: tiene que volver antes de que la escena se descargue.
            ReleasePlant();
        }

        // IInteractable: pisar una parcela vacía la siembra sola (semilla gratis) y pisar una lista la cosecha, sin
        // botones. Llega en cada frame mientras el actor esté encima: no puede hacer trabajo pesado ni escribir en la
        // consola cuando no hay nada que hacer.
        public void Interact(IActor actor)
        {
            switch (_logic.State)
            {
                case PlotState.Empty:
                    TrySow();
                    break;

                case PlotState.Ready:
                    TryHarvestFor(actor);
                    break;
            }
        }

        public bool TrySow()
        {
            if (_logic.State != PlotState.Empty || !IsConfigured()) return false;

            return _logic.TryPlant(crop.GrowSeconds);
        }

        // Deja la parcela vacía y devuelve la planta al pool. Ojo: no entrega nada. El producto lo entrega
        // TryHarvestFor (que es lo que usa Interact) antes de llamar acá; llamarlo suelto tira la cosecha.
        public bool TryHarvest()
        {
            return _logic.TryHarvest();
        }

        // Cosecha para 'actor' si tiene una mochila con lugar. La transacción es instantánea y autoritativa: el
        // producto entra a la mochila y en el mismo instante la parcela queda vacía; lo que se ve (el producto que
        // vuela hasta la espalda) es cosmético y lo anima quien lo recibe. Con la mochila llena, o con un actor sin
        // mochila, no hace nada y la parcela sigue lista (y no escribe en la consola: esto corre en cada frame).
        private bool TryHarvestFor(IActor actor)
        {
            if (crop == null || !(actor is IBackpackOwner owner)) return false;

            Transform anchor = plantAnchor != null ? plantAnchor : transform;
            if (!owner.TryAccept(crop, anchor.position + Vector3.up * HarvestHeight)) return false;

            if (!TryHarvest())
            {
                Debug.LogError("[FarmIdle] La parcela '" + name + "' entregó el producto pero no pudo vaciarse (estaba lista).", this);
            }

            Harvested?.Invoke(crop, anchor.position);

            // J-04: a veces la cosecha sale rara; el premio ya está acreditado, esto solo avisa para que se vea.
            EconomyManager economy = EconomyManager.Instance;
            long rareBonus = economy != null ? economy.TryRareHarvest(crop) : 0L;
            if (rareBonus > 0L) RareHarvested?.Invoke(crop, anchor.position, rareBonus);
            return true;
        }

        // Fertilizante: termina de crecer de una vez si estaba creciendo (el cambio de estado ya muestra la planta madura, con la
        // misma planta del pool: no se crea nada). Una parcela vacía o ya lista no se toca.
        public bool FinishGrowing()
        {
            return _logic.TryFinishGrowing();
        }

        // B-11: segundos de crecimiento que lleva lo sembrado (para guardarlo en la partida).
        public float ElapsedSeconds => _logic != null ? _logic.Elapsed : 0f;

        // B-11: deja la parcela como estaba en la partida guardada, sin entregar nada. Solo sobre una parcela vacía.
        public void Restore(PlotState state, float elapsedSeconds)
        {
            if (_logic == null || _logic.State != PlotState.Empty || state == PlotState.Empty || !IsConfigured()) return;
            if (!_logic.TryPlant(crop.GrowSeconds)) return;

            if (state == PlotState.Ready) _logic.TryFinishGrowing();
            else Tick(elapsedSeconds);
        }

        // B-11: lo sembrado se pudrió: la parcela queda vacía sin entregar nada. Devuelve true si tenía algo.
        public bool Rot()
        {
            if (_logic == null || _logic.State == PlotState.Empty) return false;

            if (_logic.State == PlotState.Growing) _logic.TryFinishGrowing();
            return _logic.TryHarvest();
        }

        // Lo llama el PlotField en cada frame con el tiempo del juego (0 en pausa y con un anuncio en pantalla).
        public void Tick(float deltaSeconds)
        {
            if (_logic.State != PlotState.Growing || !(deltaSeconds > 0f)) return;

            _logic.Tick(deltaSeconds);

            // Si acaba de quedar lista, el cambio de estado ya mostró la planta madura.
            if (_logic.State == PlotState.Growing && _plant != null) _plant.SetGrowth(_logic.Progress);
        }

        private void OnLogicStateChanged(PlotState previous, PlotState current)
        {
            if (_census != null) _census.Move(previous, current);

            ApplyVisual(animate: true);
        }

        private void ApplyVisual(bool animate)
        {
            switch (_logic.State)
            {
                case PlotState.Empty:
                    ReleasePlant();
                    break;

                case PlotState.Growing:
                    if (AcquirePlant())
                    {
                        _plant.BeginGrowing();
                        _plant.SetGrowth(_logic.Progress);
                    }
                    break;

                case PlotState.Ready:
                    if (AcquirePlant()) _plant.ShowReady(animate);
                    break;
            }
        }

        // Devuelve true si la parcela tiene planta (la pidió ahora al pool o ya la tenía).
        private bool AcquirePlant()
        {
            if (_plant != null) return true;
            if (!IsConfigured()) return false;

            Transform anchor = plantAnchor != null ? plantAnchor : transform;
            GameObject instance = PoolManager.Instance.Get(crop.PlantPrefab, anchor.position, anchor.rotation);

            if (!instance.TryGetComponent(out _plant))
            {
                Debug.LogError("[FarmIdle] El prefab de planta '" + crop.PlantPrefab.name + "' no tiene PlantView.", crop.PlantPrefab);
                PoolManager.Instance.Release(instance);
                return false;
            }

            return true;
        }

        private void ReleasePlant()
        {
            if (_plant == null) return;

            GameObject instance = _plant.gameObject;
            _plant = null;

            // Sin PoolManager (el juego se está cerrando) la planta se destruye junto con su contenedor.
            if (PoolManager.Instance != null) PoolManager.Instance.Release(instance);
        }

        // Una parcela mal armada (sin cultivo, sin prefab o sin pool) no siembra y avisa una sola vez,
        // en vez de llenar la consola con un error por frame.
        private bool IsConfigured()
        {
            bool configured = crop != null && crop.PlantPrefab != null && PoolManager.Instance != null;

            if (!configured && !_configErrorLogged)
            {
                _configErrorLogged = true;
                Debug.LogError("[FarmIdle] La parcela '" + name + "' no puede sembrar: falta el cultivo, su prefab de planta o el PoolManager.", this);
            }

            return configured;
        }
    }
}
