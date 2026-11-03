using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Arcane Age buildings in pixel art: pale moonstone and gold, violet runes, blue mana crystals that
/// float and pulse, spires, domes and standing stones. Magic, rediscovered and engineered: steel bands,
/// conduits that carry mana like power lines, and crystals held in metal emitters.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> ArcaneBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "SpireOfLights", (PxSpireOfLights, 4, 3f) }, { "ArcaneTreasury", (PxArcaneTreasury, 4, 4f) }, { "SpellbladeHall", (PxSpellbladeHall, 4, 4f) },
            { "ArcaneAcademy", (PxArcaneAcademy, 4, 6f) }, { "GriffinRoost", (PxGriffinRoost, 4, 5f) }, { "TempleOfLight", (PxTempleOfLight, 4, 4f) },
            { "ArcaneObelisk", (PxArcaneObelisk, 4, 6f) }, { "StarfallSpire", (PxStarfallSpire, 4, 4f) }, { "FireballSpire", (PxFireballSpire, 4, 6f) },
            { "Runeforge", (PxRuneforge, 4, 6f) }, { "AnimatedWorkshop", (PxAnimatedWorkshop, 4, 5f) }, { "DrakePens", (PxDrakePens, 4, 4f) },
            { "GolemSanctum", (PxGolemSanctum, 4, 4f) }, { "SkyshipDock", (PxSkyshipDock, 4, 4f) }, { "LeylineNexus", (PxLeylineNexus, 4, 6f) },
            { "ManastoneMine", (PxManastoneMine, 4, 5f) }, { "ManaWell", (PxManaWell, 4, 6f) },
        };

    private static Color32[] Moon => P.Moonstone;

    /// <summary>A block of moonstone courses with gold trim at the top and a rune glinting here and there.</summary>
    private static void MoonWall(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int row = (y - y0) / 4;
                bool joint = (y - y0) % 4 == 0 || ((x - x0 + (row % 2) * 5) % 10) == 0;
                k.Px(x, y, joint ? Moon[1] : x < x0 + 2 ? Moon[3] : Moon[2]);
            }
        k.HLine(x0, x1, y1, P.Gold[2]);
        k.HLine(x0, x1, y1 - 1, Moon[3]);
        for (int x = x0 + 6; x < x1 - 2; x += 13) k.Px(x, (y0 + y1) / 2, (x + k.Frame) % 2 == 0 ? P.Arcane[3] : P.Arcane[2]);
        Conduit(k, x0, x1, y0 + 1);
    }

    /// <summary>A steel band with a mana conduit in it, a pulse of light running along.</summary>
    private static void Conduit(K k, int x0, int x1, int y)
    {
        k.HLine(x0, x1, y, P.Steel[1]);
        k.HLine(x0, x1, y + 1, P.Mana[1]);
        k.HLine(x0, x1, y + 2, P.Steel[2]);
        int span = Mathf.Max(1, x1 - x0);
        int pulse = x0 + (k.Frame * span / 4 + span / 8) % span;
        k.Px(pulse, y + 1, P.Mana[3]); k.Px(Mathf.Min(x1, pulse + 1), y + 1, P.Mana[2]);
        for (int x = x0 + 3; x < x1; x += 8) k.Px(x, y + 2, P.Steel[3]);   // rivets
    }

    /// <summary>A pointed slate-violet roof.</summary>
    private static void Cone(K k, int x0, int x1, int yb, int h)
    {
        float mid = (x0 + x1) / 2f;
        for (int y = 0; y <= h; y++)
        {
            float half = (x1 - x0) / 2f * (1f - y / (float)h);
            for (int x = Mathf.RoundToInt(mid - half); x <= Mathf.RoundToInt(mid + half); x++)
                k.Px(x, yb + y, x < mid - half * 0.4f ? P.Arcane[2] : (x + y) % 4 == 0 ? P.Arcane[0] : P.Arcane[1]);
        }
        k.Px(Mathf.RoundToInt(mid), yb + h + 1, P.Gold[3]);
    }

    /// <summary>A mana crystal hanging in the air, bobbing and pulsing.</summary>
    private static void FloatingCrystal(K k, int x, int y, int size, Color32[] ramp = null)
    {
        ramp ??= P.Mana;
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        // a steel emitter cradle under it, a thin beam holding the crystal up
        k.Line(x - size, y - size - 3, x - size / 2, y - size - 1, P.Steel[2]);
        k.Line(x + size, y - size - 3, x + size / 2, y - size - 1, P.Steel[2]);
        k.HLine(x - size, x + size, y - size - 3, P.Steel[1]);
        if (k.Frame % 2 == 0) k.VLine(x, y - size - 2, y - size - 1, ramp[3]);
        y += bob;
        k.Polygon(new[] { new Vector2(x, y - size), new Vector2(x + size * 0.6f, y), new Vector2(x, y + size), new Vector2(x - size * 0.6f, y) },
            l => l.x < x ? ramp[3] : ramp[2]);
        k.Px(x, y - size, ramp[1]);
        if (k.Frame % 2 == 0) { k.Px(x - size, y + 1, ramp[3]); k.Px(x + size, y - 1, ramp[3]); }
    }

    // ---------- Housing and money ----------

    private static void PxSpireOfLights(K k)
    {
        // A slender moonstone tower of lit floors, a violet cone roof and a crystal floating over it.
        k.Ground(2, 2, 62, 6);
        int floors = 8 + (k.Level >= 1 ? 1 : 0), top = 4 + floors * 7;
        MoonWall(k, 12, 4, 52, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 4; i++) if ((f + i * 3) % 5 != 0) k.Rect(16 + i * 9, 6 + f * 7, 19 + i * 9, 9 + f * 7, (f + i + k.Frame) % 7 == 0 ? P.Mana[3] : P.Glow[2]);
        k.Rect(8, top, 56, top + 2, P.Gold[1]);
        Cone(k, 14, 50, top + 3, 14);
        k.VLine(46, top + 3, top + 22, P.Steel[2]); k.HLine(43, 49, top + 16, P.Steel[1]);
        k.Px(46, top + 23, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);
        FloatingCrystal(k, 32, top + 24, 4);
        k.Door(28, 4, 8, 9, arched: true);
        k.Finish();
    }

    private static void PxArcaneTreasury(K k)
    {
        // A domed vault of moonstone; enchanted coins drift in a slow ring above it.
        k.Ground(2, 2, 62, 6);
        MoonWall(k, 6, 4, 58, 30);
        k.Ellipse(32, 30, 22, 14, new[] { P.Gold[0], P.Gold[1], P.Gold[2], P.Gold[3] }, l => l.y >= 30);
        k.Door(27, 4, 10, 12, arched: true);
        for (int i = 0; i < 5; i++) k.Rect(9 + i * 2, 5, 9 + i * 2, 18, Moon[3]);
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 3 + k.Level; i++)
        {
            float t = a + i * 1.4f;
            k.Ellipse(32 + Mathf.Cos(t) * 20, 50 + Mathf.Sin(t) * 4, 1.6f, 1.6f, P.Gold);
        }
        k.Finish();
    }

    // ---------- Military ----------

    private static void PxSpellbladeHall(K k)
    {
        // A long hall with tall arched windows, a great runeblade set upright before its door.
        k.Ground(2, 2, 94, 30);
        MoonWall(k, 6, 6, 70, 30);
        for (int i = 0; i < 4; i++) { k.Rect(12 + i * 15, 14, 16 + i * 15, 24, P.Arcane[1]); k.Px(14 + i * 15, 25, P.Arcane[1]); }
        Cone(k, 4, 72, 31, 10);
        k.Door(34, 6, 8, 11, arched: true);
        // the blade, runes running up it
        k.Rect(82, 6, 83, 36, P.Steel[3]); k.HLine(78, 87, 10, P.Gold[2]);
        for (int y = 14; y < 34; y += 4) k.Px(82, y, (y / 4 + k.Frame) % 2 == 0 ? P.Arcane[3] : P.Arcane[2]);
        k.Banner(70, 30, 12);
        k.Finish();
    }

    private static void PxArcaneAcademy(K k)
    {
        // A round wizards' tower over a practice yard; mana bolts streak at enchanted targets.
        k.Ground(2, 2, 94, 80, P.Dirt);
        MoonWall(k, 10, 50, 40, 74);
        Cone(k, 6, 44, 75, 16);
        for (int i = 0; i < 3; i++) k.Rect(16 + i * 8, 60, 18 + i * 8, 66, P.Glow[2]);
        k.Door(22, 50, 7, 9, arched: true);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + (i % 2) * 18, y = 18 + (i / 2) * 24;
            int hover = (k.Frame + i) % 4 < 2 ? 0 : 1;
            k.Ellipse(x, y + hover, 5, 4, P.Steel);
            k.Ellipse(x + 1, y + hover, 2, 2, P.Mana);
            k.HLine(x - 7, x - 4, y + 3 + hover, P.Steel[1]); k.HLine(x + 4, x + 7, y + 3 + hover, P.Steel[1]);
            k.Px(x, y - 6 + hover, P.Mana[3]); k.Px(x, y - 8, P.Mana[1]);   // the lift glow under it
        }
        int f = k.Frame;
        k.Line(36 + f * 4, 46 - f * 6, 40 + f * 4, 44 - f * 6, P.Mana[3], 2);
        k.Finish();
    }

    private static void PxGriffinRoost(K k)
    {
        // A tall stone perch with straw nests on its ledges; a griffin beats its wings on top.
        k.Ground(2, 2, 94, 30);
        MoonWall(k, 30, 4, 60, 40);
        k.Rect(24, 40, 66, 43, Moon[1]);
        for (int i = 0; i < 2; i++) { int x = 10 + i * 66; k.Ellipse(x + 4, 12, 8, 3, P.Straw); k.Ellipse(x + 4, 13, 5, 1.5f, P.Straw); }
        k.Door(41, 4, 8, 10, arched: true);
        // the griffin on the perch: body, white head, wings up and down with the frame
        int up = k.Frame % 2 == 0 ? 8 : -2;
        k.Ellipse(45, 49, 8, 4, P.Griffin);
        k.Ellipse(53, 53, 3, 3, P.Linen);
        k.Polygon(new[] { new Vector2(55, 53), new Vector2(58, 52), new Vector2(55, 51) }, _ => P.Gold[2]);
        k.Polygon(new[] { new Vector2(44, 51), new Vector2(30, 51 + up), new Vector2(38, 49) }, _ => P.Linen[2]);
        k.Finish();
    }

    private static void PxTempleOfLight(K k)
    {
        // A white temple with a portico of pillars and a golden sun disc that slowly turns.
        k.Ground(4, 2, 92, 26, P.Dirt);
        k.Rect(6, 4, 82, 7, Moon[3]);
        for (int i = 0; i < 7; i++) { int x = 10 + i * 11; k.Rect(x, 8, x + 3, 28, Moon[3]); k.VLine(x, 8, 28, Moon[2]); }
        k.Rect(6, 29, 82, 32, Moon[2]);
        k.Polygon(new[] { new Vector2(4, 33), new Vector2(84, 33), new Vector2(44, 46) }, l => l.y < 36 ? (Color32?)Moon[2] : Moon[3]);
        float a = k.T * Mathf.PI * 2f;
        k.Ellipse(44, 54, 6, 6, P.Gold);
        for (int i = 0; i < 8; i++)
        {
            float t = a * 0.25f + i * Mathf.PI / 4f;
            k.Line(44 + Mathf.Cos(t) * 7, 54 + Mathf.Sin(t) * 7, 44 + Mathf.Cos(t) * 10, 54 + Mathf.Sin(t) * 10, P.Gold[3]);
        }
        k.Finish();
    }

    // ---------- Towers (crewless: magic does the shooting) ----------

    private static void PxArcaneObelisk(K k)
    {
        // A tapering obelisk carved with runes, a crystal spinning over its tip that throws mana bolts.
        k.Ground(10, 2, 54, 8);
        k.Rect(18, 2, 46, 7, Moon[1]); k.HLine(18, 46, 7, Moon[3]);
        for (int y = 8; y < 44; y++)
        {
            int half = 9 - (y - 8) / 6;
            for (int x = 32 - half; x <= 32 + half; x++) k.Px(x, y, x < 32 - half + 2 ? Moon[3] : Moon[2]);
        }
        for (int y = 12; y < 40; y += 5) k.Px(32, y, (y / 5 + k.Frame) % 2 == 0 ? P.Arcane[3] : P.Arcane[1]);
        foreach (int y in new[] { 16, 30 }) { int half = 10 - (y - 8) / 6; k.HLine(32 - half, 32 + half, y, P.Steel[2]); k.HLine(32 - half, 32 + half, y + 1, P.Steel[1]); }
        FloatingCrystal(k, 32, 50, 4);
        k.Finish();
    }

    private static void PxStarfallSpire(K k)
    {
        // A needle of a spire with a ring around its tip and a star circling it; it calls down falling stars.
        k.Ground(10, 2, 54, 8);
        MoonWall(k, 24, 2, 40, 20);
        for (int y = 21; y < 54; y++) { int half = Mathf.Max(1, 5 - (y - 21) / 7); k.HLine(32 - half, 32 + half, y, y % 6 == 0 ? P.Gold[2] : Moon[2]); }
        k.Ellipse(32, 54, 9, 2, new[] { P.Gold[0], P.Gold[1], P.Gold[2], P.Gold[3] }, l => l.y <= 54 || Mathf.Abs(l.x - 32) > 6);
        float a = k.T * Mathf.PI * 2f;
        int sx = Mathf.RoundToInt(32 + Mathf.Cos(a) * 9), sy = Mathf.RoundToInt(56 + Mathf.Sin(a) * 2);
        k.Px(sx, sy, P.White); k.Px(sx + 1, sy, P.Gold[3]); k.Px(sx, sy + 1, P.Gold[3]);
        k.Door(28, 3, 8, 9, arched: true);
        k.Finish();
    }

    private static void PxFireballSpire(K k)
    {
        // A squat tower crowned with a great brazier; a fireball gathers in it before each volley.
        k.Ground(2, 2, 62, 8);
        MoonWall(k, 16, 2, 48, 26);
        k.Rect(12, 27, 52, 30, Moon[1]);
        k.Ellipse(32, 32, 14, 5, new[] { P.Iron[0], P.Iron[1], P.Iron[2], P.Iron[3] }, l => l.y <= 32);
        int flare = k.Frame % 2 == 0 ? 2 : 0;
        k.Ellipse(32, 36 + flare / 2, 8 + flare, 6 + flare, P.Fire);
        k.Ellipse(32, 36, 4, 3, new[] { P.Fire[2], P.Fire[3], P.White, P.White });
        k.Door(28, 3, 8, 9, arched: true);
        k.Finish();
    }

    // ---------- Industry ----------

    private static void PxRuneforge(K k)
    {
        // A forge hall where runes are hammered into steel; a glowing anvil and violet sparks.
        k.Ground(2, 2, 94, 40, P.Dirt);
        MoonWall(k, 6, 8, 66, 30);
        Cone(k, 4, 68, 31, 9);
        k.Rect(74, 6, 88, 12, P.Iron[1]); k.Rect(78, 12, 84, 15, P.Iron[2]);
        k.HLine(76, 86, 16, P.Arcane[k.Frame % 2 == 0 ? 3 : 2]);
        for (int i = 0; i < 5; i++) k.Px(80 + k.R(-6, 6), 18 + k.R(0, 8), (i + k.Frame) % 2 == 0 ? P.Arcane[3] : P.Fire[3]);
        k.Door(30, 8, 10, 12, arched: true);
        k.Finish(60, 40);
    }

    private static void PxAnimatedWorkshop(K k)
    {
        // A workshop where hammers and saws work on their own, floating over the benches.
        k.Ground(2, 2, 94, 40, P.Dirt);
        MoonWall(k, 6, 8, 80, 28);
        k.Door(14, 8, 9, 11, arched: true);
        for (int i = 0; i < 3; i++)
        {
            int x = 36 + i * 16, y = 30 + ((k.Frame + i) % 4 < 2 ? 2 : 6);
            k.Rect(x, y, x + 1, y + 6, P.Wood[2]); k.Rect(x - 2, y + 6, x + 3, y + 8, P.Steel[2]);
            k.Px(x + 4, y + 3, P.Arcane[3]);
        }
        k.HLine(6, 90, 5, Moon[1]);
        for (int i = 0; i < 3; i++) k.Crate(8 + ((i * 28 + k.Frame * 4) % 80), 6);
        k.Finish();
    }

    private static void PxDrakePens(K k)
    {
        // Moonstone pens round a great barred gate; a drake's horned head looks out over it, smoke curling from its nose.
        k.Ground(2, 2, 94, 40, P.Dirt);
        MoonWall(k, 4, 8, 64, 30);
        k.Rect(18, 9, 50, 26, P.Coal[1]);
        for (int x = 20; x < 50; x += 5) k.VLine(x, 9, 26, P.Steel[2]);
        k.Rect(16, 26, 52, 28, P.Gold[2]);
        var d = P.Drake;
        int nod = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(34, 33 + nod, 6, 4, d);
        k.Polygon(new[] { new Vector2(38, 34 + nod), new Vector2(46, 33 + nod), new Vector2(38, 31 + nod) }, _ => d[2]);
        k.Line(31, 36 + nod, 27, 41 + nod, P.Bone[2], 2); k.Line(35, 36 + nod, 33, 42 + nod, P.Bone[3], 2);
        k.Px(37, 34 + nod, P.Glow[3]);
        if (k.Frame % 2 == 0) { k.Px(47, 35 + nod, P.Coal[2]); k.Px(49, 37, P.Coal[1]); }
        // a fenced yard of straw and a trough
        k.HLine(68, 92, 18, P.Wood[2]); k.VLine(68, 4, 18, P.Wood[1]); k.VLine(92, 4, 18, P.Wood[1]);
        k.Ellipse(80, 8, 9, 3, P.Straw);
        k.Rect(72, 4, 86, 6, P.Iron[1]);
        k.Finish();
    }

    private static void PxGolemSanctum(K k)
    {
        // A ring of standing stones open to the sky, a stone golem waking in the middle.
        k.Ground(4, 2, 60, 12, P.Dirt);
        for (int i = 0; i < 4; i++) { int x = 8 + i * 15; k.Rect(x, 4, x + 6, 26, Moon[2]); k.VLine(x, 4, 26, Moon[3]); k.Px(x + 3, 18, P.Arcane[(i + k.Frame) % 2 == 0 ? 3 : 2]); }
        k.Rect(6, 27, 58, 30, Moon[1]);
        k.Ellipse(32, 14, 6, 8, P.Rock);
        k.Ellipse(32, 26, 4, 4, P.Rock);
        k.HLine(31, 34, 27, P.Arcane[k.Frame % 2 == 0 ? 3 : 1]);
        k.Finish();
    }

    private static void PxSkyshipDock(K k)
    {
        // A tall mooring tower on a paved apron; an arcane skyship rides at anchor, its crystal sail aglow.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 30; y < 40; y++) k.Px(x, y, (x + y) % 9 == 0 ? Moon[1] : Moon[2]);
        MoonWall(k, 10, 40, 24, 68);
        Cone(k, 8, 26, 69, 8);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Line(24, 64, 40, 58 + bob, P.Linen[1]);
        k.Ellipse(62, 54 + bob, 20, 6, P.Wood, l => l.y <= 54 + bob);
        k.HLine(42, 82, 54 + bob, P.Gold[2]);
        k.HLine(46, 78, 51 + bob, P.Steel[2]);
        k.Rect(80, 48 + bob, 85, 52 + bob, P.Steel[1]);
        k.Rect(86, 49 + bob, 87 + (k.Frame % 2), 51 + bob, P.Mana[3]);
        k.Polygon(new[] { new Vector2(62, 56 + bob), new Vector2(62, 76 + bob), new Vector2(76, 58 + bob) }, _ => P.Mana[2]);
        k.VLine(62, 54 + bob, 78 + bob, P.Wood[1]);
        k.Finish();
    }

    private static void PxLeylineNexus(K k)
    {
        // Standing stones round a pool where three leylines meet; light runs along the lines into the stones.
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Ellipse(48, 18, 16, 6, P.Mana);
        int run = k.Frame * 8;
        foreach (var (x, y) in new[] { (6, 4), (90, 6), (48, 38) })
        {
            k.Line(x, y, 48, 18, P.Arcane[1]);
            float t = (run % 32) / 32f;
            k.Px(Mathf.RoundToInt(Mathf.Lerp(x, 48, t)), Mathf.RoundToInt(Mathf.Lerp(y, 18, t)), P.Arcane[3]);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            int x = Mathf.RoundToInt(48 + Mathf.Cos(a) * 28), y = Mathf.RoundToInt(18 + Mathf.Sin(a) * 10);
            k.Rect(x - 2, y, x + 2, y + 12, Moon[2]); k.VLine(x - 2, y, y + 12, Moon[3]);
            k.Px(x, y + 8, P.Mana[(i + k.Frame) % 2 == 0 ? 3 : 2]);
        }
        k.Rect(78, 22, 90, 32, P.Steel[1]); k.Rect(79, 23, 89, 31, P.Steel[2]);
        for (int i = 0; i < 3; i++) k.HLine(81, 87, 25 + i * 2, P.Steel[1]);
        k.Px(88, 30, k.Frame % 2 == 0 ? P.Mana[3] : P.Mana[1]);
        k.Line(64, 18, 78, 26, P.Coal[1]);
        FloatingCrystal(k, 48, 30, 5);
        k.Finish();
    }

    private static void PxManastoneMine(K k)
    {
        // An open pit glittering with blue crystal; a winch lifts baskets of manastone.
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Pit(10, 20, 60, 56);
        for (int i = 0; i < 6; i++)
        {
            int x = 18 + (i * 13) % 40, y = 26 + (i * 7) % 24;
            k.Polygon(new[] { new Vector2(x, y), new Vector2(x + 2, y + 6), new Vector2(x + 4, y) }, _ => P.Mana[2]);
            k.Px(x + 2, y + 1, (i + k.Frame) % 3 == 0 ? P.White : P.Mana[3]);
        }
        k.VLine(70, 20, 48, P.Wood[1]); k.VLine(84, 20, 48, P.Wood[1]); k.HLine(68, 86, 48, P.Wood[2]);
        int lift = (k.Frame % 4) * 4;
        k.VLine(77, 26 + lift, 47, P.Linen[1]); k.Rect(74, 22 + lift, 80, 26 + lift, P.Wood[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, P.Mana);
        k.Finish();
    }

    private static void PxManaWell(K k)
    {
        // A round well of moonstone where manastone is dissolved into glowing mana, drawn up into crystal jars.
        k.Ground(2, 2, 94, 40, P.Dirt);
        MoonWall(k, 6, 6, 50, 26);
        Cone(k, 4, 52, 27, 10);
        k.Door(22, 6, 9, 11, arched: true);
        k.Ellipse(74, 12, 14, 6, Moon);
        k.Ellipse(74, 14, 10, 3.5f, P.Mana);
        int bubble = k.Frame;
        k.Px(70 + bubble * 2, 15, P.White); k.Px(78 - bubble, 14, P.Mana[3]);
        k.VLine(62, 12, 34, P.Wood[1]); k.VLine(86, 12, 34, P.Wood[1]); k.HLine(60, 88, 34, P.Wood[2]);
        for (int i = 0; i < 3; i++) { int x = 56 + i * 6; k.Rect(x, 4, x + 3, 9, P.Mana[2]); k.HLine(x, x + 3, 10, P.Steel[2]); }
        k.HLine(50, 62, 16, P.Steel[2]); k.HLine(50, 62, 17, P.Steel[1]);
        k.Ellipse(56, 21, 2.5f, 2.5f, P.Steel); k.Px(56 + (k.Frame % 2), 22, P.Red[3]);
        k.Finish();
    }
}
