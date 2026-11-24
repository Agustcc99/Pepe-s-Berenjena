using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FarmIdle.EditorTools
{
    // Piezas de la escena de la Fase 1: suelo a cuadros, marcadores, jugador, cámara Cinemachine y joystick.
    // Ya no se ejecuta por separado: Phase2SceneBuilder las usa para armar MainLevel completo.
    internal static class Phase1SceneBuilder
    {
        internal const string MaterialsFolder = "Assets/Materials";
        private const int GroundTexels = 32;

        // Agrega el mundo de la Fase 1 a la escena abierta (que tiene que traer la Main Camera por defecto)
        // y devuelve el Canvas con el joystick, para colgarle más UI.
        internal static Canvas BuildWorld()
        {
            EnsureFolder(MaterialsFolder);

            Texture2D checker = CreateCheckerTexture();
            // Cuadros de 2 unidades: el plano mide 100, o sea 50 texels, y la textura tiene GroundTexels por lado.
            float groundTiling = 50f / GroundTexels;
            Material groundMat = CreateMaterial("M_Ground", Color.white, checker, new Vector2(groundTiling, groundTiling));
            Material playerMat = CreateMaterial("M_Player", new Color(0.95f, 0.55f, 0.15f), null, Vector2.one);
            Material noseMat = CreateMaterial("M_PlayerNose", Color.white, null, Vector2.one);

            CreateGround(groundMat);
            Material hatMat = CreateMaterial("M_PlayerHat", new Color(0.93f, 0.80f, 0.42f), null, Vector2.one);
            GameObject player = CreatePlayer(playerMat, noseMat, hatMat);
            CreateCamera(player.transform);
            return CreateJoystickUi();
        }

        // ---------- Mundo ----------

        private static void CreateGround(Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);   // 100 x 100 unidades
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------- Jugador ----------

        private static GameObject CreatePlayer(Material bodyMaterial, Material noseMaterial, Material hatMaterial)
        {
            var player = new GameObject("Player");
            player.tag = "Player";

            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.radius = 0.4f;
            controller.height = 2f;

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerActor>();   // lo que ven las parcelas y demás zonas: un IActor
            player.AddComponent<PlayerSkinView>();   // F-17: el color que lleva puesto

            // Los colliders de los hijos podrían bloquear al CharacterController, así que se quitan.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nariz";
            nose.transform.SetParent(visual.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = noseMaterial;

            // Sombrero de paja (ala y copa), colgado del cuerpo para que se incline con él: es lo que más se ve desde la cámara alta.
            CreatePlayerPart(PrimitiveType.Cylinder, "SombreroAla", visual.transform, new Vector3(0f, 0.8f, 0f), new Vector3(1.25f, 0.02f, 1.25f), hatMaterial);
            CreatePlayerPart(PrimitiveType.Cylinder, "SombreroCopa", visual.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.68f, 0.1f, 0.68f), hatMaterial);

            BuildSkinParts(visual.transform, hatMaterial);

            // Manos y pies sueltos (estilo muñeco): cuelgan de la raíz y los mueve PlayerAnimator. Llevan el color de la nariz (y de la skin).
            var footScale = new Vector3(0.3f, 0.2f, 0.46f);
            var handScale = new Vector3(0.26f, 0.26f, 0.26f);
            Transform leftFoot = CreatePlayerPart(PrimitiveType.Sphere, "PieIzq", player.transform, new Vector3(-0.27f, 0.1f, 0.08f), footScale, noseMaterial);
            Transform rightFoot = CreatePlayerPart(PrimitiveType.Sphere, "PieDer", player.transform, new Vector3(0.27f, 0.1f, 0.08f), footScale, noseMaterial);
            Transform leftHand = CreatePlayerPart(PrimitiveType.Sphere, "ManoIzq", player.transform, new Vector3(-0.62f, 0.95f, 0.05f), handScale, noseMaterial);
            Transform rightHand = CreatePlayerPart(PrimitiveType.Sphere, "ManoDer", player.transform, new Vector3(0.62f, 0.95f, 0.05f), handScale, noseMaterial);

            var animator = player.AddComponent<PlayerAnimator>();
            EditorUi.Wire(animator, ("body", visual.transform), ("leftFoot", leftFoot), ("rightFoot", rightFoot), ("leftHand", leftHand), ("rightHand", rightHand));

            return player;
        }

        // IN-04: sombreros y diseños de las skins. Cada uno es un grupo apagado ("Hat_<Nombre>", "Pat_<Nombre>") colgado del cuerpo; PlayerSkinView
        // prende el de la skin puesta y lo pinta (las piezas cuyo nombre termina en "Acc" van con el color de detalle). Nada se crea en juego.
        // Medidas en el espacio del cuerpo (cápsula de radio 0,5 y alto 2, el frente es +Z). Cilindro: x/z = diámetro, y = mitad del alto.
        private static void BuildSkinParts(Transform visual, Material material)
        {
            Transform cap = SkinGroup(visual, "Hat_" + SkinHat.Cap);
            SkinPart(PrimitiveType.Sphere, "Dome", cap, new Vector3(0f, 0.8f, 0f), new Vector3(0.98f, 0.6f, 0.98f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "VisorAcc", cap, new Vector3(0f, 0.76f, 0.6f), new Vector3(0.62f, 0.05f, 0.5f), new Vector3(10f, 0f, 0f), material);

            Transform beanie = SkinGroup(visual, "Hat_" + SkinHat.Beanie);
            SkinPart(PrimitiveType.Sphere, "Dome", beanie, new Vector3(0f, 0.78f, 0f), new Vector3(1.0f, 0.75f, 1.0f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cylinder, "Cuff", beanie, new Vector3(0f, 0.64f, 0f), new Vector3(0.98f, 0.07f, 0.98f), Vector3.zero, material);
            SkinPart(PrimitiveType.Sphere, "PompomAcc", beanie, new Vector3(0f, 1.16f, 0f), new Vector3(0.26f, 0.26f, 0.26f), Vector3.zero, material);

            Transform sprout = SkinGroup(visual, "Hat_" + SkinHat.Sprout);
            SkinPart(PrimitiveType.Cylinder, "StemAcc", sprout, new Vector3(0f, 1.1f, 0f), new Vector3(0.07f, 0.1f, 0.07f), Vector3.zero, material);
            SkinPart(PrimitiveType.Sphere, "LeafL", sprout, new Vector3(-0.17f, 1.24f, 0f), new Vector3(0.36f, 0.08f, 0.2f), new Vector3(0f, 0f, 25f), material);
            SkinPart(PrimitiveType.Sphere, "LeafR", sprout, new Vector3(0.17f, 1.24f, 0f), new Vector3(0.36f, 0.08f, 0.2f), new Vector3(0f, 0f, -25f), material);

            Transform topHat = SkinGroup(visual, "Hat_" + SkinHat.TopHat);
            SkinPart(PrimitiveType.Cylinder, "Brim", topHat, new Vector3(0f, 0.86f, 0f), new Vector3(1.0f, 0.02f, 1.0f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cylinder, "Tube", topHat, new Vector3(0f, 1.16f, 0f), new Vector3(0.62f, 0.3f, 0.62f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cylinder, "BandAcc", topHat, new Vector3(0f, 0.95f, 0f), new Vector3(0.64f, 0.06f, 0.64f), Vector3.zero, material);

            Transform flower = SkinGroup(visual, "Hat_" + SkinHat.Flower);   // el sombrero de paja se queda; esto va encima
            SkinPart(PrimitiveType.Cylinder, "Petals", flower, new Vector3(0.26f, 1.04f, 0.12f), new Vector3(0.5f, 0.03f, 0.5f), new Vector3(0f, 0f, -12f), material);
            SkinPart(PrimitiveType.Sphere, "CenterAcc", flower, new Vector3(0.27f, 1.09f, 0.12f), new Vector3(0.18f, 0.18f, 0.18f), Vector3.zero, material);

            Transform crown = SkinGroup(visual, "Hat_" + SkinHat.Crown);
            SkinPart(PrimitiveType.Cylinder, "Band", crown, new Vector3(0f, 0.98f, 0f), new Vector3(0.72f, 0.1f, 0.72f), Vector3.zero, material);
            Vector2[] spikes = { new Vector2(0.28f, 0f), new Vector2(-0.28f, 0f), new Vector2(0f, 0.28f), new Vector2(0f, -0.28f) };
            for (int i = 0; i < spikes.Length; i++)
            {
                SkinPart(PrimitiveType.Cube, "Spike" + i, crown, new Vector3(spikes[i].x, 1.18f, spikes[i].y), new Vector3(0.15f, 0.26f, 0.15f), new Vector3(0f, 45f, 0f), material);
            }

            SkinPart(PrimitiveType.Sphere, "GemAcc", crown, new Vector3(0f, 0.99f, 0.37f), new Vector3(0.15f, 0.15f, 0.15f), Vector3.zero, material);

            Transform stripe = SkinGroup(visual, "Pat_" + SkinPattern.Stripe);
            SkinPart(PrimitiveType.Cylinder, "BeltAcc", stripe, new Vector3(0f, -0.1f, 0f), new Vector3(1.04f, 0.07f, 1.04f), Vector3.zero, material);

            Transform scarf = SkinGroup(visual, "Pat_" + SkinPattern.Scarf);
            SkinPart(PrimitiveType.Cylinder, "NeckAcc", scarf, new Vector3(0f, 0.43f, 0f), new Vector3(1.03f, 0.09f, 1.03f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "TailAcc", scarf, new Vector3(0.24f, 0.18f, 0.5f), new Vector3(0.16f, 0.5f, 0.08f), new Vector3(0f, 0f, -8f), material);

            Transform overalls = SkinGroup(visual, "Pat_" + SkinPattern.Overalls);
            SkinPart(PrimitiveType.Cube, "BibAcc", overalls, new Vector3(0f, -0.08f, 0.52f), new Vector3(0.54f, 0.5f, 0.1f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "StrapLAcc", overalls, new Vector3(-0.2f, 0.28f, 0.47f), new Vector3(0.09f, 0.5f, 0.07f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "StrapRAcc", overalls, new Vector3(0.2f, 0.28f, 0.47f), new Vector3(0.09f, 0.5f, 0.07f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "BackAcc", overalls, new Vector3(0f, -0.1f, -0.52f), new Vector3(0.54f, 0.34f, 0.1f), Vector3.zero, material);   // la espalda, que es lo que más ve la cámara
            SkinPart(PrimitiveType.Cube, "BackStrapLAcc", overalls, new Vector3(-0.2f, 0.26f, -0.47f), new Vector3(0.09f, 0.5f, 0.07f), Vector3.zero, material);
            SkinPart(PrimitiveType.Cube, "BackStrapRAcc", overalls, new Vector3(0.2f, 0.26f, -0.47f), new Vector3(0.09f, 0.5f, 0.07f), Vector3.zero, material);

            Transform dots = SkinGroup(visual, "Pat_" + SkinPattern.Dots);
            // Adelante y atrás: la cámara mira al jugador desde atrás casi todo el tiempo.
            Vector3[] spots =
            {
                new Vector3(0f, 0.2f, 0.49f), new Vector3(-0.28f, -0.1f, 0.41f), new Vector3(0.28f, -0.1f, 0.41f), new Vector3(0f, -0.38f, 0.49f),
                new Vector3(0f, 0.2f, -0.49f), new Vector3(-0.28f, -0.1f, -0.41f), new Vector3(0.28f, -0.1f, -0.41f), new Vector3(0f, -0.38f, -0.49f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                SkinPart(PrimitiveType.Sphere, "Dot" + i + "Acc", dots, spots[i], new Vector3(0.17f, 0.17f, 0.17f), Vector3.zero, material);
            }
        }

        private static Transform SkinGroup(Transform visual, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(visual, false);
            group.SetActive(false);
            return group.transform;
        }

        private static void SkinPart(PrimitiveType type, string name, Transform group, Vector3 position, Vector3 scale, Vector3 eulerAngles, Material material)
        {
            Transform part = CreatePlayerPart(type, name, group, position, scale, material);
            part.localRotation = Quaternion.Euler(eulerAngles);
        }

        // Una pieza del muñeco: primitiva sin collider (bloquearía al CharacterController) y sin sombra propia que recibir de más.
        private static Transform CreatePlayerPart(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        // ---------- Cámara ----------

        private static void CreateCamera(Transform target)
        {
            Quaternion rotation = Quaternion.Euler(55f, 0f, 0f);
            var startPosition = new Vector3(0f, 13f, -9f);

            Camera mainCamera = Camera.main;
            mainCamera.transform.SetPositionAndRotation(startPosition, rotation);
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

            // La rotación se queda fija (no hay Rotation Control): la vista no gira cuando el jugador gira.
            var cameraObject = new GameObject("CM Main");
            cameraObject.transform.SetPositionAndRotation(startPosition, rotation);

            var cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
            cinemachineCamera.Target.TrackingTarget = target;
            cinemachineCamera.Lens.FieldOfView = 60f;

            var composer = cameraObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 16f;
            composer.Damping = new Vector3(0.3f, 0.3f, 0.3f);

            cameraObject.AddComponent<CameraJuice>();   // se aleja al abrir una zona y corriendo; golpecito al subir de nivel
        }

        // ---------- UI: Canvas y joystick flotante ----------

        private static Canvas CreateJoystickUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TextScaleApplier));   // TextScaleApplier: "Texto grande" (U-04)
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Al añadirse por código, el módulo recibe solo las acciones de UI por defecto.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Zona táctil transparente que cubre toda la pantalla. Es el primer hijo: la UI que se agregue
            // después se dibuja encima y recibe los toques antes que el joystick.
            RectTransform touchArea = EditorUi.CreateImage("TouchArea", canvasObject.transform, null, new Color(1f, 1f, 1f, 0f), true).rectTransform;
            EditorUi.Stretch(touchArea);

            // Los dibujos del joystick no deben capturar toques (Raycast Target desactivado).
            RectTransform background = EditorUi.CreateImage("JoystickBackground", touchArea, knob, new Color(1f, 1f, 1f, 0.35f), false).rectTransform;
            background.sizeDelta = new Vector2(240f, 240f);

            RectTransform handle = EditorUi.CreateImage("JoystickHandle", background, knob, new Color(1f, 1f, 1f, 0.8f), false).rectTransform;
            handle.sizeDelta = new Vector2(100f, 100f);

            var joystick = touchArea.gameObject.AddComponent<FloatingJoystick>();
            EditorUi.Wire(joystick, ("background", background), ("handle", handle));

            return canvas;
        }

        // ---------- Materiales y texturas ----------

        private static Texture2D CreateCheckerTexture()
        {
            const string path = MaterialsFolder + "/T_GroundChecker.png";

            // V-06: damero con variación. Cada texel (2 unidades en el mundo) sigue la paridad del damero, pero una parte sale
            // más clara o más oscura, con un generador fijo: el pasto deja de verse como un tablero y la textura es siempre la misma.
            var shades = new[]
            {
                new Color32(112, 168, 84, 255), new Color32(98, 152, 72, 255),    // damero base
                new Color32(124, 180, 90, 255), new Color32(88, 142, 66, 255),    // más claro / más oscuro
            };
            var pixels = new Color32[GroundTexels * GroundTexels];
            uint seed = 12345u;
            for (int y = 0; y < GroundTexels; y++)
            {
                for (int x = 0; x < GroundTexels; x++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    float roll = (seed >> 8 & 0xFFFF) / 65535f;
                    int index = (x + y) % 2;                   // paridad del damero
                    if (roll > 0.80f) index = 2;               // 20 %: parche claro
                    else if (roll < 0.14f) index = 3;          // 14 %: parche oscuro
                    pixels[y * GroundTexels + x] = shades[index];
                }
            }

            var texture = new Texture2D(GroundTexels, GroundTexels, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }

            // Filtro Point y sin mipmaps: los cuadros quedan nítidos.
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Material CreateMaterial(string name, Color color, Texture2D baseMap, Vector2 tiling)
        {
            string path = MaterialsFolder + "/" + name + ".mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            // Mate: sin brillos ni reflejos, como pide el estilo low poly.
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

            if (baseMap != null)
            {
                material.SetTexture("_BaseMap", baseMap);
                material.SetTextureScale("_BaseMap", tiling);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // Crea la carpeta y las que falten por encima (con sus .meta).
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
