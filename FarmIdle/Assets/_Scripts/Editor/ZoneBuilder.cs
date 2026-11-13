using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Las zonas del mundo: cada terreno cerrado (losa vallada con candado), el campo de 3 x 3 parcelas que queda apagado hasta
    // desbloquearlo y el portón entre el campo inicial y la zona. Viene de la Fase 6 (AR-05). Dónde está cada zona y qué cultivo
    // tiene son las tablas de ProgressionData; lo que comparten todos los pads, de PadBuilder.
    // Es una biblioteca: Phase2SceneBuilder.BuildAll llama a CreateAssets (las mallas se rehacen en cada corrida) y la escena, a Build.
    internal static class ZoneBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";

        private const string GateMeshPath = MeshesFolder + "/SM_ZoneGate.asset";
        private const string LockedMeshPath = MeshesFolder + "/SM_ZoneLocked.asset";

        private const float GateSize = 3f;         // el portón de una zona, 3 x 3
        private const float ZoneSlab = 8f;         // el terreno cerrado: el campo (3 x 3 a paso 2,4) más sus marcos

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            Phase3SceneBuilder.SaveMesh(BuildGate().ToMesh("SM_ZoneGate"), GateMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildLockedZone().ToMesh("SM_ZoneLocked"), LockedMeshPath);
        }

        // El portón de una zona: pad de piedra con dos postes y un travesaño, como una entrada.
        private static PaletteMeshBuilder BuildGate()
        {
            var mesh = new PaletteMeshBuilder();
            const float frame = 0.12f;
            const float padTop = PadBuilder.PadTop;

            mesh.AddBox(new Vector3(0f, frame * 0.5f, 0f), new Vector3(GateSize, frame, GateSize), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0f, padTop * 0.5f, 0f), new Vector3(GateSize - 0.3f, padTop, GateSize - 0.3f), Quaternion.identity, Palette.Cream);
            mesh.AddCylinder(new Vector3(0f, padTop + 0.01f, 0f), 1.05f, 0.02f, 16, Quaternion.identity, Palette.Stone);
            mesh.AddCylinder(new Vector3(0f, padTop + 0.02f, 0f), 0.9f, 0.02f, 16, Quaternion.identity, Palette.Cream);

            // Un candado grande en el piso.
            mesh.AddBox(new Vector3(0f, padTop + 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.28f), Quaternion.identity, Palette.WheatGold);
            mesh.AddBox(new Vector3(-0.2f, padTop + 0.62f, 0f), new Vector3(0.1f, 0.28f, 0.12f), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0.2f, padTop + 0.62f, 0f), new Vector3(0.1f, 0.28f, 0.12f), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0f, padTop + 0.78f, 0f), new Vector3(0.5f, 0.1f, 0.12f), Quaternion.identity, Palette.Stone);

            // Los postes a los costados (no tapan el cartel: este va más arriba).
            mesh.AddBox(new Vector3(-1.4f, 0.7f, 0f), new Vector3(0.2f, 1.4f, 0.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(1.4f, 0.7f, 0f), new Vector3(0.2f, 1.4f, 0.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 1.45f, 0f), new Vector3(3.1f, 0.16f, 0.2f), Quaternion.identity, Palette.Wood);
            return mesh;
        }

        // El terreno cerrado: una losa gris con una valla de madera alrededor y un candado en el medio. Ocupa el lugar del campo.
        private static PaletteMeshBuilder BuildLockedZone()
        {
            var mesh = new PaletteMeshBuilder();
            float half = ZoneSlab * 0.5f;

            mesh.AddBox(new Vector3(0f, 0.04f, 0f), new Vector3(ZoneSlab, 0.08f, ZoneSlab), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0f, 0.09f, 0f), new Vector3(ZoneSlab - 0.8f, 0.04f, ZoneSlab - 0.8f), Quaternion.identity, Palette.SoilDark);

            // Valla: postas cada 1,33 y dos varas por lado.
            for (int i = -3; i <= 3; i++)
            {
                float a = i * (ZoneSlab - 0.4f) / 6f;
                mesh.AddBox(new Vector3(a, 0.45f, half - 0.1f), new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity, Palette.Wood);
                mesh.AddBox(new Vector3(a, 0.45f, -half + 0.1f), new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity, Palette.Wood);
                mesh.AddBox(new Vector3(half - 0.1f, 0.45f, a), new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity, Palette.Wood);
                mesh.AddBox(new Vector3(-half + 0.1f, 0.45f, a), new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity, Palette.Wood);
            }

            foreach (float rail in new[] { 0.35f, 0.7f })
            {
                mesh.AddBox(new Vector3(0f, rail, half - 0.1f), new Vector3(ZoneSlab - 0.2f, 0.09f, 0.08f), Quaternion.identity, Palette.WheatStraw);
                mesh.AddBox(new Vector3(0f, rail, -half + 0.1f), new Vector3(ZoneSlab - 0.2f, 0.09f, 0.08f), Quaternion.identity, Palette.WheatStraw);
                mesh.AddBox(new Vector3(half - 0.1f, rail, 0f), new Vector3(0.08f, 0.09f, ZoneSlab - 0.2f), Quaternion.identity, Palette.WheatStraw);
                mesh.AddBox(new Vector3(-half + 0.1f, rail, 0f), new Vector3(0.08f, 0.09f, ZoneSlab - 0.2f), Quaternion.identity, Palette.WheatStraw);
            }

            // Candado grande en el medio.
            mesh.AddBox(new Vector3(0f, 0.7f, 0f), new Vector3(1.5f, 1.1f, 0.5f), Quaternion.identity, Palette.WheatGold);
            mesh.AddBox(new Vector3(-0.42f, 1.55f, 0f), new Vector3(0.22f, 0.7f, 0.22f), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0.42f, 1.55f, 0f), new Vector3(0.22f, 0.7f, 0.22f), Quaternion.identity, Palette.Stone);
            mesh.AddBox(new Vector3(0f, 1.95f, 0f), new Vector3(1.06f, 0.22f, 0.22f), Quaternion.identity, Palette.Stone);
            return mesh;
        }

        // ---------- Escena ----------

        // Agrega a la escena abierta las zonas bloqueadas, cada una con su portón y su campo apagado. Los assets se recargan por ruta
        // justo antes de usarlos.
        internal static void Build(Transform parent)
        {
            foreach (ProgressionData.ZoneSpec zone in ProgressionData.Zones) BuildZone(parent, zone);
        }

        private static void BuildZone(Transform parent, ProgressionData.ZoneSpec spec)
        {
            var zoneData = Phase2SceneBuilder.LoadRequired<ZoneData>(ProgressionData.ZonePath(spec));
            var lockedMesh = Phase2SceneBuilder.LoadRequired<Mesh>(LockedMeshPath);
            var soilMesh = Phase2SceneBuilder.LoadRequired<Mesh>("Assets/Meshes/Plots/SM_PlotSoil.asset");
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var zoneObject = new GameObject(spec.File);
            zoneObject.transform.SetParent(parent, false);

            // La mitad cerrada: terreno vallado con candado.
            var locked = new GameObject("Locked", typeof(MeshFilter), typeof(MeshRenderer));
            locked.transform.SetParent(zoneObject.transform, false);
            locked.transform.position = spec.Center;
            locked.GetComponent<MeshFilter>().sharedMesh = lockedMesh;
            var lockedRenderer = locked.GetComponent<MeshRenderer>();
            lockedRenderer.sharedMaterial = material;
            lockedRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // La mitad abierta: el campo de 3 x 3 parcelas, apagado hasta desbloquear. Sus plantas salen del pool.
            var field = new GameObject("Field");
            field.transform.SetParent(zoneObject.transform, false);
            field.transform.position = spec.Center;

            var plots = new List<Object>();
            for (int row = 0; row < Phase3SceneBuilder.FieldRows; row++)
            {
                var crop = Phase2SceneBuilder.LoadRequired<CropData>(Phase3SceneBuilder.CropPath(spec.Rows[row]));
                for (int column = 0; column < Phase3SceneBuilder.FieldColumns; column++)
                {
                    plots.Add(Phase3SceneBuilder.CreatePlot(field.transform, row, column, crop, soilMesh, material));
                }
            }

            var plotField = field.AddComponent<PlotField>();
            EditorUi.WireArray(plotField, "plots", plots);
            field.SetActive(false);

            var controller = zoneObject.AddComponent<ZoneController>();
            EditorUi.Wire(controller, ("zone", zoneData), ("lockedRoot", locked), ("unlockedRoot", field));

            // El portón entre el campo inicial y la zona.
            var gate = PadBuilder.BuildPadObject<ZoneUnlockPad>(zoneObject.transform, "Gate", spec.Gate, Phase2SceneBuilder.LoadRequired<Mesh>(GateMeshPath), GateSize, out Transform visual);
            EditorUi.Wire(gate, ("zone", zoneData));
            PadBuilder.WirePadCommon(gate, visual);
        }
    }
}
