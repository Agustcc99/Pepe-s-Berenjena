using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FarmIdle.EditorTools
{
    // F-18 (tanda 2): lo visible de los pedidos del pueblo. En el mundo, el cartel de madera y sus tres pads de entrega (uno por
    // lugar de OrderBoard; el último es el pedido grande), en el césped entre el campo inicial y la plaza, al oeste de la avenida.
    // En el HUD, la tarjeta con los pedidos activos. La lógica es de OrdersManager (tanda 1).
    internal static class OrdersBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const string BoardMeshPath = MeshesFolder + "/SM_OrdersBoard.asset";

        // El centro de la fila de pads y la separación entre ellos (los pads miden PadBuilder.PadSize).
        internal const float RowZ = -7.6f;
        internal const float CenterX = -6.9f;
        private const float Spacing = 2.9f;

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            Phase3SceneBuilder.SaveMesh(BuildBoardMesh().ToMesh("SM_OrdersBoard"), BoardMeshPath);
        }

        // El dibujo del pad de entrega (sobre la plataforma común de Phase8SceneBuilder): un cajón de madera abierto con verdura
        // asomando y una nota pinchada al frente.
        internal static void AddPadIcon(PaletteMeshBuilder mesh, float y)
        {
            Quaternion none = Quaternion.identity;
            mesh.AddBox(new Vector3(0f, y + 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.8f), none, Palette.Wood);
            mesh.AddBox(new Vector3(0f, y + 0.62f, 0f), new Vector3(0.94f, 0.06f, 0.64f), none, Palette.SoilDark);
            mesh.AddSphere(new Vector3(-0.25f, y + 0.7f, 0.05f), 0.2f, Palette.TomatoRed);
            mesh.AddSphere(new Vector3(0.2f, y + 0.72f, -0.08f), 0.22f, Palette.Purple);
            mesh.AddCone(new Vector3(0.05f, y + 0.62f, 0.18f), 0.12f, new Vector3(0.3f, y + 1.0f, 0.22f), 6, Palette.CarrotOrange);
            mesh.AddBox(new Vector3(0f, y + 0.32f, -0.42f), new Vector3(0.5f, 0.34f, 0.03f), none, Palette.Cream);
            mesh.AddBox(new Vector3(0f, y + 0.36f, -0.44f), new Vector3(0.34f, 0.04f, 0.02f), none, Palette.Stone);
            mesh.AddBox(new Vector3(0f, y + 0.27f, -0.44f), new Vector3(0.34f, 0.04f, 0.02f), none, Palette.Stone);
        }

        // El cartel del pueblo: dos postes, un tablón con tres notas de colores y un techito. De cara a la cámara (hacia -z).
        private static PaletteMeshBuilder BuildBoardMesh()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion none = Quaternion.identity;

            for (int side = -1; side <= 1; side += 2)
            {
                mesh.AddStanding(new Vector3(side * 1.7f, 0f, 0f), new Vector3(0.2f, 2.6f, 0.2f), none, Palette.Wood);
            }

            mesh.AddBox(new Vector3(0f, 1.65f, 0f), new Vector3(3.6f, 1.5f, 0.12f), none, Palette.SoilLight);
            mesh.AddBox(new Vector3(0f, 2.62f, -0.05f), new Vector3(4.2f, 0.14f, 0.7f), Quaternion.Euler(-14f, 0f, 0f), Palette.TomatoDark);

            int[] notes = { Palette.Cream, Palette.WheatGold, Palette.White };
            for (int i = 0; i < notes.Length; i++)
            {
                float x = (i - 1) * 1.1f;
                mesh.AddBox(new Vector3(x, 1.65f + (i == 1 ? 0.08f : -0.04f), -0.08f), new Vector3(0.8f, 1.0f, 0.03f), Quaternion.Euler(0f, 0f, (i - 1) * -4f), notes[i]);
                mesh.AddSphere(new Vector3(x, 2.08f + (i == 1 ? 0.08f : -0.04f), -0.11f), 0.06f, Palette.TomatoRed);
            }

            return mesh;
        }

        internal static void BuildWorldPieces()
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var root = new GameObject("OrdersBoard");

            Transform board = PadBuilder.CreateMeshChild(root.transform, "Board", Phase2SceneBuilder.LoadRequired<Mesh>(BoardMeshPath), material, new Vector3(CenterX, 0f, RowZ + 2.0f));
            board.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            for (int slot = 0; slot < OrderBoard.SlotCount; slot++)
            {
                // De izquierda a derecha en pantalla: lugar 0, 1 y el grande.
                var position = new Vector3(CenterX + (slot - 1) * Spacing, 0f, RowZ);
                OrderPad pad = Phase8SceneBuilder.BuildDwellPad<OrderPad>(root.transform, "OrderPad_" + slot, position, Phase8SceneBuilder.PadIcon.Order);

                var serialized = new SerializedObject(pad);
                serialized.FindProperty("slot").intValue = slot;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // La tarjeta de pedidos activos: a la izquierda, debajo de las filas de buffs y fertilizante.
        internal static void BuildHud(RectTransform hud)
        {
            RectTransform root = EditorUi.CreateRect("OrdersHud", hud);
            EditorUi.Anchor(root, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(20f, -50f), new Vector2(450f, 190f));   // más ancha: cada línea trae el premio

            Image card = EditorUi.CreatePanel("Card", root, new Color(0.05f, 0.07f, 0.06f, 0.65f), false);
            EditorUi.Stretch(card.rectTransform);

            TextMeshProUGUI text = EditorUi.CreateText("Text", card.transform, "", 32f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 32f;
            text.raycastTarget = false;
            EditorUi.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 10f);
            text.rectTransform.offsetMax = new Vector2(-12f, -10f);

            card.gameObject.SetActive(false);

            // El aviso de "pedido completado" con lo ganado: arriba del jugador, unos segundos. No recibe toques.
            Image done = EditorUi.CreatePanel("OrderDone", hud, new Color(0.05f, 0.07f, 0.06f, 0.78f), false);
            EditorUi.Anchor(done.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(760f, 170f));
            TextMeshProUGUI doneText = EditorUi.CreateText("Text", done.transform, "", 52f, new Color(1f, 0.88f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            doneText.enableAutoSizing = true;
            doneText.fontSizeMin = 30f;
            doneText.fontSizeMax = 52f;
            doneText.raycastTarget = false;
            EditorUi.Stretch(doneText.rectTransform);
            doneText.rectTransform.offsetMin = new Vector2(16f, 8f);
            doneText.rectTransform.offsetMax = new Vector2(-16f, -8f);
            done.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<OrdersHudView>();
            EditorUi.Wire(view, ("card", card.gameObject), ("text", text), ("completedLabel", doneText), ("completedRoot", done.gameObject));
        }
    }
}
