using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // El ícono de la app y la pantalla de arranque (splash), dibujados por código hasta que haya arte definitivo: un campo verde con
    // surcos y una moneda dorada con un brote. Android pide un ícono "adaptable" (fondo + primer plano en capas, que cada celular
    // recorta a su forma) además de los clásicos: se arman los tres a partir del mismo dibujo.
    internal static class ReleaseArt
    {
        private const string Folder = "Assets/Sprites/AppIcon";
        private const string FullPath = Folder + "/icon_full.png";
        private const string BackgroundPath = Folder + "/icon_background.png";
        private const string ForegroundPath = Folder + "/icon_foreground.png";
        private const string SplashPath = Folder + "/splash_logo.png";

        internal static void Apply()
        {
            Phase1SceneBuilder.EnsureFolder(Folder);

            Texture2D full = WriteTexture(FullPath, 512, (x, y) => Over(Background(x, y), Foreground(x, y)), sprite: false);
            Texture2D background = WriteTexture(BackgroundPath, 432, Background, sprite: false);
            Texture2D foreground = WriteTexture(ForegroundPath, 432, Foreground, sprite: false);
            Texture2D splash = WriteTexture(SplashPath, 512, Foreground, sprite: true);

            SetAndroidIcons(full, background, foreground);
            SetSplash(AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath), splash);
        }

        // ---------- El dibujo ----------

        // Campo: degradé verde de arriba abajo y, en el tercio de abajo, tierra con surcos.
        private static Color Background(float x, float y)
        {
            Color top = new Color(0.45f, 0.78f, 0.32f);
            Color bottom = new Color(0.24f, 0.55f, 0.24f);
            Color color = Color.Lerp(bottom, top, Mathf.Clamp01((y - 0.1f) / 0.9f));

            if (y < 0.30f)
            {
                color = new Color(0.45f, 0.30f, 0.18f);
                float furrow = Mathf.Abs(Mathf.Repeat(y * 22f, 1f) - 0.5f);   // surcos horizontales
                if (furrow < 0.12f) color = new Color(0.34f, 0.22f, 0.13f);
            }

            return color;
        }

        // Moneda dorada con brillo y un brote verde en el medio. Todo dentro del 66 % central: es la "zona segura" de un ícono adaptable.
        private static Color Foreground(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            Color result = new Color(0f, 0f, 0f, 0f);

            if (distance <= 0.30f)
            {
                Color rim = new Color(0.78f, 0.50f, 0.07f);
                Color face = new Color(1.00f, 0.80f, 0.22f);
                result = distance > 0.245f ? rim : face;
                if (Mathf.Abs(distance - 0.20f) < 0.006f) result = new Color(0.86f, 0.62f, 0.10f);   // aro fino

                // Brillo arriba a la izquierda.
                float sx = (x - 0.40f) / 0.09f, sy = (y - 0.66f) / 0.045f;
                if (sx * sx + sy * sy < 1f && distance < 0.24f) result = Color.Lerp(result, new Color(1f, 0.94f, 0.66f), 0.6f);

                // Brote: tallo y dos hojas.
                var leaf = new Color(0.20f, 0.55f, 0.18f);
                if (x > 0.485f && x < 0.515f && y > 0.36f && y < 0.54f) result = leaf;
                if (InLeaf(x, y, 0.43f, 0.575f, 35f) || InLeaf(x, y, 0.57f, 0.575f, -35f)) result = leaf;
                if (InLeaf(x, y, 0.435f, 0.578f, 35f, 0.6f) || InLeaf(x, y, 0.565f, 0.578f, -35f, 0.6f)) result = new Color(0.40f, 0.78f, 0.28f);
            }

            return result;
        }

        private static bool InLeaf(float x, float y, float cx, float cy, float degrees, float scale = 1f)
        {
            float a = degrees * Mathf.Deg2Rad;
            float dx = x - cx, dy = y - cy;
            float u = dx * Mathf.Cos(a) + dy * Mathf.Sin(a);
            float v = -dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
            float rx = 0.075f * scale, ry = 0.034f * scale;
            return (u * u) / (rx * rx) + (v * v) / (ry * ry) < 1f;
        }

        private static Color Over(Color back, Color front)
        {
            float a = front.a + back.a * (1f - front.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

            Color c = (front * front.a + back * back.a * (1f - front.a)) / a;
            c.a = a;
            return c;
        }

        // ---------- Archivos ----------

        private static Texture2D WriteTexture(string path, int size, Func<float, float, Color> color, bool sprite)
        {
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // Supermuestreo 2x2 para bordes suaves.
                    Color sum = Color.clear;
                    float alpha = 0f;
                    for (int s = 0; s < 4; s++)
                    {
                        Color c = color((px + 0.25f + 0.5f * (s % 2)) / size, (py + 0.25f + 0.5f * (s / 2)) / size);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        alpha += c.a;
                    }

                    alpha /= 4f;
                    Color rgb = alpha > 0f ? new Color(sum.r / 4f / alpha, sum.g / 4f / alpha, sum.b / 4f / alpha) : Color.black;
                    pixels[py * size + px] = new Color32((byte)(rgb.r * 255f), (byte)(rgb.g * 255f), (byte)(rgb.b * 255f), (byte)(alpha * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Ajustes de Unity ----------

        private static void SetAndroidIcons(Texture2D full, Texture2D background, Texture2D foreground)
        {
            // Unity 6: solo queda el ícono adaptable (fondo + primer plano); los clásicos y el redondo se derivan de él.
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in adaptive) icon.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Application);   // el ícono por defecto
        }

        // La pantalla de arranque: el logo de FarmIdle sobre verde oscuro. El logo de Unity se saca (Unity 6 lo permite en todos los planes).
        private static void SetSplash(Sprite logo, Texture2D fallback)
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.07f, 0.12f, 0.08f);
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
        }
    }
}
