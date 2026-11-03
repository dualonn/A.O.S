using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Astral Age buildings in pixel art: dark star-steel panels seamed with gold, glass domes, rings that
/// float and turn, lenses and dishes aimed at the sky, mana-blue light and white starlight.
/// Magic and machine as one.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> AstralBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "StarArcology", (PxStarArcology, 4, 3f) }, { "StarExchange", (PxStarExchange, 4, 4f) }, { "StarknightHall", (PxStarknightHall, 4, 4f) },
            { "TechnomancyCollege", (PxTechnomancyCollege, 4, 5f) }, { "SkylancerHangar", (PxSkylancerHangar, 4, 5f) }, { "AstralSanctum", (PxAstralSanctum, 4, 4f) },
            { "AstralLance", (PxAstralLance, 4, 6f) }, { "MeteorArray", (PxMeteorArray, 4, 4f) }, { "NovaBattery", (PxNovaBattery, 4, 6f) },
            { "StarFoundry", (PxStarFoundry, 4, 6f) }, { "ConstructWorks", (PxConstructWorks, 4, 5f) }, { "ThroneWorks", (PxThroneWorks, 4, 4f) },
            { "ConstructVault", (PxConstructVault, 4, 4f) }, { "Starport", (PxStarport, 4, 4f) }, { "AstralReactor", (PxAstralReactor, 4, 6f) },
            { "MeteorMine", (PxMeteorMine, 4, 5f) }, { "CoreForge", (PxCoreForge, 4, 6f) },
            { "ColonyHub", (PxColonyHub, 4, 4f) },
        };

    private static Color32[] Star => P.Astral;

    /// <summary>A block of dark star-steel panels: gold seams, a gold cap, a mana line along the foot and a star glinting.</summary>
    private static void StarSteel(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int pu = (x - x0) % 12;
                k.Px(x, y, pu == 0 ? Star[1] : pu < 3 ? Star[3] : (y - y0) % 10 == 0 ? Star[1] : Star[2]);
            }
        for (int x = x0 + 12; x < x1; x += 24) k.VLine(x, y0 + 2, y1 - 2, P.Gold[1]);
        k.HLine(x0, x1, y1, P.Gold[2]);
        k.HLine(x0, x1, y1 - 1, Star[3]);
        k.HLine(x0, x1, y0 + 1, P.Mana[k.Frame % 2 == 0 ? 2 : 1]);
        for (int x = x0 + 7; x < x1 - 2; x += 17) k.Px(x, (y0 + y1) / 2 + ((x / 17 + k.Frame) % 3), (x + k.Frame) % 2 == 0 ? P.White : P.Mana[3]);
    }

    /// <summary>A row of lit glass windows in a star-steel wall.</summary>
    private static void StarWindows(K k, int x0, int x1, int y, int h, int step)
    {
        for (int x = x0; x + 3 <= x1; x += step)
        {
            k.Rect(x, y, x + 3, y + h, P.Mana[1]);
            k.Rect(x, y + h - 1, x + 1, y + h, P.Mana[3]);
        }
    }

    /// <summary>A glass dome: blue glass with a gold frame and a glint.</summary>
    private static void GlassDome(K k, int cx, int yb, int rx, int ry)
    {
        k.Ellipse(cx, yb, rx, ry, new[] { P.Mana[0], P.Mana[1], P.Mana[2], P.Mana[3] }, l => l.y >= yb);
        for (int i = -1; i <= 1; i++) k.Line(cx + i * rx * 0.5f, yb, cx + i * rx * 0.2f, yb + ry, P.Gold[2]);
        k.HLine(cx - rx, cx + rx, yb, P.Gold[2]);
        k.Px(cx - rx / 2, yb + ry * 2 / 3, P.White);
    }

    /// <summary>A gold ring floating round a point, turning: its front half bright, its back half dim.</summary>
    private static void FloatingRing(K k, float cx, float cy, float rx, float ry)
    {
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 48; i++)
        {
            float t = i / 48f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(cx + Mathf.Cos(t) * rx), y = Mathf.RoundToInt(cy + Mathf.Sin(t) * ry);
            bool front = Mathf.Sin(t) < 0;
            k.Px(x, y, front ? P.Gold[2] : P.Gold[0]);
        }
        int lx = Mathf.RoundToInt(cx + Mathf.Cos(a) * rx), ly = Mathf.RoundToInt(cy + Mathf.Sin(a) * ry);
        k.Px(lx, ly, P.White);
    }

    // ---------- Housing and money ----------

    private static void PxStarArcology(K k)
    {
        // A tall tower of star-steel and glass, rings of garden floating round it, a lens at its tip.
        k.Ground(2, 2, 62, 6);
        int floors = 8 + (k.Level >= 1 ? 1 : 0) + (k.Level >= 2 ? 1 : 0), top = 4 + floors * 7;
        StarSteel(k, 16, 4, 48, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 3; i++) k.Rect(20 + i * 9, 6 + f * 7, 23 + i * 9, 9 + f * 7, (f + i + k.Frame) % 6 == 0 ? P.White : P.Mana[2]);
        for (int r = 0; r < 2; r++)
        {
            int ry = 20 + r * 26;
            k.Ellipse(32, ry, 26, 3, P.Leaf, l => l.y <= ry);   // a garden ring
            FloatingRing(k, 32, ry, 27, 3.5f);
        }
        k.Polygon(new[] { new Vector2(18, top + 1), new Vector2(46, top + 1), new Vector2(32, top + 12) }, l => l.x < 28 ? (Color32?)Star[3] : Star[2]);
        k.VLine(32, top + 12, top + 18, P.Steel[2]);
        k.Ellipse(32, top + 20, 2.5f, 2.5f, new[] { P.Gold[2], P.White, P.White, P.White });
        k.Door(28, 4, 8, 8);
        k.Finish();
    }

    private static void PxStarExchange(K k)
    {
        // A star-steel hall under a glass dome; a star-map of prices turns above it.
        k.Ground(2, 2, 62, 6);
        StarSteel(k, 6, 4, 58, 28);
        StarWindows(k, 10, 54, 14, 6, 9);
        GlassDome(k, 32, 28, 20, 12);
        k.Door(27, 4, 10, 9);
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 4 + k.Level; i++)
        {
            float t = a * 0.5f + i * 1.3f;
            int x = Mathf.RoundToInt(32 + Mathf.Cos(t) * 22), y = Mathf.RoundToInt(50 + Mathf.Sin(t) * 4);
            k.Px(x, y, i % 2 == 0 ? P.Gold[3] : P.Mana[3]); k.Px(x + 1, y, P.Gold[2]);
        }
        k.Finish();
    }

    // ---------- Military ----------

    private static void PxStarknightHall(K k)
    {
        // A long star-steel hall, its door flanked by two upright blades of starlight.
        k.Ground(2, 2, 94, 30);
        StarSteel(k, 6, 6, 72, 30);
        StarWindows(k, 12, 66, 16, 8, 12);
        k.Rect(4, 31, 74, 34, Star[1]); k.HLine(4, 74, 34, P.Gold[2]);
        k.Door(34, 6, 9, 11);
        foreach (int x in new[] { 80, 88 })
        {
            k.Rect(x - 1, 6, x + 1, 9, P.Gold[2]);
            k.Rect(x, 10, x + 1, 32, (x + k.Frame) % 2 == 0 ? P.Arcane[3] : P.Arcane[2]);
            k.VLine(x, 10, 30, P.White);
        }
        k.Banner(72, 34, 12);
        k.Finish();
    }

    private static void PxTechnomancyCollege(K k)
    {
        // A glass-and-steel tower, spells running down its sides as light; target drones over the yard.
        k.Ground(2, 2, 94, 80, P.Dirt);
        StarSteel(k, 10, 50, 40, 76);
        for (int y = 54; y < 74; y++) if ((y + k.Frame * 3) % 9 < 2) { k.Px(13, y, P.Mana[3]); k.Px(37, y, P.Mana[3]); }
        StarWindows(k, 16, 36, 60, 6, 8);
        GlassDome(k, 25, 76, 15, 9);
        k.Door(22, 50, 7, 8);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + (i % 2) * 18, y = 18 + (i / 2) * 24, hover = (k.Frame + i) % 4 < 2 ? 0 : 1;
            k.Ellipse(x, y + hover, 5, 4, Star);
            k.Ellipse(x + 1, y + hover, 2, 2, P.Gold);
            k.HLine(x - 7, x - 4, y + 3 + hover, Star[1]); k.HLine(x + 4, x + 7, y + 3 + hover, Star[1]);
            k.Px(x, y - 6 + hover, P.Mana[3]);
        }
        int f = k.Frame;
        k.Line(36 + f * 4, 46 - f * 6, 40 + f * 4, 44 - f * 6, P.Gold[3], 2);
        k.Finish();
    }

    private static void PxSkylancerHangar(K k)
    {
        // A curved hangar with a launch rail on its roof; a jetbike shoots off the end of the rail.
        k.Ground(2, 2, 94, 30);
        StarSteel(k, 8, 4, 80, 24);
        k.Ellipse(44, 24, 36, 10, Star, l => l.y >= 24);
        k.Rect(24, 4, 64, 18, P.Coal[1]);
        for (int x = 26; x < 64; x += 6) k.VLine(x, 4, 18, Star[1]);
        k.Line(14, 34, 92, 42, P.Steel[2], 2);
        int t = (k.Frame * 14) % 56;
        int bx = 30 + t, by = 37 + t / 7;
        k.Rect(bx - 6, by, bx + 6, by + 3, Star[2]); k.HLine(bx - 6, bx + 6, by + 3, P.Gold[2]);
        k.Ellipse(bx - 8, by + 1.5f, 2, 1.2f, P.Mana);
        k.Finish();
    }

    private static void PxAstralSanctum(K k)
    {
        // A white dome on star-steel, a great lens at its crown focusing a shaft of starlight.
        k.Ground(4, 2, 92, 26, P.Dirt);
        StarSteel(k, 8, 4, 80, 22);
        k.Ellipse(44, 22, 30, 18, new[] { P.Moonstone[1], P.Moonstone[2], P.Moonstone[3], P.White }, l => l.y >= 22);
        k.HLine(14, 74, 22, P.Gold[2]);
        k.Ellipse(44, 42, 6, 2, new[] { P.Gold[1], P.Gold[2], P.Mana[3], P.White });
        if (k.Frame % 2 == 0) for (int y = 44; y < 62; y += 2) k.Px(44, y, y % 4 == 0 ? P.White : P.Gold[3]);
        k.Ellipse(44, 62, 3, 3, new[] { P.Gold[2], P.Gold[3], P.White, P.White });
        k.Door(39, 4, 10, 10);
        k.Finish();
    }

    // ---------- Towers (crewless: the machines do the shooting) ----------

    private static void PxAstralLance(K k)
    {
        // A needle of star-steel on a braced base, a gold lens at its tip that fires bolts of starlight.
        k.Ground(10, 2, 54, 8);
        StarSteel(k, 18, 2, 46, 12);
        for (int y = 13; y < 48; y++) { int half = Mathf.Max(2, 7 - (y - 13) / 6); k.HLine(32 - half, 32 + half, y, y % 7 == 0 ? P.Gold[2] : (y < 30 ? Star[2] : Star[3])); }
        k.Line(20, 12, 28, 30, Star[1]); k.Line(44, 12, 36, 30, Star[1]);
        for (int y = 16; y < 46; y += 6) k.Px(32, y, (y / 6 + k.Frame) % 2 == 0 ? P.Mana[3] : P.Mana[1]);
        k.Ellipse(32, 52, 4, 4, P.Gold);
        k.Ellipse(32, 52, 2, 2, k.Frame % 2 == 0 ? new[] { P.White, P.White, P.White, P.White } : P.Mana);
        k.Finish();
    }

    private static void PxMeteorArray(K k)
    {
        // A great dish of star-steel on a turret, aimed at the sky, a star glowing at its focus.
        k.Ground(10, 2, 54, 8);
        StarSteel(k, 22, 2, 42, 20);
        k.Rect(29, 20, 35, 30, Star[1]);
        int tilt = k.Frame % 4 < 2 ? 0 : 1;
        k.Polygon(new[] { new Vector2(12, 34 + tilt), new Vector2(52, 44 - tilt), new Vector2(44, 52 - tilt), new Vector2(20, 46 + tilt) },
            l => l.y < 40 ? (Color32?)Star[1] : Star[3]);
        k.Line(32, 42, 38, 58, P.Steel[2]);
        k.Ellipse(38, 59, 2.4f, 2.4f, new[] { P.Gold[2], P.Gold[3], P.White, P.White });
        if (k.Frame % 2 == 0) { k.Px(34, 62, P.Gold[3]); k.Px(42, 61, P.Gold[3]); }
        k.Door(28, 3, 8, 8);
        k.Finish();
    }

    private static void PxNovaBattery(K k)
    {
        // A squat star-steel tower with a little sun caught in a cage on its top.
        k.Ground(2, 2, 62, 8);
        StarSteel(k, 14, 2, 50, 24);
        k.Rect(10, 25, 54, 28, Star[1]); k.HLine(10, 54, 28, P.Gold[2]);
        int pulse = k.Frame % 2 == 0 ? 2 : 0;
        k.Ellipse(32, 38, 7 + pulse, 7 + pulse, new[] { P.Fire[2], P.Gold[2], P.Gold[3], P.White });
        k.Ellipse(32, 38, 3, 3, new[] { P.White, P.White, P.White, P.White });
        foreach (int x in new[] { 22, 27, 37, 42 }) k.VLine(x, 29, 47, Star[3]);
        k.HLine(20, 44, 48, Star[3]);
        k.Door(28, 3, 8, 8);
        k.Finish();
    }

    // ---------- Industry ----------

    private static void PxStarFoundry(K k)
    {
        // A star-steel foundry; a crucible of molten starmetal tips, gold-white sparks flying.
        k.Ground(2, 2, 94, 40, P.Dirt);
        StarSteel(k, 6, 8, 66, 32);
        StarWindows(k, 44, 62, 18, 6, 7);
        k.Rect(14, 32, 20, 40, Star[1]); k.Rect(26, 32, 32, 38, Star[1]);
        if (k.Frame % 2 == 0) k.Ellipse(17, 42, 3, 2, P.Mana);
        k.Door(28, 8, 10, 11);
        int tip = k.Frame % 4;
        k.Rect(74, 8, 88, 18, P.Iron[1]); k.Rect(76, 10, 86, 17, P.Starmetal[4]);
        k.Line(86, 17, 90, 10 - tip, P.Gold[3], 2);
        for (int i = 0; i < 5; i++) k.Px(80 + k.R(-6, 8), 20 + k.R(0, 8), (i + k.Frame) % 2 == 0 ? P.White : P.Gold[3]);
        k.Finish(60, 40);
    }

    private static void PxConstructWorks(K k)
    {
        // A glass-roofed works; star-steel constructs carry gears along a line.
        k.Ground(2, 2, 94, 40, P.Dirt);
        StarSteel(k, 6, 8, 84, 26);
        for (int x = 8; x < 84; x += 8) k.Polygon(new[] { new Vector2(x, 27), new Vector2(x + 8, 27), new Vector2(x + 8, 33) }, _ => P.Mana[2]);
        k.Door(14, 8, 9, 10);
        k.HLine(6, 90, 5, Star[1]);
        for (int i = 0; i < 3; i++)
        {
            int x = 34 + ((i * 20 + k.Frame * 5) % 56), step = (k.Frame + i) % 2;
            k.Rect(x, 9, x + 3, 16, P.Synth[2]); k.Ellipse(x + 1.5f, 18, 2, 2, P.Synth);
            k.Px(x + 2, 18, P.Mana[3]);
            k.VLine(x + step, 6, 8, P.Synth[1]); k.VLine(x + 3 - step, 6, 8, P.Synth[1]);
            k.Ellipse(x + 6, 14, 2, 2, P.Brass);
        }
        k.Finish();
    }

    private static void PxThroneWorks(K k)
    {
        // A star-steel hall with a gold launch ring before it; a throne-disc hovers in the ring, crystals circling.
        k.Ground(2, 2, 94, 40, P.Dirt);
        StarSteel(k, 4, 8, 50, 30);
        k.Door(18, 8, 14, 16);
        k.Ellipse(74, 8, 18, 4, new[] { P.Gold[1], P.Gold[2], Star[1], Star[1] });
        k.Ellipse(74, 8, 14, 2.6f, P.Mana);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(74, 18 + bob, 12, 3, Star); k.HLine(62, 86, 18 + bob, P.Gold[2]);
        k.Rect(69, 19 + bob, 79, 27 + bob, Star[2]); k.Rect(70, 26 + bob, 78, 29 + bob, Star[3]); k.HLine(69, 79, 22 + bob, P.Gold[2]);
        k.Line(80, 24 + bob, 90, 25 + bob, Star[1], 2); k.Ellipse(91, 25 + bob, 1.6f, 1.6f, P.Gold);
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 3; i++)
        {
            float t = a + i * 2.1f;
            k.Px(Mathf.RoundToInt(74 + Mathf.Cos(t) * 15), Mathf.RoundToInt(24 + bob + Mathf.Sin(t) * 3), P.Mana[3]);
        }
        k.Finish();
    }

    private static void PxConstructVault(K k)
    {
        // A sealed vault of star-steel; constructs stand waiting in its glowing alcoves.
        k.Ground(4, 2, 60, 12, P.Dirt);
        StarSteel(k, 6, 4, 58, 30);
        for (int i = 0; i < 3; i++)
        {
            int x = 12 + i * 16;
            k.Rect(x, 8, x + 9, 24, P.Coal[1]);
            k.Rect(x + 1, 9, x + 8, 9, (i + k.Frame) % 3 == 0 ? P.Mana[3] : P.Mana[1]);
            k.Rect(x + 3, 10, x + 6, 19, P.Synth[2]); k.Ellipse(x + 4.5f, 21, 2, 2, P.Synth);
            k.Px(x + 5, 21, P.Mana[3]);
        }
        k.Rect(4, 31, 60, 34, Star[1]); k.HLine(4, 60, 34, P.Gold[2]);
        k.Finish();
    }

    private static void PxStarport(K k)
    {
        // A landing pad with a control tower; a star frigate settles in its cradle, its drive glowing.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 30; y < 40; y++) k.Px(x, y, (x + y) % 9 == 0 ? Star[1] : Star[2]);
        k.Ellipse(56, 35, 20, 4, new[] { P.Gold[1], P.Gold[2], Star[2], Star[2] }, l => Mathf.Abs(l.x - 56) > 16 || Mathf.Abs(l.y - 35) > 2.5f);
        StarSteel(k, 8, 40, 22, 66);
        StarWindows(k, 10, 20, 60, 4, 6);
        GlassDome(k, 15, 66, 8, 5);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Polygon(new[] { new Vector2(40, 50 + bob), new Vector2(76, 50 + bob), new Vector2(84, 54 + bob), new Vector2(76, 58 + bob), new Vector2(40, 58 + bob) },
            l => l.y > 56 + bob ? (Color32?)Star[3] : Star[2]);
        k.HLine(42, 78, 54 + bob, P.Gold[2]);
        k.Polygon(new[] { new Vector2(48, 50 + bob), new Vector2(62, 50 + bob), new Vector2(42, 44 + bob) }, _ => Star[1]);
        k.Ellipse(38, 54 + bob, 2 + k.Frame % 2, 1.6f, P.Mana);
        k.Finish();
    }

    private static void PxAstralReactor(K k)
    {
        // A ring of star-steel turning round a captured leyline; light runs up into it from the ground.
        k.Ground(2, 2, 94, 40, P.Dirt);
        StarSteel(k, 8, 4, 30, 20);
        k.Rect(78, 6, 90, 18, Star[1]); for (int i = 0; i < 3; i++) k.HLine(80, 88, 9 + i * 3, Star[3]);
        k.Px(88, 16, k.Frame % 2 == 0 ? P.Mana[3] : P.Mana[1]);
        k.Rect(44, 4, 68, 8, Star[1]); k.HLine(44, 68, 8, P.Gold[2]);
        for (int y = 9; y < 40; y++) if ((y + k.Frame * 4) % 6 < 3) k.Px(56, y, P.Arcane[3]); else k.Px(56, y, P.Arcane[1]);
        FloatingRing(k, 56, 26, 14, 10);
        FloatingRing(k, 56, 26, 10, 14);
        k.Ellipse(56, 26, 3, 3, new[] { P.Arcane[2], P.Mana[3], P.White, P.White });
        k.Line(30, 12, 44, 6, P.Coal[1]); k.Line(68, 6, 78, 12, P.Coal[1]);
        k.Finish();
    }

    private static void PxMeteorMine(K k)
    {
        // A crater round a fallen meteorite; a star-steel crane cuts glowing slabs of starmetal from it.
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Pit(10, 20, 60, 56);
        k.Ellipse(36, 38, 14, 10, P.Starmetal);
        for (int i = 0; i < 6; i++) k.Px(26 + (i * 7) % 20, 32 + (i * 5) % 12, (i + k.Frame) % 3 == 0 ? P.White : P.Gold[3]);
        int cut = k.Frame % 4;
        k.Line(36, 48, 40 + cut, 40, P.Mana[3], 1);
        k.VLine(76, 20, 60, Star[1]); k.HLine(46, 82, 60, Star[2]); k.HLine(46, 82, 59, P.Gold[2]);
        int lift = cut * 3;
        k.VLine(52, 48 - lift, 59, P.Steel[2]); k.Rect(49, 44 - lift, 55, 47 - lift, P.Starmetal[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, P.Starmetal);
        k.Finish();
    }

    private static void PxColonyHub(K k)
    {
        // The landed colony ship opened into a hub: its hull on landing legs, a glass dome over the
        // hold, a ramp down to the ground, solar wings and a beacon blinking on a mast.
        k.Ground(2, 2, 94, 40, P.Dirt);
        foreach (int x in new[] { 18, 34, 62, 78 }) { k.Line(x, 4, x + (x < 48 ? 4 : -4), 12, Star[1], 2); k.HLine(x - 3, x + 3, 3, Star[2]); }
        k.Ellipse(48, 18, 36, 9, Star, l => l.y >= 12);
        StarSteel(k, 14, 12, 82, 24);
        k.Line(46, 4, 56, 12, P.Steel[2], 3);   // the ramp
        k.Rect(44, 12, 54, 20, P.Coal[1]);
        GlassDome(k, 48, 24, 18, 11);
        for (int i = 0; i < 3; i++) k.Px(40 + i * 6, 28 + (i % 2) * 2, P.Leaf[2]);   // gardens under the dome
        // solar wings either side
        for (int side = -1; side <= 1; side += 2)
        {
            int x0 = side < 0 ? 2 : 80, x1 = side < 0 ? 16 : 94;
            k.Rect(x0, 20, x1, 26, P.Mana[0]);
            for (int x = x0 + 2; x < x1; x += 3) k.VLine(x, 20, 26, P.Mana[1]);
            k.HLine(x0, x1, 26, P.Gold[2]);
        }
        k.VLine(70, 24, 44, P.Steel[2]);
        k.Px(70, 45, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);
        k.Px(71, 45, k.Frame % 2 == 0 ? P.Red[2] : P.Red[0]);
        k.Finish();
    }

    private static void PxCoreForge(K k)
    {
        // A star-steel forge where starmetal is shaped round a drop of mana; finished cores glow on a rack.
        k.Ground(2, 2, 94, 40, P.Dirt);
        StarSteel(k, 6, 6, 52, 28);
        GlassDome(k, 29, 28, 14, 8);
        k.Door(22, 6, 9, 10);
        k.Rect(62, 6, 88, 9, Star[1]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 66 + i * 6;
            k.Ellipse(x, 13, 2.6f, 2.6f, Star);
            k.Px(x, 13, (i + k.Frame) % 2 == 0 ? P.White : P.Mana[3]);
        }
        k.VLine(62, 9, 30, Star[2]); k.VLine(88, 9, 30, Star[2]); k.HLine(60, 90, 30, P.Gold[2]);
        k.Ellipse(75, 24, 3, 3, new[] { P.Mana[1], P.Mana[2], P.Mana[3], P.White });
        if (k.Frame % 2 == 0) { k.Px(71, 22, P.White); k.Px(79, 26, P.Mana[3]); }
        k.Finish();
    }
}
