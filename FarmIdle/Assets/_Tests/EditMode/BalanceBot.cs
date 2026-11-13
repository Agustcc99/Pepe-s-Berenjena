using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El "bot de balance": un jugador perfecto y determinista que juega la economía entera sobre los DATOS reales del juego
    // (GameConfig: cultivos, zonas y mejoras) y anota cuándo compra cada cosa. No usa escenas ni servicios: es una simulación
    // por eventos (cada acción dura lo que tarda en caminar y esperar), así que 15 minutos de juego se calculan en milisegundos.
    //
    // Reglas que reproduce (todas salen del juego real):
    //   - Pisar una parcela lista la cosecha y, en el mismo paso, vuelve a sembrarla; pisar una vacía la siembra.
    //   - La mochila tiene capacidad base + lo que dan las mejoras de capacidad; con la mochila llena la parcela queda lista.
    //   - Vender: una unidad cada GameConfig.SellIntervalSeconds, al precio base x mejoras (redondeado como PriceCalculator).
    //   - Mejoras: velocidad (camina más rápido), crecimiento (las plantas crecen más rápido), precio y capacidad.
    //   - Compras: parado en el pad, una espera corta y el pago "gotea" en PurchaseSeconds. El bot siempre apunta a lo MÁS BARATO
    //     que todavía puede comprar (zonas y niveles de mejora) y va cuando le alcanzan las monedas.
    //   - Fertilizante: un video cada 150 s (el stock se acumula hasta el tope) y se usa cuando muchas plantas están creciendo.
    //
    // Lo que NO modela (hace al bot un poco más lento que un jugador voraz con todo): perks de nivel, el Turbo por video y
    // el fertilizante comprado con monedas.
    //
    // Mantener sincronizado con el mundo (Phase3SceneBuilder / PlazaBuilder / Phase8SceneBuilder): posiciones de los campos,
    // cultivos de cada fila, pad de venta, plaza de mejoras y fila del fertilizante.
    internal sealed class BalanceBot
    {
        // ---------- El mundo (ver el comentario de arriba) ----------

        private const float BaseSpeed = 5f;                       // PlayerMovement.moveSpeed
        private const float PlotPitch = 2.4f;                     // Phase3SceneBuilder.PlotPitch
        private static readonly Vector2 SellPad = new Vector2(0f, -3.5f);
        private const float FertilizerZ = -13.9f;                // fila de los sectores Videos y Fertilizante: PlazaBuilder.VideoRowZ
        private static readonly Vector2 VideoPad = new Vector2(-3.1f, FertilizerZ);
        private static readonly Vector2 UsePad = new Vector2(3.1f, FertilizerZ);
        private const float DwellSeconds = 0.6f;                  // DwellPad.dwellSeconds

        // Qué ofrece cada ranura de mejora (id -> posición del pad en la plaza, ProgressionData y PlazaBuilder) y cada zona (id -> campo y portón).
        private static readonly Dictionary<string, Vector2> UpgradePad = new Dictionary<string, Vector2>
        {
            { "upgrade_growth", new Vector2(-6.7f, -20.6f) }, { "upgrade_price", new Vector2(-3.1f, -20.6f) }, { "upgrade_speed", new Vector2(-6.7f, -24.4f) },
            { "upgrade_capacity", new Vector2(-3.1f, -24.4f) }, { "upgrade_offline", new Vector2(3.1f, -25f) }
        };

        private sealed class FieldSpec
        {
            public string ZoneId;          // null = el campo inicial, abierto desde el principio
            public string RequiresZoneId;  // el portón queda dentro de esa zona: hay que tenerla abierta para llegar (null = ninguna)
            public Vector2 Center, Gate;
            public string[] Rows;          // id del cultivo de cada fila (3 parcelas por fila)
        }

        private static readonly FieldSpec[] Fields =
        {
            new FieldSpec { ZoneId = null, Center = new Vector2(0f, 7f), Gate = new Vector2(0f, 3f), Rows = new[] { "wheat", "carrot", "tomato" } },
            new FieldSpec { ZoneId = "zone_east", Center = new Vector2(11f, 7f), Gate = new Vector2(5.5f, 7f), Rows = new[] { "carrot", "tomato", "tomato" } },
            new FieldSpec { ZoneId = "zone_west", Center = new Vector2(-11f, 7f), Gate = new Vector2(-5.5f, 7f), Rows = new[] { "pumpkin", "pumpkin", "pumpkin" } },
            new FieldSpec { ZoneId = "zone_north", Center = new Vector2(0f, 19f), Gate = new Vector2(0f, 13f), Rows = new[] { "corn", "corn", "eggplant" } },
            new FieldSpec { ZoneId = "zone_northwest", RequiresZoneId = "zone_north", Center = new Vector2(-11f, 19f), Gate = new Vector2(-5.5f, 19f), Rows = new[] { "eggplant", "watermelon", "watermelon" } },
            new FieldSpec { ZoneId = "zone_northeast", RequiresZoneId = "zone_north", Center = new Vector2(11f, 19f), Gate = new Vector2(5.5f, 19f), Rows = new[] { "watermelon", "sunflower", "sunflower" } },
        };

        // Las zonas que existían antes de F-02: la economía original (10 a 16 minutos) se vigila solo con ellas.
        public static readonly string[] OriginalZoneIds = { "zone_east", "zone_west" };

        // ---------- Ajustes del bot ----------

        public float FertilizerVideoEverySeconds = 150f;
        public float FertilizerUseWhenGrowingFraction = 0.6f;
        public float MaxSeconds = 1800f;
        public float DecisionSeconds = 0.1f;                      // lo que tarda un jugador en "decidir" hacia dónde ir
        public float IdleSecondsToUseFertilizer = 4f;             // nada para cosechar en tantos segundos = ocioso

        // ---------- Resultado ----------

        public sealed class Purchase
        {
            public string Id;             // id de la zona o de la mejora
            public string Name;
            public float AtSeconds;
            public float SincePrevious;
            public long Cost;
        }

        public readonly List<Purchase> Purchases = new List<Purchase>();

        // LevelSeconds[k] = segundo en que se alcanzó el nivel k + 2 (la XP sale de los precios base vendidos, como en el juego).
        public readonly List<float> LevelSeconds = new List<float>();

        // Una compra del plan de calibración: qué es, cuándo tendría que pasar y (al terminar) cuánto tiene que costar para que pase.
        public sealed class CalibrationStep
        {
            public string ZoneId;          // zona a abrir, o null si es una mejora
            public string UpgradeId;       // mejora cuyo próximo nivel se compra
            public float AtSeconds;
            public long Cost;
            public long EarnedUntilHere;   // monedas ganadas en total hasta esta compra (para ver cuándo se satura el ingreso)
            public long XpUntilHere;       // XP base acumulada hasta esta compra (para elegir la curva de niveles)
        }

        public float TotalPurchaseSeconds;                       // cuando se compró lo último (0 si no se compró todo)
        public int TotalPurchasesNeeded;
        public int FertilizersUsed;
        public long CoinsEarned;
        public long BaseXp;                                       // suma de los precios BASE vendidos (la XP del juego)
        public float WalkSeconds;
        public float WaitSeconds;

        // Hitos de la guía de los primeros minutos (U-08), en segundos del bot; -1 = todavía no pasó.
        public float ThirdSowSeconds = -1f, FirstHarvestSeconds = -1f, ThirdHarvestSeconds = -1f, FirstSaleSeconds = -1f;
        public long CoinsAfterFirstSale;
        private int _harvests, _sown;
        private bool _sowDone;

        // ---------- Estado ----------

        private sealed class Plot
        {
            public CropData Crop;
            public Vector2 Pos;
            public FieldSpec Field;
            public int State;             // 0 vacía, 1 creciendo, 2 lista
            public float Elapsed;
        }

        private readonly GameConfig _config;
        private readonly IReadOnlyCollection<string> _zoneIds;
        private readonly List<Plot> _plots = new List<Plot>();
        private readonly Dictionary<string, int> _upgradeLevel = new Dictionary<string, int>();
        private readonly HashSet<string> _openZones = new HashSet<string>();
        private readonly List<CropData> _backpack = new List<CropData>();
        private Vector2 _pos;
        private float _time;
        private long _coins;
        private int _fertilizer;
        private float _nextVideoAt;
        private float _lastPurchaseAt;
        private LevelCurve _curve;
        private int _level = 1;
        private bool _calibrating;
        private string _targetId;          // id de lo que NextTarget eligió por última vez

        // 'zoneIds' limita qué zonas del GameConfig juega el bot (null = todas); las demás ni se compran ni cuentan.
        public BalanceBot(GameConfig config, IReadOnlyCollection<string> zoneIds = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _zoneIds = zoneIds;

            foreach (FieldSpec field in Fields)
            {
                for (int row = 0; row < field.Rows.Length; row++)
                {
                    CropData crop = config.FindCrop(field.Rows[row]);
                    if (crop == null) throw new InvalidOperationException("El bot de balance no encuentra el cultivo '" + field.Rows[row] + "' en el GameConfig.");

                    for (int column = 0; column < 3; column++)
                    {
                        _plots.Add(new Plot
                        {
                            Crop = crop,
                            Field = field,
                            Pos = field.Center + new Vector2((column - 1) * PlotPitch, (row - 1) * PlotPitch)
                        });
                    }
                }
            }

            foreach (UpgradeData upgrade in config.Upgrades) _upgradeLevel[upgrade.Id] = 0;
            foreach (ZoneData zone in config.Zones)
            {
                if (IsPlayed(zone)) TotalPurchasesNeeded++;
            }

            foreach (UpgradeData upgrade in config.Upgrades) TotalPurchasesNeeded += upgrade.MaxLevel;

            _curve = new LevelCurve(config.LevelBaseXp, config.MaxLevel);
            _pos = Vector2.zero;
            _coins = config.StartingCoins;
            _nextVideoAt = FertilizerVideoEverySeconds;
        }

        // ---------- Efectos de las mejoras ----------

        private float Multiplier(UpgradeKind kind)
        {
            float value = 1f;
            foreach (UpgradeData upgrade in _config.Upgrades)
            {
                if (upgrade.Kind == kind) value *= upgrade.ValueAtLevel(_upgradeLevel[upgrade.Id]);
            }

            return value;
        }

        private float Speed => BaseSpeed * Mathf.Min(Multiplier(UpgradeKind.MoveSpeed), _config.MaxMoveSpeedMultiplier);
        private float Growth => Multiplier(UpgradeKind.GrowthSpeed);

        private int Capacity
        {
            get
            {
                int capacity = _config.BackpackCapacity;
                foreach (UpgradeData upgrade in _config.Upgrades)
                {
                    if (upgrade.Kind == UpgradeKind.BackpackCapacity) capacity += Mathf.RoundToInt(upgrade.ValueAtLevel(_upgradeLevel[upgrade.Id]));
                }

                return capacity;
            }
        }

        private bool IsOpen(Plot plot)
        {
            return plot.Field.ZoneId == null || _openZones.Contains(plot.Field.ZoneId);
        }

        private bool IsPlayed(ZoneData zone)
        {
            if (_zoneIds == null) return true;

            foreach (string id in _zoneIds)
            {
                if (id == zone.Id) return true;
            }

            return false;
        }

        // El portón de una zona se puede pisar si la zona donde está ya está abierta.
        private bool IsReachable(FieldSpec field)
        {
            return field == null || field.RequiresZoneId == null || _openZones.Contains(field.RequiresZoneId);
        }

        // ---------- Tiempo ----------

        private void Advance(float seconds)
        {
            if (!(seconds > 0f)) return;

            _time += seconds;
            float growth = seconds * Growth;
            foreach (Plot plot in _plots)
            {
                if (plot.State != 1 || !IsOpen(plot)) continue;

                plot.Elapsed += growth;
                if (plot.Elapsed >= plot.Crop.GrowSeconds)
                {
                    plot.Elapsed = plot.Crop.GrowSeconds;
                    plot.State = 2;
                }
            }
        }

        private void WalkTo(Vector2 target)
        {
            float seconds = Vector2.Distance(_pos, target) / Speed;
            WalkSeconds += seconds;
            Advance(seconds);
            _pos = target;
        }

        // ---------- La partida ----------

        public void Run()
        {
            int guard = 0;
            while (_time < MaxSeconds && Purchases.Count < TotalPurchasesNeeded && guard++ < 5000000)
            {
                Advance(DecisionSeconds);

                if (TryBuyNext()) continue;
                if (_backpack.Count >= Capacity || ShouldSellNow()) { Sell(); continue; }
                if (TryFertilizer()) continue;

                VisitBestPlot();
            }

            if (Purchases.Count >= TotalPurchasesNeeded) TotalPurchaseSeconds = _time;
        }

        // Lo más barato que todavía se puede comprar: (nombre, costo, dónde está el pad, cómo se aplica).
        private bool NextTarget(out string name, out long cost, out Vector2 pad, out Action apply)
        {
            name = null;
            cost = long.MaxValue;
            pad = Vector2.zero;
            apply = null;

            foreach (ZoneData zone in _config.Zones)
            {
                if (_openZones.Contains(zone.Id) || zone.UnlockCost >= cost || !IsPlayed(zone)) continue;

                ZoneData captured = zone;
                FieldSpec field = Array.Find(Fields, f => f.ZoneId == captured.Id);
                if (!IsReachable(field)) continue;

                name = "Zona " + zone.DisplayName;
                _targetId = zone.Id;
                cost = zone.UnlockCost;
                pad = field != null ? field.Gate : Vector2.zero;
                apply = () => _openZones.Add(captured.Id);
            }

            foreach (UpgradeData upgrade in _config.Upgrades)
            {
                int level = _upgradeLevel[upgrade.Id];
                if (level >= upgrade.MaxLevel) continue;

                long price = upgrade.CostOfLevel(level + 1);
                if (price >= cost) continue;

                UpgradeData captured = upgrade;
                name = upgrade.DisplayName + " " + (level + 1);
                _targetId = upgrade.Id;
                cost = price;
                pad = UpgradePad.TryGetValue(upgrade.Id, out Vector2 position) ? position : UpgradePad["upgrade_price"];
                apply = () => _upgradeLevel[captured.Id]++;
            }

            return apply != null;
        }

        private bool TryBuyNext()
        {
            if (!NextTarget(out string name, out long cost, out Vector2 pad, out Action apply)) return false;
            if (_coins < cost) return false;

            WalkTo(pad);
            Advance(_config.PurchaseStartDelaySeconds + _config.PurchaseSeconds);   // espera y goteo parado en el pad
            _coins -= cost;
            apply();

            Purchases.Add(new Purchase { Id = _targetId, Name = name, AtSeconds = _time, SincePrevious = _time - _lastPurchaseAt, Cost = cost });
            _lastPurchaseAt = _time;
            return true;
        }

        // El fertilizante: un video cada tanto (el stock se acumula) y se gasta cuando muchas plantas están creciendo. Solo se hace el
        // viaje cuando el jugador está "ocioso" (nada para cosechar en los próximos segundos): si no, el viaje le costaría más de lo
        // que ahorra, y un jugador de verdad tampoco lo haría.
        private bool TryFertilizer()
        {
            BestPlot(out _, out float idleSeconds);
            bool idle = idleSeconds > IdleSecondsToUseFertilizer;

            if (idle && _time >= _nextVideoAt && _fertilizer < _config.FertilizerMaxStock)
            {
                WalkTo(VideoPad);
                Advance(DwellSeconds);   // el video en sí congela el juego: no corre tiempo de juego
                _fertilizer++;
                _nextVideoAt = _time + FertilizerVideoEverySeconds;
                return true;
            }

            if (!idle || _fertilizer <= 0) return false;

            int open = 0, growing = 0;
            foreach (Plot plot in _plots)
            {
                if (!IsOpen(plot)) continue;
                open++;
                if (plot.State == 1) growing++;
            }

            if (open == 0 || growing < 4 || growing < open * FertilizerUseWhenGrowingFraction) return false;

            WalkTo(UsePad);
            Advance(DwellSeconds);
            _fertilizer--;
            FertilizersUsed++;
            foreach (Plot plot in _plots)
            {
                if (IsOpen(plot) && plot.State == 1)
                {
                    plot.Elapsed = plot.Crop.GrowSeconds;
                    plot.State = 2;
                }
            }

            return true;
        }

        // Se vende cuando lo que se lleva alcanza para la próxima compra, o cuando no hay nada para cosechar pronto y la mochila ya va
        // por la mitad (el viaje al pad de venta vale la pena).
        private bool ShouldSellNow()
        {
            if (_backpack.Count == 0) return false;

            if (!_calibrating && NextTarget(out _, out long cost, out _, out _) && _coins < cost && _coins + BackpackValue() >= cost) return true;

            if (_backpack.Count * 2 < Capacity) return false;
            return BestPlot(out _, out float costSeconds) == null || costSeconds > 6f;
        }

        private long BackpackValue()
        {
            float price = Multiplier(UpgradeKind.SellPrice);
            long total = 0;
            foreach (CropData crop in _backpack) total += PriceCalculator.FinalPrice(crop.SellPrice, price, 1f);
            return total;
        }

        // La parcela a la que conviene ir ahora: la que se puede pisar antes (caminar o esperar a que madure). null si no hay.
        private Plot BestPlot(out float travelSeconds, out float costSeconds)
        {
            Plot best = null, bestWaiting = null;
            float bestScore = -1f, bestTravel = 0f, waitingTravel = 0f, waitingCost = float.MaxValue;
            float speed = Speed;
            float growth = Growth;
            bool full = _backpack.Count >= Capacity;

            foreach (Plot plot in _plots)
            {
                if (!IsOpen(plot)) continue;
                if (plot.State == 2 && full) continue;   // con la mochila llena no se puede cosechar

                float travel = Vector2.Distance(_pos, plot.Pos) / speed;
                float ripensIn = plot.State == 1 ? (plot.Crop.GrowSeconds - plot.Elapsed) / growth : 0f;

                if (ripensIn <= travel)
                {
                    // Lista al llegar: entre varias, la que más paga por segundo de viaje (un jugador de verdad va primero a lo que rinde).
                    float score = plot.Crop.SellPrice / (travel + 0.4f);
                    if (score > bestScore)
                    {
                        best = plot;
                        bestScore = score;
                        bestTravel = travel;
                    }
                }
                else
                {
                    // Si no hay ninguna lista, la que madura antes (entre dos parecidas, la que paga más).
                    bool better = ripensIn < waitingCost - 0.01f || (Mathf.Abs(ripensIn - waitingCost) <= 0.01f && bestWaiting != null && plot.Crop.SellPrice > bestWaiting.Crop.SellPrice);
                    if (!better) continue;

                    bestWaiting = plot;
                    waitingTravel = travel;
                    waitingCost = ripensIn;
                }
            }

            if (best != null)
            {
                travelSeconds = bestTravel;
                costSeconds = bestTravel;
                return best;
            }

            travelSeconds = waitingTravel;
            costSeconds = waitingCost;
            return bestWaiting;
        }

        private void VisitBestPlot()
        {
            Plot plot = BestPlot(out float travel, out float cost);
            if (plot == null)
            {
                Advance(1f);
                WaitSeconds += 1f;
                return;
            }

            WalkTo(plot.Pos);
            if (cost > travel)
            {
                Advance(cost - travel);   // esperó a que madurara
                WaitSeconds += cost - travel;
            }

            // Pisar la parcela: cosecha si está lista (y entra en la mochila) y vuelve a sembrar.
            if (plot.State == 2 && _backpack.Count < Capacity)
            {
                _backpack.Add(plot.Crop);
                plot.State = 0;
                _harvests++;
                CreditRareHarvest(plot.Crop);
                if (_harvests == 1) FirstHarvestSeconds = _time;
                if (_harvests == 3) ThirdHarvestSeconds = _time;
            }

            if (plot.State == 0)
            {
                if (!_sowDone && ++_sown >= 3) { _sowDone = true; ThirdSowSeconds = _time; }   // U-08: tres parcelas creciendo = paso "sembrar"
                plot.State = 1;
                plot.Elapsed = 0f;
            }
        }

        // J-04: la cosecha rara paga, en promedio, chance x multiplicador x precio final. El bot no usa azar (la simulación tiene
        // que dar siempre lo mismo): acumula el valor esperado y acredita las monedas enteras a medida que se juntan.
        private double _rareCarry;

        // F-18: los pedidos del pueblo pagan un extra (OrderBonus) sobre lo que valdrían las unidades entregadas. El bot no entrega:
        // supone que OrdersDeliveredShare de lo que vende iría a pedidos y acredita ese extra esperado, solo con el cartel abierto.
        public const double OrdersDeliveredShare = 0.3;

        private double _orderCarry;

        private void CreditOrderBonus(long coins)
        {
            if (_openZones.Count < _config.OrdersMinZones) return;

            _orderCarry += coins * (double)_config.OrderBonus * OrdersDeliveredShare;

            long whole = (long)_orderCarry;
            if (whole <= 0L) return;

            _orderCarry -= whole;
            _coins += whole;
            CoinsEarned += whole;
        }

        private void CreditRareHarvest(CropData crop)
        {
            double expected = _config.RareHarvestChance * _config.RareHarvestMultiplier
                              * PriceCalculator.FinalPrice(crop.SellPrice, Multiplier(UpgradeKind.SellPrice), 1f);
            _rareCarry += expected;

            long whole = (long)_rareCarry;
            if (whole <= 0L) return;

            _rareCarry -= whole;
            _coins += whole;
            CoinsEarned += whole;
        }

        private void Sell()
        {
            if (_backpack.Count == 0) return;

            WalkTo(SellPad);

            // LIFO: la última cosecha sale primero.
            float price = Multiplier(UpgradeKind.SellPrice);
            while (_backpack.Count > 0)
            {
                CropData crop = _backpack[_backpack.Count - 1];
                _backpack.RemoveAt(_backpack.Count - 1);

                long coins = PriceCalculator.FinalPrice(crop.SellPrice, price, 1f);
                _coins += coins;
                CoinsEarned += coins;
                CreditOrderBonus(coins);
                BaseXp += crop.SellPrice;
                Advance(_config.SellIntervalSeconds);

                while (_level < _curve.MaxLevel && _curve.LevelForXp(BaseXp) > _level)
                {
                    _level++;
                    LevelSeconds.Add(_time);
                }
            }

            if (FirstSaleSeconds < 0f) { FirstSaleSeconds = _time; CoinsAfterFirstSale = _coins; }
        }

        // Segundo en que el bot llegó a ese nivel (-1 si no llegó; el nivel 1 es el 0).
        public float SecondsToLevel(int level)
        {
            if (level <= 1) return 0f;
            return level - 2 < LevelSeconds.Count ? LevelSeconds[level - 2] : -1f;
        }

        // ---------- Calibración ----------

        // Dado un orden de compras con su instante objetivo, juega hasta cada instante sin comprar nada, vende lo que lleva y anota
        // las monedas que tiene: ese es el costo que hace que esa compra ocurra justo ahí. Después la compra y sigue con la
        // siguiente. Los costos salen redondeados hacia abajo a 2 cifras. Es la herramienta con la que se arma la tabla de costos
        // (B-02); el test de balance después verifica la tabla real con el bot normal (siempre a lo más barato).
        public void Calibrate(IList<CalibrationStep> steps)
        {
            _calibrating = true;

            foreach (CalibrationStep step in steps)
            {
                int guard = 0;
                while (_time < step.AtSeconds && guard++ < 5000000)
                {
                    Advance(DecisionSeconds);
                    if (_backpack.Count >= Capacity || ShouldSellNow()) { Sell(); continue; }
                    if (TryFertilizer()) continue;
                    VisitBestPlot();
                }

                Sell();
                step.Cost = RoundDown(_coins);
                step.EarnedUntilHere = CoinsEarned;
                step.XpUntilHere = BaseXp;

                string name;
                Vector2 pad;
                Action apply;
                if (step.ZoneId != null)
                {
                    FieldSpec field = Array.Find(Fields, f => f.ZoneId == step.ZoneId);
                    name = "Zona " + step.ZoneId;
                    pad = field.Gate;
                    apply = () => _openZones.Add(step.ZoneId);
                }
                else
                {
                    name = step.UpgradeId + " +1";
                    pad = UpgradePad[step.UpgradeId];
                    apply = () => _upgradeLevel[step.UpgradeId]++;
                }

                WalkTo(pad);
                Advance(_config.PurchaseStartDelaySeconds + _config.PurchaseSeconds);
                _coins -= step.Cost;
                apply();
                Purchases.Add(new Purchase { Name = name, AtSeconds = _time, SincePrevious = _time - _lastPurchaseAt, Cost = step.Cost });
                _lastPurchaseAt = _time;
            }

            _calibrating = false;
        }

        // Sigue jugando sin comprar nada hasta ese segundo (para medir cuánta XP junta el bot cuando ya tiene todo).
        public void PlayUntil(float seconds)
        {
            int guard = 0;
            while (_time < seconds && guard++ < 5000000)
            {
                Advance(DecisionSeconds);
                if (_backpack.Count >= Capacity || ShouldSellNow()) { Sell(); continue; }
                if (TryFertilizer()) continue;
                VisitBestPlot();
            }
        }

        public float ElapsedSeconds => _time;

        private static long RoundDown(long value)
        {
            if (value < 100L) return Math.Max(1L, value / 5L * 5L);

            long unit = 1L;
            while (value / unit >= 100L) unit *= 10L;
            return value / unit * unit;
        }
    }
}
