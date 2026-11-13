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

        // Agrega el mundo de la Fase 1 a la escena abierta (que tiene que traer la Main Camera por defecto)
        // y devuelve el Canvas con el joystick, para colgarle más UI.
        internal static Canvas BuildWorld()
        {
            EnsureFolder(MaterialsFolder);

            Texture2D checker = CreateCheckerTexture();
            Material groundMat = CreateMaterial("M_Ground", Color.white, checker, new Vector2(25f, 25f));   // cuadros de 2 unidades
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

            if (!File.Exists(path))
            {
                var colorA = new Color32(112, 168, 84, 255);
                var colorB = new Color32(98, 152, 72, 255);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels32(new[] { colorA, colorB, colorB, colorA });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
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
