using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // La guía de los primeros minutos, sin pantallas de texto: sembrar -> cosechar -> vender -> comprar la primera mejora. Este servicio
    // solo decide EN QUÉ PASO va el jugador (mirando lo que pasa en el juego, por eventos de Managers) y A DÓNDE hay que ir; dibujarlo
    // es de Environment (la flecha sobre el lugar) y de la UI (la flecha en el borde de la pantalla), que no se conocen entre sí.
    //   - Pasos (TutorialProgress): tres parcelas creciendo, tres cosechas, la primera venta, la primera compra. Cumplir un paso
    //     posterior completa los anteriores. Al terminar queda en Done y no vuelve a aparecer.
    //   - Lugares: los componentes de Environment se registran como ITutorialTarget y el servicio elige el del paso actual (el campo
    //     para sembrar y cosechar, el pad de venta, la primera mejora). Solo el lugar actual muestra su flecha.
    //   - Partida guardada: es el ISaveable "tutorial" (el paso). Una partida de una versión anterior no trae la sección pero SÍ tiene
    //     progreso: en ese caso la guía se da por hecha (nadie con una granja armada necesita que le expliquen a dónde ir).
    //   - Los eventos se escuchan recién en Bind(), que el Bootstrapper llama DESPUÉS de cargar la partida: restaurar no cuenta como jugar.
    public sealed class TutorialManager : MonoBehaviour, ISaveable
    {
        public static TutorialManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class TutorialSave
        {
            public int version = SaveVersion;
            public int step;
        }

        private readonly TutorialProgress _progress = new TutorialProgress();
        private readonly List<ITutorialTarget> _targets = new List<ITutorialTarget>(4);
        private ITutorialTarget _current;
        private bool _bound;
        private bool _subscribed;
        private bool _sawSavedSection;
        private PlotCensus _census;
        private IReadOnlyInventory _backpack;
        private EconomyManager _economy;
        private ProgressionManager _progression;

        public TutorialStep Step => _progress.Step;
        public bool IsActive => !_progress.IsDone;
        public int Harvested => _progress.Harvested;

        // El lugar al que hay que ir ahora (null si la guía terminó o no hay un lugar para ese paso).
        public ITutorialTarget CurrentTarget => IsActive ? _current : null;

        // Se cumplió un paso (con el paso nuevo). Nunca al restaurar.
        public event Action<TutorialStep> StepChanged;

        // Cambió el lugar actual (otro paso, o se registró/dio de baja un lugar).
        public event Action TargetChanged;

        // Lo llama el Bootstrapper, antes del SaveManager; hasta entonces Instance es null.
        public void Initialize()
        {
            _progress.StepChanged += OnStepChanged;
            Instance = this;
        }

        // Lo llama el Bootstrapper DESPUÉS de cargar la partida: empieza a escuchar el juego. Un jugador con una partida anterior (sin la
        // sección del tutorial pero con progreso) salta la guía.
        public void Bind()
        {
            if (_bound) return;
            _bound = true;

            _progression = ProgressionManager.Instance;
            _economy = EconomyManager.Instance;
            _census = PlotCensus.Instance;
            BackpackManager backpacks = BackpackManager.Instance;
            _backpack = backpacks != null ? backpacks.Backpack : null;

            if (!_sawSavedSection && !_progress.IsDone && HasPriorProgress())
            {
                _progress.Finish();   // sin ruido: todavía nadie escucha StepChanged
            }

            if (!_progress.IsDone) Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            if (_census != null) _census.Changed += OnCensusChanged;
            if (_backpack != null) _backpack.Changed += OnBackpackChanged;
            if (_economy != null) _economy.ProductSold += OnProductSold;
            if (_progression != null) _progression.Changed += OnProgressChanged;
        }

        private bool HasPriorProgress()
        {
            if (_progression == null) return false;

            return _progression.Xp > 0L || _progression.UnlockedZoneCount > 0 || _progression.Progress.TotalPerkPicks > 0 || AnyUpgradeBought();
        }

        private bool AnyUpgradeBought()
        {
            IReadOnlyList<UpgradeData> upgrades = _progression.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null && _progression.LevelOf(upgrades[i]) > 0) return true;
            }

            return false;
        }

        // ---------- Lugares ----------

        public void RegisterTarget(ITutorialTarget target)
        {
            if (target == null || _targets.Contains(target)) return;

            _targets.Add(target);
            RefreshCurrent();
        }

        public void UnregisterTarget(ITutorialTarget target)
        {
            if (!_targets.Remove(target)) return;

            if (target == _current) target.SetCurrent(false);
            RefreshCurrent();
        }

        // Sembrar y cosechar comparten lugar: el campo.
        private static TutorialStep PlaceStepOf(TutorialStep step)
        {
            return step == TutorialStep.Harvest ? TutorialStep.Sow : step;
        }

        private void RefreshCurrent()
        {
            ITutorialTarget next = null;
            if (IsActive)
            {
                TutorialStep wanted = PlaceStepOf(_progress.Step);
                for (int i = 0; i < _targets.Count; i++)
                {
                    if (_targets[i].Step == wanted)
                    {
                        next = _targets[i];
                        break;
                    }
                }
            }

            if (next == _current) return;

            if (_current != null) _current.SetCurrent(false);
            _current = next;
            if (_current != null) _current.SetCurrent(true);
            TargetChanged?.Invoke();
        }

        // ---------- Lo que pasa en el juego ----------

        private void OnCensusChanged()
        {
            _progress.ReportGrowing(_census.Growing);
        }

        private void OnBackpackChanged(InventoryChange change)
        {
            if (change.IsReplacement || !change.Added) return;

            _progress.ReportHarvest();
        }

        private void OnProductSold(CropData crop, long coins)
        {
            _progress.ReportSold();
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked || change.Kind == ProgressChangeKind.UpgradeLevelChanged) _progress.ReportPurchased();
        }

        private void OnStepChanged(TutorialStep step)
        {
            RefreshCurrent();
            SaveRequested?.Invoke();
            StepChanged?.Invoke(step);

            if (step == TutorialStep.Done) Unbind();
        }

        private void Unbind()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (_census != null) _census.Changed -= OnCensusChanged;
            if (_backpack != null) _backpack.Changed -= OnBackpackChanged;
            if (_economy != null) _economy.ProductSold -= OnProductSold;
            if (_progression != null) _progression.Changed -= OnProgressChanged;
        }

        // Debug y pruebas: termina la guía ya.
        public void Skip()
        {
            _progress.Finish();
        }

        // ---------- Partida guardada ----------

        public string SaveId => "tutorial";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new TutorialSave { step = (int)_progress.Step });
        }

        // Con null o vacío empieza la guía de cero. Restaurar no avisa ningún paso.
        public void Restore(string json)
        {
            TutorialSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<TutorialSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _sawSavedSection = save != null;
            _progress.Restore(save != null ? save.step : (int)TutorialStep.Sow);

            // Restaurar después de Bind (borrar la partida en desarrollo) tiene que volver a escuchar si la guía quedó activa.
            if (_bound)
            {
                Unbind();
                if (!_progress.IsDone) Subscribe();
            }

            RefreshCurrent();
        }

        private void OnDestroy()
        {
            _progress.StepChanged -= OnStepChanged;
            Unbind();
            if (Instance == this) Instance = null;
        }
    }
}
