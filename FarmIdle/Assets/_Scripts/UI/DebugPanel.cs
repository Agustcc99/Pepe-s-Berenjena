// SOLO existe en el Editor y en los builds de desarrollo (define DEVELOPMENT_BUILD): un build final ni siquiera compila esta clase, y
// ReleaseSceneStripper saca de las escenas el objeto que la lleva.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // Panel de pruebas para desarrollo. Existe en el Editor y en los Development Build; en un build final no existe (ver arriba).
    //   "+10":     suma monedas por la misma vía que usará el juego (EconomyManager): el HUD reacciona por evento.
    //   "Pausa":   alterna Playing <-> Paused; se nota porque el mundo se congela.
    //   "Pool x5": pide objetos de prueba al PoolManager y los devuelve a los pocos segundos. El texto de abajo
    //              muestra cuántas instancias se crearon: después del Prewarm ese número no tiene que subir.
    //   Parcelas:  una línea del texto cuenta las parcelas vacías, creciendo y listas (las lee del PlotCensus:
    //              la UI no conoce la capa Environment).
    //   Mochila:   una línea muestra cuántos productos lleva el jugador, de cuántos que entran, y el detalle por
    //              cultivo (la lee del BackpackManager como IReadOnlyInventory: la UI no conoce la capa Player).
    //   Vendido:   una línea cuenta lo vendido en la sesión, unidades y monedas (las lee del EconomyManager, que es
    //              quien cobra cada venta).
    //   Progreso:  la última línea cuenta las zonas abiertas y el nivel de cada mejora (las lee del ProgressionManager).
    //   Nivel:     una línea con el nivel, la XP, los puntos de perk libres y las elecciones de cada perk (del ProgressionManager).
    //   Guardado:  una línea con el estado de la partida guardada: versión, cómo cargó, cuándo se escribió por última vez y si hay
    //              cambios sin guardar (del SaveManager).
    //   "Borrar partida": borra el archivo y deja todo como en una partida nueva (solo desarrollo; no es un botón de gameplay).
    //   "+1000":   suma monedas grandes por la misma vía de siempre (EconomyManager), para probar las compras sin esperar
    //              a juntarlas. No hay ningún atajo que se salte los costos: pagar usa el mismo camino que el juego.
    public sealed class DebugPanel : MonoBehaviour
    {
        [Header("Botones")]
        [SerializeField] private Button addCoinsButton;
        [SerializeField] private Button bigCoinsButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button poolButton;
        [SerializeField, Tooltip("Borra la partida guardada y reinicia los sistemas (solo en builds de desarrollo). Puede faltar.")]
        private Button deleteSaveButton;
        [SerializeField] private TMP_Text pauseLabel;
        [SerializeField] private TMP_Text statsLabel;

        [Header("Anuncios (Fase 8)")]
        [SerializeField, Tooltip("Cambia el resultado que da el anuncio simulado: completo, saltado, error, sin anuncio. Puede faltar.")]
        private Button adModeButton;
        [SerializeField] private TMP_Text adModeLabel;
        [SerializeField, Tooltip("Suma un fertilizante al stock. Puede faltar.")]
        private Button giveFertilizerButton;
        [SerializeField, Tooltip("Hace aparecer ya el símbolo de oferta de velocidad. Puede faltar.")]
        private Button offerNowButton;

        [Header("Monedas")]
        [SerializeField, Min(1)] private int coinsPerTap = 10;
        [SerializeField, Min(1)] private int coinsPerBigTap = 1000;

        [Header("Pool de prueba")]
        [SerializeField] private GameObject probePrefab;
        [SerializeField, Min(1)] private int probePrewarm = 8;
        [SerializeField, Min(1)] private int probesPerTap = 5;
        [SerializeField, Min(0.1f)] private float probeSeconds = 3f;
        [SerializeField, Min(0.5f)] private float probeRingRadius = 2.5f;

        private readonly StringBuilder _text = new StringBuilder(240);
        private readonly List<CropData> _backpackCrops = new List<CropData>(4);   // cultivos distintos de la mochila
        private readonly List<int> _backpackCounts = new List<int>(4);
        private GameManager _game;
        private PlotCensus _census;
        private IReadOnlyInventory _backpack;
        private EconomyManager _economy;
        private ProgressionManager _progression;
        private SaveManager _save;
        private AdsManager _ads;
        private BuffManager _buffs;
        private FertilizerManager _fertilizer;
        private OfferManager _offers;
        private bool _active;
        private int _adMode;                    // 0 completo, 1 saltado, 2 error, 3 sin anuncio
        private string _lastAd = "-";
        private float _nextRefresh;

        public GameObject ProbePrefab => probePrefab;
        public int ProbePrewarm => probePrewarm;
        public int ProbesPerTap => probesPerTap;
        public float ProbeSeconds => probeSeconds;

        private void Awake()
        {
            _active = Debug.isDebugBuild;
            if (!_active) gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!_active) return;

            addCoinsButton.onClick.AddListener(OnAddCoinsClicked);
            if (bigCoinsButton != null) bigCoinsButton.onClick.AddListener(OnAddBigCoinsClicked);
            pauseButton.onClick.AddListener(OnPauseClicked);
            poolButton.onClick.AddListener(SpawnProbes);
            if (deleteSaveButton != null) deleteSaveButton.onClick.AddListener(OnDeleteSaveClicked);
            if (adModeButton != null) adModeButton.onClick.AddListener(OnAdModeClicked);
            if (giveFertilizerButton != null) giveFertilizerButton.onClick.AddListener(OnGiveFertilizerClicked);
            if (offerNowButton != null) offerNowButton.onClick.AddListener(OnOfferNowClicked);

            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;

            _census = PlotCensus.Instance;
            if (_census != null) _census.Changed += RefreshStats;

            BackpackManager backpacks = BackpackManager.Instance;
            if (backpacks != null)
            {
                _backpack = backpacks.Backpack;
                _backpack.Changed += OnBackpackChanged;
            }

            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.ProductSold += OnProductSold;

            _progression = ProgressionManager.Instance;
            if (_progression != null) _progression.Changed += OnProgressChanged;

            _save = SaveManager.Instance;
            if (_save != null) _save.StatusChanged += RefreshStats;

            _ads = AdsManager.Instance;
            if (_ads != null) _ads.AdFinished += OnAdFinished;

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.Changed += RefreshStats;

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Changed += RefreshStats;

            _offers = OfferManager.Instance;
            ApplyAdMode();

            RefreshPauseLabel();
            RefreshStats();
        }

        private void OnDisable()
        {
            if (!_active) return;

            addCoinsButton.onClick.RemoveListener(OnAddCoinsClicked);
            if (bigCoinsButton != null) bigCoinsButton.onClick.RemoveListener(OnAddBigCoinsClicked);
            pauseButton.onClick.RemoveListener(OnPauseClicked);
            poolButton.onClick.RemoveListener(SpawnProbes);
            if (deleteSaveButton != null) deleteSaveButton.onClick.RemoveListener(OnDeleteSaveClicked);
            if (adModeButton != null) adModeButton.onClick.RemoveListener(OnAdModeClicked);
            if (giveFertilizerButton != null) giveFertilizerButton.onClick.RemoveListener(OnGiveFertilizerClicked);
            if (offerNowButton != null) offerNowButton.onClick.RemoveListener(OnOfferNowClicked);

            if (_game != null)
            {
                _game.StateChanged -= OnStateChanged;
                _game = null;
            }

            if (_census != null)
            {
                _census.Changed -= RefreshStats;
                _census = null;
            }

            if (_backpack != null)
            {
                _backpack.Changed -= OnBackpackChanged;
                _backpack = null;
            }

            if (_economy != null)
            {
                _economy.ProductSold -= OnProductSold;
                _economy = null;
            }

            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression = null;
            }

            if (_save != null)
            {
                _save.StatusChanged -= RefreshStats;
                _save = null;
            }

            if (_ads != null)
            {
                _ads.AdFinished -= OnAdFinished;
                _ads = null;
            }

            if (_buffs != null)
            {
                _buffs.Changed -= RefreshStats;
                _buffs = null;
            }

            if (_fertilizer != null)
            {
                _fertilizer.Changed -= RefreshStats;
                _fertilizer = null;
            }

            _offers = null;
        }

        // Los segundos de los buffs y de la oferta cambian con el tiempo: el texto se refresca unas 2 veces por segundo (solo desarrollo).
        private void Update()
        {
            if (!_active || Time.unscaledTime < _nextRefresh) return;

            _nextRefresh = Time.unscaledTime + 0.5f;
            if (_ads != null || _buffs != null) RefreshStats();
        }

        private void Start()
        {
            if (!_active) return;

            // El Prewarm se declara una vez, al arrancar; de ahí en más el pool entrega sin Instantiate.
            if (PoolManager.Instance == null)
            {
                Debug.LogError("[FarmIdle] DebugPanel: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            PoolManager.Instance.Prewarm(probePrefab, probePrewarm);
            RefreshStats();
        }

        private void OnAddCoinsClicked()
        {
            if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(coinsPerTap);
        }

        private void OnAddBigCoinsClicked()
        {
            if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(coinsPerBigTap);
        }

        private void OnDeleteSaveClicked()
        {
            if (_save != null) _save.DeleteSave();
        }

        // Cicla el resultado que va a dar el anuncio simulado: completo -> saltado -> error -> sin anuncio.
        private void OnAdModeClicked()
        {
            _adMode = (_adMode + 1) % 4;
            ApplyAdMode();
            RefreshStats();
        }

        private void ApplyAdMode()
        {
            string label;
            switch (_adMode)
            {
                case 1: label = "Anuncio: saltado"; break;
                case 2: label = "Anuncio: error"; break;
                case 3: label = "Anuncio: sin anuncio"; break;
                default: label = "Anuncio: completo"; break;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_ads != null && _ads.Service is MockAdService mock)
            {
                mock.Available = _adMode != 3;
                mock.NextResult = _adMode == 1 ? AdResult.Closed : (_adMode == 2 ? AdResult.Failed : AdResult.Rewarded);
            }
            else
            {
                label = "Anuncio real";   // el resultado lo decide el servicio, no se puede forzar
            }
#else
            label = "Anuncio real";   // el simulado no existe en un build final
#endif

            if (adModeLabel != null) adModeLabel.text = label;
        }

        private void OnGiveFertilizerClicked()
        {
            if (_fertilizer != null) _fertilizer.DebugGive(1);
        }

        private void OnOfferNowClicked()
        {
            if (_offers != null) _offers.ForceOfferSoon();
        }

        private void OnAdFinished(RewardData reward, AdResult result, bool granted)
        {
            _lastAd = (reward != null ? reward.ShortLabel : "?") + " " + result + (granted ? " (otorgado)" : " (nada)");
            RefreshStats();
        }

        private void OnPauseClicked()
        {
            if (_game == null) return;

            if (_game.State == GameState.Playing) _game.Pause();
            else if (_game.State == GameState.Paused) _game.Resume();
        }

        // Pide 'probesPerTap' objetos en círculo alrededor del punto del suelo que está en el centro de la pantalla
        // (que es donde está el jugador, porque la cámara lo sigue) y los devuelve pasado 'probeSeconds'.
        public void SpawnProbes()
        {
            PoolManager pools = PoolManager.Instance;
            Camera cam = Camera.main;
            if (pools == null || cam == null) return;

            Vector3 center = GroundPointUnderScreenCenter(cam);
            var spawned = new List<GameObject>(probesPerTap);

            for (int i = 0; i < probesPerTap; i++)
            {
                float angle = i * Mathf.PI * 2f / probesPerTap;
                Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * probeRingRadius;
                position.y = 0.4f;   // apoyados sobre el suelo
                spawned.Add(pools.Get(probePrefab, position, Quaternion.identity));
            }

            RefreshStats();
            StartCoroutine(ReleaseAfterDelay(spawned));
        }

        private IEnumerator ReleaseAfterDelay(List<GameObject> spawned)
        {
            yield return new WaitForSeconds(probeSeconds);

            for (int i = 0; i < spawned.Count; i++)
            {
                PoolManager.Instance.Release(spawned[i]);
            }

            RefreshStats();
        }

        private static Vector3 GroundPointUnderScreenCenter(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var ground = new Plane(Vector3.up, Vector3.zero);
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : cam.transform.position + cam.transform.forward * 10f;
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            RefreshPauseLabel();
            RefreshStats();
        }

        private void OnBackpackChanged(InventoryChange change)
        {
            RefreshStats();
        }

        private void OnProductSold(CropData crop, long coins)
        {
            RefreshStats();
        }

        private void OnProgressChanged(ProgressChange change)
        {
            RefreshStats();
        }

        private void RefreshPauseLabel()
        {
            if (pauseLabel != null && _game != null)
            {
                pauseLabel.text = _game.State == GameState.Paused ? "Reanudar" : "Pausa";
            }
        }

        private void RefreshStats()
        {
            if (statsLabel == null) return;

            _text.Clear();
            _text.Append("Estado: ").Append(_game != null ? _game.State.ToString() : "-");

            if (PoolManager.Instance != null && PoolManager.Instance.TryGetStats(probePrefab, out PoolStats stats))
            {
                _text.Append("\nPool: ").Append(stats.Created).Append(" creadas, ")
                     .Append(stats.Active).Append(" activas, ")
                     .Append(stats.Inactive).Append(" libres, ")
                     .Append(stats.Misses).Append(" extra");
            }

            if (_census != null)
            {
                _text.Append("\nParcelas: ").Append(_census.Empty).Append(" vacías, ")
                     .Append(_census.Growing).Append(" creciendo, ")
                     .Append(_census.Ready).Append(" listas");
            }

            if (_backpack != null)
            {
                AppendBackpack(_text);
            }

            if (_economy != null)
            {
                _text.Append("\nVendido: ").Append(_economy.SoldUnits).Append(" unidades, ")
                     .Append(_economy.SoldCoins).Append(" monedas");
            }

            if (_progression != null)
            {
                AppendProgress(_text);
                AppendLevel(_text);
            }

            if (_save != null)
            {
                AppendSave(_text);
            }

            if (_ads != null)
            {
                AppendAds(_text);
            }

            statsLabel.text = _text.ToString();
        }

        // "Progreso: zonas 1/2 | Crec 0/3 Moch 2/3 Vel 0/3 Prec 1/3": zonas abiertas y nivel de cada mejora.
        private void AppendProgress(StringBuilder text)
        {
            text.Append("\nProgreso: zonas ").Append(_progression.UnlockedZoneCount).Append('/').Append(_progression.AvailableZoneCount);

            IReadOnlyList<UpgradeData> upgrades = _progression.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] == null) continue;

                text.Append(i == 0 ? " | " : " ").Append(upgrades[i].ShortLabel).Append(' ')
                    .Append(_progression.LevelOf(upgrades[i])).Append('/').Append(upgrades[i].MaxLevel);
            }
        }

        // "Nivel: 4 | XP 1300 (175/625) | puntos 1 | Fuerza 2/10 Vel 1/10": nivel, XP total y de este nivel, puntos libres y perks elegidos.
        private void AppendLevel(StringBuilder text)
        {
            text.Append("\nNivel: ").Append(_progression.Level);
            if (_progression.IsMaxLevel) text.Append(" MAX");

            _progression.GetLevelProgress(out long into, out long needed);
            text.Append(" | XP ").Append(_progression.Xp);
            if (needed > 0L) text.Append(" (").Append(into).Append('/').Append(needed).Append(')');

            text.Append(" | puntos ").Append(_progression.PerkPoints);

            IReadOnlyList<PerkData> perks = _progression.Perks;
            for (int i = 0; i < perks.Count; i++)
            {
                if (perks[i] == null) continue;

                text.Append(i == 0 ? " | " : " ").Append(perks[i].ShortLabel).Append(' ')
                    .Append(_progression.PicksOf(perks[i])).Append('/').Append(perks[i].MaxPicks);
            }
        }

        // "Anuncios: Mock | ult Turbo Rewarded (otorgado)" y "Buffs: Turbo 24.5 s | Fert 1/3 | oferta en 83 s": el servicio de anuncios, el
        // último resultado, los buffs activos con sus segundos de juego, el stock de fertilizante y la próxima oferta.
        private void AppendAds(StringBuilder text)
        {
            char newLine = (char)10;
            text.Append(newLine).Append("Anuncios: ").Append(_ads.Service.Name).Append(_ads.IsShowing ? " MOSTRANDO" : "").Append(" | ult ").Append(_lastAd);

            text.Append(newLine).Append("Buffs:");
            if (_buffs != null)
            {
                if (_buffs.Active.Count == 0) text.Append(" ninguno");
                for (int i = 0; i < _buffs.Active.Count; i++)
                {
                    RewardData reward = _buffs.RewardOf(_buffs.Active[i]);
                    text.Append(i == 0 ? " " : ", ").Append(reward != null ? reward.ShortLabel : _buffs.Active[i].Id)
                        .Append(' ').Append(_buffs.Active[i].Remaining.ToString("0.0")).Append(" s");
                }
            }

            if (_fertilizer != null) text.Append(" | Fert ").Append(_fertilizer.Stock).Append('/').Append(_fertilizer.MaxStock);

            if (_offers != null)
            {
                if (_offers.HasOffer) text.Append(" | oferta YA (").Append(Mathf.CeilToInt(_offers.OfferRemaining)).Append(" s)");
                else text.Append(" | oferta en ").Append(Mathf.CeilToInt(_offers.NextOfferIn)).Append(" s");
            }
        }

        // "Guardado: v1 Loaded | ultimo 21:04:12 (3) | sin cambios": versión del archivo, cómo cargó, última escritura (y cuántas van en
        // la sesión) y si hay cambios pendientes. Con un error, el error.
        private void AppendSave(StringBuilder text)
        {
            text.Append("\nGuardado: v").Append(_save.SavedVersion).Append(' ').Append(_save.LoadStatus);

            if (_save.LastSavedUtc.HasValue)
            {
                text.Append(" | ultimo ").Append(_save.LastSavedUtc.Value.ToLocalTime().ToString("HH:mm:ss"))
                    .Append(" (").Append(_save.SaveCount).Append(')');
            }
            else
            {
                text.Append(" | sin guardar aun");
            }

            text.Append(_save.IsDirty ? " | cambios pendientes" : " | al dia");
            if (!string.IsNullOrEmpty(_save.LastError)) text.Append(" | ERROR: ").Append(_save.LastError);
        }

        // "Mochila: 3/10 (Trigo 2, Tomate 1)", o "Mochila: 10/10 LLENA (...)". El detalle sale en el orden en que
        // aparece cada cultivo, de abajo hacia arriba.
        private void AppendBackpack(StringBuilder text)
        {
            text.Append("\nMochila: ").Append(_backpack.Count).Append('/').Append(_backpack.Capacity);
            if (_backpack.IsFull) text.Append(" LLENA");

            _backpackCrops.Clear();
            _backpackCounts.Clear();
            IReadOnlyList<CropData> items = _backpack.Items;
            for (int i = 0; i < items.Count; i++)
            {
                int slot = _backpackCrops.IndexOf(items[i]);
                if (slot < 0)
                {
                    _backpackCrops.Add(items[i]);
                    _backpackCounts.Add(1);
                }
                else
                {
                    _backpackCounts[slot]++;
                }
            }

            if (_backpackCrops.Count == 0) return;

            text.Append(" (");
            for (int i = 0; i < _backpackCrops.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(_backpackCrops[i].DisplayName).Append(' ').Append(_backpackCounts[i]);
            }
            text.Append(')');
        }
    }
}
#endif
