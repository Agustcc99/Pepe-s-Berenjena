using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Paleta de colores única del mundo: una textura chiquita de 8 x 8 celdas, un color por celda.
    // Todas las mallas del mundo apuntan con su UV al centro de una celda y comparten un solo material (M_Palette),
    // que es el mismo esquema de los modelos de Kenney con su colormap: un material, un draw call.
    // Las primeras 17 celdas son los colores del arte provisorio (los índices de abajo). El resto se llena con los
    // colores de los modelos de Kenney que se usan (KenneyArt los registra con Register): así los dos kits y el arte
    // provisorio terminan en una sola textura y un solo material, en vez de uno por kit.
    internal static class Palette
    {
        internal const int Columns = 8;
        internal const int Rows = 8;
        private const int CellPixels = 4;

        // Índice de cada color: la celda se cuenta de izquierda a derecha y de abajo hacia arriba.
        internal const int SoilDark = 0;
        internal const int SoilLight = 1;
        internal const int LeafDark = 2;
        internal const int LeafLight = 3;
        internal const int WheatGold = 4;
        internal const int WheatStraw = 5;
        internal const int CarrotOrange = 6;
        internal const int CarrotDark = 7;
        internal const int TomatoRed = 8;
        internal const int TomatoDark = 9;
        internal const int Cream = 10;
        internal const int Stone = 11;
        internal const int Wood = 12;
        internal const int Water = 13;
        internal const int Flower = 14;
        internal const int White = 15;
        internal const int Purple = 16;

        private static readonly Color32[] Colors =
        {
            new Color32(0x5B, 0x3A, 0x24, 255),   // 0  tierra oscura
            new Color32(0x7E, 0x54, 0x35, 255),   // 1  tierra clara
            new Color32(0x2E, 0x7D, 0x32, 255),   // 2  hoja oscura
            new Color32(0x6D, 0xBE, 0x45, 255),   // 3  hoja clara
            new Color32(0xF2, 0xC9, 0x4C, 255),   // 4  trigo dorado
            new Color32(0xC9, 0x9A, 0x2E, 255),   // 5  paja
            new Color32(0xF2, 0x8C, 0x1E, 255),   // 6  zanahoria
            new Color32(0xC9, 0x68, 0x0F, 255),   // 7  zanahoria oscura
            new Color32(0xDB, 0x3A, 0x2B, 255),   // 8  tomate
            new Color32(0x9C, 0x2A, 0x20, 255),   // 9  tomate oscuro
            new Color32(0xF6, 0xEE, 0xDB, 255),   // 10 crema
            new Color32(0x8D, 0x94, 0x9A, 255),   // 11 piedra
            new Color32(0x9C, 0x6B, 0x3C, 255),   // 12 madera
            new Color32(0x4A, 0x90, 0xD9, 255),   // 13 agua
            new Color32(0xFF, 0x6F, 0x91, 255),   // 14 flor
            new Color32(0xFF, 0xFF, 0xFF, 255),   // 15 blanco
            new Color32(0x6A, 0x2C, 0x91, 255),   // 16 berenjena
        };

        // Colores que se fueron sumando después de los 17 propios (índices 17 en adelante). Se vacía al empezar cada
        // corrida del builder y se llena siempre en el mismo orden, así la textura sale idéntica cada vez.
        private static readonly List<Color32> Extras = new List<Color32>();

        internal static void ResetExtras()
        {
            Extras.Clear();
        }

        // Devuelve el índice de ese color en la paleta (RGB exacto; el alfa se ignora): el que ya tenía o uno nuevo.
        internal static int Register(Color32 color)
        {
            for (int i = 0; i < Colors.Length; i++)
            {
                if (SameRgb(Colors[i], color)) return i;
            }

            for (int i = 0; i < Extras.Count; i++)
            {
                if (SameRgb(Extras[i], color)) return Colors.Length + i;
            }

            if (Colors.Length + Extras.Count >= Columns * Rows)
            {
                throw new System.InvalidOperationException("La paleta se llenó (" + Columns * Rows + " colores): agrandá Columns/Rows o usá menos colores de Kenney.");
            }

            Extras.Add(new Color32(color.r, color.g, color.b, 255));
            return Colors.Length + Extras.Count - 1;
        }

        private static bool SameRgb(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        // Cuántos colores hay en uso (los propios más los registrados).
        internal static int UsedColors => Colors.Length + Extras.Count;

        // UV del centro de la celda: así el color sale puro aunque el filtro mezcle texels vecinos.
        internal static Vector2 UvOf(int color)
        {
            if (color < 0 || color >= UsedColors)
            {
                throw new System.ArgumentOutOfRangeException(nameof(color), "Color fuera de la paleta: " + color);
            }

            int column = color % Columns;
            int row = color / Columns;
            return new Vector2((column + 0.5f) / Columns, (row + 0.5f) / Rows);
        }

        internal static byte[] RenderPng()
        {
            int width = Columns * CellPixels;
            int height = Rows * CellPixels;

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cell = (y / CellPixels) * Columns + (x / CellPixels);

                    // Las celdas sin usar quedan magenta: si una malla apunta a una, se nota enseguida.
                    Color32 color = new Color32(255, 0, 255, 255);
                    if (cell < Colors.Length) color = Colors[cell];
                    else if (cell - Colors.Length < Extras.Count) color = Extras[cell - Colors.Length];

                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }
    }
}
