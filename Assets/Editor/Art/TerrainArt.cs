using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Smooth terrain tiles. Hills and water are drawn on a grid offset by half a tile, so each
/// tile's corners sit on four map tiles. The corner pattern (16 cases) picks the shape.
///
/// Shapes come from a field that is 1 at corners of that terrain and 0 elsewhere, interpolated
/// over two triangles per tile. The field matches along shared tile edges, so neighbouring
/// shapes join up, and its 0.5 line makes diagonal coastlines instead of staircases.
/// </summary>
public static partial class PlaceholderArt
{
    // Water is drawn once per shape; the water shader animates it (see Shaders/WaterSprite.shader).
    private const int WaterFrames = 1;

    private static readonly Color GrassGreen = new Color(0.42f, 0.6f, 0.31f);

    [MenuItem("Tools/Age of Stickman/Generate Terrain Art")]
    public static void GenerateTerrainOnly()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        if (art == null)
        {
            Debug.LogError("No ArtLibrary yet: run Generate Placeholder Art first.");
            return;
        }
        GenerateTerrain(art);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Terrain art generated.");
    }

    private static void GenerateTerrain(ArtLibrary art)
    {
        Directory.CreateDirectory(Path.Combine(Root, "World/Terrain"));

        art.grassVariants = new List<Sprite>
        {
            Tile("World/Terrain/Grass0.png", 1, 1, p => DrawGrass(p, GrassGreen, 11)),
            Tile("World/Terrain/Grass1.png", 1, 1, p => DrawGrassFlowers(p, 31)),
            Tile("World/Terrain/Grass2.png", 1, 1, p => DrawGrassPebbles(p, 47)),
            Tile("World/Terrain/Grass3.png", 1, 1, p => DrawGrassClover(p, 59)),
            Tile("World/Terrain/Grass4.png", 1, 1, p => DrawGrass(p, GrassGreen, 73)),
        };

        // Three tiers: big hill areas rise in terraces towards their middle.
        art.hillShapes = HillTier(1);
        art.hillShapes2 = HillTier(2);
        art.hillShapes3 = HillTier(3);
        art.hillFullVariants = new List<Sprite>
        {
            EdgeTile("World/Terrain/Hill_15b.png", p => DrawHillShape(p, 15, 61, 1)),
            EdgeTile("World/Terrain/Hill_15c.png", p => DrawHillShape(p, 15, 97, 1)),
        };

        art.waterFrameCount = WaterFrames;
        art.waterFps = 3f;
        art.waterMaterial = WaterMaterial();
        art.waterFrames = new List<Sprite>();
        for (int mask = 0; mask < 16; mask++)
        {
            for (int f = 0; f < WaterFrames; f++)
            {
                if (mask == 0)
                {
                    art.waterFrames.Add(null);
                    continue;
                }
                int m = mask;
                float t = f / (float)WaterFrames;
                art.waterFrames.Add(EdgeTile($"World/Terrain/Water_{m:00}_{f}.png", p => DrawWaterShape(p, m, t)));
            }
        }
    }

    private static List<Sprite> HillTier(int level)
    {
        var shapes = new List<Sprite> { null };
        string suffix = level == 1 ? "" : "_T" + level;
        for (int mask = 1; mask < 16; mask++)
        {
            int m = mask;
            shapes.Add(EdgeTile($"World/Terrain/Hill{suffix}_{m:00}.png", p => DrawHillShape(p, m, 29 + level * 13, level)));
        }
        return shapes;
    }

    private static Sprite EdgeTile(string path, System.Action<Painter> draw)
    {
        var p = new Painter(1, 1, BuildingPPU);
        draw(p);
        return Save(p, path, BuildingPPU, Vector2.zero, tile: true, clamp: true);
    }

    // ---------- The corner field ----------

    /// <summary>
    /// The corner field at q (0..1 inside the tile), plus its gradient. Corner bits: 1 = bottom-left,
    /// 2 = bottom-right, 4 = top-left, 8 = top-right. 'joinDiagonals': when only two opposite
    /// corners are set, join them (water channels) instead of keeping them apart.
    /// </summary>
    private static float CornerField(int mask, Vector2 q, bool joinDiagonals, out Vector2 gradient)
    {
        float bl = mask & 1, br = (mask >> 1) & 1, tl = (mask >> 2) & 1, tr = (mask >> 3) & 1;
        // Split along the diagonal that keeps a lone corner (or the missing one) whole.
        bool antiDiagonal; // true: split from top-left to bottom-right
        switch (mask)
        {
            case 1: case 8: case 14: case 7: antiDiagonal = true; break;
            case 2: case 4: case 13: case 11: antiDiagonal = false; break;
            case 6: antiDiagonal = joinDiagonals; break;   // bottom-right + top-left
            case 9: antiDiagonal = !joinDiagonals; break;  // bottom-left + top-right
            default: antiDiagonal = true; break;
        }
        float u = q.x, v = q.y;
        if (antiDiagonal)
        {
            if (u + v <= 1)
            {
                gradient = new Vector2(br - bl, tl - bl);
                return bl + (br - bl) * u + (tl - bl) * v;
            }
            gradient = new Vector2(tr - tl, tr - br);
            return tr + (tl - tr) * (1 - u) + (br - tr) * (1 - v);
        }
        if (v <= u)
        {
            gradient = new Vector2(br - bl, tr - br);
            return bl + (br - bl) * u + (tr - br) * v;
        }
        gradient = new Vector2(tr - tl, tl - bl);
        return bl + (tl - bl) * v + (tr - tl) * u;
    }

    /// <summary>A small wobble that repeats every tile, so edges look hand-drawn but still join up.</summary>
    private static float EdgeWobble(Vector2 q, float amount)
    {
        const float twoPi = Mathf.PI * 2;
        return amount * (0.6f * Mathf.Sin(twoPi * (2 * q.x + q.y) + 0.7f) + 0.4f * Mathf.Sin(twoPi * (q.x - 3 * q.y) + 2.1f));
    }

    /// <summary>Field height including wobble: the shape is where this is above 0.5.</summary>
    private static float Height(int mask, Vector2 q, bool join, float wobble) =>
        CornerField(mask, q, join, out _) + EdgeWobble(q, wobble);

    /// <summary>Paint 'color' where the field height is above 'level'.</summary>
    private static void Above(Painter p, System.Func<Vector2, float> height, float level, Color color) =>
        p.Draw(q => level - height(q), new Rect(0, 0, 1, 1), color);

    // ---------- Water ----------

    private static void DrawWaterShape(Painter p, int mask, float t)
    {
        const float twoPi = Mathf.PI * 2;
        Color sand = new Color(0.86f, 0.78f, 0.55f);
        Color wetSand = new Color(0.72f, 0.66f, 0.46f);
        Color foam = new Color(0.9f, 0.96f, 1f, 0.9f);
        Color shallow = new Color(0.4f, 0.7f, 0.88f);
        Color mid = new Color(0.32f, 0.6f, 0.84f);
        Color deep = new Color(0.25f, 0.5f, 0.78f);

        System.Func<Vector2, float> h = q => Height(mask, q, true, 0.05f);
        float lap = 0.018f * Mathf.Sin(twoPi * t);

        Above(p, h, 0.5f, sand);
        Above(p, h, 0.525f, wetSand);
        Above(p, h, 0.545f + lap, foam);
        Above(p, h, 0.565f + lap, shallow);
        Above(p, h, 0.68f, mid);
        Above(p, h, 0.82f, deep);
    }

    private static Material WaterMaterial()
    {
        const string path = Root + "/World/Terrain/Water.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Age of Stickman/Water Sprite"));
            AssetDatabase.CreateAsset(material, path);
        }
        // Remove frames left over from the old drawn animation.
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "World/Terrain"), "Water_*_*.png"))
        {
            if (!file.EndsWith("_0.png"))
            {
                AssetDatabase.DeleteAsset(file.Replace(Path.DirectorySeparatorChar, '/'));
            }
        }
        return material;
    }

    // ---------- Hills ----------

    private static void DrawHillShape(Painter p, int mask, int seed, int level)
    {
        // Higher tiers are lighter (catching more sun) with taller cliffs.
        float lift = (level - 1) * 0.06f;
        Color Tint(Color c) => new Color(Mathf.Min(1, c.r + lift), Mathf.Min(1, c.g + lift), Mathf.Min(1, c.b + lift * 0.9f), c.a);
        Color rock = Tint(new Color(0.56f, 0.51f, 0.43f));
        Color top = Tint(new Color(0.62f, 0.57f, 0.47f));
        Color cliff = Tint(new Color(0.42f, 0.37f, 0.3f));
        Color cliffDark = Tint(new Color(0.33f, 0.29f, 0.24f));
        Color strata = Tint(new Color(0.37f, 0.32f, 0.26f));
        Color lip = Tint(new Color(0.74f, 0.69f, 0.58f));
        Color dark = Tint(new Color(0.45f, 0.4f, 0.33f));
        Color light = Tint(new Color(0.68f, 0.63f, 0.54f));
        // Tier 1 shades the grass; higher tiers shade the rock of the tier below.
        Color edgeShade = level == 1 ? new Color(0.25f, 0.32f, 0.17f, 0.4f) : new Color(0.2f, 0.17f, 0.13f, 0.35f);
        Color castShadow = new Color(0.1f, 0.1f, 0.08f, 0.28f);
        float cliffTop = 0.66f + level * 0.04f;   // taller cliff faces higher up

        System.Func<Vector2, float> h = q => Height(mask, q, false, 0.06f);
        // Which way the edge faces: +1 where the hill rises northward (its south face shows).
        System.Func<Vector2, float> facing = q =>
        {
            CornerField(mask, q, false, out var g);
            return g.sqrMagnitude < 0.0001f ? 0 : g.y / g.magnitude;
        };
        System.Func<Vector2, float> south = q => 0.2f - facing(q);   // < 0 on south-facing edges

        // Shadow: a soft rim all round, plus a longer one cast down from south faces.
        Above(p, h, 0.42f, edgeShade);
        p.Draw(q => Mathf.Max(0.32f - h(q), south(q)), new Rect(0, 0, 1, 1), castShadow);
        Above(p, h, 0.5f, rock);
        // South faces: a tall cliff, darker at its foot, with a rock layer line across it.
        p.Draw(q => Mathf.Max(0.5f - h(q), Mathf.Max(h(q) - cliffTop, south(q))), new Rect(0, 0, 1, 1), cliff);
        p.Draw(q => Mathf.Max(0.5f - h(q), Mathf.Max(h(q) - 0.56f, south(q))), new Rect(0, 0, 1, 1), cliffDark);
        float mid = (0.56f + cliffTop) * 0.5f;
        p.Draw(q => Mathf.Max(Mathf.Abs(h(q) - mid) - 0.008f, south(q)), new Rect(0, 0, 1, 1), strata);
        // The edge of the top, catching the light, just above the cliff.
        p.Draw(q => Mathf.Max(cliffTop - h(q), Mathf.Max(h(q) - cliffTop - 0.035f, south(q))), new Rect(0, 0, 1, 1), lip);
        // North and side faces: a light lip.
        p.Draw(q => Mathf.Max(0.5f - h(q), Mathf.Max(h(q) - 0.56f, -0.2f + facing(q))), new Rect(0, 0, 1, 1), lip);
        Above(p, h, 0.86f, top);

        // Pebbles and cracks, only well inside the hill so nothing is cut at tile edges.
        var rng = new System.Random(seed + mask * 7);
        for (int i = 0; i < 6; i++)
        {
            var c = V(0.14f + (float)rng.NextDouble() * 0.72f, 0.14f + (float)rng.NextDouble() * 0.72f);
            var r = V(0.035f + (float)rng.NextDouble() * 0.025f, 0.022f + (float)rng.NextDouble() * 0.012f);
            Color col = i % 2 == 0 ? dark : light;
            p.Draw(q => Mathf.Max(SdEllipse(q, c, r), cliffTop + 0.08f - h(q)), new Rect(0, 0, 1, 1), col);
        }
        for (int i = 0; i < 2; i++)
        {
            var a = V(0.2f + (float)rng.NextDouble() * 0.5f, 0.2f + (float)rng.NextDouble() * 0.5f);
            var b = a + V(0.1f, (float)rng.NextDouble() * 0.12f - 0.06f);
            var m = (a + b) * 0.5f + V(0, 0.03f);
            p.Draw(q => Mathf.Max(Mathf.Min(SdSegment(q, a, m), SdSegment(q, m, b)) - 0.008f, cliffTop + 0.1f - h(q)), new Rect(0, 0, 1, 1), dark);
        }
    }

    // ---------- Grass variants ----------

    private static void DrawGrassFlowers(Painter p, int seed)
    {
        DrawGrass(p, GrassGreen, seed);
        var rng = new System.Random(seed);
        Color[] petals = { new Color(0.98f, 0.92f, 0.45f), new Color(0.96f, 0.96f, 0.98f), new Color(0.85f, 0.55f, 0.85f) };
        for (int i = 0; i < 4; i++)
        {
            var c = V(0.15f + (float)rng.NextDouble() * 0.7f, 0.15f + (float)rng.NextDouble() * 0.7f);
            Color col = petals[rng.Next(petals.Length)];
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2 / 5;
                p.Circle(c + V(Mathf.Cos(a), Mathf.Sin(a)) * 0.022f, 0.016f, col);
            }
            p.Circle(c, 0.013f, new Color(0.95f, 0.7f, 0.2f));
        }
    }

    private static void DrawGrassPebbles(Painter p, int seed)
    {
        DrawGrass(p, GrassGreen, seed);
        var rng = new System.Random(seed);
        for (int i = 0; i < 3; i++)
        {
            var c = V(0.15f + (float)rng.NextDouble() * 0.7f, 0.15f + (float)rng.NextDouble() * 0.7f);
            var r = V(0.03f + (float)rng.NextDouble() * 0.02f, 0.02f + (float)rng.NextDouble() * 0.01f);
            p.Ellipse(c - V(0, 0.01f), r, new Color(0, 0, 0, 0.18f));
            p.Ellipse(c, r, Stone);
            p.Ellipse(c + V(-r.x * 0.3f, r.y * 0.35f), r * 0.45f, StoneLight);
        }
    }

    private static void DrawGrassClover(Painter p, int seed)
    {
        DrawGrass(p, GrassGreen, seed);
        var rng = new System.Random(seed);
        Color patch = new Color(0.36f, 0.55f, 0.27f);
        Color leaf = new Color(0.3f, 0.5f, 0.24f);
        var center = V(0.35f + (float)rng.NextDouble() * 0.3f, 0.35f + (float)rng.NextDouble() * 0.3f);
        p.Ellipse(center, V(0.2f, 0.12f), patch);
        for (int i = 0; i < 5; i++)
        {
            var c = center + V((float)rng.NextDouble() * 0.26f - 0.13f, (float)rng.NextDouble() * 0.14f - 0.07f);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2 / 3 + 0.5f;
                p.Circle(c + V(Mathf.Cos(a), Mathf.Sin(a)) * 0.016f, 0.014f, leaf);
            }
        }
    }
}
