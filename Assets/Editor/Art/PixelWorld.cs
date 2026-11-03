using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// The map in pixel art (32 pixels a tile): grass, hills in three terraces, water with sandy
/// shores, trees, bushes, stone and ore deposits, the rally flag and building scaffolds. Hill and
/// water shapes use the same corner field as before (TerrainArt), sampled per pixel into crisp
/// colour bands, so neighbouring tiles still join into smooth coastlines.
/// </summary>
public static partial class PlaceholderArt
{
    private const int TilePx = 32;

    [MenuItem("Tools/Age of Stickman/Generate World Art")]
    public static void GenerateWorldArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        GeneratePixelTerrain(art);
        GeneratePixelProps(art, database);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("World art generated.");
    }

    // ---------- Terrain ----------

    private static void GeneratePixelTerrain(ArtLibrary art)
    {
        ResetDir("World/Terrain", keepMaterials: true);
        art.grassVariants = new List<Sprite>
        {
            TilePixel("World/Terrain/Grass0.png", c => PixelGrass(c, 11, 0, 0, 0)),
            TilePixel("World/Terrain/Grass1.png", c => PixelGrass(c, 31, 3, 0, 0)),
            TilePixel("World/Terrain/Grass2.png", c => PixelGrass(c, 47, 0, 3, 0)),
            TilePixel("World/Terrain/Grass3.png", c => PixelGrass(c, 59, 0, 0, 1)),
            TilePixel("World/Terrain/Grass4.png", c => PixelGrass(c, 73, 1, 0, 0)),
        };
        art.grassTile = art.grassVariants[0];
        art.settlementTile = TilePixel("World/SettlementGround.png", c => PixelGrass(c, 23, 0, 1, 0, dry: true));
        art.pathTile = TilePixel("World/Path.png", c => PixelDirt(c, 5));
        art.waterTile = TilePixel("World/Water.png", c => PixelWaterShape(c, 15));
        art.hillTile = TilePixel("World/Hill.png", c => PixelHillShape(c, 15, 29, 1));

        art.hillShapes = PixelHillTier(1);
        art.hillShapes2 = PixelHillTier(2);
        art.hillShapes3 = PixelHillTier(3);
        art.hillFullVariants = new List<Sprite>
        {
            EdgePixel("World/Terrain/Hill_15b.png", c => PixelHillShape(c, 15, 61, 1)),
            EdgePixel("World/Terrain/Hill_15c.png", c => PixelHillShape(c, 15, 97, 1)),
        };
        art.waterFrameCount = 1;
        art.waterFps = 3f;
        art.waterMaterial = WaterMaterial();
        art.waterFrames = new List<Sprite>();
        for (int mask = 0; mask < 16; mask++)
        {
            int m = mask;
            art.waterFrames.Add(mask == 0 ? null : EdgePixel($"World/Terrain/Water_{m:00}_0.png", c => PixelWaterShape(c, m)));
        }
    }

    private static List<Sprite> PixelHillTier(int level)
    {
        var shapes = new List<Sprite> { null };
        string suffix = level == 1 ? "" : "_T" + level;
        for (int mask = 1; mask < 16; mask++)
        {
            int m = mask;
            shapes.Add(EdgePixel($"World/Terrain/Hill{suffix}_{m:00}.png", c => PixelHillShape(c, m, 29 + level * 13, level)));
        }
        return shapes;
    }

    private static Sprite TilePixel(string path, System.Action<PixelCanvas> draw)
    {
        var c = new PixelCanvas(TilePx, TilePx);
        draw(c);
        return SavePixel(c, path, PixelsPerTile, Vector2.zero, tile: true);
    }

    private static Sprite EdgePixel(string path, System.Action<PixelCanvas> draw)
    {
        var c = new PixelCanvas(TilePx, TilePx);
        draw(c);
        return SavePixel(c, path, PixelsPerTile, Vector2.zero);
    }

    private static void ResetDir(string dir, bool keepMaterials)
    {
        string full = Path.Combine(Root, dir);
        Directory.CreateDirectory(full);
        foreach (var old in Directory.GetFiles(full, "*.png")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
    }

    /// <summary>Calm grass: one main tone, scattered little blades (wrapping so tiles join), optional flowers, pebbles or clover.</summary>
    private static void PixelGrass(PixelCanvas c, int seed, int flowers, int pebbles, int clover, bool dry = false)
    {
        var g = dry ? new[] { P.Hex("2f4a26"), P.Hex("4f7034"), P.Hex("6f9446"), P.Hex("98b864"), P.Hex("c4d88a") } : P.Grass;
        c.Rect(0, 0, TilePx - 1, TilePx - 1, g[2]);
        var rng = new System.Random(seed);
        int W(int v) => ((v % TilePx) + TilePx) % TilePx;
        if (clover > 0)
        {
            int cx = 8 + rng.Next(16), cy = 8 + rng.Next(16);
            for (int i = 0; i < 9; i++)
            {
                int x = cx + rng.Next(-5, 6), y = cy + rng.Next(-3, 4);
                c.Put(W(x), W(y), g[1]);
                c.Put(W(x + 1), W(y), g[3]);
            }
        }
        for (int i = 0; i < 11; i++)
        {
            int x = rng.Next(TilePx), y = rng.Next(TilePx);
            c.Put(x, y, g[3]);
            c.Put(x, W(y + 1), rng.NextDouble() < 0.5 ? g[3] : g[4]);
            c.Put(x, W(y - 1), g[1]);
            if (rng.NextDouble() < 0.5)
            {
                c.Put(W(x + 1), y, g[3]);
                c.Put(W(x + 1), W(y - 1), g[1]);
            }
        }
        for (int i = 0; i < pebbles; i++)
        {
            int x = 2 + rng.Next(TilePx - 4), y = 2 + rng.Next(TilePx - 4);
            c.Put(x, y, P.Stone[3]); c.Put(x + 1, y, P.Stone[2]); c.Put(x, y - 1, P.Stone[1]); c.Put(x + 1, y - 1, g[1]);
        }
        for (int i = 0; i < flowers; i++)
        {
            int x = 2 + rng.Next(TilePx - 4), y = 2 + rng.Next(TilePx - 4);
            var petal = P.Flower[rng.Next(P.Flower.Length)];
            c.Put(x - 1, y, petal); c.Put(x + 1, y, petal); c.Put(x, y + 1, petal); c.Put(x, y - 1, g[1]);
            c.Put(x, y, P.Gold[2]);
        }
    }

    private static void PixelDirt(PixelCanvas c, int seed)
    {
        var d = P.Dirt;
        c.Rect(0, 0, TilePx - 1, TilePx - 1, d[2]);
        var rng = new System.Random(seed);
        for (int i = 0; i < 14; i++)
        {
            int x = rng.Next(TilePx), y = rng.Next(TilePx);
            c.Put(x, y, i % 3 == 0 ? d[3] : d[1]);
        }
        for (int i = 0; i < 4; i++)
        {
            int x = rng.Next(TilePx - 1), y = rng.Next(1, TilePx);
            c.Put(x, y, P.Stone[3]); c.Put(x, y - 1, P.Stone[1]);
        }
    }

    /// <summary>Pixel centres in 0..1 tile space.</summary>
    private static Vector2 Q(int x, int y) => new Vector2((x + 0.5f) / TilePx, (y + 0.5f) / TilePx);

    private static void PixelWaterShape(PixelCanvas c, int mask)
    {
        var w = P.Water;
        for (int y = 0; y < TilePx; y++)
            for (int x = 0; x < TilePx; x++)
            {
                float h = Height(mask, Q(x, y), true, 0.05f);
                if (h < 0.5f) continue;
                Color32 col = h < 0.525f ? P.Sand[2] : h < 0.545f ? P.Sand[1] : h < 0.565f ? w[4] : h < 0.68f ? w[3] : h < 0.82f ? w[2] : w[1];
                c.Put(x, y, col);
            }
        // a few glints in open water
        var rng = new System.Random(mask * 13 + 1);
        for (int i = 0; i < 3; i++)
        {
            int x = 3 + rng.Next(TilePx - 6), y = 3 + rng.Next(TilePx - 6);
            if (Height(mask, Q(x, y), true, 0.05f) > 0.75f)
            {
                c.Put(x, y, w[3]); c.Put(x + 1, y, w[3]); c.Put(x + 2, y, w[3]);
            }
        }
    }

    private static void PixelHillShape(PixelCanvas c, int mask, int seed, int level)
    {
        // Higher terraces catch more light.
        var r = P.Rock;
        Color32 L(int i) => PixelCanvas.Mix(r[Mathf.Clamp(i, 0, 4)], P.White, (level - 1) * 0.07f);
        float cliffTop = 0.66f + level * 0.04f;
        var edgeShade = level == 1 ? new Color32(30, 60, 30, 110) : new Color32(40, 30, 24, 100);
        for (int y = 0; y < TilePx; y++)
            for (int x = 0; x < TilePx; x++)
            {
                var q = Q(x, y);
                float h = Height(mask, q, false, 0.06f);
                CornerField(mask, q, false, out var g);
                float facing = g.sqrMagnitude < 0.0001f ? 0 : g.y / g.magnitude;
                bool south = facing > 0.2f;
                if (h < 0.5f)
                {
                    if (h > 0.42f || (south && h > 0.32f)) c.Put(x, y, edgeShade);
                    continue;
                }
                Color32 col;
                if (south && h < cliffTop)
                {
                    float mid = (0.56f + cliffTop) * 0.5f;
                    col = h < 0.56f ? L(0) : Mathf.Abs(h - mid) < 0.012f ? L(0) : ((x + y) % 7 == 0 ? L(0) : L(1));
                }
                else if (south && h < cliffTop + 0.035f) col = L(4);
                else if (!south && h < 0.56f) col = L(3);
                else col = h > 0.86f ? L(3) : L(2);
                c.Put(x, y, col);
            }
        // pebbles and cracks well inside the top
        var rng = new System.Random(seed + mask * 7);
        for (int i = 0; i < 7; i++)
        {
            int x = 4 + rng.Next(TilePx - 8), y = 4 + rng.Next(TilePx - 8);
            if (Height(mask, Q(x, y), false, 0.06f) < cliffTop + 0.08f) continue;
            if (i % 2 == 0) { c.Put(x, y, L(1)); c.Put(x + 1, y, L(1)); c.Put(x, y + 1, L(4)); }
            else { c.Put(x, y, L(1)); c.Put(x + 1, y + 1, L(1)); c.Put(x + 2, y + 1, L(1)); }
        }
    }

    // ---------- Props ----------

    private static void GeneratePixelProps(ArtLibrary art, GameDatabase database)
    {
        ResetDir("World/Props", keepMaterials: false);
        var berry = PropPixel("World/Props/BerryBush.png", 30, 24, c => PixelBush(c, 4, true));
        art.trees = new List<Sprite>
        {
            PropPixel("World/Props/TreeRound.png", 48, 64, c => PixelRoundTree(c, 3)),
            PropPixel("World/Props/TreeRound2.png", 48, 64, c => PixelRoundTree(c, 8)),
            PropPixel("World/Props/TreePine.png", 40, 70, c => PixelPineTree(c, 5)),
        };
        art.bushes = new List<Sprite> { PropPixel("World/Props/Bush.png", 30, 22, c => PixelBush(c, 2, false)) };
        art.rallyFlag = PropPixel("World/Props/RallyFlag.png", 20, 40, PixelRallyFlag);
        var scaffold = new PixelCanvas(TilePx, TilePx);
        PixelScaffold(scaffold);
        art.scaffold = SavePixel(scaffold, "World/Props/Scaffold.png", PixelsPerTile, Vector2.zero, tile: true);

        // stone and ore deposits
        ResetDir("World/Deposits", keepMaterials: false);
        art.rocks = PixelDeposits("Stone", 4, Ore.None);
        var copper = PixelDeposits("Copper", 3, Ore.Copper);
        var iron = PixelDeposits("Iron", 3, Ore.Iron);
        var gold = PixelDeposits("Gold", 3, Ore.Gold);
        var runes = PixelDeposits("Runestone", 3, Ore.Rune);
        var saltpetre = PixelDeposits("Saltpetre", 3, Ore.Saltpetre);
        var quicksilver = PixelDeposits("Quicksilver", 3, Ore.Quicksilver);
        if (database == null) return;
        foreach (var deposit in database.deposits)
        {
            if (deposit == null) continue;
            switch (deposit.id)
            {
                case "Stone": deposit.sprites = new List<Sprite>(art.rocks); break;
                case "CopperOre": deposit.sprites = copper; break;
                case "IronOre": deposit.sprites = iron; break;
                case "GoldOre": deposit.sprites = gold; break;
                case "Runestone": deposit.sprites = runes; break;
                case "Saltpetre": deposit.sprites = saltpetre; break;
                case "Quicksilver": deposit.sprites = quicksilver; break;
                case "Forest": deposit.sprites = new List<Sprite>(art.trees); break;
                case "Berries": deposit.sprites = new List<Sprite> { berry }; break;
                default: continue;
            }
            EditorUtility.SetDirty(deposit);
        }
    }

    /// <summary>A prop standing on the ground: pivot at the middle of its base row (+ shadow room).</summary>
    private static Sprite PropPixel(string path, int w, int h, System.Action<PixelCanvas> draw)
    {
        var c = new PixelCanvas(w, h);
        draw(c);
        return SavePixel(c, path, PixelsPerTile, new Vector2((w / 2 + 0.5f) / w, 3f / h));
    }

    private static void PixelRoundTree(PixelCanvas c, int seed)
    {
        int cx = c.W / 2;
        var rng = new System.Random(seed);
        var wood = P.Wood;
        for (int y = 3; y < 22; y++)
        {
            int half = y > 6 ? 2 : 3 + (6 - y) / 2;
            for (int x = cx - half; x <= cx + half; x++)
                c.Put(x, y, x == cx - half ? wood[3] : x >= cx + half - 1 ? wood[1] : wood[2]);
        }
        var leaf = P.Leaf;
        var blobs = new (float x, float y, float rx, float ry)[] { (cx, 37, 15, 13), (cx - 9, 31, 9, 8), (cx + 9, 31, 9, 8), (cx - 5, 45, 10, 9), (cx + 6, 46, 9, 8), (cx, 53, 8, 6) };
        foreach (var b in blobs)
        {
            float jx = (float)rng.NextDouble() * 2 - 1;
            c.Ellipse(new Vector2(b.x + jx, b.y), b.rx, b.ry, leaf);
        }
        for (int i = 0; i < 46; i++)
        {
            int x = rng.Next(cx - 18, cx + 18), y = rng.Next(24, 60);
            if (c.Get(x, y).a < 200) continue;
            bool lit = (x - cx) * -0.5f + (y - 40) * 0.8f > 0;
            c.Put(x, y, lit ? leaf[4] : leaf[1]);
            c.Put(x + 1, y, lit ? leaf[3] : leaf[1]);
        }
        if (seed % 2 == 1)
            for (int i = 0; i < 4; i++)
            {
                int x = rng.Next(cx - 11, cx + 11), y = rng.Next(29, 50);
                if (c.Get(x, y).a > 200) { c.Put(x, y, P.Red[2]); c.Put(x, y + 1, P.Red[3]); }
            }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(cx + 2, 3, 13, 3, 70);
    }

    private static void PixelPineTree(PixelCanvas c, int seed)
    {
        int cx = c.W / 2;
        for (int y = 3; y < 14; y++)
            for (int x = cx - 2; x <= cx + 2; x++)
                c.Put(x, y, x == cx - 2 ? P.Wood[3] : x >= cx + 1 ? P.Wood[1] : P.Wood[2]);
        var pine = P.Pine;
        // three stacked tiers, each a jagged cone
        var tiers = new (int bottom, int top, int half)[] { (10, 36, 17), (26, 52, 13), (42, 67, 9) };
        foreach (var (bottom, top, half) in tiers)
        {
            for (int y = bottom; y <= top; y++)
            {
                float t = (y - bottom) / (float)(top - bottom);
                int w = Mathf.RoundToInt(half * (1 - t)) + ((y % 3 == 0 && t < 0.8f) ? 1 : 0);
                for (int x = cx - w; x <= cx + w; x++)
                {
                    float side = (x - cx) / (float)Mathf.Max(1, w);
                    Color32 col = side < -0.45f ? pine[3] : side > 0.4f ? pine[1] : pine[2];
                    if (y == bottom && (x + seed) % 3 == 0) col = pine[0];
                    if (side < -0.6f && y % 4 == 1) col = pine[4];
                    c.Put(x, y, col);
                }
            }
        }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(cx + 2, 3, 11, 3, 70);
    }

    private static void PixelBush(PixelCanvas c, int seed, bool berries)
    {
        int cx = c.W / 2;
        var leaf = P.Leaf;
        c.Ellipse(new Vector2(cx - 6, 9), 7, 6, leaf);
        c.Ellipse(new Vector2(cx + 6, 9), 7, 6, leaf);
        c.Ellipse(new Vector2(cx, 13), 9, 8, leaf);
        var rng = new System.Random(seed);
        for (int i = 0; i < 12; i++)
        {
            int x = rng.Next(cx - 12, cx + 12), y = rng.Next(5, 20);
            if (c.Get(x, y).a < 200) continue;
            c.Put(x, y, (x - cx) * -0.5f + (y - 12) > 0 ? leaf[4] : leaf[1]);
        }
        if (berries)
            for (int i = 0; i < 9; i++)
            {
                int x = rng.Next(cx - 11, cx + 11), y = rng.Next(5, 19);
                if (c.Get(x, y).a < 200) continue;
                c.Put(x, y, P.Berry[2]); c.Put(x, y + 1, P.Berry[3]); c.Put(x + 1, y, P.Berry[1]);
            }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(cx + 1, 3, 12, 2.5f, 70);
    }

    private static void PixelRallyFlag(PixelCanvas c)
    {
        int x = 6;
        for (int y = 3; y < 37; y++) { c.Put(x, y, P.Wood[2]); c.Put(x + 1, y, P.Wood[1]); }
        c.Put(x, 37, P.Gold[3]); c.Put(x + 1, 37, P.Gold[2]);
        for (int y = 24; y < 35; y++)
        {
            int len = 11 - Mathf.Abs(y - 29) / 2;
            for (int i = 0; i < len; i++)
                c.Put(x + 2 + i, y + (i > 6 ? -1 : 0), y > 31 ? P.Blue[3] : y < 26 ? P.Blue[1] : P.Blue[2]);
        }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(x + 2, 3, 4, 1.5f, 70);
    }

    private static void PixelScaffold(PixelCanvas c)
    {
        var w = P.Wood;
        for (int y = 0; y < TilePx; y++)
        {
            c.Put(2, y, w[3]); c.Put(3, y, w[2]);
            c.Put(28, y, w[2]); c.Put(29, y, w[1]);
        }
        for (int x = 0; x < TilePx; x++)
        {
            c.Put(x, 14, w[3]); c.Put(x, 15, w[2]);
            c.Put(x, 30, w[2]); c.Put(x, 31, w[1]);
        }
        for (int i = 0; i < 12; i++) { c.Put(4 + i * 2, 16 + i, w[1]); c.Put(5 + i * 2, 16 + i, w[1]); }
        for (int i = 0; i < 12; i++) { c.Put(4 + i * 2, 13 - i, w[1]); c.Put(5 + i * 2, 13 - i, w[1]); }
        c.Put(3, 15, P.Steel[2]); c.Put(28, 15, P.Steel[2]); c.Put(3, 31, P.Steel[2]); c.Put(28, 31, P.Steel[2]);   // lashings
    }

    /// <summary>Dark blue-grey runestone.</summary>
    private static readonly Color32[] Slate = { P.Hex("1e2230"), P.Hex("343a4c"), P.Hex("4c5468"), P.Hex("6a7388"), P.Hex("8e97aa") };

    private static List<Sprite> PixelDeposits(string name, int count, Ore ore)
    {
        var sprites = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            int v = i;
            sprites.Add(PropPixel($"World/Deposits/{name}{i}.png", 32, 26, c => PixelDeposit(c, v, ore)));
        }
        return sprites;
    }

    /// <summary>A few chunky boulders (lit tops, shaded sides) with ore veins showing.</summary>
    private static void PixelDeposit(PixelCanvas c, int variant, Ore ore)
    {
        var rng = new System.Random(variant * 31 + (int)ore * 7);
        if (ore == Ore.Saltpetre)
        {
            // not rock: a white crust of nitre on bare dry earth, with a few glinting crystals
            c.Ellipse(new Vector2(16, 8), 12 - variant, 5.5f, P.Dirt);
            for (int i = 0; i < 28; i++)
            {
                int x = rng.Next(6, 27), y = rng.Next(4, 13);
                if (c.Get(x, y).a < 200) continue;
                c.Put(x, y, i % 4 == 0 ? P.White : P.Linen[3]);
                if (i % 5 == 0) c.Put(x, y + 1, P.Linen[2]);
            }
            c.Outline(P.Ink, 0.4f);
            return;
        }
        var rock = ore == Ore.None ? P.Stone : ore == Ore.Rune ? Slate : P.Rock;
        if (ore == Ore.Quicksilver || ore == Ore.Aether) rock = P.Stone;
        (float x, float y, float rx, float ry)[] stones = variant switch
        {
            0 => new (float x, float y, float rx, float ry)[] { (16, 10, 10, 7) },
            1 => new[] { (11f, 9f, 7f, 5.5f), (21f, 8f, 6f, 4.5f), (16f, 14f, 6f, 5f) },
            2 => new[] { (15f, 9f, 11f, 5f), (22f, 12f, 5f, 4f) },
            _ => new[] { (9f, 7f, 5f, 4f), (17f, 9f, 6f, 5f), (24f, 7f, 4f, 3f), (13f, 13f, 4f, 3.5f) },
        };
        foreach (var s in stones)
        {
            c.Ellipse(new Vector2(s.x, s.y), s.rx, s.ry, rock);
            // a flat lit top face
            c.Ellipse(new Vector2(s.x - s.rx * 0.15f, s.y + s.ry * 0.35f), s.rx * 0.6f, s.ry * 0.4f, new[] { rock[3], rock[3], rock[3], rock[4], rock[4] });
        }
        var vein = ore switch
        {
            Ore.Copper => new[] { P.Hex("3f9c7a"), P.Hex("d9783a"), P.Hex("6fd0a0") },
            Ore.Iron => new[] { P.Hex("6e2e22"), P.Hex("a4503a"), P.Hex("c8785a") },
            Ore.Gold => new[] { P.Gold[1], P.Gold[2], P.Gold[3] },
            Ore.Rune => new[] { P.Rune[2], P.Rune[1], P.Rune[3] },
            Ore.Quicksilver => new[] { P.Red[1], P.Red[2], P.Steel[3] },   // red cinnabar, silver beads
            Ore.Coal => new[] { P.Coal[0], P.Coal[1], P.Iron[3] },          // black seams with an oily sheen
            Ore.Aether => new[] { P.Aether[1], P.Aether[2], P.Aether[3] },
            _ => null,
        };
        if (ore == Ore.Rune)
        {
            // carved runes glowing on the biggest stone's face
            var s0 = stones[0];
            int gx = (int)s0.x - 3, gy = (int)s0.y - 2;
            for (int i = 0; i < 2; i++)
            {
                int x = gx + i * 5;
                for (int y = 0; y < 4; y++) c.Put(x, gy + y, P.Rune[2]);
                c.Put(x + 1, gy + 2, P.Rune[3]); c.Put(x - 1, gy + 1, P.Rune[2]);
            }
        }
        if (ore == Ore.Aether)
        {
            // violet crystals jutting from the rock
            foreach (var (x, y, h) in new[] { (12, 12, 5), (17, 14, 7), (22, 11, 4) })
            {
                for (int d = 0; d < h; d++) { c.Put(x, y + d, P.Aether[2]); c.Put(x + 1, y + d, d == h - 1 ? P.Aether[3] : P.Aether[1]); }
                c.Put(x, y + h, P.Aether[3]);
            }
        }
        for (int i = 0; i < (vein != null ? (ore == Ore.Coal ? 16 : 10) : 4); i++)
        {
            int x = rng.Next(4, 28), y = rng.Next(4, 20);
            if (c.Get(x, y).a < 200) continue;
            if (vein == null) { c.Put(x, y, rock[1]); continue; }
            c.Put(x, y, vein[i % 2]);
            if (i % 3 == 0) c.Put(x + 1, y + 1, vein[2]);   // a glint
        }
        c.Outline(P.Ink, 0.55f);
        c.Shadow(16, 3, 13, 2.5f, 70);
    }
}
