using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmIdle.EditorTools
{
    // V-02: el entorno de la granja. Todo estático, sin scripts y con el material de paleta único:
    //  - un bosque que rodea el área jugable (4 mallas, una por lado, así la cámara descarta las que no ve);
    //  - el "prado": matas de pasto, flores y piedritas en el césped libre de adentro (1 malla, sin sombra);
    //  - 4 paredes invisibles delante del bosque, para que el jugador no se vaya al vacío.
    // Son 5 draw calls como máximo y cero costo por cuadro. Las posiciones salen de una semilla fija: la escena
    // queda igual en cada corrida del builder.
    internal static class SceneryBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const int Seed = 20261113;

        // El área jugable (hasta donde llega el jugador) y el grosor del bosque de cada lado. La cámara mira al norte
        // inclinada 55°: hacia el norte se ve lejos (bosque profundo) y hacia el sur casi nada (bosque angosto).
        internal const float WestX = -22.5f, EastX = 22.5f, SouthZ = -34.5f, NorthZ = 38.5f;   // F-24: +12 al norte para la tercera fila de granjas (z 27 a 35)
        private const float SideDepth = 11f, NorthDepth = 20f, SouthDepth = 6f;

        private static readonly string[] Chunks = { "North", "South", "East", "West" };

        // Colores nuevos de la paleta (se registran en Prepare, después de los de Kenney).
        private static int _pine, _pineLight, _lime, _rock, _bark, _petal;
        internal static int RoadSand, RoadEdge, PaverLight, PaverDark;

        private static PaletteMeshBuilder[] _forest;
        private static PaletteMeshBuilder _meadow;

        private static string ForestPath(string chunk) { return MeshesFolder + "/SM_Forest_" + chunk + ".asset"; }
        private static string MeadowPath => MeshesFolder + "/SM_Meadow.asset";

        // Tiene que correr después de KenneyArt.Prepare (que vacía los colores extra) y antes de que se escriba la
        // textura de la paleta (Phase3SceneBuilder.CreateAssets).
        internal static void Prepare()
        {
            _pine = Palette.Register(new Color32(0x1F, 0x66, 0x3C, 255));
            _pineLight = Palette.Register(new Color32(0x3F, 0x9A, 0x4B, 255));
            _lime = Palette.Register(new Color32(0x9C, 0xCB, 0x4E, 255));
            _rock = Palette.Register(new Color32(0x6E, 0x74, 0x7C, 255));
            _bark = Palette.Register(new Color32(0x6B, 0x45, 0x29, 255));
            _petal = Palette.Register(new Color32(0xFF, 0xD8, 0x4D, 255));

            // V-07: caminos de arena y plaza adoquinada (PlazaBuilder.BuildPlazaDecor). Van al final para no mover los índices de arriba.
            RoadSand = Palette.Register(new Color32(0xCF, 0xAE, 0x76, 255));
            RoadEdge = Palette.Register(new Color32(0xA9, 0x82, 0x52, 255));
            PaverLight = Palette.Register(new Color32(0xBC, 0xB5, 0xA6, 255));
            PaverDark = Palette.Register(new Color32(0xA6, 0xA0, 0x92, 255));

            var random = new System.Random(Seed);

            _forest = new PaletteMeshBuilder[Chunks.Length];
            for (int i = 0; i < _forest.Length; i++) _forest[i] = new PaletteMeshBuilder();

            float outerWest = WestX - SideDepth, outerEast = EastX + SideDepth;
            FillForest(_forest[0], random, outerWest, outerEast, NorthZ + 1.2f, NorthZ + NorthDepth, 3.3f);
            FillForest(_forest[1], random, outerWest, outerEast, SouthZ - SouthDepth, SouthZ - 1.2f, 3.0f);
            FillForest(_forest[2], random, EastX + 1.2f, outerEast, SouthZ - 1f, NorthZ + 1f, 3.1f);
            FillForest(_forest[3], random, outerWest, WestX - 1.2f, SouthZ - 1f, NorthZ + 1f, 3.1f);

            _meadow = new PaletteMeshBuilder();
            FillMeadow(_meadow, random);
        }

        internal static void CreateAssets()
        {
            if (_forest == null) throw new InvalidOperationException("SceneryBuilder.Prepare tiene que correr antes de CreateAssets.");

            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            for (int i = 0; i < Chunks.Length; i++) Save(_forest[i].ToMesh("SM_Forest_" + Chunks[i]), ForestPath(Chunks[i]));
            Save(_meadow.ToMesh("SM_Meadow"), MeadowPath);
        }

        private static void Save(Mesh mesh, string path)
        {
            // El formato de índices por defecto es de 16 bits.
            if (mesh.vertexCount > 65000) throw new InvalidOperationException(mesh.name + " tiene " + mesh.vertexCount + " vértices: partila en más mallas.");
            Phase3SceneBuilder.SaveMesh(mesh, path);
        }

        internal static void Build()
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var root = new GameObject("Scenery");
            root.isStatic = true;

            foreach (string chunk in Chunks)
            {
                Transform forest = PadBuilder.CreateMeshChild(root.transform, "Forest_" + chunk, Phase2SceneBuilder.LoadRequired<Mesh>(ForestPath(chunk)), material, Vector3.zero);
                forest.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                forest.gameObject.isStatic = true;
            }

            Transform meadow = PadBuilder.CreateMeshChild(root.transform, "Meadow", Phase2SceneBuilder.LoadRequired<Mesh>(MeadowPath), material, Vector3.zero);
            meadow.gameObject.isStatic = true;

            float width = EastX - WestX, length = NorthZ - SouthZ, centerX = (EastX + WestX) * 0.5f, centerZ = (NorthZ + SouthZ) * 0.5f;
            AddWall(root.transform, "Wall_North", new Vector3(centerX, 2f, NorthZ + 0.5f), new Vector3(width + 2f, 4f, 1f));
            AddWall(root.transform, "Wall_South", new Vector3(centerX, 2f, SouthZ - 0.5f), new Vector3(width + 2f, 4f, 1f));
            AddWall(root.transform, "Wall_East", new Vector3(EastX + 0.5f, 2f, centerZ), new Vector3(1f, 4f, length + 2f));
            AddWall(root.transform, "Wall_West", new Vector3(WestX - 0.5f, 2f, centerZ), new Vector3(1f, 4f, length + 2f));
        }

        // V-01: ambiente cálido sin costo extra: misma luz direccional (con sombra), ambiente por gradiente
        // (sin skybox ni sondas), niebla lineal que funde el borde del suelo con el cielo y fondo de color liso.
        internal static void BuildAtmosphere()
        {
            var haze = new Color(0.95f, 0.89f, 0.76f);   // niebla y cielo: durazno claro

            Light sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.93f, 0.80f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.7f;
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.86f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.72f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.42f, 0.33f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 85f;

            Camera camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = haze;
        }

        private static void AddWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider));
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            wall.GetComponent<BoxCollider>().size = size;
            wall.isStatic = true;
        }

        // ---------- Bosque ----------

        // Una grilla con desorden: cada celda puede tener un árbol, un arbusto, una roca o nada.
        private static void FillForest(PaletteMeshBuilder mesh, System.Random random, float minX, float maxX, float minZ, float maxZ, float step)
        {
            for (float z = minZ + step * 0.5f; z < maxZ; z += step)
            {
                for (float x = minX + step * 0.5f; x < maxX; x += step)
                {
                    var at = new Vector3(x + Range(random, -1f, 1f) * step * 0.35f, 0f, z + Range(random, -1f, 1f) * step * 0.35f);
                    float pick = (float)random.NextDouble();
                    float size = Range(random, 0.85f, 1.3f);
                    float yaw = Range(random, 0f, 360f);

                    if (pick < 0.40f) AddPine(mesh, at, size, yaw, random);
                    else if (pick < 0.72f) AddRoundTree(mesh, at, size, yaw, random);
                    else if (pick < 0.84f) AddBush(mesh, at, size, random);
                    else if (pick < 0.91f) AddRock(mesh, at, size, yaw);
                }
            }
        }

        private static void AddPine(PaletteMeshBuilder mesh, Vector3 at, float size, float yaw, System.Random random)
        {
            int leaf = random.Next(3) == 0 ? _pineLight : _pine;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            mesh.AddStanding(at, new Vector3(0.36f, 1.1f, 0.36f) * size, turn, _bark);

            for (int tier = 0; tier < 3; tier++)
            {
                float baseY = (0.9f + tier * 1.05f) * size;
                float radius = (1.45f - tier * 0.38f) * size;
                mesh.AddCone(at + Vector3.up * baseY, radius, at + Vector3.up * (baseY + 1.7f * size), 6, leaf);
            }
        }

        private static void AddRoundTree(PaletteMeshBuilder mesh, Vector3 at, float size, float yaw, System.Random random)
        {
            int roll = random.Next(5);
            int leaf = roll == 0 ? _lime : roll < 3 ? Palette.LeafLight : _pineLight;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            mesh.AddStanding(at, new Vector3(0.4f, 1.9f, 0.4f) * size, turn, _bark);
            mesh.AddSphere(at + Vector3.up * (2.7f * size), 1.35f * size, leaf);
            mesh.AddSphere(at + turn * new Vector3(0.75f, 2.1f, 0.2f) * size, 0.85f * size, leaf);
            mesh.AddSphere(at + turn * new Vector3(-0.6f, 3.3f, -0.25f) * size, 0.8f * size, leaf);
        }

        private static void AddBush(PaletteMeshBuilder mesh, Vector3 at, float size, System.Random random)
        {
            int leaf = random.Next(2) == 0 ? Palette.LeafLight : _pineLight;
            mesh.AddSphere(at + new Vector3(0f, 0.45f, 0f) * size, 0.75f * size, leaf);
            mesh.AddSphere(at + new Vector3(0.6f, 0.35f, 0.25f) * size, 0.55f * size, leaf);
            if (random.Next(3) == 0) mesh.AddSphere(at + new Vector3(-0.2f, 0.95f, -0.1f) * size, 0.14f * size, Palette.Flower);
        }

        private static void AddRock(PaletteMeshBuilder mesh, Vector3 at, float size, float yaw)
        {
            mesh.AddBox(at + Vector3.up * (0.35f * size), new Vector3(1.3f, 0.9f, 1.0f) * size, Quaternion.Euler(8f, yaw, 12f), _rock);
            mesh.AddBox(at + new Vector3(0.7f, 0.2f, 0.3f) * size, new Vector3(0.7f, 0.5f, 0.6f) * size, Quaternion.Euler(-10f, yaw + 40f, 6f), Palette.Stone);
        }

        // ---------- Prado ----------

        // Lo que hay en el césped de adentro (campo, venta, avenida, zonas con su portón, plaza, decoraciones, símbolos
        // de oferta): ahí no va nada. (minX, maxX, minZ, maxZ)
        private static readonly Vector4[] Busy =
        {
            new Vector4(-5.2f, 5.2f, -6.5f, 5.6f),        // campo inicial, puesto y camión de venta
            new Vector4(-2.2f, 2.2f, -11f, 14.8f),        // la avenida, de la plaza a la zona norte
            new Vector4(-15.8f, -3.6f, 2.2f, 11.8f),      // zona oeste + portón
            new Vector4(3.6f, 15.8f, 2.2f, 11.8f),        // zona este + portón
            new Vector4(-15.8f, 15.8f, 14.2f, 23.8f),     // las tres zonas del norte + portones
            new Vector4(-15.8f, 15.8f, 23.8f, 35.8f),     // F-24: la tercera fila (granjas de temporada, portones y bandera)
            new Vector4(-13f, 13f, -34.5f, -9.4f),        // la plaza de mejoras y los pads de perk detrás
            new Vector4(15.8f, 19.6f, 4.4f, 9.6f),        // F-23: banco (este)
            new Vector4(-21.6f, -15.2f, 3.6f, 10.4f),     // F-23: estanque (oeste)
            new Vector4(15.6f, 21f, 16.4f, 22.6f),        // F-23: molino (este)
            new Vector4(-21.8f, -15.6f, 16.4f, 22.6f),    // F-23: fardo de heno (oeste)
            new Vector4(-13.6f, -8.4f, 11.8f, 14.2f),     // F-23: parterre de flores (pasillo entre filas)
            new Vector4(-10f, -6f, -6.5f, 3f),            // símbolos de oferta (oeste)
            new Vector4(6f, 10f, -6.5f, 3f),              // símbolos de oferta (este)
            new Vector4(-11.6f, -2.2f, -9.6f, -4.6f),     // cartel de pedidos y sus pads (F-18)
        };

        private static bool IsBusy(float x, float z)
        {
            foreach (Vector4 rect in Busy)
            {
                if (x > rect.x && x < rect.y && z > rect.z && z < rect.w) return true;
            }

            return false;
        }

        private static void FillMeadow(PaletteMeshBuilder mesh, System.Random random)
        {
            const float step = 1.7f;
            int[] petals = { Palette.Flower, Palette.White, _petal, Palette.Purple };

            for (float z = SouthZ + 1f; z < NorthZ - 0.5f; z += step)
            {
                for (float x = WestX + 1f; x < EastX - 0.5f; x += step)
                {
                    float px = x + Range(random, -0.6f, 0.6f);
                    float pz = z + Range(random, -0.6f, 0.6f);
                    float pick = (float)random.NextDouble();
                    float yaw = Range(random, 0f, 360f);
                    if (IsBusy(px, pz)) continue;

                    var at = new Vector3(px, 0f, pz);
                    if (pick < 0.34f) AddTuft(mesh, at, yaw, random.Next(2) == 0 ? Palette.LeafLight : _pineLight);
                    else if (pick < 0.46f) AddFlower(mesh, at, yaw, petals[random.Next(petals.Length)]);
                    else if (pick < 0.50f) mesh.AddBox(at + Vector3.up * 0.06f, new Vector3(0.34f, 0.16f, 0.26f), Quaternion.Euler(0f, yaw, 0f), random.Next(2) == 0 ? _rock : Palette.Stone);
                }
            }
        }

        // Tres briznas (pirámides finas) abiertas en abanico.
        private static void AddTuft(PaletteMeshBuilder mesh, Vector3 at, float yaw, int color)
        {
            for (int blade = 0; blade < 3; blade++)
            {
                Quaternion turn = Quaternion.Euler(0f, yaw + blade * 120f, 0f);
                Vector3 foot = at + turn * new Vector3(0.09f, 0f, 0f);
                mesh.AddCone(foot, 0.075f, foot + turn * new Vector3(0.14f, 0.36f, 0f), 3, color);
            }
        }

        private static void AddFlower(PaletteMeshBuilder mesh, Vector3 at, float yaw, int petal)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            mesh.AddStanding(at, new Vector3(0.04f, 0.26f, 0.04f), turn, Palette.LeafDark);
            mesh.AddBox(at + Vector3.up * 0.3f, new Vector3(0.17f, 0.1f, 0.17f), turn, petal);
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
