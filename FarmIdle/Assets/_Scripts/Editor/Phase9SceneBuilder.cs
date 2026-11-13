using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 9: el banco de audio (sonidos de Kenney y la música generada) y su enganche al GameConfig, los sprites y la
    // flecha del HUD, y la guía de los primeros minutos (flechas en el mundo). El HUD en sí lo arma HudBuilder.
    // Igual que las demás, es una biblioteca: Phase2SceneBuilder.BuildAll la llama. Los datos que ya existen NO se pisan.
    //   Menú:    FarmIdle > Fase 9 > Reimportar audio y rehacer el banco
    //   Consola: Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.Phase9SceneBuilder.RebuildAudio
    internal static class Phase9SceneBuilder
    {
        private const string AudioFolder = "Assets/Audio";
        private const string SfxFolder = AudioFolder + "/Sfx";
        private const string MusicPath = AudioFolder + "/Music/farm_loop.wav";
        private const string BankFolder = "Assets/ScriptableObjects/Audio";
        private const string BankPath = BankFolder + "/AudioBank.asset";
        private const string UiSpritesFolder = "Assets/Sprites/UI";
        internal const string SoundOnPath = UiSpritesFolder + "/icon_sound_on.png";
        internal const string SoundOffPath = UiSpritesFolder + "/icon_sound_off.png";
        internal const string ArrowSpritePath = UiSpritesFolder + "/icon_arrow.png";
        internal const string VignettePath = UiSpritesFolder + "/vignette.png";
        private const string TrailMaterialPath = "Assets/Materials/M_TurboTrail.mat";
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const string ArrowMeshPath = MeshesFolder + "/SM_TutorialArrow.asset";

        // ---------- Banco de audio ----------

        // Cada sonido: sus variantes (archivos de Assets/Audio/Sfx sin extensión), volumen, rango de tono y mínimo entre disparos.
        private sealed class SoundSpec
        {
            public SoundCue Cue;
            public string[] Clips;
            public float Volume, PitchMin, PitchMax, MinInterval;
        }

        private static readonly SoundSpec[] Sounds =
        {
            new SoundSpec { Cue = SoundCue.Harvest, Clips = new[] { "harvest_1", "harvest_2", "harvest_3" }, Volume = 0.80f, PitchMin = 0.95f, PitchMax = 1.12f, MinInterval = 0.05f },
            new SoundSpec { Cue = SoundCue.Sell, Clips = new[] { "sell_1", "sell_2", "sell_3" }, Volume = 0.70f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 0.045f },
            new SoundSpec { Cue = SoundCue.Purchase, Clips = new[] { "purchase_1", "purchase_2" }, Volume = 0.90f, PitchMin = 0.98f, PitchMax = 1.02f, MinInterval = 0.20f },
            new SoundSpec { Cue = SoundCue.LevelUp, Clips = new[] { "levelup_1" }, Volume = 1.00f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 0.50f },
            new SoundSpec { Cue = SoundCue.Perk, Clips = new[] { "perk_1" }, Volume = 0.90f, PitchMin = 0.98f, PitchMax = 1.04f, MinInterval = 0.20f },
            new SoundSpec { Cue = SoundCue.Fertilize, Clips = new[] { "fertilize_1" }, Volume = 0.90f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 0.50f },
            new SoundSpec { Cue = SoundCue.Offer, Clips = new[] { "offer_1" }, Volume = 0.80f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 1.00f },
            new SoundSpec { Cue = SoundCue.Reward, Clips = new[] { "reward_1" }, Volume = 1.00f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 0.50f },
            new SoundSpec { Cue = SoundCue.Denied, Clips = new[] { "denied_1" }, Volume = 0.55f, PitchMin = 0.90f, PitchMax = 0.96f, MinInterval = 0.70f },
            new SoundSpec { Cue = SoundCue.Turbo, Clips = new[] { "turbo_1" }, Volume = 0.90f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 1.00f },
            new SoundSpec { Cue = SoundCue.Step, Clips = new[] { "step_1" }, Volume = 0.90f, PitchMin = 1.00f, PitchMax = 1.00f, MinInterval = 0.50f },
            new SoundSpec { Cue = SoundCue.Click, Clips = new[] { "click_1" }, Volume = 0.80f, PitchMin = 0.98f, PitchMax = 1.04f, MinInterval = 0.08f },
        };

        [MenuItem("FarmIdle/Fase 9/Reimportar audio y rehacer el banco")]
        public static void RebuildAudio()
        {
            ConfigureAudioImports();
            FillBank(AssetDatabase.LoadAssetAtPath<AudioBank>(BankPath) ?? CreateBank());
            LinkConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] Audio: banco rehecho con " + Sounds.Length + " sonidos y la música.");
        }

        // Ajustes de importación pensados para el tamaño y el uso de memoria: todo en mono, efectos cortos descomprimidos al cargar
        // (suenan sin demora y pesan poco en RAM) y la música comprimida en memoria. Vorbis en todo.
        internal static void ConfigureAudioImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                bool music = path.Contains("/Music/");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.30f : 0.50f;
                settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

                importer.forceToMono = true;
                importer.loadInBackground = false;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static AudioBank CreateBank()
        {
            Phase1SceneBuilder.EnsureFolder(BankFolder);
            var bank = ScriptableObject.CreateInstance<AudioBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
            return bank;
        }

        private static void FillBank(AudioBank bank)
        {
            var serialized = new SerializedObject(bank);

            SerializedProperty sounds = serialized.FindProperty("sounds");
            sounds.arraySize = Sounds.Length;
            for (int i = 0; i < Sounds.Length; i++)
            {
                SoundSpec spec = Sounds[i];
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("cue").enumValueIndex = (int)spec.Cue;
                entry.FindPropertyRelative("volume").floatValue = spec.Volume;
                entry.FindPropertyRelative("pitchMin").floatValue = spec.PitchMin;
                entry.FindPropertyRelative("pitchMax").floatValue = spec.PitchMax;
                entry.FindPropertyRelative("minIntervalSeconds").floatValue = spec.MinInterval;

                SerializedProperty clips = entry.FindPropertyRelative("clips");
                clips.arraySize = spec.Clips.Length;
                for (int c = 0; c < spec.Clips.Length; c++)
                {
                    clips.GetArrayElementAtIndex(c).objectReferenceValue = Phase2SceneBuilder.LoadRequired<AudioClip>(SfxFolder + "/" + spec.Clips[c] + ".ogg");
                }
            }

            serialized.FindProperty("music").objectReferenceValue = Phase2SceneBuilder.LoadRequired<AudioClip>(MusicPath);
            serialized.FindProperty("musicVolume").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);
        }

        // El GameConfig recibe el banco solo si todavía no tiene uno (respeta lo que se haya puesto a mano).
        private static void LinkConfig()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            SerializedProperty bank = serialized.FindProperty("audioBank");
            if (bank == null) throw new InvalidOperationException("GameConfig no tiene el campo 'audioBank'.");
            if (bank.objectReferenceValue == null) bank.objectReferenceValue = Phase2SceneBuilder.LoadRequired<AudioBank>(BankPath);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(UiSpritesFolder);
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            AdMobConfigSync.LoadOrCreateConfig();   // los IDs de AdMob (por defecto, los de prueba de Google)

            ConfigureAudioImports();
            AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BankPath);
            if (bank == null) FillBank(CreateBank());   // un banco que ya existe no se pisa
            LinkConfig();

            CreateIconSprite(SoundOnPath, speakerWaves: true, slash: false);
            CreateIconSprite(SoundOffPath, speakerWaves: false, slash: true);
            CreateArrowSprite(ArrowSpritePath);
            CreateVignetteSprite(VignettePath);
            CreateTrailMaterial();
            Phase3SceneBuilder.SaveMesh(BuildArrowMesh().ToMesh("SM_TutorialArrow"), ArrowMeshPath);

            AssetDatabase.SaveAssets();
        }

        // ---------- Íconos dibujados por código (hasta que haya arte definitivo) ----------

        private static void CreateIconSprite(string path, bool speakerWaves, bool slash)
        {
            WriteSprite(path, size: 128, coverage: (x, y) =>
            {
                float c = 0f;

                // Parlante: caja + cono.
                if (x >= 0.16f && x <= 0.32f && Mathf.Abs(y - 0.5f) <= 0.13f) c = 1f;
                if (x > 0.32f && x <= 0.56f && Mathf.Abs(y - 0.5f) <= 0.13f + (x - 0.32f) / 0.24f * 0.20f) c = 1f;

                if (speakerWaves)
                {
                    // Dos ondas: arcos a la derecha del parlante.
                    float dx = x - 0.50f, dy = y - 0.50f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (Mathf.Abs(angle) <= 42f && dx > 0.08f && (Mathf.Abs(radius - 0.26f) <= 0.025f || Mathf.Abs(radius - 0.39f) <= 0.025f)) c = 1f;
                }

                if (slash)
                {
                    // Una cruz a la derecha: "silenciado".
                    float a = Mathf.Abs((x - 0.64f) - (y - 0.50f));
                    float b = Mathf.Abs((x - 0.64f) + (y - 0.50f));
                    if (x >= 0.64f && x <= 0.90f && (a <= 0.03f || b <= 0.03f) && Mathf.Abs(y - 0.50f) <= 0.17f) c = 1f;
                }

                return c;
            });
        }

        // Flecha hacia arriba (+Y): triángulo con tallo.
        private static void CreateArrowSprite(string path)
        {
            WriteSprite(path, size: 128, coverage: (x, y) =>
            {
                // Triángulo de y = 0.45 (base) a y = 0.95 (punta).
                if (y >= 0.45f && y <= 0.95f && Mathf.Abs(x - 0.5f) <= (0.95f - y) / 0.50f * 0.40f) return 1f;
                if (y >= 0.08f && y < 0.45f && Mathf.Abs(x - 0.5f) <= 0.12f) return 1f;   // tallo
                return 0f;
            });
        }

        // Viñeteado: transparente en el centro y cada vez más opaco hacia los bordes (el destello de la pantalla lo tiñe de color).
        private static void CreateVignetteSprite(string path)
        {
            WriteSprite(path, size: 128, coverage: (x, y) =>
            {
                float dx = (x - 0.5f) * 2f, dy = (y - 0.5f) * 2f;
                float distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.5f + Mathf.Sqrt(dx * dx + dy * dy) * 0.5f / 1.4142f;   // mezcla caja y círculo
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.0f, distance));
            });
        }

        // Material de la estela del Turbo: sin luz, con el color y la transparencia de cada vértice del rastro.
        private static void CreateTrailMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath) != null) return;

            Phase1SceneBuilder.EnsureFolder(Phase1SceneBuilder.MaterialsFolder);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("No se encontró el shader Sprites/Default para la estela.");

            AssetDatabase.CreateAsset(new Material(shader) { name = "M_TurboTrail" }, TrailMaterialPath);
        }

        private static void WriteSprite(string path, int size, Func<float, float, float> coverage)
        {
            // Supermuestreo 3x3 por píxel para bordes suaves.
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            sum += coverage((px + (sx + 0.5f) / 3f) / size, (py + (sy + 0.5f) / 3f) / size);
                        }
                    }

                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(sum / 9f * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            // Se reescribe solo si cambió, para no ensuciar el AssetDatabase en cada BuildAll.
            if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        // La flecha que flota sobre el lugar de la guía: un cono hacia abajo con tallo, dorado. El origen es la punta.
        private static PaletteMeshBuilder BuildArrowMesh()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddCone(new Vector3(0f, 0.62f, 0f), 0.46f, new Vector3(0f, 0f, 0f), 6, Palette.WheatGold);
            mesh.AddCylinder(new Vector3(0f, 0.95f, 0f), 0.17f, 0.66f, 6, Quaternion.identity, Palette.WheatGold);
            mesh.AddCylinder(new Vector3(0f, 0.64f, 0f), 0.30f, 0.04f, 6, Quaternion.identity, Palette.WheatStraw);
            return mesh;
        }

        // ---------- Escena: la guía de los primeros minutos ----------

        private static readonly Vector3 FieldCenter = new Vector3(0f, 0f, 7f);
        private static readonly Vector3 SellPadPosition = new Vector3(0f, 0f, -3.5f);
        private static readonly Vector3 FirstUpgradePadPosition = new Vector3(PlazaBuilder.WestFarX, 0f, PlazaBuilder.AttributeRow1Z);   // la mejora de crecimiento, la más barata

        internal static void BuildWorldPieces()
        {
            var root = new GameObject("TutorialMarkers");
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(ArrowMeshPath);

            CreateMarker(root.transform, "Marker_Field", FieldCenter, TutorialStep.Sow, 3.6f, mesh, material);
            CreateMarker(root.transform, "Marker_Sell", SellPadPosition, TutorialStep.Sell, 4.4f, mesh, material);
            CreateMarker(root.transform, "Marker_Buy", FirstUpgradePadPosition, TutorialStep.Buy, 4.8f, mesh, material);

            BuildTurboEffects();
        }

        // El feedback del Turbo: el campo de visión de la cámara (en la Cinemachine Camera) y la estela del jugador.
        private static void BuildTurboEffects()
        {
            GameObject cameraObject = GameObject.Find("CM Main");
            if (cameraObject == null) throw new InvalidOperationException("No está la Cinemachine Camera 'CM Main' en la escena.");
            cameraObject.AddComponent<TurboCameraEffect>();

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) throw new InvalidOperationException("No está el jugador (tag Player) en la escena.");

            var trailObject = new GameObject("TurboTrail");
            trailObject.transform.SetParent(player.transform, false);
            trailObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Phase2SceneBuilder.LoadRequired<Material>(TrailMaterialPath);
            trail.time = 0.4f;
            trail.minVertexDistance = 0.1f;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.6f, 0.88f, 1f), 0f), new GradientColorKey(new Color(0.35f, 0.7f, 1f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            trail.emitting = false;
            trailObject.AddComponent<TurboTrail>();
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position, TutorialStep step, float arrowHeight, Mesh mesh, Material material)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;

            Transform arrow = PadBuilder.CreateMeshChild(marker.transform, "Arrow", mesh, material, new Vector3(0f, arrowHeight, 0f));
            arrow.localScale = Vector3.one * 1.3f;

            var component = marker.AddComponent<TutorialTargetMarker>();
            EditorUi.Wire(component, ("arrow", arrow));

            var serialized = new SerializedObject(component);
            serialized.FindProperty("step").enumValueIndex = (int)step;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
