using System;
using UnityEngine;

namespace FarmIdle
{
    // El audio del juego (el último servicio del Bootstrapper). Efectos y música, con una regla simple: el audio ESCUCHA eventos de
    // Managers y el gameplay y la UI no lo conocen (ni siquiera saben que existe).
    //   - Efectos: un pool de AudioSource creados UNA vez al arrancar (GameConfig.AudioVoices), repartidos en ronda; nada de Instantiate
    //     ni de PlayOneShot mientras se juega. Cada sonido sale del AudioBank (variantes al azar, tono con un poco de variación) y
    //     tiene un mínimo entre disparos (SoundThrottle) para que ninguna cosecha ni venta suene como una ametralladora.
    //   - Venta: vender varias unidades seguidas sube el tono un paso por unidad (ComboCounter), el "ding, ding, ding" ascendente.
    //   - Cosecha (J-08): cosechar seguido sube por una escala pentatónica (HarvestScale); la cosecha que llena la mochila suena a
    //     festejo (BackpackFull) y al terminar una venta grande suena la bocina del camión (StreakTracker).
    //   - Música: un loop suave en su propia fuente, que arranca cuando el jugador toca "Tap to Play" (en la web ese toque es el que
    //     le permite al navegador reproducir audio) y sigue hasta cerrar.
    //   - Mezcla por categorías: la ganancia de la música y la de los efectos salen de SettingsManager (volumen general x categoría,
    //     con mute). Se reaplican al cambiar un ajuste.
    //   - Silencio: en Paused, en AdShowing (el anuncio trae su propio sonido) y al perder el foco la app entera se calla con
    //     AudioListener.pause (Time.timeScale = 0 no calla el audio).
    //   - Sin AudioBank, sin clips o sin ganancia no pasa nada: el juego anda igual, sin sonido.
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const float SellComboWindowSeconds = 0.45f;
        private const int SellComboMaxSteps = 7;
        private const float SellComboPitchStep = 0.06f;
        private const float BackpackFullEverySeconds = 2.5f;   // "mochila llena" se avisa en cada intento de cosecha: se limita aparte

        private GameConfig _config;
        private AudioBank _bank;
        private SettingsManager _settings;
        private AudioSource[] _voices;
        private AudioSource _musicSource;
        private int _nextVoice;
        private readonly SoundThrottle _throttle = new SoundThrottle(Enum.GetValues(typeof(SoundCue)).Length);
        private readonly ComboCounter _sellCombo = new ComboCounter(SellComboWindowSeconds, SellComboMaxSteps);
        private readonly ComboCounter _harvestCombo = new ComboCounter(JuiceRules.HarvestStreakWindowSeconds, HarvestScale.MaxStep);
        private readonly StreakTracker _saleRun = new StreakTracker(JuiceRules.SaleRunWindowSeconds, JuiceRules.BigSaleUnits);
        private int[] _lastClip;
        private int[] _triggerCounts;
        private System.Random _random = new System.Random();
        private bool _focused = true;
        private bool _gameSilenced;
        private float _lastSfxGain = -1f;
        private float _nextFullAllowedAt;
        private bool _subscribed;
        private bool _sourcesActive;

        // Servicios que se escuchan (se guardan para darse de baja).
        private GameManager _game;
        private IReadOnlyInventory _backpack;
        private BackpackManager _backpackManager;
        private EconomyManager _economy;
        private ProgressionManager _progression;
        private FertilizerManager _fertilizer;
        private BuffManager _buffs;
        private AdsManager _ads;
        private OfferManager _offers;
        private TutorialManager _tutorial;

        // Para pruebas y depuración.
        public int VoiceCount => _voices != null ? _voices.Length : 0;
        public bool HasBank => _bank != null;
        public SoundCue LastCue { get; private set; }
        public int TotalTriggers { get; private set; }
        public bool ListenerPaused => AudioListener.pause;
        public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;
        public float MusicVolume => _musicSource != null ? _musicSource.volume : 0f;

        // Cuántas veces se disparó ese sonido (pasó el límite de frecuencia), haya clip o no.
        public int TriggerCount(SoundCue cue)
        {
            return _triggerCounts != null ? _triggerCounts[(int)cue] : 0;
        }

        // Lo llama el Bootstrapper, al final; hasta entonces Instance es null. 'seed' (pruebas) fija el azar.
        public void Initialize(GameConfig config, int? seed = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bank = config.AudioBank;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            _settings = SettingsManager.Instance;

            int cues = Enum.GetValues(typeof(SoundCue)).Length;
            _lastClip = new int[cues];
            _triggerCounts = new int[cues];
            for (int i = 0; i < cues; i++) _lastClip[i] = -1;

            CreateVoices(Mathf.Max(1, config.AudioVoices));
            Subscribe();
            ApplySettings();

            // Un arranque que ya pasó de Boot (pruebas con servicios sueltos) no va a recibir el aviso: se prenden de una.
            if (_game != null && _game.State != GameState.Boot) ActivateSources();

            Instance = this;
        }

