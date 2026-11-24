using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Ajustes globales del juego. Hay un solo asset (Assets/ScriptableObjects/Config/GameConfig) y el Bootstrapper
    // se lo entrega a los servicios que lo necesitan. Es de solo lectura; como es único no lleva Id.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "FarmIdle/Datos/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Economía")]
        [SerializeField, Min(0), Tooltip("Monedas con las que empieza una partida nueva.")]
        private int startingCoins;

        [Header("Mochila")]
        [SerializeField, Min(1), Tooltip("Cuántos productos entran en la mochila del granjero (una sola capacidad, cultivos mezclados).")]
        private int backpackCapacity = 10;

        [Header("Venta")]
        [SerializeField, Min(0.02f), Tooltip("Segundos entre una venta y la siguiente mientras el jugador está en la zona de venta (usa el tiempo del juego: en pausa y con un anuncio no corre).")]
        private float sellIntervalSeconds = 0.12f;

        [SerializeField, Min(0.05f), Tooltip("Segundos que tarda un producto en volar de la espalda del jugador a la zona de venta (solo la animación; la moneda y el \"+N\" aparecen al llegar).")]
        private float sellFlightSeconds = 0.3f;

        [Header("Progresión")]
        [SerializeField, Tooltip("Las zonas bloqueables del nivel (ProgressionManager las busca por Id).")]
        private ZoneData[] zones = new ZoneData[0];

        [SerializeField, Tooltip("Las mejoras comprables (ProgressionManager las busca por Id).")]
        private UpgradeData[] upgrades = new UpgradeData[0];

        [SerializeField, Min(0.2f), Tooltip("Segundos que tarda en pagarse una compra completa parado sobre su pad (las monedas \"gotean\" a ese ritmo; usa el tiempo del juego).")]
        private float purchaseSeconds = 1.2f;

        [SerializeField, Min(0f), Tooltip("Segundos que hay que estar parado en el pad antes de que empiece a gotear, para no gastar monedas solo por cruzarlo.")]
        private float purchaseStartDelaySeconds = 1f;

        [Header("Cultivos")]
        [SerializeField, Tooltip("Todos los cultivos del juego. La partida guardada los referencia por Id (la mochila): SaveManager los busca acá.")]
        private CropData[] crops = new CropData[0];

        [Header("Niveles y perks (Fase 7)")]
        [SerializeField, Tooltip("Los perks que se eligen al subir de nivel (ProgressionManager los busca por Id).")]
        private PerkData[] perks = new PerkData[0];

        [SerializeField, Min(1), Tooltip("XP que hace falta para el nivel 2. El nivel L pide base x (L-1)^2 de XP acumulada: la XP es la suma de los precios BASE de lo vendido.")]
        private int levelBaseXp = 125;

        [SerializeField, Min(2), Tooltip("Nivel máximo. Cada nivel que se sube da un punto de perk (nivel máximo - 1 puntos en total).")]
        private int maxLevel = 20;

        [SerializeField, Min(1f), Tooltip("Tope del multiplicador PERMANENTE de velocidad (mejoras de monedas x perks). Deja lugar para el buff temporal de la Fase 8 y mantiene el movimiento controlable.")]
        private float maxMoveSpeedMultiplier = 1.5f;

        [Header("Guardado (Fase 7)")]
        [SerializeField, Min(0.2f), Tooltip("Segundos que se espera tras un cambio importante (compra, venta...) antes de escribir la partida. Los cambios seguidos se juntan en un solo guardado.")]
        private float saveDelaySeconds = 3f;

        [SerializeField, Min(0.2f), Tooltip("Igual que 'saveDelaySeconds' pero en la WEB: más corto, porque cerrar la pestaña de golpe es lo más común. Sigue siendo un solo guardado por tanda de cambios (no escribe en cada cuadro).")]
        private float webSaveDelaySeconds = 1f;

        [Header("Prestigio (F-03)")]
        [SerializeField, Min(0.01f), Tooltip("Cuánto suma al precio de venta CADA temporada completada: el multiplicador es 1 + paso x temporadas (0,25 = x1,25, x1,5, x1,75...).")]
        private float prestigeStep = 0.25f;

        [SerializeField, Min(0f), Tooltip("F-11: semillas doradas que da cada temporada = base + temporadas / divisor (división entera).")]
        private int seedsPerSeasonBase = 1;

        [SerializeField, Min(1), Tooltip("F-11: ver 'seedsPerSeasonBase'.")]
        private int seedsPerSeasonDivisor = 2;

        [SerializeField, Tooltip("F-11: las mejoras permanentes de la tienda de prestigio, que se pagan con semillas doradas.")]
        private PrestigeShopItem[] prestigeShop = PrestigeShopItem.Defaults();

        [Header("Cosecha rara (J-04)")]
        [SerializeField, Range(0f, 1f), Tooltip("J-04: probabilidad de que una cosecha salga rara (dorada) y dé un premio extra en monedas al instante. 0 = apagado.")]
        private float rareHarvestChance = 0.03f;

        [SerializeField, Min(0f), Tooltip("J-04: el premio de una cosecha rara, en veces el precio de venta final del cultivo (con mejoras, prestigio y buffs).")]
        private float rareHarvestMultiplier = 3f;

        [Header("Pedidos del pueblo (F-18)")]
        [SerializeField, Min(0), Tooltip("F-18: zonas compradas que hacen falta para que aparezca el cartel de pedidos (0 = desde el principio).")]
        private int ordersMinZones = 2;

        [SerializeField, Range(0.1f, 3f), Tooltip("F-18: cantidad de un pedido normal, en fracción de la capacidad de la mochila.")]
        private float orderQuantityShare = 0.8f;

        [SerializeField, Min(1), Tooltip("F-18: cantidad mínima de un pedido normal.")]
        private int orderMinQuantity = 4;

        [SerializeField, Min(1), Tooltip("F-18: cantidad máxima de un pedido normal.")]
        private int orderMaxQuantity = 24;

        [SerializeField, Min(1f), Tooltip("F-18: el pedido grande pide esta cantidad de veces la de uno normal.")]
        private float orderBigFactor = 2f;

        [SerializeField, Min(1), Tooltip("F-18: cantidad máxima de un pedido grande.")]
        private int orderBigMaxQuantity = 40;

        [SerializeField, Min(0f), Tooltip("F-18: premio de un pedido normal, en fracción extra sobre lo que valdrían esas unidades vendidas (0,25 = +25 %).")]
        private float orderBonus = 0.25f;

        [SerializeField, Min(0f), Tooltip("F-18: lo mismo para el pedido grande.")]
        private float orderBigBonus = 0.4f;

        [SerializeField, Min(0f), Tooltip("F-18: segundos de juego hasta que sale un pedido nuevo en el lugar de uno normal cumplido.")]
        private float orderRefillSeconds = 60f;

        [SerializeField, Min(0f), Tooltip("F-18: lo mismo para el lugar del pedido grande.")]
        private float orderBigRefillSeconds = 600f;

        [SerializeField, Min(0), Tooltip("F-18: semillas doradas que pueden dar los pedidos grandes en una temporada (de a 1 por pedido grande).")]
        private int orderSeedsPerSeasonCap = 2;

        [SerializeField, Tooltip("F-18: los cultivos que pueden salir en un pedido, con su zona y su peso.")]
        private OrderCropRule[] orderCrops = OrderCropRule.Defaults();

        [Header("Cultivo del día (IN-02)")]
        [SerializeField, Range(0f, 2f), Tooltip("IN-02: cuánto más paga el cultivo del día (0,25 = +25 %). Se elige por fecha entre los cultivos de las zonas abiertas. 0 = apagado. PROVISORIO (B-05).")]
        private float dailyCropBonus = 0.25f;

        [Header("Camión de reparto")]
        [SerializeField, Tooltip("Mejoras visuales del camión, en el orden en que se compran (el pad vende siempre la siguiente). Solo adorno.")]
        private TruckSkinRule[] truckSkins = TruckSkinRule.Defaults();

        [Header("Progreso offline (B-01)")]
        [SerializeField, Range(0f, 1f), Tooltip("Qué fracción de lo que rendirían las parcelas cosechadas sin parar se acredita por el tiempo fuera (nadie cosecha ni vende).")]
        private float offlineEfficiency = 0.15f;

        [SerializeField, Min(0f), Tooltip("Tope base, en segundos, del tiempo fuera que se acredita (7200 = 2 h). Una mejora lo amplía a 4 h.")]
        private float offlineBaseCapSeconds = 7200f;

        [SerializeField, Min(0f), Tooltip("Tope de monedas por cada hora fuera que se acredita, antes de Duplicar (con 2 h de tope de tiempo, 25.000 por hora = 50.000). Crece con el Almacén porque cuenta el tiempo acreditado. 0 = sin límite en monedas.")]
        private float offlineMaxCoinsPerHour = 15000f;

        [SerializeField, Min(0f), Tooltip("Segundos fuera mínimos para que aparezca la tarjeta de bienvenida (evita avisos por cambiar de app un momento).")]
        private float offlineMinSeconds = 120f;

        [SerializeField, Min(60f), Tooltip("B-11: segundos fuera a partir de los cuales lo sembrado se pudre y las parcelas vuelven vacías (86400 = 24 h). Antes de eso, lo sembrado vuelve crecido.")]
        private float offlineRotSeconds = 86400f;

        [Header("Misiones diarias (F-05)")]
        [SerializeField, Tooltip("Las plantillas de misión de las que se sortean las de cada día (MissionManager las busca por Id).")]
        private MissionData[] missions = new MissionData[0];

        [SerializeField, Range(1, 3), Tooltip("Cuántas misiones tiene cada día (la tarjeta del HUD tiene lugar para 3).")]
        private int missionsPerDay = 3;

        [SerializeField, Min(0), Tooltip("Monedas mínimas de un premio en monedas (para que al principio, con poca producción, igual valga la pena).")]
        private int missionMinCoins = 25;

        [SerializeField, Range(0.05f, 1f), Tooltip("Qué parte de los minutos de producción de la misión se paga de verdad. Los minutos se calculan con la producción ideal (todas las parcelas siempre a punto); jugando se llega a mucho menos, y sin esto dos misiones pagaban varias huertas (playtest Q-01).")]
        private float missionRewardScale = 0.3f;

        [Header("Logros (F-06)")]
        [SerializeField, Tooltip("Los logros, en el orden en que se muestran en la tarjeta (AchievementManager los busca por Id). Su premio usa la escala y el mínimo de las misiones.")]
        private AchievementData[] achievements = new AchievementData[0];

        [Header("Premio diario (F-07)")]
        [SerializeField, Tooltip("Premio en monedas de cada día de la racha de 7: minutos de producción actual (con la escala de las misiones). Crece de a poco; el día 7 es el grande.")]
        private float[] dailyRewardMinutes = { 4f, 5f, 6f, 8f, 10f, 12f, 20f };

        [SerializeField, Tooltip("Fertilizante que da cada día de la racha, además o en vez de las monedas (0 = solo monedas). Con el stock lleno cae a las monedas.")]
        private int[] dailyRewardFertilizer = { 0, 0, 0, 1, 0, 0, 2 };

        [SerializeField, Range(0, 3), Tooltip("Cuántos días salteados perdona la racha (1: saltear un día no la corta; saltear dos, sí).")]
        private int dailyGraceDays = 1;

        [Header("Decoraciones (F-17)")]
        [SerializeField, Tooltip("Decoraciones cosméticas de la granja, en el orden en que se compran (el pad vende siempre la siguiente).")]
        private DecorationData[] decorations = new DecorationData[0];

        [SerializeField, Tooltip("Niveles de ayudantes automáticos, en el orden en que se compran (se reinician con cada temporada).")]
        private HelperData[] helpers = new HelperData[0];

        [SerializeField, Min(0f), Tooltip("Cada cuántos segundos de juego los ayudantes entregan, de una, las monedas que juntaron (0 = moneda a moneda).")]
        private float helperPayoutSeconds = 5f;

        [SerializeField, Tooltip("Skins (colores) del jugador, en el orden en que se compran. El color de fábrica no está en la lista.")]
        private SkinData[] skins = new SkinData[0];

        [Header("Anuncios y recompensas (Fase 8)")]
        [SerializeField, Tooltip("Las recompensas de anuncio (AdsManager y BuffManager las buscan por Id).")]
        private RewardData[] rewards = new RewardData[0];

        [SerializeField, Min(1f), Tooltip("Tope de la velocidad FINAL del jugador, en unidades por segundo, con mejoras, perks y buff juntos (base 5, permanente máximo x1,5 = 7,5). Nunca baja la velocidad permanente: solo recorta lo que el buff sumaría de más.")]
        private float maxFinalMoveSpeed = 11f;

        [SerializeField, Min(0), Tooltip("Monedas que cuesta un fertilizante en el pad de compra. Tiene que ser mayor que lo que rinde una cosecha, para que no sea un truco infinito.")]
        private int fertilizerPrice = 1500;

        [SerializeField, Min(1f), Tooltip("B-08: por cada zona abierta el precio del fertilizante se multiplica por este factor (1 = precio fijo). El fertilizante termina de crecer todos los campos, así que rinde más con cada granja.")]
        private float fertilizerPriceStepPerZone = 1.8f;

        [SerializeField, Min(1), Tooltip("Cuántos fertilizantes se acumulan como máximo (stock guardado, el video llena hasta acá).")]
        private int fertilizerMaxStock = 3;

        [SerializeField, Min(1), Tooltip("Con cuántos fertilizantes en stock deja de poder comprarse con monedas (el video sí sigue sumando hasta el máximo).")]
        private int fertilizerBuyMaxStock = 1;

        [SerializeField, Min(0f), Tooltip("Segundos que tienen que pasar entre una compra de fertilizante con monedas y la siguiente.")]
        private float fertilizerBuyCooldownSeconds = 180f;

        [SerializeField, Min(0f), Tooltip("Segundos (reloj real, sin escala) que tienen que pasar entre el fin de un anuncio y el siguiente. B-03: ~3 min (el SDK de CrazyGames ignora pedidos con menos).")]
        private float adMinIntervalSeconds = 180f;

        [SerializeField, Min(5f), Tooltip("Segundos (reloj sin escala, sin contar la app en segundo plano) tras los que un anuncio sin respuesta se da por cortado: el juego vuelve a Playing y no se otorga nada.")]
        private float adTimeoutSeconds = 120f;

        [SerializeField, Min(0f), Tooltip("Segundos de tiempo de juego hasta que aparece el primer símbolo de oferta (velocidad por video).")]
        private float offerFirstDelaySeconds = 60f;

        [SerializeField, Min(0f), Tooltip("Segundos de tiempo de juego, como mínimo, entre que una oferta termina y aparece la siguiente.")]
        private float offerMinIntervalSeconds = 120f;

        [SerializeField, Min(0f), Tooltip("Lo mismo, como máximo (la espera se sortea entre el mínimo y este valor).")]
        private float offerMaxIntervalSeconds = 180f;

        [SerializeField, Min(1f), Tooltip("Segundos de tiempo de juego que el símbolo de oferta queda en el mundo antes de irse.")]
        private float offerLifetimeSeconds = 25f;

        [Header("Audio (Fase 9)")]
        [SerializeField, Tooltip("Los sonidos y la música (AudioManager). Sin banco no hay audio.")]
        private AudioBank audioBank;

        [SerializeField, Range(1, 32), Tooltip("Cuántos sonidos pueden sonar a la vez (fuentes de audio que se crean una sola vez al arrancar).")]
        private int audioVoices = 10;

        [Header("Rendimiento")]
        [SerializeField, Range(30, 120), Tooltip("FPS objetivo en móvil (en WebGL manda el navegador).")]
        private int targetFrameRate = 60;

        [SerializeField, Tooltip("Evita que la pantalla se apague mientras se juega.")]
        private bool keepScreenOn = true;

        public long StartingCoins => startingCoins;
        public int BackpackCapacity => backpackCapacity;
        public float SellIntervalSeconds => sellIntervalSeconds;
        public float SellFlightSeconds => sellFlightSeconds;
        public IReadOnlyList<ZoneData> Zones => zones;
        public IReadOnlyList<UpgradeData> Upgrades => upgrades;
        public float PurchaseSeconds => purchaseSeconds;
        public float PurchaseStartDelaySeconds => purchaseStartDelaySeconds;
        public IReadOnlyList<CropData> Crops => crops;
        public IReadOnlyList<PerkData> Perks => perks;
        public int LevelBaseXp => levelBaseXp;
        public int MaxLevel => maxLevel;
        public float MaxMoveSpeedMultiplier => maxMoveSpeedMultiplier;
        public float PrestigeStep => prestigeStep;
        public int OrdersMinZones => ordersMinZones;
        public float OrderQuantityShare => orderQuantityShare;
        public int OrderMinQuantity => orderMinQuantity;
        public int OrderMaxQuantity => Mathf.Max(orderMinQuantity, orderMaxQuantity);
        public float OrderBigFactor => orderBigFactor;
        public int OrderBigMaxQuantity => Mathf.Max(OrderMaxQuantity, orderBigMaxQuantity);
        public float OrderBonus => orderBonus;
        public float OrderBigBonus => orderBigBonus;
        public float OrderRefillSeconds => orderRefillSeconds;
        public float OrderBigRefillSeconds => orderBigRefillSeconds;
        public int OrderSeedsPerSeasonCap => orderSeedsPerSeasonCap;
        public IReadOnlyList<OrderCropRule> OrderCrops => orderCrops;
        public float DailyCropBonus => dailyCropBonus;
        public IReadOnlyList<TruckSkinRule> TruckSkins => truckSkins;
        public float RareHarvestChance => rareHarvestChance;
        public float RareHarvestMultiplier => rareHarvestMultiplier;
        public System.Collections.Generic.IReadOnlyList<PrestigeShopItem> PrestigeShop => prestigeShop;

        // Semillas doradas que da cerrar la temporada número 'seasonNumber' (1 = la primera).
        public int SeedsForSeason(int seasonNumber)
        {
            return Mathf.Max(0, seedsPerSeasonBase) + Mathf.Max(0, seasonNumber) / Mathf.Max(1, seedsPerSeasonDivisor);
        }
        public IReadOnlyList<MissionData> Missions => missions;
        public IReadOnlyList<AchievementData> Achievements => achievements;
        public int DailyGraceDays => dailyGraceDays;

        // Los minutos de producción del premio del día 'day' de la racha (1 a 7); fuera de la tabla vale el último.
        public float DailyRewardMinutes(int day)
        {
            if (dailyRewardMinutes == null || dailyRewardMinutes.Length == 0) return 5f;
            return dailyRewardMinutes[Mathf.Clamp(day - 1, 0, dailyRewardMinutes.Length - 1)];
        }

        // El fertilizante del premio del día 'day' (0 = solo monedas).
        public int DailyRewardFertilizer(int day)
        {
            if (dailyRewardFertilizer == null || day < 1 || day > dailyRewardFertilizer.Length) return 0;
            return dailyRewardFertilizer[day - 1];
        }
        public int MissionsPerDay => missionsPerDay;
        public int MissionMinCoins => missionMinCoins;
        public float MissionRewardScale => missionRewardScale;
        public float OfflineEfficiency => offlineEfficiency;
        public float OfflineBaseCapSeconds => offlineBaseCapSeconds;
        public float OfflineMaxCoinsPerHour => offlineMaxCoinsPerHour;
        public float OfflineMinSeconds => offlineMinSeconds;
        public float OfflineRotSeconds => offlineRotSeconds;
        public float SaveDelaySeconds => saveDelaySeconds;
        public float WebSaveDelaySeconds => webSaveDelaySeconds;
        public IReadOnlyList<RewardData> Rewards => rewards;
        public IReadOnlyList<DecorationData> Decorations => decorations;
        public IReadOnlyList<HelperData> Helpers => helpers;
        public float HelperPayoutSeconds => helperPayoutSeconds;
        public IReadOnlyList<SkinData> Skins => skins;
        public float MaxFinalMoveSpeed => maxFinalMoveSpeed;
        public int FertilizerPrice => fertilizerPrice;
        public float FertilizerPriceStepPerZone => fertilizerPriceStepPerZone;

        // B-08: precio del fertilizante con 'unlockedZones' zonas abiertas. El precio base se multiplica por FertilizerPriceStepPerZone
        // con cada zona y se redondea a 50. Sin zonas abiertas, o con factor 1, es el precio base tal cual.
        public long FertilizerPriceFor(int unlockedZones)
        {
            if (unlockedZones <= 0 || fertilizerPriceStepPerZone <= 1f) return fertilizerPrice;

            double price = fertilizerPrice;
            for (int i = 0; i < unlockedZones; i++) price *= fertilizerPriceStepPerZone;
            return (long)(System.Math.Round(price / 50.0) * 50.0);
        }
        public int FertilizerMaxStock => fertilizerMaxStock;
        public int FertilizerBuyMaxStock => fertilizerBuyMaxStock;
        public float FertilizerBuyCooldownSeconds => fertilizerBuyCooldownSeconds;
        public float AdMinIntervalSeconds => adMinIntervalSeconds;
        public float AdTimeoutSeconds => adTimeoutSeconds;
        public float OfferFirstDelaySeconds => offerFirstDelaySeconds;
        public float OfferMinIntervalSeconds => offerMinIntervalSeconds;
        public float OfferMaxIntervalSeconds => Mathf.Max(offerMinIntervalSeconds, offerMaxIntervalSeconds);
        public float OfferLifetimeSeconds => offerLifetimeSeconds;
        public AudioBank AudioBank => audioBank;
        public int AudioVoices => audioVoices;
        public int TargetFrameRate => targetFrameRate;
        public bool KeepScreenOn => keepScreenOn;

        // La recompensa con ese Id, o null (por ejemplo, una partida vieja que guardó un buff que ya no existe).
        public RewardData FindReward(string id)
        {
            if (string.IsNullOrEmpty(id) || rewards == null) return null;

            for (int i = 0; i < rewards.Length; i++)
            {
                if (rewards[i] != null && rewards[i].Id == id) return rewards[i];
            }

            return null;
        }

        // La primera recompensa de ese tipo (la que ofrece el símbolo de velocidad, la del video de fertilizante...), o null.
        public RewardData FirstRewardOfKind(RewardKind kind)
        {
            if (rewards == null) return null;

            for (int i = 0; i < rewards.Length; i++)
            {
                if (rewards[i] != null && rewards[i].Kind == kind) return rewards[i];
            }

            return null;
        }

        // El logro con ese Id, o null (por ejemplo, una partida vieja que guardó un logro que ya no existe).
        public AchievementData FindAchievement(string id)
        {
            if (string.IsNullOrEmpty(id) || achievements == null) return null;

            for (int i = 0; i < achievements.Length; i++)
            {
                if (achievements[i] != null && achievements[i].Id == id) return achievements[i];
            }

            return null;
        }

        // La plantilla de misión con ese Id, o null (por ejemplo, una partida vieja que guardó una misión que ya no existe).
        public MissionData FindMission(string id)
        {
            if (string.IsNullOrEmpty(id) || missions == null) return null;

            for (int i = 0; i < missions.Length; i++)
            {
                if (missions[i] != null && missions[i].Id == id) return missions[i];
            }

            return null;
        }

        // El cultivo con ese Id, o null si no existe (por ejemplo, una partida vieja que guardó un cultivo que ya no está).
        public CropData FindCrop(string id)
        {
            if (string.IsNullOrEmpty(id) || crops == null) return null;

            for (int i = 0; i < crops.Length; i++)
            {
                if (crops[i] != null && crops[i].Id == id) return crops[i];
            }

            return null;
        }

        // La mayor capacidad que puede llegar a tener la mochila: la base más lo que dan las mejoras de capacidad al
        // máximo nivel y los perks de capacidad elegidos hasta agotarse. Sirve para dimensionar el Prewarm de la pila
        // visible una sola vez, al cargar el nivel.
        public int MaxBackpackCapacity
        {
            get
            {
                int bonus = 0;
                if (upgrades != null)
                {
                    foreach (UpgradeData upgrade in upgrades)
                    {
                        if (upgrade == null || upgrade.Kind != UpgradeKind.BackpackCapacity) continue;
                        bonus += Mathf.Max(0, Mathf.RoundToInt(upgrade.ValueAtLevel(upgrade.MaxLevel)));
                    }
                }

                if (perks != null)
                {
                    foreach (PerkData perk in perks)
                    {
                        if (perk == null || perk.Kind != PerkKind.BackpackCapacity) continue;
                        bonus += Mathf.Max(0, Mathf.RoundToInt(perk.TotalAt(perk.MaxPicks)));
                    }
                }

                return backpackCapacity + bonus;
            }
        }
    }
}
