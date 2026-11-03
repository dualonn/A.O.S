using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Culture and faith buildings in pixel art, age by age: a story circle and a shrine, bards' hall
/// and temple, amphitheatre, cathedral, playhouse, opera house and basilica, museum, picture palace,
/// a chrome broadcast palace and a lightning chapel, a data-arts hub, arcane theatre and sanctum,
/// a star dome and sun temple, and the cosmic choir and cathedral. Upgrades add banners and lights.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> CultureBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "CulStoryCircle", (PxCulStoryCircle, 4, 5f) }, { "CulBardsHall", (PxCulBardsHall, 4, 4f) },
            { "CulAmphitheatre", (PxCulAmphitheatre, 4, 3f) }, { "CulPlayhouse", (PxCulPlayhouse, 4, 3f) },
            { "CulOperaHouse", (PxCulOperaHouse, 4, 3f) }, { "CulMuseum", (PxCulMuseum, 4, 2f) },
            { "CulCinema", (PxCulCinema, 4, 6f) }, { "CulBroadcastPalace", (PxCulBroadcast, 4, 5f) },
            { "CulDataArtsHub", (PxCulDataArts, 4, 5f) }, { "CulIllusionTheatre", (PxCulIllusion, 4, 4f) },
            { "CulStarDome", (PxCulStarDome, 4, 3f) }, { "CulCosmicChoir", (PxCulCosmicChoir, 4, 4f) },
            { "FthShrine", (PxFthShrine, 4, 4f) }, { "FthTemple", (PxFthTemple, 4, 4f) },
            { "FthCathedral", (PxFthCathedral, 4, 2f) }, { "FthBasilica", (PxFthBasilica, 4, 2f) },
            { "FthLightningChapel", (PxFthLightning, 4, 8f) }, { "FthArcaneSanctum", (PxFthSanctum, 4, 4f) },
            { "FthSunTemple", (PxFthSunTemple, 4, 4f) }, { "FthCosmicCathedral", (PxFthCosmic, 4, 3f) },
        };

    private static readonly Color32[] CulVerdigris = { P.Hex("23443e"), P.Hex("3a7465"), P.Hex("62a68e"), P.Hex("a6d8bf") };
    private static readonly Color32[] CulOchre = { P.Hex("6a2e14"), P.Hex("a8501e"), P.Hex("d88a3a"), P.Hex("f2c27a") };
    private static readonly Color32[] CulVelvet = { P.Hex("3a0e1a"), P.Hex("6e1a2c"), P.Hex("a8283e"), P.Hex("e0586a") };
    private static readonly Color32[] CulGlass = { P.Hex("2a4a6a"), P.Hex("4a7aa0"), P.Hex("8ab8d8"), P.Hex("d6ecf8") };

    /// <summary>Draws a culture or faith building's looks (one per upgrade level), like the other buildings'.</summary>
    private static void GenerateCultureLooks(BuildingDefinition def)
    {
        if (!CultureBuildingArt.TryGetValue(def.id, out var art)) return;
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = Mathf.RoundToInt(def.ArtSize.x * TilePx), artH = Mathf.RoundToInt(def.ArtSize.y * TilePx);
        int w = artW + MarginSide * 2, h = artH + MarginBottom + MarginTop;
        var pivot = new Vector2(MarginSide / (float)w, MarginBottom / (float)h);
        def.looks = new List<BuildingLook>();
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            var look = new BuildingLook { fps = art.fps };
            for (int f = 0; f < art.frames; f++)
            {
                var c = new PixelCanvas(w, h);
                var k = new K(c, MarginSide, MarginBottom, level, f, art.frames, def.id.GetHashCode());
                art.draw(k);
                look.frames.Add(SavePixel(c, $"{dir}/{def.id}_L{level}_F{f}.png", PixelsPerTile, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        UnityEditor.EditorUtility.SetDirty(def);
    }

    // ---------- Shared pieces ----------

    /// <summary>An ellipse outline (rings, arcs); only the part where 'only' holds, if given.</summary>
    private static void CulRing(K k, float cx, float cy, float rx, float ry, Color32 col, System.Func<float, bool> only = null)
    {
        int n = Mathf.CeilToInt((rx + ry) * 3f);
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            float x = cx + Mathf.Cos(a) * rx, y = cy + Mathf.Sin(a) * ry;
            if (only != null && !only(a)) continue;
            k.Px(Mathf.RoundToInt(x), Mathf.RoundToInt(y), col);
        }
    }

    /// <summary>A row of bulbs that chase along with the frames.</summary>
    private static void CulBulbs(K k, int x0, int x1, int y, int step = 3)
    {
        for (int x = x0, i = 0; x <= x1; x += step, i++)
            k.Px(x, y, (i + k.Frame) % 3 == 0 ? P.Glow[3] : (i + k.Frame) % 3 == 1 ? P.Glow[2] : P.Glow[1]);
    }

    /// <summary>A star that twinkles.</summary>
    private static void CulStar(K k, int x, int y, int phase, Color32 col)
    {
        bool bright = (k.Frame + phase) % 4 == 0;
        k.Px(x, y, col);
        if (bright) { k.Px(x - 1, y, col); k.Px(x + 1, y, col); k.Px(x, y - 1, col); k.Px(x, y + 1, col); }
    }

    /// <summary>Stars scattered in a region (same every frame, twinkling).</summary>
    private static void CulStarfield(K k, int x0, int y0, int x1, int y1, int count, System.Func<int, int, bool> inside = null)
    {
        var rng = new System.Random(x0 * 31 + y0 * 17 + count);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(x0, x1 + 1), y = rng.Next(y0, y1 + 1);
            if (inside != null && !inside(x, y)) continue;
            CulStar(k, x, y, i, i % 5 == 0 ? P.Gold[3] : P.White);
        }
    }

    /// <summary>A comedy-and-tragedy pair of masks.</summary>
    private static void CulMasks(K k, int x, int y)
    {
        k.Ellipse(x, y, 3.5f, 4, P.Bone);
        k.Ellipse(x + 7, y - 1, 3.5f, 4, P.Gold);
        k.Px(x - 1, y + 1, P.Ink); k.Px(x + 1, y + 1, P.Ink); k.HLine(x - 1, x + 1, y - 2, P.Ink); k.Px(x - 2, y - 1, P.Ink); k.Px(x + 2, y - 1, P.Ink);
        k.Px(x + 6, y, P.Ink); k.Px(x + 8, y, P.Ink); k.HLine(x + 6, x + 8, y - 3, P.Ink); k.Px(x + 5, y - 4, P.Ink); k.Px(x + 9, y - 4, P.Ink);
    }

    /// <summary>Upgrades show: a banner at level 1, a second at level 2.</summary>
    private static void CulBanners(K k, int xLeft, int xRight, int y, int h, Color32[] ramp)
    {
        if (k.Level >= 1) k.Banner(xLeft, y, h, ramp);
        if (k.Level >= 2) k.Banner(xRight, y, h, ramp);
    }

    // ---------- Stone to Classical ----------

    private static void PxCulStoryCircle(K k)
    {
        k.Ground(2, 2, 61, 52, P.Dirt);
        // The painted stone at the back: ochre hands and a hunt.
        Block(k, 42, 28, 52, 56, P.Rock);
        k.HLine(42, 52, 56, P.Rock[4]);
        k.Px(45, 48, CulOchre[2]); k.Px(46, 49, CulOchre[2]); k.Px(44, 49, CulOchre[2]); k.Px(45, 50, CulOchre[2]);
        k.Line(44, 38, 50, 38, CulOchre[1]); k.Px(49, 40, CulOchre[1]); k.Px(48, 41, CulOchre[1]); k.Px(46, 42, CulOchre[3]);
        if (k.Level >= 2) { k.Rect(10, 30, 12, 54, P.Wood[1]); k.Rect(9, 54, 13, 57, P.Red[2]); k.Px(11, 50, P.Gold[2]); }   // a carved totem
        // Log seats in a ring round the fire.
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f + 0.3f;
            int x = Mathf.RoundToInt(30 + Mathf.Cos(a) * 20), y = Mathf.RoundToInt(24 + Mathf.Sin(a) * 12);
            k.Rect(x - 4, y, x + 4, y + 3, P.Wood[2]);
            k.HLine(x - 4, x + 4, y + 3, P.Wood[3]);
            k.Px(x - 4, y + 1, P.Wood[0]); k.Px(x + 4, y + 1, P.Wood[0]);
        }
        k.Rocks(25, 20, 3);
        k.Fire(27, 23, 7, 10);
        k.Smoke(30, 36, 0.8f, 120);
        if (k.Level >= 1) { k.Pot(16, 14, P.Mud); k.Rect(46, 12, 52, 18, P.Leather[2]); k.HLine(46, 52, 18, P.Leather[3]); }   // a drum
        k.Finish();
    }

    private static void PxFthShrine(K k)
    {
        k.Ground(2, 2, 61, 52, P.Grass);
        // A low ring of stones.
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(32 + Mathf.Cos(a) * 24), y = Mathf.RoundToInt(26 + Mathf.Sin(a) * 14);
            k.Ellipse(x, y, 2.5f, 2f, P.Stone);
        }
        // The spirit post, its face carved and painted.
        k.Rect(28, 22, 35, 60, P.Wood[2]);
        k.VLine(28, 22, 60, P.Wood[3]); k.VLine(35, 22, 60, P.Wood[0]);
        k.Rect(26, 50, 37, 53, P.Wood[1]);
        k.Px(30, 56, P.Ink); k.Px(33, 56, P.Ink); k.HLine(30, 33, 45, CulOchre[1]); k.Px(31, 44, P.Ink); k.Px(32, 44, P.Ink);
        k.Rect(30, 40, 33, 41, P.Bone[2]);
        k.Polygon(new[] { new Vector2(26, 60), new Vector2(37, 60), new Vector2(31.5f, 66) }, _ => P.Red[2]);
        // The offering stone: flowers and a flickering candle.
        Block(k, 18, 10, 46, 18, P.Stone);
        k.Px(22, 19, P.Flower[0]); k.Px(24, 19, P.Flower[1]); k.Px(42, 19, P.Flower[2]); k.Px(40, 20, P.Flower[0]);
        k.Rect(31, 19, 32, 22, P.Bone[3]);
        k.Px(31, 23 + k.Frame % 2, P.Fire[3]); k.Px(32, 23, P.Fire[2]);
        if (k.Level >= 1)
            for (int x = 4; x <= 60; x += 4)   // prayer flags on a string
            {
                int y = 48 - Mathf.RoundToInt(Mathf.Sin((x - 4) / 56f * Mathf.PI) * 6);
                var c = (x / 4) % 4 == 0 ? P.Red[2] : (x / 4) % 4 == 1 ? P.Gold[2] : (x / 4) % 4 == 2 ? P.Blue[2] : P.Green[3];
                k.Rect(x, y - 3 + (k.Frame + x / 4) % 2, x + 2, y, c);
            }
        if (k.Level >= 2) { k.Torch(12, 20); k.Torch(52, 20); }
        k.Finish();
    }

    private static void PxCulBardsHall(K k)
    {
        k.Ground(2, 2, 61, 44, P.Dirt);
        int top = Hall(k, 5, 58, 6, 24, K.Mat.Logs, K.RoofType.Thatch, 26, 12, 2, true, false, false);
        // Carved dragon heads on the gable ends, crossed.
        k.Line(17, top + 24, 12, top + 32, P.Wood[2], 2); k.Line(46, top + 24, 51, top + 32, P.Wood[2], 2);
        k.Px(11, top + 33, P.Wood[3]); k.Px(52, top + 33, P.Wood[3]);
        // The door, a harp painted above it.
        k.Door(27, 7, 10, 14, true, true);
        k.Px(31, 12, P.Glow[2]); k.Px(32, 13, P.Glow[3]);   // firelight inside
        k.Line(28, 23, 35, 29, P.Gold[2]); k.VLine(28, 23, 29, P.Gold[2]); k.HLine(28, 35, 29, P.Gold[1]);
        for (int x = 30; x <= 34; x += 2) k.Line(x, 24 + (x - 28) * 6 / 7, x, 29, P.Gold[3]);
        k.Torch(22, 7); k.Torch(41, 7);
        CulBanners(k, 2, 60, 6, 26, P.Red);
        k.Finish(32, top + 20);
    }

    private static void PxFthTemple(K k)
    {
        k.Ground(2, 2, 61, 44, P.Dirt);
        for (int i = 0; i < 3; i++) k.Rect(4 + i * 2, 4 + i * 2, 59 - i * 2, 5 + i * 2, P.Marble[3 - (i % 2)]);
        Block(k, 12, 10, 51, 44, P.Marble);
        k.Door(27, 10, 10, 16, true, true);
        k.Px(31, 16 + k.Frame % 2, P.Fire[3]);   // the inner flame
        foreach (int x in new[] { 9, 18, 41, 50 }) k.Column(x, 10, 34);
        k.Rect(8, 44, 55, 48, P.Marble[2]);
        k.Polygon(new[] { new Vector2(6, 49), new Vector2(57, 49), new Vector2(31.5f, 66) }, l => l.x < 26 ? (Color32?)P.Marble[3] : P.Marble[2]);
        k.Line(6, 49, 57, 49, P.Gold[2]);
        k.Ellipse(31.5f, 55, 3, 3, P.Gold);
        // A bronze brazier with the sacred fire, out front.
        k.Rect(30, 2, 33, 6, P.Bronze[1]); k.Rect(27, 6, 36, 8, P.Bronze[2]);
        k.Fire(28, 9, 8, 9);
        CulBanners(k, 0, 60, 8, 30, P.Purple);
        k.Finish();
    }

    private static void PxCulAmphitheatre(K k)
    {
        k.Ground(2, 2, 93, 90, P.Sand);
        // Stone tiers in a half circle, back rows first.
        for (int i = 5; i >= 0; i--)
        {
            var ramp = i % 2 == 0 ? P.Stone : P.Rock;
            float rx = 18 + i * 6, ry = 10 + i * 11;
            k.Ellipse(48, 20, rx, ry, ramp, p => p.y >= 20);
            CulRing(k, 48, 20, rx, ry, ramp[3], a => Mathf.Sin(a) >= 0);
            if (k.Level >= 2 && i == 5) CulBulbs(k, 20, 76, 20 + Mathf.RoundToInt(ry) - 2, 6);
        }
        // A crowd in the seats (dots of cloth), and the stage with its columned wall.
        var rng = new System.Random(5);
        for (int i = 0; i < 40; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI, r = 0.35f + (float)rng.NextDouble() * 0.6f;
            int x = Mathf.RoundToInt(48 + Mathf.Cos(a) * 48 * r), y = Mathf.RoundToInt(20 + Mathf.Sin(a) * 64 * r);
            if (y < 32) continue;
            k.Px(x, y, (i % 4) switch { 0 => P.Red[2], 1 => P.Blue[2], 2 => P.Linen[2], _ => P.Green[2] });
            k.Px(x, y + 1, P.Skin[2]);
        }
        Block(k, 26, 4, 70, 14, P.Wood);
        k.Rect(24, 14, 72, 16, P.Marble[3]);
        Colonnade(k, 26, 70, 16, 12, 9);
        k.Rect(24, 28, 72, 30, P.Marble[2]);
        CulMasks(k, 44, 9);
        CulBanners(k, 12, 82, 12, 22, P.Purple);
        k.Finish();
    }

    // ---------- Medieval to Industrial ----------

    private static void PxFthCathedral(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        // The nave, two towers with spires, buttresses between.
        k.Wall(22, 8, 73, 64, K.Mat.Stone);
        k.Roof(20, 75, 64, 18, K.RoofType.Slate, 22);
        for (int t = 0; t < 2; t++)
        {
            int x0 = t == 0 ? 6 : 72, x1 = x0 + 17;
            k.Wall(x0, 8, x1, 92, K.Mat.Stone);
            k.Polygon(new[] { new Vector2(x0 - 1, 92), new Vector2(x1 + 1, 92), new Vector2((x0 + x1) / 2f, 124) },
                l => l.x < (x0 + x1) / 2f ? (Color32?)P.Iron[2] : P.Iron[1]);
            k.Rect(x0 + 6, 66, x1 - 6, 78, P.Coal[1]);   // belfry
            if (t == 1)
            {
                int swing = k.Frame % 2 == 0 ? -1 : 1;   // the bell, ringing
                k.Ellipse((x0 + x1) / 2f + swing, 70, 3, 3, P.Bronze);
            }
            k.Window(x0 + 6, 36, 6, 12, k.Level >= 1);
            k.Px((x0 + x1) / 2, 125, P.Gold[3]);
        }
        // The rose window.
        k.Ellipse(48, 46, 11, 11, P.Stone);
        k.Ellipse(48, 46, 9, 9, new[] { P.Blue[1], P.Blue[2], P.Red[2], P.Gold[3] });
        for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; k.Line(48, 46, 48 + Mathf.Cos(a) * 9, 46 + Mathf.Sin(a) * 9, P.Stone[1]); }
        k.Ellipse(48, 46, 2, 2, P.Gold);
        // The great pointed door.
        k.Polygon(new[] { new Vector2(41, 8), new Vector2(55, 8), new Vector2(55, 24), new Vector2(48, 32), new Vector2(41, 24) }, _ => P.Stone[1]);
        k.Door(43, 8, 10, 16, true);
        CulBanners(k, 2, 92, 8, 30, P.Blue);
        k.Finish();
    }

    private static void PxCulPlayhouse(K k)
    {
        k.Ground(2, 2, 93, 44, P.Dirt);
        // A many-sided timber drum, whitewashed, its thatch round an open yard.
        k.Wall(8, 6, 87, 54, K.Mat.Plaster);
        for (int x = 18; x < 86; x += 14) k.VLine(x, 6, 54, P.Wood[1]);
        for (int x = 12; x < 86; x += 14) { k.Window(x, 34, 5, 7, true); k.Window(x, 18, 5, 7, k.Level >= 1); }
        k.Roof(4, 91, 54, 14, K.RoofType.Thatch, 8);
        k.Ellipse(48, 66, 30, 4, P.Coal);   // the open yard
        // The stage house on top, the flag up: there's a play on today.
        k.Wall(40, 62, 56, 76, K.Mat.Plaster);
        k.Roof(38, 58, 76, 6, K.RoofType.Tile, 4);
        k.Banner(47, 82, 24, P.Red, false);
        k.Door(41, 6, 14, 16, false, true);
        CulMasks(k, 44, 69);
        CulBanners(k, 0, 88, 8, 24, P.Gold);
        if (k.Level >= 2) CulBulbs(k, 10, 86, 55, 4);
        k.Finish();
    }

    private static void PxCulOperaHouse(K k)
    {
        k.Ground(2, 2, 93, 44, P.Dirt);
        for (int i = 0; i < 3; i++) k.Rect(6 + i * 3, 3 + i * 2, 89 - i * 3, 4 + i * 2, P.Marble[3 - (i % 2)]);
        Block(k, 10, 9, 85, 62, P.Plaster);
        // Ground-floor arcade, then tall arched windows, gilded.
        for (int x = 14; x < 82; x += 12)
        {
            k.Rect(x, 9, x + 7, 22, CulVelvet[0]);
            k.Px(x + 1, 22, P.Plaster[3]); k.Px(x + 6, 22, P.Plaster[3]);
            k.Window(x + 1, 30, 6, 14, true);
            k.HLine(x, x + 7, 45, P.Gold[2]);
        }
        Colonnade(k, 12, 84, 24, 20, 12);
        k.Rect(8, 62, 87, 65, P.Plaster[3]);
        k.HLine(8, 87, 66, P.Gold[2]);
        // The green copper dome with a golden lyre on top.
        k.Ellipse(48, 66, 22, 20, CulVerdigris, p => p.y >= 66);
        for (int x = 32; x <= 64; x += 8) k.Line(x, 67, 48 + (x - 48) * 0.3f, 84, CulVerdigris[0]);
        k.Rect(45, 86, 51, 90, CulVerdigris[2]);
        k.Line(45, 91, 47, 98, P.Gold[2]); k.Line(51, 91, 49, 98, P.Gold[2]); k.HLine(46, 50, 98, P.Gold[3]);
        k.VLine(48, 92, 97, P.Gold[3]);
        // Statues at the corners.
        k.Rect(10, 66, 13, 74, P.Marble[2]); k.Rect(82, 66, 85, 74, P.Marble[2]);
        CulBanners(k, 2, 88, 10, 30, CulVelvet);
        if (k.Level >= 2) CulBulbs(k, 10, 86, 63, 3);
        k.Finish();
    }

    private static void PxFthBasilica(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        Block(k, 6, 8, 89, 56, P.Marble);
        Colonnade(k, 10, 86, 8, 36, 10);
        k.Rect(4, 44, 91, 48, P.Marble[3]);
        k.Polygon(new[] { new Vector2(28, 49), new Vector2(68, 49), new Vector2(48, 62) }, l => l.x < 44 ? (Color32?)P.Marble[3] : P.Marble[2]);
        k.Door(42, 8, 12, 18, true);
        // The drum and the great dome, a lantern and a golden orb on top.
        Block(k, 28, 56, 67, 72, P.Marble);
        for (int x = 31; x < 66; x += 6) k.Window(x, 60, 3, 8, k.Level >= 1);
        k.Ellipse(48, 72, 22, 26, new[] { P.Iron[1], P.Iron[2], P.Steel[2], P.Steel[3] }, p => p.y >= 72);
        for (int i = -2; i <= 2; i++) k.Line(48 + i * 8, 73, 48 + i * 2, 96, P.Gold[1]);
        k.Rect(45, 97, 51, 104, P.Marble[2]);
        k.Ellipse(48, 108, 3, 3, P.Gold);
        if (k.Frame % 2 == 0) k.Px(47, 109, P.White);
        // Two little bell towers.
        for (int t = 0; t < 2; t++)
        {
            int x0 = t == 0 ? 8 : 76;
            Block(k, x0, 56, x0 + 11, 78, P.Marble);
            k.Ellipse(x0 + 5.5f, 78, 6, 6, P.Gold, p => p.y >= 78);
        }
        CulBanners(k, 0, 92, 8, 28, P.Gold);
        k.Finish();
    }

    private static void PxCulMuseum(K k)
    {
        k.Ground(2, 2, 93, 44, P.Dirt);
        // The glass-and-iron hall behind.
        for (int y = 46; y <= 76; y++)
        {
            int half = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((y - 46) / 30f, 2))) * 38);
            for (int x = 48 - half; x <= 48 + half; x++)
                k.Px(x, y, (x - 10) % 8 == 0 || y % 7 == 0 ? P.Iron[1] : ((x + y) % 11 == 0 ? CulGlass[3] : CulGlass[2]));
        }
        // Steps, columns and the pediment, banners of the exhibitions.
        for (int i = 0; i < 4; i++) k.Rect(4 + i * 3, 3 + i * 2, 91 - i * 3, 4 + i * 2, P.Marble[3 - (i % 2)]);
        Block(k, 12, 11, 83, 46, P.Marble);
        Colonnade(k, 14, 82, 11, 30, 11);
        k.Rect(10, 41, 85, 45, P.Marble[3]);
        k.Polygon(new[] { new Vector2(8, 46), new Vector2(87, 46), new Vector2(48, 62) }, l => l.x < 40 ? (Color32?)P.Marble[3] : P.Marble[2]);
        k.Line(8, 46, 87, 46, P.Marble[1]);
        k.Door(42, 11, 12, 18, true);
        k.Rect(20, 22, 25, 36, P.Red[2]); k.Rect(70, 22, 75, 36, P.Blue[2]);   // exhibition banners
        k.Px(22, 30, P.Gold[3]); k.Px(72, 30, P.Gold[3]);
        if (k.Level >= 1) { k.Rect(2, 4, 8, 10, P.Bronze[2]); k.Rect(4, 10, 6, 18, P.Bronze[1]); }   // a bronze statue
        if (k.Level >= 2) { k.Rect(88, 4, 94, 10, P.Bronze[2]); k.Ellipse(91, 14, 3, 4, P.Bronze); }
        k.Finish();
    }

    // ---------- Modern to Information ----------

    private static void PxCulCinema(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        ConcreteBlock(k, 8, 6, 87, 64);
        // Stepped art-deco top and fluted piers.
        k.Rect(30, 64, 65, 72, Concrete[2]); k.Rect(38, 72, 57, 80, Concrete[2]); k.Rect(44, 80, 51, 86, Concrete[3]);
        for (int x = 12; x < 86; x += 18) { k.VLine(x, 40, 64, Concrete[3]); k.VLine(x + 1, 40, 64, Concrete[1]); }
        // The marquee, a hundred bulbs chasing round it.
        k.Rect(10, 28, 85, 38, CulVelvet[1]);
        k.Rect(12, 30, 83, 36, P.Linen[3]);
        for (int x = 16; x < 80; x += 4) k.VLine(x, 32, 34, P.Ink);   // the letters of tonight's film
        CulBulbs(k, 10, 85, 38, 3); CulBulbs(k, 10, 85, 28, 3);
        // The tall blade sign.
        k.Rect(44, 42, 51, 92, CulVelvet[2]);
        for (int y = 46; y < 90; y += 4) k.Px(47 + (y / 4 + k.Frame) % 2, y, P.Glow[3]);
        // Posters and the doors.
        k.Rect(14, 10, 23, 24, P.Gold[2]); k.Rect(16, 12, 21, 22, P.Blue[2]);
        k.Rect(72, 10, 81, 24, P.Gold[2]); k.Rect(74, 12, 79, 22, P.Red[2]);
        k.Rect(34, 6, 61, 24, CulGlass[1]); for (int x = 34; x <= 61; x += 7) k.VLine(x, 6, 24, P.Brass[2]);
        if (k.Level >= 1)   // searchlights sweeping the sky
        {
            float sweep = Mathf.Sin(k.T * Mathf.PI * 2f) * 10f;
            var beam = new Color32(255, 244, 196, 150);
            k.Line(20, 64, 8 + sweep, 110, beam); k.Line(76, 64, 88 - sweep, 110, beam);
        }
        if (k.Level >= 2) CulBulbs(k, 30, 65, 72, 3);
        k.Finish();
    }

    private static void PxCulBroadcast(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        // A streamlined chrome block with rounded ends and speed lines.
        for (int y = 6; y <= 60; y++)
            for (int x = 8; x <= 87; x++)
            {
                float dx = x < 20 ? 20 - x : x > 75 ? x - 75 : 0, dy = y > 48 ? y - 48 : 0;
                if (dx * dx + dy * dy > 144) continue;
                k.Px(x, y, (y - 6) % 9 == 0 ? P.Chrome[1] : x < 16 ? P.Chrome[3] : P.Chrome[2]);
            }
        for (int x = 14; x < 82; x += 9) k.Window(x, 30, 5, 8, true);
        k.Rect(36, 6, 59, 22, CulGlass[1]);
        k.HLine(30, 65, 24, P.Neon[2]); k.HLine(30, 65, 25, P.Neon[3]);
        // The mast, a lattice to the sky, sending rings out.
        for (int y = 60; y < 150; y++)
        {
            int half = Mathf.RoundToInt(Mathf.Lerp(8, 1, (y - 60) / 90f));
            k.Px(48 - half, y, P.Iron[2]); k.Px(48 + half, y, P.Iron[1]);
            if (y % 6 == 0) k.Line(48 - half, y, 48 + half, y + 6, P.Iron[2]);
        }
        k.Ellipse(48, 151, 3, 3, P.Red);
        for (int i = 0; i < 3; i++)
        {
            float r = 6 + ((i * 4 + k.Frame * 3) % 12) * 1.6f;
            CulRing(k, 48, 146, r, r * 0.7f, i == 0 ? P.Neon[3] : P.Neon[2], a => Mathf.Abs(Mathf.Cos(a)) > 0.5f);
        }
        if (k.Level >= 1) { k.Rect(10, 60, 13, 76, P.Chrome[2]); k.Ellipse(11.5f, 78, 3, 3, P.Neon); }
        if (k.Level >= 2) { k.Rect(82, 60, 85, 76, P.Chrome[2]); k.Ellipse(83.5f, 78, 3, 3, P.Neon); }
        k.Finish();
    }

    private static void PxFthLightning(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        // A brass chapel with neon-lit stained glass.
        Block(k, 14, 6, 81, 58, P.Brass);
        for (int x = 20; x < 78; x += 14)
        {
            k.Rect(x, 22, x + 7, 46, P.Iron[1]);
            k.Rect(x + 1, 23, x + 6, 45, (x / 14 + k.Frame) % 3 == 0 ? P.Neon[3] : P.Neon[2]);
            k.Px(x + 3, 47, P.Iron[1]); k.Px(x + 4, 47, P.Iron[1]);
            k.HLine(x + 1, x + 6, 34, P.Iron[1]);
        }
        k.Roof(10, 85, 58, 14, K.RoofType.Slate, 18);
        k.Door(42, 6, 12, 16, true);
        // The spire, a coil at its tip, calling down the storm.
        k.Polygon(new[] { new Vector2(40, 72), new Vector2(56, 72), new Vector2(48, 128) }, l => l.x < 48 ? (Color32?)P.Brass[2] : P.Brass[1]);
        for (int y = 76; y < 124; y += 5) k.HLine(48 - (128 - y) * 8 / 56, 48 + (128 - y) * 8 / 56, y, P.Brass[3]);
        k.Ellipse(48, 132, 5, 5, P.Chrome);
        var rng = new System.Random(k.Frame * 7 + 3);
        for (int b = 0; b < 2; b++)
        {
            float x = 48, y = 136;
            float dir = b == 0 ? -1 : 1;
            for (int s = 0; s < 6; s++)
            {
                float nx = x + dir * (2 + rng.Next(0, 4)), ny = y + 2 + rng.Next(0, 3);
                k.Line(x, y, nx, ny, s % 2 == 0 ? P.White : P.Neon[3]);
                x = nx; y = ny;
            }
        }
        CulBanners(k, 4, 86, 6, 28, P.Neon);
        k.Finish();
    }

    private static void PxCulDataArts(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        ConcreteBlock(k, 6, 4, 89, 10);
        // A cube of screens, each showing something different every frame.
        var rng = new System.Random(11);
        for (int gy = 0; gy < 6; gy++)
            for (int gx = 0; gx < 8; gx++)
            {
                int x0 = 10 + gx * 10, y0 = 12 + gy * 10;
                k.Rect(x0, y0, x0 + 9, y0 + 9, P.Tactical[0]);
                int pick = (rng.Next(0, 4) + k.Frame + gx) % 4;
                var col = P.Prism[pick];
                k.Rect(x0 + 1, y0 + 1, x0 + 8, y0 + 8, pick % 2 == 0 ? P.Neon[1] : P.Synth[0]);
                if ((gx + gy + k.Frame) % 3 == 0) k.Rect(x0 + 2, y0 + 2, x0 + 7, y0 + 7, col);
                else k.Line(x0 + 1, y0 + 1 + (gx * 3 + k.Frame) % 7, x0 + 8, y0 + 8 - (gy * 2) % 7, col);
            }
        k.Rect(38, 10, 57, 24, CulGlass[1]); k.VLine(47, 10, 24, P.Chrome[2]);
        // A hologram turning in the air above it.
        float t = k.T * Mathf.PI * 2f;
        k.Ellipse(48, 86, 10 * Mathf.Abs(Mathf.Cos(t)) + 2, 10, new[] { P.Prism[3], P.Prism[2], P.Prism[1], P.White });
        k.Line(48, 72, 48, 76, P.Neon[2]);
        if (k.Level >= 1) CulBulbs(k, 8, 88, 73, 4);
        if (k.Level >= 2) { k.Line(6, 74, 18, 96, P.Neon[2]); k.Line(89, 74, 77, 96, P.Neon[2]); }
        k.Finish();
    }

    // ---------- Arcane to Galactic ----------

    private static void PxCulIllusion(K k)
    {
        k.Ground(2, 2, 93, 40, P.Moonstone);
        Block(k, 10, 6, 85, 46, P.Moonstone);
        for (int x = 16; x < 82; x += 13) { k.Rect(x, 14, x + 6, 34, P.Arcane[0]); k.Px(x + 3, 35, P.Arcane[2]); }
        k.Door(42, 6, 12, 18, true, true);
        // A great tent dome of violet silk, with dreams floating out of it.
        k.Polygon(new[] { new Vector2(4, 46), new Vector2(92, 46), new Vector2(48, 108) },
            l => l.x < 34 ? (Color32?)P.Arcane[3] : l.x < 60 ? P.Arcane[2] : P.Arcane[1]);
        k.Ellipse(48, 46, 44, 6, P.Arcane, p => p.y >= 46);
        for (int x = 8; x <= 88; x += 8) k.Line(x, 47, 48, 106, P.Arcane[1]);
        k.Polygon(new[] { new Vector2(6, 46), new Vector2(90, 46), new Vector2(48, 108) }, l => ((int)l.x + (int)l.y) % 9 == 0 ? (Color32?)P.Arcane[3] : null);
        k.Ellipse(48, 112, 4, 4, P.Mana);
        float t = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 5; i++)
        {
            float a = t + i * 1.256f;
            Glow(k, 48 + Mathf.Cos(a) * 34, 88 + Mathf.Sin(a * 2) * 10 + i * 3, 2, i % 2 == 0 ? P.Mana : P.Arcane);
        }
        CulMasks(k, 44, 36);
        CulBanners(k, 2, 86, 8, 28, P.Arcane);
        k.Finish();
    }

    private static void PxFthSanctum(K k)
    {
        k.Ground(2, 2, 93, 44, P.Moonstone);
        // A round sanctum of pale stone round a well of mana.
        Block(k, 14, 6, 81, 40, P.Moonstone);
        for (int x = 18; x < 80; x += 10) k.Rect(x, 12, x + 4, 32, P.Rune[1]);
        k.Ellipse(48, 42, 30, 7, P.Mana);
        k.Ellipse(48, 42, 20, 4, new[] { P.Mana[2], P.Mana[3], P.White, P.White });
        // A crystal floating over it, and rings of runes turning round it.
        float bob = Mathf.Sin(k.T * Mathf.PI * 2f) * 2f;
        k.Polygon(new[] { new Vector2(48, 60 + bob), new Vector2(56, 76 + bob), new Vector2(48, 96 + bob), new Vector2(40, 76 + bob) },
            l => l.x < 48 ? (Color32?)P.Rune[3] : P.Rune[2]);
        k.Line(48, 61 + bob, 48, 95 + bob, P.White);
        for (int r = 0; r < 3; r++)
        {
            float rx = 18 + r * 9, ry = 4 + r * 2, cy = 62 + r * 14;
            CulRing(k, 48, cy, rx, ry, P.Rune[2]);
            for (int i = 0; i < 6; i++)
            {
                float a = k.T * Mathf.PI * 2f * (r % 2 == 0 ? 1 : -1) + i * 1.047f;
                k.Px(Mathf.RoundToInt(48 + Mathf.Cos(a) * rx), Mathf.RoundToInt(cy + Mathf.Sin(a) * ry), P.Rune[3]);
            }
        }
        CulBanners(k, 4, 84, 8, 28, P.Rune);
        k.Finish();
    }

    private static void PxCulStarDome(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        // A ring of starmetal round the base, and a dome showing the whole night sky.
        Block(k, 8, 6, 87, 28, P.Starmetal);
        for (int x = 12; x < 84; x += 8) k.Px(x, 18, (x / 8 + k.Frame) % 2 == 0 ? P.Solar[3] : P.Solar[1]);
        k.Door(42, 6, 12, 16, true, true);
        k.Ellipse(48, 28, 40, 54, P.Cosmic, p => p.y >= 28);
        CulStarfield(k, 10, 30, 86, 80, 60, (x, y) => Mathf.Pow((x - 48) / 39f, 2) + Mathf.Pow((y - 28) / 53f, 2) < 0.95f);
        // A constellation drawn across it.
        k.Line(30, 50, 40, 62, P.Stardust[2]); k.Line(40, 62, 54, 58, P.Stardust[2]); k.Line(54, 58, 64, 70, P.Stardust[2]);
        CulRing(k, 48, 28, 40, 54, P.Starmetal[3], a => Mathf.Sin(a) >= 0);
        CulBulbs(k, 8, 87, 29, 4);
        CulBanners(k, 0, 88, 6, 24, P.Astral);
        k.Finish();
    }

    private static void PxFthSunTemple(K k)
    {
        k.Ground(2, 2, 93, 40, P.Sand);
        // Steps of sun-stone, a stair up the middle.
        for (int i = 0; i < 5; i++)
        {
            int x0 = 6 + i * 8, x1 = 89 - i * 8, y0 = 4 + i * 11, y1 = 14 + i * 11;
            Block(k, x0, y0, x1, y1, P.Heliolite);
            k.HLine(x0, x1, y1, P.Solar[3]);
        }
        for (int y = 4; y < 60; y += 3) k.HLine(42, 53, y, (y / 3) % 2 == 0 ? P.Solar[2] : P.Solar[1]);
        // The captured spark of a star on top, pulsing.
        float pulse = 7 + Mathf.Sin(k.T * Mathf.PI * 2f) * 1.5f;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f + k.T;
            k.Line(48 + Mathf.Cos(a) * (pulse + 2), 86 + Mathf.Sin(a) * (pulse + 2), 48 + Mathf.Cos(a) * (pulse + 7), 86 + Mathf.Sin(a) * (pulse + 7), P.Solar[2]);
        }
        Glow(k, 48, 86, pulse, P.Solar);
        k.Rect(44, 60, 51, 72, P.Solar[1]); k.VLine(44, 60, 72, P.Solar[3]);
        k.Line(48, 72, 48, 78, P.Solar[3]);
        CulBanners(k, 2, 86, 6, 26, P.Solar);
        k.Finish();
    }

    private static void PxCulCosmicChoir(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        // A platform of the void, crystal pipes rising in a fan behind it.
        for (int i = 0; i < 11; i++)
        {
            int x = 10 + i * 7, h = 40 + (5 - Mathf.Abs(i - 5)) * 18;
            var ramp = i % 2 == 0 ? P.Riftstone : P.Prism;
            k.Rect(x, 24, x + 4, 24 + h, ramp[1]);
            k.VLine(x, 24, 24 + h, ramp[3]);
            k.Ellipse(x + 2, 24 + h, 2.5f, 2.5f, new[] { ramp[1], ramp[2], ramp[3], P.White });
            // The note each pipe is singing, rising.
            int ny = 24 + h + 4 + (k.Frame * 4 + i * 3) % 14;
            if ((i + k.Frame) % 2 == 0) k.Px(x + 2, ny, P.Stardust[3]);
        }
        Block(k, 4, 6, 91, 24, P.Cosmic);
        CulStarfield(k, 6, 8, 89, 22, 18);
        k.HLine(4, 91, 24, P.Stardust[2]);
        k.Door(42, 6, 12, 14, true, true);
        for (int i = 0; i < 3; i++)
        {
            float r = 8 + ((i * 5 + k.Frame * 4) % 15) * 2f;
            CulRing(k, 48, 110, r * 1.4f, r, P.Stardust[2], a => Mathf.Sin(a) > 0.3f);
        }
        CulBanners(k, 0, 88, 6, 26, P.Cosmic);
        k.Finish();
    }

    private static void PxFthCosmic(K k)
    {
        k.Ground(2, 2, 93, 40, P.Dirt);
        Block(k, 10, 6, 85, 60, P.Starmetal);
        for (int x = 14; x < 84; x += 8)
        {
            if (x > 26 && x < 70) continue;
            k.VLine(x, 8, 58, P.Starmetal[0]);
            k.Rect(x + 2, 24, x + 4, 46, (x / 8 + k.Frame) % 3 == 0 ? P.Cosmic[3] : P.Riftstone[2]);
        }
        k.HLine(10, 85, 58, P.Starmetal[4]);
        // Three spires of starmetal, the middle one tallest.
        for (int s = 0; s < 3; s++)
        {
            float cx = s == 0 ? 18 : s == 1 ? 48 : 78;
            int h = s == 1 ? 96 : 62, w = s == 1 ? 12 : 8;
            k.Polygon(new[] { new Vector2(cx - w, 58), new Vector2(cx + w, 58), new Vector2(cx, 58 + h) },
                l => l.x < cx ? (Color32?)P.Starmetal[3] : P.Starmetal[1]);
            k.VLine(Mathf.RoundToInt(cx), 60, 58 + h - 2, P.Starmetal[4]);
            CulStar(k, Mathf.RoundToInt(cx), 60 + h, s, P.Gold[3]);
        }
        // The great round window: a nebula where stars are born.
        k.Ellipse(48, 36, 18, 18, P.Starmetal);
        k.Ellipse(48, 36, 15, 15, new[] { P.Void[1], P.Void[2], P.Cosmic[2], P.Cosmic[3] });
        k.Ellipse(44 + k.Frame % 2, 39, 6, 4, new[] { P.Void[2], P.Void[3], P.Prism[0], P.White });
        CulStarfield(k, 34, 22, 62, 50, 14, (x, y) => (x - 48) * (x - 48) + (y - 36) * (y - 36) < 196);
        k.Door(42, 6, 12, 12, true, true);
        CulBanners(k, 2, 86, 6, 26, P.Cosmic);
        k.Finish();
    }
}
