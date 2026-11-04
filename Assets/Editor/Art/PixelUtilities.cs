using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;
using U = UtlCatalog;

/// <summary>
/// Utilities in pixel art (Setup Utilities): water works from the cistern to the matter
/// synthesiser, power stations from the steam dynamo to the vacuum engine, poles, pylons and
/// masts, data centres and spires, parks and scrubbers; and the aqueduct's 16 pieces (by which
/// sides join: N=1, E=2, S=4, W=8), a stone channel on arches that joins up seamlessly.
/// Pixels from the footprint's bottom-left corner, 32 a tile.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> UtlBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { U.Cistern, (PxUtlCistern, 4, 3f) }, { U.Fountain, (PxUtlFountain, 4, 6f) }, { U.Waterwheel, (PxUtlWaterwheel, 4, 5f) },
            { U.PumpStation, (PxUtlPumpStation, 4, 5f) }, { U.WaterTower, (PxUtlWaterTower, 4, 3f) }, { U.Purifier, (PxUtlPurifier, 4, 4f) },
            { U.Extractor, (PxUtlExtractor, 4, 5f) }, { U.Font, (PxUtlFont, 4, 5f) }, { U.Synth, (PxUtlSynth, 4, 6f) },
            { U.SteamDynamo, (PxUtlSteamDynamo, 4, 6f) }, { U.HydroDam, (PxUtlHydroDam, 4, 6f) }, { U.CoalPlant, (PxUtlCoalPlant, 4, 4f) },
            { U.OilPlant, (PxUtlOilPlant, 4, 4f) }, { U.WindTurbine, (PxUtlWindTurbine, 6, 8f) }, { U.NuclearPlant, (PxUtlNuclearPlant, 4, 4f) },
            { U.SolarFarm, (PxUtlSolarFarm, 4, 2f) }, { U.ManaReactor, (PxUtlManaReactor, 4, 6f) }, { U.AetherReactor, (PxUtlAetherReactor, 4, 6f) },
            { U.StellarTap, (PxUtlStellarTap, 4, 6f) }, { U.DysonCollector, (PxUtlDyson, 4, 5f) }, { U.VoidEngine, (PxUtlVoidEngine, 4, 6f) },
            { U.PowerPole, (PxUtlPowerPole, 1, 1f) }, { U.Pylon, (PxUtlPylon, 1, 1f) }, { U.Substation, (PxUtlSubstation, 4, 4f) },
            { U.TeslaMast, (PxUtlTeslaMast, 4, 8f) }, { U.LeyObelisk, (PxUtlLeyObelisk, 4, 4f) }, { U.BeamRelay, (PxUtlBeamRelay, 4, 6f) },
            { U.DataRelay, (PxUtlDataRelay, 4, 4f) }, { U.DataCentre, (PxUtlDataCentre, 4, 4f) }, { U.MindwebSpire, (PxUtlMindweb, 4, 4f) },
            { U.QuantumNode, (PxUtlQuantumNode, 4, 5f) }, { U.Noosphere, (PxUtlNoosphere, 4, 5f) },
            { U.Garden, (PxUtlGarden, 4, 2f) }, { U.CityPark, (PxUtlCityPark, 4, 3f) }, { U.Recycler, (PxUtlRecycler, 4, 5f) },
            { U.Scrubber, (PxUtlScrubber, 4, 6f) }, { U.SylvanGrove, (PxUtlSylvanGrove, 4, 3f) }, { U.Terraformer, (PxUtlTerraformer, 4, 5f) },
        };

    /// <summary>Draw one utility's looks (like GenerateBuildingLooks, for this file's art only).</summary>
    private static void GenerateUtlLooks(BuildingDefinition def)
    {
        if (!UtlBuildingArt.TryGetValue(def.id, out var art)) return;
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = def.ArtSize.x * TilePx, artH = def.ArtSize.y * TilePx;
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
        EditorUtility.SetDirty(def);
    }

    // =====================================================================
    // Shared pieces
    // =====================================================================

    private static readonly Color32[] UConcrete = { P.Hex("4c4c4e"), P.Hex("78787a"), P.Hex("a2a29e"), P.Hex("c8c8c2") };
    private static readonly Color32[] UBrickRed = { P.Hex("4a1e18"), P.Hex("7a3424"), P.Hex("a8503a"), P.Hex("cf7556") };
    private static readonly Color32[] UTarmac = { P.Hex("1e1e22"), P.Hex("34343a"), P.Hex("4a4a52"), P.Hex("62626a") };
    private static readonly Color32[] UPanelBlue = { P.Hex("0e1630"), P.Hex("1a2a5a"), P.Hex("2e4c94"), P.Hex("6c94d8") };

    /// <summary>An upright cylinder (a tank, a silo) lit from the left, with an oval top.</summary>
    private static void UCyl(K k, int x0, int x1, int y0, int y1, Color32[] ramp, bool bands = true)
    {
        float w = Mathf.Max(1, x1 - x0);
        for (int x = x0; x <= x1; x++)
        {
            float t = (x - x0) / w;
            var col = t < 0.12f ? ramp[1] : t < 0.4f ? ramp[3] : t < 0.82f ? ramp[2] : ramp[1];
            k.VLine(x, y0, y1, col);
            if (bands) for (int y = y0 + 5; y < y1; y += 7) k.Px(x, y, ramp[1]);
        }
        k.Ellipse((x0 + x1) / 2f, y1, w / 2f + 0.5f, Mathf.Max(1.5f, w / 6f), ramp);
    }

    /// <summary>A cooling tower: a waisted concrete shell, steam rising out of the top (drawn after Finish).</summary>
    private static void UCoolingTower(K k, int cx, int y0, int h, int rb)
    {
        for (int y = y0; y <= y0 + h; y++)
        {
            float t = (y - y0) / (float)h;
            float r = rb * (0.66f + 0.34f * Mathf.Pow((t - 0.72f) / 0.72f, 2f));
            for (int x = Mathf.RoundToInt(cx - r); x <= Mathf.RoundToInt(cx + r); x++)
            {
                float u = (x - (cx - r)) / (2f * r);
                var col = u < 0.1f ? UConcrete[1] : u < 0.45f ? UConcrete[3] : u < 0.85f ? UConcrete[2] : UConcrete[1];
                if ((y - y0) % 9 == 0) col = UConcrete[1];
                k.Px(x, y, col);
            }
        }
        float top = rb * 0.72f;
        k.Ellipse(cx, y0 + h, top, 2.5f, new[] { P.Coal[0], P.Coal[1], UConcrete[1], UConcrete[2] });
    }

    /// <summary>A tall chimney, banded red and white in the modern ages.</summary>
    private static void UStack(K k, int x, int y0, int h, int w, bool stripes)
    {
        for (int y = y0; y <= y0 + h; y++)
            for (int xx = x; xx < x + w; xx++)
            {
                bool red = stripes && ((y0 + h - y) / 6) % 2 == 0 && y > y0 + h - 30;
                var ramp = red ? P.Red : stripes ? UConcrete : UBrickRed;
                k.Px(xx, y, xx == x ? ramp[3] : xx == x + w - 1 ? ramp[1] : ramp[2]);
            }
        k.HLine(x - 1, x + w, y0 + h, P.Coal[1]);
        if (stripes) k.Px(x + w / 2, y0 + h + 1, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);   // aircraft lamp
    }

    /// <summary>A steel lattice tower tapering to a crossarm at the top.</summary>
    private static void ULattice(K k, int cx, int y0, int h, int half, Color32[] ramp)
    {
        int top = y0 + h;
        k.Line(cx - half, y0, cx - 2, top, ramp[1]);
        k.Line(cx + half, y0, cx + 2, top, ramp[2]);
        for (int y = y0 + 4; y < top; y += 6)
        {
            float t = (y - y0) / (float)h;
            int hw = Mathf.RoundToInt(Mathf.Lerp(half, 2, t));
            k.Line(cx - hw, y, cx + hw, y + 6, ramp[1]);
            k.Line(cx + hw, y, cx - hw, y + 6, ramp[1]);
            k.HLine(cx - hw, cx + hw, y, ramp[2]);
        }
        k.HLine(cx - 9, cx + 9, top - 6, ramp[2]);
        k.HLine(cx - 6, cx + 6, top, ramp[2]);
        foreach (int dx in new[] { -9, 9, -6, 6 })
            k.Px(cx + dx, (Mathf.Abs(dx) == 9 ? top - 6 : top) - 1, P.Linen[3]);   // insulators
    }

    /// <summary>A glowing orb that swells and fades with the animation.</summary>
    private static void UOrb(K k, float cx, float cy, float r, Color32[] ramp)
    {
        float pulse = 1f + 0.12f * k.Wave();
        k.Ellipse(cx, cy, r * pulse + 1.5f, r * pulse + 1.5f, new[] { ramp[0], ramp[0], ramp[1], ramp[1] });
        k.Ellipse(cx, cy, r * pulse, r * pulse, ramp);
        k.Px(Mathf.RoundToInt(cx - r * 0.35f), Mathf.RoundToInt(cy + r * 0.35f), P.White);
    }

    /// <summary>A pool of rippling water.</summary>
    private static void UPool(K k, float cx, float cy, float rx, float ry)
    {
        k.Ellipse(cx, cy, rx, ry, new[] { P.Water[0], P.Water[1], P.Water[2], P.Water[3] });
        for (int i = 0; i < 3; i++)
        {
            float a = (k.T + i / 3f) * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(cx + Mathf.Cos(a) * rx * 0.5f), Mathf.RoundToInt(cy + Mathf.Sin(a) * ry * 0.4f), P.Water[4]);
        }
    }

    /// <summary>A round tree (oak, plane, lime).</summary>
    private static void UTree(K k, int x, int y, int r, Color32[] leaf)
    {
        k.VLine(x, y, y + r, P.Wood[1]);
        k.Px(x + 1, y, P.Wood[0]);
        int sway = k.Frame % 4 == 1 ? 1 : 0;
        k.Ellipse(x + sway, y + r + r * 0.7f, r, r * 0.85f, new[] { leaf[0], leaf[1], leaf[2], leaf[3] });
    }

    /// <summary>A fenced yard of gravel.</summary>
    private static void UYard(K k, int x0, int y0, int x1, int y1)
    {
        k.Rect(x0, y0, x1, y1, UConcrete[1]);
        for (int i = 0; i < (x1 - x0) * (y1 - y0) / 18; i++) k.Px(x0 + k.R(0, x1 - x0), y0 + k.R(0, y1 - y0), UConcrete[2]);
    }

    private static void UInsulators(K k, int x, int y, int n)
    {
        for (int i = 0; i < n; i++) { k.Px(x, y + i * 2, P.Linen[3]); k.Px(x, y + i * 2 + 1, P.Brass[2]); }
    }

    // =====================================================================
    // Water
    // =====================================================================

    private static void PxUtlCistern(K k)
    {
        k.Ground(2, 2, 60, 58, P.Dirt);
        // A square stone tank with a plastered lip, half sunk, a gutter channel running in.
        k.Rect(8, 8, 56, 44, P.Stone[1]);
        k.Rect(10, 10, 54, 42, P.Plaster[2]);
        k.Rect(14, 18, 50, 40, P.Water[1]);
        UPool(k, 32, 30, 15, 8);
        k.HLine(10, 54, 42, P.Plaster[3]);
        k.Rect(8, 6, 56, 9, P.Stone[2]);
        k.Line(50, 54, 46, 42, P.Tile[2], 2);   // the gutter from the roofs
        k.Rect(2, 50, 52, 54, P.Tile[1]);
        k.HLine(2, 52, 54, P.Tile[3]);
        k.Pot(4, 10, P.Tile);
        k.Finish();
    }

    private static void PxUtlFountain(K k)
    {
        k.Ground(2, 2, 60, 60, P.Stone);
        // The castellum behind (a small domed water house), a round basin in front, a spout arcing.
        k.Wall(14, 34, 50, 64, K.Mat.Marble);
        Dome(k, 32, 64, 14, P.Marble);
        k.Door(28, 34, 8, 12, arched: true);
        k.Ellipse(32, 18, 24, 11, P.Marble);
        UPool(k, 32, 19, 20, 8);
        // a bronze lion's-head column with a jet of water
        k.Rect(30, 18, 34, 32, P.Bronze[2]);
        k.Ellipse(32, 33, 3, 3, P.Bronze);
        float t = k.T;
        for (int i = 0; i < 8; i++)
        {
            float u = i / 7f;
            int x = Mathf.RoundToInt(32 + u * 12), y = Mathf.RoundToInt(32 + Mathf.Sin(u * Mathf.PI) * 6 - u * 10);
            k.Px(x, y, (i + k.Frame) % 3 == 0 ? P.Water[4] : P.Water[3]);
        }
        if (k.Level >= 1) { k.Column(8, 10, 22); k.Column(54, 10, 22); }
        k.Finish();
    }

    private static void PxUtlWaterwheel(K k)
    {
        k.Ground(2, 2, 60, 46, P.Dirt);
        // The mill race in front, a big wheel turning in it, a timber tower and tank behind.
        k.Rect(0, 2, 63, 12, P.Water[1]);
        for (int x = 0; x < 64; x += 5) k.Px(x + k.Frame % 5, 7, P.Water[3]);
        k.Wall(32, 14, 58, 52, K.Mat.Planks);
        k.Roof(30, 60, 52, 10, K.RoofType.Shingle, 6);
        UCyl(k, 36, 54, 62, 74, P.Wood);
        int wheels = 1 + Mathf.Min(1, k.Level);
        for (int wI = 0; wI < wheels; wI++)
        {
            float cx = 16 + wI * 8, cy = 22;
            k.Ellipse(cx, cy, 13, 13, P.Wood, l => (l - new Vector2(cx, cy)).sqrMagnitude > 100f);
            for (int s = 0; s < 8; s++)
            {
                float a = (s / 8f + k.T / 8f) * Mathf.PI * 2f;
                k.Line(cx, cy, cx + Mathf.Cos(a) * 13, cy + Mathf.Sin(a) * 13, P.Wood[2]);
                k.Rect(Mathf.RoundToInt(cx + Mathf.Cos(a) * 12) - 1, Mathf.RoundToInt(cy + Mathf.Sin(a) * 12) - 1,
                    Mathf.RoundToInt(cx + Mathf.Cos(a) * 12) + 1, Mathf.RoundToInt(cy + Mathf.Sin(a) * 12) + 1, P.Wood[3]);
            }
            k.Ellipse(cx, cy, 2.5f, 2.5f, P.Iron);
        }
        k.Line(30, 22, 40, 64, P.Iron[1]);   // the bucket chain up to the tank
        k.Finish();
    }

    private static void PxUtlPumpStation(K k)
    {
        k.Ground(2, 2, 94, 62, P.Dirt);
        // A tall brick engine house with round-arched windows, a beam rocking out of its top, a chimney.
        SootWall(k, 10, 10, 70, 58);
        k.Roof(8, 72, 58, 12, K.RoofType.Slate, 6);
        for (int i = 0; i < 3; i++) k.Window(18 + i * 18, 26, 8, 14, true);
        k.Door(36, 10, 10, 14, arched: true);
        float rock = k.Wave() * 4f;
        k.Line(40, 72, 86, 72 + rock, P.Iron[2], 2);   // the beam
        k.Rect(84, 30, 88, 70 + Mathf.RoundToInt(rock), P.Iron[1]);   // pump rod
        k.Ellipse(86, 26, 5, 5, P.Iron);
        k.Chimney(76, 40, 6, 50, SootBrick);
        if (k.Level >= 1) UCyl(k, 2, 10, 10, 30, P.Steel);
        if (k.Level >= 2) k.Pit(80, 4, 92, 14);
        k.Finish();
        k.Smoke(79, 92, 0.9f, 150);
    }

    private static void PxUtlWaterTower(K k)
    {
        k.Ground(4, 2, 60, 40, P.Dirt);
        // A brick tower with a riveted iron tank on top and a ladder.
        k.Wall(16, 6, 48, 74, K.Mat.Stone);
        for (int y = 14; y < 74; y += 14) k.Window(29, y, 6, 8, false);
        k.Door(27, 6, 10, 12, arched: true);
        UCyl(k, 8, 56, 78, 112, P.Iron);
        for (int x = 10; x <= 54; x += 4) { k.Px(x, 80, P.Iron[3]); k.Px(x, 110, P.Iron[3]); }
        k.Roof(6, 58, 116, 8, K.RoofType.Slate, 2);
        k.VLine(52, 74, 112, P.Iron[0]);
        for (int y = 76; y < 112; y += 3) k.HLine(51, 53, y, P.Iron[2]);
        k.Finish();
    }

    private static void PxUtlPurifier(K k)
    {
        k.Ground(2, 2, 94, 94, UConcrete);
        // Round settling tanks with a turning arm, sand-filter beds, and a white chlorine house.
        for (int i = 0; i < 2; i++)
        {
            float cx = 22 + i * 40, cy = 22;
            k.Ellipse(cx, cy, 17, 10, UConcrete);
            k.Ellipse(cx, cy + 1, 14, 8, P.Water);
            float a = (k.T + i * 0.3f) * Mathf.PI * 2f;
            k.Line(cx, cy + 1, cx + Mathf.Cos(a) * 14, cy + 1 + Mathf.Sin(a) * 8, P.Steel[3]);
        }
        for (int i = 0; i < 3; i++) { k.Rect(8 + i * 18, 44, 22 + i * 18, 56, P.Sand[2]); k.HLine(8 + i * 18, 22 + i * 18, 56, P.Sand[3]); }
        k.Wall(64, 44, 92, 74, K.Mat.Plaster);
        k.FlatRoof(62, 94, 74, 6, UConcrete);
        k.Window(70, 54, 6, 8, true); k.Window(82, 54, 6, 8, true);
        UCyl(k, 66, 74, 76, 92, P.Green);
        if (k.Level >= 1) UCyl(k, 80, 88, 76, 96, P.Blue);
        if (k.Level >= 2) k.Rect(60, 4, 92, 14, P.Steel[2]);
        k.Finish();
    }

    private static void PxUtlExtractor(K k)
    {
        k.Ground(4, 2, 60, 50, P.Sand);
        // A tall white vane tower whose fins frost over; a collecting tank at its foot.
        UCyl(k, 10, 26, 6, 26, P.Chrome);
        k.Rect(30, 6, 44, 70, Brushed[2]);
        for (int y = 12; y < 70; y += 4)
        {
            bool frost = ((y / 4) + k.Frame) % 4 == 0;
            k.HLine(26, 48, y, frost ? P.White : Brushed[3]);
        }
        k.Rect(28, 70, 46, 76, Brushed[1]);
        k.Ellipse(37, 80, 6, 3, P.Neon);
        int drip = 26 - (k.Frame * 4) % 20;
        k.Px(18, drip, P.Water[4]);
        k.Line(26, 18, 30, 24, P.Steel[1]);
        k.Finish();
    }

    private static void PxUtlFont(K k)
    {
        k.Ground(2, 2, 60, 56, P.Grass);
        // A rune-carved basin welling over, a ring of standing stones, blue light rising.
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(32 + Mathf.Cos(a) * 24), y = Mathf.RoundToInt(26 + Mathf.Sin(a) * 14);
            k.Rect(x - 2, y, x + 2, y + 16, P.Rock[2]);
            k.VLine(x - 2, y, y + 16, P.Rock[3]);
            k.Px(x, y + 10, (i + k.Frame) % 2 == 0 ? P.Rune[3] : P.Rune[2]);
        }
        k.Ellipse(32, 24, 16, 8, P.Marble);
        UPool(k, 32, 26, 13, 6);
        for (int i = 0; i < 4; i++)
        {
            float u = (k.T + i / 4f) % 1f;
            k.Px(26 + i * 4, 30 + Mathf.RoundToInt(u * 40), new Color32(140, 210, 255, (byte)(220 * (1 - u))));
        }
        k.Finish();
    }

    private static void PxUtlSynth(K k)
    {
        k.Ground(2, 2, 94, 94, UConcrete);
        // A great sphere of light in a ring frame, condensing water into a clear tank below.
        UCyl(k, 14, 82, 6, 30, new[] { P.Water[1], P.Water[2], P.Water[3], P.Water[4] }, false);
        k.Rect(10, 30, 86, 36, P.Starmetal[2]);
        k.Line(18, 36, 40, 92, P.Starmetal[1], 2);
        k.Line(78, 36, 56, 92, P.Starmetal[1], 2);
        UOrb(k, 48, 72, 16, P.Cosmic);
        FloatingRing(k, 48, 72, 26, 8);
        for (int i = 0; i < 5; i++) k.Px(40 + i * 4, 40 + (k.Frame * 5 + i * 7) % 18, P.Water[4]);
        k.Finish();
    }

    // =====================================================================
    // Power
    // =====================================================================

    private static void PxUtlSteamDynamo(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        // A brick boiler house under a tall chimney; through the open end a dynamo whirls; a coal heap.
        SootWall(k, 6, 10, 66, 52);
        k.Roof(4, 68, 52, 12, K.RoofType.Slate, 6);
        k.Door(30, 10, 16, 18, open: true);
        Cog(k, 38, 19, 6, k.Frame, P.Brass);
        for (int i = 0; i < 2; i++) k.Window(10 + i * 44, 26, 8, 12, true);
        k.Chimney(70, 20, 8, 90, SootBrick);
        k.Ellipse(84, 10, 10, 6, P.Coal, l => l.y >= 4);
        // the first wires leave from a pole on the roof
        k.VLine(14, 64, 82, P.Wood[1]); k.HLine(9, 19, 80, P.Wood[2]); UInsulators(k, 9, 81, 1); UInsulators(k, 19, 81, 1);
        if (k.Level >= 1) UCyl(k, 82, 92, 20, 40, P.Iron);
        if (k.Level >= 2) k.Chimney(56, 64, 5, 30, SootBrick);
        k.Finish();
        k.Smoke(74, 112, 1.2f, 170);
    }

    private static void PxUtlHydroDam(K k)
    {
        k.Ground(2, 2, 94, 62, P.Stone);
        // A concrete weir with water pouring over its face, a turbine hall on the bank beside it.
        k.Rect(0, 30, 60, 46, UConcrete[2]);
        k.HLine(0, 60, 46, UConcrete[3]);
        for (int x = 2; x < 60; x += 6) k.Rect(x, 32, x + 2, 44, UConcrete[1]);
        for (int x = 0; x <= 60; x++)
        {
            int fall = (x + k.Frame * 3) % 7;
            k.Px(x, 28 - fall % 3, P.Water[3]);
            for (int y = 2; y < 28; y++) k.Px(x, y, (y + k.Frame * 2 + x) % 6 == 0 ? P.Water[4] : P.Water[2]);
        }
        k.Wall(62, 12, 92, 54, K.Mat.Stone);
        k.FlatRoof(60, 94, 54, 6, UConcrete);
        k.Window(68, 28, 6, 12, true); k.Window(80, 28, 6, 12, true);
        k.VLine(88, 60, 82, P.Iron[1]); k.HLine(82, 94, 80, P.Iron[2]); UInsulators(k, 83, 81, 1); UInsulators(k, 93, 81, 1);
        if (k.Level >= 1) Cog(k, 77, 18, 5, k.Frame, P.Steel);
        k.Finish();
    }

    private static void PxUtlCoalPlant(K k)
    {
        k.Ground(2, 2, 126, 94, UConcrete);
        // Two cooling towers behind, the turbine hall and a banded chimney, a coal yard and conveyor.
        UCoolingTower(k, 30, 40, 72, 22);
        UCoolingTower(k, 74, 44, 64, 19);
        k.Wall(8, 10, 84, 40, K.Mat.Stone);
        k.FlatRoof(6, 86, 40, 6, UConcrete);
        for (int x = 14; x < 82; x += 12) k.Window(x, 20, 6, 12, true);
        UStack(k, 98, 20, 120, 8, true);
        k.Ellipse(110, 12, 14, 7, P.Coal, l => l.y >= 6);
        k.Line(86, 24, 104, 12, P.Iron[1], 2);
        if (k.Level >= 1) k.Rect(88, 40, 96, 60, P.Steel[2]);
        if (k.Level >= 2) UCyl(k, 112, 122, 24, 50, P.Steel);
        k.Finish();
        k.Smoke(30, 114, 1.6f, 200);
        k.Smoke(74, 110, 1.4f, 200);
        k.Smoke(102, 142, 1.1f, 150);
    }

    private static void PxUtlOilPlant(K k)
    {
        k.Ground(2, 2, 94, 94, UConcrete);
        // White fuel tanks, a boiler block with pipes, and a red-and-white chimney.
        UCyl(k, 6, 30, 50, 72, P.Linen);
        UCyl(k, 34, 54, 54, 72, P.Linen);
        k.Wall(8, 8, 62, 42, K.Mat.Plaster);
        k.FlatRoof(6, 64, 42, 6, UConcrete);
        k.Window(16, 20, 6, 10, true); k.Window(40, 20, 6, 10, true);
        for (int i = 0; i < 3; i++) k.HLine(62, 76, 18 + i * 6, P.Steel[2]);
        UStack(k, 74, 10, 100, 8, true);
        if (k.Level >= 1) UCyl(k, 82, 92, 10, 34, P.Steel);
        if (k.Level >= 2) k.Rect(60, 50, 72, 70, P.Steel[1]);
        k.Finish();
        k.Smoke(78, 112, 1f, 140);
    }

    private static void PxUtlWindTurbine(K k)
    {
        k.Ground(4, 2, 28, 24, P.Grass);
        // A tapering white mast, a nacelle and three long blades turning.
        for (int y = 4; y < 80; y++)
        {
            int hw = y < 40 ? 3 : 2;
            for (int x = 16 - hw; x <= 16 + hw - 1; x++) k.Px(x, y, x < 16 ? P.Linen[3] : P.Linen[2]);
        }
        k.Rect(12, 78, 22, 84, P.Linen[3]);
        float spin = k.Frame / (float)k.Frames * Mathf.PI * 2f / 3f;
        for (int b = 0; b < 3; b++)
        {
            float a = spin + b * Mathf.PI * 2f / 3f + 0.3f;
            k.Line(12, 81, 12 + Mathf.Cos(a) * 30, 81 + Mathf.Sin(a) * 30, P.White, 2);
        }
        k.Ellipse(12, 81, 2.5f, 2.5f, P.Steel);
        k.Finish();
    }

    private static void PxUtlNuclearPlant(K k)
    {
        k.Ground(2, 2, 126, 126, UConcrete);
        // A chrome reactor dome with a glowing blue band between two cooling towers; the turbine hall in front.
        UCoolingTower(k, 22, 60, 70, 19);
        UCoolingTower(k, 106, 60, 70, 19);
        k.Rect(44, 40, 84, 70, P.Chrome[2]);
        k.Ellipse(64, 70, 20, 22, P.Chrome, l => l.y >= 70);
        byte glow = (byte)(160 + 60 * k.Wave());
        k.HLine(44, 84, 56, new Color32(90, 200, 255, glow));
        k.HLine(44, 84, 57, new Color32(150, 230, 255, glow));
        k.Wall(14, 8, 114, 38, K.Mat.Plaster);
        k.FlatRoof(12, 116, 38, 6, UConcrete);
        for (int x = 22; x < 110; x += 14) k.Window(x, 18, 8, 10, true);
        k.Px(64, 93, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);
        if (k.Level >= 1) UOrb(k, 64, 82, 4, P.Neon);
        if (k.Level >= 2) FloatingRing(k, 64, 70, 26, 6);
        k.Finish();
        k.Smoke(22, 132, 1.5f, 200);
        k.Smoke(106, 132, 1.5f, 200);
    }

    private static void PxUtlSolarFarm(K k)
    {
        k.Ground(2, 2, 94, 94, P.Grass);
        // Rows of dark blue panels on frames, glinting.
        for (int row = 0; row < 4; row++)
        {
            int y = 70 - row * 20;
            for (int x = 6; x < 90; x++)
                for (int yy = y; yy < y + 12; yy++)
                {
                    bool grid = (x - 6) % 10 == 0 || (yy - y) % 6 == 0;
                    var col = grid ? UPanelBlue[0] : (yy - y) > 8 ? UPanelBlue[3] : UPanelBlue[2];
                    if (!grid && (x + row * 7 + k.Frame * 12) % 40 == 0) col = P.White;
                    k.Px(x, yy, col);
                }
            k.HLine(6, 89, y - 1, P.Steel[1]);
            for (int x = 10; x < 90; x += 20) k.VLine(x, y - 5, y - 1, P.Steel[1]);
        }
        if (k.Level >= 1) k.Rect(84, 2, 94, 10, P.Steel[2]);
        k.Finish();
    }

    private static void PxUtlManaReactor(K k)
    {
        k.Ground(2, 2, 94, 94, P.Dirt);
        // A ring of runestones wound with copper round a floating crystal blazing with mana.
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(48 + Mathf.Cos(a) * 36), y = Mathf.RoundToInt(36 + Mathf.Sin(a) * 18);
            k.Rect(x - 3, y, x + 3, y + 26, P.Rock[2]);
            k.VLine(x - 3, y, y + 26, P.Rock[3]); k.HLine(x - 3, x + 3, y + 26, P.Rock[3]);
            k.Px(x, y + 18, (i + k.Frame) % 3 == 0 ? P.Rune[3] : P.Rune[1]);
            k.Line(x, y + 24, 48, 70, P.Bronze[2]);
        }
        float bob = k.Wave() * 2f;
        var crystal = new[] { new Vector2(48, 50 + bob), new Vector2(58, 70 + bob), new Vector2(48, 96 + bob), new Vector2(38, 70 + bob) };
        k.Polygon(crystal, p => p.x < 48 ? P.Mana[3] : P.Mana[2]);
        UOrb(k, 48, 70 + bob, 5, P.Mana);
        if (k.Level >= 1)
        {
            var c2 = new[] { new Vector2(70, 40), new Vector2(76, 52), new Vector2(70, 66), new Vector2(64, 52) };
            k.Polygon(c2, p => p.x < 70 ? P.Arcane[3] : P.Arcane[2]);
        }
        k.Finish();
    }

    private static void PxUtlAetherReactor(K k)
    {
        k.Ground(2, 2, 94, 94, P.Stone);
        // A white spire on a stepped base; a bead of aether burns inside spinning gold rings at its top.
        k.Rect(18, 8, 78, 22, P.Marble[2]); k.HLine(18, 78, 22, P.Marble[3]);
        k.Rect(28, 22, 68, 34, P.Marble[2]); k.HLine(28, 68, 34, P.Marble[3]);
        for (int y = 34; y < 120; y++)
        {
            int hw = Mathf.RoundToInt(Mathf.Lerp(10, 3, (y - 34) / 86f));
            for (int x = 48 - hw; x <= 48 + hw; x++) k.Px(x, y, x < 48 ? P.Marble[3] : P.Marble[2]);
        }
        UOrb(k, 48, 132, 7, P.Aether);
        FloatingRing(k, 48, 132, 16, 5);
        if (k.Level >= 1) FloatingRing(k, 48, 132, 22, 8);
        k.Finish();
    }

    private static void PxUtlStellarTap(K k)
    {
        k.Ground(2, 2, 94, 94, UConcrete);
        // A great mirrored dish on a tripod catching a beam that comes straight down out of the sky.
        k.Line(26, 8, 48, 70, Brushed[1], 2); k.Line(70, 8, 48, 70, Brushed[2], 2); k.Line(48, 4, 48, 70, Brushed[1], 2);
        k.Ellipse(48, 78, 34, 14, new[] { P.Solar[0], P.Solar[1], P.Chrome[2], P.Chrome[3] }, l => l.y <= 82);
        k.Ellipse(48, 84, 30, 6, P.Chrome);
        byte a = (byte)(150 + 70 * k.Wave());
        for (int y = 86; y < 160; y++) { k.Px(47, y, new Color32(255, 240, 170, a)); k.Px(48, y, new Color32(255, 255, 220, a)); k.Px(49, y, new Color32(255, 240, 170, a)); }
        UOrb(k, 48, 90, 4, P.Solar);
        if (k.Level >= 1) { k.Px(46, 150, P.White); k.Px(50, 140, P.White); }
        k.Finish();
    }

    private static void PxUtlDyson(K k)
    {
        k.Ground(2, 2, 126, 126, UConcrete);
        // A ring of receiver dishes round a pillar of starlight.
        for (int i = 0; i < 6; i++)
        {
            float ang = i / 6f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(64 + Mathf.Cos(ang) * 46), y = Mathf.RoundToInt(56 + Mathf.Sin(ang) * 26);
            k.VLine(x, y, y + 14, Brushed[1]);
            k.Ellipse(x, y + 18, 7, 4, Brushed);
            k.Px(x, y + 19, P.Solar[3]);
        }
        k.Rect(52, 30, 76, 60, P.Starmetal[2]);
        byte a = (byte)(170 + 60 * k.Wave());
        for (int y = 60; y < 166; y++)
            for (int x = 58; x <= 70; x++)
                if (Mathf.Abs(x - 64) <= 6 - (y % 5 == 0 ? 1 : 0)) k.Px(x, y, Mathf.Abs(x - 64) < 2 ? new Color32(255, 255, 230, a) : new Color32(255, 210, 120, (byte)(a / 2)));
        UOrb(k, 64, 62, 6, P.Solar);
        if (k.Level >= 1) FloatingRing(k, 64, 100, 20, 5);
        k.Finish();
    }

    private static void PxUtlVoidEngine(K k)
    {
        k.Ground(2, 2, 94, 94, P.Void);
        // A black sphere caged in bars of light, crackling.
        k.Rect(20, 8, 76, 20, P.Starmetal[1]); k.HLine(20, 76, 20, P.Starmetal[3]);
        for (int i = 0; i < 5; i++)
        {
            int x = 24 + i * 12;
            k.Line(x, 20, 48 + (x - 48) / 2, 130, i % 2 == 0 ? P.Cosmic[3] : P.Cosmic[2]);
        }
        float r = 20 + k.Wave() * 1.5f;
        k.Ellipse(48, 74, r + 3, r + 3, new[] { P.Cosmic[1], P.Cosmic[2], P.Cosmic[2], P.Cosmic[3] });
        k.Ellipse(48, 74, r, r, new[] { P.Void[0], P.Void[0], P.Void[1], P.Void[2] });
        for (int i = 0; i < 4; i++)
        {
            float a = (k.T + i / 4f) * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(48 + Mathf.Cos(a) * (r + 6)), Mathf.RoundToInt(74 + Mathf.Sin(a) * (r + 6)), P.White);
        }
        if (k.Level >= 1) FloatingRing(k, 48, 74, 32, 9);
        k.Finish();
    }

    // ---------- Carrying power ----------

    private static void PxUtlPowerPole(K k)
    {
        // A creosoted pole, a crossarm with glass insulators, a step peg or two.
        k.Ellipse(16, 4, 5, 2, P.Dirt);
        k.VLine(15, 3, 56, P.Wood[1]); k.VLine(16, 3, 56, P.Wood[0]);
        k.HLine(6, 25, 50, P.Wood[2]); k.HLine(6, 25, 49, P.Wood[1]);
        UInsulators(k, 7, 51, 1); UInsulators(k, 24, 51, 1); UInsulators(k, 15, 57, 1);
        k.Px(14, 30, P.Iron[2]); k.Px(17, 36, P.Iron[2]);
        k.Finish();
    }

    private static void PxUtlPylon(K k)
    {
        k.Ellipse(16, 4, 9, 3, P.Dirt);
        ULattice(k, 16, 3, 86, 9, P.Steel);
        k.Finish();
    }

    private static void PxUtlSubstation(K k)
    {
        k.Ground(2, 2, 60, 60, UConcrete);
        UYard(k, 4, 4, 58, 52);
        // Transformers with cooling fins, bushings and a gantry, behind a wire fence.
        for (int i = 0; i < 2; i++)
        {
            int x = 10 + i * 24;
            k.Rect(x, 14, x + 16, 34, P.Olive[2]);
            for (int f = x + 1; f < x + 16; f += 3) k.VLine(f, 16, 32, P.Olive[1]);
            k.HLine(x, x + 16, 34, P.Olive[3]);
            for (int b = 0; b < 3; b++) { k.VLine(x + 3 + b * 5, 35, 42, P.Linen[3]); k.Px(x + 3 + b * 5, 43, P.Brass[2]); }
        }
        k.VLine(6, 30, 58, P.Steel[2]); k.VLine(56, 30, 58, P.Steel[2]); k.HLine(6, 56, 56, P.Steel[2]);
        if (k.Frame % 2 == 0) k.Px(30, 46, P.Neon[3]);
        k.Fence(2, 60, 2, 8);
        k.Finish();
    }

    private static void PxUtlTeslaMast(K k)
    {
        k.Ellipse(16, 4, 7, 3, P.Dirt);
        // A copper mast in coils, crowned with a sphere that throws sparks.
        k.Rect(10, 3, 22, 10, P.Brass[1]);
        for (int y = 10; y < 72; y++) k.HLine(13, 19, y, y % 3 == 0 ? P.Bronze[3] : P.Bronze[2]);
        k.Ellipse(16, 80, 8, 8, P.Chrome);
        for (int i = 0; i < 2; i++)
        {
            float a = (k.Frame * 0.9f + i * 3.1f);
            float x = 16 + Mathf.Cos(a) * 14, y = 80 + Mathf.Sin(a) * 12;
            k.Line(16 + Mathf.Cos(a) * 8, 80 + Mathf.Sin(a) * 8, x, y, new Color32(180, 220, 255, 230));
        }
        k.Finish();
    }

    private static void PxUtlLeyObelisk(K k)
    {
        k.Ellipse(16, 4, 7, 3, P.Grass);
        for (int y = 3; y < 80; y++)
        {
            int hw = Mathf.RoundToInt(Mathf.Lerp(6, 2, (y - 3) / 77f));
            for (int x = 16 - hw; x <= 16 + hw; x++) k.Px(x, y, x < 16 ? P.Stone[3] : P.Stone[2]);
        }
        k.Polygon(new[] { new Vector2(13, 80), new Vector2(16, 88), new Vector2(19, 80) }, _ => P.Stone[3]);
        for (int y = 12; y < 74; y += 9) k.Px(16, y, ((y / 9) + k.Frame) % 3 == 0 ? P.Rune[3] : P.Rune[1]);
        k.Finish();
    }

    private static void PxUtlBeamRelay(K k)
    {
        k.Ground(4, 2, 60, 56, UConcrete);
        for (int y = 6; y < 74; y++) k.HLine(28, 35, y, y % 8 == 0 ? Brushed[1] : Brushed[2]);
        k.Rect(20, 6, 44, 12, Brushed[1]);
        var lens = new[] { new Vector2(22, 80), new Vector2(32, 92), new Vector2(42, 80), new Vector2(32, 72) };
        k.Polygon(lens, p => p.x < 32 ? P.Prism[3] : P.Prism[2]);
        UOrb(k, 32, 82, 3, P.Solar);
        k.Finish();
    }

    // =====================================================================
    // Data
    // =====================================================================

    private static void PxUtlDataRelay(K k)
    {
        k.Ellipse(16, 4, 8, 3, UConcrete);
        ULattice(k, 16, 3, 82, 7, Brushed);
        k.Rect(8, 58, 12, 70, P.Linen[3]); k.Rect(20, 50, 24, 62, P.Linen[3]);   // panel antennas
        Dish(k, 18, 40, 5);
        k.Px(16, 87, k.Frame % 2 == 0 ? P.Red[3] : P.Red[0]);
        k.Finish();
    }

    private static void PxUtlDataCentre(K k)
    {
        k.Ground(2, 2, 94, 62, UConcrete);
        // A windowless grey box, a row of chillers and fans on the roof, blinking status lights.
        k.Rect(6, 8, 90, 52, UConcrete[2]);
        for (int x = 6; x <= 90; x += 12) k.VLine(x, 8, 52, UConcrete[1]);
        k.HLine(6, 90, 52, UConcrete[3]);
        k.FlatRoof(4, 92, 52, 8, UConcrete);
        for (int i = 0; i < 4; i++)
        {
            int x = 10 + i * 20;
            k.Rect(x, 60, x + 14, 70, Brushed[2]);
            float a = (k.T + i * 0.25f) * Mathf.PI * 2f;
            k.Ellipse(x + 7, 70, 5, 2, Brushed);
            k.Line(x + 7 - Mathf.Cos(a) * 4, 70, x + 7 + Mathf.Cos(a) * 4, 70, P.Coal[1]);
        }
        for (int i = 0; i < 8; i++) k.Px(12 + i * 10, 30, (i + k.Frame) % 3 == 0 ? P.Neon[3] : P.Green[3]);
        k.Door(44, 8, 8, 12);
        if (k.Level >= 1) UCyl(k, 84, 92, 8, 30, P.Blue);
        if (k.Level >= 2) Dish(k, 70, 80, 6);
        k.Finish();
    }

    private static void PxUtlMindweb(K k)
    {
        k.Ground(4, 2, 60, 56, P.Stone);
        // A spire of violet crystal, threads of light running between the seers' seats round it.
        for (int y = 6; y < 110; y++)
        {
            int hw = Mathf.RoundToInt(Mathf.Lerp(12, 1, (y - 6) / 104f));
            for (int x = 32 - hw; x <= 32 + hw; x++) k.Px(x, y, x < 32 ? P.Purple[3] : P.Purple[2]);
        }
        for (int i = 0; i < 3; i++)
        {
            int x = 8 + i * 24;
            k.Rect(x - 3, 6, x + 3, 12, P.Marble[2]);
            k.Line(x, 12, 32, 60 + i * 10, new Color32(220, 160, 255, (byte)(140 + 60 * Mathf.Sin(k.T * 6.28f + i))));
        }
        UOrb(k, 32, 112, 4, P.Arcane);
        k.Finish();
    }

    private static void PxUtlQuantumNode(K k)
    {
        k.Ground(4, 2, 60, 60, UConcrete);
        // A frosted vacuum bell over a lattice of glowing atoms, wrapped in cold pipes.
        k.Rect(10, 6, 54, 16, Brushed[1]); k.HLine(10, 54, 16, Brushed[3]);
        k.Ellipse(32, 44, 18, 28, new[] { P.Steel[1], P.Steel[2], P.Pearl[2], P.Pearl[3] }, l => l.y >= 16);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                k.Px(26 + i * 6, 30 + j * 8, (i + j + k.Frame) % 2 == 0 ? P.Neon[3] : P.Neon[1]);
        k.VLine(8, 16, 60, P.Blue[2]); k.VLine(56, 16, 60, P.Blue[2]);
        k.Finish();
    }

    private static void PxUtlNoosphere(K k)
    {
        k.Ground(2, 2, 94, 94, P.Void);
        // A lattice of light climbing into the sky from a ring of nodes.
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(48 + Mathf.Cos(a) * 34), y = Mathf.RoundToInt(28 + Mathf.Sin(a) * 16);
            UOrb(k, x, y + 6, 4, P.Cosmic);
            k.Line(x, y + 6, 48, 150, new Color32(200, 180, 255, (byte)(120 + 80 * Mathf.Sin(k.T * 6.28f + i))));
        }
        for (int y = 40; y < 150; y += 12) k.HLine(48 - (150 - y) / 6, 48 + (150 - y) / 6, y, new Color32(220, 210, 255, 140));
        UOrb(k, 48, 150, 5, P.Prism);
        k.Finish();
    }

    // =====================================================================
    // Clean air
    // =====================================================================

    private static void PxUtlGarden(K k)
    {
        k.Ground(2, 2, 60, 60, P.Grass);
        // Walled beds of herbs round a sundial, two fruit trees.
        k.Rect(4, 4, 59, 6, P.Stone[2]); k.Rect(4, 54, 59, 58, P.Stone[2]);
        for (int i = 0; i < 4; i++)
        {
            int x = 8 + (i % 2) * 28, y = 12 + (i / 2) * 22;
            k.Rect(x, y, x + 20, y + 12, P.Dirt[1]);
            k.CropRow(x + 1, x + 19, y + 3, 3, i % 2 == 0 ? P.Leaf : P.Green);
            k.CropRow(x + 1, x + 19, y + 8, 2, P.Flower);
        }
        k.Rect(30, 28, 33, 34, P.Stone[3]);
        UTree(k, 10, 44, 6, P.Leaf);
        UTree(k, 52, 44, 6, P.Leaf);
        k.Finish();
    }

    private static void PxUtlCityPark(K k)
    {
        k.Ground(2, 2, 94, 94, P.Grass);
        // Lawns crossed by gravel paths, plane trees, a bandstand, railings.
        k.Rect(44, 2, 52, 94, P.Sand[2]); k.Rect(2, 44, 94, 52, P.Sand[2]);
        UTree(k, 14, 60, 9, P.Leaf); UTree(k, 78, 62, 9, P.Leaf); UTree(k, 16, 14, 8, P.Pine); UTree(k, 80, 12, 8, P.Leaf);
        k.Ellipse(48, 48, 12, 6, P.Stone);
        for (int x = 38; x <= 58; x += 5) k.VLine(x, 50, 62, P.Iron[2]);
        k.Ellipse(48, 64, 13, 5, P.Red);
        if (k.Level >= 1) UPool(k, 72, 30, 14, 7);
        k.Fence(2, 94, 2, 6);
        k.Finish();
    }

    private static void PxUtlRecycler(K k)
    {
        k.Ground(2, 2, 94, 62, UConcrete);
        // A green-clad shed, sorting belts carrying scrap, bales stacked outside, a short clean stack.
        k.Rect(8, 10, 70, 50, P.Green[1]);
        for (int x = 8; x <= 70; x += 4) k.VLine(x, 10, 50, P.Green[0]);
        k.FlatRoof(6, 72, 50, 6, UConcrete);
        k.Door(30, 10, 18, 20, open: true);
        for (int x = 32; x < 46; x += 3) k.Px(x + k.Frame % 3, 20, k.R(0, 2) == 0 ? P.Steel[3] : P.Red[2]);
        for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++) k.Rect(74 + i * 6, 8 + j * 6, 79 + i * 6, 13 + j * 6, j == 0 ? P.Steel[2] : P.Blue[2]);
        UStack(k, 60, 50, 30, 5, false);
        k.Finish();
        k.Smoke(62, 82, 0.6f, 80);
    }

    private static void PxUtlScrubber(K k)
    {
        k.Ground(4, 2, 60, 56, UConcrete);
        // A tall tower of filter beds and fan rings, drawing in brown air at the bottom and blowing white out the top.
        k.Rect(14, 6, 50, 110, Brushed[2]);
        for (int y = 14; y < 106; y += 12)
        {
            k.HLine(14, 50, y, Brushed[1]);
            k.Rect(18, y + 2, 46, y + 8, P.Green[1]);
            for (int x = 20; x < 46; x += 3) k.Px(x, y + 5, P.Green[3]);
        }
        float a = k.T * Mathf.PI * 2f;
        k.Ellipse(32, 114, 14, 4, Brushed);
        k.Line(32 - Mathf.Cos(a) * 12, 114, 32 + Mathf.Cos(a) * 12, 114, P.Coal[1]);
        if (k.Level >= 1) k.Rect(6, 6, 12, 60, Brushed[1]);
        k.Finish();
        k.Smoke(32, 118, 0.8f, 200);
    }

    private static void PxUtlSylvanGrove(K k)
    {
        k.Ground(2, 2, 60, 56, P.Grass);
        // Silver-leaved enchanted trees round a still pool with motes of light.
        UPool(k, 32, 20, 14, 6);
        var silver = new[] { P.Moonstone[0], P.Moonstone[1], P.Moonstone[2], P.Moonstone[3] };
        UTree(k, 10, 26, 8, silver); UTree(k, 52, 28, 8, silver); UTree(k, 32, 42, 10, P.Leaf);
        for (int i = 0; i < 4; i++)
        {
            float u = (k.T + i / 4f) % 1f;
            k.Px(14 + i * 11, 30 + Mathf.RoundToInt(u * 40), new Color32(220, 255, 220, (byte)(230 * (1 - u))));
        }
        k.Finish();
    }

    private static void PxUtlTerraformer(K k)
    {
        k.Ground(2, 2, 94, 94, P.Dirt);
        // A great stepped engine with intake vents round its foot and a tall exhaust breathing clean air into the sky.
        k.Rect(8, 8, 88, 40, P.Starmetal[1]);
        for (int x = 12; x < 86; x += 8) k.Rect(x, 14, x + 4, 30, P.Coal[1]);
        k.HLine(8, 88, 40, P.Starmetal[3]);
        k.Rect(24, 40, 72, 70, P.Starmetal[2]); k.HLine(24, 72, 70, P.Starmetal[3]);
        for (int y = 70; y < 150; y++)
        {
            int hw = Mathf.RoundToInt(Mathf.Lerp(12, 7, (y - 70) / 80f));
            for (int x = 48 - hw; x <= 48 + hw; x++) k.Px(x, y, x < 48 - hw / 2 ? P.Starmetal[3] : P.Starmetal[2]);
        }
        k.Ellipse(48, 150, 8, 3, new[] { P.Coal[0], P.Coal[1], P.Neon[1], P.Neon[2] });
        for (int i = 0; i < 4; i++) k.Px(30 + i * 12, 56, (i + k.Frame) % 2 == 0 ? P.Neon[3] : P.Neon[1]);
        k.Finish();
        k.Smoke(48, 152, 1.6f, 190);
    }

    // =====================================================================
    // Aqueduct pieces (index = which sides join: N=1, E=2, S=4, W=8)
    // =====================================================================

    private const int AqLift = 18, AqHalf = 5, AqCanvasH = 64;

    private static List<Sprite> UtlAqueductPieces(string id)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var list = new List<Sprite>();
        for (int mask = 0; mask < 16; mask++)
            list.Add(SavePixel(UtlAqueductPiece(mask), $"{dir}/{id}_piece{mask}.png", PixelsPerTile, Vector2.zero));
        return list;
    }

    /// <summary>
    /// One aqueduct tile: a stone channel lifted AqLift pixels above the ground, its arms running to
    /// each joined side (a lone piece lies east-west). East-west arms stand on an arcade of round arches
    /// (a pier every 16 pixels, so neighbours line up); a run that ends or turns towards you shows the
    /// pier under its front. No outline round the canvas edge, so tiles join without seams.
    /// </summary>
    private static PixelCanvas UtlAqueductPiece(int mask)
    {
        var c = new PixelCanvas(32, AqCanvasH);
        bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
        if (!n && !e && !s && !w) { e = true; w = true; }
        var stone = P.Plaster;
        int ground = 16 - AqHalf - 3, top = 16 - AqHalf + AqLift;   // the arcade's foot, and the channel's front lip
        // Arcade under the east-west arms: piers 4 wide, round arches between them.
        void Arcade(int x0, int x1)
        {
            for (int x = x0; x < x1; x++)
            {
                int u = x % 16;
                bool pier = u < 4;
                float dx = u - 9.5f, rise = Mathf.Sqrt(Mathf.Max(0f, 30.25f - dx * dx));
                int spring = top - 11;
                for (int y = ground; y < top; y++)
                {
                    if (!pier && y > ground && y < spring + rise) continue;   // the opening
                    Color32 col = u == 0 ? stone[1] : u == 3 ? stone[1] : u == 1 ? stone[3] : stone[2];
                    if (!pier) col = y >= top - 3 ? stone[2] : (y - ground) % 4 == 0 ? stone[1] : stone[2];   // the spandrel courses
                    if (y == ground) col = stone[1];
                    if (!pier && Mathf.Abs(y - (spring + rise)) < 1f) col = stone[3];   // the arch ring
                    c.Put(x, y, col);
                }
            }
        }
        if (w) Arcade(0, 16);
        if (e) Arcade(16, 32);
        // A pier under the front of a north-south run where it ends or turns (the rest hide under the channel).
        if (s ? false : n)
            for (int y = ground; y < top; y++)
                for (int x = 12; x < 20; x++)
                    c.Put(x, y, x == 12 ? stone[3] : x == 19 ? stone[1] : (y - ground) % 4 == 0 ? stone[1] : stone[2]);
        // The channel on top: a stone trough with water in it.
        bool Inside(int x, int y)
        {
            bool inX = x >= 16 - AqHalf && x < 16 + AqHalf, inY = y >= 16 - AqHalf && y < 16 + AqHalf;
            if (inX && inY) return true;
            if (e && x >= 16 && inY) return true;
            if (w && x < 16 && inY) return true;
            if (n && y >= 16 && inX) return true;
            if (s && y < 16 && inX) return true;
            return false;
        }
        bool In(int x, int y) => x < 0 ? w && Inside(0, y) : x > 31 ? e && Inside(31, y) : y < 0 ? s && Inside(x, 0) : y > 31 ? n && Inside(x, 31) : Inside(x, y);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                if (!Inside(x, y)) continue;
                bool edge = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1);
                bool rim = !In(x - 2, y) || !In(x + 2, y) || !In(x, y - 2) || !In(x, y + 2);
                Color32 col = edge ? stone[1] : rim ? stone[3] : P.Water[2];
                if (!edge && !rim && (x * 5 + y * 3) % 23 == 0) col = P.Water[3];
                c.Put(x, y + AqLift, col);
            }
        // The channel's front lip, and a shadow line under it on the arcade.
        for (int x = 0; x < 32; x++)
        {
            if (!Inside(x, 16 - AqHalf) || In(x, 16 - AqHalf - 1)) continue;
            c.Put(x, top - 1, stone[2]);
            c.Put(x, top - 2, stone[1]);
        }
        return c;
    }
}
