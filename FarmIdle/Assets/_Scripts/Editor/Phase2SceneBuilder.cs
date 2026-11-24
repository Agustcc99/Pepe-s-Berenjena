using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Genera todo el contenido hasta la fase actual de forma repetible: assets de datos, sprite de moneda, prefab de
    // prueba del pool, el arte de la Fase 3 (paleta, mallas, plantas: ver Phase3SceneBuilder y KenneyArt), los productos
    // apilados de la Fase 4 (ver Phase4SceneBuilder), la zona de venta de la Fase 5 (ver Phase5SceneBuilder), las zonas y
    // mejoras de la Fase 6 (ver ProgressionData, ZoneBuilder y PlazaBuilder) y las escenas Bootstrap y MainLevel (mundo de la Fase 1 + campo de
    // parcelas + pila de la espalda + zona de venta + zonas bloqueadas y plaza de mejoras + HUD + menú inicial).
    // Las escenas se rehacen desde cero cada vez; los assets de datos que ya existen no se pisan, así que
    // si ajustás un valor a mano (por ejemplo en GameConfig) se respeta.
    //   Menú:    FarmIdle > Fase 2 > ...
    //   Consola: Unity.exe -batchmode -projectPath <ruta> -executeMethod FarmIdle.EditorTools.Phase2SceneBuilder.ImportTmpEssentials     (sin -quit: se cierra solo)
    //            Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.Phase2SceneBuilder.BuildAll
    public static class Phase2SceneBuilder
    {
        private const string ConfigFolder = "Assets/ScriptableObjects/Config";
        internal const string CropsFolder = "Assets/ScriptableObjects/Plants";
        private const string SpritesFolder = "Assets/Sprites/UI";
        private const string DebugPrefabsFolder = "Assets/Prefabs/Debug";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        internal const string ConfigPath = ConfigFolder + "/GameConfig.asset";
        private const string CoinSpritePath = SpritesFolder + "/UI_Coin.png";
        private const string ProbePrefabPath = DebugPrefabsFolder + "/PoolProbe.prefab";

        // ---------- Pasos públicos ----------

        private static double _tmpImportDeadline;

        // TextMeshPro necesita sus "Essential Resources" (fuente por defecto, shaders y ajustes) dentro de Assets.
        // Vienen con el paquete uGUI del Editor: no se descarga nada. Se corre una sola vez, en un arranque
        // de Unity aparte de BuildAll para que los assets queden importados antes de usarlos.
        // La importación es asíncrona: en batchmode hay que llamar a este método SIN -quit, y el propio método
        // cierra Unity cuando termina (o a los 3 minutos, con error).
        [MenuItem("FarmIdle/Fase 2/Importar recursos de TextMeshPro")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Debug.Log("[FarmIdle] Los recursos de TextMeshPro ya estaban importados.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);

            if (Application.isBatchMode)
            {
                _tmpImportDeadline = EditorApplication.timeSinceStartup + 180d;
                EditorApplication.update += WaitForTmpImport;
            }
        }

        private static void WaitForTmpImport()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;

            if (File.Exists(TmpSettingsPath))
            {
                EditorApplication.update -= WaitForTmpImport;
                AssetDatabase.SaveAssets();
                Debug.Log("[FarmIdle] Recursos de TextMeshPro importados en Assets/TextMesh Pro.");
                EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup > _tmpImportDeadline)
            {
                EditorApplication.update -= WaitForTmpImport;
                Debug.LogError("[FarmIdle] No se pudieron importar los recursos de TextMeshPro (falta " + TmpSettingsPath + ").");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("FarmIdle/Fase 2/Construir assets y escenas")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!File.Exists(TmpSettingsPath))
            {
                throw new InvalidOperationException("Faltan los recursos de TextMeshPro. Ejecutá antes FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            Phase1SceneBuilder.EnsureFolder(ScenePaths.Folder);

            // Primero los assets; las escenas los cargan por ruta justo antes de usarlos. No se guardan referencias
            // en variables entre un paso y otro: al crear una escena nueva Unity descarta objetos de memoria y la
            // referencia puede quedar nula.
            CreateGameConfig();
            CreateSampleCrops();
            CreateCoinSprite();
            CreateProbePrefab();
            KenneyArt.Prepare();                 // arte de Kenney compuesto y sus colores en la paleta (antes de escribirla)
            SceneryBuilder.Prepare();            // entorno (V-02): compone bosque y prado y registra sus colores en la paleta
            Phase3SceneBuilder.CreateAssets();   // paleta, mallas y plantas; engancha cada planta a su CropData
            SceneryBuilder.CreateAssets();       // mallas del bosque y del prado
            Phase4SceneBuilder.CreateAssets();   // productos apilados; engancha cada uno a su CropData y guarda la capacidad de la mochila
            Phase5SceneBuilder.CreateAssets();   // pad de venta y moneda; guarda los valores de venta del GameConfig
            PlazaBuilder.CreateAssets();         // mallas de los pads de mejora y del recinto de la plaza
            ZoneBuilder.CreateAssets();          // mallas del portón y del terreno cerrado de las zonas
            PadBuilder.CreateAssets();           // mallas de la barra de progreso del pago de los pads
            ProgressionData.CreateAssets();      // zonas y mejoras (datos); engancha todo al GameConfig
            Phase7SceneBuilder.CreateAssets();   // perks y cultivos en el GameConfig, mallas de los pads de perk
            Phase8SceneBuilder.CreateAssets();   // recompensas de anuncio en el GameConfig, mallas de los pads del fertilizante y prefab del símbolo de oferta
            OrdersBuilder.CreateAssets();        // cartel de los pedidos del pueblo (F-18)
            DailyCropBuilder.CreateAssets();     // estrella del cultivo del día (IN-02)
            Phase9SceneBuilder.CreateAssets();   // audio (importación, banco y enganche al GameConfig), íconos del HUD y flecha de la guía
            Phase10SceneBuilder.CreateAssets();  // plantillas de las misiones diarias (F-05), enganchadas al GameConfig
            Phase11SceneBuilder.CreateAssets();  // plantillas de los logros (F-06), enganchadas al GameConfig

            BuildBootstrapScene();
            BuildMainLevelScene();

            EditorBuildSettings.scenes = ScenePaths.All.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] Fase 2 generada: escenas " + string.Join(", ", ScenePaths.All) + " y assets de datos.");
        }

        internal static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("No se pudo cargar " + typeof(T).Name + " desde " + path + ".");
            }

            return asset;
        }

        // ---------- Datos ----------

        private static void CreateGameConfig()
        {
            Phase1SceneBuilder.EnsureFolder(ConfigFolder);

            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;   // respeta lo que se haya ajustado a mano

            var config = ScriptableObject.CreateInstance<GameConfig>();   // los valores por defecto salen de la clase
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        // La tabla de cultivos. Balance de la Fase 7 (sale de la simulación del bot voraz): tiempos x3 y precios x2 respecto de la
        // Fase 6, para que lo que más tarda pague más (0,33 / 0,40 / 0,50 / 0,57 monedas por segundo de parcela). Los assets que ya
        // existen NO se pisan al construir; para llevarlos a esta tabla está ApplyBalance (menú FarmIdle > Fase 7 > Aplicar balance).
        internal readonly struct CropSpec
        {
            public readonly string File, Id, Name;
            public readonly float GrowSeconds;
            public readonly int SellPrice;

            public CropSpec(string file, string id, string name, float growSeconds, int sellPrice)
            {
                File = file;
                Id = id;
                Name = name;
                GrowSeconds = growSeconds;
                SellPrice = sellPrice;
            }
        }

        internal static readonly CropSpec[] CropSpecs =
        {
            new CropSpec("Crop_Wheat", "wheat", "Trigo", 18f, 6),
            new CropSpec("Crop_Carrot", "carrot", "Zanahoria", 30f, 12),
            new CropSpec("Crop_Tomato", "tomato", "Tomate", 48f, 24),
            new CropSpec("Crop_Pumpkin", "pumpkin", "Calabaza", 84f, 48),   // Fase 6: el cultivo de la zona dorada
            // F-01: tiers 5 a 8. Cada uno tarda ~x1,7 y paga x2 respecto del anterior (0,69 / 0,80 / 0,96 / 1,13 monedas por segundo de parcela).
            new CropSpec("Crop_Corn", "corn", "Maíz", 140f, 96),
            new CropSpec("Crop_Eggplant", "eggplant", "Berenjena", 240f, 192),
            new CropSpec("Crop_Watermelon", "watermelon", "Sandía", 400f, 384),
            new CropSpec("Crop_Sunflower", "sunflower", "Girasol", 680f, 768)
        };

        private static void CreateSampleCrops()
        {
            Phase1SceneBuilder.EnsureFolder(CropsFolder);

            foreach (CropSpec crop in CropSpecs) CreateCrop(crop.File, crop.Id, crop.Name, crop.GrowSeconds, crop.SellPrice);
        }

        // Lleva los cultivos que ya existen a la tabla (tiempo y precio). Es idempotente.
        internal static void ApplyCropBalance()
        {
            foreach (CropSpec spec in CropSpecs)
            {
                string path = CropsFolder + "/" + spec.File + ".asset";
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);
                if (crop == null) continue;

                var serialized = new SerializedObject(crop);
                serialized.FindProperty("growSeconds").floatValue = spec.GrowSeconds;
                serialized.FindProperty("sellPrice").intValue = spec.SellPrice;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(crop);
            }
        }

        private static void CreateCrop(string fileName, string id, string displayName, float growSeconds, int sellPrice)
        {
            string path = CropsFolder + "/" + fileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CropData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var crop = ScriptableObject.CreateInstance<CropData>();

            var serialized = new SerializedObject(crop);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("growSeconds").floatValue = growSeconds;
            serialized.FindProperty("sellPrice").intValue = sellPrice;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(crop, path);
        }

        // ---------- Sprite de la moneda y prefab de prueba ----------

        // Moneda dibujada por código (disco dorado con borde, aro y brillo) hasta que haya arte definitivo.
        private static void CreateCoinSprite()
        {
            Phase1SceneBuilder.EnsureFolder(SpritesFolder);

            if (!File.Exists(CoinSpritePath))
            {
                File.WriteAllBytes(CoinSpritePath, RenderCoinPng());
                AssetDatabase.ImportAsset(CoinSpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(CoinSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static byte[] RenderCoinPng()
        {
            const int size = 128;
            const float rimWidth = 12f;
            float center = (size - 1) * 0.5f;
            float outer = size * 0.5f - 2f;

            var rimColor = new Color(0.78f, 0.50f, 0.07f);
            var faceColor = new Color(1.00f, 0.80f, 0.22f);
            var lineColor = new Color(0.86f, 0.62f, 0.10f);
            var shineColor = new Color(1.00f, 0.94f, 0.66f);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(outer - distance + 0.5f);   // borde suave
                    Color color = distance > outer - rimWidth ? rimColor : faceColor;

                    // Aro fino dentro de la cara.
                    float ring = Mathf.Abs(distance - (outer - rimWidth - 12f));
                    if (ring < 1.5f) color = lineColor;

                    // Brillo: óvalo claro arriba a la izquierda.
                    float sx = (x - (center - 22f)) / 22f;
                    float sy = (y - (center + 26f)) / 12f;
                    if (sx * sx + sy * sy < 1f && distance < outer - rimWidth - 6f) color = Color.Lerp(color, shineColor, 0.7f);

                    pixels[y * size + x] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)(alpha * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        // Cubo celeste que usa el panel de debug para probar el pool.
        private static void CreateProbePrefab()
        {
            Phase1SceneBuilder.EnsureFolder(DebugPrefabsFolder);
            Phase1SceneBuilder.EnsureFolder(Phase1SceneBuilder.MaterialsFolder);
            Material material = Phase1SceneBuilder.CreateMaterial("M_PoolProbe", new Color(0.20f, 0.80f, 0.95f), null, Vector2.one);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PoolProbe";
            cube.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(cube, ProbePrefabPath);
            Object.DestroyImmediate(cube);
        }

        // ---------- Escena Bootstrap ----------

        private static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = LoadRequired<GameConfig>(ConfigPath);

            // Cámara solo para tener algo que dibujar mientras carga (sin AudioListener ni tag MainCamera,
            // y con profundidad baja para que la cámara de MainLevel la tape).
            var cameraObject = new GameObject("BootstrapCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.12f, 0.08f);
            camera.cullingMask = 0;
            camera.depth = -100f;

            var bootstrapper = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
            EditorUi.Wire(bootstrapper, ("config", config));

            EditorSceneManager.SaveScene(scene, ScenePaths.Bootstrap);
        }

        // ---------- Escena MainLevel ----------

        private static void BuildMainLevelScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var coinSprite = LoadRequired<Sprite>(CoinSpritePath);
            var probePrefab = LoadRequired<GameObject>(ProbePrefabPath);

            Canvas canvas = Phase1SceneBuilder.BuildWorld();
            SceneryBuilder.BuildAtmosphere();   // luz cálida, ambiente, niebla y cielo (V-01)
            SceneryBuilder.Build();   // bosque alrededor, prado y paredes invisibles del borde (V-02)
            Phase3SceneBuilder.BuildField();
            Phase4SceneBuilder.BuildBackpack();   // el ancla de la espalda y la pila visible en el jugador
            Phase5SceneBuilder.BuildSellPad();    // la zona de venta, detrás del punto de aparición
            Phase5SceneBuilder.BuildHarvestFeedback();   // el "+1" que sube al cosechar (J-01)
            Phase5SceneBuilder.BuildBurstFeedback();     // el estallido al cosechar y comprar (J-02)
            var progression = new GameObject("Progression");
            ZoneBuilder.Build(progression.transform);    // zonas bloqueadas con su portón
            PlazaBuilder.Build(progression.transform);   // la plaza de mejoras
            Phase7SceneBuilder.BuildPerkPads();      // los dos pads de perk (Fuerza y Velocidad), detrás de la plaza de mejoras
            Phase8SceneBuilder.BuildWorldPieces();   // los pads del fertilizante (video, usar, comprar) y los lugares del símbolo de oferta
            OrdersBuilder.BuildWorldPieces();        // el cartel de pedidos y sus tres pads de entrega (F-18)
            Phase9SceneBuilder.BuildWorldPieces();   // las flechas de la guía de los primeros minutos (campo, venta, primera mejora)

            // El orden de los hijos del Canvas es el orden de dibujo: TouchArea (joystick) al fondo,
            // después el HUD, y el menú inicial encima de todo.
            RectTransform hud = EditorUi.CreateRect("Hud", canvas.transform);
            EditorUi.Stretch(hud);
            hud.gameObject.AddComponent<SafeAreaFitter>();

            BuildCoinCounter(hud, coinSprite);
            Phase7SceneBuilder.BuildLevelBar(hud);   // barra de XP y nivel
            Phase8SceneBuilder.BuildHud(hud);        // buffs activos y stock de fertilizante
            OrdersBuilder.BuildHud(hud);             // pedidos activos (F-18)
            HudBuilder.BuildHud(hud, canvas.transform);   // ícono de sonido con su tarjeta de ajustes y la flecha del borde de la guía
            Phase10SceneBuilder.BuildHud(hud, canvas.transform);  // botón "Misiones" y su tarjeta (F-05)
            Phase11SceneBuilder.BuildHud(hud, canvas.transform);  // botón "Logros" y su tarjeta con scroll (F-06)
            Phase12SceneBuilder.BuildHud(hud, canvas.transform);  // botón "Premio diario" y su tarjeta de 7 días (F-07)
            BuildDebugPanel(hud, probePrefab);
            Phase8SceneBuilder.BuildAdOverlay(canvas.transform);   // la capa de "anuncio en pantalla", encima del HUD
            BuildTapToPlay(canvas.transform);

            EditorSceneManager.SaveScene(scene, ScenePaths.MainLevel);
        }

        private static void BuildCoinCounter(RectTransform hud, Sprite coinSprite)
        {
            Image pill = EditorUi.CreatePanel("CoinCounter", hud, new Color(0.05f, 0.07f, 0.06f, 0.65f), false);
            RectTransform root = pill.rectTransform;

            // Arriba a la derecha. El pivote va al centro para que el "latido" escale desde el medio.
            EditorUi.Anchor(root, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -85f), new Vector2(400f, 110f));

            RectTransform icon = EditorUi.CreateImage("Icon", root, coinSprite, Color.white, false).rectTransform;
            EditorUi.Anchor(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(82f, 82f));

            TextMeshProUGUI amount = EditorUi.CreateText("Amount", root, "0", 64f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            amount.enableAutoSizing = true;   // si el número crece, la letra se achica en vez de salirse
            amount.fontSizeMin = 30f;
            amount.fontSizeMax = 64f;
            EditorUi.Stretch(amount.rectTransform);
            amount.rectTransform.offsetMin = new Vector2(112f, 8f);
            amount.rectTransform.offsetMax = new Vector2(-28f, -8f);

            var view = root.gameObject.AddComponent<CoinCounterView>();
            EditorUi.Wire(view, ("amountLabel", amount), ("pulseTarget", root));
        }

        private static void BuildDebugPanel(RectTransform hud, GameObject probePrefab)
        {
            RectTransform panel = EditorUi.CreateRect("DebugPanel", hud);
            EditorUi.Stretch(panel);

            Button addCoins = CreateDebugButton(panel, "Button +10", "+10", 0, new Color(0.95f, 0.60f, 0.10f), out _);
            Button pause = CreateDebugButton(panel, "Button Pausa", "Pausa", 1, new Color(0.35f, 0.55f, 0.85f), out TextMeshProUGUI pauseLabel);
            Button pool = CreateDebugButton(panel, "Button Pool", "Pool x5", 2, new Color(0.25f, 0.70f, 0.55f), out _);
            Button bigCoins = CreateDebugButton(panel, "Button +1000", "+1000", 3, new Color(0.85f, 0.45f, 0.10f), out _);   // para probar las compras (Fase 6)
            Button deleteSave = CreateDebugButton(panel, "Button Borrar partida", "Borrar", 4, new Color(0.75f, 0.20f, 0.20f), out _);   // Fase 7: borra la partida guardada (solo debug)

            // Texto de estado abajo a la izquierda: hasta 11 líneas (estado, pool, parcelas, mochila, vendido, progreso, nivel, guardado, anuncios, buffs) de unos 34 de alto cada una.
            TextMeshProUGUI stats = EditorUi.CreateText("Stats", panel, "", 28f, new Color(1f, 1f, 0.75f), TextAlignmentOptions.BottomLeft);
            EditorUi.Anchor(stats.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(1000f, 420f));

            var debugPanel = panel.gameObject.AddComponent<DebugPanel>();
            EditorUi.Wire(debugPanel,
                ("addCoinsButton", addCoins),
                ("bigCoinsButton", bigCoins),
                ("pauseButton", pause),
                ("poolButton", pool),
                ("deleteSaveButton", deleteSave),
                ("pauseLabel", pauseLabel),
                ("statsLabel", stats),
                ("probePrefab", probePrefab));

            Phase8SceneBuilder.AddDebugButtons(panel, debugPanel);   // Fase 8: resultado del anuncio simulado, +Fert, Oferta
        }

        // Botones en columna, arriba a la derecha, debajo del contador de monedas y del ícono de sonido (Fase 9: la primera fila bajó de
        // y = 170 a 300 para dejarle lugar al ícono, de 300 a 410 para el botón "Logros" y a 520 para el de "Premio diario").
        internal static Button CreateDebugButton(RectTransform parent, string name, string label, int row, Color color, out TextMeshProUGUI labelText)
        {
            Button button = EditorUi.CreateButton(name, parent, label, color, out labelText);
            EditorUi.Anchor((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -520f - row * 105f), new Vector2(260f, 90f));
            return button;
        }

        private static void BuildTapToPlay(Transform canvas)
        {
            // Capa oscura a pantalla completa (sin esquinas redondeadas) que recibe el toque.
            Image dim = EditorUi.CreatePanel("Menu", canvas, new Color(0.02f, 0.05f, 0.03f, 0.62f), true);
            dim.sprite = null;
            dim.type = Image.Type.Simple;
            EditorUi.Stretch(dim.rectTransform);

            TextMeshProUGUI title = EditorUi.CreateText("Title", dim.transform, "@menu.title", 150f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            // "Pepe's Berenjena" no entra en una línea a 150: se parte en dos y se achica si hace falta (DEC-2).
            title.textWrappingMode = TextWrappingModes.Normal;
            title.enableAutoSizing = true;
            title.fontSizeMin = 60f;
            title.fontSizeMax = 150f;
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(960f, 320f));

            TextMeshProUGUI prompt = EditorUi.CreateText("Prompt", dim.transform, "@menu.tap_to_play", 80f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(960f, 140f));

            TextMeshProUGUI hint = EditorUi.CreateText("KeyboardHint", dim.transform, "@menu.keyboard_hint", 44f, new Color(0.9f, 0.95f, 0.9f), TextAlignmentOptions.Center, FontStyles.Normal);
            EditorUi.Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -290f), new Vector2(960f, 150f));

            // Cambio de idioma sin entrar a Ajustes: el rótulo muestra el otro idioma (clave menu.language_switch).
            Button language = EditorUi.CreateButton("MenuLanguageButton", dim.transform, "@menu.language_switch", new Color(0.2f, 0.4f, 0.65f, 0.95f), out TextMeshProUGUI languageLabel);
            EditorUi.Anchor((RectTransform)language.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -480f), new Vector2(440f, 110f));
            language.gameObject.AddComponent<LanguageSwitchButton>();

            var overlay = dim.gameObject.AddComponent<TapToPlayOverlay>();   // agrega también el CanvasGroup que exige
            EditorUi.Wire(overlay, ("prompt", prompt), ("keyboardHint", hint));
        }
    }
}
