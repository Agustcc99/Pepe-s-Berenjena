using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de la progresión: tiene el PlayerProgress (zonas abiertas, nivel de cada mejora, lo pagado de cada compra en
    // curso) y es la única puerta para comprar. Mismo esquema que EconomyManager con la billetera: la UI y el gameplay lo
    // ven como IReadOnlyPlayerProgress (estado + evento) y piden las compras acá.
    //   - Comprar es una transacción autoritativa: TryPay saca las monedas por EconomyManager.TrySpend y recién
    //     entonces suma lo pagado; cuando se completa el costo, abre la zona o sube el nivel de la mejora. Un pad
    //     solo ofrece monedas (a su ritmo); lo cosmético (la cuenta que baja, el campo que aparece) viene después.
    //   - Cada efecto se aplica en la capa que corresponde, no a las patadas: el precio en PriceCalculator, la capacidad
    //     en BackpackManager, y la velocidad de movimiento y la de crecimiento las leen el jugador y los campos del
    //     Modifiers (con el evento ModifiersChanged).
    //   - Solo se compra en Playing: en pausa o con un anuncio en pantalla TryPay no cobra nada (además de que los
    //     pads no reciben al jugador en esos estados).
    //   - Niveles y perks (Fase 7): cada venta suma al progreso la XP del precio BASE del producto (EconomyManager.ProductSold).
    //     El nivel se deduce de la XP con la LevelCurve (no se guarda) y cada nivel que se sube da un punto; el punto se gasta
    //     en un perk (TryChoosePerk: Fuerza = +1 lugar en la mochila, Velocidad = un poquito más rápido) parado en su pad.
    //   - Partida guardada: es el ISaveable "progress" (zonas, niveles de mejoras, pagos a medias, XP y perks elegidos). Restaurar
    //     no dispara aperturas ni avisos de nivel: los pads y las zonas leen el estado y aparecen como estaban.
    public sealed class ProgressionManager : MonoBehaviour, ISaveable
    {
        public static ProgressionManager Instance { get; private set; }

        private GameConfig _config;
        private PlayerProgress _progress;
        private LevelCurve _curve;
        private EconomyManager _economy;
        private ProgressionModifiers _modifiers = ProgressionModifiers.None;

        // Vista de solo lectura del progreso.
        public IReadOnlyPlayerProgress Progress => _progress;

        // El efecto actual de las mejoras (lo leen el jugador y los campos).
        public ProgressionModifiers Modifiers => _modifiers;

        // Las tablas de diseño (las mismas del GameConfig), para quien necesite recorrerlas (el panel de debug).
        public IReadOnlyList<ZoneData> Zones => _config.Zones;
        public IReadOnlyList<UpgradeData> Upgrades => _config.Upgrades;
        public IReadOnlyList<PerkData> Perks => _config.Perks;

        // Se dispara después de cada cambio del progreso (zona abierta, nivel nuevo, pago parcial, progreso restaurado),
        // cuando los efectos ya están aplicados.
        public event Action<ProgressChange> Changed;

        // Se dispara cuando cambia el efecto total de las mejoras (velocidad, crecimiento...).
        public event Action ModifiersChanged;

        // Se dispara al subir de nivel jugando (con el nivel nuevo), después de Changed. Nunca al restaurar una partida.
        public event Action<int> LevelChanged;

        // Un pad de compra o de perk avisa que el jugador se quedó parado y no se puede (sin monedas, sin puntos). Lo escucha el audio
        // ("no se puede"); el pad lo avisa una sola vez por visita.
        public event Action Refused;

        public void ReportRefused()
        {
            Refused?.Invoke();
        }

        // Lo llama el Bootstrapper, después de la economía y la mochila; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _curve = new LevelCurve(config.LevelBaseXp, config.MaxLevel);
            _progress = new PlayerProgress();
            _progress.Changed += OnProgressChanged;

            // La XP sale de las ventas: se escucha a la economía (que ya existe, el Bootstrapper la crea antes).
            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.ProductSold += OnProductSold;

            Instance = this;
            ApplyEffects();
        }

        // ---------- Consultas ----------

        public bool IsZoneUnlocked(ZoneData zone)
        {
            return zone != null && _progress.IsZoneUnlocked(zone.Id);
        }

        public int LevelOf(UpgradeData upgrade)
        {
            return upgrade != null ? _progress.LevelOf(upgrade.Id) : 0;
        }

        public bool IsMaxed(UpgradeData upgrade)
        {
            return upgrade != null && LevelOf(upgrade) >= upgrade.MaxLevel;
        }

        // Costo de la compra en curso: abrir la zona, o el próximo nivel de la mejora (0 si ya está al máximo).
        public long CostOf(ZoneData zone)
        {
            return zone != null ? zone.UnlockCost : 0L;
        }

        public long CostOfNextLevel(UpgradeData upgrade)
        {
            return upgrade != null && !IsMaxed(upgrade) ? upgrade.CostOfLevel(LevelOf(upgrade) + 1) : 0L;
        }

        // Cuánto se pagó ya de esa compra (se conserva si el jugador se va del pad).
        public long PaidFor(ZoneData zone)
        {
            return zone != null ? _progress.PaidOf(PlayerProgress.ZoneKey(zone.Id)) : 0L;
        }

        public long PaidFor(UpgradeData upgrade)
        {
            return upgrade != null ? _progress.PaidOf(PlayerProgress.UpgradeKey(upgrade.Id)) : 0L;
        }

        public int UnlockedZoneCount
        {
            get
            {
                int count = 0;
                IReadOnlyList<ZoneData> zones = _config.Zones;
                for (int i = 0; i < zones.Count; i++)
                {
                    if (zones[i] != null && _progress.IsZoneUnlocked(zones[i].Id)) count++;
                }

                return count;
            }
        }

        // true si ya se abrieron todas las zonas y todas las mejoras están al máximo: el contenido de la temporada se agotó y
        // se puede cerrar con una nueva (F-03). Los perks no cuentan: dependen del nivel, no de las monedas.
        public bool IsEverythingBought
        {
            get
            {
                IReadOnlyList<ZoneData> zones = _config.Zones;
                for (int i = 0; i < zones.Count; i++)
                {
                    if (zones[i] != null && !_progress.IsZoneUnlocked(zones[i].Id)) return false;
                }

                IReadOnlyList<UpgradeData> upgrades = _config.Upgrades;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    if (upgrades[i] != null && !IsMaxed(upgrades[i])) return false;
                }

                return true;
            }
        }

        // Nueva temporada (F-03): cierra las zonas, baja las mejoras a 0 y borra lo pagado a medias. Se conservan la XP (el nivel)
        // y los perks elegidos. Avisa como una restauración (los pads y las zonas releen todo, sin efectos) y la mochila se achica.
        public void ResetSeason()
        {
            ProgressData data = _progress.ToData();
            data.unlockedZones = new string[0];
            data.upgradeLevels = new UpgradeLevelEntry[0];
            data.payments = new PaymentEntry[0];
            _progress.LoadFrom(data);
        }

        // ---------- Niveles y perks ----------

        public long Xp => _progress.Xp;
        public int Level => _curve.LevelForXp(_progress.Xp);
        public int MaxLevel => _curve.MaxLevel;
        public bool IsMaxLevel => Level >= _curve.MaxLevel;

        // Cuánta XP lleva dentro del nivel actual y cuánta pide el siguiente (en el nivel máximo, 0 y 0).
        public void GetLevelProgress(out long intoLevel, out long neededForNext)
        {
            _curve.Progress(_progress.Xp, out intoLevel, out neededForNext);
        }

        // Puntos ganados (uno por cada nivel sobre el 1), gastados (perks elegidos que existen en la tabla) y libres.
        public int PerkPointsEarned => Level - 1;

        public int PerkPointsSpent
        {
            get
            {
                int spent = 0;
                IReadOnlyList<PerkData> perks = _config.Perks;
                for (int i = 0; i < perks.Count; i++)
                {
                    if (perks[i] != null) spent += Math.Min(_progress.PerkPicksOf(perks[i].Id), perks[i].MaxPicks);
                }

                return spent;
            }
        }

        public int PerkPoints => Math.Max(0, PerkPointsEarned - PerkPointsSpent);

        public int PicksOf(PerkData perk)
        {
            return perk != null ? _progress.PerkPicksOf(perk.Id) : 0;
        }

        public bool IsMaxed(PerkData perk)
        {
            return perk != null && PicksOf(perk) >= perk.MaxPicks;
        }

        // true si hay un punto libre y el perk todavía no se agotó (sin mirar si el juego está en Playing).
        public bool CanChoose(PerkData perk)
        {
            return perk != null && !IsMaxed(perk) && PerkPoints > 0;
        }

        // Gasta UN punto en el perk. Como las compras, solo en Playing: en pausa o con un anuncio no hace nada. Devuelve true
        // si se eligió.
        public bool TryChoosePerk(PerkData perk)
        {
            if (perk == null) throw new ArgumentNullException(nameof(perk));

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return false;
            if (!CanChoose(perk)) return false;

            _progress.AddPerkPick(perk.Id);   // avisa PerkChosen; los efectos se aplican en OnProgressChanged
            return true;
        }

        private void OnProductSold(CropData crop, long coins)
        {
            if (crop == null || crop.SellPrice <= 0) return;

            int before = Level;
            _progress.AddXp(System.Math.Max(1L, (long)System.Math.Round(crop.SellPrice * (double)PrestigeManager.XpFactor)));   // F-11: x1 sin mejora

            int after = Level;
            if (after > before) LevelChanged?.Invoke(after);
        }

        // ---------- Compras ----------

        // Ofrece hasta 'coins' monedas para abrir la zona. Cobra por la economía lo que haga falta (nunca más que lo que
        // falta ni más que el saldo) y devuelve cuántas monedas se gastaron de verdad. Con eso completa el costo,
        // la zona queda abierta en ese mismo instante.
        public long TryPay(ZoneData zone, long coins)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (_progress.IsZoneUnlocked(zone.Id)) return 0L;

            string id = zone.Id;
            return Contribute(PlayerProgress.ZoneKey(id), zone.UnlockCost, coins, () => _progress.UnlockZone(id));
        }

        // Lo mismo para el próximo nivel de una mejora. Al completarse sube un nivel y aplica el efecto.
        public long TryPay(UpgradeData upgrade, long coins)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));
            if (IsMaxed(upgrade)) return 0L;

            string id = upgrade.Id;
            int nextLevel = _progress.LevelOf(id) + 1;
            return Contribute(PlayerProgress.UpgradeKey(id), upgrade.CostOfLevel(nextLevel), coins, () => _progress.SetLevel(id, nextLevel));
        }

        private long Contribute(string key, long cost, long coins, Action complete)
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return 0L;   // pausa, anuncio o menú: no se compra

            long remaining = cost - _progress.PaidOf(key);
            if (remaining <= 0L)
            {
                // Ya estaba todo pago (por ejemplo, una compra gratis): solo falta completarla.
                _progress.ClearPaid(key);
                complete();
                return 0L;
            }

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || coins <= 0L) return 0L;

            long amount = Math.Min(coins, remaining);
            if (!economy.TrySpend(amount)) return 0L;

            if (amount >= remaining)
            {
                _progress.ClearPaid(key);
                complete();
            }
            else
            {
                _progress.AddPaid(key, amount);
            }

            return amount;
        }

        // ---------- Partida guardada ----------

        // Una copia del progreso en un formato simple, lista para serializar...
        public ProgressData CreateSnapshot()
        {
            return _progress.ToData();
        }

        // ...y la vuelta: reemplaza el progreso y reaplica los efectos (la mochila también puede achicarse: es una restauración).
        public void Restore(ProgressData data)
        {
            _progress.LoadFrom(data);
        }

        public string SaveId => "progress";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(CreateSnapshot());
        }

        // Con null o vacío vuelve al progreso de una partida nueva. Un JSON ilegible se toma igual como partida nueva.
        public void Restore(string json)
        {
            ProgressData data = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    data = JsonUtility.FromJson<ProgressData>(json);
                }
                catch (ArgumentException)
                {
                    data = null;
                }
            }

            Restore(data);
        }

        // ---------- Efectos ----------

        private void OnProgressChanged(ProgressChange change)
        {
            // Los efectos primero: quien reaccione al evento ya ve la mochila, el precio y los multiplicadores nuevos. Lo pagado y la XP
            // no cambian ningún efecto (el nivel solo da puntos; el efecto llega al elegir un perk).
            if (change.Kind != ProgressChangeKind.PaymentChanged && change.Kind != ProgressChangeKind.XpChanged)
            {
                ApplyEffects(allowShrink: change.Kind == ProgressChangeKind.Restored);
            }

            Changed?.Invoke(change);
            SaveRequested?.Invoke();
        }

        private void ApplyEffects(bool allowShrink = false)
        {
            ProgressionModifiers next = ProgressionModifiers.Compute(_progress, _config.Upgrades, _config.Perks, _config.MaxMoveSpeedMultiplier);
            bool changed = !SameModifiers(_modifiers, next);
            _modifiers = next;

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Pricing.SetUpgradeMultiplier(next.SellPrice);

            BackpackManager backpack = BackpackManager.Instance;
            if (backpack != null) backpack.SetCapacityBonus(next.BackpackBonus, allowShrink);

            if (changed) ModifiersChanged?.Invoke();
        }

        private static bool SameModifiers(ProgressionModifiers a, ProgressionModifiers b)
        {
            return a.BackpackBonus == b.BackpackBonus && a.MoveSpeed == b.MoveSpeed && a.SellPrice == b.SellPrice && a.GrowthSpeed == b.GrowthSpeed
                && a.OfflineCapBonusSeconds == b.OfflineCapBonusSeconds;
        }

        private void OnDestroy()
        {
            if (_progress != null) _progress.Changed -= OnProgressChanged;
            if (_economy != null) _economy.ProductSold -= OnProductSold;
            if (Instance == this) Instance = null;
        }
    }
}
