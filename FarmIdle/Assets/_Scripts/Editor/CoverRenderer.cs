using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    /// <summary>
    /// K-01: renders de MainLevel para las portadas del portal, en modo batch.
    /// Lee de variables de entorno: FI_OUT (carpeta), FI_SHOTS (lista "nombre,ancho,alto,px,py,pz,rx,ry,rz,fov;...").
    /// Sin texto: el título se compone aparte (Tools/make_covers.py).
    /// Uso: Unity -batchmode -executeMethod FarmIdle.Editor.CoverRenderer.Render -quit
    /// </summary>
    internal static class CoverRenderer
    {
        public static void Render()
        {
            string outDir = Environment.GetEnvironmentVariable("FI_OUT");
            string shots = Environment.GetEnvironmentVariable("FI_SHOTS");
            if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(shots))
            {
                Debug.LogError("CoverRenderer: faltan FI_OUT o FI_SHOTS.");
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(outDir);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(ScenePaths.MainLevel, OpenSceneMode.Single);

            PlantCrops();
            if (Environment.GetEnvironmentVariable("FI_HIDE_MARKERS") == "1")
            {
                GameObject markers = GameObject.Find("TutorialMarkers");
                if (markers != null) markers.SetActive(false);
            }
            var mover = UnityEngine.Object.FindFirstObjectByType<FarmIdle.PlayerMovement>();
            if (mover != null) mover.gameObject.SetActive(false);
            var cm = Camera.main;
            if (cm != null) Debug.Log("CoverRenderer main camera " + cm.transform.position + " " + cm.transform.eulerAngles + " fov " + cm.fieldOfView);
            foreach (string shot in shots.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = shot.Split(',');
                int w = int.Parse(f[1], CultureInfo.InvariantCulture);
                int h = int.Parse(f[2], CultureInfo.InvariantCulture);
                var pos = new Vector3(P(f[3]), P(f[4]), P(f[5]));
                var rot = new Vector3(P(f[6]), P(f[7]), P(f[8]));
                float fov = P(f[9]);
                RenderShot(Path.Combine(outDir, f[0] + ".png"), w, h, pos, rot, fov);
            }

            EditorApplication.Exit(0);
        }

        // Solo en memoria (la escena no se guarda): una planta madura en cada parcela, para que las portadas no muestren tierra pelada.
        private static void PlantCrops()
        {
            string[] names = { "Eggplant", "Carrot", "Tomato", "Pumpkin", "Corn", "Wheat", "Sunflower", "Watermelon" };
            var plots = UnityEngine.Object.FindObjectsByType<FarmIdle.PlotView>(FindObjectsInactive.Exclude);
            Array.Sort(plots, (a, b) => string.CompareOrdinal(a.transform.GetSiblingIndex().ToString("D3") + a.name, b.transform.GetSiblingIndex().ToString("D3") + b.name));
            for (int i = 0; i < plots.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Plants/Plant_" + names[i % names.Length] + ".prefab");
                if (prefab == null) continue;
                var plant = (GameObject)PrefabUtility.InstantiatePrefab(prefab, plots[i].transform);
                plant.transform.position = plots[i].transform.position;
                if (plant.TryGetComponent(out FarmIdle.PlantView view)) view.ShowReady(false);
            }
        }

        private static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static void RenderShot(string path, int w, int h, Vector3 pos, Vector3 rot, float fov)
        {
            var go = new GameObject("CoverCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Camera main = Camera.main;
            cam.backgroundColor = main != null ? main.backgroundColor : new Color(0.7f, 0.85f, 0.95f);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("CoverRenderer: " + path);
        }
    }
}
