using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Age of Enlightenment buildings in pixel art: Georgian brick and pale limestone, white sash windows,
/// pediments and slate, star-fort stonework, the first coke furnaces, and (the fantasy layer) copper
/// globes and Leyden jars crackling with galvanic sparks.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> EnlightenmentBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "GeorgianTownhouse", (PxGeorgianTownhouse, 4, 3f) }, { "RoyalBank", (PxRoyalBank, 4, 3f) },
            { "MusketeerBarracks", (PxMusketeerBarracks, 4, 4f) }, { "JagerRange", (PxJagerRange, 4, 4f) },
            { "HussarStables", (PxHussarStables, 4, 3f) }, { "SurgeonsHall", (PxSurgeonsHall, 4, 3f) },
            { "CasemateBattery", (PxCasemateBattery, 4, 5f) }, { "BlastFurnace", (PxBlastFurnace, 4, 6f) },
            { "ArtilleryPark", (PxArtilleryPark, 4, 4f) }, { "GalvanicLab", (PxGalvanicLab, 4, 6f) },
        };

    /// <summary>Warm red Georgian brick, cleaner than the sooty factory kind.</summary>
    private static readonly Color32[] GeorgianBrick = { P.Hex("4e231c"), P.Hex("86402e"), P.Hex("a85a40"), P.Hex("c9805e") };
    private static readonly Color32[] Limestone = { P.Hex("6e6250"), P.Hex("a39276"), P.Hex("d2c3a2"), P.Hex("efe4c8") };

    private static void GeorgianWall(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int row = (y - y0) / 3, bx = (x - x0 + (row % 2) * 3) % 6;
                k.Px(x, y, (y - y0) % 3 == 0 || bx == 0 ? GeorgianBrick[0] : ((x / 6 + row) % 3 == 0 ? GeorgianBrick[3] : GeorgianBrick[2]));
            }
    }

    /// <summary>A tall white-framed sash window with glazing bars.</summary>
    private static void Sash(K k, int x, int y, int w = 5, int h = 8)
    {
        k.Rect(x - 1, y - 1, x + w, y + h, P.Linen[3]);
        k.Rect(x, y, x + w - 1, y + h - 1, P.Water[1]);
        k.HLine(x, x + w - 1, y + h / 2, P.Linen[3]);
        k.VLine(x + w / 2, y, y + h - 1, P.Linen[3]);
        k.Px(x, y + h - 1, P.Water[3]);
    }

    private static void Pediment(K k, int x0, int x1, int y, int h, Color32[] ramp)
    {
        int mid = (x0 + x1) / 2;
        for (int yy = 0; yy <= h; yy++)
        {
            int half = (x1 - x0) / 2 * (h - yy) / h;
            for (int x = mid - half; x <= mid + half; x++) k.Px(x, y + yy, x == mid - half || x == mid + half ? ramp[0] : yy == 0 ? ramp[1] : ramp[2]);
        }
    }

    // ---------- Housing and money ----------

    private static void PxGeorgianTownhouse(K k)
    {
        // A tall, flat-fronted brick townhouse: white sash windows, a fanlight over the door, railings.
        k.Ground(2, 2, 62, 6);
        int floors = 3 + (k.Level >= 1 ? 1 : 0), top = 6 + floors * 10;
        GeorgianWall(k, 4, 4, 60, top);
        k.Rect(4, 4, 60, 6, Limestone[2]); k.HLine(4, 60, 6, Limestone[3]);
        k.Door(28, 7, 7, 11, arched: true);
        k.Ellipse(31.5f, 18, 3.5f, 1.6f, new[] { P.Linen[3], P.Water[2], P.Water[3], P.White }, l => l.y >= 18);   // fanlight
        for (int f = 0; f < floors; f++)
            for (int i = 0; i < 4; i++)
            {
                if (f == 0 && (i == 1 || i == 2)) continue;
                Sash(k, 8 + i * 13, 9 + f * 10, 5, 7);
            }
        k.HLine(4, 60, top, Limestone[3]);
        k.Roof(2, 62, top + 1, 8, K.RoofType.Slate, 6);
        k.Chimney(12, top + 3, 5, 10, GeorgianBrick); k.Chimney(48, top + 3, 5, 10, GeorgianBrick);
        for (int x = 6; x <= 58; x += 3) k.VLine(x, 3, 6, P.Coal[1]);   // area railings
        k.HLine(6, 58, 6, P.Coal[1]);
        if (k.Level >= 2) { k.FlowerBox(8, 17, 6); k.FlowerBox(48, 17, 6); }
        k.Finish();
        k.Smoke(14, top + 14, 0.4f, 100);
    }

    private static void PxRoyalBank(K k)
    {
        // A Palladian bank: limestone front, a giant portico of columns, a pediment with the royal arms.
        k.Rect(2, 2, 62, 4, Limestone[1]); k.Rect(4, 5, 60, 7, Limestone[2]);
        k.Wall(6, 8, 58, 34, K.Mat.Marble);
        for (int i = 0; i < 4; i++) k.Column(14 + i * 11, 8, 26);
        k.Door(28, 8, 8, 13, arched: true);
        Sash(k, 9, 18, 4, 8); Sash(k, 52, 18, 4, 8);
        k.Rect(4, 34, 60, 36, Limestone[3]);
        Pediment(k, 8, 56, 37, 10, Limestone);
        k.Ellipse(32, 41, 3, 2.5f, P.Gold);   // the arms
        k.Px(32, 42, k.Team[2]);
        for (int i = 0; i < 2 + k.Level; i++) for (int h = 0; h < 2 + i % 2; h++) k.HLine(44 + i * 5, 47 + i * 5, 2 + h, h % 2 == 0 ? P.Gold[2] : P.Gold[3]);
        if (k.Level >= 1) k.Banner(0, 2, 30);
        k.Finish();
        if (k.Frame % 2 == 0) k.Px(45, 5, P.White);
    }

    // ---------- Industry ----------

    private static void PxBlastFurnace(K k)
    {
        // A tall stone furnace stack against a bank, fed with coke from the top, a water wheel for the bellows.
        k.Ground(2, 2, 94, 40, P.Dirt);
        // the furnace: a battered stone tower, glowing at the hearth
        for (int y = 4; y < 54; y++)
        {
            int inset = (y - 4) / 7;
            for (int x = 28 + inset; x <= 56 - inset; x++)
                k.Px(x, y, (y % 5 == 0 || (x + y / 5 * 3) % 8 == 0) ? Limestone[0] : x < 34 + inset ? Limestone[3] : Limestone[2]);
        }
        float glow = 0.5f + 0.5f * k.Wave();
        k.Rect(37, 4, 47, 14, P.Coal[1]);
        k.Rect(39, 4, 45, 11, glow > 0.5f ? P.Glow[2] : P.Fire[2]);
        k.Rect(30, 2, 54, 4, P.Fire[1]); k.HLine(30, 54, 3, P.Glow[3]);   // the tapped iron running out
        // charging bridge and a coke barrow
        k.Line(56, 50, 76, 40, P.Wood[1], 2);
        k.Cart(66, 42, P.Coal);
        // water wheel driving the bellows
        float cx = 16, cy = 18, r = 12;
        k.Rect(4, 4, 28, 8, P.Water[2]); k.HLine(4, 28, 8, P.Water[3]);
        k.Ellipse(cx, cy, r, r, P.Wood, l => (l - new Vector2(cx, cy)).sqrMagnitude > (r - 3) * (r - 3));
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f + k.T / 8f) * Mathf.PI * 2;
            k.Line(cx, cy, cx + Mathf.Cos(a) * (r - 1), cy + Mathf.Sin(a) * (r - 1), P.Wood[1]);
        }
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(66 + i * 7, 6, 3, 2, P.Iron);   // pig iron
        if (k.Level >= 2) k.Banner(90, 4, 22);
        k.Finish();
        k.Smoke(42, 62, 1.0f, 150);
        for (int i = 0; i < 3; i++) k.Px(40 + k.R(0, 5), 56 + k.R(0, 4), P.Glow[3]);   // sparks from the throat
    }

    // ---------- Military ----------

    private static void PxMusketeerBarracks(K k)
    {
        // A long Georgian barracks around a parade ground: sash windows, a clock pediment, stands of muskets.
        k.Ground(2, 2, 94, 30);
        GeorgianWall(k, 6, 10, 70, 32);
        k.Rect(6, 10, 70, 12, Limestone[2]);
        for (int i = 0; i < 5; i++) if (i != 2) Sash(k, 11 + i * 13, 17, 5, 8);
        k.Door(34, 11, 8, 13, arched: true);
        k.Roof(3, 73, 32, 10, K.RoofType.Slate, 8);
        Pediment(k, 28, 48, 33, 7, Limestone);
        k.Ellipse(38, 36, 2, 2, new[] { P.Brass[0], P.Linen[2], P.Linen[3], P.White });   // clock
        // muskets piled in a stand on the parade ground
        for (int i = 0; i < 2 + k.Level; i++)
        {
            int x = 76 + (i % 2) * 9, y = 4 + (i / 2) * 10;
            k.Line(x, y, x + 3, y + 10, P.Wood[2]); k.Line(x + 6, y, x + 3, y + 10, P.Wood[2]); k.Line(x + 3, y, x + 3, y + 10, P.Wood[1]);
            k.Px(x + 3, y + 11, P.Steel[3]);
        }
        k.Ellipse(16, 5, 4, 3, new[] { P.Red[1], P.Red[2], P.Linen[2], P.Linen[3] });   // drum
        k.Banner(54, 42, 14);
        k.Finish();
    }

    private static void PxJagerRange(K k)
    {
        // A forester's lodge at the edge of the range: timber and brick, antlers over the door, targets in the trees.
        k.Ground(2, 2, 94, 80, P.Dirt);
        GeorgianWall(k, 10, 56, 60, 70);
        k.Rect(10, 56, 60, 58, Limestone[2]);
        Sash(k, 16, 60, 5, 7); Sash(k, 48, 60, 5, 7);
        k.Door(31, 57, 8, 11);
        k.Line(32, 70, 30, 74, P.Bone[2]); k.Line(38, 70, 40, 74, P.Bone[2]);   // antlers
        k.Roof(6, 64, 70, 10, K.RoofType.Shingle, 6);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) k.Target(12 + i * 22, 30);
        foreach (int x in new[] { 74, 86 }) { k.VLine(x, 20, 42, P.Wood[1]); k.Ellipse(x, 48, 6, 9, P.Pine); }
        k.Rect(6, 8, 90, 11, P.Wood[1]); k.HLine(6, 90, 11, P.Wood[3]);   // the firing rail
        if (k.Level >= 1) k.Banner(2, 14, 22, P.Green);
        if (k.Level >= 2) k.Banner(90, 56, 20, P.Green);
        k.Finish();
        if (k.Frame == 1) { k.Px(40, 14, P.Glow[3]); k.Px(41, 14, P.White); }
        k.Smoke(44, 16, 0.5f, 80);
    }

    private static void PxHussarStables(K k)
    {
        // Elegant stables with arched doors and a cupola, sabres crossed over the gate.
        k.Ground(2, 2, 94, 30);
        GeorgianWall(k, 6, 14, 90, 32);
        k.Rect(6, 14, 90, 15, Limestone[2]);
        k.Roof(3, 93, 32, 10, K.RoofType.Slate, 10);
        // the cupola
        k.Rect(44, 42, 52, 48, P.Linen[3]); k.Cone(48, 48, 6, 6, K.RoofType.Slate);
        for (int i = 0; i < 4; i++)
        {
            int x = 12 + i * 20;
            k.Door(x, 15, 10, 14, arched: true);
            if (i < 2 + k.Level)
            {
                var coat = i % 2 == 0 ? P.Horse : P.DarkHorse;
                k.Ellipse(x + 5, 24, 3, 3.5f, coat);
                k.Rect(x + 5, 20 + (k.Frame % 2), x + 8, 23 + (k.Frame % 2), coat[2]);
                k.Px(x + 6, 25, P.Ink);
            }
        }
        k.Line(42, 36, 54, 40, P.Steel[3]); k.Line(42, 40, 54, 36, P.Steel[3]);   // crossed sabres
        k.Hay(2, 2); k.Trough(70, 2, 12);
        if (k.Level >= 1) k.Banner(92, 4, 22);
        k.Finish();
    }

    private static void PxSurgeonsHall(K k)
    {
        // A limestone hall with a domed anatomy theatre; a carriage-lamp and a sign of the red-and-white pole.
        k.Ground(4, 2, 92, 26, P.Dirt);
        k.Wall(6, 10, 70, 36, K.Mat.Plaster);
        for (int i = 0; i < 4; i++) if (i != 2) Sash(k, 12 + i * 15, 18, 5, 9);
        k.Door(40, 11, 8, 13, arched: true);
        k.Roof(3, 73, 36, 8, K.RoofType.Slate, 8);
        Dome(k, 40, 44, 8, P.Iron);
        // the barber-surgeon's pole
        k.VLine(80, 4, 24, P.Linen[3]);
        for (int y = 6; y < 24; y += 4) { k.Px(80, y, P.Red[2]); k.Px(80, y + 1, P.Red[2]); }
        k.Ellipse(80, 25, 1.5f, 1.5f, P.Brass);
        for (int i = 0; i < 2 + k.Level; i++) k.Rect(6 + i * 10, 3, 13 + i * 10, 6, P.Linen[3]);
        if (k.Level >= 1) k.Banner(92, 4, 22, P.Linen);
        k.Finish();
    }

    private static void PxCasemateBattery(K k)
    {
        // A low star-fort casemate of dressed stone, earth on its roof; the gun looks out through an embrasure.
        k.Ground(4, 2, 60, 8);
        for (int y = 2; y < 30; y++)
        {
            int inset = (y - 2) / 5;
            for (int x = 6 + inset; x <= 58 - inset; x++)
                k.Px(x, y, (y % 5 == 0 || (x + y / 5 * 4) % 9 == 0) ? Limestone[0] : x < 12 + inset ? Limestone[3] : Limestone[2]);
        }
        k.Rect(10, 30, 54, 36, P.Dirt[1]);   // turf on the roof
        for (int x = 11; x < 54; x += 3) k.Px(x, 36, P.Grass[3]);
        k.Door(14, 3, 8, 10, arched: true);
        // the embrasure and the gun's muzzle poking out of it
        k.Rect(40, 16, 52, 24, P.Coal[0]);
        int recoil = k.Frame == 1 ? 2 : 0;
        k.Rect(44 - recoil, 18, 61 - recoil, 21, P.Iron[1]); k.HLine(44 - recoil, 61 - recoil, 21, P.Iron[3]);
        k.Rect(59 - recoil, 17, 61 - recoil, 22, P.Iron[2]);
        if (k.Level >= 1) k.Banner(2, 2, 26);
        if (k.Level >= 2) k.HLine(10, 54, 29, Limestone[3]);
        if (k.Level >= 3) for (int x = 12; x < 54; x += 8) k.Px(x, 12, P.Iron[2]);
        k.Finish();
        if (k.Frame == 1) { k.Px(63, 19, P.Glow[3]); k.Px(64, 20, P.White); }
    }

    private static void PxArtilleryPark(K k)
    {
        // A walled gun park: a brick shed, a howitzer on its carriage, pyramids of shot, a powder magazine.
        k.Ground(2, 2, 94, 40, P.Dirt);
        GeorgianWall(k, 6, 14, 52, 34);
        k.Roof(3, 55, 34, 9, K.RoofType.Slate, 8);
        k.Door(20, 15, 14, 14, arched: true, open: true);
        // a stubby howitzer on its carriage
        k.Line(58, 6, 72, 12, P.Wood[1], 3);
        k.Ellipse(74, 14, 7, 4.5f, P.Bronze);
        k.Ellipse(64, 8, 5, 5, P.Wood, l => true);
        k.Ellipse(64, 8, 2, 2, P.Wood);
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < 3 - row; i++) k.Ellipse(84 + i * 3 + row * 1.5f, 4 + row * 2.5f, 1.5f, 1.5f, P.Iron);
        // the magazine
        k.Rect(78, 22, 92, 34, Limestone[2]); k.Cone(85, 34, 8, 6, K.RoofType.Slate);
        k.Rect(83, 22, 87, 28, P.Coal[1]);
        if (k.Level >= 1) k.Banner(2, 4, 24);
        k.Finish();
    }

    private static void PxGalvanicLab(K k)
    {
        // A natural philosopher's tower: copper globes on brass rods, a bank of Leyden jars, sparks leaping between.
        k.Ground(4, 2, 60, 12, P.Dirt);
        GeorgianWall(k, 14, 2, 44, 40);
        Sash(k, 20, 18, 5, 9); Sash(k, 33, 26, 5, 9);
        k.Door(25, 3, 8, 12, arched: true);
        k.Rect(12, 40, 46, 43, Limestone[3]);
        // the globes
        foreach (int x in new[] { 18, 40 })
        {
            k.VLine(x, 44, 56, P.Brass[1]);
            k.Ellipse(x, 59, 3.5f, 3.5f, P.Bronze);
            k.Px(x - 1, 60, P.Bronze[3]);
        }
        // the spark, jumping between them
        int f = k.Frame;
        Vector2 a = new Vector2(21, 59), b = new Vector2(37, 59);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p0 = Vector2.Lerp(a, b, i / 4f) + new Vector2(0, ((i + f) % 2 == 0 ? 2 : -2) * (i == 0 ? 0 : 1));
            Vector2 p1 = Vector2.Lerp(a, b, (i + 1) / 4f) + new Vector2(0, ((i + 1 + f) % 2 == 0 ? 2 : -2) * (i == 3 ? 0 : 1));
            k.Line(p0.x, p0.y, p1.x, p1.y, f % 2 == 0 ? P.White : P.Water[4]);
        }
        // Leyden jars on a bench outside
        for (int i = 0; i < 2 + k.Level; i++)
        {
            int x = 48 + i * 5;
            k.Ellipse(x, 5, 2, 3, new[] { P.Water[1], P.Water[2], P.Water[3], P.White });
            k.HLine(x - 2, x + 2, 4, P.Brass[2]);
            k.VLine(x, 8, 10, P.Brass[1]);
        }
        if (k.Level >= 1) k.Banner(2, 12, 20, P.Water);
        k.Finish();
    }
}
