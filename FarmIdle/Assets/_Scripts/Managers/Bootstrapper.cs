using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmIdle
{
    // Punto de entrada del juego: vive en la escena Bootstrap, la primera del build.
    //   1) aplica los ajustes globales,
    //   2) crea los servicios en un orden explícito bajo un objeto persistente ([Services], DontDestroyOnLoad),
    //   3) carga MainLevel encima y lo deja como escena activa,
    //   4) pasa el juego a Ready y descarga la escena Bootstrap.
    // Los servicios no se crean solos ni dependen del orden de Awake: si uno necesita a otro, el orden de
    // CreateServices lo garantiza.
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        private IEnumerator Start()
        {
            if (config == null)
            {
                Debug.LogError("[FarmIdle] Bootstrapper: falta asignar el GameConfig.", this);
                yield break;
            }

            if (GameManager.Instance == null)
            {
                CreateServices();
            }
            else
            {
                Debug.LogWarning("[FarmIdle] Bootstrapper: los servicios ya existían, se reutilizan.", this);
            }

            yield return LoadLevel();
        }

        private void CreateServices()
        {
            ApplyRuntimeSettings();

            var root = new GameObject("[Services]");
            DontDestroyOnLoad(root);

            // Orden de arranque. Cada servicio solo puede depender de los que están arriba.
            GameManager game = AddService<GameManager>(root.transform);
            game.Initialize();

            PoolManager pools = AddService<PoolManager>(root.transform);
            pools.Initialize();

            EconomyManager economy = AddService<EconomyManager>(root.transform);
            economy.Initialize(config);

            // Censo de parcelas: punto de encuentro del gameplay (Environment) y la UI, que no se ven entre sí.
            PlotCensus plotCensus = AddService<PlotCensus>(root.transform);
            plotCensus.Initialize();

            // Mochila del jugador: la leen la UI y la pila visible del jugador (Player), que no se ven entre sí,
            // igual que la billetera de la economía.
            BackpackManager backpack = AddService<BackpackManager>(root.transform);
            backpack.Initialize(config);

            // Progresión: zonas y mejoras. Va después de la economía y la mochila porque aplica sus efectos sobre ellas
            // (el multiplicador de precio y la capacidad).
            ProgressionManager progression = AddService<ProgressionManager>(root.transform);
            progression.Initialize(config);

            // Buffs temporales y fertilizante (Fase 8). Van antes del SaveManager porque tienen estado que se guarda (segundos restantes
            // de un buff, stock de fertilizante) y la partida se carga una sola vez, al final. Los buffs aplican su efecto de precio sobre
            // la economía (ya creada); el fertilizante espera a los campos, que se le registran al cargar el nivel.
            BuffManager buffs = AddService<BuffManager>(root.transform);
            buffs.Initialize(config);

            FertilizerManager fertilizer = AddService<FertilizerManager>(root.transform);
            fertilizer.Initialize(config);

            // Prestigio (F-03): el multiplicador permanente de las temporadas. Aplica su efecto sobre la economía y reinicia la progresión,
            // así que va después de ambas; su sección de la partida se registra al final (un archivo anterior no la trae y arranca en x1).
            PrestigeManager prestige = AddService<PrestigeManager>(root.transform);
            prestige.Initialize(config);

            // Progreso offline (B-01): acredita lo que produjeron las parcelas mientras el jugador no estaba. Mide el tiempo con la hora
            // guardada en la partida y convierte en monedas al empezar a jugar (los campos del nivel ya están registrados), así que solo
            // necesita a la economía y la progresión; su sección se registra al final igual que la de prestigio.
            OfflineManager offline = AddService<OfflineManager>(root.transform);
            offline.Initialize(config);

            // Misiones diarias (F-05): escuchan la mochila, las ventas, el fertilizante y las compras (todos ya creados) y miden el premio
            // con lo que producen las parcelas (OfflineManager). Su sección se registra al final, como las de prestigio y offline.
            MissionManager missions = AddService<MissionManager>(root.transform);
            missions.Initialize(config);

            // Logros (F-06): metas de por vida que escuchan lo mismo que las misiones, más el Turbo, las misiones reclamadas y el
            // prestigio (todos ya creados). Los videos se enganchan después de crear el servicio de anuncios (BindAds).
            AchievementManager achievements = AddService<AchievementManager>(root.transform);
            achievements.Initialize(config);

            // Premio de login diario (F-07): usa el fertilizante, la economía y la producción (OfflineManager) para medir el premio.
            DailyRewardManager daily = AddService<DailyRewardManager>(root.transform);
            daily.Initialize(config);

            // Decoraciones de la granja (F-17): cosméticas, se compran con monedas; solo necesitan a la economía y la progresión.
            DecorationManager decorations = AddService<DecorationManager>(root.transform);
            decorations.Initialize(config);

            // Ayudantes automáticos (F-17, tanda 2): suman monedas a partir de la producción (OfflineManager) y se reinician con el prestigio.
            HelperManager helpers = AddService<HelperManager>(root.transform);
            helpers.Initialize(config);

            // Skins del jugador (F-17, tanda 3): cosméticas; la vista del jugador (Player) las escucha.
            SkinManager skins = AddService<SkinManager>(root.transform);
            skins.Initialize(config);

            // Pedidos del pueblo (F-18): leen la mochila, la economía, la progresión (zonas abiertas) y el prestigio (semillas, reinicio).
            OrdersManager orders = AddService<OrdersManager>(root.transform);
            orders.Initialize(config);

            // Idioma que informa el portal (D-03): antes de los ajustes, que resuelven el idioma "Automático" al iniciar.
            PortalManager.ApplyPortalLocale();
            Loc.AutoIsEnglishOnly = Application.platform == RuntimePlatform.WebGLPlayer;

            // Ajustes del jugador y guía de los primeros minutos (Fase 9). Van antes del SaveManager porque tienen una sección que se
            // guarda ("settings", "tutorial") y la partida se carga una sola vez, al final. El audio, que usa los ajustes, es el último.
            SettingsManager settings = AddService<SettingsManager>(root.transform);
            settings.Initialize();

            TutorialManager tutorial = AddService<TutorialManager>(root.transform);
            tutorial.Initialize();

            // Partida guardada (Fase 7). Carga ANTES de que el nivel arranque: así cuando MainLevel hace su Start la billetera, el
            // progreso y la mochila ya tienen lo guardado y no hay nada que "reabrir". El orden de registro es el de restauración: el
            // progreso antes que la mochila, porque es el que le agranda la capacidad antes de que se le devuelva el contenido. Las
            // secciones de la Fase 8 van después de las existentes (un archivo de la Fase 7 no las trae y arrancan vacías).
            SaveManager save = AddService<SaveManager>(root.transform);
            save.Initialize(config);
            save.Register(economy);
            save.Register(progression);
            save.Register(backpack);
            save.Register(buffs);
            save.Register(fertilizer);
            save.Register(settings);
            save.Register(tutorial);
            save.Register(prestige);
            save.Register(offline);
            save.Register(missions);
            save.Register(achievements);
            save.Register(daily);
            save.Register(decorations);
            save.Register(helpers);
            save.Register(skins);
            save.Register(orders);
            save.Load();

            // La guía empieza a escuchar el juego recién ahora: restaurar la partida no cuenta como jugar.
            tutorial.Bind();

            // Anuncios con recompensa (Fase 8). Después del SaveManager: otorga lo ganado a los buffs y al fertilizante y, al hacerlo,
            // pide guardar. OfferManager decide cuándo aparece el símbolo de oferta y escucha al AdsManager.
            AdsManager ads = AddService<AdsManager>(root.transform);
            ads.Initialize(config);
            achievements.BindAds();

            OfferManager offers = AddService<OfferManager>(root.transform);
            offers.Initialize(config);

            // El audio (Fase 9) escucha a todos los demás, así que va último.
            AudioManager audioManager = AddService<AudioManager>(root.transform);
            audioManager.Initialize(config);

            // La vibración (U-06) escucha lo mismo que el audio y respeta el interruptor de los ajustes.
            HapticsManager haptics = AddService<HapticsManager>(root.transform);
            haptics.Initialize();

            // La analítica (N-01) también escucha a todos los demás y no cambia nada del juego: va después del audio.
            AnalyticsManager analytics = AddService<AnalyticsManager>(root.transform);
            analytics.Initialize();

            // El portal web (D-03) solo escucha el estado del juego para avisarle cuándo empieza y cuándo se corta.
            PortalManager portal = AddService<PortalManager>(root.transform);
            portal.Initialize();

            Debug.Log("[FarmIdle] Servicios listos: GameManager, PoolManager, EconomyManager, PlotCensus, BackpackManager, ProgressionManager, BuffManager, FertilizerManager, PrestigeManager, OfflineManager, MissionManager, AchievementManager, DailyRewardManager, DecorationManager, HelperManager, SkinManager, OrdersManager, SettingsManager, TutorialManager, SaveManager, AdsManager, OfferManager, AudioManager, HapticsManager, AnalyticsManager, PortalManager.");
        }

        private static T AddService<T>(Transform parent) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private void ApplyRuntimeSettings()
        {
#if UNITY_ANDROID || UNITY_IOS
            // En móvil Unity renderiza a 30 FPS por defecto, así que hay que pedir el objetivo.
            // En WebGL no se toca: el navegador ya sincroniza con la pantalla.
            Application.targetFrameRate = config.TargetFrameRate;
#endif
            Screen.sleepTimeout = config.KeepScreenOn ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        // MainLevel se carga de forma aditiva y esta escena se descarga al final. Así esta corrutina sigue viva
        // hasta el último paso (con una carga "Single" el Bootstrapper se destruiría antes de poder marcar Ready).
        private IEnumerator LoadLevel()
        {
            Scene bootstrapScene = gameObject.scene;

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneNames.MainLevel, LoadSceneMode.Additive);
            while (!load.isDone)
            {
                yield return null;
            }

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(SceneNames.MainLevel));

            // Un frame para que el nivel termine su Start() y su UI quede suscrita antes de avisar.
            yield return null;

            GameManager.Instance.MarkReady();
            Debug.Log("[FarmIdle] Nivel cargado: el juego pasa a Ready.");

            SceneManager.UnloadSceneAsync(bootstrapScene);
        }
    }
}
