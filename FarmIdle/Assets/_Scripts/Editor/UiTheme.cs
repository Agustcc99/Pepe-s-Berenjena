using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Tema visual de la UI: todos los colores de paneles y botones salen de acá, así el estilo se cambia en un solo lugar.
    // Dirección: granja cálida (tarjetas verde bosque, botones de madera y dorado, letras crema), sin el azul genérico de Unity.
    internal static class UiTheme
    {
        // Fondos
        internal static readonly Color Card = new Color(0.14f, 0.21f, 0.15f, 0.98f);
        internal static readonly Color CardBorder = new Color(0.95f, 0.78f, 0.35f, 0.55f);
        internal static readonly Color SubPanel = new Color(0.09f, 0.15f, 0.10f, 0.99f);
        internal static readonly Color Pill = new Color(0.06f, 0.08f, 0.06f, 0.74f);
        internal static readonly Color Backdrop = new Color(0.03f, 0.06f, 0.04f, 0.72f);

        // Filas de listas (misiones, logros, premios diarios) y barras de progreso
        internal static readonly Color Row = new Color(0.08f, 0.13f, 0.09f, 0.95f);
        internal static readonly Color BarBack = new Color(0.02f, 0.03f, 0.02f, 0.88f);
        internal static readonly Color BarFill = new Color(0.45f, 0.85f, 0.30f, 1f);

        // Botones
        internal static readonly Color Primary = new Color(0.96f, 0.72f, 0.16f);     // dorado: la acción principal
        internal static readonly Color Positive = new Color(0.40f, 0.72f, 0.32f);    // verde hoja: cobrar, aceptar
        internal static readonly Color Secondary = new Color(0.62f, 0.42f, 0.24f);   // madera: acciones normales
        internal static readonly Color Neutral = new Color(0.36f, 0.42f, 0.36f);     // gris verdoso: créditos, privacidad
        internal static readonly Color Danger = new Color(0.78f, 0.26f, 0.22f);      // tomate: borrar, cerrar

        // Letras
        internal static readonly Color Title = new Color(1f, 0.92f, 0.55f);
        internal static readonly Color Gold = new Color(1f, 0.88f, 0.4f);            // letras doradas de las pastillas del HUD
        internal static readonly Color Reward = new Color(1f, 0.85f, 0.3f);          // premios
        internal static readonly Color BodyText = new Color(0.9f, 0.95f, 0.85f);     // progreso y texto secundario
        internal static readonly Color OnPrimary = new Color(0.22f, 0.13f, 0.02f);   // letra oscura sobre dorado
        internal static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);

        // Forma: un solo rectángulo redondeado (96 px, radio 36, borde 9-slice de 40) tintado por cada Image. Como todas las piezas
        // comparten la textura, uGUI las agrupa; el sprite se genera por código (blanco con alfa) y no suma materiales.
        internal const string RoundedPath = "Assets/Sprites/UI/rounded.png";
        private const int RoundedSize = 96;
        private const float RoundedRadius = 36f;
        private const int RoundedBorder = 40;

        internal static Sprite RoundedSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath) == null) WriteRounded();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        }

        private static void WriteRounded()
        {
            const int n = RoundedSize;
            var pixels = new Color32[n * n];
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Distancia con signo a un rectángulo redondeado; 1 px de suavizado en el borde.
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - RoundedRadius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - RoundedRadius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - RoundedRadius;
                    float a = Mathf.Clamp01(0.5f - d);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            Phase1SceneBuilder.EnsureFolder(Path.GetDirectoryName(RoundedPath).Replace('\\', '/'));
            File.WriteAllBytes(RoundedPath, png);
            AssetDatabase.ImportAsset(RoundedPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(RoundedBorder, RoundedBorder, RoundedBorder, RoundedBorder);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
