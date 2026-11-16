using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmIdle.EditorTools
{
    // Lo que comparten todos los pads del mundo (mejoras, portones, perks, fertilizante, Turbo): medidas, la barra de progreso del pago
    // (sus dos mallas), el objeto del pad con su trigger, su modelo y su cartel, y el cableado común. Viene de la Fase 6 (AR-05).
    // Es una biblioteca: Phase2SceneBuilder.BuildAll llama a CreateAssets; las mallas se rehacen en cada corrida.
    internal static class PadBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";

        private const string FillBackMeshPath = MeshesFolder + "/SM_PadFillBack.asset";
        private const string FillMeshPath = MeshesFolder + "/SM_PadFill.asset";

        internal const float PadSize = 2.6f;        // el pad de una mejora mide 2,6 x 2,6
        internal const float PadTop = 0.14f;
        internal const float TriggerSize = 2.2f;    // un poco más chico que el pad: se "pisa" con el centro del jugador
        private const float LabelHeight = 3f;
        private const float LabelDepth = 0.7f;      // un poco hacia el fondo: así el cartel no tapa el dibujo del pad desde la cámara
        private const float FillWidth = 2.2f;
        private const float FillFront = 0.35f;     // la barra va delante del pad (hacia la cámara)

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            Phase3SceneBuilder.SaveMesh(BuildFillBar(true).ToMesh("SM_PadFillBack"), FillBackMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildFillBar(false).ToMesh("SM_PadFill"), FillMeshPath);
        }

        // La barra de progreso del pago: un fondo oscuro, y la barra que se llena con el origen en su borde izquierdo (así
        // al escalar en X crece de izquierda a derecha).
        private static PaletteMeshBuilder BuildFillBar(bool background)
        {
            var mesh = new PaletteMeshBuilder();

            if (background) mesh.AddBox(new Vector3(0f, 0.04f, 0f), new Vector3(FillWidth + 0.12f, 0.08f, 0.34f), Quaternion.identity, Palette.SoilDark);
            else mesh.AddBox(new Vector3(FillWidth * 0.5f, 0.1f, 0f), new Vector3(FillWidth, 0.1f, 0.22f), Quaternion.identity, Palette.WheatGold);

            return mesh;
        }

        // El pad en sí: objeto con su trigger, su modelo, la barra de progreso y el cartel. Todavía sin cablear (eso es WirePadCommon).
        internal static T BuildPadObject<T>(Transform parent, string name, Vector3 position, Mesh mesh, float size, out Transform visual) where T : PurchasePad
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var pad = new GameObject(name);
            pad.transform.SetParent(parent, false);
            pad.transform.position = position;

            // Trigger de la altura del jugador (2), como los demás pads.
            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(Mathf.Min(TriggerSize, size - 0.4f), 2f, Mathf.Min(TriggerSize, size - 0.4f));

            var component = pad.AddComponent<T>();
            pad.AddComponent<InteractionTrigger>();

            visual = CreateMeshChild(pad.transform, "Visual", mesh, material, Vector3.zero);

            float front = -(size * 0.5f + FillFront);
            CreateMeshChild(pad.transform, "FillBack", Phase2SceneBuilder.LoadRequired<Mesh>(FillBackMeshPath), material, new Vector3(0f, 0f, front));
            CreateMeshChild(pad.transform, "Fill", Phase2SceneBuilder.LoadRequired<Mesh>(FillMeshPath), material, new Vector3(-FillWidth * 0.5f, 0f, front));

            CreateLabel(pad.transform);
            return component;
        }

        internal static void WirePadCommon(PurchasePad pad, Transform visual)
        {
            EditorUi.Wire(pad,
                ("config", Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath)),
                ("label", pad.transform.Find("Label").GetComponent<TextMeshPro>()),
                ("fillBar", pad.transform.Find("Fill")),
                ("padVisual", visual));
        }

        internal static Transform CreateMeshChild(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;   // planos y a ras del suelo
            return child.transform;
        }

        // El cartel en el mundo (no en el Canvas): PurchasePad escribe el texto y lo hace mirar a la cámara.
        internal static void CreateLabel(Transform parent)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var go = new GameObject("Label", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, LabelHeight, LabelDepth);

            var label = go.GetComponent<TextMeshPro>();
            label.font = font;
            label.text = "-";
            label.fontSize = 5.5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(5f, 2f);
            ApplyWorldLabelStyle(label);
        }

        // El material con borde que trae TextMeshPro: uno solo para todos los carteles del mundo.
        internal const string OutlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        // U-12 (contraste): los carteles del mundo flotan sobre pasto, piedra y madera, y el amarillo solo se perdía. Van en un
        // amarillo casi blanco con borde negro, que se lee sobre cualquier fondo.
        internal static void ApplyWorldLabelStyle(TextMeshPro label)
        {
            label.fontSharedMaterial = Phase2SceneBuilder.LoadRequired<Material>(OutlineMaterialPath);
            label.color = new Color(1f, 0.98f, 0.72f);
        }
    }
}
