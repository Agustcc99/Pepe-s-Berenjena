using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // F-19: los ayudantes que se ven. Un peón por cada nivel de GameConfig.Helpers, armado en la escena y APAGADO; HelperCrew los
    // prende según el nivel comprado y HelperWalker los hace dar vueltas por su circuito. Son adorno (como el camión): sin colliders
    // y todo con el material de paleta único. Los circuitos van por el césped libre entre el campo inicial y la zona de venta, sin
    // pisar parcelas, pads, el camión ni el puesto (lo vigila HelperCrewTests).
    // Es una biblioteca: Phase8SceneBuilder llama a CreateAssets (las mallas se rehacen en cada corrida) y a Build.
    internal static class HelperCrewBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const string FootMeshPath = MeshesFolder + "/SM_HelperFoot.asset";
        private const string PileMeshPath = MeshesFolder + "/SM_HelperPile.asset";

        private sealed class WalkerSpec
        {
            public int Overall, Hat;
            public Vector3[] Route;       // circuito cerrado; el punto 0 es donde carga
            public int Unload;            // el punto donde deja la carga
            public Vector3 LoadFacing, UnloadFacing;
        }

        // En el orden de los niveles (Peón, Capataz, Cosechadora). Referencias del mapa: el campo inicial ocupa x ±3,4 y z 3,6..10,4;
        // el pad de venta, x ±1,5 y z -5..-2; el camión, x 1,85..3,05 y z -4,96..-2,04 (la caja mira al sur); el puesto, x -3,25..-1,75.
        private static readonly WalkerSpec[] Specs =
        {
            // Del borde sur del campo (columna este) al costado este de la caja del camión.
            new WalkerSpec
            {
                Overall = Palette.Water, Hat = Palette.WheatStraw,
                Route = new[] { new Vector3(2.4f, 0f, 2.9f), new Vector3(3.9f, 0f, 1f), new Vector3(3.75f, 0f, -3.6f), new Vector3(4.9f, 0f, -1.5f), new Vector3(4.9f, 0f, 1.6f) },
                Unload = 2, LoadFacing = Vector3.forward, UnloadFacing = Vector3.left,
            },
            // El espejo, del lado oeste: del campo (columna oeste) al costado del puesto de verduras.
            new WalkerSpec
            {
                Overall = Palette.TomatoRed, Hat = Palette.Wood,
                Route = new[] { new Vector3(-2.4f, 0f, 2.9f), new Vector3(-3.9f, 0f, 1f), new Vector3(-4.05f, 0f, -3.6f), new Vector3(-5f, 0f, -1.5f), new Vector3(-5f, 0f, 1.6f) },
                Unload = 2, LoadFacing = Vector3.forward, UnloadFacing = Vector3.right,
            },
            // Una vuelta más larga por afuera de la del primero: del borde este del campo al fondo de la caja del camión.
            new WalkerSpec
            {
                Overall = Palette.LeafDark, Hat = Palette.WheatGold,
                Route = new[] { new Vector3(3.95f, 0f, 4.6f), new Vector3(5.8f, 0f, 3.4f), new Vector3(5.8f, 0f, -4.7f), new Vector3(3.75f, 0f, -4.7f), new Vector3(4.6f, 0f, -5.8f), new Vector3(6.4f, 0f, -5.8f), new Vector3(6.4f, 0f, 3.9f) },
                Unload = 3, LoadFacing = Vector3.left, UnloadFacing = Vector3.left,
            },
        };

        private static string BodyMeshPath(int level) { return MeshesFolder + "/SM_Helper_" + level + ".asset"; }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            for (int i = 0; i < Specs.Length; i++)
            {
                Phase3SceneBuilder.SaveMesh(BuildBody(Specs[i]).ToMesh("SM_Helper_" + (i + 1)), BodyMeshPath(i + 1));
            }

            Phase3SceneBuilder.SaveMesh(BuildFoot().ToMesh("SM_HelperFoot"), FootMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildPile().ToMesh("SM_HelperPile"), PileMeshPath);
        }

        // El muñeco, con el origen al pie y de frente hacia +z: overol, cabeza con nariz, manos y sombrero. Más bajo que el jugador
        // (que mide 2) para que no se confundan.
        private static PaletteMeshBuilder BuildBody(WalkerSpec spec)
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion none = Quaternion.identity;

            mesh.AddSphere(new Vector3(0f, 0.42f, 0f), 0.32f, spec.Overall);
            mesh.AddCylinder(new Vector3(0f, 0.6f, 0f), 0.32f, 0.36f, 12, none, spec.Overall);
            mesh.AddSphere(new Vector3(0f, 0.78f, 0f), 0.32f, spec.Overall);
            mesh.AddSphere(new Vector3(0f, 1.2f, 0f), 0.26f, Palette.CarrotOrange);
            mesh.AddBox(new Vector3(0f, 1.18f, 0.26f), new Vector3(0.12f, 0.12f, 0.12f), none, Palette.Cream);
            mesh.AddSphere(new Vector3(-0.4f, 0.68f, 0.04f), 0.1f, Palette.CarrotOrange);
            mesh.AddSphere(new Vector3(0.4f, 0.68f, 0.04f), 0.1f, Palette.CarrotOrange);
            mesh.AddCylinder(new Vector3(0f, 1.4f, 0f), 0.46f, 0.05f, 12, none, spec.Hat);
            mesh.AddCone(new Vector3(0f, 1.42f, 0f), 0.27f, new Vector3(0f, 1.66f, 0f), 8, spec.Hat);
            return mesh;
        }

        private static PaletteMeshBuilder BuildFoot()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddBox(new Vector3(0f, 0.07f, 0.04f), new Vector3(0.2f, 0.14f, 0.32f), Quaternion.identity, Palette.SoilDark);
            return mesh;
        }

        // La pila chica de la espalda: dos cajones, el de arriba con producto a la vista. Origen en la base.
        private static PaletteMeshBuilder BuildPile()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion none = Quaternion.identity;

            mesh.AddBox(new Vector3(0f, 0.13f, 0f), new Vector3(0.5f, 0.26f, 0.36f), none, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 0.38f, 0f), new Vector3(0.44f, 0.22f, 0.32f), none, Palette.SoilLight);
            mesh.AddSphere(new Vector3(-0.11f, 0.5f, 0f), 0.1f, Palette.TomatoRed);
            mesh.AddSphere(new Vector3(0.1f, 0.5f, 0.03f), 0.1f, Palette.WheatGold);
            mesh.AddSphere(new Vector3(0f, 0.52f, -0.07f), 0.09f, Palette.LeafLight);
            return mesh;
        }

        // ---------- Escena ----------

        internal static void Build(Transform parent)
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);
            var footMesh = Phase2SceneBuilder.LoadRequired<Mesh>(FootMeshPath);
            var pileMesh = Phase2SceneBuilder.LoadRequired<Mesh>(PileMeshPath);

            var root = new GameObject("HelperCrew");
            root.transform.SetParent(parent, false);
            root.transform.position = Vector3.zero;   // los circuitos están en coordenadas del mundo
            var crew = root.AddComponent<HelperCrew>();

            var walkers = new List<Object>();
            for (int i = 0; i < Specs.Length; i++)
            {
                WalkerSpec spec = Specs[i];

                var walker = new GameObject("Helper_" + (i + 1));
                walker.transform.SetParent(root.transform, false);
                walker.transform.localPosition = spec.Route[0];

                Transform body = PadBuilder.CreateMeshChild(walker.transform, "Body", Phase2SceneBuilder.LoadRequired<Mesh>(BodyMeshPath(i + 1)), material, Vector3.zero);
                body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;   // es lo único del peón que da sombra

                Transform leftFoot = PadBuilder.CreateMeshChild(walker.transform, "FootLeft", footMesh, material, new Vector3(-0.16f, 0f, 0f));
                Transform rightFoot = PadBuilder.CreateMeshChild(walker.transform, "FootRight", footMesh, material, new Vector3(0.16f, 0f, 0f));

                // La pila cuelga del cuerpo (rebota con él), pegada a la espalda; HelperWalker la muestra mientras lleva carga.
                Transform pile = PadBuilder.CreateMeshChild(body, "Pile", pileMesh, material, new Vector3(0f, 0.5f, -0.5f));
                pile.localScale = Vector3.zero;

                var behaviour = walker.AddComponent<HelperWalker>();
                EditorUi.Wire(behaviour, ("body", body), ("leftFoot", leftFoot), ("rightFoot", rightFoot), ("pile", pile));

                var serialized = new SerializedObject(behaviour);
                SerializedProperty route = serialized.FindProperty("route");
                route.arraySize = spec.Route.Length;
                for (int point = 0; point < spec.Route.Length; point++) route.GetArrayElementAtIndex(point).vector3Value = spec.Route[point];
                serialized.FindProperty("unloadIndex").intValue = spec.Unload;
                serialized.FindProperty("loadFacing").vector3Value = spec.LoadFacing;
                serialized.FindProperty("unloadFacing").vector3Value = spec.UnloadFacing;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                walker.SetActive(false);   // HelperCrew lo prende al comprar su nivel
                walkers.Add(walker);
            }

            EditorUi.WireArray(crew, "walkers", walkers);
        }
    }
}
