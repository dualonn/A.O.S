using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// The sea economy in pixel art (32 px a tile, like PixelRivers): coastal buildings from fish weirs
/// and stilt huts to void moorings and sea domes; fishing boats, merchant ships and pirates' ships in
/// side view (facing right, waterline on the pivot); sea creatures; and the shoal markers. Drawn by
/// Tools > Age of Stickman > Setup Sea, which only redraws these.
/// </summary>
public static partial class PlaceholderArt
{
    // =====================================================================
    // Buildings
    // =====================================================================

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> SeaBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "SeaFishWeir", (PxSeaFishWeir, 4, 3f) }, { "SeaStiltHuts", (PxSeaStiltHuts, 4, 3f) },
            { "SeaSaltPans", (PxSeaSaltPans, 4, 3f) }, { "SeaBeacon", (PxSeaBeacon, 4, 6f) },
            { "SeaPearlDivers", (PxSeaPearlDivers, 4, 3f) }, { "SeaFishPens", (PxSeaFishPens, 4, 3f) },
            { "SeaLighthouse", (PxSeaLighthouse, 4, 5f) }, { "SeaWharf", (PxSeaWharf, 4, 3f) },
            { "SeaCogHarbour", (PxSeaCogHarbour, 4, 3f) }, { "SeaWhalingStation", (PxSeaWhalingStation, 4, 4f) },
            { "SeaPierHouses", (PxSeaPierHouses, 4, 3f) }, { "SeaTradingPort", (PxSeaTradingPort, 4, 3f) },
            { "SeaLampLighthouse", (PxSeaLampLighthouse, 4, 5f) }, { "SeaSteamerDocks", (PxSeaSteamerDocks, 4, 5f) },
            { "SeaHarpoonWharf", (PxSeaHarpoonWharf, 4, 5f) }, { "SeaContainerPort", (PxSeaContainerPort, 4, 4f) },
            { "SeaTrawlerPort", (PxSeaTrawlerPort, 4, 5f) }, { "SeaOilRig", (PxSeaOilRig, 4, 5f) },
            { "SeaFloatingHomes", (PxSeaFloatingHomes, 4, 3f) }, { "SeaTidalGenerator", (PxSeaTidalGenerator, 4, 6f) },
            { "SeaKelpFarm", (PxSeaKelpFarm, 4, 3f) }, { "SeaDeepHarvester", (PxSeaDeepHarvester, 4, 5f) },
            { "SeaHoverQuay", (PxSeaHoverQuay, 4, 6f) }, { "SeaLeviathanRanch", (PxSeaLeviathanRanch, 4, 4f) },
            { "SeaVoidMooring", (PxSeaVoidMooring, 4, 6f) }, { "SeaDome", (PxSeaDome, 4, 4f) },
            { "SeaStarSpire", (PxSeaStarSpire, 4, 6f) }, { "SeaPlanktonLattice", (PxSeaPlanktonLattice, 4, 5f) },
            { "SeaVoidTrawler", (PxSeaVoidTrawler, 4, 6f) },
        };

    /// <summary>Draw one sea building's looks (one per upgrade level, like GenerateRiverLooks).</summary>
    private static void GenerateSeaLooks(BuildingDefinition def)
    {
        if (!SeaBuildingArt.TryGetValue(def.id, out var art)) return;
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

    /// <summary>Rippling water over a strip (what shows under a floating building's deck).</summary>
    private static void SeaWater(K k, int x0, int x1, int y0, int y1, byte alpha = 255)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int n = (x * 3 + y * 7 + k.Frame * 5) % 23;
                var col = n == 0 ? P.Water[4] : n < 3 ? P.Water[3] : (y + x / 9) % 5 == 0 ? P.Water[1] : P.Water[2];
                k.Px(x, y, new Color32(col.r, col.g, col.b, alpha));
            }
    }

    /// <summary>A row of piles standing in the water, from 'y0' up to the deck at 'y1'.</summary>
    private static void SeaPiles(K k, int x0, int x1, int y0, int y1, int step, Color32[] ramp)
    {
        for (int x = x0; x <= x1; x += step)
        {
            k.VLine(x, y0, y1, ramp[1]);
            k.VLine(x + 1, y0, y1, ramp[2]);
            k.Px(x, y0, P.Water[3]); k.Px(x + 2, y0, P.Water[4]);   // a ripple at the foot
        }
    }

    /// <summary>A plank (or steel) deck: front edge darker so it reads as a platform.</summary>
    private static void SeaDeck(K k, int x0, int x1, int y0, int y1, Color32[] ramp, bool planks = true)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var col = y <= y0 + 1 ? ramp[1] : planks && (x - x0) % 6 == 5 ? ramp[1] : (x * 5 + y * 3) % 17 == 0 ? ramp[3] : ramp[2];
                k.Px(x, y, col);
            }
    }

    /// <summary>A round lighthouse tower, wider at the foot, with bands of colour; returns the top.</summary>
    private static int SeaTower(K k, int cx, int y0, int h, int halfBottom, int halfTop, Color32[] ramp, Color32[] band, int bandEvery)
    {
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)Mathf.Max(1, h - 1);
            int half = Mathf.RoundToInt(Mathf.Lerp(halfBottom, halfTop, t));
            bool striped = band != null && bandEvery > 0 && (y / bandEvery) % 2 == 1;
            var r = striped ? band : ramp;
            for (int x = -half; x <= half; x++)
            {
                float u = (x + half) / (float)Mathf.Max(1, half * 2);
                var col = u < 0.25f ? r[3] : u > 0.75f ? r[1] : r[2];
                if (!striped && ramp == P.Stone && (x * 3 + y * 5) % 13 == 0) col = r[1];
                k.Px(cx + x, y0 + y, col);
            }
        }
        return y0 + h;
    }

    /// <summary>The lamp room: a gallery, glass, a cap and a beam that sweeps round (frame by frame).</summary>
    private static void SeaLamp(K k, int cx, int y, int half, Color32 glow, Color32[] cap, bool fire = false)
    {
        k.HLine(cx - half - 2, cx + half + 2, y, P.Iron[1]);
        k.HLine(cx - half - 2, cx + half + 2, y + 1, P.Iron[2]);
        for (int x = cx - half - 2; x <= cx + half + 2; x += 2) k.Px(x, y + 2, P.Iron[1]);
        for (int yy = y + 2; yy <= y + 8; yy++)
            for (int x = cx - half; x <= cx + half; x++)
                k.Px(x, yy, x == cx - half || x == cx + half ? P.Iron[1] : yy > y + 6 ? glow : new Color32(glow.r, glow.g, glow.b, 230));
        if (fire) k.Fire(cx - 2, y + 2, 5, 6);
        for (int i = 0; i <= half + 1; i++) k.HLine(cx - half - 1 + i, cx + half + 1 - i, y + 9 + i, i == 0 ? cap[1] : cap[2]);
        k.Px(cx, y + 11 + half, cap[3]);
        // The beam: sweeps right, then out of sight behind, then left.
        int phase = k.Frame % 4;
        if (phase == 1 || phase == 3)
        {
            int dir = phase == 1 ? 1 : -1;
            for (int i = 2; i < 20; i++)
                for (int j = -i / 5; j <= i / 5; j++)
                    k.Px(cx + dir * (half + i), y + 5 + j, new Color32(glow.r, glow.g, glow.b, (byte)Mathf.Max(30, 170 - i * 7)));
        }
    }

    /// <summary>A little moored boat seen from the side, bobbing.</summary>
    private static void SeaMoored(K k, int cx, int y, int half, Color32[] hull, Color32[] sail = null) =>
        RiverSmallBoat(k, cx, y, half, hull, sail != null);

    private static void SeaRope(K k, int x0, int y0, int x1, int y1) => k.Line(x0, y0, x1, y1, P.Linen[1]);

    // ---------- Fishing and the shore ----------

    private static void PxSeaFishWeir(K k)
    {
        // A fence of stakes and woven wicker out into the water, a trap basket at its point, a drying rack ashore.
        SeaWater(k, 20, 63, 0, 22, 150);
        for (int i = 0; i <= 10; i++)
        {
            int x = 22 + i * 4, y = 2 + i * 2;
            k.VLine(x, y, y + 9, P.Wood[1]); k.VLine(x + 1, y + 1, y + 9, P.Wood[2]);
            if (i < 10) { k.Line(x, y + 4, x + 4, y + 6, P.Straw[1]); k.Line(x, y + 7, x + 4, y + 9, P.Straw[2]); }
        }
        k.Ellipse(24, 6, 6, 4, P.Straw);
        for (int x = 20; x <= 28; x += 2) k.Px(x, 6, P.Straw[1]);
        if (k.Frame % 2 == 0) RiverFish(k, 23, 8);
        k.Ground(2, 26, 34, 60, P.Dirt);
        k.HLine(6, 30, 46, P.Wood[2]); k.VLine(6, 30, 46, P.Wood[1]); k.VLine(30, 30, 46, P.Wood[1]);
        for (int x = 9; x <= 28; x += 4) RiverFish(k, x, 40);
        k.Basket(10, 28, P.Steel);
        if (k.Level >= 1) { k.Ellipse(56, 18, 5, 3.5f, P.Straw); k.Px(56, 18, P.Straw[1]); }
        if (k.Level >= 2) k.Fire(20, 28, 5, 5);
        k.Finish();
    }

    private static void PxSeaStiltHuts(K k)
    {
        // Two reed huts on stilts over the shallows, a walkway between, a dugout tied below.
        SeaWater(k, 0, 63, 0, 14, 120);
        SeaPiles(k, 4, 60, 0, 16, 7, P.Wood);
        SeaDeck(k, 2, 61, 16, 22, P.Wood);
        k.Wall(4, 22, 28, 40, K.Mat.Wattle);
        k.Door(12, 23, 6, 10);
        k.Roof(1, 31, 40, 14, K.RoofType.Thatch, 8);
        k.Wall(36, 22, 60, 38, K.Mat.Wattle);
        k.Door(44, 23, 6, 9);
        k.Roof(33, 63, 38, 13, K.RoofType.Thatch, 8);
        if (k.Level >= 1) { k.Window(52, 29, 4, 4); k.Window(22, 30, 4, 4); }
        if (k.Level >= 2) RiverNet(k, 30, 36, 24, 34);
        SeaMoored(k, 30, 4, 8, P.Wood);
        k.Finish();
    }

    private static void PxSeaSaltPans(K k)
    {
        // Shallow square pans of brine on the shore, white salt raked into heaps, a boiling shed.
        k.Ground(0, 0, 95, 60, P.Sand);
        for (int i = 0; i < 4; i++)
        {
            int x0 = 4 + (i % 2) * 34, y0 = 4 + (i / 2) * 22;
            k.Rect(x0, y0, x0 + 30, y0 + 18, P.Mud[1]);
            for (int y = y0 + 1; y < y0 + 18; y++)
                for (int x = x0 + 1; x < x0 + 30; x++)
                {
                    float dry = (i + k.Level) % 3 == 0 ? 0.8f : 0.3f;
                    bool salt = ((x * 7 + y * 13) % 10) / 10f < dry;
                    k.Px(x, y, salt ? (((x + y + k.Frame) % 11 == 0) ? P.White : P.Pearl[2]) : P.Water[(x + y + k.Frame) % 7 == 0 ? 3 : 2]);
                }
        }
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(76 + (i % 2) * 8, 10 + (i / 2) * 9, 5, 4, P.Pearl);
        Hall(k, 72, 92, 30, 14, K.Mat.Planks, K.RoofType.Shingle, 9, 6, windows: 0);
        k.Smoke(82, 54, 0.7f, 120);
        k.Finish();
    }

    private static void PxSeaPearlDivers(K k)
    {
        // A diving platform out over clear water, a diver on the edge, baskets of oysters, a hut.
        SeaWater(k, 24, 63, 0, 20, 140);
        SeaPiles(k, 30, 60, 0, 20, 8, P.Wood);
        SeaDeck(k, 28, 62, 20, 25, P.Wood);
        k.Ground(0, 22, 30, 60, P.Sand);
        k.Wall(2, 26, 26, 42, K.Mat.Wattle);
        k.Door(10, 27, 6, 9);
        k.Roof(0, 29, 42, 12, K.RoofType.Thatch, 7);
        k.Basket(34, 26, P.Pearl);
        if (k.Level >= 1) k.Basket(42, 26, P.Pearl);
        // The diver: standing, then in the air, then a splash.
        int ph = k.Frame;
        if (ph < 2) { k.VLine(55, 26, 31, P.Skin[2]); k.Px(55, 32, P.Skin[3]); k.Px(55, 29, P.Red[2]); }
        else if (ph == 2) { k.Line(54, 16, 58, 22, P.Skin[2]); k.Px(58, 23, P.Skin[3]); }
        else { k.Px(56, 10, P.White); k.Px(55, 11, P.Water[4]); k.Px(57, 11, P.Water[4]); k.Px(56, 12, P.White); }
        for (int i = 0; i < 3; i++) k.Px(40 + i * 6, 4 + (i + k.Frame) % 3, P.Pearl[3]);   // pearls glinting on the bed
        k.Finish();
    }

    private static void PxSeaFishPens(K k)
    {
        // Stone-walled pens let into the shore (Roman piscinae), fish rippling, a keeper's lodge.
        SeaWater(k, 0, 95, 0, 30, 110);
        for (int i = 0; i < 3; i++)
        {
            int x0 = 2 + i * 31;
            k.Rect(x0, 2, x0 + 28, 3, P.Stone[2]);
            k.Rect(x0, 2, x0 + 1, 28, P.Stone[2]);
            k.Rect(x0 + 27, 2, x0 + 28, 28, P.Stone[1]);
            for (int f = 0; f < 3 + k.Level; f++)
            {
                int fx = x0 + 5 + (f * 7 + k.Frame * 3 + i * 5) % 18, fy = 8 + (f * 5 + i * 3) % 16;
                k.Px(fx, fy, P.Steel[3]); k.Px(fx + 1, fy, P.Steel[2]); k.Px(fx - 1, fy + ((k.Frame + f) % 2), P.Steel[1]);
            }
        }
        k.Rect(0, 29, 95, 31, P.Stone[3]);
        Hall(k, 34, 64, 32, 16, K.Mat.Plaster, K.RoofType.Tile, 10, 8, windows: 2);
        k.Barrel(70, 32); k.Barrel(77, 32);
        k.Finish();
    }

    private static void PxSeaWhalingStation(K k)
    {
        // A slipway with a whaleboat, a try-works boiling blubber, barrels, and great ribs as an arch.
        SeaWater(k, 0, 95, 0, 14, 255);
        RiverSlip(k, 8, 40, 10, 30, P.Wood);
        SeaMoored(k, 24, 14, 12, P.Wood);
        k.Ground(42, 12, 95, 60, P.Dirt);
        // The try-works: a brick furnace with two iron pots.
        k.Rect(48, 16, 72, 26, P.Tile[1]);
        k.Rect(49, 17, 71, 25, P.Tile[2]);
        k.Fire(52, 17, 6, 5); k.Fire(62, 17, 6, 5);
        k.Ellipse(54, 28, 5, 2.5f, P.Iron); k.Ellipse(66, 28, 5, 2.5f, P.Iron);
        k.Smoke(60, 34, 1.1f, 160);
        for (int i = 0; i < 3 + k.Level; i++) k.Barrel(76 + (i % 3) * 6, 16 + (i / 3) * 7);
        // Whale ribs arching over the gate.
        for (int i = 0; i < 2; i++) { int x = 80 + i * 10; k.Line(x, 30, x + (i == 0 ? 4 : -4), 52, P.Bone[2], 2); }
        Hall(k, 44, 74, 34, 14, K.Mat.Planks, K.RoofType.Shingle, 10, 6, windows: 1);
        k.Finish();
    }

    private static void PxSeaHarpoonWharf(K k)
    {
        // An iron wharf, a harpoon gun on a steam catcher, a flensing deck, oil tanks.
        SeaWater(k, 0, 95, 0, 14, 255);
        for (int x = 0; x <= 95; x++) { k.Px(x, 15, P.Iron[3]); k.Px(x, 14, P.Iron[1]); }
        RiverSmallBoat(k, 30, 2, 18, P.Iron);
        k.Rect(26, 6, 34, 13, P.Red[1]);
        k.VLine(46, 6, 12, P.Iron[2]); k.Line(46, 12, 52, 15, P.Iron[2], 2);   // the harpoon gun at the bow
        k.Smoke(29, 15, 0.6f, 120);
        k.Ground(0, 16, 95, 60, P.Stone);
        Hall(k, 4, 40, 18, 18, K.Mat.Stone, K.RoofType.Slate, 10, 10, windows: 2);
        for (int i = 0; i < 2 + Mathf.Min(1, k.Level); i++)
        {
            int cx = 54 + i * 14;
            k.Rect(cx - 5, 18, cx + 5, 38, P.Steel[1]);
            k.Rect(cx - 4, 19, cx + 4, 37, P.Steel[2]);
            k.Ellipse(cx, 38, 5, 2, P.Steel);
            k.HLine(cx - 4, cx + 4, 28, P.Coal[2]);
        }
        k.Chimney(88, 20, 5, 26, P.Tile);
        k.Smoke(90, 48, 1f, 150);
        k.Finish();
    }

    private static void PxSeaTrawlerPort(K k)
    {
        // A concrete quay with an ice plant, a gantry and a big stern trawler alongside.
        SeaWater(k, 0, 95, 0, 22, 255);
        for (int x = 0; x <= 95; x++) { k.Px(x, 23, P.Stone[3]); k.Px(x, 22, P.Stone[1]); }
        RiverSmallBoat(k, 48, 4, 30, P.Steel);
        k.Rect(56, 8, 72, 18, P.Marble[2]); k.Rect(58, 12, 70, 16, P.Water[3]);
        k.VLine(30, 8, 26, P.Red[2]); k.Line(30, 26, 22, 12, P.Linen[1]);
        k.Smoke(64, 20, 0.7f, 130);
        k.Ground(0, 24, 95, 92, P.Stone);
        k.Wall(4, 26, 56, 58, K.Mat.Plaster);
        for (int x = 8; x <= 52; x += 9) k.Window(x, 44, 5, 5);
        k.FlatRoof(2, 58, 58, 5, P.Steel);
        k.Rect(64, 26, 90, 40, P.Steel[1]); k.Rect(65, 27, 89, 39, P.Steel[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Crate(66 + (i % 3) * 8, 42 + (i / 3) * 7);
        k.VLine(92, 26, 70, P.Iron[2]); k.HLine(70, 94, 70, P.Iron[2]);
        k.Finish();
    }

    private static void PxSeaDeepHarvester(K k)
    {
        // A sleek white hub with a launch bay, harvester drones skimming out, sonar dish turning.
        SeaWater(k, 0, 95, 0, 22, 255);
        for (int x = 0; x <= 95; x++) { k.Px(x, 23, P.Marble[3]); k.Px(x, 22, P.Steel[1]); }
        int bob = k.Frame % 2;
        for (int i = 0; i < 2; i++)
        {
            int cx = 20 + i * 40 + k.Frame * 2;
            k.Ellipse(cx, 8 + bob, 9, 3, P.Marble);
            k.HLine(cx - 6, cx + 6, 7 + bob, P.Neon[2]);
            k.Px(cx + 8, 9, P.Neon[3]);
        }
        k.Ground(0, 24, 95, 92, P.Stone);
        k.Wall(8, 26, 72, 52, K.Mat.Plaster);
        for (int x = 12; x <= 68; x += 7) k.Rect(x, 34, x + 4, 46, P.Water[3]);
        k.FlatRoof(6, 74, 52, 6, P.Marble);
        // The sonar dish.
        int tilt = k.Frame % 4;
        k.VLine(84, 26, 60, P.Steel[1]);
        k.Ellipse(84 + (tilt - 1), 64, 7, 3, P.Steel);
        k.Px(84, 66, P.Neon[3]);
        if (k.Level >= 1) { k.Rect(76, 26, 92, 40, P.Marble[2]); k.HLine(76, 92, 34, P.Neon[2]); }
        k.Finish();
    }

    private static void PxSeaKelpFarm(K k)
    {
        // Rows of floats over the water with kelp swaying beneath, a small floating shed.
        SeaWater(k, 0, 95, 0, 95, 200);
        int sway = Mathf.RoundToInt(k.Wave() * 1.5f);
        for (int row = 0; row < 5; row++)
        {
            int y = 8 + row * 16;
            for (int x = 4; x <= 90; x += 6)
            {
                for (int j = 1; j <= 7; j++) k.Px(x + (j > 3 ? sway : 0), y - j, j % 2 == 0 ? P.Leaf[1] : P.Olive[2]);
                k.Ellipse(x, y, 2, 1.4f, row % 2 == 0 ? P.Red : P.Gold);
            }
            k.HLine(2, 92, y, P.Linen[1]);
        }
        SeaDeck(k, 60, 92, 70, 80, P.Steel, false);
        k.Wall(64, 80, 88, 92, K.Mat.Planks);
        k.FlatRoof(62, 90, 92, 3, P.Steel);
        if (k.Level >= 1) k.Window(70, 85, 4, 4);
        k.Finish();
    }

    private static void PxSeaPlanktonLattice(K k)
    {
        // A lattice of glowing tubes on the water, plankton blooming green-gold inside, a dome at one end.
        SeaWater(k, 0, 95, 0, 95, 200);
        for (int i = 0; i < 6; i++)
        {
            int y = 6 + i * 14;
            for (int x = 2; x <= 93; x++)
            {
                int n = (x + i * 7 + k.Frame * 4) % 16;
                k.Px(x, y, n < 2 ? P.Glow[3] : P.Leaf[2]);
                k.Px(x, y + 1, P.Steel[2]);
            }
            for (int x = 6; x <= 90; x += 14) k.VLine(x, y, y + 14 > 92 ? 92 : y + 14, P.Steel[1]);
        }
        k.Ellipse(80, 82, 12, 8, P.Neon);
        k.Ellipse(80, 83, 9, 5, P.Water);
        k.Px(80 + k.Frame - 2, 85, P.White);
        k.Finish();
    }

    private static void PxSeaLeviathanRanch(K k)
    {
        // A great pen of carved stone posts out in the sea, a leviathan's back rising inside, a rune-hall ashore.
        SeaWater(k, 0, 127, 0, 46, 255);
        for (int x = 2; x <= 125; x += 9) { k.VLine(x, 2, 14, P.Moonstone[1]); k.VLine(x + 1, 3, 14, P.Moonstone[2]); k.Px(x, 15, P.Rune[3]); }
        k.HLine(2, 126, 12, P.Rune[2]);
        int rise = k.Frame == 1 || k.Frame == 2 ? 3 : 0;
        k.Ellipse(60, 24 + rise, 28, 6 + rise, P.Whale);
        for (int x = 40; x <= 80; x += 6) k.Px(x, 30 + rise, P.Whale[3]);
        if (k.Frame == 2) for (int i = 0; i < 4; i++) k.Px(52 + i, 36 + i * 2, P.Water[4]);   // a spout
        k.Ground(0, 48, 127, 92, P.Stone);
        Hall(k, 30, 96, 50, 20, K.Mat.Marble, K.RoofType.Slate, 14, 12, windows: 3, arched: true);
        k.Banner(100, 56, 22, P.Arcane);
        k.Finish();
    }

    private static void PxSeaVoidTrawler(K k)
    {
        // A dock of dark star-steel where a void trawler hangs over black water, its nets of light trailing.
        for (int y = 0; y <= 30; y++)
            for (int x = 0; x <= 95; x++)
                k.Px(x, y, (x * 7 + y * 11 + k.Frame * 3) % 29 == 0 ? P.Void[3] : y % 6 == 0 ? P.Void[1] : P.Void[0]);
        int bob = k.Frame % 2;
        k.Ellipse(44, 18 + bob, 26, 5, P.Astral);
        k.HLine(22, 66, 18 + bob, P.Void[3]);
        for (int i = 0; i < 12; i++) k.Px(18 - i, 14 + (i + k.Frame) % 3, P.Void[3]);
        k.Ground(0, 32, 95, 92, P.Coal);
        k.Wall(6, 34, 62, 60, K.Mat.Stone);
        for (int x = 10; x <= 58; x += 8) k.Rect(x, 44, x + 4, 50, P.Void[3]);
        k.FlatRoof(4, 64, 60, 5, P.Astral);
        k.VLine(80, 34, 80, P.Starmetal[1]); k.Ellipse(80, 82, 4, 4, P.Void);
        k.Finish();
    }

    // ---------- Lighthouses ----------

    private static void PxSeaBeacon(K k)
    {
        // A stone cairn tower with a fire basket on top.
        k.Ground(2, 0, 61, 22, P.Stone);
        int top = SeaTower(k, 32, 4, 54, 12, 7, P.Stone, null, 0);
        k.Rect(24, top, 40, top + 2, P.Iron[1]);
        k.Fire(27, top + 2, 10, 12);
        k.Door(29, 5, 6, 9);
        if (k.Level >= 1) k.Torch(16, 10);
        k.Finish();
    }

    private static void PxSeaLighthouse(K k)
    {
        // A tall stone pharos with a keeper's lodge and a great fire behind bronze mirrors.
        k.Ground(2, 0, 61, 24, P.Stone);
        Hall(k, 2, 22, 2, 12, K.Mat.Stone, K.RoofType.Tile, 8, 6, windows: 1);
        int top = SeaTower(k, 38, 4, 72, 13, 8, P.Marble, null, 0);
        for (int y = 20; y < top - 6; y += 14) k.Window(37, y, 3, 4);
        SeaLamp(k, 38, top, 6, P.Fire[3], P.Bronze, fire: true);
        k.Finish();
    }

    private static void PxSeaLampLighthouse(K k)
    {
        // A red-and-white banded tower with an oil lamp and lens.
        k.Ground(2, 0, 61, 24, P.Stone);
        Hall(k, 2, 24, 2, 13, K.Mat.Plaster, K.RoofType.Slate, 8, 6, windows: 1);
        int top = SeaTower(k, 40, 4, 74, 11, 7, P.Plaster, P.Red, 10);
        SeaLamp(k, 40, top, 5, P.Glow[3], P.Iron);
        k.Finish();
    }

    private static void PxSeaStarSpire(K k)
    {
        // A slender crystal spire throwing a star-coloured beam, rings of light round its waist.
        k.Ground(2, 0, 61, 24, P.Coal);
        int top = SeaTower(k, 32, 4, 80, 9, 3, P.Astral, P.Void, 16);
        for (int i = 0; i < 3; i++)
        {
            int y = 24 + i * 20 + (k.Frame % 2);
            k.Ellipse(32, y, 13 - i * 2, 2, P.Neon);
        }
        SeaLamp(k, 32, top, 4, P.Comet[3], P.Astral);
        k.Finish();
    }

    // ---------- Ports ----------

    /// <summary>A quay along the front (water below it), in a material.</summary>
    private static void SeaQuay(K k, int w, int y, Color32[] ramp)
    {
        SeaWater(k, 0, w - 1, 0, y - 2, 255);
        for (int x = 0; x < w; x++) { k.Px(x, y, ramp[3]); k.Px(x, y - 1, ramp[2]); k.Px(x, y - 2, ramp[1]); }
        for (int x = 4; x < w; x += 12) { k.Px(x, y + 1, P.Iron[1]); k.Px(x, y + 2, P.Iron[2]); }   // bollards
    }

    private static void PxSeaWharf(K k)
    {
        // A timber wharf with a warehouse and a round-hulled merchant ship tied up.
        SeaQuay(k, 96, 18, P.Wood);
        RiverSmallBoat(k, 50, 2, 22, P.Wood, sail: true);
        k.Ground(0, 19, 95, 92, P.Dirt);
        Hall(k, 6, 54, 22, 22, K.Mat.Planks, K.RoofType.Tile, 14, 10, windows: 2);
        for (int i = 0; i < 2 + k.Level; i++) k.Pot(62 + (i % 3) * 7, 22 + (i / 3) * 8);   // amphorae
        k.Crate(82, 22);
        k.Finish();
    }

    private static void PxSeaCogHarbour(K k)
    {
        // A stone harbour wall, a crane treadwheel and a cog with its high castle at the quay.
        SeaQuay(k, 96, 18, P.Stone);
        RiverSmallBoat(k, 40, 2, 20, P.Wood, sail: true);
        k.Rect(54, 5, 60, 12, P.Wood[2]);   // the stern castle
        k.Ground(0, 19, 95, 92, P.Stone);
        Hall(k, 4, 46, 22, 24, K.Mat.Plaster, K.RoofType.Shingle, 16, 10, windows: 2);
        // The treadwheel crane.
        k.Ellipse(70, 34, 9, 9, P.Wood);
        k.Ellipse(70, 34, 6, 6, P.Coal);
        k.VLine(70, 20, 58, P.Wood[1]); k.Line(70, 58, 86, 50, P.Wood[2], 2);
        k.VLine(86, 30, 50, P.Linen[1]); k.Crate(83, 24 + Mathf.RoundToInt(k.Wave() * 2));
        for (int i = 0; i < k.Level; i++) k.Barrel(50 + i * 6, 22);
        k.Finish();
    }

    private static void PxSeaTradingPort(K k)
    {
        // A customs house with a clock, warehouses and a galleon's masts at the quay.
        SeaQuay(k, 96, 18, P.Stone);
        RiverSmallBoat(k, 64, 2, 24, P.Wood);
        for (int i = 0; i < 2; i++)
        {
            int mx = 56 + i * 16;
            k.VLine(mx, 6, 44, P.Wood[1]);
            RiverSailK(k, mx, 16, 38, 12);
        }
        k.Ground(0, 19, 95, 92, P.Stone);
        Hall(k, 2, 44, 22, 28, K.Mat.Stone, K.RoofType.Slate, 14, 10, windows: 3, arched: true);
        k.Ellipse(23, 56, 4, 4, P.Marble); k.Px(23, 56, P.Ink); k.Px(23, 58, P.Ink);
        for (int i = 0; i < 2 + k.Level; i++) k.Crate(80 + (i % 2) * 7, 22 + (i / 2) * 7);
        k.Finish();
    }

    /// <summary>A plain square sail on a mast (building art scale).</summary>
    private static void RiverSailK(K k, int mx, int bottom, int top, int width)
    {
        int bil = Mathf.RoundToInt(k.Wave() * 1f);
        for (int y = bottom; y <= top; y++)
        {
            float t = (y - bottom) / (float)Mathf.Max(1, top - bottom);
            int bulge = Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * (1.5f + bil));
            for (int x = mx - width / 2 + bulge; x <= mx + width / 2 + bulge; x++) k.Px(x, y, x == mx + width / 2 + bulge ? P.Linen[1] : P.Linen[2]);
        }
        k.HLine(mx - width / 2 - 1, mx + width / 2 + 1, top + 1, P.Wood[1]);
    }

    private static void PxSeaSteamerDocks(K k)
    {
        // Brick bonded warehouses, a coaling tower and a steamship's funnel at the iron quay.
        SeaQuay(k, 96, 18, P.Iron);
        RiverSmallBoat(k, 54, 2, 26, P.Iron);
        k.Rect(48, 6, 64, 14, P.Marble[2]);
        k.Rect(54, 14, 58, 26, P.Red[1]); k.HLine(54, 58, 24, P.Coal[2]);
        k.Smoke(56, 28, 1f, 150);
        k.Ground(0, 19, 95, 92, P.Stone);
        Hall(k, 2, 40, 22, 26, K.Mat.Stone, K.RoofType.Slate, 12, 10, windows: 3);
        k.Rect(76, 22, 92, 64, P.Iron[1]); k.Rect(78, 24, 90, 62, P.Iron[2]);
        k.HLine(70, 92, 64, P.Iron[1]);
        k.Chimney(44, 22, 6, 40, P.Tile);
        k.Smoke(47, 64, 1.1f, 160);
        k.Finish();
    }

    private static void PxSeaContainerPort(K k)
    {
        // Stacks of coloured containers, a gantry crane over a container ship.
        SeaQuay(k, 96, 18, P.Stone);
        RiverSmallBoat(k, 48, 2, 32, P.Red);
        for (int i = 0; i < 6; i++) k.Rect(26 + i * 8, 6, 32 + i * 8, 11, i % 3 == 0 ? P.Blue[2] : i % 3 == 1 ? P.Gold[2] : P.Green[2]);
        k.Ground(0, 19, 95, 92, P.Stone);
        Color32[][] cols = { P.Red, P.Blue, P.Green, P.Gold, P.Steel };
        for (int i = 0; i < 6 + k.Level * 2; i++)
        {
            int x = 4 + (i % 4) * 12, y = 22 + (i / 4) * 7;
            var c = cols[(i * 3) % cols.Length];
            k.Rect(x, y, x + 10, y + 6, c[2]); k.HLine(x, x + 10, y + 6, c[3]);
            for (int xx = x + 2; xx < x + 10; xx += 2) k.VLine(xx, y + 1, y + 5, c[1]);
        }
        // The gantry: two legs and a boom out over the water, the spreader moving.
        k.VLine(60, 20, 76, P.Red[2]); k.VLine(61, 20, 76, P.Red[1]);
        k.VLine(84, 20, 76, P.Red[2]); k.VLine(85, 20, 76, P.Red[1]);
        k.HLine(30, 94, 76, P.Red[2]); k.HLine(30, 94, 75, P.Red[1]);
        int sx = 36 + k.Frame * 6;
        k.VLine(sx, 60, 75, P.Linen[1]); k.Rect(sx - 4, 56, sx + 4, 60, P.Blue[2]);
        k.Finish();
    }

    private static void PxSeaHoverQuay(K k)
    {
        // Glowing landing rings on the water and a dome terminal, a hover-barge humming over a ring.
        SeaWater(k, 0, 95, 0, 20, 255);
        for (int i = 0; i < 2; i++) { int cx = 24 + i * 44; k.Ellipse(cx, 10, 16, 5, P.Neon); k.Ellipse(cx, 10, 12, 3, P.Water); }
        int lift = k.Frame % 2;
        k.Ellipse(24, 18 + lift, 13, 4, P.Chrome);
        k.HLine(14, 34, 17 + lift, P.Neon[3]);
        k.Ground(0, 21, 95, 92, P.Stone);
        k.Ellipse(48, 30, 34, 16, P.Chrome, p => p.y >= 30);
        k.Rect(14, 22, 82, 30, P.Chrome[2]);
        for (int x = 20; x <= 76; x += 8) k.Rect(x, 24, x + 4, 29, P.Neon[2]);
        k.VLine(88, 22, 70, P.Steel[1]); k.Px(88, 71, k.Frame % 2 == 0 ? P.Neon[3] : P.Neon[1]);
        k.Finish();
    }

    private static void PxSeaVoidMooring(K k)
    {
        // Black stone piers into a sea of starlight, a void barge moored to a pylon of light.
        for (int y = 0; y <= 22; y++)
            for (int x = 0; x <= 95; x++)
                k.Px(x, y, (x * 5 + y * 9 + k.Frame * 7) % 31 == 0 ? P.Comet[3] : y % 5 == 0 ? P.Void[1] : P.Void[0]);
        int bob = k.Frame % 2;
        k.Ellipse(52, 12 + bob, 28, 5, P.Astral);
        for (int x = 30; x <= 74; x += 6) k.Px(x, 12 + bob, P.Void[3]);
        for (int i = 0; i < 3; i++) k.Rect(10 + i * 30, 14, 16 + i * 30, 23, P.Coal[2]);
        k.Ground(0, 23, 95, 92, P.Coal);
        k.Wall(4, 24, 60, 52, K.Mat.Stone);
        for (int x = 8; x <= 56; x += 8) k.Rect(x, 34, x + 4, 44, P.Void[3]);
        k.FlatRoof(2, 62, 52, 6, P.Astral);
        k.VLine(80, 24, 82, P.Starmetal[2]);
        k.Ellipse(80, 84, 5, 5, P.Void);
        for (int i = 0; i < 4; i++) k.Px(80 + (i - 2) * 3, 84 + ((k.Frame + i) % 3) * 3, P.Comet[3]);
        k.Finish();
    }

    // ---------- Industry and power ----------

    private static void PxSeaOilRig(K k)
    {
        // Steel legs in the sea, a deck with a derrick, a crane and a gas flare.
        SeaWater(k, 0, 63, 0, 20, 180);
        for (int i = 0; i < 4; i++) { int x = 6 + i * 16; k.VLine(x, 0, 30, P.Iron[2]); k.VLine(x + 1, 0, 30, P.Iron[1]); }
        for (int i = 0; i < 3; i++) k.Line(6 + i * 16, 4, 22 + i * 16, 24, P.Iron[1]);
        SeaDeck(k, 2, 61, 30, 36, P.Steel, false);
        k.Rect(4, 37, 26, 50, P.Marble[2]); k.Rect(6, 42, 24, 46, P.Water[3]);
        // The derrick.
        for (int y = 37; y <= 90; y++)
        {
            int half = Mathf.RoundToInt(Mathf.Lerp(9, 2, (y - 37) / 53f));
            k.Px(40 - half, y, P.Red[2]); k.Px(40 + half, y, P.Red[1]);
            if ((y - 37) % 7 == 0) k.HLine(40 - half, 40 + half, y, P.Red[2]);
        }
        k.Line(54, 36, 62, 64, P.Gold[2]);
        k.Fire(58, 64, 4, 6 + k.Frame % 3);
        k.Finish();
    }

    private static void PxSeaTidalGenerator(K k)
    {
        // A barrage across the shoreline with turbines churning the tide, cables to a pylon.
        SeaWater(k, 0, 63, 0, 26, 160);
        k.Rect(0, 18, 63, 32, P.Stone[2]);
        k.HLine(0, 63, 32, P.Stone[3]);
        for (int i = 0; i < 3; i++)
        {
            int cx = 10 + i * 20;
            k.Ellipse(cx, 24, 6, 6, P.Coal);
            int a = (k.Frame + i) % 4;
            for (int r = 1; r <= 5; r++)
            {
                int dx = a == 0 ? r : a == 1 ? 0 : a == 2 ? -r : 0, dy = a == 1 ? r : a == 3 ? -r : 0;
                k.Px(cx + dx, 24 + dy, P.Steel[3]); k.Px(cx - dx, 24 - dy, P.Steel[3]);
            }
            for (int j = 0; j < 3; j++) k.Px(cx - 2 + j * 2, 14 - (k.Frame + j) % 2, P.Water[4]);   // churned water
        }
        k.Rect(8, 33, 34, 46, P.Steel[2]); k.Rect(10, 38, 32, 42, P.Neon[2]);
        k.FlatRoof(6, 36, 46, 3, P.Steel);
        k.VLine(52, 33, 80, P.Iron[2]); k.HLine(46, 58, 76, P.Iron[2]); k.HLine(48, 56, 70, P.Iron[2]);
        k.Px(52, 81, k.Frame % 2 == 0 ? P.Glow[3] : P.Glow[1]);
        k.Finish();
    }

    // ---------- Homes on the water ----------

    private static void PxSeaPierHouses(K k)
    {
        // Timber-framed houses on a pier, washing lines, a rowing boat below.
        SeaWater(k, 0, 95, 0, 16, 130);
        SeaPiles(k, 3, 92, 0, 18, 9, P.Wood);
        SeaDeck(k, 0, 95, 18, 24, P.Wood);
        Hall(k, 3, 30, 24, 22, K.Mat.Plaster, K.RoofType.Shingle, 14, 8, windows: 2);
        Hall(k, 36, 62, 24, 26, K.Mat.Plaster, K.RoofType.Tile, 14, 8, windows: 2);
        Hall(k, 68, 93, 24, 20, K.Mat.Planks, K.RoofType.Shingle, 12, 8, windows: 1);
        k.Line(30, 44, 36, 42, P.Linen[1]);
        for (int x = 31; x <= 35; x += 2) k.Px(x, 42, k.Level >= 1 ? P.Red[2] : P.Blue[2]);
        SeaMoored(k, 50, 4, 9, P.Wood);
        k.Finish();
    }

    private static void PxSeaFloatingHomes(K k)
    {
        // Modern houses on pontoons, linked by gangways, a solar panel on each roof.
        SeaWater(k, 0, 63, 0, 63, 170);
        for (int i = 0; i < 2; i++)
        {
            int x0 = 2 + i * 32, y0 = 6 + i * 4;
            k.Rect(x0, y0, x0 + 26, y0 + 4, P.Stone[3]); k.HLine(x0, x0 + 26, y0, P.Stone[1]);
            k.Wall(x0 + 2, y0 + 5, x0 + 24, y0 + 24, K.Mat.Plaster);
            k.Rect(x0 + 5, y0 + 10, x0 + 21, y0 + 19, P.Water[3]);
            k.FlatRoof(x0, x0 + 26, y0 + 24, 4, P.Steel);
            k.Rect(x0 + 6, y0 + 28, x0 + 20, y0 + 30, P.Neon[0]);
        }
        k.HLine(28, 34, 12, P.Wood[2]);
        if (k.Level >= 1) { k.Rect(18, 44, 44, 48, P.Stone[3]); k.Wall(20, 49, 42, 60, K.Mat.Plaster); k.FlatRoof(18, 44, 60, 3, P.Steel); }
        k.Finish();
    }

    private static void PxSeaDome(K k)
    {
        // A glass dome on a ring of floats, gardens and towers inside, light at the top.
        SeaWater(k, 0, 95, 0, 95, 170);
        k.Ellipse(48, 14, 44, 9, P.Steel);
        k.Ellipse(48, 15, 40, 7, P.Stone);
        for (int y = 16; y <= 84; y++)
        {
            float t = (y - 16) / 68f;
            int half = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * 40f);
            for (int x = 48 - half; x <= 48 + half; x++)
            {
                bool rib = (x - 48 + 64) % 12 == 0 || y % 14 == 0;
                var g = rib ? P.Chrome[2] : P.Water[3];
                k.Px(x, y, new Color32(g.r, g.g, g.b, (byte)(rib ? 230 : 90)));
            }
        }
        for (int i = 0; i < 4; i++) { int x = 22 + i * 16, h = 18 + (i % 2) * 14; k.Rect(x, 18, x + 6, 18 + h, P.Marble[2]); k.Px(x + 3, 20 + h, P.Glow[3]); }
        for (int x = 14; x <= 82; x += 5) k.Px(x, 18, P.Leaf[2]);
        k.Px(48, 85, k.Frame % 2 == 0 ? P.Neon[3] : P.Neon[1]);
        k.Finish();
    }

    // =====================================================================
    // Boats and ships (side view, facing right, waterline on the pivot)
    // =====================================================================

    private struct SeaShape { public int w, h, gy; }

    private static SeaShape SeaBoatShape(int kind) => kind switch
    {
        0 => new SeaShape { w = 40, h = 26, gy = 6 },
        1 => new SeaShape { w = 52, h = 50, gy = 7 },
        2 => new SeaShape { w = 58, h = 42, gy = 7 },
        3 => new SeaShape { w = 62, h = 42, gy = 7 },
        4 => new SeaShape { w = 56, h = 34, gy = 7 },
        _ => new SeaShape { w = 56, h = 38, gy = 7 },
    };

    private static Vector2 SeaPivot(SeaShape s) => new Vector2((s.w / 2 + 0.5f) / s.w, s.gy / (float)s.h);

    /// <summary>A fishing boat frame: 0-1 bobbing, 2-3 sailing, 4-5 hauling the nets.</summary>
    private static PixelCanvas SeaFishingBoat(int kind, int frame)
    {
        var s = SeaBoatShape(kind);
        var c = new PixelCanvas(s.w, s.h);
        bool moving = frame == 2 || frame == 3, haul = frame >= 4;
        int bob = frame % 2;
        int cx = s.w / 2, gy = s.gy + (moving ? bob : 0);
        var team = P.Blue;
        int arm = haul ? 1 + bob : 0;
        switch (kind)
        {
            case 0:   // a dugout with a fisherman casting
            {
                int half = 15;
                RiverHull(c, cx, gy, half, 2, 3, 3, 3, P.Wood, P.Wood[3]);
                RiverCrew(c, cx - 7, gy + 3, team);
                RiverCrew(c, cx + 4, gy + 3, team, arm);
                if (moving) c.LineRaw(new Vector2(cx - 5, gy + 7), new Vector2(cx - 12 + bob * 3, gy - 2), P.Wood[1]);
                if (haul) { c.LineRaw(new Vector2(cx + 6, gy + 7), new Vector2(cx + 14, gy - 1), P.Linen[1]); if (bob == 1) c.Put(cx + 13, gy, P.Steel[3]); }
                RiverWaterline(c, cx, gy, half, moving, frame);
                break;
            }
            case 1:   // a sailing smack with a brown sail
            {
                int half = 20;
                RiverHull(c, cx, gy, half, 3, 5, 5, 3, P.Wood, P.Wood[3]);
                c.LineRaw(new Vector2(cx - 2, gy + 5), new Vector2(cx - 2, gy + 36), P.Wood[1]);
                RiverSail(c, cx - 1, gy + 12, gy + 34, 16, P.Leather, null, moving ? 2f + bob : 1f);
                RiverCrew(c, cx - 12, gy + 5, team);
                RiverCrew(c, cx + 10, gy + 5, team, arm);
                if (haul) for (int i = 0; i < 6; i++) c.Put(cx + 14 + i, gy + 4 - i, P.Linen[1]);
                RiverWaterline(c, cx, gy, half, moving, frame);
                break;
            }
            case 2:   // a steam trawler with a tall funnel
            {
                int half = 23;
                RiverHull(c, cx, gy, half, 3, 6, 6, 2, P.Iron, P.Red[2]);
                c.Rect(cx - 4, gy + 6, cx + 8, gy + 13, P.Marble[2]);
                c.Rect(cx - 2, gy + 9, cx + 6, gy + 11, P.Water[3]);
                c.Rect(cx - 10, gy + 6, cx - 7, gy + 22, P.Coal[2]); c.Rect(cx - 10, gy + 19, cx - 7, gy + 20, P.Red[2]);
                c.LineRaw(new Vector2(cx - 18, gy + 6), new Vector2(cx - 18, gy + 26), P.Wood[1]);
                c.LineRaw(new Vector2(cx - 18, gy + 26), new Vector2(cx - 24 + (haul ? 0 : 3), gy + (haul ? 0 : 12)), P.Linen[1]);
                RiverCrew(c, cx + 14, gy + 6, team, arm);
                RiverWaterline(c, cx, gy, half, moving, frame);
                break;
            }
            case 3:   // a diesel stern trawler with a gantry
            {
                int half = 25;
                RiverHull(c, cx, gy, half, 3, 7, 8, 1, P.Steel, P.Blue[2]);
                c.Rect(cx + 2, gy + 7, cx + 16, gy + 17, P.Marble[2]);
                c.Rect(cx + 4, gy + 13, cx + 14, gy + 15, P.Water[3]);
                c.LineRaw(new Vector2(cx + 10, gy + 17), new Vector2(cx + 10, gy + 26), P.Steel[2]);
                c.Rect(cx - 22, gy + 7, cx - 20, gy + 20, P.Gold[2]); c.Rect(cx - 12, gy + 7, cx - 10, gy + 20, P.Gold[2]);
                c.Rect(cx - 22, gy + 19, cx - 10, gy + 21, P.Gold[2]);
                if (haul) for (int i = 0; i < 8; i++) c.Put(cx - 22 - i / 2, gy + 18 - i * 2, P.Linen[1]);
                RiverCrew(c, cx - 4, gy + 7, team, arm, true);
                RiverWaterline(c, cx, gy, half, moving, frame);
                break;
            }
            case 4:   // a white harvester drone skimming the water
            {
                int half = 22, lift = 2 + bob;
                int hy = gy + lift;
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = hy + 7 - Mathf.RoundToInt(t * t * 4);
                    for (int y = hy + Mathf.RoundToInt(Mathf.Abs(t) * 2); y <= top; y++) c.Put(cx + x, y, y == top ? P.Marble[3] : y < hy + 3 ? P.Steel[1] : P.Marble[2]);
                }
                for (int x = -half + 4; x <= half - 6; x += 3) c.Put(cx + x, hy + 4, P.Neon[2]);
                c.Rect(cx + 6, hy + 7, cx + 12, hy + 10, P.Water[3]);
                if (haul) for (int i = 0; i < 5; i++) c.Put(cx - half + 2 + i * 2, gy - 1 + (i + frame) % 2, P.Neon[3]);
                RiverWaterline(c, cx, gy, half, moving, frame, hover: true);
                break;
            }
            default:  // a void skiff of dark star-steel trailing a net of light
            {
                int half = 22;
                RiverHull(c, cx, gy, half, 2, 5, 7, 4, P.Astral, P.Void[3]);
                c.LineRaw(new Vector2(cx, gy + 5), new Vector2(cx, gy + 22), P.Starmetal[2]);
                for (int y = gy + 8; y <= gy + 20; y++) c.Put(cx + 1 + (y - gy - 8) / 3, y, P.Void[3]);
                RiverCrew(c, cx - 10, gy + 5, P.Purple, arm, true);
                if (haul) for (int i = 0; i < 9; i++) c.Put(cx - half - 1 - i, gy + (i + frame) % 3 - 1, P.Comet[3]);
                RiverWaterline(c, cx, gy, half, moving, frame);
                break;
            }
        }
        c.Outline(P.Ink, 0.5f);
        return c;
    }

    private static SeaShape SeaShipShape(int tier) => tier switch
    {
        0 => new SeaShape { w = 70, h = 62, gy = 8 },
        1 => new SeaShape { w = 76, h = 72, gy = 8 },
        2 => new SeaShape { w = 92, h = 92, gy = 9 },
        3 => new SeaShape { w = 96, h = 62, gy = 8 },
        4 => new SeaShape { w = 112, h = 56, gy = 8 },
        5 => new SeaShape { w = 92, h = 50, gy = 8 },
        _ => new SeaShape { w = 100, h = 66, gy = 8 },
    };

    /// <summary>A merchant (or pirate) ship frame: 'moving' with a wake, 'f' the animation step.</summary>
    private static PixelCanvas SeaShipFrame(int tier, int f, bool moving, bool pirate, int shot = -1)
    {
        var s = SeaShipShape(tier);
        var c = new PixelCanvas(s.w, s.h);
        int bob = moving ? f % 2 : 0;
        int cx = s.w / 2, gy = s.gy + bob;
        var team = pirate ? P.Red : P.Blue;
        var cloth = pirate ? P.Coal : P.Linen;
        float billow = moving ? 2f + (f % 2) : 1.2f;
        switch (tier)
        {
            case 0:   // a round-bellied merchant ship with one square sail and a steering oar
            {
                int half = 28;
                RiverHull(c, cx, gy, half, 4, 7, 7, 9, P.Wood, P.Wood[3]);
                c.LineRaw(new Vector2(cx - half + 2, gy + 9), new Vector2(cx - half - 2, gy - 3), P.Wood[1], 2);
                c.LineRaw(new Vector2(cx, gy + 7), new Vector2(cx, gy + 46), P.Wood[1]);
                RiverSail(c, cx + 1, gy + 18, gy + 44, 28, cloth, pirate ? null : P.Red, billow);
                for (int i = 0; i < 3; i++) c.Ellipse(new Vector2(cx - 14 + i * 7, gy + 9), 2.5f, 2f, P.Tile);   // amphorae
                RiverCrew(c, cx + 16, gy + 7, team);
                break;
            }
            case 1:   // a cog: clinker hull, castles fore and aft, one big sail
            {
                int half = 30;
                RiverHull(c, cx, gy, half, 4, 8, 6, 6, P.Wood, P.Wood[3]);
                for (int x = -half + 3; x <= half - 3; x++) c.Put(cx + x, gy + 3, P.Wood[1]);
                c.Rect(cx - half + 2, gy + 9, cx - half + 14, gy + 15, P.Wood[2]); c.Rect(cx - half + 2, gy + 15, cx - half + 14, gy + 16, P.Wood[3]);
                c.Rect(cx + half - 12, gy + 9, cx + half - 2, gy + 14, P.Wood[2]);
                c.LineRaw(new Vector2(cx, gy + 8), new Vector2(cx, gy + 58), P.Wood[1], 2);
                RiverSail(c, cx + 1, gy + 22, gy + 54, 32, cloth, pirate ? null : team, billow);
                c.Put(cx, gy + 59, team[2]); c.Put(cx + 1, gy + 59, team[2]); c.Put(cx + 2, gy + 58, team[2]);
                RiverCrew(c, cx - half + 8, gy + 16, team);
                break;
            }
            case 2:   // a galleon: three masts, gunports, a high stern
            {
                int half = 38;
                RiverHull(c, cx, gy, half, 5, 11, 8, 16, P.Wood, P.Gold[2]);
                for (int x = cx - half + 10; x <= cx + half - 10; x += 7) c.Rect(x, gy + 5, x + 2, gy + 7, P.Coal[1]);
                for (int i = 0; i < 3; i++)
                {
                    int mx = cx - 20 + i * 20, top = i == 1 ? 80 : 68;
                    c.LineRaw(new Vector2(mx, gy + 10), new Vector2(mx, gy + top), P.Wood[1], 2);
                    RiverSail(c, mx + 1, gy + 20, gy + 40, 18, cloth, null, billow);
                    RiverSail(c, mx + 1, gy + 44, gy + top - 6, 14, cloth, null, billow * 0.8f);
                }
                if (pirate) { for (int x = 0; x < 6; x++) for (int y = 0; y < 4; y++) c.Put(cx + 1 + x, gy + 76 + y, P.Coal[1]); c.Put(cx + 3, gy + 78, P.White); }
                else { c.Put(cx + 1, gy + 81, team[2]); c.Put(cx + 2, gy + 81, team[2]); c.Put(cx + 3, gy + 80, team[2]); }
                if (shot >= 2) for (int i = 0; i < 4; i++) c.Put(cx + half + 1 + i, gy + 6 + (i % 2), P.Fire[3]);
                break;
            }
            case 3:   // a steamship: black hull, white superstructure, a funnel
            {
                int half = 42;
                RiverHull(c, cx, gy, half, 4, 9, 6, 3, P.Coal, P.Red[2]);
                c.Rect(cx - 16, gy + 9, cx + 14, gy + 19, P.Marble[2]);
                for (int x = cx - 13; x <= cx + 11; x += 5) c.Rect(x, gy + 13, x + 2, gy + 15, P.Water[3]);
                c.Rect(cx - 4, gy + 19, cx + 4, gy + 36, pirate ? P.Coal[2] : P.Red[2]); c.Rect(cx - 4, gy + 33, cx + 4, gy + 36, P.Coal[2]);
                c.LineRaw(new Vector2(cx - 30, gy + 9), new Vector2(cx - 30, gy + 44), P.Wood[1]);
                c.LineRaw(new Vector2(cx + 28, gy + 9), new Vector2(cx + 28, gy + 40), P.Wood[1]);
                if (shot >= 2) for (int i = 0; i < 4; i++) c.Put(cx + half + 1 + i, gy + 8, P.Fire[3]);
                break;
            }
            case 4:   // a container ship stacked with boxes, bridge aft
            {
                int half = 50;
                RiverHull(c, cx, gy, half, 4, 8, 7, 2, pirate ? P.Coal : P.Red, P.White);
                Color32[][] cols = { P.Blue, P.Gold, P.Green, P.Steel, P.Red };
                for (int i = 0; i < 9; i++)
                    for (int row = 0; row < 2 + (i % 3 == 0 ? 1 : 0); row++)
                    {
                        int x = cx - 30 + i * 8, y = gy + 8 + row * 5;
                        var col = cols[(i * 2 + row) % cols.Length];
                        c.Rect(x, y, x + 6, y + 4, col[2]); c.Put(x + 3, y + 2, col[1]);
                    }
                c.Rect(cx - 46, gy + 8, cx - 36, gy + 26, P.Marble[2]);
                c.Rect(cx - 45, gy + 22, cx - 37, gy + 24, P.Water[3]);
                c.Rect(cx - 42, gy + 26, cx - 40, gy + 32, P.Steel[2]);
                break;
            }
            case 5:   // a hover-barge
            {
                int half = 40, lift = 3 + f % 2;
                int hy = gy + lift;
                for (int x = -half; x <= half; x++)
                {
                    float t = x / (float)half;
                    int top = hy + 10 - Mathf.RoundToInt(t * t * 4);
                    for (int y = hy + Mathf.RoundToInt(Mathf.Abs(t) * 3); y <= top; y++) c.Put(cx + x, y, y == top ? P.Chrome[3] : y < hy + 3 ? P.Steel[1] : P.Chrome[2]);
                }
                for (int x = -half + 6; x <= half - 6; x += 4) c.Put(cx + x, hy + 5, pirate ? P.Red[3] : P.Neon[2]);
                c.Rect(cx + 14, hy + 10, cx + 28, hy + 18, P.Chrome[2]); c.Rect(cx + 16, hy + 13, cx + 26, hy + 16, P.Water[3]);
                for (int i = 0; i < 4; i++) c.Rect(cx - 30 + i * 10, hy + 10, cx - 23 + i * 10, hy + 15, i % 2 == 0 ? P.Gold[2] : P.Steel[2]);
                if (shot >= 2) for (int i = 0; i < 4; i++) c.Put(cx + half + 1 + i, hy + 6, P.Neon[3]);
                RiverWaterline(c, cx, gy, half, moving, f, hover: true);
                c.Outline(P.Ink, 0.5f);
                return c;
            }
            default:  // a void barge: a dark hull with starlight sails
            {
                int half = 42;
                RiverHull(c, cx, gy, half, 3, 8, 12, 8, P.Astral, P.Starmetal[3]);
                for (int x = cx - half + 6; x <= cx + half - 6; x += 5) c.Put(x, gy + 4, pirate ? P.Red[3] : P.Void[3]);
                for (int i = 0; i < 2; i++)
                {
                    int mx = cx - 12 + i * 24;
                    c.LineRaw(new Vector2(mx, gy + 8), new Vector2(mx, gy + 54), P.Starmetal[2]);
                    for (int y = gy + 16; y <= gy + 50; y++)
                    {
                        int w = Mathf.RoundToInt(Mathf.Sin((y - gy - 16) / 34f * Mathf.PI) * 8 + 2);
                        for (int x = mx + 1; x <= mx + w; x++)
                        {
                            var g = (x + y + f) % 5 == 0 ? P.Comet[3] : pirate ? P.Red[1] : P.Void[2];
                            c.Put(x, y, new Color32(g.r, g.g, g.b, 200));
                        }
                    }
                }
                if (shot >= 2) for (int i = 0; i < 4; i++) c.Put(cx + half + 1 + i, gy + 8, P.Void[3]);
                break;
            }
        }
        RiverWaterline(c, cx, gy, s.w / 2 - 6, moving, f);
        c.Outline(P.Ink, 0.5f);
        return c;
    }

    // =====================================================================
    // Sea creatures (side view, facing right, waterline on the pivot)
    // =====================================================================

    private static SeaShape SeaCreatureShape(string id) => id switch
    {
        "SeaWhale" => new SeaShape { w = 76, h = 34, gy = 10 },
        "SeaSerpent" => new SeaShape { w = 68, h = 44, gy = 9 },
        "SeaKraken" => new SeaShape { w = 76, h = 64, gy = 12 },
        "SeaGlassRay" => new SeaShape { w = 52, h = 26, gy = 9 },
        "SeaVoidEel" => new SeaShape { w = 60, h = 36, gy = 9 },
        _ => new SeaShape { w = 120, h = 64, gy = 14 },
    };

    /// <summary>A creature frame: 0 idle, 1-4 swimming, 5-8 attacking, 9-11 sinking away.</summary>
    private static PixelCanvas SeaCreatureFrame(string id, int frame)
    {
        var s = SeaCreatureShape(id);
        var c = new PixelCanvas(s.w, s.h);
        bool dying = frame >= 9;
        int f = dying ? 0 : frame;
        bool attack = f >= 5 && f <= 8;
        int a = attack ? f - 5 : 0;          // 2 = the strike
        float wave = Mathf.Sin(f * Mathf.PI / 2f);
        int cx = s.w / 2, gy = s.gy;
        switch (id)
        {
            case "SeaWhale":
            {
                // A long grey-blue back breaking the surface, a fluke, a spout when it breathes.
                for (int x = -30; x <= 30; x++)
                {
                    float t = (x + 30) / 60f;
                    int top = gy + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 9) + (x > 18 ? 1 : 0);
                    int bottom = gy - Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 6);
                    for (int y = bottom; y <= top; y++) c.Put(cx + x, y, y == top ? P.Whale[3] : y > top - 3 ? P.Whale[2] : y < gy ? P.Whale[0] : P.Whale[1]);
                }
                int fl = Mathf.RoundToInt(wave * 3);
                c.Polygon(new[] { new Vector2(cx - 30, gy + 2), new Vector2(cx - 38, gy + 8 + fl), new Vector2(cx - 36, gy + 1 + fl), new Vector2(cx - 38, gy - 4 + fl) }, P.Whale[1]);
                c.Put(cx + 22, gy + 4, P.Ink);
                for (int x = cx + 4; x <= cx + 26; x += 3) c.Put(x, gy + 1, P.Whale[3]);
                if (f == 2 || f == 3) for (int i = 0; i < 6; i++) { c.Put(cx + 16 - i / 2, gy + 10 + i * 2, P.Water[4]); c.Put(cx + 18 + i / 2, gy + 10 + i * 2, P.Water[3]); }
                break;
            }
            case "SeaSerpent":
            case "SeaVoidEel":
            {
                bool eel = id == "SeaVoidEel";
                var ramp = eel ? P.Void : P.Reefskin;
                // Coils looping out of the water, head reared up (lunging on the strike).
                for (int i = 0; i < 3; i++)
                {
                    int lx = cx - 22 + i * 13 + Mathf.RoundToInt(wave * 2);
                    c.Ellipse(new Vector2(lx, gy + 3), 5, 6 + (i == 1 ? 1 : 0), ramp, p => p.y >= gy);
                    c.Ellipse(new Vector2(lx, gy + 3), 2.5f, 3.5f, P.Water, p => p.y >= gy);
                }
                int lunge = attack ? (a == 2 ? 8 : a == 1 || a == 3 ? 4 : 0) : 0;
                int hx = cx + 18 + lunge, hy = gy + 16 + (attack ? 4 : Mathf.RoundToInt(wave * 1.5f));
                c.Capsule(new Vector2(cx + 12, gy + 2), new Vector2(hx - 2, hy - 2), 3f, ramp);
                c.Ellipse(new Vector2(hx, hy), 6, 4, ramp);
                c.Put(hx + 2, hy + 2, eel ? P.Comet[3] : P.Gold[3]);
                if (attack && a >= 1) { for (int x = hx + 3; x <= hx + 6; x++) c.Put(x, hy - 1, P.White); }
                // Fins down the neck.
                for (int i = 0; i < 3; i++) c.Put(cx + 13 + i * 2, gy + 6 + i * 3 + 2, eel ? P.Void[3] : P.Gold[2]);
                if (eel) for (int i = 0; i < 6; i++) c.Put(cx - 26 + i * 9, gy + 9, new Color32(P.Void[3].r, P.Void[3].g, P.Void[3].b, 160));
                break;
            }
            case "SeaKraken":
            {
                // A great mantle half out of the sea, eyes, and tentacles curling up (slamming down on the strike).
                c.Ellipse(new Vector2(cx, gy + 10), 16, 13, P.Wyvern, p => p.y >= gy - 2);
                c.Ellipse(new Vector2(cx + 6, gy + 12), 3, 2.5f, P.Gold); c.Put(cx + 7, gy + 12, P.Ink);
                c.Ellipse(new Vector2(cx - 5, gy + 12), 3, 2.5f, P.Gold); c.Put(cx - 4, gy + 12, P.Ink);
                for (int t = 0; t < 4; t++)
                {
                    int side = t < 2 ? -1 : 1;
                    int bx = cx + side * (14 + (t % 2) * 10);
                    float lift = attack ? (a == 2 ? 0.2f : 1.3f) : 1f + 0.2f * Mathf.Sin(f + t);
                    var prev = new Vector2(bx, gy);
                    for (int seg = 1; seg <= 8; seg++)
                    {
                        float u = seg / 8f;
                        var p = new Vector2(bx + side * Mathf.Sin(u * 3f + t) * 6f, gy + u * 34f * lift);
                        c.LineRaw(prev, p, seg < 4 ? P.Wyvern[1] : P.Wyvern[2], seg < 3 ? 3 : seg < 6 ? 2 : 1);
                        prev = p;
                    }
                    c.Put(prev.x, prev.y, P.Wyvern[3]);
                }
                if (attack && a == 2) for (int i = 0; i < 8; i++) c.Put(cx - 30 + i * 8, gy + 1, P.Water[4]);
                break;
            }
            case "SeaGlassRay":
            {
                // A flat, see-through ray gliding just under the surface, wings rippling.
                for (int x = -20; x <= 20; x++)
                {
                    int span = Mathf.RoundToInt((1f - Mathf.Abs(x) / 21f) * 8f + Mathf.Sin(x * 0.3f + f) * 1.5f);
                    for (int y = gy - span / 2; y <= gy + span / 2; y++)
                    {
                        var g = (x + y) % 6 == 0 ? P.Prism[3] : P.Prism[2];
                        c.Put(cx + x, y, new Color32(g.r, g.g, g.b, 150));
                    }
                }
                c.LineRaw(new Vector2(cx - 20, gy), new Vector2(cx - 30, gy + Mathf.RoundToInt(wave * 2)), P.Prism[1]);
                c.Put(cx + 14, gy + 1, P.Ink);
                break;
            }
            default:  // the leviathan: an island-sized back with ridges, a vast eye and jaw
            {
                for (int x = -52; x <= 50; x++)
                {
                    float t = (x + 52) / 102f;
                    int top = gy + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 18);
                    int bottom = gy - Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 10);
                    for (int y = bottom; y <= top; y++) c.Put(cx + x, y, y == top ? P.Astral[3] : y > top - 4 ? P.Astral[2] : y < gy ? P.Void[1] : P.Astral[1]);
                    if (x % 9 == 0 && x > -44 && x < 40) for (int r = 1; r <= 4; r++) c.Put(cx + x, top + r, P.Starmetal[2]);
                }
                int jaw = attack ? (a == 2 ? 7 : a == 1 || a == 3 ? 3 : 0) : 0;
                c.Ellipse(new Vector2(cx + 38, gy + 10), 3, 3, P.Comet);
                c.Put(cx + 38, gy + 10, P.Ink);
                for (int x = cx + 30; x <= cx + 50; x++) { c.Put(x, gy + 3 + jaw / 2, P.Void[0]); if (jaw > 0) c.Put(x, gy + 2 - jaw / 2, P.White); }
                int fl = Mathf.RoundToInt(wave * 4);
                c.Polygon(new[] { new Vector2(cx - 52, gy + 2), new Vector2(cx - 62, gy + 12 + fl), new Vector2(cx - 58, gy + 1), new Vector2(cx - 62, gy - 6 + fl) }, P.Astral[1]);
                break;
            }
        }
        RiverWaterline(c, cx, gy, s.w / 2 - 8, f >= 1 && f <= 4, f);
        c.Outline(P.Ink, 0.5f);
        if (dying) c = RiverSink(c, cx, gy, frame - 9);
        return c;
    }

    // =====================================================================
    // Shoal markers
    // =====================================================================

    /// <summary>Silver glints and rings on the water (fish), or pale shells (oyster beds).</summary>
    private static PixelCanvas SeaShoalFrame(bool oysters, int frame)
    {
        var c = new PixelCanvas(40, 22);
        var rng = new System.Random(oysters ? 77 : 33);
        for (int i = 0; i < 12; i++)
        {
            int x = 4 + rng.Next(32), y = 3 + rng.Next(16);
            bool on = (i + frame) % 3 != 0;
            if (oysters)
            {
                c.Put(x, y, P.Pearl[on ? 2 : 1]); c.Put(x + 1, y, P.Pearl[1]);
                if (i % 4 == frame) c.Put(x, y + 1, P.White);
            }
            else if (on)
            {
                c.Put(x, y, P.Steel[3]); c.Put(x + 1, y, P.Steel[2]);
                if ((i + frame) % 4 == 0) { c.Put(x - 1, y + 1, P.White); c.Put(x + 2, y + 1, new Color32(255, 255, 255, 160)); }
            }
        }
        // A ring spreading where a fish broke the surface.
        int r = 2 + frame * 2;
        for (int k = 0; k < 16; k++)
        {
            float a = k / 16f * Mathf.PI * 2f;
            c.Put(20 + Mathf.Cos(a) * r, 11 + Mathf.Sin(a) * r * 0.5f, new Color32(255, 255, 255, (byte)(180 - frame * 40)));
        }
        return c;
    }

    /// <summary>The marker seen when zoomed out: a soft round patch.</summary>
    private static PixelCanvas SeaShoalBlob(bool oysters)
    {
        var c = new PixelCanvas(24, 24);
        var col = oysters ? P.Pearl[3] : new Color32(140, 230, 255, 255);
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(12, 12)) / 12f;
                if (d > 1f) continue;
                c.Put(x, y, new Color32(col.r, col.g, col.b, (byte)(d > 0.8f ? 230 : 110 * (1f - d) + 40)));
            }
        return c;
    }
}
