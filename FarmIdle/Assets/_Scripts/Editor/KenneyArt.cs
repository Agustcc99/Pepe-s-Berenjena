using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Arte de Kenney (Nature Kit y Food Kit, CC0, en Assets/Models_3D/Kenney) para plantas y productos.
    // No se usan los modelos tal cual: cada kit trae su propio material (el Nature Kit, un color plano por material; el
    // Food Kit, una textura "colormap"), y eso serían varios materiales. En cambio se "hornean" a la paleta única del
    // juego (Palette + M_Palette): cada triángulo toma su color de Kenney (del material, o del colormap en el punto
    // de su UV) y apunta a una celda de la paleta, igual que las mallas provisorias. Resultado: todos los modelos
    // comparten un solo material y un solo draw call, y las plantas y los productos pueden mezclarse sin cambiar de
    // textura.
    // Cada planta o producto se compone con una o varias piezas (modelo + posición + giro + escala). Si falta un FBX,
    // ese cultivo no tiene arte de Kenney y el builder usa el provisorio: el resto del código no se entera.
    // Uso: Prepare() una vez al empezar la corrida (antes de generar la textura de la paleta); después Plant()/Product().
    internal static class KenneyArt
    {
        private const string NatureFolder = "Assets/Models_3D/Kenney/NatureKit/";
        private const string FoodFolder = "Assets/Models_3D/Kenney/FoodKit/";

        // Un modelo dentro de la composición. 'colorOverrides' cambia el color de un material por nombre (índice de la
        // paleta) cuando el color de Kenney no sirve para este cultivo (por ejemplo, el trigo maduro es blanco).
        private readonly struct Piece
        {
            public readonly string Path;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Scale;
            public readonly (string material, int color)[] ColorOverrides;

            public Piece(string path, Vector3 position, float yawDegrees, float scale, params (string material, int color)[] colorOverrides)
                : this(path, position, Quaternion.Euler(0f, yawDegrees, 0f), scale, colorOverrides)
            {
            }

            public Piece(string path, Vector3 position, Quaternion rotation, float scale, params (string material, int color)[] colorOverrides)
            {
                Path = path;
                Position = position;
                Rotation = rotation;
                Scale = scale;
                ColorOverrides = colorOverrides;
            }
        }

        private struct Triangle
        {
            public Vector3 A, B, C;
            public Vector3 NormalA, NormalB, NormalC;
            public int Color;
        }

        private static readonly Dictionary<string, PaletteMeshBuilder> Builders = new Dictionary<string, PaletteMeshBuilder>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        // Colores de la paleta que reemplazan a los de Kenney donde no leen bien.
        private static readonly (string, int)[] RipeWheat = { ("_defaultMat", Palette.WheatGold), ("woodInner", Palette.WheatStraw) };
        private static readonly (string, int)[] RipeCarrot = { ("leafsFall", Palette.CarrotOrange) };

        // ---------- API ----------

        // Compone todas las plantas y productos y registra sus colores en la paleta. Tiene que correr ANTES de generar
        // la textura de la paleta (Phase3SceneBuilder.CreateAssets), porque ahí se escriben los colores registrados.
        internal static void Prepare()
        {
            Palette.ResetExtras();
            Builders.Clear();
            ReleaseTextures();

            Compose("plant/Wheat/Growing", WheatPieces(ripe: false));
            Compose("plant/Wheat/Ready", WheatPieces(ripe: true));
            Compose("plant/Carrot/Growing", CarrotPieces(ripe: false));
            Compose("plant/Carrot/Ready", CarrotPieces(ripe: true));
            Compose("plant/Tomato/Growing", TomatoPieces(ripe: false));
            Compose("plant/Tomato/Ready", TomatoPieces(ripe: true));

            // Productos acostados y centrados en el origen, como los provisorios. El trigo no tiene producto en Kenney:
            // el Food Kit no trae trigo y el atado de espigas del Nature Kit es la planta parada.
            Compose("product/Carrot", new[] { new Piece(FoodFolder + "carrot.fbx", Vector3.zero, Quaternion.Euler(0f, 0f, -90f), 1.05f) }, recenter: true);
            Compose("product/Tomato", new[] { new Piece(FoodFolder + "tomato.fbx", Vector3.zero, 0f, 1.9f) }, recenter: true);

            ReleaseTextures();
        }

        // La malla de la planta ('Growing' o 'Ready') o null si ese cultivo no tiene arte de Kenney.
        // Devuelve un constructor nuevo cada vez (ToMesh crea una malla propia).
        internal static PaletteMeshBuilder Plant(string crop, bool ripe)
        {
            return Builders.TryGetValue("plant/" + crop + "/" + (ripe ? "Ready" : "Growing"), out PaletteMeshBuilder builder) ? builder : null;
        }

        internal static PaletteMeshBuilder Product(string crop)
        {
            return Builders.TryGetValue("product/" + crop, out PaletteMeshBuilder builder) ? builder : null;
        }

        // ---------- Composiciones ----------

        // Cuatro matas de trigo repartidas en la parcela (la tierra mide unos 1,76 de lado).
        private static Piece[] WheatPieces(bool ripe)
        {
            string model = NatureFolder + (ripe ? "crops_wheatStageB.fbx" : "crops_wheatStageA.fbx");
            (string, int)[] overrides = ripe ? RipeWheat : new (string, int)[0];
            return new[]
            {
                new Piece(model, new Vector3(-0.44f, 0f, -0.42f), 0f, 1.5f, overrides),
                new Piece(model, new Vector3(0.44f, 0f, -0.46f), 90f, 1.5f, overrides),
                new Piece(model, new Vector3(-0.42f, 0f, 0.44f), 180f, 1.5f, overrides),
                new Piece(model, new Vector3(0.46f, 0f, 0.42f), 270f, 1.5f, overrides),
            };
        }

        // Cuatro zanahorias: de hojas mientras crece, con la raíz asomando al madurar.
        private static Piece[] CarrotPieces(bool ripe)
        {
            string model = NatureFolder + (ripe ? "crop_carrot.fbx" : "crops_leafsStageA.fbx");
            (string, int)[] overrides = ripe ? RipeCarrot : new (string, int)[0];
            return new[]
            {
                new Piece(model, new Vector3(-0.44f, 0f, -0.44f), 20f, 1.6f, overrides),
                new Piece(model, new Vector3(0.44f, 0f, -0.44f), 110f, 1.6f, overrides),
                new Piece(model, new Vector3(-0.44f, 0f, 0.44f), 200f, 1.6f, overrides),
                new Piece(model, new Vector3(0.44f, 0f, 0.44f), 290f, 1.6f, overrides),
            };
        }

        // El Nature Kit no trae tomatera: la mata son tres brotes de hojas y, al madurar, se le suman tomates del Food Kit.
        private static Piece[] TomatoPieces(bool ripe)
        {
            string leaves = NatureFolder + "crops_leafsStageB.fbx";
            string tomato = FoodFolder + "tomato.fbx";

            var pieces = new List<Piece>
            {
                new Piece(leaves, new Vector3(0f, 0f, 0.28f), 0f, 1.5f),
                new Piece(leaves, new Vector3(-0.27f, 0f, -0.18f), 120f, 1.5f),
                new Piece(leaves, new Vector3(0.27f, 0f, -0.18f), 240f, 1.5f),
            };

            if (ripe)
            {
                pieces.Add(new Piece(tomato, new Vector3(-0.30f, 0.16f, -0.52f), 0f, 1.7f));
                pieces.Add(new Piece(tomato, new Vector3(0.26f, 0.18f, -0.56f), 70f, 1.7f));
                pieces.Add(new Piece(tomato, new Vector3(-0.58f, 0.2f, 0.08f), 150f, 1.7f));
                pieces.Add(new Piece(tomato, new Vector3(0.58f, 0.2f, 0.02f), 210f, 1.7f));
                pieces.Add(new Piece(tomato, new Vector3(0.0f, 0.44f, -0.2f), 280f, 1.6f));
                pieces.Add(new Piece(tomato, new Vector3(0.14f, 0.3f, 0.36f), 320f, 1.6f));
            }

            return pieces.ToArray();
        }

        // ---------- Composición ----------

        private static void Compose(string key, Piece[] pieces, bool recenter = false)
        {
            var triangles = new List<Triangle>();
            foreach (Piece piece in pieces)
            {
                if (!AddPiece(triangles, piece))
                {
                    Debug.LogWarning("[FarmIdle] KenneyArt: falta " + piece.Path + "; '" + key + "' queda con el arte provisorio.");
                    return;
                }
            }

            if (triangles.Count == 0) return;

            Vector3 offset = Vector3.zero;
            if (recenter)
            {
                var bounds = new Bounds(triangles[0].A, Vector3.zero);
                foreach (Triangle t in triangles)
                {
                    bounds.Encapsulate(t.A);
                    bounds.Encapsulate(t.B);
                    bounds.Encapsulate(t.C);
                }

                offset = -bounds.center;
            }

            var builder = new PaletteMeshBuilder();
            foreach (Triangle t in triangles)
            {
                builder.AddModelTriangle(t.A + offset, t.B + offset, t.C + offset, t.NormalA, t.NormalB, t.NormalC, t.Color);
            }

            Builders[key] = builder;
        }

        // Pasa los triángulos del modelo a la composición (ya movidos y escalados) con su color en la paleta.
        // Devuelve false si el modelo no está en el proyecto.
        private static bool AddPiece(List<Triangle> into, Piece piece)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(piece.Path);
            if (model == null) return false;

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null) continue;

                // Del espacio del submodelo al del modelo (por si el FBX trae la malla dentro de un hijo con su propia escala).
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 place = Matrix4x4.TRS(piece.Position, piece.Rotation, Vector3.one * piece.Scale) * toRoot;

                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                Material[] materials = renderer.sharedMaterials;

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material material = sub < materials.Length ? materials[sub] : materials[materials.Length - 1];
                    int[] indices = mesh.GetTriangles(sub);

                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        int color = PaletteColorOf(material, piece, uvs.Length > 0 ? (uvs[a] + uvs[b] + uvs[c]) / 3f : Vector2.zero);

                        into.Add(new Triangle
                        {
                            A = place.MultiplyPoint3x4(vertices[a]),
                            B = place.MultiplyPoint3x4(vertices[b]),
                            C = place.MultiplyPoint3x4(vertices[c]),
                            NormalA = place.MultiplyVector(normals[a]).normalized,
                            NormalB = place.MultiplyVector(normals[b]).normalized,
                            NormalC = place.MultiplyVector(normals[c]).normalized,
                            Color = color,
                        });
                    }
                }
            }

            return true;
        }

        // El color de Kenney de un triángulo -> su celda de la paleta. Con colormap (Food Kit) el color sale de la
        // textura en el centro de la UV del triángulo; sin textura (Nature Kit) es el color del material.
        private static int PaletteColorOf(Material material, Piece piece, Vector2 uv)
        {
            foreach ((string name, int color) in piece.ColorOverrides)
            {
                if (material.name == name) return color;
            }

            Color32 source;
            Texture2D colormap = LoadReadableTexture(material.mainTexture);
            if (colormap != null)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * colormap.width), 0, colormap.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * colormap.height), 0, colormap.height - 1);
                source = colormap.GetPixel(x, y);
            }
            else
            {
                source = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            }

            return Palette.Register(source);
        }

        // La textura importada no es legible desde código: se vuelve a leer el archivo como una copia temporal.
        private static Texture2D LoadReadableTexture(Texture texture)
        {
            if (texture == null) return null;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return null;
            if (Textures.TryGetValue(path, out Texture2D cached)) return cached;

            var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!copy.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw new InvalidOperationException("No se pudo leer la textura " + path + ".");
            }

            Textures[path] = copy;
            return copy;
        }

        private static void ReleaseTextures()
        {
            foreach (Texture2D texture in Textures.Values)
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }

            Textures.Clear();
        }
    }
}
