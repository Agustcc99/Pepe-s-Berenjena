using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // Qué cambió en el progreso (ver PlayerProgress.Changed).
    public enum ProgressChangeKind
    {
        ZoneUnlocked,         // se abrió una zona
        UpgradeLevelChanged,  // una mejora subió (o se restauró) de nivel
        PaymentChanged,       // cambió lo pagado hasta ahora de una compra que todavía no se completó
        XpChanged,            // la experiencia subió (con cada venta); el nivel se deduce de ella
        PerkChosen,           // se gastó un punto en un perk
        Restored              // se cargó un progreso entero (partida guardada): hay que releer todo
    }

    public readonly struct ProgressChange
    {
        public readonly ProgressChangeKind Kind;
        public readonly string Id;   // id del ZoneData / UpgradeData (vacío en Restored)

        public ProgressChange(ProgressChangeKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }
    }

    // Lo que la UI y el gameplay pueden hacer con el progreso: mirarlo y escuchar sus cambios. Los cambios los hace
    // ProgressionManager (que cobra por la economía antes de tocar nada).
    public interface IReadOnlyPlayerProgress
    {
        bool IsZoneUnlocked(string zoneId);
        int LevelOf(string upgradeId);
        long PaidOf(string purchaseKey);
        int UnlockedZoneCount { get; }

        // Experiencia total ganada y veces que se eligió cada perk (los puntos gastados). El nivel y los puntos libres los
        // calcula ProgressionManager con la curva de niveles.
        long Xp { get; }
        int PerkPicksOf(string perkId);
        int TotalPerkPicks { get; }
        event Action<ProgressChange> Changed;
    }

    // El progreso del jugador: qué zonas están abiertas, qué nivel tiene cada mejora y cuánto lleva pagado de la compra
    // en curso de cada pad. Es el estado que cambia; los ScriptableObjects (ZoneData, UpgradeData) son solo la tabla y se
    // referencian por Id.
    // Es C# puro, sin UnityEngine: se prueba sin escena. Pensado para la Fase 7: ToData / FromData lo convierten a un
    // ProgressData simple (arrays de strings y números) que el SaveManager puede serializar tal cual, y no hay nada
    // que "rehacer" para guardarlo.
    public sealed class PlayerProgress : IReadOnlyPlayerProgress
    {
        // Las claves de pago distinguen zonas y mejoras que podrían compartir id.
        public static string ZoneKey(string zoneId) { return "zone:" + zoneId; }
        public static string UpgradeKey(string upgradeId) { return "upgrade:" + upgradeId; }

        private readonly HashSet<string> _zones = new HashSet<string>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        private readonly Dictionary<string, long> _paid = new Dictionary<string, long>();
        private readonly Dictionary<string, int> _perkPicks = new Dictionary<string, int>();

        public int UnlockedZoneCount => _zones.Count;
        public long Xp { get; private set; }
        public int TotalPerkPicks { get; private set; }

        public event Action<ProgressChange> Changed;

        public bool IsZoneUnlocked(string zoneId)
        {
            return zoneId != null && _zones.Contains(zoneId);
        }

        public int LevelOf(string upgradeId)
        {
            return upgradeId != null && _levels.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        public long PaidOf(string purchaseKey)
        {
            return purchaseKey != null && _paid.TryGetValue(purchaseKey, out long paid) ? paid : 0L;
        }

        public int PerkPicksOf(string perkId)
        {
            return perkId != null && _perkPicks.TryGetValue(perkId, out int picks) ? picks : 0;
        }

        // Abre la zona. Devuelve false (sin avisar) si ya estaba abierta.
        public bool UnlockZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) throw new ArgumentException("La zona necesita un id.", nameof(zoneId));
            if (!_zones.Add(zoneId)) return false;

            Changed?.Invoke(new ProgressChange(ProgressChangeKind.ZoneUnlocked, zoneId));
            return true;
        }

        // Fija el nivel de una mejora. Devuelve false (sin avisar) si ya tenía ese nivel.
        public bool SetLevel(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId)) throw new ArgumentException("La mejora necesita un id.", nameof(upgradeId));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level), "El nivel no puede ser negativo.");
            if (LevelOf(upgradeId) == level) return false;

            if (level == 0) _levels.Remove(upgradeId);
            else _levels[upgradeId] = level;

            Changed?.Invoke(new ProgressChange(ProgressChangeKind.UpgradeLevelChanged, upgradeId));
            return true;
        }

        // Suma lo pagado de una compra. 'purchaseKey' sale de ZoneKey / UpgradeKey.
        public void AddPaid(string purchaseKey, long coins)
        {
            if (string.IsNullOrEmpty(purchaseKey)) throw new ArgumentException("La compra necesita una clave.", nameof(purchaseKey));
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins), "No se puede pagar un monto negativo.");
            if (coins == 0) return;

            _paid[purchaseKey] = PaidOf(purchaseKey) + coins;
            Changed?.Invoke(new ProgressChange(ProgressChangeKind.PaymentChanged, purchaseKey));
        }

        // Borra lo pagado (la compra se completó). No avisa si no había nada.
        public void ClearPaid(string purchaseKey)
        {
            if (purchaseKey == null || !_paid.Remove(purchaseKey)) return;

            Changed?.Invoke(new ProgressChange(ProgressChangeKind.PaymentChanged, purchaseKey));
        }

        // Suma experiencia. Los montos de 0 o negativos se ignoran (la XP solo sube).
        public void AddXp(long amount)
        {
            if (amount <= 0L) return;

            Xp = amount > long.MaxValue - Xp ? long.MaxValue : Xp + amount;
            Changed?.Invoke(new ProgressChange(ProgressChangeKind.XpChanged, null));
        }

        // Anota una elección de perk (un punto gastado). Quién puede elegir y hasta cuántas veces lo decide
        // ProgressionManager; acá solo se cuenta.
        public void AddPerkPick(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) throw new ArgumentException("El perk necesita un id.", nameof(perkId));

            _perkPicks[perkId] = PerkPicksOf(perkId) + 1;
            TotalPerkPicks++;
            Changed?.Invoke(new ProgressChange(ProgressChangeKind.PerkChosen, perkId));
        }

        // ---------- Para guardar (Fase 7) ----------

        public ProgressData ToData()
        {
            var data = new ProgressData { version = ProgressData.CurrentVersion };

            data.unlockedZones = new string[_zones.Count];
            _zones.CopyTo(data.unlockedZones);
            Array.Sort(data.unlockedZones, StringComparer.Ordinal);   // mismo orden siempre: el archivo no cambia sin motivo

            var levelIds = new List<string>(_levels.Keys);
            levelIds.Sort(StringComparer.Ordinal);
            data.upgradeLevels = new UpgradeLevelEntry[levelIds.Count];
            for (int i = 0; i < levelIds.Count; i++)
            {
                data.upgradeLevels[i] = new UpgradeLevelEntry { id = levelIds[i], level = _levels[levelIds[i]] };
            }

            var paidKeys = new List<string>(_paid.Keys);
            paidKeys.Sort(StringComparer.Ordinal);
            data.payments = new PaymentEntry[paidKeys.Count];
            for (int i = 0; i < paidKeys.Count; i++)
            {
                data.payments[i] = new PaymentEntry { key = paidKeys[i], coins = _paid[paidKeys[i]] };
            }

            data.xp = Xp;

            var perkIds = new List<string>(_perkPicks.Keys);
            perkIds.Sort(StringComparer.Ordinal);
            data.perkPicks = new PerkPickEntry[perkIds.Count];
            for (int i = 0; i < perkIds.Count; i++)
            {
                data.perkPicks[i] = new PerkPickEntry { id = perkIds[i], picks = _perkPicks[perkIds[i]] };
            }

            return data;
        }

        // Reemplaza TODO el estado por el de 'data' y avisa una sola vez (Restored). Es tolerante: ignora ids vacíos,
        // niveles o montos negativos y listas nulas, para que un archivo dañado no rompa el arranque.
        public void LoadFrom(ProgressData data)
        {
            _zones.Clear();
            _levels.Clear();
            _paid.Clear();
            _perkPicks.Clear();
            Xp = 0L;
            TotalPerkPicks = 0;

            if (data != null)
            {
                if (data.unlockedZones != null)
                {
                    foreach (string id in data.unlockedZones)
                    {
                        if (!string.IsNullOrEmpty(id)) _zones.Add(id);
                    }
                }

                if (data.upgradeLevels != null)
                {
                    foreach (UpgradeLevelEntry entry in data.upgradeLevels)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.level > 0) _levels[entry.id] = entry.level;
                    }
                }

                if (data.payments != null)
                {
                    foreach (PaymentEntry entry in data.payments)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.key) && entry.coins > 0) _paid[entry.key] = entry.coins;
                    }
                }

                Xp = Math.Max(0L, data.xp);

                if (data.perkPicks != null)
                {
                    foreach (PerkPickEntry entry in data.perkPicks)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.id) || entry.picks <= 0) continue;

                        _perkPicks[entry.id] = entry.picks;
                        TotalPerkPicks += entry.picks;
                    }
                }
            }

            Changed?.Invoke(new ProgressChange(ProgressChangeKind.Restored, null));
        }
    }

    // El progreso en un formato simple para guardar (JsonUtility lo entiende tal cual: arrays de clases serializables).
    // 'version' permite migrar el formato cuando cambie.
    [Serializable]
    public sealed class ProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string[] unlockedZones = new string[0];
        public UpgradeLevelEntry[] upgradeLevels = new UpgradeLevelEntry[0];
        public PaymentEntry[] payments = new PaymentEntry[0];
        public long xp;                                                   // experiencia total (el nivel se deduce de ella)
        public PerkPickEntry[] perkPicks = new PerkPickEntry[0];          // veces que se eligió cada perk
    }

    [Serializable]
    public sealed class UpgradeLevelEntry
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class PerkPickEntry
    {
        public string id;
        public int picks;
    }

    [Serializable]
    public sealed class PaymentEntry
    {
        public string key;
        public long coins;
    }
}
