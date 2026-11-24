using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // ROADMAP U-04: los pads no pueden depender solo del color. Cada pad de compra (mejoras, perks, fertilizante, portón de zona)
    // lleva su propio dibujo en 3D sobre la misma plataforma; si dos pads tuvieran la misma forma, en escala de grises serían
    // indistinguibles. Se compara la geometría (cantidad de vértices, tamaño y posición de cada vértice), no los colores.
    public class PadShapeTests
    {
        private static readonly string[] PadMeshes =
        {
            "SM_UpgradePad_GrowthSpeed", "SM_UpgradePad_BackpackCapacity", "SM_UpgradePad_MoveSpeed", "SM_UpgradePad_SellPrice", "SM_UpgradePad_OfflineCap",
            "SM_PerkPad_BackpackCapacity", "SM_PerkPad_MoveSpeed", "SM_PerkPad_RareChance", "SM_PerkPad_SellPrice", "SM_PerkPad_GrowthSpeed",
            "SM_Reward_Use", "SM_Reward_Buy", "SM_Reward_Video", "SM_Reward_Turbo", "SM_Reward_Decor", "SM_Reward_Helper", "SM_Reward_Skin", "SM_Reward_Order",
            "SM_ZoneGate", "SM_SellPad"
        };

        private static string Signature(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            long hash = 17;
            for (int i = 0; i < vertices.Length; i++)
            {
                hash = hash * 31 + Mathf.RoundToInt(vertices[i].x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(vertices[i].y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(vertices[i].z * 100f);
            }

            return vertices.Length + "|" + mesh.bounds.size.ToString("F2") + "|" + hash;
        }

        [Test]
        public void EveryPad_HasItsOwnShape_SoGrayscaleStillTellsThemApart()
        {
            var seen = new Dictionary<string, string>();
            foreach (string name in PadMeshes)
            {
                string[] guids = AssetDatabase.FindAssets(name + " t:Mesh");
                Mesh mesh = null;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == name) mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                }

                Assert.IsNotNull(mesh, "Falta la malla del pad " + name + " (¿se regeneraron las escenas con BuildAll?).");

                string signature = Signature(mesh);
                Assert.IsFalse(seen.TryGetValue(signature, out string other), "Los pads " + name + " y " + other + " tienen la misma forma.");
                seen[signature] = name;
            }
        }
    }
}
