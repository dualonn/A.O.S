using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Rivers and the sea in pixel art (32 px a tile): bridge and ford pieces for six ages, fishing
/// docks, harbours, and boats from reed rafts to hover-barges (plus the raiders' boats). Boats face
/// right with their waterline on the pivot; the 12 frames are idle, 4 sailing, 4 shooting and 3
/// sinking. Drawn by Tools > Age of Stickman > Setup Rivers, which only redraws these.
/// </summary>
public static partial class PlaceholderArt
{
    // =====================================================================
    // Bridges and fords
    // =====================================================================

    /// <summary>Bridge piece canvas: a tile with an 8 px margin all round (pivot on the tile's corner).</summary>
    private const int RiverPieceMargin = 8, RiverPieceSize = 32 + RiverPieceMargin * 2;

    private static PixelCanvas RiverBridgePiece(int tier, int piece)
    {
        var c = new PixelCanvas(RiverPieceSize, RiverPieceSize);
        int o = RiverPieceMargin;
        bool ew = piece == RiverWorks.PieceWestEnd || piece == RiverWorks.PieceEastWest || piece == RiverWorks.PieceEastEnd || piece == RiverWorks.PieceEastWestSingle;
        bool ns = piece == RiverWorks.PieceSouthEnd || piece == RiverWorks.PieceNorthSouth || piece == RiverWorks.PieceNorthEnd || piece == RiverWorks.PieceNorthSouthSingle;
        bool lowEnd = piece == RiverWorks.PieceWestEnd || piece == RiverWorks.PieceSouthEnd || piece == RiverWorks.PieceEastWestSingle || piece == RiverWorks.PieceNorthSouthSingle;
        bool highEnd = piece == RiverWorks.PieceEastEnd || piece == RiverWorks.PieceNorthEnd || piece == RiverWorks.PieceEastWestSingle || piece == RiverWorks.PieceNorthSouthSingle;
        if (tier == 0) { RiverFord(c, o, ew, ns); return c; }
        // Deck rectangle in tile pixels.
        int x0, x1, y0, y1;
        if (ew) { x0 = 0; x1 = 31; y0 = 5; y1 = 28; }
        else if (ns) { x0 = 5; x1 = 26; y0 = 0; y1 = 31; }
        else { x0 = 3; x1 = 28; y0 = 4; y1 = 28; }
        void Px(int x, int y, Color32 col) => c.Put(x + o, y + o, col);

        Color32[] deck = tier == 1 ? P.Wood : tier == 2 ? P.Stone : tier == 3 ? P.Iron : tier == 4 ? P.Stone : P.Aether;
        // Front face and supports (seen below the deck's south edge).
        if (!ns)
        {
            for (int x = x0; x <= x1; x++)
            {
                Color32 face = tier == 1 ? P.Wood[1] : tier == 2 ? P.Stone[1] : tier == 3 ? P.Iron[1] : tier == 4 ? P.Stone[1] : P.Neon[1];
                Px(x, y0 - 1, face); Px(x, y0 - 2, face);
                if (tier == 5) Px(x, y0 - 2, new Color32(P.Neon[2].r, P.Neon[2].g, P.Neon[2].b, 150));
            }
            // Piles (wood), arches (stone), girder legs (iron), piers (steel); light beams for force bridges.
            for (int x = x0 + 4; x <= x1 - 2; x += 16)
            {
                if (tier == 1) for (int y = -5; y < y0 - 2; y++) { Px(x, y, P.Wood[1]); Px(x + 1, y, P.Wood[2]); }
                else if (tier == 2) for (int y = -5; y < y0 - 2; y++) { Px(x - 1, y, P.Stone[2]); Px(x, y, P.Stone[2]); Px(x + 1, y, P.Stone[1]); }
                else if (tier == 3) for (int y = -5; y < y0 - 2; y++) { Px(x + (y & 1), y, P.Iron[2]); Px(x + 3 - (y & 1), y, P.Iron[1]); }
                else if (tier == 4) for (int y = -6; y < y0 - 2; y++) { Px(x - 1, y, P.Stone[3]); Px(x, y, P.Stone[2]); Px(x + 1, y, P.Stone[2]); Px(x + 2, y, P.Stone[1]); }
                else for (int y = -4; y < y0 - 2; y++) Px(x, y, new Color32(P.Neon[3].r, P.Neon[3].g, P.Neon[3].b, (byte)(90 + y * 10)));
            }
            if (tier == 2)
            {
                // A masonry face with round arches between the piers, the river showing through.
                for (int x = x0; x <= x1; x++)
                {
                    int m = ((x - x0 - 4) % 16 + 16) % 16;
                    int open = m >= 2 && m <= 14 ? Mathf.RoundToInt(Mathf.Sin((m - 1.5f) / 13f * Mathf.PI) * 5f) : -1;
                    for (int y = -5; y < y0 - 2; y++)
                    {
                        if (y < -5 + open) Px(x, y, new Color32(P.Water[0].r, P.Water[0].g, P.Water[0].b, 170));
                        else Px(x, y, y == -5 + open ? P.Stone[3] : (x + y * 3) % 7 == 0 ? P.Stone[1] : P.Stone[2]);
                    }
                }
            }
        }
        // The deck itself.
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Color32 col;
                switch (tier)
                {
                    case 1:   // planks across the way you walk
                        int seam = ew ? x % 4 : y % 4;
                        col = seam == 3 ? P.Wood[1] : ((ew ? x / 4 : y / 4) % 3 == 0 ? P.Wood[3] : P.Wood[2]);
                        if ((x * 7 + y * 5) % 29 == 0) col = P.Wood[1];
                        break;
                    case 2:   // cobbles
                        bool mortar = (y % 4 == 0) || ((x + (y / 4) * 3) % 6 == 0);
                        col = mortar ? P.Stone[1] : ((x * 3 + y * 7) % 11 == 0 ? P.Stone[3] : P.Stone[2]);
                        break;
                    case 3:   // riveted plates
                        bool plateSeam = ew ? x % 8 == 7 : y % 8 == 7;
                        col = plateSeam ? P.Iron[0] : ((x % 8 == 2 && y % 6 == 2) ? P.Iron[3] : P.Iron[2]);
                        break;
                    case 4:   // a road: kerbs, tarmac, a dashed line
                        int across = ew ? y - y0 : x - x0, width = ew ? y1 - y0 : x1 - x0;
                        col = across <= 2 || across >= width - 2 ? P.Stone[3] : P.Coal[2];
                        if (across == width / 2 && (ew ? x % 8 < 4 : y % 8 < 4)) col = P.Gold[2];
                        if (col.Equals(P.Coal[2]) && (x * 5 + y * 3) % 17 == 0) col = P.Coal[3];
                        break;
                    default:  // hard light: a see-through glowing grid
                        bool grid = x % 6 == 0 || y % 6 == 0;
                        var g = grid ? P.Neon[3] : P.Aether[2];
                        col = new Color32(g.r, g.g, g.b, (byte)(grid ? 230 : 140));
                        break;
                }
                Px(x, y, col);
            }
        // Rails along both sides (posts every 8 px), and the ends' abutments.
        Color32 rail = tier == 1 ? P.Linen[1] : tier == 2 ? P.Stone[3] : tier == 3 ? P.Iron[1] : tier == 4 ? P.Steel[2] : P.Neon[3];
        Color32 post = tier == 1 ? P.Wood[1] : tier == 2 ? P.Stone[2] : tier == 3 ? P.Iron[2] : tier == 4 ? P.Steel[1] : P.Neon[2];
        if (ew)
        {
            foreach (int ry in new[] { y0, y1 - 1 })
            {
                for (int x = x0; x <= x1; x++)
                {
                    Px(x, ry + 2, rail);
                    if (tier >= 2) Px(x, ry + 1, tier == 2 ? P.Stone[2] : rail);
                    if (tier == 3 && (x + ry) % 4 == 0) Px(x, ry + 1, P.Iron[2]);   // lattice
                }
                for (int x = x0 + 2; x <= x1; x += 8) { Px(x, ry, post); Px(x, ry + 1, post); Px(x, ry + 2, post); Px(x, ry + 3, post); }
                if (tier == 4 && ry == y1 - 1) for (int x = x0 + 6; x <= x1; x += 16) { for (int y = ry + 2; y < ry + 9; y++) Px(x, y, P.Steel[1]); Px(x + 1, ry + 8, P.Glow[3]); Px(x + 2, ry + 8, P.Glow[2]); }
            }
            if (lowEnd) RiverAbutment(c, o, tier, true, x0, y0, y1);
            if (highEnd) RiverAbutment(c, o, tier, false, x1, y0, y1);
        }
        else if (ns)
        {
            foreach (int rx in new[] { x0, x1 - 2 })
            {
                for (int y = y0; y <= y1; y++)
                {
                    Px(rx + 1, y, rail);
                    if (tier >= 2) Px(rx, y, tier == 2 ? P.Stone[3] : rail);
                    if (tier == 3 && (y + rx) % 4 == 0) Px(rx + 2, y, P.Iron[2]);
                }
                for (int y = y0 + 2; y <= y1; y += 8) { Px(rx, y, post); Px(rx + 1, y, post); Px(rx + 2, y, post); Px(rx + 1, y + 1, post); }
            }
            if (lowEnd) for (int x = x0; x <= x1; x++) { Px(x, 0, tier == 5 ? P.Neon[2] : P.Dirt[2]); Px(x, 1, tier == 5 ? P.Neon[1] : P.Dirt[1]); }
            if (highEnd) for (int x = x0; x <= x1; x++) { Px(x, 31, tier == 5 ? P.Neon[2] : P.Dirt[2]); Px(x, 30, tier == 5 ? P.Neon[1] : P.Dirt[2]); }
            if (lowEnd)
            {
                // The front end shows its supports.
                for (int x = x0; x <= x1; x++) { Px(x, -1, deck[1]); Px(x, -2, deck[0]); }
            }
        }
        else
        {
            for (int x = x0; x <= x1; x++) { Px(x, y0 - 1, deck[1]); Px(x, y0 - 2, deck[0]); }
            foreach (var (px, py) in new[] { (x0, y0), (x1 - 1, y0), (x0, y1 - 2), (x1 - 1, y1 - 2) })
                for (int y = py; y < py + 4; y++) { Px(px, y, post); Px(px + 1, y, post); }
        }
        c.Outline(P.Ink, 0.4f);
        return c;
    }

    private static void RiverAbutment(PixelCanvas c, int o, int tier, bool west, int x, int y0, int y1)
    {
        // Where the bridge meets the bank: a stone or earth footing, a newel post.
        int dir = west ? 1 : -1;
        Color32[] r = tier == 5 ? P.Neon : tier >= 2 ? P.Stone : P.Dirt;
        for (int i = 0; i < 4; i++)
            for (int y = y0 - 3; y <= y1 + 1; y++)
            {
                var col = i == 0 ? r[1] : (y + i) % 5 == 0 ? r[1] : r[2];
                if (tier == 5) col = new Color32(col.r, col.g, col.b, 200);
                c.Put(x + dir * i + o, y + o, col);
            }
        for (int y = y0; y <= y0 + 5; y++) c.Put(x + dir * 1 + o, y + o, r[3]);
        for (int y = y1 - 3; y <= y1 + 2; y++) c.Put(x + dir * 1 + o, y + o, r[3]);
        if (tier == 5) { c.Put(x + dir + o, y0 + 6 + o, P.White); c.Put(x + dir + o, y1 + 3 + o, P.White); }
    }

    private static void RiverFord(PixelCanvas c, int o, bool ew, bool ns)
    {
        // A gravel bar under shallow water, with flat stepping stones across it.
        bool along = !ns;   // pads and east-west pieces lie east-west
        for (int y = 2; y <= 29; y++)
            for (int x = 0; x <= 31; x++)
            {
                int across = along ? y : x;
                if (across < 6 || across > 25) continue;
                if (!ew && !ns && (x < 3 || x > 28)) continue;
                var s = (x * 7 + y * 11) % 13 == 0 ? P.Sand[3] : P.Sand[2];
                c.Put(x + o, y + o, new Color32(s.r, s.g, s.b, 120));
            }
        for (int i = 0; i < 4; i++)
        {
            int a = 3 + i * 8, b = 12 + ((i * 5) % 3) * 3;
            float sx = along ? a : b, sy = along ? b : a;
            c.Ellipse(new Vector2(sx + o + 2.5f, sy + o + 2f), 3.2f, 2.2f, P.Rock);
            c.Put(Mathf.RoundToInt(sx) + o + 1, Mathf.RoundToInt(sy) + o + 3, P.Rock[4]);
            // a ripple round each stone
            for (int k = -4; k <= 4; k += 2) c.Put(Mathf.RoundToInt(sx) + o + 2 + k, Mathf.RoundToInt(sy) + o - 1, new Color32(255, 255, 255, 110));
        }
        c.Outline(P.Ink, 0.25f);
    }

    // =====================================================================
    // Fishing docks and harbours (building looks)
    // =====================================================================

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> RiverBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "RiverFishingDock", (PxRiverFishingDock, 4, 3f) }, { "RiverFishery", (PxRiverFishery, 4, 4f) },
            { "RiverCannery", (PxRiverCannery, 4, 5f) }, { "RiverAquafarm", (PxRiverAquafarm, 4, 5f) },
            { "RiverRaftLanding", (PxRiverRaftLanding, 4, 3f) }, { "RiverGalleyDock", (PxRiverGalleyDock, 4, 3f) },
            { "RiverLongshipYard", (PxRiverLongshipYard, 4, 3f) }, { "RiverShipyard", (PxRiverShipyard, 4, 4f) },
            { "RiverSteamDockyard", (PxRiverSteamDockyard, 4, 5f) }, { "RiverHoverDock", (PxRiverHoverDock, 4, 6f) },
        };

    /// <summary>Draw one river building's looks (like GenerateBuildingLooks, for this file's art only).</summary>
    private static void GenerateRiverLooks(BuildingDefinition def)
    {
        if (!RiverBuildingArt.TryGetValue(def.id, out var art)) return;
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

    /// <summary>A strip of water along the front of the yard (the slip or the landing), rippling.</summary>
    private static void RiverInlet(K k, int x0, int x1, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float edge = Mathf.Min(x - x0, x1 - x, y1 - y);
                if (edge < 0) continue;
                int n = (x * 3 + y * 7 + k.Frame * 5) % 23;
                var col = edge < 1 ? P.Sand[1] : n == 0 ? P.Water[4] : n < 3 ? P.Water[3] : y > (y0 + y1) / 2 ? P.Water[2] : P.Water[1];
                k.Px(x, y, col);
            }
    }

    private static void RiverJetty(K k, int x0, int x1, int y0, int y1, Color32[] ramp = null)
    {
        ramp ??= P.Wood;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, (y - y0) % 3 == 2 ? ramp[1] : (x + y) % 9 == 0 ? ramp[3] : ramp[2]);
        for (int y = y0; y <= y1; y += 5) { k.VLine(x0 - 1, y - 3, y, ramp[1]); k.VLine(x1 + 1, y - 3, y, ramp[1]); }
    }

    private static void RiverNet(K k, int x0, int x1, int y0, int y1)
    {
        k.VLine(x0, y0 - 4, y1 + 3, P.Wood[1]); k.VLine(x1, y0 - 4, y1 + 3, P.Wood[1]);
        int sway = Mathf.RoundToInt(k.Wave() * 1.2f);
        for (int y = y0; y <= y1; y++)
            for (int x = x0 + 1; x < x1; x++)
                if ((x + y) % 3 == 0 || (x - y + 99) % 3 == 0) k.Px(x + (y < (y0 + y1) / 2 ? sway : 0), y, new Color32(P.Linen[1].r, P.Linen[1].g, P.Linen[1].b, 200));
        k.Px(x0 + 3, y0 + 2, P.Red[2]); k.Px(x1 - 3, y0 + 5, P.Red[2]);   // floats
    }

    private static void RiverFish(K k, int x, int y, Color32[] ramp = null)
    {
        ramp ??= P.Steel;
        k.VLine(x, y, y + 3, ramp[2]); k.Px(x, y + 3, ramp[3]); k.Px(x - 1, y, ramp[1]); k.Px(x + 1, y, ramp[1]);
    }

    private static void RiverSmallBoat(K k, int cx, int y, int half, Color32[] hull, bool sail = false)
    {
        int bob = k.Frame % 2;
        for (int x = -half; x <= half; x++)
        {
            int depth = Mathf.Abs(x) > half - 3 ? 1 : 3;
            for (int d = 0; d < depth; d++) k.Px(cx + x, y + bob + d, d == depth - 1 ? hull[3] : hull[1]);
        }
        k.HLine(cx - half + 2, cx + half - 2, y + bob + 3, hull[2]);
        if (sail)
        {
            k.VLine(cx, y + bob + 3, y + bob + 16, P.Wood[1]);
            for (int r = 0; r < 10; r++) k.HLine(cx + 1, cx + 1 + (10 - r) / 2 + 2, y + bob + 6 + r, r % 4 == 0 ? k.Team[2] : P.Linen[2]);
        }
        for (int x = -half - 2; x <= half + 2; x += 3) k.Px(cx + x, y - 1, new Color32(255, 255, 255, 140));
    }

    private static void PxRiverFishingDock(K k)
    {
        k.Ground(2, 12, 93, 60, P.Sand);
        RiverInlet(k, 38, 95, 0, 14);
        RiverJetty(k, 58, 70, 2, 22);
        // A stilt hut of reeds and hides.
        for (int x = 10; x <= 36; x += 8) k.VLine(x, 14, 22, P.Wood[1]);
        k.Wall(8, 22, 38, 38, K.Mat.Wattle);
        k.Door(20, 23, 6, 10);
        k.Roof(5, 41, 38, 14, K.RoofType.Thatch, 10);
        RiverNet(k, 44, 56, 30, 46);
        if (k.Level >= 1) RiverNet(k, 76, 90, 30, 46);
        // A fish rack and a basket of the catch.
        k.HLine(72, 92, 54, P.Wood[2]); k.VLine(72, 40, 54, P.Wood[1]); k.VLine(92, 40, 54, P.Wood[1]);
        for (int x = 75; x <= 90; x += 3) RiverFish(k, x, 48);
        k.Basket(40, 14, P.Steel);
        RiverSmallBoat(k, 82, 4, 9, P.Wood);
        if (k.Level >= 2) { k.Smoke(24, 52, 0.7f, 120); k.Fire(28, 14, 5, 5); }
        k.Finish();
    }

    private static void PxRiverFishery(K k)
    {
        k.Ground(2, 12, 93, 60, P.Dirt);
        RiverInlet(k, 30, 95, 0, 14);
        RiverJetty(k, 50, 62, 2, 24);
        // The smokehouse: timber frame and shingles, smoke from its louvre.
        Hall(k, 6, 44, 22, 20, K.Mat.Plaster, K.RoofType.Shingle, 16, 12, windows: 1);
        k.Chimney(30, 50, 5, 10);
        k.Smoke(32, 62, 1f, 150);
        for (int i = 0; i < 2 + k.Level; i++) k.Barrel(66 + i * 7, 26);
        RiverNet(k, 70, 90, 38, 54);
        k.Crate(8, 12);
        RiverSmallBoat(k, 78, 4, 11, P.Wood, sail: true);
        if (k.Level >= 2) { k.HLine(46, 64, 52, P.Wood[2]); for (int x = 48; x <= 62; x += 3) RiverFish(k, x, 46, P.Gold); }
        k.Finish();
    }

    private static void PxRiverCannery(K k)
    {
        k.Ground(2, 12, 93, 60, P.Stone);
        RiverInlet(k, 26, 95, 0, 14);
        // A stone quay along the water.
        for (int x = 26; x <= 95; x++) { k.Px(x, 15, P.Stone[3]); k.Px(x, 14, P.Stone[1]); }
        // The brick cannery and its tall chimney.
        Hall(k, 4, 50, 20, 24, K.Mat.Stone, K.RoofType.Slate, 12, 14, windows: 3);
        k.Chimney(52, 24, 7, 44, P.Tile);
        k.Smoke(55, 70, 1.2f, 170);
        // A crane over the water swinging a crate.
        k.VLine(70, 16, 56, P.Iron[2]); k.VLine(71, 16, 56, P.Iron[1]);
        k.HLine(58, 90, 56, P.Iron[2]);
        int swing = Mathf.RoundToInt(k.Wave() * 2);
        k.VLine(86 + swing, 40, 55, P.Linen[1]);
        k.Crate(83 + swing, 34);
        for (int i = 0; i < 2 + k.Level; i++) k.Crate(74 + (i % 2) * 8, 18 + (i / 2) * 7);
        // A steam trawler at the quay.
        RiverSmallBoat(k, 46, 3, 14, P.Iron);
        k.Rect(44, 7, 48, 14, P.Red[1]);
        k.Smoke(46, 16, 0.5f, 120);
        k.Finish();
    }

    private static void PxRiverAquafarm(K k)
    {
        k.Ground(2, 12, 93, 60, P.Stone);
        RiverInlet(k, 2, 95, 0, 16);
        // Round fish pens out in the water, glowing faintly.
        for (int i = 0; i < 2 + Mathf.Min(1, k.Level); i++)
        {
            int cx = 18 + i * 30;
            k.Ellipse(cx, 8, 11, 4.5f, P.Steel);
            k.Ellipse(cx, 8, 9, 3.2f, P.Neon);
            int glint = (k.Frame + i) % 4;
            k.Px(cx - 4 + glint * 2, 9, P.White);
        }
        // A white research hall with a glass front and a pipe to the pens.
        k.Wall(10, 20, 70, 44, K.Mat.Plaster);
        for (int x = 14; x <= 66; x += 8) k.Rect(x, 26, x + 5, 38, P.Water[3]);
        k.FlatRoof(8, 72, 44, 6, P.Marble);
        k.HLine(70, 88, 30, P.Steel[2]); k.VLine(88, 14, 30, P.Steel[2]);
        // Feed silos and a dish.
        k.Rect(76, 34, 84, 54, P.Marble[2]); k.Ellipse(80, 54, 4, 2, P.Marble);
        k.Ellipse(24, 52, 6, 3, P.Steel); k.VLine(24, 44, 50, P.Steel[1]);
        int blink = k.Frame % 2;
        k.Px(24, 56, blink == 0 ? P.Neon[3] : P.Neon[1]);
        k.Finish();
    }

    private static void RiverSlip(K k, int x0, int x1, int y0, int y1, Color32[] ramp)
    {
        // A sloping slipway of logs or stone down into the water.
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, (y - y0) % 4 == 3 ? ramp[1] : x == x0 || x == x1 ? ramp[1] : ramp[2]);
    }

    private static void PxRiverRaftLanding(K k)
    {
        k.Ground(2, 16, 93, 90, P.Sand);
        RiverInlet(k, 2, 95, 0, 18);
        RiverSlip(k, 30, 66, 10, 40, P.Wood);
        // A reed raft being lashed on the slip.
        k.Ellipse(48, 30, 15, 4.5f, P.Straw);
        for (int x = 36; x <= 60; x += 4) k.VLine(x, 27, 33, P.Leather[1]);
        // Bundles of reeds drying, a lean-to of hides.
        for (int i = 0; i < 3 + k.Level; i++) k.Ellipse(10 + (i % 3) * 7, 44 + (i / 3) * 6, 3, 6, P.Straw);
        k.Tent(76, 46, 13, 18, P.Leather);
        k.Fire(74, 30, 5, 6);
        k.Banner(66, 54, 22, k.Team);
        // A raft afloat, moored to a post.
        k.VLine(86, 8, 20, P.Wood[1]);
        RiverSmallBoat(k, 16, 4, 10, P.Straw);
        k.Finish();
    }

    private static void PxRiverGalleyDock(K k)
    {
        k.Ground(2, 18, 93, 90, P.Dirt);
        RiverInlet(k, 2, 95, 0, 20);
        // A stone quay and a boathouse with a tiled roof.
        for (int x = 2; x <= 95; x++) { k.Px(x, 21, P.Stone[3]); k.Px(x, 20, P.Stone[2]); k.Px(x, 19, P.Stone[1]); }
        Hall(k, 52, 90, 26, 24, K.Mat.Stone, K.RoofType.Tile, 16, 10, windows: 2, arched: true);
        // A galley on the slip: a long low hull, a painted eye, a bronze ram and a row of oars.
        RiverSlip(k, 6, 48, 12, 30, P.Stone);
        for (int x = 8; x <= 46; x++)
        {
            int top = 34 - (x < 12 ? 12 - x : 0) / 2 + (x > 42 ? (x - 42) : 0);
            for (int y = 26; y <= top; y++) k.Px(x, y, y == top ? P.Wood[3] : y < 28 ? P.Wood[1] : P.Wood[2]);
        }
        k.Rect(46, 26, 50, 27, P.Bronze[2]);
        k.Px(42, 31, P.White); k.Px(43, 31, P.Ink);
        for (int x = 12; x <= 40; x += 4) k.Line(x, 28, x - 3, 22 + (k.Frame % 2), P.Wood[1]);
        k.VLine(26, 34, 54, P.Wood[1]);
        for (int r = 0; r < 12; r++) k.HLine(18, 34, 42 + r, r % 4 < 2 ? k.Team[2] : P.Linen[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Pot(10 + i * 6, 48, P.Tile);
        k.Banner(92, 56, 24, k.Team);
        k.Finish();
    }

    private static void PxRiverLongshipYard(K k)
    {
        k.Ground(2, 18, 93, 90, P.Dirt);
        RiverInlet(k, 2, 95, 0, 18);
        // A long timber hall with crossed gable ends.
        Hall(k, 40, 92, 46, 20, K.Mat.Logs, K.RoofType.Shingle, 18, 14, windows: 1);
        k.Line(38, 84, 34, 90, P.Wood[2], 2); k.Line(94, 84, 98, 90, P.Wood[2], 2);
        // A longship on rollers: clinker planks, a carved dragon prow, shields along the side.
        for (int x = 6; x <= 76; x++)
        {
            float t = (x - 6) / 70f;
            int top = 30 + Mathf.RoundToInt(Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 3f) * 8f);
            int bottom = 20 + Mathf.RoundToInt(Mathf.Abs(t - 0.5f) * 8f);
            for (int y = bottom; y <= top; y++) k.Px(x, y, (y - bottom) % 3 == 2 ? P.Wood[1] : y == top ? P.Wood[3] : P.Wood[2]);
        }
        k.Line(76, 38, 80, 46, P.Wood[2], 2); k.Ellipse(80, 47, 2.5f, 2, P.Wood); k.Px(81, 48, P.Red[2]);   // the dragon head
        k.Line(6, 38, 3, 45, P.Wood[2], 2);
        for (int x = 14; x <= 68; x += 7) { k.Ellipse(x, 30, 2.6f, 2.6f, (x / 7) % 2 == 0 ? k.Team : P.Gold); k.Px(x, 30, P.Steel[2]); }
        for (int x = 10; x <= 70; x += 12) k.Ellipse(x, 17, 2, 2, P.Wood);   // rollers
        k.SpearRack(8, 50, 3 + k.Level, axes: true);
        k.Banner(30, 52, 24, k.Team);
        k.Finish();
    }

    private static void PxRiverShipyard(K k)
    {
        k.Ground(2, 18, 93, 90, P.Dirt);
        RiverInlet(k, 2, 95, 0, 18);
        RiverSlip(k, 8, 80, 12, 24, P.Wood);
        // A galleon's hull in its frame of props, half planked.
        for (int x = 14; x <= 74; x++)
        {
            float t = (x - 14) / 60f;
            int bottom = 22 + Mathf.RoundToInt(Mathf.Abs(t - 0.45f) * 14f);
            int top = 44 + (x < 26 ? (26 - x) / 2 : 0);
            for (int y = bottom; y <= top; y++)
            {
                bool planked = x < 50 + k.Level * 8 || (y - bottom) % 5 == 0;
                if (!planked && (x % 5 != 0)) continue;
                k.Px(x, y, y == top ? P.Gold[2] : (y - bottom) % 5 == 0 ? P.Wood[1] : P.Wood[2]);
            }
            if (x % 6 == 0 && x < 50) k.Px(x, 36, P.Coal[1]);   // gun ports
        }
        for (int x = 18; x <= 70; x += 13) k.Line(x, 14, x - 6, 26, P.Wood[1]);   // props
        // A sheer-legs crane and a timber pile.
        k.Line(84, 16, 78, 76, P.Wood[1], 2); k.Line(92, 16, 80, 76, P.Wood[1], 2);
        int swing = Mathf.RoundToInt(k.Wave() * 1.5f);
        k.VLine(76 + swing, 56, 75, P.Linen[1]); k.Rect(72 + swing, 52, 80 + swing, 55, P.Wood[3]);
        k.LogPile(2, 52, 2 + Mathf.Min(1, k.Level));
        k.Banner(44, 60, 24, k.Team);
        k.Finish();
    }

    private static void PxRiverSteamDockyard(K k)
    {
        k.Ground(2, 18, 93, 90, P.Stone);
        RiverInlet(k, 2, 95, 0, 18);
        // A dry dock with an ironclad's hull on its blocks.
        for (int x = 6; x <= 70; x++) for (int y = 14; y <= 22; y++) k.Px(x, y, y == 22 ? P.Stone[3] : P.Stone[1]);
        for (int x = 10; x <= 66; x++)
        {
            int top = 34 + (x > 56 ? -(x - 56) / 2 : 0);
            for (int y = 22; y <= top; y++) k.Px(x, y, y == top ? P.Iron[3] : (x % 7 == 0 && y % 3 == 0) ? P.Iron[3] : P.Iron[2]);
        }
        k.Rect(30, 34, 44, 40, P.Iron[1]); k.HLine(44, 54, 38, P.Iron[0]);   // casemate and gun
        // An iron gantry over the dock, its hook moving.
        k.VLine(4, 22, 70, P.Iron[1]); k.VLine(74, 22, 70, P.Iron[1]);
        k.HLine(4, 74, 70, P.Iron[2]); k.HLine(4, 74, 69, P.Iron[1]);
        for (int x = 6; x <= 72; x += 6) k.Line(x, 69, x + 3, 66, P.Iron[1]);
        int hook = 20 + (k.Frame * 12) % 40;
        k.VLine(hook, 50, 68, P.Steel[1]); k.Rect(hook - 2, 47, hook + 2, 50, P.Steel[2]);
        // Brick sheds and a chimney.
        Hall(k, 76, 94, 24, 22, K.Mat.Stone, K.RoofType.Slate, 8, 6, windows: 1, door: false);
        k.Chimney(84, 54, 5, 26, P.Tile);
        k.Smoke(86, 82, 1.1f, 170);
        k.Rails(6, 70, 10);
        k.Finish();
    }

    private static void PxRiverHoverDock(K k)
    {
        k.Ground(2, 18, 93, 90, P.Stone);
        RiverInlet(k, 2, 95, 0, 18);
        // A landing pad with glowing rings, a hover-barge resting over it.
        int pulse = k.Frame;
        k.Ellipse(36, 30, 28, 9, P.Steel);
        for (int r = 0; r < 3; r++)
        {
            int ring = 8 + r * 6;
            for (int a = 0; a < 48; a++)
            {
                float ang = a / 48f * Mathf.PI * 2f;
                var col = (a + pulse * 3 + r * 5) % 12 < 3 ? P.Neon[3] : P.Neon[1];
                k.Px(36 + Mathf.RoundToInt(Mathf.Cos(ang) * ring * 1.6f), 30 + Mathf.RoundToInt(Mathf.Sin(ang) * ring * 0.5f), col);
            }
        }
        int hover = 40 + (pulse % 2);
        k.Ellipse(36, hover, 20, 5, P.Chrome);
        k.HLine(18, 54, hover - 2, P.Neon[2]);
        k.Ellipse(40, hover + 5, 6, 3.5f, P.Water);   // canopy
        // A dome hangar and an aerial mast.
        k.Ellipse(78, 40, 16, 22, P.Marble, l => l.y >= 40);
        k.Rect(62, 26, 94, 40, P.Marble[2]);
        k.Rect(72, 26, 84, 38, P.Coal[1]);
        for (int x = 64; x <= 92; x += 4) k.Px(x, 40, P.Neon[2]);
        k.VLine(92, 62, 84, P.Steel[1]);
        k.Px(92, 85, pulse % 2 == 0 ? P.Red[3] : P.Red[1]);
        if (k.Level >= 1) { k.Ellipse(12, 60, 6, 3, P.Steel); k.VLine(12, 52, 58, P.Steel[1]); }
        k.Finish();
    }

    // =====================================================================
    // Boats
    // =====================================================================

    public enum RiverBoatKind { ReedRaft, WarGalley, Longship, Galleon, Ironclad, HoverBarge, RaiderCanoe, RaiderLongboat, RaiderLaunch, RaiderSkiff }

    private struct RiverBoatShape { public int w, h, gy; }

    private static RiverBoatShape RiverShape(RiverBoatKind kind) => kind switch
    {
        RiverBoatKind.ReedRaft => new RiverBoatShape { w = 50, h = 36, gy = 8 },
        RiverBoatKind.WarGalley => new RiverBoatShape { w = 78, h = 56, gy = 9 },
        RiverBoatKind.Longship => new RiverBoatShape { w = 82, h = 58, gy = 9 },
        RiverBoatKind.Galleon => new RiverBoatShape { w = 86, h = 88, gy = 9 },
        RiverBoatKind.Ironclad => new RiverBoatShape { w = 86, h = 60, gy = 8 },
        RiverBoatKind.HoverBarge => new RiverBoatShape { w = 82, h = 56, gy = 8 },
        RiverBoatKind.RaiderCanoe => new RiverBoatShape { w = 48, h = 30, gy = 7 },
        RiverBoatKind.RaiderLongboat => new RiverBoatShape { w = 68, h = 52, gy = 8 },
        RiverBoatKind.RaiderLaunch => new RiverBoatShape { w = 62, h = 48, gy = 8 },
        _ => new RiverBoatShape { w = 58, h = 40, gy = 8 },
    };

    private static Vector2 RiverBoatPivot(RiverBoatKind kind)
    {
        var s = RiverShape(kind);
        return new Vector2((s.w / 2 + 0.5f) / s.w, s.gy / (float)s.h);
    }

    /// <summary>A tiny stick crewman at (x, y) (his feet), in the team's colour; 'arm' raises his right arm (0..2).</summary>
    private static void RiverCrew(PixelCanvas c, int x, int y, Color32[] team, int arm = 0, bool helmet = false)
    {
        c.Put(x, y, P.Ink); c.Put(x, y + 1, team[1]);
        c.Put(x, y + 2, team[2]); c.Put(x, y + 3, team[2]);
        c.Put(x, y + 4, P.Skin[2]); c.Put(x, y + 5, P.Skin[3]);
        if (helmet) { c.Put(x, y + 6, P.Steel[2]); c.Put(x - 1, y + 5, P.Steel[1]); }
        c.Put(x - 1, y + 3, team[1]);
        if (arm == 0) c.Put(x + 1, y + 2, P.Skin[2]);
        else if (arm == 1) { c.Put(x + 1, y + 3, P.Skin[2]); c.Put(x + 2, y + 4, P.Skin[2]); }
        else { c.Put(x + 1, y + 4, P.Skin[2]); c.Put(x + 1, y + 5, P.Skin[2]); c.Put(x + 1, y + 6, P.Skin[3]); }
    }

    /// <summary>A hull from stern to bow along the waterline: 'depth' below it, 'free' above it, the bow rising by 'sheer'.</summary>
    private static void RiverHull(PixelCanvas c, int cx, int gy, int half, int depth, int free, int sheerBow, int sheerStern, Color32[] ramp, Color32 trim)
    {
        for (int x = -half; x <= half; x++)
        {
            float t = x / (float)half;
            int top = gy + free + Mathf.RoundToInt(t > 0 ? Mathf.Pow(t, 3) * sheerBow : Mathf.Pow(-t, 3) * sheerStern);
            int bottom = gy - Mathf.RoundToInt(depth * (1f - Mathf.Pow(Mathf.Abs(t), 2.2f)));
            if (Mathf.Abs(t) > 0.92f) bottom = Mathf.Max(bottom, gy - 1);
            for (int y = bottom; y <= top; y++)
            {
                var col = y == top ? trim : y > top - 2 ? ramp[3] : y < gy ? ramp[1] : ramp[2];
                c.Put(cx + x, y, col);
            }
        }
    }

    /// <summary>The water round the hull: a darker wash over what's under the waterline, foam at the ends, a wake when moving.</summary>
    private static void RiverWaterline(PixelCanvas c, int cx, int gy, int half, bool moving, int frame, bool hover = false)
    {
        var tint = new Color32(P.Water[1].r, P.Water[1].g, P.Water[1].b, 150);
        if (!hover)
            for (int y = 0; y < gy; y++)
                for (int x = 0; x < c.W; x++)
                    if (c.Get(x, y).a > 0) c.Put(x, y, tint);
        var foam = new Color32(255, 255, 255, 170);
        for (int x = -half - 2; x <= half + 3; x++)
            if ((x + frame) % 3 == 0) c.Put(cx + x, gy - 1, foam);
        c.Put(cx + half + 2, gy, foam); c.Put(cx + half + 3, gy - 1, foam);
        if (moving)
        {
            // A V of wake trailing off the stern.
            for (int i = 1; i <= 9; i++)
            {
                int x = cx - half - i * 2 + (frame % 2);
                var w = new Color32(255, 255, 255, (byte)(170 - i * 15));
                c.Put(x, gy - 1 + i / 3, w); c.Put(x, gy - 1 - i / 3, w);
            }
        }
        if (hover)
        {
            // Spray kicked up under a hover hull.
            for (int x = -half + 4; x <= half - 4; x += 3) c.Put(cx + x + (frame % 3), gy + ((x + frame) & 1), new Color32(220, 250, 255, 150));
        }
    }

    private static void RiverSail(PixelCanvas c, int mx, int bottom, int top, int width, Color32[] cloth, Color32[] stripe, float billow)
    {
        for (int y = bottom; y <= top; y++)
        {
            float t = (y - bottom) / (float)Mathf.Max(1, top - bottom);
            int bulge = Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * billow);
            int x0 = mx - width / 2 + bulge, x1 = mx + width / 2 + bulge;
            for (int x = x0; x <= x1; x++)
            {
                bool band = stripe != null && ((x - x0) / 3) % 2 == 0;
                var col = band ? stripe[2] : (x == x1 ? cloth[1] : x == x0 ? cloth[3] : cloth[2]);
                c.Put(x, y, col);
            }
        }
        c.LineRaw(new Vector2(mx - width / 2 - 1, top + 1), new Vector2(mx + width / 2 + 1, top + 1), P.Wood[1]);   // the yard
    }

    /// <summary>Draw one frame of a boat (0 idle, 1-4 sailing, 5-8 shooting, 9-11 sinking).</summary>
    private static PixelCanvas RiverBoatFrame(RiverBoatKind kind, int frame)
    {
        var s = RiverShape(kind);
        bool sinking = frame >= 9;
        int f = sinking ? 0 : frame;
        bool moving = f >= 1 && f <= 4;
        bool shooting = f >= 5 && f <= 8;
        int shot = shooting ? f - 5 : -1;     // 2 = the moment it fires
        int bob = moving ? (f % 2) : 0;
        var c = new PixelCanvas(s.w, s.h);
        int cx = s.w / 2, gy = s.gy + bob;
        bool raider = kind >= RiverBoatKind.RaiderCanoe;
        var team = raider ? P.Red : P.Blue;
        float billow = moving ? 2f + (f % 2) : 1.2f;
        int oar = f % 2;
        switch (kind)
        {
            case RiverBoatKind.ReedRaft:
            {
                int half = 17;
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = gy + 3 + Mathf.RoundToInt(Mathf.Pow(Mathf.Abs(t), 4) * 3);
                    for (int y = gy - 1; y <= top; y++) c.Put(cx + x, y, y == top ? P.Straw[3] : (x + half) % 5 == 0 ? P.Leather[1] : P.Straw[2]);
                }
                // a poler at the stern, a slinger at the bow
                RiverCrew(c, cx - 10, gy + 4, team);
                c.LineRaw(new Vector2(cx - 8, gy + 9), new Vector2(cx - 15 + oar * 2, gy - 3), P.Wood[1]);
                int arm = shot < 0 ? 0 : shot <= 1 ? 2 : 1;
                RiverCrew(c, cx + 6, gy + 4, team, arm);
                if (shot >= 0 && shot <= 1) c.Put(cx + 7, gy + 12, P.Stone[2]);   // the stone whirling
                c.Put(cx - 2, gy + 4, P.Leather[2]); c.Put(cx - 1, gy + 4, P.Leather[2]); c.Put(cx - 2, gy + 5, P.Leather[3]);   // a bundle
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.WarGalley:
            {
                int half = 32;
                RiverHull(c, cx, gy, half, 3, 6, 6, 8, P.Wood, P.Gold[2]);
                c.Rect(cx + half - 1, gy - 1, cx + half + 3, gy + 1, P.Bronze[2]);   // the ram
                c.Put(cx + half - 5, gy + 4, P.White); c.Put(cx + half - 4, gy + 4, P.Ink);   // the painted eye
                // oars
                for (int x = cx - half + 8; x <= cx + half - 10; x += 5)
                    c.LineRaw(new Vector2(x, gy + 3), new Vector2(x - 3 + oar * 3, gy - 2), P.Wood[1]);
                // the mast and a striped square sail
                c.LineRaw(new Vector2(cx - 2, gy + 6), new Vector2(cx - 2, gy + 34), P.Wood[1]);
                RiverSail(c, cx - 1, gy + 14, gy + 32, 20, P.Linen, team, billow);
                // archers along the deck
                for (int i = 0; i < 3; i++) RiverCrew(c, cx - 18 + i * 14, gy + 7, team, shooting && i == 1 ? (shot <= 1 ? 2 : 1) : 0, true);
                if (shot == 2) c.Put(cx + 2, gy + 14, P.Wood[3]);
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.Longship:
            case RiverBoatKind.RaiderLongboat:
            {
                bool big = kind == RiverBoatKind.Longship;
                int half = big ? 34 : 27;
                RiverHull(c, cx, gy, half, 3, 5, big ? 10 : 8, big ? 10 : 8, P.Wood, P.Wood[3]);
                // clinker strakes
                for (int x = -half + 3; x <= half - 3; x++) c.Put(cx + x, gy + 2, P.Wood[1]);
                // dragon prow and curled stern
                c.LineRaw(new Vector2(cx + half - 1, gy + 12), new Vector2(cx + half + 2, gy + 17), P.Wood[2], 2);
                c.Ellipse(new Vector2(cx + half + 3, gy + 18), 2.2f, 1.8f, P.Wood);
                c.Put(cx + half + 4, gy + 19, P.Red[2]);
                c.LineRaw(new Vector2(cx - half + 1, gy + 12), new Vector2(cx - half - 1, gy + 16), P.Wood[2], 2);
                c.Put(cx - half, gy + 17, P.Wood[3]);
                // shields along the side
                for (int x = cx - half + 6; x <= cx + half - 6; x += 6)
                {
                    c.Ellipse(new Vector2(x, gy + 5), 2.4f, 2.4f, ((x / 6) % 2 == 0) ? team : P.Gold);
                    c.Put(x, gy + 5, P.Steel[2]);
                }
                for (int x = cx - half + 8; x <= cx + half - 10; x += 6)
                    c.LineRaw(new Vector2(x, gy + 3), new Vector2(x - 3 + oar * 3, gy - 2), P.Wood[1]);
                c.LineRaw(new Vector2(cx, gy + 6), new Vector2(cx, gy + (big ? 38 : 32)), P.Wood[1]);
                RiverSail(c, cx + 1, gy + 15, gy + (big ? 36 : 30), big ? 24 : 18, P.Linen, raider ? P.Red : team, billow);
                int crewN = big ? 4 : 3;
                for (int i = 0; i < crewN; i++)
                    RiverCrew(c, cx - half + 10 + i * (big ? 14 : 13), gy + 6, team, shooting && i == 2 ? (shot <= 1 ? 2 : 1) : (raider && moving ? oar + 1 : 0), true);
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.Galleon:
            {
                int half = 34;
                RiverHull(c, cx, gy, half, 4, 10, 6, 14, P.Wood, P.Gold[2]);
                // a high stern castle, gun ports, a gilded stripe
                c.Rect(cx - half + 2, gy + 10, cx - half + 14, gy + 18, P.Wood[2]);
                c.Rect(cx - half + 2, gy + 18, cx - half + 14, gy + 19, P.Gold[2]);
                c.Put(cx - half + 6, gy + 14, P.Glow[2]); c.Put(cx - half + 10, gy + 14, P.Glow[2]);
                for (int x = cx - half + 8; x <= cx + half - 6; x++) c.Put(x, gy + 6, P.Gold[1]);
                for (int x = cx - half + 10; x <= cx + half - 8; x += 6) { c.Put(x, gy + 3, P.Coal[0]); c.Put(x + 1, gy + 3, P.Coal[0]); }
                // a bowsprit and three masts of billowing sails
                c.LineRaw(new Vector2(cx + half - 2, gy + 10), new Vector2(cx + half + 6, gy + 16), P.Wood[1]);
                int[] masts = { cx - 16, cx, cx + 16 };
                for (int m = 0; m < 3; m++)
                {
                    int mtop = gy + (m == 1 ? 72 : 60);
                    c.LineRaw(new Vector2(masts[m], gy + 10), new Vector2(masts[m], mtop), P.Wood[1]);
                    RiverSail(c, masts[m] + 1, gy + 16, gy + 34, m == 1 ? 18 : 14, P.Linen, null, billow);
                    RiverSail(c, masts[m] + 1, gy + 38, mtop - 6, m == 1 ? 14 : 10, P.Linen, null, billow * 0.8f);
                    // a pennant in the team's colour
                    for (int i = 0; i < 6; i++) c.Put(masts[m] + 1 + i, mtop - (i + f) % 2, team[2]);
                }
                RiverCrew(c, cx - half + 8, gy + 19, team, 0, true);
                if (shot >= 1 && shot <= 2)
                {
                    int gx = cx + 6;
                    c.Ellipse(new Vector2(gx + 3, gy + 3), shot == 2 ? 3.5f : 2f, shot == 2 ? 2.5f : 1.5f, P.Fire);
                    if (shot == 2) for (int i = 0; i < 6; i++) c.Put(gx + 6 + i, gy + 4 + (i & 1), new Color32(220, 220, 215, 200));
                }
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.Ironclad:
            {
                int half = 36;
                RiverHull(c, cx, gy, half, 4, 5, 3, 2, P.Iron, P.Iron[3]);
                for (int x = cx - half + 4; x <= cx + half - 4; x += 4) c.Put(x, gy + 3, P.Iron[3]);   // rivets
                // the armoured casemate, its gun, a funnel and a mast
                c.Rect(cx - 12, gy + 5, cx + 10, gy + 13, P.Iron[2]);
                c.Rect(cx - 12, gy + 13, cx + 10, gy + 14, P.Iron[3]);
                for (int x = cx - 10; x <= cx + 8; x += 6) c.Put(x, gy + 9, P.Coal[0]);
                int recoil = shot == 2 ? -2 : 0;
                c.Rect(cx + 10 + recoil, gy + 9, cx + 22 + recoil, gy + 10, P.Coal[2]);
                c.Rect(cx - 4, gy + 14, cx + 1, gy + 26, P.Coal[2]);
                c.Rect(cx - 4, gy + 25, cx + 1, gy + 26, P.Red[1]);
                for (int i = 0; i < 3; i++)
                {
                    int sx = cx - 3 - i * 5 - (f % 2) * 2, sy = gy + 30 + i * 4;
                    c.Ellipse(new Vector2(sx, sy), 2.2f + i * 0.8f, 1.8f + i * 0.6f, i == 0 ? P.Wolf : P.Stone);
                }
                c.LineRaw(new Vector2(cx - 20, gy + 5), new Vector2(cx - 20, gy + 30), P.Iron[1]);
                for (int i = 0; i < 5; i++) c.Put(cx - 19 + i, gy + 30 - (i + f) % 2, team[2]);
                RiverCrew(c, cx + 14, gy + 5, team, 0, true);
                if (shot == 2 || shot == 3)
                {
                    c.Ellipse(new Vector2(cx + 25, gy + 10), shot == 2 ? 3.5f : 2.5f, 2.5f, P.Fire);
                    for (int i = 0; i < 5; i++) c.Put(cx + 28 + i, gy + 10 + (i & 1), new Color32(210, 210, 205, 190));
                }
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.HoverBarge:
            {
                int half = 34, lift = 4 + (f % 2);
                int hy = gy + lift;
                // a sleek rounded hull floating over the water on a glowing skirt
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = hy + 8 + Mathf.RoundToInt((t > 0 ? -t * t * 4 : -t * t * 2));
                    int bottom = hy + Mathf.RoundToInt(Mathf.Abs(t) * 3);
                    for (int y = bottom; y <= top; y++) c.Put(cx + x, y, y == top ? P.Chrome[3] : y == bottom ? P.Neon[2] : y < hy + 3 ? P.Chrome[1] : P.Chrome[2]);
                }
                for (int x = -half + 4; x <= half - 4; x++) c.Put(cx + x, hy + 5, P.Neon[3]);
                c.Ellipse(new Vector2(cx + 8, hy + 11), 8, 4, P.Water);   // bubble canopy
                c.Put(cx + 6, hy + 13, P.White);
                // a ray turret
                c.Ellipse(new Vector2(cx - 10, hy + 11), 5, 3, P.Steel);
                c.Rect(cx - 6, hy + 11, cx + (shot == 2 ? 2 : 1), hy + 12, P.Neon[2]);
                if (shot >= 1 && shot <= 2) c.Ellipse(new Vector2(cx + 4, hy + 12), 2.5f, 2.5f, P.Neon);
                // under-glow and spray
                for (int x = -half + 6; x <= half - 6; x += 2) c.Put(cx + x, gy, new Color32(P.Neon[3].r, P.Neon[3].g, P.Neon[3].b, 110));
                RiverWaterline(c, cx, gy, half, moving, f, hover: true);
                break;
            }
            case RiverBoatKind.RaiderCanoe:
            {
                int half = 18;
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = gy + 3 + Mathf.RoundToInt(Mathf.Pow(Mathf.Abs(t), 3) * 2);
                    int bottom = gy - Mathf.RoundToInt(2 * (1 - t * t));
                    for (int y = bottom; y <= top; y++) c.Put(cx + x, y, y == top ? P.Wood[3] : y < gy ? P.Wood[1] : P.Wood[2]);
                }
                for (int i = 0; i < 3; i++)
                {
                    int x = cx - 10 + i * 9;
                    RiverCrew(c, x, gy + 3, team, 0);
                    c.LineRaw(new Vector2(x + 1, gy + 6), new Vector2(x - 2 + oar * 4, gy - 2), P.Wood[1]);   // paddles
                }
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            case RiverBoatKind.RaiderLaunch:
            {
                int half = 24;
                RiverHull(c, cx, gy, half, 3, 5, 4, 2, P.Wood, P.Red[2]);
                c.Rect(cx - 8, gy + 5, cx + 4, gy + 11, P.Wood[3]);
                c.Rect(cx - 8, gy + 11, cx + 4, gy + 12, P.Coal[2]);
                c.Rect(cx - 4, gy + 12, cx - 1, gy + 22, P.Coal[2]);
                for (int i = 0; i < 3; i++) c.Ellipse(new Vector2(cx - 3 - i * 4 - (f % 2) * 2, gy + 25 + i * 4), 2 + i * 0.7f, 1.6f + i * 0.5f, i == 0 ? P.Wolf : P.Stone);
                c.LineRaw(new Vector2(cx + 14, gy + 5), new Vector2(cx + 14, gy + 20), P.Wood[1]);
                for (int i = 0; i < 5; i++) c.Put(cx + 15 + i, gy + 20 - (i + f) % 2, P.Red[2]);
                RiverCrew(c, cx + 8, gy + 5, team, 0);
                RiverCrew(c, cx - 14, gy + 5, team, 0);
                RiverWaterline(c, cx, gy, half, moving, f);
                break;
            }
            default:   // raider hover-skiff
            {
                int half = 24, lift = 3 + (f % 2);
                int hy = gy + lift;
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = hy + 6 - Mathf.RoundToInt(Mathf.Abs(t) * 3);
                    for (int y = hy + Mathf.RoundToInt(Mathf.Abs(t) * 2); y <= top; y++) c.Put(cx + x, y, y == top ? P.Coal[3] : P.Coal[2]);
                }
                for (int x = -half + 3; x <= half - 3; x++) c.Put(cx + x, hy + 3, P.Red[3]);
                RiverCrew(c, cx - 6, hy + 6, team, 0, true);
                RiverCrew(c, cx + 4, hy + 6, team, 0, true);
                for (int x = -half + 4; x <= half - 4; x += 2) c.Put(cx + x, gy, new Color32(255, 120, 100, 110));
                RiverWaterline(c, cx, gy, half, moving, f, hover: true);
                break;
            }
        }
        c.Outline(P.Ink, 0.5f);
        if (sinking) c = RiverSink(c, cx, s.gy, frame - 9);
        return c;
    }

    /// <summary>A sinking frame: the boat heels over and settles, everything under the waterline gone, bubbles round it.</summary>
    private static PixelCanvas RiverSink(PixelCanvas src, int cx, int gy, int step)
    {
        var dst = new PixelCanvas(src.W, src.H);
        float angle = (10f + step * 9f) * Mathf.Deg2Rad;
        int drop = 2 + step * 5;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        for (int y = gy; y < dst.H; y++)
            for (int x = 0; x < dst.W; x++)
            {
                // Inverse map: undo the drop, then the heel (bow up).
                float dx = x + 0.5f - cx, dy = y + 0.5f - gy + drop;
                float sx = dx * cos + dy * sin, sy = -dx * sin + dy * cos;
                var col = src.Get(Mathf.FloorToInt(sx + cx), Mathf.FloorToInt(sy + gy));
                if (col.a > 0) dst.Put(x, y, col);
            }
        var bubble = new Color32(220, 245, 255, 200);
        for (int i = 0; i < 4 + step * 2; i++)
        {
            int bx = cx - 14 + (i * 7) % 28, by = gy + (i * 3 + step) % 5;
            dst.Put(bx, by, bubble);
            if (i % 2 == 0) dst.Put(bx + 1, by + 1, bubble);
        }
        for (int x = cx - 18 - step * 2; x <= cx + 18 + step * 2; x += 2) dst.Put(x, gy - 1, new Color32(255, 255, 255, 150));
        return dst;
    }
}
