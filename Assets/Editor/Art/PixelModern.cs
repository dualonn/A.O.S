using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Modern Age buildings in pixel art: poured concrete and red brick, corrugated-iron sheds, sandbags,
/// art-deco trim, and (the dieselpunk layer) great riveted walker gantries and smoking diesel stacks.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> ModernBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "ApartmentBlock", (PxApartmentBlock, 4, 3f) }, { "CentralBank", (PxCentralBank, 4, 3f) },
            { "InfantryBarracks", (PxInfantryBarracks, 4, 4f) }, { "MachineGunSchool", (PxMachineGunSchool, 4, 6f) },
            { "MotorPool", (PxMotorPool, 4, 4f) }, { "FieldHospital", (PxFieldHospital, 4, 3f) },
            { "Pillbox", (PxPillbox, 4, 8f) }, { "SteelMill", (PxSteelMill, 4, 6f) },
            { "TankFactory", (PxTankFactory, 4, 6f) }, { "WalkerWorks", (PxWalkerWorks, 4, 5f) },
            { "Factory", (PxFactory, 4, 6f) }, { "OilWell", (PxOilWell, 4, 4f) }, { "Refinery", (PxRefinery, 4, 6f) },
        };

    private static readonly Color32[] Concrete = { P.Hex("4c4c4e"), P.Hex("78787a"), P.Hex("a2a29e"), P.Hex("c8c8c2") };

    private static void ConcreteBlock(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, (x - x0) % 12 == 0 || (y - y0) % 9 == 0 ? Concrete[1] : ((x * 7 + y * 3) % 29 == 0 ? Concrete[1] : x < x0 + 3 ? Concrete[3] : Concrete[2]));
    }

    /// <summary>A shed of ribbed corrugated iron.</summary>
    private static void Corrugated(K k, int x0, int y0, int x1, int y1, Color32[] ramp = null)
    {
        ramp ??= P.Iron;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, (x - x0) % 3 == 0 ? ramp[1] : (x - x0) % 3 == 1 ? ramp[3] : ramp[2]);
    }

    private static void Sandbags(K k, int x0, int x1, int y, int rows = 2)
    {
        for (int r = 0; r < rows; r++)
            for (int x = x0 + (r % 2) * 3; x <= x1; x += 6) k.Ellipse(x, y + r * 4, 3.5f, 2.4f, P.Khaki);
    }

    // ---------- Housing and money ----------

    private static void PxApartmentBlock(K k)
    {
        // A tall brick apartment block with steel-framed windows, balconies and a flat roof with a water tank.
        k.Ground(2, 2, 62, 6);
        int floors = 5 + (k.Level >= 1 ? 1 : 0), top = 4 + floors * 9;
        GeorgianWall(k, 4, 4, 60, top);
        k.Rect(4, 4, 60, 7, Concrete[2]);
        k.Door(28, 5, 8, 10);
        for (int f = 1; f < floors; f++)
            for (int i = 0; i < 4; i++)
            {
                int x = 8 + i * 13, y = 6 + f * 9;
                k.Rect(x, y, x + 6, y + 5, P.Iron[1]); k.Rect(x + 1, y + 1, x + 5, y + 4, f % 2 == 0 ? P.Glow[2] : P.Water[1]);
                k.VLine(x + 3, y + 1, y + 4, P.Iron[1]);
            }
        k.Rect(2, top, 62, top + 2, Concrete[3]);
        k.Rect(44, top + 3, 54, top + 10, P.Wood[1]); k.HLine(44, 54, top + 10, P.Wood[2]);   // water tank
        k.VLine(46, top + 3, top + 6, P.Iron[1]); k.VLine(52, top + 3, top + 6, P.Iron[1]);
        if (k.Level >= 2) for (int f = 2; f < floors; f += 2) k.HLine(6, 58, 5 + f * 9, P.Iron[2]);   // balcony rails
        k.Finish();
    }

    private static void PxCentralBank(K k)
    {
        // An art-deco bank: a stepped stone tower, tall fluted piers, gilded bands and a clock.
        k.Rect(2, 2, 62, 4, Concrete[2]);
        k.Wall(6, 5, 58, 30, K.Mat.Marble);
        for (int i = 0; i < 5; i++) k.Rect(10 + i * 10, 6, 12 + i * 10, 28, P.Marble[3]);
        k.Door(28, 5, 8, 12);
        k.HLine(6, 58, 30, P.Gold[2]);
        k.Wall(16, 31, 48, 42, K.Mat.Marble);
        k.HLine(16, 48, 42, P.Gold[2]);
        k.Wall(24, 43, 40, 52, K.Mat.Marble);
        for (int i = 0; i < 3; i++) k.VLine(28 + i * 4, 44, 52, P.Marble[1]);
        k.Ellipse(32, 36, 3, 3, new[] { P.Gold[1], P.Linen[2], P.Linen[3], P.White });
        float a = k.T * Mathf.PI * 2f;
        k.Line(32, 36, 32 + Mathf.Cos(a) * 2, 36 + Mathf.Sin(a) * 2, P.Coal[1]);
        for (int i = 0; i < 2 + k.Level; i++) k.HLine(46 + i * 4, 49 + i * 4, 3, P.Gold[2]);
        if (k.Level >= 1) k.Banner(0, 2, 30);
        k.Finish();
    }

    // ---------- Industry ----------

    private static void PxOilWell(K k)
    {
        // A nodding-donkey pump jack over a seep of oil, a little storage tank beside it.
        k.Ground(2, 2, 62, 16, P.Dirt);
        k.Ellipse(20, 6, 10, 3, new[] { P.Coal[0], P.Coal[0], P.Coal[1], P.Coal[2] });
        float nod = k.Wave();
        // the A-frame and the walking beam
        k.Line(22, 4, 28, 24, P.Iron[2], 2); k.Line(34, 4, 28, 24, P.Iron[1], 2);
        Vector2 pivot = new Vector2(28, 25);
        Vector2 head = pivot + new Vector2(-12, nod * 4), tail = pivot + new Vector2(10, -nod * 4);
        k.Line(head.x, head.y, tail.x, tail.y, P.Olive[2], 3);
        k.Ellipse(head.x - 1, head.y - 1, 3, 4, P.Olive);            // the horse's head
        k.VLine((int)head.x - 2, 6, (int)head.y - 3, P.Iron[3]);     // the polished rod into the well
        k.Ellipse(tail.x + 2, tail.y - 3, 4, 4, P.Iron);              // counterweight
        k.Rect(14, 4, 20, 7, P.Iron[1]);                              // the wellhead
        // a tank of crude
        k.Rect(44, 4, 58, 20, P.Coal[2]); k.Ellipse(51, 20, 7, 2, P.Coal);
        k.HLine(44, 58, 12, P.Red[2]);
        for (int i = 0; i < k.Level; i++) k.Barrel(2 + i * 7, 2);
        k.Finish();
    }

    private static void PxRefinery(K k)
    {
        // Distillation towers, a tangle of pipes and a burning flare stack; tanks of fuel.
        k.Ground(2, 2, 94, 40, P.Dirt);
        foreach (var (x, h) in new[] { (14, 46), (28, 38) })
        {
            k.Rect(x - 4, 4, x + 4, h, P.Steel[2]); k.Rect(x - 4, 4, x - 3, h, P.Steel[3]);
            for (int y = 10; y < h; y += 8) k.HLine(x - 5, x + 5, y, P.Iron[1]);
            k.Ellipse(x, h, 4, 2, P.Steel);
        }
        k.Line(18, 30, 24, 30, P.Iron[2], 2); k.Line(32, 22, 48, 22, P.Iron[2], 2); k.Line(48, 22, 48, 12, P.Iron[2], 2);
        // fuel tanks
        for (int i = 0; i < 2 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + i * 14;
            k.Rect(x, 4, x + 11, 18, P.Steel[2]); k.Ellipse(x + 5.5f, 18, 6, 2, P.Steel);
            k.HLine(x, x + 11, 11, P.Red[2]);
        }
        // the flare stack
        k.VLine(88, 4, 50, P.Iron[2]); k.VLine(89, 4, 50, P.Iron[1]);
        k.Fire(85, 51, 8, 6 + k.Frame % 2 * 2);
        if (k.Level >= 2) k.Banner(2, 4, 22);
        k.Finish();
        k.Smoke(88, 62, 0.6f, 120);
    }

    private static void PxSteelMill(K k)
    {
        // A great steel mill: a long corrugated shed, a row of stacks, a ladle car of molten steel on rails.
        k.Ground(2, 2, 94, 40, P.Dirt);
        Corrugated(k, 6, 8, 70, 32);
        k.Rect(4, 32, 72, 35, P.Iron[2]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) k.Chimney(12 + i * 14, 36, 6, 22 + i * 3, SootBrick);
        k.Door(30, 9, 14, 16, open: true);
        float glow = 0.5f + 0.5f * k.Wave();
        k.Rect(32, 9, 42, 18, glow > 0.5f ? P.Glow[2] : P.Fire[2]);
        k.Rails(52, 94, 2);
        k.Rect(76, 6, 90, 10, P.Iron[1]); k.Ellipse(83, 13, 6, 5, P.Iron);
        k.Ellipse(83, 16, 4, 1.5f, P.Fire);
        k.Finish();
        for (int i = 0; i < 3; i++) k.Smoke(15 + i * 14, 62 + i * 3, 0.9f, 150);
    }

    private static void PxFactory(K k)
    {
        // An assembly-line factory: a long sawtooth shed with big steel windows, a water tower and a stack.
        k.Ground(2, 2, 94, 40, P.Dirt);
        GeorgianWall(k, 6, 8, 80, 30);
        SawtoothRoof(k, 6, 80, 30, 6, 8);
        for (int i = 0; i < 5; i++)
        {
            int x = 10 + i * 14;
            k.Rect(x, 14, x + 9, 24, P.Iron[1]); k.Rect(x + 1, 15, x + 8, 23, P.Water[1]);
            k.HLine(x + 1, x + 8, 19, P.Iron[1]); k.VLine(x + 4, 15, 23, P.Iron[1]);
        }
        k.Door(38, 9, 10, 10, open: true);
        Cog(k, 43, 13, 3, k.Frame, P.Iron);
        k.Chimney(84, 30, 6, 30, SootBrick);
        for (int i = 0; i < 2 + k.Level; i++) k.Crate(2 + i * 8, 2);
        if (k.Level >= 2) k.Banner(92, 4, 22);
        k.Finish();
        k.Smoke(87, 66, 0.9f, 150);
    }

    // ---------- Military ----------

    private static void PxInfantryBarracks(K k)
    {
        // Rows of brick barracks with a flagpole, a sandbagged firing step and rifles stacked by the door.
        k.Ground(2, 2, 94, 30);
        GeorgianWall(k, 6, 10, 70, 30);
        k.Rect(6, 10, 70, 12, Concrete[2]);
        for (int i = 0; i < 5; i++) if (i != 2) { int x = 10 + i * 13; k.Rect(x, 17, x + 6, 23, P.Iron[1]); k.Rect(x + 1, 18, x + 5, 22, P.Water[1]); }
        k.Door(34, 11, 8, 12);
        k.Roof(3, 73, 30, 8, K.RoofType.Slate, 8);
        Sandbags(k, 74, 94, 4);
        for (int i = 0; i < 2 + k.Level; i++) { int x = 76 + i * 6; k.Line(x, 12, x + 2, 22, P.Wood[1]); k.Px(x + 2, 23, P.Steel[3]); }
        k.VLine(90, 12, 46, P.Iron[2]);
        k.Banner(90, 40, 10);
        k.Finish();
    }

    private static void PxMachineGunSchool(K k)
    {
        // A sandbagged firing line facing a range of steel targets, a concrete hut behind.
        k.Ground(2, 2, 94, 80, P.Dirt);
        ConcreteBlock(k, 10, 56, 60, 70);
        k.Rect(8, 70, 62, 72, Concrete[3]);
        k.Door(30, 57, 8, 10);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) { int x = 12 + i * 22; k.Rect(x, 30, x + 6, 40, P.Iron[1]); k.Ellipse(x + 3, 36, 2, 2, P.Red); }
        Sandbags(k, 6, 90, 8, 2);
        // a machine gun on its tripod, firing
        k.Line(44, 16, 54, 16, P.Iron[1], 2); k.Line(46, 16, 44, 12, P.Iron[2]); k.Line(46, 16, 49, 12, P.Iron[2]);
        if (k.Frame % 2 == 0) { k.Px(56, 16, P.Glow[3]); k.Px(57, 17, P.White); }
        if (k.Level >= 1) k.Banner(2, 12, 22);
        k.Finish();
    }

    private static void PxMotorPool(K k)
    {
        // A corrugated garage with its doors open on an armoured car, oil drums and a fuel pump.
        k.Ground(2, 2, 94, 30);
        Corrugated(k, 6, 8, 62, 32, P.Olive);
        k.Rect(4, 32, 64, 34, P.Iron[2]);
        k.Rect(14, 9, 54, 26, P.Coal[1]);
        k.Rect(20, 12, 46, 20, P.Olive[2]); k.HLine(20, 46, 20, P.Olive[3]);   // the car inside
        k.Ellipse(25, 11, 3, 3, P.Coal); k.Ellipse(41, 11, 3, 3, P.Coal);
        for (int i = 0; i < 2 + k.Level; i++) { k.Barrel(68 + i * 7, 2); k.Px(70 + i * 7, 6, P.Red[2]); }
        k.Rect(86, 4, 92, 18, P.Red[2]); k.Ellipse(89, 18, 3, 2, P.Red);   // fuel pump
        if (k.Level >= 1) k.Banner(92, 22, 18);
        k.Finish();
    }

    private static void PxFieldHospital(K k)
    {
        // Canvas ward tents with red crosses, a concrete treatment hut and an ambulance.
        k.Ground(4, 2, 92, 30, P.Dirt);
        for (int i = 0; i < 2; i++)
        {
            int cxT = 20 + i * 30;
            k.Tent(cxT, 4, 13, 18, P.Khaki);
            k.Rect(cxT - 2, 12, cxT + 2, 14, P.White); k.Rect(cxT - 1, 10, cxT + 1, 16, P.White);
            k.Rect(cxT - 1, 12, cxT + 1, 14, P.Red[2]); k.Rect(cxT, 11, cxT, 15, P.Red[2]);
        }
        ConcreteBlock(k, 66, 4, 90, 22);
        k.Rect(76, 22, 80, 26, P.White); k.Rect(77, 23, 79, 25, P.Red[2]);
        for (int i = 0; i < 1 + k.Level; i++) k.Rect(6 + i * 9, 3, 12 + i * 9, 5, P.Linen[3]);
        k.Finish();
    }

    private static void PxPillbox(K k)
    {
        // A squat concrete pillbox with a firing slit; the machine gun inside swings to meet the raiders.
        k.Ground(4, 2, 60, 8);
        for (int y = 2; y < 24; y++)
        {
            int inset = (y - 2) / 4;
            for (int x = 6 + inset; x <= 58 - inset; x++)
                k.Px(x, y, ((x * 7 + y * 3) % 29 == 0) ? Concrete[1] : y > 21 ? Concrete[3] : x < 12 + inset ? Concrete[3] : Concrete[2]);
        }
        k.Rect(12, 24, 52, 26, Concrete[3]);
        Sandbags(k, 8, 56, 26, 1);
        k.Rect(34, 12, 50, 16, P.Coal[0]);
        k.Line(46, 14, 58, 14, P.Iron[1], 2);   // the gun's barrel out of the slit
        if (k.Frame % 2 == 0) { k.Px(60, 14, P.Glow[3]); k.Px(61, 15, P.White); }
        k.Door(14, 3, 7, 9);
        if (k.Level >= 1) k.Banner(2, 2, 22);
        if (k.Level >= 2) for (int x = 8; x < 56; x += 3) k.Px(x, 30 + (x % 2), P.Iron[1]);   // barbed wire
        k.Finish();
    }

    private static void PxTankFactory(K k)
    {
        // A huge assembly hall; a finished tank rolls out of its doors onto a ramp.
        k.Ground(2, 2, 94, 40, P.Dirt);
        Corrugated(k, 4, 8, 68, 38);
        SawtoothRoof(k, 4, 68, 38, 4, 8);
        k.Rect(18, 9, 54, 30, P.Coal[1]);
        k.Rect(24, 12, 50, 20, P.Olive[2]); k.Rect(32, 20, 42, 24, P.Olive[3]); k.Line(42, 22, 56, 22, P.Olive[1], 2);   // a tank inside
        k.Rect(24, 9, 50, 12, P.Coal[2]);
        k.Rails(56, 94, 2);
        k.Chimney(74, 42, 6, 24, SootBrick);
        Cog(k, 82, 18, 6, k.Frame, P.Iron);
        if (k.Level >= 2) k.Banner(2, 4, 24);
        k.Finish();
        k.Smoke(77, 70, 1.0f, 160);
        if (k.Frame % 2 == 1) { k.Px(48, 26, P.Glow[3]); k.Px(49, 27, P.White); }
    }

    private static void PxWalkerWorks(K k)
    {
        // A tall gantry shed with a walker standing half-assembled inside, a crane over it, aether lamps.
        k.Ground(4, 2, 60, 12, P.Dirt);
        Corrugated(k, 8, 2, 56, 40, P.Olive);
        k.Rect(6, 40, 58, 42, P.Iron[2]);
        k.Rect(20, 3, 44, 36, P.Coal[1]);
        // the walker: two bent legs and a cockpit
        k.Line(28, 4, 30, 14, P.Olive[2], 2); k.Line(30, 14, 28, 22, P.Olive[2], 3);
        k.Line(36, 4, 34, 14, P.Olive[1], 2); k.Line(34, 14, 36, 22, P.Olive[1], 3);
        k.Rect(25, 22, 39, 30, P.Olive[2]); k.HLine(25, 39, 30, P.Olive[3]); k.Px(28, 27, P.Glow[3]);
        // the crane hook swaying above it
        int sway = Mathf.RoundToInt(k.Wave());
        k.HLine(14, 50, 38, P.Iron[2]);
        k.VLine(32 + sway, 31, 37, P.Iron[3]);
        k.Ellipse(14, 46, 2.5f, 2.5f, P.Aether); k.Ellipse(50, 46, 2.5f, 2.5f, P.Aether);
        k.VLine(14, 42, 44, P.Bronze[2]); k.VLine(50, 42, 44, P.Bronze[2]);
        for (int i = 0; i < 1 + k.Level; i++) k.Crate(2 + i * 8, 2);
        if (k.Level >= 2) k.Banner(58, 2, 20);
        k.Finish();
        if (k.Frame % 2 == 1) { k.Px(38, 25, P.Glow[3]); k.Px(39, 26, P.White); }   // welding
    }
}
