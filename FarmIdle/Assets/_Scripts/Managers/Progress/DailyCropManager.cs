using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // IN-02: el cultivo del día. Cada día un cultivo de las zonas abiertas paga GameConfig.DailyCropBonus de más (+50 %).
    //   - Se elige por fecha (DailyCropRules, misma semilla por día que las misiones) entre los cultivos de OrderCropRule cuya zona está
    //     abierta; no se guarda nada: se vuelve a calcular. Se recalcula al abrir una zona, al cambiar de temporada, al cargar la
    //     partida, al volver a la app y, jugando, cada tanto por si pasó la medianoche.
    //   - El extra lo cobra PriceCalculator (FinalPrice con el id del cultivo): venta, cosecha rara y pedidos. Acá solo se le avisa cuál es.
    //   - El reloj es el local del dispositivo (se puede adelantar a mano: es un bonus de retorno, no una economía de pago).
    //   - Environment (el cartel de la plaza y el ícono sobre el campo) escucha Changed; este servicio no lo conoce.
    public sealed class DailyCropManager : MonoBehaviour
    {
        public static DailyCropManager Instance { get; private set; }

        private const float CheckSeconds = 20f;

        private GameConfig _config;
        private ProgressionManager _progression;
        private PrestigeManager _prestige;
        private string _cropId = "";
        private float _nextCheck;
        private int _day;

        // Para pruebas: el reloj.
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        // El cultivo de hoy ("" si no hay: sin zonas abiertas o el extra apagado).
        public string CropId => _cropId;
        public CropData Crop => _config != null && _cropId.Length > 0 ? _config.FindCrop(_cropId) : null;
        public float Bonus => _config != null ? _config.DailyCropBonus : 0f;
        public bool IsActive => _cropId.Length > 0;

        public bool IsDaily(CropData crop) => crop != null && _cropId.Length > 0 && string.Equals(crop.Id, _cropId, StringComparison.Ordinal);

        // Cambió el cultivo del día (día nuevo, zona abierta, partida cargada).
        public event Action Changed;

        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Instance = this;

            _progression = ProgressionManager.Instance;
            if (_progression != null) _progression.Changed += OnProgressChanged;

            _prestige = PrestigeManager.Instance;
            if (_prestige != null) _prestige.Changed += Refresh;

            Refresh();
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked || change.Kind == ProgressChangeKind.Restored) Refresh();
        }

        private void Update()
        {
            if (_config == null || Time.unscaledTime < _nextCheck) return;

            _nextCheck = Time.unscaledTime + CheckSeconds;
            if (MissionRules.DayKey(Clock()) != _day) Refresh();   // solo si pasó la medianoche: el sorteo asigna memoria
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus && _config != null) Refresh();
        }

        // Vuelve a calcular el cultivo de hoy. Si no cambió, no hace nada (ni asigna memoria más que el sorteo).
        public void Refresh()
        {
            if (_config == null) return;

            _day = MissionRules.DayKey(Clock());
            string id = null;
            if (_config.DailyCropBonus > 0f) id = DailyCropRules.Pick(CandidateIds(), IsOpen, _day);

            id = id ?? "";
            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Pricing.SetDailyCrop(id, _config.DailyCropBonus);

            if (id == _cropId) return;

            _cropId = id;
            Changed?.Invoke();
        }

        private List<string> CandidateIds()
        {
            var rules = _config.OrderCrops;
            var ids = new List<string>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && _config.FindCrop(rules[i].CropId) != null) ids.Add(rules[i].CropId);
            }

            return ids;
        }

        // Un cultivo está abierto si la zona donde se planta lo está (zoneId vacío = la granja inicial, siempre).
        private bool IsOpen(string cropId)
        {
            var rules = _config.OrderCrops;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].CropId == cropId) return IsZoneOpen(rules[i].ZoneId);
            }

            return false;
        }

        private bool IsZoneOpen(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return true;
            if (_progression == null) return false;

            var zones = _config.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].Id == zoneId) return _progression.IsZoneUnlocked(zones[i]);
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_progression != null) _progression.Changed -= OnProgressChanged;
            if (_prestige != null) _prestige.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }
    }
}
