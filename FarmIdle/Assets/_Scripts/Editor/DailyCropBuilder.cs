using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // IN-02: las piezas del cultivo del día. La estrella dorada que flota sobre la fila de cada campo (DailyCropMarker, una por fila: cada
    // fila es un cultivo) y el texto del cartel de la entrada de la plaza (DailyCropSign). Es una biblioteca: Phase2SceneBuilder.BuildAll
    // llama a CreateAssets; Phase3SceneBuilder (campo inicial) y ZoneBuilder (zonas) llaman a AddMarkers; PlazaBuilder, a BuildSign.
    internal static class DailyCropBuilder
    {
        private const string StarMeshPath = "Assets/Meshes/Environment/SM_DailyStar.asset";
        private const float MarkerOffsetX = 1.7f;   // cuánto afuera del borde izquierdo del campo flota la estrella
        private const float MarkerHeight = 1.5f;

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder("Assets/Meshes/Environment");
            Phase3SceneBuilder.SaveMesh(BuildStar().ToMesh("SM_DailyStar"), StarMeshPath);
        }

        // Una estrella de ocho puntas hecha con cuatro barras cruzadas, de cara a la cámara, con un botón claro en el centro.
        private static PaletteMeshBuilder BuildStar()
        {
            var mesh = new PaletteMeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float length = i % 2 == 0 ? 0.9f : 0.62f;
                mesh.AddBox(Vector3.zero, new Vector3(length, 0.14f, 0.14f), Quaternion.Euler(0f, 0f, i * 45f), Palette.WheatGold);
            }

            mesh.AddSphere(Vector3.zero, 0.17f, Palette.Cream);
            return mesh;
        }

        // Una estrella por fila del campo (cuelga del campo: solo se ve con la zona abierta). 'plots' va fila por fila, como los arma
        // Phase3SceneBuilder.CreatePlot.
        internal static void AddMarkers(Transform field, IReadOnlyList<Object> plots)
        {
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(StarMeshPath);
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            int rows = Phase3SceneBuilder.FieldRows;
            int columns = Phase3SceneBuilder.FieldColumns;
            for (int row = 0; row < rows; row++)
            {
                var anchor = new GameObject("DailyMarker_r" + row);
                anchor.transform.SetParent(field, false);
                anchor.transform.localPosition = new Vector3(-(columns - 1) * 0.5f * Phase3SceneBuilder.PlotPitch - MarkerOffsetX, 0f, (row - (rows - 1) * 0.5f) * Phase3SceneBuilder.PlotPitch);

                var icon = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer));
                icon.transform.SetParent(anchor.transform, false);
                icon.transform.localPosition = new Vector3(0f, MarkerHeight, 0f);
                icon.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = icon.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                icon.SetActive(false);   // DailyCropMarker la prende

                var marker = anchor.AddComponent<DailyCropMarker>();
                EditorUi.Wire(marker, ("referencePlot", plots[row * columns]), ("icon", icon.transform));
            }
        }

        // El texto del cartel de la entrada de la plaza: el tablón crema que dibuja PlazaBuilder.BuildPlazaDecor, en el centro de la entrada.
        internal static void BuildSign(Transform parent, Vector3 position)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var go = new GameObject("SignLabel_dailycrop", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var label = go.GetComponent<TextMeshPro>();
            label.font = font;
            label.text = Loc.Get("daily.sign_none", GameLanguage.Spanish);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.25f, 0.15f, 0.05f);   // sobre el tablón crema: marrón oscuro
            label.enableAutoSizing = true;
            label.fontSizeMin = 1f;
            label.fontSizeMax = 3.2f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(1.4f, 0.7f);

            go.AddComponent<DailyCropSign>();
        }
    }
}
