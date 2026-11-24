using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    /// <summary>
    /// Vista previa de la UI y el mundo en modo batch (para pulir el estilo sin abrir el juego).
    /// Abre MainLevel (sin guardarla), pasa los Canvas a "Screen Space - Camera" y renderiza:
    /// "hud" (juego tal cual), "world" (sin UI) y un PNG por cada panel modal (objetos "*Backdrop" apagados).
    /// Variables de entorno: FI_OUT (carpeta de salida), FI_W y FI_H (opcional, por defecto 720x1280).
    /// Uso: Unity -batchmode -projectPath FarmIdle -executeMethod FarmIdle.EditorTools.UiPreviewRenderer.Render -quit
    /// </summary>
    internal static class UiPreviewRenderer
    {
        private const string Ext = ".png";   // aparte: el test de localizacion confunde "nombre.png" con una clave de texto

        public static void Render()
        {
            string outDir = Environment.GetEnvironmentVariable("FI_OUT");
            if (string.IsNullOrEmpty(outDir))
            {
                Debug.LogError("UiPreviewRenderer: falta FI_OUT.");
                EditorApplication.Exit(1);
                return;
            }
            int w = ReadInt("FI_W", 720);
            int h = ReadInt("FI_H", 1280);

            Directory.CreateDirectory(outDir);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(ScenePaths.MainLevel, OpenSceneMode.Single);
            CoverRenderer.PlantCrops();

            Camera main = Camera.main;
            var go = new GameObject("PreviewCam");
            var cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                cam.fieldOfView = main.fieldOfView;
                cam.backgroundColor = main.backgroundColor;
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;

            var canvases = new List<Canvas>(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include));
            foreach (Canvas c in canvases)
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;

            // Mundo sin UI: apaga los canvas un momento.
            SetCanvases(canvases, false);
            Shot(cam, rt, Path.Combine(outDir, "world" + Ext));
            SetCanvases(canvases, true);

            // "Toca para jugar" tapa todo en edicion: una toma con el y el resto sin el.
            var tap = UnityEngine.Object.FindFirstObjectByType<FarmIdle.TapToPlayOverlay>(FindObjectsInactive.Include);
            if (tap != null)
            {
                tap.gameObject.SetActive(true);
                Shot(cam, rt, Path.Combine(outDir, "tap_to_play" + Ext));
                tap.gameObject.SetActive(false);
            }
            Shot(cam, rt, Path.Combine(outDir, "hud" + Ext));

            // Paneles modales: cada "*Backdrop" que está apagado, de a uno.
            foreach (Canvas c in canvases)
            {
                if (!c.isRootCanvas) continue;
                foreach (Transform t in c.transform)
                {
                    if (t.gameObject.activeSelf || !t.name.EndsWith("Backdrop", StringComparison.Ordinal)) continue;
                    t.gameObject.SetActive(true);
                    Shot(cam, rt, Path.Combine(outDir, "panel_" + t.name + Ext));
                    t.gameObject.SetActive(false);
                }
            }

            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            EditorApplication.Exit(0);
        }

        private static int ReadInt(string name, int fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return int.TryParse(v, out int n) ? n : fallback;
        }

        private static void SetCanvases(List<Canvas> canvases, bool on)
        {
            foreach (Canvas c in canvases) if (c.isRootCanvas) c.gameObject.SetActive(on);
        }

        private static void Shot(Camera cam, RenderTexture rt, string path)
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("UiPreviewRenderer: " + path);
        }
    }
}