        private void CreateVoices(int count)
        {
            _voices = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                _voices[i] = CreateSource("Voice " + i, loop: false);
            }

            _musicSource = CreateSource("Music", loop: true);
            if (_bank != null) _musicSource.clip = _bank.Music;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            go.SetActive(false);   // apagadas hasta que el nivel (y su AudioListener) esté cargado: ver ActivateSources

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;   // 2D: el sonido no depende de dónde está la cámara
            return source;
        }

        // ---------- Escuchar al juego ----------

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;

            _backpackManager = BackpackManager.Instance;
            if (_backpackManager != null)
            {
                _backpack = _backpackManager.Backpack;
                _backpack.Changed += OnBackpackChanged;
                _backpackManager.FullRejected += OnBackpackFull;
            }

            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.ProductSold += OnProductSold;

            _progression = ProgressionManager.Instance;
            if (_progression != null)
            {
                _progression.Changed += OnProgressChanged;
                _progression.LevelChanged += OnLevelChanged;
                _progression.Refused += OnRefused;
            }

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null)
            {
                _fertilizer.Used += OnFertilizerUsed;
                _fertilizer.Refused += OnRefused;
            }

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.BuffStarted += OnBuffStarted;

            _ads = AdsManager.Instance;
            if (_ads != null)
            {
                _ads.AdFinished += OnAdFinished;
                _ads.RequestRejected += OnAdRejected;
            }

            _offers = OfferManager.Instance;
            if (_offers != null) _offers.OfferStarted += OnOfferStarted;

            _tutorial = TutorialManager.Instance;
            if (_tutorial != null) _tutorial.StepChanged += OnTutorialStep;

            if (_settings != null) _settings.Changed += OnSettingsChanged;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (_backpack != null) _backpack.Changed -= OnBackpackChanged;
            if (_backpackManager != null) _backpackManager.FullRejected -= OnBackpackFull;
            if (_economy != null) _economy.ProductSold -= OnProductSold;

            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
                _progression.Refused -= OnRefused;
            }

            if (_fertilizer != null)
            {
                _fertilizer.Used -= OnFertilizerUsed;
                _fertilizer.Refused -= OnRefused;
            }

            if (_buffs != null) _buffs.BuffStarted -= OnBuffStarted;

            if (_ads != null)
            {
                _ads.AdFinished -= OnAdFinished;
                _ads.RequestRejected -= OnAdRejected;
            }

            if (_offers != null) _offers.OfferStarted -= OnOfferStarted;
            if (_tutorial != null) _tutorial.StepChanged -= OnTutorialStep;
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        private void OnBackpackChanged(InventoryChange change)
        {
            if (change.IsReplacement || !change.Added) return;

            // J-08: cosechar seguido sube una nota de la escala por parcela; la que llena la mochila suma su festejo.
            Play(SoundCue.Harvest, HarvestScale.PitchFor(_harvestCombo.Next(Time.unscaledTime)));
            if (_backpack.IsFull) Play(SoundCue.BackpackFull);
        }

        private void OnBackpackFull()
        {
            float now = Time.unscaledTime;
            if (now < _nextFullAllowedAt) return;

            _nextFullAllowedAt = now + BackpackFullEverySeconds;
            Play(SoundCue.Denied);
        }

        private void OnProductSold(CropData crop, long coins)
        {
            int step = _sellCombo.Next(Time.unscaledTime);
            Play(SoundCue.Sell, 1f + step * SellComboPitchStep);
            _saleRun.Add(Time.unscaledTime);
        }

        // J-08: cuando termina una venta grande (JuiceRules.BigSaleUnits sin cortar), el camión toca bocina. Una comparación por
        // cuadro mientras hay una tanda abierta; nada más.
        private void Update()
        {
            if (_saleRun.Count > 0 && _saleRun.PollBigStreakEnded(Time.unscaledTime)) Play(SoundCue.TruckHorn);
        }

        private void OnProgressChanged(ProgressChange change)
        {
            switch (change.Kind)
            {
                case ProgressChangeKind.ZoneUnlocked:
                case ProgressChangeKind.UpgradeLevelChanged:
                    Play(SoundCue.Purchase);
                    break;

                case ProgressChangeKind.PerkChosen:
                    Play(SoundCue.Perk);
                    break;
            }
        }

        private void OnLevelChanged(int level)
        {
            Play(SoundCue.LevelUp);
        }

        private void OnFertilizerUsed(int grown)
        {
            Play(SoundCue.Fertilize);
        }

        private void OnBuffStarted(RewardData reward)
        {
            if (reward != null && reward.Kind == RewardKind.MoveSpeedBuff) Play(SoundCue.Turbo);
        }

        private void OnAdFinished(RewardData reward, AdResult result, bool granted)
        {
            if (granted) Play(SoundCue.Reward);
        }

        private void OnAdRejected(RewardData reward, AdRequestStatus status)
        {
            // Pedir un video con el juego en menú, pausa o con otro anuncio no es un "no se puede" que el jugador deba oír.
            if (status == AdRequestStatus.NotPlaying || status == AdRequestStatus.Busy || status == AdRequestStatus.Ready) return;

            Play(SoundCue.Denied);
        }

        private void OnRefused()
        {
            Play(SoundCue.Denied);
        }

        private void OnOfferStarted(RewardData reward, int slot)
        {
            Play(SoundCue.Offer);
        }

        private void OnTutorialStep(TutorialStep step)
        {
            Play(SoundCue.Step);
        }

        // ---------- Estado del juego, foco y ajustes ----------

        // Las fuentes de audio arrancan apagadas: el AudioListener vive en la cámara de MainLevel, que se carga después de los servicios, y
        // con las fuentes prendidas Unity avisaría "no hay AudioListener" en los primeros cuadros. Se prenden al estar el nivel listo.
        private void ActivateSources()
        {
            if (_sourcesActive || _voices == null) return;
            _sourcesActive = true;

            for (int i = 0; i < _voices.Length; i++) _voices[i].gameObject.SetActive(true);
            if (_musicSource != null) _musicSource.gameObject.SetActive(true);
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Boot) ActivateSources();

            _gameSilenced = current == GameState.Paused || current == GameState.AdShowing;
            if (current == GameState.Playing) StartMusic();
            ApplyListenerPause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _focused = hasFocus;
            ApplyListenerPause();
        }

        private void OnApplicationPause(bool paused)
        {
            _focused = !paused;
            ApplyListenerPause();
        }

        private void ApplyListenerPause()
        {
            AudioListener.pause = _gameSilenced || !_focused;
        }

        private void StartMusic()
        {
            if (!_sourcesActive || _musicSource == null || _musicSource.clip == null || _musicSource.isPlaying) return;

            _musicSource.Play();
        }

        private void OnSettingsChanged()
        {
            float previousSfx = _lastSfxGain;
            ApplySettings();

            // Un "clic" al mover el volumen de los efectos (o al sacar el mute) deja oír cómo quedó.
            if (_settings != null && _lastSfxGain > 0f && !Mathf.Approximately(previousSfx, _lastSfxGain)) Play(SoundCue.Click);
        }

        private void ApplySettings()
        {
            float music = _settings != null ? _settings.Audio.MusicGain : 1f;
            _lastSfxGain = _settings != null ? _settings.Audio.SfxGain : 1f;

            if (_musicSource != null) _musicSource.volume = (_bank != null ? _bank.MusicVolume : 0f) * music;
        }

        // ---------- Reproducir ----------

        // Dispara un sonido. Devuelve true si salió por una voz (false: límite de frecuencia, sin banco, sin clip o sin volumen).
        public bool Play(SoundCue cue, float pitchMultiplier = 1f)
        {
            SoundEntry entry = _bank != null ? _bank.Find(cue) : null;
            double minInterval = entry != null ? entry.MinIntervalSeconds : 0d;
            if (!_throttle.TryPlay((int)cue, Time.unscaledTime, minInterval)) return false;

            TotalTriggers++;
            LastCue = cue;
            if (_triggerCounts != null) _triggerCounts[(int)cue]++;

            if (entry == null || entry.Clips == null || entry.Clips.Length == 0 || _voices == null || !_sourcesActive) return false;

            float gain = _settings != null ? _settings.Audio.SfxGain : 1f;
            if (gain <= 0f) return false;

            AudioClip clip = PickClip(entry, (int)cue);
            if (clip == null) return false;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            float pitch = Mathf.Lerp(entry.PitchMin, entry.PitchMax, (float)_random.NextDouble()) * pitchMultiplier;
            voice.clip = clip;
            voice.pitch = Mathf.Clamp(pitch, 0.5f, 3f);
            voice.volume = entry.Volume * gain;
            voice.Play();
            return true;
        }

        // Una variante al azar, distinta de la anterior si hay más de una.
        private AudioClip PickClip(SoundEntry entry, int cue)
        {
            AudioClip[] clips = entry.Clips;
            int index = _random.Next(clips.Length);
            if (clips.Length > 1 && index == _lastClip[cue]) index = (index + 1 + _random.Next(clips.Length - 1)) % clips.Length;

            _lastClip[cue] = index;
            return clips[index];
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this)
            {
                AudioListener.pause = false;   // la pausa del oyente es global: no puede quedar puesta al cerrar el audio
                Instance = null;
            }
        }
    }
}
