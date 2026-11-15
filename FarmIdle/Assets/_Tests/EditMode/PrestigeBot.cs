using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El "bot de prestigio": juega varias temporadas seguidas con el BalanceBot (una partida completa por temporada) y, entre una y
    // otra, hace lo que hace PrestigeManager.TryStartSeason: suma la temporada, cobra las semillas doradas, las gasta en la tienda
    // (GameConfig.PrestigeShop) y arranca la siguiente con el multiplicador y las mejoras permanentes. Todo sale de los DATOS reales.
    // Sirve para ver cuánto dura cada temporada y dónde se rompe el final del juego (B-09).
    //
    // Reglas que reproduce:
    //   - Se reinicia apenas está todo comprado (zonas + mejoras al máximo), que es cuando el juego habilita «Nueva temporada».
    //   - Precio x (1 + PrestigeStep x temporadas) x «Buen precio»; crecimiento x «Cultivos veloces»; XP x «Aprendiz veloz».
    //   - Arranca con StartingCoins + «Monedas iniciales» y con el fertilizante que sobró + el de regalo (hasta el tope).
    //   - La XP (el nivel) no se reinicia.
    //
    // Lo que NO modela (además de lo que no modela el BalanceBot): los ayudantes y los sumideros cosméticos (el bot no gasta en
    // ellos: sus temporadas son las MÁS CORTAS posibles), el progreso offline («Depósito grande» no le cambia nada) y la entrega de
    // pedidos: las semillas de los pedidos grandes entran como un supuesto fijo por temporada (OrderSeedsPerSeason).
    internal sealed class PrestigeBot
    {
        public enum ShopPolicy
        {
            IncomeFirst,     // voraz: ahorra para «Buen precio», después «Cultivos veloces», después lo más barato
            CheapestFirst    // casual: siempre el nivel más barato que le alcanza (a igual costo, el primero del catálogo)
        }

        public sealed class Season
        {
            public int Number;                 // 1 = la partida nueva
            public float PriceMultiplier;      // con el que se jugó
            public float GrowthMultiplier;
            public long StartCoins;
            public int StartFertilizer;
            public bool Finished;              // compró todo dentro de MaxSeasonSeconds
            public float Seconds;              // de BOT (x EconomyPlan.PlayerSlowdown = jugador real)
            public float FirstPurchaseSeconds;
            public float LongestGapSeconds;
            public string LongestGapName;
            public int Purchases;
            public int QuickPurchases;         // compras a menos de QuickGapSeconds (de bot) de la anterior
            public long CoinsEarned;
            public int LevelAtEnd;
            public int SeedsEarned;            // al cerrarla (temporada + pedidos)
            public int SeedsLeft;              // sin gastar después de pasar por la tienda
            public string ShopBought = "";     // qué compró en la tienda al cerrarla
            public bool ShopComplete;          // la tienda quedó completa al cerrarla
        }

        // ---------- Ajustes ----------

        public ShopPolicy Policy = ShopPolicy.IncomeFirst;
        public int OrderSeedsPerSeason;                     // semillas de pedidos grandes que se suponen por temporada (0 a GameConfig.OrderSeedsPerSeasonCap)
        public float MaxSeasonSeconds = 8f * 3600f;         // de bot
        public float QuickGapSeconds = 24f;                 // 1 min de jugador real con el factor 2,5

        // ---------- Resultado ----------

        public readonly List<Season> Seasons = new List<Season>();
        public int ShopCompleteAfterSeason = -1;            // número de la temporada al cerrar la cual la tienda quedó completa (-1 = no llegó)
        public float TotalSeconds;                          // de bot, sumando las temporadas terminadas

        private readonly GameConfig _config;
        private readonly int[] _levels;
        private int _seeds;

        public PrestigeBot(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _levels = new int[config.PrestigeShop.Count];
        }

        // Valor total de las mejoras de un tipo, como PrestigeManager.ShopValue.
        private float ShopValue(PrestigeShopKind kind)
        {
            float value = PrestigeShopItem.Neutral(kind);
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_config.PrestigeShop[i].Kind == kind) value = _config.PrestigeShop[i].ValueAt(_levels[i]);
            }

            return value;
        }

        public void Run(int seasons)
        {
            int done = 0, fertilizer = 0;
            long xp = 0L;

            for (int number = 1; number <= seasons; number++)
            {
                var start = new BalanceBot.SeasonStart
                {
                    PriceMultiplier = PrestigeManager.MultiplierFor(done, _config.PrestigeStep) * ShopValue(PrestigeShopKind.SellPrice),
                    GrowthMultiplier = ShopValue(PrestigeShopKind.GrowthSpeed),
                    XpMultiplier = ShopValue(PrestigeShopKind.XpGain),
                    CoinsBonus = (long)ShopValue(PrestigeShopKind.StartCoins),
                    Fertilizer = fertilizer,
                    Xp = xp
                };

                var bot = new BalanceBot(_config, null, start) { MaxSeconds = MaxSeasonSeconds };
                bot.Run();

                var season = new Season
                {
                    Number = number,
                    PriceMultiplier = start.PriceMultiplier,
                    GrowthMultiplier = start.GrowthMultiplier,
                    StartCoins = _config.StartingCoins + start.CoinsBonus,
                    StartFertilizer = start.Fertilizer,
                    Finished = bot.Purchases.Count >= bot.TotalPurchasesNeeded,
                    Seconds = bot.ElapsedSeconds,
                    Purchases = bot.Purchases.Count,
                    CoinsEarned = bot.CoinsEarned,
                    LevelAtEnd = bot.Level
                };

                for (int i = 0; i < bot.Purchases.Count; i++)
                {
                    BalanceBot.Purchase purchase = bot.Purchases[i];
                    if (i == 0) season.FirstPurchaseSeconds = purchase.AtSeconds;
                    if (purchase.SincePrevious < QuickGapSeconds) season.QuickPurchases++;
                    if (purchase.SincePrevious > season.LongestGapSeconds)
                    {
                        season.LongestGapSeconds = purchase.SincePrevious;
                        season.LongestGapName = purchase.Name;
                    }
                }

                Seasons.Add(season);
                if (!season.Finished) break;   // sin todo comprado no hay «Nueva temporada»

                TotalSeconds += season.Seconds;

                // Lo que hace TryStartSeason: una temporada más, sus semillas, y el fertilizante de regalo sobre lo que sobró.
                done++;
                season.SeedsEarned = _config.SeedsForSeason(done) + Mathf.Clamp(OrderSeedsPerSeason, 0, _config.OrderSeedsPerSeasonCap);
                _seeds += season.SeedsEarned;
                season.ShopBought = BuyShop();
                season.SeedsLeft = _seeds;
                season.ShopComplete = IsShopComplete();
                if (season.ShopComplete && ShopCompleteAfterSeason < 0) ShopCompleteAfterSeason = number;

                fertilizer = Mathf.Min(_config.FertilizerMaxStock, bot.FertilizerLeft + (int)ShopValue(PrestigeShopKind.StartFertilizer));
                xp = bot.BaseXp;
            }
        }

        private bool IsShopComplete()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] < _config.PrestigeShop[i].MaxLevel) return false;
            }

            return true;
        }

        private string BuyShop()
        {
            var bought = new StringBuilder();
            int guard = 0;
            for (int pick = Pick(); pick >= 0 && guard++ < 100; pick = Pick())
            {
                PrestigeShopItem item = _config.PrestigeShop[pick];
                _seeds -= item.CostOf(_levels[pick]);
                _levels[pick]++;
                if (bought.Length > 0) bought.Append(", ");
                bought.Append(ShortName(item.Kind)).Append(' ').Append(_levels[pick]);
            }

            return bought.ToString();
        }

        // Qué nivel compra ahora con las semillas que tiene (-1 = nada: no le alcanza, no queda nada o está ahorrando).
        private int Pick()
        {
            if (Policy == ShopPolicy.IncomeFirst)
            {
                // Lo que acorta la temporada va primero y en orden; mientras falte, no gasta en otra cosa.
                int sell = Pending(PrestigeShopKind.SellPrice);
                if (sell >= 0) return Affordable(sell) ? sell : -1;

                int growth = Pending(PrestigeShopKind.GrowthSpeed);
                if (growth >= 0) return Affordable(growth) ? growth : -1;
            }

            int best = -1, bestCost = int.MaxValue;
            for (int i = 0; i < _levels.Length; i++)
            {
                PrestigeShopItem item = _config.PrestigeShop[i];
                if (_levels[i] >= item.MaxLevel) continue;

                int cost = item.CostOf(_levels[i]);
                if (cost > _seeds || cost >= bestCost) continue;

                best = i;
                bestCost = cost;
            }

            return best;
        }

        private int Pending(PrestigeShopKind kind)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_config.PrestigeShop[i].Kind == kind && _levels[i] < _config.PrestigeShop[i].MaxLevel) return i;
            }

            return -1;
        }

        private bool Affordable(int index)
        {
            return _config.PrestigeShop[index].CostOf(_levels[index]) <= _seeds;
        }

        private static string ShortName(PrestigeShopKind kind)
        {
            switch (kind)
            {
                case PrestigeShopKind.StartCoins: return "Monedas";
                case PrestigeShopKind.GrowthSpeed: return "Crecimiento";
                case PrestigeShopKind.OfflineCap: return "Depósito";
                case PrestigeShopKind.SellPrice: return "Precio";
                case PrestigeShopKind.StartFertilizer: return "Fertilizante";
                case PrestigeShopKind.XpGain: return "XP";
                default: return kind.ToString();
            }
        }

        // ---------- Informe ----------

        // La tabla por temporada, en minutos de JUGADOR REAL (los del bot por 'slowdown').
        public string Report(float slowdown)
        {
            var text = new StringBuilder(2048);
            text.AppendLine("Bot de prestigio | tienda: " + (Policy == ShopPolicy.IncomeFirst ? "voraz (Precio primero)" : "casual (lo más barato)")
                            + " | semillas de pedidos por temporada: " + OrderSeedsPerSeason + " | minutos de jugador real (x" + slowdown + ")");
            text.AppendLine("  T   xPrecio xCrec  dura(min)  vs ant.  acum(h)  1.a compra  hueco max  rafagas  nivel  semillas(+/quedan)  tienda");

            float total = 0f, previous = 0f;
            foreach (Season season in Seasons)
            {
                float minutes = season.Seconds * slowdown / 60f;
                if (season.Finished) total += minutes;

                text.AppendLine(string.Format("  {0,-3} {1,6:0.00} {2,6:0.00}  {3,8:0.0}  {4,7}  {5,7:0.0}  {6,10:0.0}  {7,9:0.0}  {8,3}/{9,-3}  {10,5}  {11,7}  {12}",
                    season.Number, season.PriceMultiplier, season.GrowthMultiplier, minutes,
                    previous > 0f ? (100f * (minutes / previous - 1f)).ToString("+0;-0") + " %" : "",
                    total / 60f, season.FirstPurchaseSeconds * slowdown / 60f, season.LongestGapSeconds * slowdown / 60f,
                    season.QuickPurchases, season.Purchases, season.LevelAtEnd,
                    season.Finished ? "+" + season.SeedsEarned + "/" + season.SeedsLeft : "-",
                    season.Finished ? season.ShopBought + (season.ShopComplete ? " [tienda completa]" : "") : "NO TERMINO (" + season.Purchases + " compras)"));

                previous = minutes;
            }

            text.AppendLine(ShopCompleteAfterSeason > 0
                ? "Tienda completa al cerrar la temporada " + ShopCompleteAfterSeason + ": desde ahí las semillas se acumulan sin uso."
                : "La tienda no se completó en " + Seasons.Count + " temporadas.");
            return text.ToString();
        }
    }
}
