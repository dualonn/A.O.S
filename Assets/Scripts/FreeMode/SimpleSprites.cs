using UnityEngine;

/// <summary>Placeholder sprites generated in code, so free mode works before real art exists.</summary>
public static class SimpleSprites
{
    private static Sprite square;
    private static Sprite gridCell;
    private static Sprite gridBlock;

    /// <summary>An 8x8-cell block of grid lines (one world unit per cell), for tiling big areas cheaply.</summary>
    public static Sprite GridBlock
    {
        get
        {
            if (gridBlock == null)
            {
                const int cell = 16, size = cell * 8;
                var tex = new Texture2D(size, size)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
                var clear = new Color(1, 1, 1, 0);
                var line = new Color(1, 1, 1, 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        tex.SetPixel(x, y, x % cell == 0 || y % cell == 0 ? line : clear);
                    }
                }
                tex.Apply();
                gridBlock = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.zero, cell, 0, SpriteMeshType.FullRect);
                gridBlock.name = "GridBlock";
            }
            return gridBlock;
        }
    }

    /// <summary>A white 1x1 world-unit square, pivot bottom-left.</summary>
    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }
                tex.SetPixels(pixels);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.zero, 4, 0, SpriteMeshType.FullRect);
                square.name = "SimpleSquare";
            }
            return square;
        }
    }

    /// <summary>One grid cell with a thin border, for tiling across the settlement.</summary>
    public static Sprite GridCell
    {
        get
        {
            if (gridCell == null)
            {
                const int size = 32;
                var tex = new Texture2D(size, size)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
                var fill = new Color(1, 1, 1, 0.04f);
                var line = new Color(1, 1, 1, 0.18f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        tex.SetPixel(x, y, x == 0 || y == 0 ? line : fill);
                    }
                }
                tex.Apply();
                gridCell = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.zero, size, 0, SpriteMeshType.FullRect);
                gridCell.name = "GridCell";
            }
            return gridCell;
        }
    }
}
