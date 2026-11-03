using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Biome art: the ground, trees and berry bushes of the lands beyond home (desert, tundra, swamp,
/// volcanic and crystal). Terrain types never change, so these only swap how the land looks.
/// (Tools > Age of Stickman > Generate Biome Art.)
/// </summary>
public static partial class PlaceholderArt
{
    private static Color32[] BiomeRamp(Biome b) => b switch
    {
        Biome.Desert => new[] { P.Hex("8a6a42"), P.Hex("b48c5c"), P.Hex("d6b07a"), P.Hex("ecd09a"), P.Hex("f8e6bc") },
        Biome.Tundra => new[] { P.Hex("7a90a6"), P.Hex("a8bccc"), P.Hex("d4e0ea"), P.Hex("eef4f8"), P.Hex("ffffff") },
        Biome.Swamp => new[] { P.Hex("26301e"), P.Hex("3a4628"), P.Hex("4e5a34"), P.Hex("667246"), P.Hex("86905c") },
        Biome.Volcanic => new[] { P.Hex("221c1e"), P.Hex("362c2e"), P.Hex("4a3e3e"), P.Hex("62544e"), P.Hex("7e6c62") },
        Biome.Barren => new[] { P.Hex("3a3a3e"), P.Hex("55555a"), P.Hex("74747a"), P.Hex("96969c"), P.Hex("c0c0c6") },
        Biome.Jungle => new[] { P.Hex("14301a"), P.Hex("1e4426"), P.Hex("2a5c30"), P.Hex("3e7a3e"), P.Hex("5a9a50") },
        Biome.Fungal => new[] { P.Hex("3a1e3a"), P.Hex("5a2e58"), P.Hex("7a4278"), P.Hex("9a5e96"), P.Hex("c08abc") },
        _ => new[] { P.Hex("2c2648"), P.Hex("403a68"), P.Hex("5a528c"), P.Hex("776eb0"), P.Hex("9e96d8") },   // crystal
    };

    [MenuItem("Tools/Age of Stickman/Generate Biome Art")]
    public static void GenerateBiomeArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        ResetDir("World/Biomes");
        art.biomeGround = new List<Sprite>();
        art.biomeTrees = new List<Sprite>();
        art.biomeBushes = new List<Sprite>();
        foreach (Biome b in new[] { Biome.Desert, Biome.Tundra, Biome.Swamp, Biome.Volcanic, Biome.Crystal, Biome.Barren, Biome.Jungle, Biome.Fungal })
        {
            var biome = b;
            for (int v = 0; v < 5; v++)
            {
                int variant = v;
                art.biomeGround.Add(TilePixel($"World/Biomes/{b}Ground{v}.png", c => BiomeGround(c, biome, variant)));
            }
            for (int t = 0; t < 2; t++)
            {
                int kind = t;
                art.biomeTrees.Add(PropPixel($"World/Biomes/{b}Tree{t}.png", 48, 70, c => BiomeTree(c, biome, kind)));
            }
            art.biomeBushes.Add(PropPixel($"World/Biomes/{b}Bush.png", 30, 24, c => BiomeBush(c, biome)));
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Biome art generated.");
    }

    /// <summary>Add art only for lands that don't have it yet (the others are left as they are).</summary>
    [MenuItem("Tools/Age of Stickman/Add New Biome Art")]
    public static void AddNewBiomeArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        int have = art.biomeGround.Count / 5;   // biomes with art, after Temperate
        int all = System.Enum.GetValues(typeof(Biome)).Length - 1;
        for (int i = have + 1; i <= all; i++)
        {
            var biome = (Biome)i;
            for (int v = 0; v < 5; v++)
            {
                int variant = v;
                art.biomeGround.Add(TilePixel($"World/Biomes/{biome}Ground{v}.png", c => BiomeGround(c, biome, variant)));
            }
            for (int t = 0; t < 2; t++)
            {
                int kind = t;
                art.biomeTrees.Add(PropPixel($"World/Biomes/{biome}Tree{t}.png", 48, 70, c => BiomeTree(c, biome, kind)));
            }
            art.biomeBushes.Add(PropPixel($"World/Biomes/{biome}Bush.png", 30, 24, c => BiomeBush(c, biome)));
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added biome art for {all - have} new lands.");
    }

    /// <summary>Ground for a biome: a main tone with scattered specks, and a feature on the detailed variants.</summary>
    private static void BiomeGround(PixelCanvas c, Biome b, int variant)
    {
        var g = BiomeRamp(b);
        c.Rect(0, 0, TilePx - 1, TilePx - 1, g[2]);
        var rng = new System.Random(variant * 41 + (int)b * 7);
        for (int i = 0; i < 12; i++) { int x = rng.Next(TilePx), y = rng.Next(TilePx); c.Put(x, y, g[3]); c.Put((x + 1) % TilePx, y, g[1]); }
        if (variant == 0) return;
        switch (b)
        {
            case Biome.Desert:
                // ripples of wind-blown sand, or a bleached pebble
                for (int i = 0; i < 3; i++) { int y = rng.Next(4, 28), x = rng.Next(0, 18); for (int dx = 0; dx < 10; dx++) c.Put(x + dx, y + (dx / 4) % 2, g[3]); }
                if (variant == 4) { c.Put(14, 14, g[4]); c.Put(15, 14, g[4]); c.Put(15, 13, g[1]); }
                break;
            case Biome.Tundra:
                // tufts of frozen grass poking through the snow
                for (int i = 0; i < 3 + variant; i++) { int x = rng.Next(2, 30), y = rng.Next(2, 28); c.Put(x, y, P.Pine[2]); c.Put(x, y + 1, P.Pine[1]); }
                break;
            case Biome.Swamp:
                // a stagnant puddle with a lily pad, or reeds
                if (variant % 2 == 1) { c.Ellipse(new Vector2(16, 14), 7, 4, new[] { P.Hex("1e2a20"), P.Hex("2a3c2e"), P.Hex("3c5240"), P.Hex("4e6a52") }); c.Put(14, 15, P.Leaf[3]); c.Put(15, 15, P.Leaf[3]); }
                else for (int i = 0; i < 4; i++) { int x = rng.Next(4, 28), y = rng.Next(4, 24); for (int h = 0; h < 4; h++) c.Put(x, y + h, h == 3 ? P.Wood[1] : P.Leaf[2]); }
                break;
            case Biome.Volcanic:
                // cracks glowing with lava, or a scatter of black glass
                if (variant % 2 == 1) { int x = rng.Next(4, 20), y = rng.Next(6, 26); for (int i = 0; i < 9; i++) { c.Put(x + i, y + (i % 3 == 0 ? 1 : 0), i % 2 == 0 ? P.Fire[2] : P.Fire[1]); } }
                else for (int i = 0; i < 4; i++) { int x = rng.Next(2, 30), y = rng.Next(2, 30); c.Put(x, y, P.Coal[0]); c.Put(x, y + 1, P.Steel[3]); }
                break;
            case Biome.Barren:
                // a small crater, or scattered grey stones
                if (variant % 2 == 1) { c.Ellipse(new Vector2(rng.Next(10, 22), rng.Next(10, 22)), 5, 3.5f, new[] { g[1], g[1], g[0], g[3] }); }
                else for (int i = 0; i < 4; i++) { int x = rng.Next(2, 30), y = rng.Next(2, 30); c.Put(x, y, g[4]); c.Put(x + 1, y, g[1]); }
                break;
            case Biome.Jungle:
                // fallen leaves and a red flower
                for (int i = 0; i < 4 + variant; i++) { int x = rng.Next(2, 30), y = rng.Next(2, 30); c.Put(x, y, P.Leaf[3]); c.Put(x + 1, y + 1, P.Leaf[1]); }
                if (variant >= 3) { c.Put(16, 16, P.Red[3]); c.Put(17, 16, P.Red[2]); c.Put(16, 17, P.Red[2]); c.Put(15, 16, P.Red[2]); c.Put(16, 15, P.Glow[3]); }
                break;
            case Biome.Fungal:
                // spore dots glowing in the spongy ground
                for (int i = 0; i < 3 + variant; i++) { int x = rng.Next(2, 30), y = rng.Next(2, 30); c.Put(x, y, i % 2 == 0 ? P.Hex("f0a0e8") : P.Neon[3]); }
                break;
            default:
                // little crystal shards glinting in the violet grass
                for (int i = 0; i < 2 + variant; i++) { int x = rng.Next(3, 29), y = rng.Next(3, 27); c.Put(x, y, P.Aether[2]); c.Put(x, y + 1, P.Aether[3]); c.Put(x + 1, y, P.Aether[1]); }
                break;
        }
    }

    /// <summary>Two trees per biome, in place of the temperate oaks and pines.</summary>
    private static void BiomeTree(PixelCanvas c, Biome b, int kind)
    {
        int cx = c.W / 2;
        var rng = new System.Random((int)b * 13 + kind);
        switch (b)
        {
            case Biome.Desert when kind == 0:
            {
                // a tall saguaro cactus with two arms
                var g = P.Green;
                c.Capsule(new Vector2(cx, 4), new Vector2(cx, 44), 4.5f, g);
                c.Capsule(new Vector2(cx - 4, 20), new Vector2(cx - 11, 22), 3, g); c.Capsule(new Vector2(cx - 11, 22), new Vector2(cx - 11, 34), 3, g);
                c.Capsule(new Vector2(cx + 4, 26), new Vector2(cx + 10, 28), 3, g); c.Capsule(new Vector2(cx + 10, 28), new Vector2(cx + 10, 38), 3, g);
                for (int y = 8; y < 44; y += 4) c.Put(cx - 2, y, g[3]);
                c.Put(cx, 47, P.Flower[0]); c.Put(cx + 1, 47, P.Flower[1]);
                break;
            }
            case Biome.Desert:
            {
                // a palm: a leaning ringed trunk and drooping fronds
                for (int y = 3; y < 48; y++) { int x = cx + (y - 3) / 8; c.Put(x - 1, y, P.Wood[3]); c.Put(x, y, y % 4 == 0 ? P.Wood[1] : P.Wood[2]); c.Put(x + 1, y, P.Wood[1]); }
                Vector2 top = new Vector2(cx + 5, 49);
                for (int f = 0; f < 6; f++)
                {
                    float a = f * Mathf.PI / 5f;
                    Vector2 tip = top + new Vector2(Mathf.Cos(a) * 16, Mathf.Sin(a) * 6 - 6);
                    c.Line(top, tip, P.Leaf[2], 2);
                    c.Line(top + new Vector2(0, 1), tip + new Vector2(0, 1), P.Leaf[3]);
                }
                c.Ellipse(top, 2.5f, 2, P.Wood);
                break;
            }
            case Biome.Tundra when kind == 0:
            {
                // a pine heavy with snow
                for (int y = 3; y < 14; y++) for (int x = cx - 2; x <= cx + 2; x++) c.Put(x, y, x == cx - 2 ? P.Wood[3] : P.Wood[2]);
                foreach (var (bottom, top, half) in new[] { (10, 36, 17), (26, 52, 13), (42, 67, 9) })
                    for (int y = bottom; y <= top; y++)
                    {
                        int w = Mathf.RoundToInt(half * (1 - (y - bottom) / (float)(top - bottom)));
                        for (int x = cx - w; x <= cx + w; x++)
                        {
                            bool snow = y > bottom + (top - bottom) * 0.55f || (y - bottom) % 6 < 2 && x < cx;
                            c.Put(x, y, snow ? (x < cx ? P.White : P.Linen[3]) : x < cx ? P.Pine[2] : P.Pine[1]);
                        }
                    }
                break;
            }
            case Biome.Tundra:
            {
                // a bare white birch, twigs dusted with frost
                for (int y = 3; y < 50; y++) { c.Put(cx, y, y % 5 == 0 ? P.Coal[1] : P.White); c.Put(cx + 1, y, P.Linen[2]); }
                for (int i = 0; i < 6; i++)
                {
                    int y = 22 + i * 5, dir = i % 2 == 0 ? 1 : -1;
                    c.Line(new Vector2(cx, y), new Vector2(cx + dir * (8 + i), y + 8), P.Linen[2]);
                    c.Put(cx + dir * (8 + i), y + 9, P.White);
                }
                break;
            }
            case Biome.Swamp when kind == 0:
            {
                // a gnarled dead tree hung with moss
                c.Capsule(new Vector2(cx, 3), new Vector2(cx + 2, 36), 3, P.Wood);
                c.Line(new Vector2(cx + 1, 30), new Vector2(cx - 12, 44), P.Wood[1], 2);
                c.Line(new Vector2(cx + 2, 34), new Vector2(cx + 14, 46), P.Wood[1], 2);
                c.Line(new Vector2(cx + 2, 36), new Vector2(cx + 1, 50), P.Wood[2], 2);
                foreach (var (x, y) in new[] { (cx - 10, 42), (cx + 12, 44), (cx - 4, 38), (cx + 6, 40) })
                    for (int h = 0; h < 7; h++) c.Put(x, y - h, h % 2 == 0 ? P.Leaf[1] : P.Pine[2]);
                break;
            }
            case Biome.Swamp:
            {
                // a mangrove on stilt roots with a dark round crown
                for (int i = -2; i <= 2; i++) c.Line(new Vector2(cx + i * 4, 3), new Vector2(cx + i, 16), P.Wood[1], 2);
                c.Capsule(new Vector2(cx, 16), new Vector2(cx, 30), 2.5f, P.Wood);
                c.Ellipse(new Vector2(cx, 40), 16, 12, new[] { P.Hex("16301e"), P.Hex("204428"), P.Hex("2e5a32"), P.Hex("447440"), P.Hex("5c8c50") });
                break;
            }
            case Biome.Volcanic when kind == 0:
            {
                // a charred dead tree, embers glowing in its bark
                c.Capsule(new Vector2(cx, 3), new Vector2(cx, 40), 3, P.Coal);
                c.Line(new Vector2(cx, 28), new Vector2(cx - 10, 42), P.Coal[1], 2);
                c.Line(new Vector2(cx, 34), new Vector2(cx + 11, 46), P.Coal[1], 2);
                for (int i = 0; i < 5; i++) c.Put(cx - 1 + rng.Next(0, 3), 8 + i * 6, i % 2 == 0 ? P.Fire[2] : P.Glow[2]);
                break;
            }
            case Biome.Volcanic:
            {
                // a spire of black volcanic glass
                c.Polygon(new[] { new Vector2(cx - 9, 3), new Vector2(cx + 9, 3), new Vector2(cx + 2, 52) }, l => l.x < cx - 2 ? P.Coal[3] : l.x < cx + 3 ? P.Coal[2] : P.Coal[1]);
                c.Polygon(new[] { new Vector2(cx + 4, 3), new Vector2(cx + 14, 3), new Vector2(cx + 10, 28) }, l => P.Coal[2]);
                c.Line(new Vector2(cx - 3, 6), new Vector2(cx + 1, 40), P.Steel[3]);
                break;
            }
            case Biome.Crystal when kind == 0:
            {
                // a cluster of tall violet crystals
                foreach (var (x, h, w) in new[] { (cx - 8, 34, 4), (cx, 54, 6), (cx + 9, 40, 4) })
                    c.Polygon(new[] { new Vector2(x - w, 3), new Vector2(x + w, 3), new Vector2(x + w, h - w * 2), new Vector2(x, h), new Vector2(x - w, h - w * 2) },
                        l => l.x < x - 1 ? P.Aether[3] : l.x < x + 2 ? P.Aether[2] : P.Aether[1]);
                break;
            }
            case Biome.Barren when kind == 0:
            {
                // a jagged spire of grey rock
                c.Polygon(new[] { new Vector2(cx - 10, 3), new Vector2(cx + 9, 3), new Vector2(cx + 3, 40), new Vector2(cx - 1, 46), new Vector2(cx - 4, 30) },
                    l => l.x < cx - 3 ? P.Stone[3] : l.x < cx + 2 ? P.Stone[2] : P.Stone[1]);
                c.Line(new Vector2(cx - 2, 8), new Vector2(cx + 1, 36), P.Stone[4]);
                break;
            }
            case Biome.Barren:
            {
                // a boulder balanced on a stone pillar
                c.Rect(cx - 3, 3, cx + 3, 26, P.Stone[2]); c.Line(new Vector2(cx - 3, 3), new Vector2(cx - 3, 26), P.Stone[3]);
                c.Ellipse(new Vector2(cx, 34), 11, 8, P.Stone);
                break;
            }
            case Biome.Jungle when kind == 0:
            {
                // a giant broad-crowned jungle tree hung with vines
                c.Capsule(new Vector2(cx, 3), new Vector2(cx, 40), 4, P.Wood);
                for (int i = -2; i <= 2; i++) c.Line(new Vector2(cx + i * 3, 3), new Vector2(cx + i, 10), P.Wood[1], 2);   // buttress roots
                var crown = new[] { P.Hex("103018"), P.Hex("1a4422"), P.Hex("28602e"), P.Hex("3a7c3a"), P.Hex("58a04c") };
                c.Ellipse(new Vector2(cx - 8, 48), 13, 9, crown); c.Ellipse(new Vector2(cx + 8, 50), 13, 9, crown); c.Ellipse(new Vector2(cx, 56), 14, 9, crown);
                foreach (int x in new[] { cx - 14, cx - 4, cx + 10 }) for (int h = 0; h < 12; h++) c.Put(x + (h % 4 == 0 ? 1 : 0), 42 - h, P.Leaf[2]);
                break;
            }
            case Biome.Jungle:
            {
                // a tree fern: a slim trunk and a burst of arching fronds
                for (int y = 3; y < 38; y++) { c.Put(cx, y, y % 3 == 0 ? P.Wood[1] : P.Wood[2]); c.Put(cx + 1, y, P.Wood[1]); }
                Vector2 top = new Vector2(cx, 39);
                for (int f = 0; f < 8; f++)
                {
                    float a = f * Mathf.PI / 7f;
                    Vector2 tip = top + new Vector2(Mathf.Cos(a) * 15, Mathf.Sin(a) * 9 - 4);
                    c.Line(top, tip, P.Leaf[2], 2);
                    c.Line(top + new Vector2(0, 1), tip + new Vector2(0, 1), P.Leaf[3]);
                }
                break;
            }
            case Biome.Fungal when kind == 0:
            {
                // a towering mushroom with a pink cap spotted white
                c.Capsule(new Vector2(cx, 3), new Vector2(cx + 2, 40), 4, P.Linen);
                var cap = new[] { P.Hex("5a1e46"), P.Hex("8a2e6a"), P.Hex("c04a90"), P.Hex("e07ab4"), P.Hex("f8b8dc") };
                c.Ellipse(new Vector2(cx + 2, 44), 19, 11, cap, l => l.y >= 38);
                for (int i = 0; i < 6; i++) c.Ellipse(new Vector2(cx - 12 + rng.Next(0, 26), 44 + rng.Next(0, 7)), 1.5f, 1.2f, new[] { P.Linen[3], P.White, P.White, P.White });
                break;
            }
            case Biome.Fungal:
            {
                // a cluster of tall puffball stalks
                foreach (var (x, h, r) in new[] { (cx - 8, 28, 6f), (cx + 1, 40, 7f), (cx + 10, 22, 5f) })
                {
                    c.Line(new Vector2(x, 3), new Vector2(x, 3 + h), P.Hex("c8a8c0"), 3);
                    c.Ellipse(new Vector2(x, 3 + h + r * 0.5f), r, r, new[] { P.Hex("6a3a5e"), P.Hex("9a5a88"), P.Hex("c886b4"), P.Hex("eab8da") });
                }
                break;
            }
            default:
            {
                // a giant glowing mushroom
                c.Capsule(new Vector2(cx, 3), new Vector2(cx, 34), 4, P.Linen);
                c.Ellipse(new Vector2(cx, 40), 18, 10, new[] { P.Hex("1a3a5c"), P.Hex("2a5a8a"), P.Hex("3a8ac8"), P.Hex("6ac0f0"), P.Hex("c8f0ff") }, l => l.y >= 34);
                for (int i = 0; i < 7; i++) c.Put(cx - 14 + rng.Next(0, 28), 38 + rng.Next(0, 9), P.Neon[3]);
                break;
            }
        }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(cx + 2, 3, 11, 3, 70);
    }

    /// <summary>A berry bush for a biome: prickly pear, frost berries, mushrooms, fire-blooms, glow-fungi.</summary>
    private static void BiomeBush(PixelCanvas c, Biome b)
    {
        int cx = c.W / 2;
        switch (b)
        {
            case Biome.Desert:
                c.Ellipse(new Vector2(cx - 5, 8), 5, 6, P.Green); c.Ellipse(new Vector2(cx + 4, 11), 5, 6, P.Green); c.Ellipse(new Vector2(cx, 17), 4, 5, P.Green);
                foreach (var (x, y) in new[] { (cx - 5, 14), (cx + 4, 17), (cx, 22) }) { c.Put(x, y, P.Berry[2]); c.Put(x + 1, y, P.Berry[3]); }
                break;
            case Biome.Tundra:
                c.Ellipse(new Vector2(cx, 10), 10, 7, P.Pine);
                for (int i = 0; i < 8; i++) c.Put(cx - 8 + i * 2, 8 + (i % 3) * 3, i % 2 == 0 ? P.Blue[3] : P.Blue[2]);
                c.Line(new Vector2(cx - 9, 15), new Vector2(cx + 9, 15), P.White);
                break;
            case Biome.Swamp:
                foreach (var (x, h, r) in new[] { (cx - 6, 8, 4f), (cx + 2, 12, 5f), (cx + 9, 6, 3f) })
                {
                    c.Line(new Vector2(x, 3), new Vector2(x, 3 + h), P.Linen[2], 2);
                    c.Ellipse(new Vector2(x, 3 + h), r, r * 0.6f, P.Red);
                    c.Put(x - 1, 4 + h, P.White);
                }
                break;
            case Biome.Volcanic:
                c.Ellipse(new Vector2(cx, 9), 9, 6, P.Coal);
                for (int i = 0; i < 6; i++) { int x = cx - 7 + i * 3; c.Line(new Vector2(x, 9), new Vector2(x, 15 + i % 2 * 2), P.Fire[1]); c.Put(x, 16 + i % 2 * 2, P.Fire[3]); }
                break;
            case Biome.Barren:
                // grey stones furred with glowing lichen
                c.Ellipse(new Vector2(cx - 4, 7), 6, 4.5f, P.Stone); c.Ellipse(new Vector2(cx + 5, 9), 5, 4, P.Stone);
                for (int i = 0; i < 6; i++) c.Put(cx - 8 + i * 3, 9 + (i % 2) * 2, P.Ray[2]);
                break;
            case Biome.Jungle:
                // broad leaves round a bunch of red fruit
                c.Ellipse(new Vector2(cx - 6, 9), 7, 4, P.Leaf); c.Ellipse(new Vector2(cx + 6, 10), 7, 4, P.Leaf); c.Ellipse(new Vector2(cx, 14), 6, 4, P.Leaf);
                foreach (var (x, y) in new[] { (cx - 1, 11), (cx + 1, 12), (cx, 9) }) { c.Put(x, y, P.Red[2]); c.Put(x + 1, y, P.Red[3]); }
                break;
            case Biome.Fungal:
                foreach (var (x, h, r) in new[] { (cx - 6, 5, 3.5f), (cx + 1, 9, 4.5f), (cx + 8, 6, 3f) })
                {
                    c.Line(new Vector2(x, 3), new Vector2(x, 3 + h), P.Linen[3], 2);
                    c.Ellipse(new Vector2(x, 3 + h), r, r * 0.7f, new[] { P.Hex("8a2e6a"), P.Hex("c04a90"), P.Hex("e07ab4"), P.Hex("f8b8dc") });
                }
                break;
            default:
                foreach (var (x, h, r) in new[] { (cx - 6, 6, 3f), (cx + 1, 10, 4.5f), (cx + 8, 7, 3f) })
                {
                    c.Line(new Vector2(x, 3), new Vector2(x, 3 + h), P.Linen[3], 2);
                    c.Ellipse(new Vector2(x, 3 + h), r, r * 0.7f, P.Neon);
                }
                break;
        }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(cx + 1, 3, 11, 2.5f, 70);
    }
}
