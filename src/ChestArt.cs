using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>
    /// Pixel-art textures for the bomb chest, drawn in code. The chest is see-through like the coin sack in Kingdom:
    /// solid wooden walls, floor and iron corners, with a faint wooden back and a near-clear front so the bomb pile shows.
    /// One texel covers PixelUnits of chest units, so the art lines up with the physics shape.
    /// </summary>
    internal static class ChestArt
    {
        public const float PixelUnits = 2.5f;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(40, 24, 16, 255);
        private static readonly Color32 Wood = new Color32(138, 88, 48, 255);
        private static readonly Color32 WoodLight = new Color32(176, 120, 66, 255);
        private static readonly Color32 WoodDark = new Color32(98, 60, 32, 255);
        private static readonly Color32 Iron = new Color32(70, 72, 82, 255);
        private static readonly Color32 IronLight = new Color32(130, 134, 148, 255);

        public static int Width => Mathf.RoundToInt(Core.BombChestShape.OuterWidth / PixelUnits);

        public static int Height => Mathf.RoundToInt(Core.BombChestShape.OuterHeight / PixelUnits);

        private static int WallPixels => Mathf.Max(2, Mathf.RoundToInt(Core.BombChestShape.Wall / PixelUnits));

        /// <summary>The inside back wall, drawn behind the bombs: faint planks you can see through.</summary>
        public static Texture2D Back()
        {
            Texture2D texture = New(Width, Height);
            int wall = WallPixels;
            for (int y = wall; y < Height; y++)
            {
                for (int x = wall; x < Width - wall; x++)
                {
                    bool seam = (y - wall) % 4 == 3;
                    Color32 colour = seam ? WoodDark : Wood;
                    colour.a = seam ? (byte)120 : (byte)70;
                    texture.SetPixel(x, y, colour);
                }
            }
            texture.Apply(false);
            return texture;
        }

        /// <summary>The walls, floor, rim and corners, drawn in front of the bombs, with a near-clear front panel.</summary>
        public static Texture2D Front()
        {
            Texture2D texture = New(Width, Height);
            int wall = WallPixels;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool edge = x == 0 || x == Width - 1 || y == 0;
                    bool side = x < wall || x >= Width - wall;
                    bool floor = y < wall;
                    bool rim = y == Height - 1 && !side;
                    Color32 colour;
                    if (edge)
                    {
                        colour = Outline;
                    }
                    else if (side || floor)
                    {
                        // Solid wood with a lit top edge and a darker grain line.
                        colour = (y == Height - 1 || (side && (x == 1 || x == Width - 2))) ? WoodLight : ((y + x) % 5 == 0 ? WoodDark : Wood);
                    }
                    else if (rim)
                    {
                        colour = WoodLight;
                        colour.a = 150;
                    }
                    else
                    {
                        // The see-through front: a light wooden tint with faint plank seams.
                        colour = (y - wall) % 4 == 3 ? WoodDark : WoodLight;
                        colour.a = (y - wall) % 4 == 3 ? (byte)60 : (byte)22;
                    }
                    texture.SetPixel(x, y, colour);
                }
            }
            // Iron corner brackets.
            int[,] corners = { { 0, 0 }, { Width - 4, 0 }, { 0, Height - 4 }, { Width - 4, Height - 4 } };
            for (int c = 0; c < 4; c++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        bool shine = x == 1 && y == 2;
                        texture.SetPixel(corners[c, 0] + x, corners[c, 1] + y, shine ? IronLight : Iron);
                    }
                }
            }
            texture.Apply(false);
            return texture;
        }

        /// <summary>A loose plank from the burst chest.</summary>
        public static Texture2D Plank()
        {
            Texture2D texture = New(7, 2);
            for (int x = 0; x < 7; x++)
            {
                texture.SetPixel(x, 1, x == 0 || x == 6 ? WoodDark : WoodLight);
                texture.SetPixel(x, 0, x == 0 || x == 6 ? Outline : Wood);
            }
            texture.Apply(false);
            return texture;
        }

        /// <summary>A splinter: a couple of pixels of wood.</summary>
        public static Texture2D Splinter()
        {
            Texture2D texture = New(2, 1);
            texture.SetPixel(0, 0, WoodLight);
            texture.SetPixel(1, 0, WoodDark);
            texture.Apply(false);
            return texture;
        }

        private static Texture2D New(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PeglinBalatroScoreChest",
            };
            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Clear;
            }
            texture.SetPixels32(pixels);
            return texture;
        }
    }
}
