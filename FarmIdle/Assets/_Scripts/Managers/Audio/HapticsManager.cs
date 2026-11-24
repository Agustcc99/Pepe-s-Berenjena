using System;
using UnityEngine;

namespace FarmIdle
{
    // La vibración del juego (ROADMAP U-06). Escucha a los managers, igual que el AudioManager, y pide pulsos cortos a un IVibrator:
    // suave al cosechar y vender, medio al comprar, fuerte al subir de nivel. Solo móvil (Android y la web táctil); en el Editor y en PC
    // el vibrador es NullVibrator.
    //   - Respeta el interruptor "Vibración" de los ajustes en el momento de cada pulso (si no hay SettingsManager, vibra).
    //   - PulseThrottle: una racha de ventas o cosechas no es un zumbido continuo, pero un pulso más fuerte (subir de nivel) siempre pasa.
    //   - Solo reacciona a eventos del juego (pocos por segundo): sin trabajo por cuadro.
    public sealed class HapticsManager : MonoBehaviour
    {
        // Pulsos: duración en ms y fuerza (1 a 255). La fuerza también decide quién pasa el límite de frecuencia.
        private static readonly Pulse Harvest = new Pulse(12, 60);
        private static readonly Pulse Sell = new Pulse(15, 80);
        private static readonly Pulse Purchase = new Pulse(40, 180);
        private static readonly Pulse Perk = new Pulse(30, 150);
        private static readonly Pulse Boost = new Pulse(40, 170);   // fertilizante y Turbo
        private static readonly Pulse LevelUp = new Pulse(70, 230);

        private readonly struct Pulse
        {
            public readonly int Milliseconds;
            public readonly int Amplitude;

            public Pulse(int milliseconds, int amplitude)
            {
                Milliseconds = milliseconds;
                Amplitude = amplitude;
            }
        }

        public const float MinSecondsBetweenPulses = 0.08f;

        public static HapticsManager Instance { get; private set; }

        private IVibrator _vibrator;
        private readonly PulseThrottle _throttle = new PulseThrottle(MinSecondsBetweenPulses);
        private bool _subscribed;

        private SettingsManager _settings;
        private BackpackManager _backpackManager;
        private IReadOnlyInventory _backpack;
        private EconomyManager _economy;
        private ProgressionManager _progression;
        private FertilizerManager _fertilizer;
        private BuffManager _buffs;
        private DecorationManager _decorations;

        public IVibrator Vibrator => _vibrator;

        // Cuántos pulsos se pidieron al vibrador (para pruebas y depuración).
        public int PulseCount { get; private set; }

        // Lo llama el Bootstrapper, después del audio; hasta entonces Instance es null. Con 'vibrator' (pruebas) se usa ese.
        public void Initialize(IVibrator vibrator = null)
        {
            Unsubscribe();
            _vibrator = vibrator ?? CreateDefaultVibrator();
            _throttle.Reset();
            Subscribe();
            Instance = this;
        }

        private static IVibrator CreateDefaultVibrator()
        {
#if UNITY_EDITOR
            return new NullVibrator();
#elif UNITY_ANDROID
            return new AndroidVibrator();
#elif UNITY_WEBGL
            return new WebVibrator();
#else
            return new NullVibrator();
#endif
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            _settings = SettingsManager.Instance;

            _backpackManager = BackpackManager.Instance;
            if (_backpackManager != null)
            {
                _backpack = _backpackManager.Backpack;
                _backpack.Changed += OnBackpackChanged;
            }

            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.ProductSold += OnProductSold;

            _progression = ProgressionManager.Instance;
            if (_progression != null)
            {
                _progression.Changed += OnProgressChanged;
                _progression.LevelChanged += OnLevelChanged;
            }

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Used += OnFertilizerUsed;

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.BuffStarted += OnBuffStarted;

            _decorations = DecorationManager.Instance;
            if (_decorations != null) _decorations.Bought += OnDecorationBought;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (_backpack != null) _backpack.Changed -= OnBackpackChanged;
            if (_economy != null) _economy.ProductSold -= OnProductSold;

            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
            }

            if (_fertilizer != null) _fertilizer.Used -= OnFertilizerUsed;
            if (_buffs != null) _buffs.BuffStarted -= OnBuffStarted;
            if (_decorations != null) _decorations.Bought -= OnDecorationBought;
        }

        // F-22: comprar una decoración vibra como elegir un perk.
        private void OnDecorationBought(int index)
        {
            DoPulse(Perk);
        }

        private void DoPulse(Pulse pulse)
        {
            if (_vibrator == null) return;
            if (_settings != null && !_settings.VibrationEnabled) return;
            if (!_throttle.TryPass(Time.unscaledTimeAsDouble, pulse.Amplitude)) return;

            PulseCount++;
            _vibrator.Pulse(pulse.Milliseconds, pulse.Amplitude);
        }

        private void OnBackpackChanged(InventoryChange change)
        {
            if (change.IsReplacement || !change.Added) return;

            DoPulse(Harvest);
        }

        private void OnProductSold(CropData crop, long coins)
        {
            DoPulse(Sell);
        }

        private void OnProgressChanged(ProgressChange change)
        {
            switch (change.Kind)
            {
                case ProgressChangeKind.ZoneUnlocked:
                case ProgressChangeKind.UpgradeLevelChanged:
                    DoPulse(Purchase);
                    break;

                case ProgressChangeKind.PerkChosen:
                    DoPulse(Perk);
                    break;
            }
        }

        private void OnLevelChanged(int level)
        {
            DoPulse(LevelUp);
        }

        private void OnFertilizerUsed(int grown)
        {
            DoPulse(Boost);
        }

        private void OnBuffStarted(RewardData reward)
        {
            if (reward != null && reward.Kind == RewardKind.MoveSpeedBuff) DoPulse(Boost);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }
    }
}
