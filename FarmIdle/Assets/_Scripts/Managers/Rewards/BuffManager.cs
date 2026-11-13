using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de los buffs TEMPORALES (los que da un video), separados de las mejoras y los perks permanentes. Tiene el BuffTracker
    // (estado puro: id, segundos restantes) y reparte los efectos por las mismas puertas de siempre:
    //   - velocidad: PlayerMovement le pide FinalMoveSpeed(base, permanente) y recibe el valor con el tope final; el multiplicador
    //     PERMANENTE sigue saliendo de ProgressionManager.Modifiers sin que nadie lo toque desde acá;
    //   - precio: PriceCalculator.SetBuffMultiplier (EconomyManager.Pricing), que multiplica aparte del multiplicador de mejoras.
    //   - El tiempo es el del juego (Time.deltaTime), y solo mientras el estado es Playing: no corre en el menú, en pausa ni con
    //     un anuncio en pantalla (ahí el tiempo vale 0 igual). Sin reloj de pared.
    //   - Partida guardada: es el ISaveable "buffs" y guarda los SEGUNDOS RESTANTES por id (decisión 10). Al cargar se ponen en
    //     silencio: Restore no dispara Activated ni toca nada del mundo. Un id que ya no existe en el GameConfig se descarta.
    //     Como los segundos cambian con el tiempo sin avisar, implementa ISaveableVolatile: al pausar o cerrar se escribe siempre.
    public sealed class BuffManager : MonoBehaviour, ISaveable, ISaveableVolatile
    {
        public static BuffManager Instance { get; private set; }

        private const int SaveVersion = 1;
        private const float SaveEverySeconds = 5f;   // mientras haya un buff, se pide guardar cada tanto (el SaveManager lo junta)

        [Serializable]
        private sealed class BuffEntrySave
        {
            public string id;
            public float remaining;
        }

        [Serializable]
        private sealed class BuffsSave
        {
            public int version = SaveVersion;
            public BuffEntrySave[] buffs = new BuffEntrySave[0];
        }

        private GameConfig _config;
        private BuffTracker _tracker;
        private float _saveClock;

        // Los buffs activos y sus segundos restantes (solo lectura: la UI y el panel de debug los leen).
        public IReadOnlyList<ActiveBuff> Active => _tracker.Active;
        public bool HasAnyBuff => _tracker.Count > 0;

        // El multiplicador de velocidad del buff (1 = sin buff). El valor FINAL de la velocidad sale de FinalMoveSpeed.
        public float MoveSpeedMultiplier => _tracker.MultiplierOf(BuffKind.MoveSpeed);
        public float SellPriceMultiplier => _tracker.MultiplierOf(BuffKind.SellPrice);
        public float MaxFinalMoveSpeed => _config != null ? _config.MaxFinalMoveSpeed : 0f;

        // Se activó (o renovó) un buff jugando, con su recompensa. Nunca al restaurar una partida.
        public event Action<RewardData> BuffStarted;

        // Un buff se terminó solo (con su id).
        public event Action<string> BuffExpired;

        // Cambió la lista (activó, renovó, venció, se restauró). Lo lee el HUD.
        public event Action Changed;

        // Lo llama el Bootstrapper, después de la economía y la progresión; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tracker = new BuffTracker();
            _tracker.Expired += OnTrackerExpired;
            _tracker.Changed += OnTrackerChanged;
            Instance = this;
        }

        // ---------- Consultas ----------

        public bool IsActive(RewardData reward)
        {
            return reward != null && _tracker.IsActive(reward.Id);
        }

        public float RemainingOf(RewardData reward)
        {
            return reward != null ? _tracker.RemainingOf(reward.Id) : 0f;
        }

        // La recompensa (datos) de un buff activo, para mostrar su nombre. null si ya no existe en el GameConfig.
        public RewardData RewardOf(ActiveBuff buff)
        {
            return buff != null ? _config.FindReward(buff.Id) : null;
        }

        // La velocidad final: base x permanente x buff, con el tope final del GameConfig (ver MoveSpeedRules).
        public float FinalMoveSpeed(float baseSpeed, float permanentMultiplier)
        {
            return MoveSpeedRules.Final(baseSpeed, permanentMultiplier, MoveSpeedMultiplier, MaxFinalMoveSpeed);
        }

        // ---------- Activar ----------

        // Activa el buff de la recompensa (o lo renueva a su duración completa si ya estaba). Devuelve false si la recompensa no es
        // un buff. No mira el estado del juego: quien otorga (AdsManager) ya lo hizo.
        public bool Activate(RewardData reward)
        {
            if (reward == null || !reward.IsBuff) return false;

            BuffKind kind = reward.Kind == RewardKind.MoveSpeedBuff ? BuffKind.MoveSpeed : BuffKind.SellPrice;
            if (!_tracker.Activate(reward.Id, kind, reward.Magnitude, reward.DurationSeconds)) return false;

            BuffStarted?.Invoke(reward);
            SaveRequested?.Invoke();
            return true;
        }

        // Debug y pruebas: apaga todo.
        public void ClearAll()
        {
            _tracker.Clear();
            SaveRequested?.Invoke();
        }

        // ---------- Tiempo ----------

        private void Update()
        {
            if (_tracker == null || _tracker.Count == 0) return;

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return;   // menú, pausa o anuncio: los buffs no corren

            float delta = Time.deltaTime;
            if (!(delta > 0f)) return;

            _tracker.Tick(delta);

            _saveClock += delta;
            if (_saveClock >= SaveEverySeconds)
            {
                _saveClock = 0f;
                if (_tracker.Count > 0) SaveRequested?.Invoke();
            }
        }

        private void OnTrackerExpired(ActiveBuff buff)
        {
            BuffExpired?.Invoke(buff.Id);
            SaveRequested?.Invoke();
        }

        private void OnTrackerChanged()
        {
            ApplyEffects();
            Changed?.Invoke();
        }

        // El precio se aplica por la puerta de siempre; la velocidad la lee el jugador (FinalMoveSpeed).
        private void ApplyEffects()
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Pricing.SetBuffMultiplier(SellPriceMultiplier);
        }

        // ---------- Partida guardada ----------

        public string SaveId => "buffs";

        public event Action SaveRequested;

        public bool HasVolatileState => _tracker != null && _tracker.Count > 0;

        public string Capture()
        {
            var save = new BuffsSave { buffs = new BuffEntrySave[_tracker.Count] };
            for (int i = 0; i < save.buffs.Length; i++)
            {
                save.buffs[i] = new BuffEntrySave { id = _tracker.Active[i].Id, remaining = _tracker.Active[i].Remaining };
            }

            return JsonUtility.ToJson(save);
        }

        // Con null o vacío no hay buffs (partida nueva, o un archivo de la Fase 7 que no trae esta sección). Un JSON ilegible se toma
        // igual como "sin buffs". No dispara BuffStarted: es una restauración, no una activación.
        public void Restore(string json)
        {
            BuffsSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<BuffsSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            var restored = new List<ActiveBuff>();
            if (save != null && save.buffs != null)
            {
                foreach (BuffEntrySave entry in save.buffs)
                {
                    RewardData reward = entry != null ? _config.FindReward(entry.id) : null;
                    if (reward == null || !reward.IsBuff) continue;   // el buff ya no existe: se descarta

                    restored.Add(new ActiveBuff
                    {
                        Id = reward.Id,
                        Kind = reward.Kind == RewardKind.MoveSpeedBuff ? BuffKind.MoveSpeed : BuffKind.SellPrice,
                        Magnitude = reward.Magnitude,
                        Duration = reward.DurationSeconds,
                        Remaining = entry.remaining
                    });
                }
            }

            _saveClock = 0f;
            _tracker.Restore(restored);
        }

        private void OnDestroy()
        {
            if (_tracker != null)
            {
                _tracker.Expired -= OnTrackerExpired;
                _tracker.Changed -= OnTrackerChanged;
            }

            if (Instance == this) Instance = null;
        }
    }
}
