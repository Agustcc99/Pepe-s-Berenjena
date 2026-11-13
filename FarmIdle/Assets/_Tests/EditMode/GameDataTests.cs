using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Vigila los ScriptableObjects de datos que hay en el proyecto.
    public class GameDataTests
    {
        [Test]
        public void EveryGameDataAsset_HasAUniqueStableId()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDataAsset");
            Assert.Greater(guids.Length, 0, "No se encontró ningún dato de diseño; ¿corriste FarmIdle > Fase 2 > Construir assets y escenas?");

            var seen = new Dictionary<string, string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameDataAsset>(path);

                Assert.IsFalse(string.IsNullOrEmpty(asset.Id), "Sin id: " + path);
                Assert.IsTrue(Regex.IsMatch(asset.Id, "^[a-z0-9_-]+$"), "Id con caracteres no permitidos ('" + asset.Id + "'): " + path);

                bool isNew = seen.TryAdd(asset.Id, path);
                Assert.IsTrue(isNew, "Id repetido '" + asset.Id + "': " + path + " y " + (isNew ? "" : seen[asset.Id]));
            }
        }

        [Test]
        public void CropData_HasPlayableValues()
        {
            string[] guids = AssetDatabase.FindAssets("t:CropData");
            Assert.Greater(guids.Length, 0);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);

                Assert.IsFalse(string.IsNullOrWhiteSpace(crop.DisplayName), "Sin nombre: " + path);
                Assert.Greater(crop.GrowSeconds, 0f, "Tiempo de crecimiento inválido: " + path);
                Assert.GreaterOrEqual(crop.SellPrice, 0, "Precio inválido: " + path);
            }
        }

        // F-01: ocho cultivos en el GameConfig, cada uno más lento, más caro y con mejor rendimiento por segundo que el anterior.
        [Test]
        public void GameConfig_HasEightCrops_EachSlowerAndMoreProfitableThanThePrevious()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            Assert.AreEqual(8, config.Crops.Count, "Faltan cultivos en el GameConfig (correr Construir assets y escenas).");
            for (int i = 1; i < config.Crops.Count; i++)
            {
                CropData previous = config.Crops[i - 1];
                CropData crop = config.Crops[i];
                Assert.Greater(crop.GrowSeconds, previous.GrowSeconds, crop.Id + " tiene que tardar más que " + previous.Id);
                Assert.Greater(crop.SellPrice, previous.SellPrice, crop.Id + " tiene que pagar más que " + previous.Id);
                Assert.Greater(crop.SellPrice / crop.GrowSeconds, previous.SellPrice / previous.GrowSeconds, crop.Id + " tiene que rendir más por segundo que " + previous.Id);
            }
        }

        // El prefab de planta sale del PoolManager y lo maneja la parcela: tiene que traer un PlantView con sus dos formas
        // y arrancar con la verde. Si falla, falta correr FarmIdle > Fase 2 > Construir assets y escenas.
        [Test]
        public void CropData_HasAPlantPrefab_ReadyToBePooled()
        {
            string[] guids = AssetDatabase.FindAssets("t:CropData");
            Assert.Greater(guids.Length, 0);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);

                Assert.IsNotNull(crop.PlantPrefab, "Sin prefab de planta: " + path);

                var plant = crop.PlantPrefab.GetComponent<PlantView>();
                Assert.IsNotNull(plant, "El prefab de planta no tiene PlantView: " + path);
                Assert.IsNotNull(plant.GrowingModel, "Falta la forma Growing: " + path);
                Assert.IsNotNull(plant.ReadyModel, "Falta la forma Ready: " + path);
                Assert.IsTrue(plant.GrowingModel.activeSelf, "La planta tiene que arrancar con la forma verde: " + path);
                Assert.IsFalse(plant.ReadyModel.activeSelf, "La forma madura tiene que arrancar apagada: " + path);
            }
        }

        // El producto apilado sale del PoolManager y va colgado del jugador: tiene que traer malla y material (y el mismo
        // material de paleta en todos) y ningún collider, porque bloquearía al CharacterController. Si falla, falta
        // correr FarmIdle > Fase 2 > Construir assets y escenas.
        [Test]
        public void CropData_HasAProductPrefab_ReadyToBePooledAndHungOnThePlayer()
        {
            string[] guids = AssetDatabase.FindAssets("t:CropData");
            Assert.Greater(guids.Length, 0);

            Material sharedMaterial = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var crop = AssetDatabase.LoadAssetAtPath<CropData>(path);

                GameObject product = crop.ProductPrefab;
                Assert.IsNotNull(product, "Sin prefab de producto: " + path);
                Assert.AreNotSame(crop.PlantPrefab, product, "El producto no es la planta: " + path);
                Assert.IsTrue(product.activeSelf, "El producto tiene que arrancar activo (el pool lo activa al entregarlo): " + path);

                var filter = product.GetComponentInChildren<MeshFilter>();
                var renderer = product.GetComponentInChildren<MeshRenderer>();
                Assert.IsNotNull(filter, "El producto no tiene MeshFilter: " + path);
                Assert.IsNotNull(filter.sharedMesh, "El producto no tiene malla: " + path);
                Assert.IsNotNull(renderer, "El producto no tiene MeshRenderer: " + path);
                Assert.IsNotNull(renderer.sharedMaterial, "El producto no tiene material: " + path);
                Assert.AreEqual(0, product.GetComponentsInChildren<Collider>(true).Length, "Un collider en el producto bloquearía al jugador: " + path);

                if (sharedMaterial == null) sharedMaterial = renderer.sharedMaterial;
                Assert.AreSame(sharedMaterial, renderer.sharedMaterial, "Todos los productos comparten el material de paleta: " + path);
            }
        }

        [Test]
        public void GameConfig_Exists_WithSaneValues()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length, "Tiene que haber un único GameConfig.");

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Assert.GreaterOrEqual(config.StartingCoins, 0L);
            Assert.GreaterOrEqual(config.BackpackCapacity, 1, "La mochila tiene que tener lugar para al menos 1 producto.");
            Assert.That(config.TargetFrameRate, Is.InRange(30, 120));
            Assert.Greater(config.SellIntervalSeconds, 0f, "La venta necesita un intervalo entre unidades.");
            Assert.Less(config.SellIntervalSeconds, 1f, "Un intervalo de venta de un segundo o más haría eterno vaciar la mochila.");
            Assert.Greater(config.SellFlightSeconds, 0f, "El producto tiene que tardar algo en volar a la venta.");
        }
    }
}
