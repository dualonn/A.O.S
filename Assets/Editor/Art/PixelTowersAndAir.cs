using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Lighter towers beside the heavy guns (marksman, gatling, railgun, sniper) plus the Modern gun
/// emplacement, and the buildings for flyers (airship mast, airfield). Crewless towers turn to face
/// their target; each one's muzzle point in its art is set alongside it in TowerAndAirSetup.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> TowerAndAirArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "MarksmanTower", (PxMarksmanTower, 4, 4f) }, { "RailgunTower", (PxRailgunTower, 4, 6f) },
            { "GatlingTower", (PxGatlingTower, 4, 10f) }, { "GunEmplacement", (PxGunEmplacement, 4, 5f) },
            { "SniperNest", (PxSniperNest, 4, 4f) }, { "AirshipMast", (PxAirshipMast, 4, 3f) }, { "Airfield", (PxAirfield, 4, 4f) },
        };

    private static void PxMarksmanTower(K k)
    {
        // A slim limestone tower with a loophole near the top; a long rifle pokes out of it.
        k.Ground(10, 2, 54, 8);
        for (int y = 2; y < 54; y++)
            for (int x = 20; x <= 44; x++)
                k.Px(x, y, (y % 5 == 0 || (x + y / 5 * 3) % 8 == 0) ? Limestone[0] : x < 24 ? Limestone[3] : Limestone[2]);
        for (int x = 18; x <= 46; x++) { k.Px(x, 54, Limestone[3]); k.Px(x, 55, Limestone[1]); }
        for (int i = 0; i < 4; i++) k.Rect(18 + i * 8, 56, 21 + i * 8, 59, Limestone[2]);
        k.Door(28, 3, 8, 11, arched: true);
        k.Rect(36, 43, 41, 47, P.Coal[0]);
        int recoil = k.Frame == 1 ? 1 : 0;
        k.Line(38 - recoil, 45, 50 - recoil, 45, P.Iron[1]);
        if (k.Level >= 1) k.Banner(46, 2, 22);
        if (k.Level >= 2) k.HLine(20, 44, 30, Limestone[3]);
        k.Finish();
        if (k.Frame == 1) { k.Px(51, 45, P.Glow[3]); k.Px(52, 46, P.White); }
    }

    private static void PxRailgunTower(K k)
    {
        // A brick base carrying a long brass railgun on a pivot; aether coils along the rails glow before it fires.
        k.Ground(4, 2, 60, 8);
        SootWall(k, 10, 2, 54, 22);
        k.Rect(8, 22, 56, 25, P.Iron[2]); k.HLine(8, 56, 25, P.Iron[3]);
        k.Door(28, 3, 8, 10);
        k.Ellipse(30, 29, 6, 4, P.Iron);   // the pivot
        int recoil = k.Frame == 1 ? 2 : 0;
        k.Rect(22 - recoil, 31, 62 - recoil, 32, P.Brass[2]); k.Rect(22 - recoil, 35, 62 - recoil, 36, P.Brass[1]);   // twin rails
        k.Rect(20 - recoil, 31, 30 - recoil, 36, P.Iron[1]);
        float charge = k.Frame == 0 ? 1f : k.Frame == 3 ? 0.5f : 0f;
        for (int x = 34; x < 60; x += 5) k.Rect(x - recoil, 33, x + 1 - recoil, 34, charge > 0.5f ? P.Aether[3] : charge > 0 ? P.Aether[2] : P.Aether[1]);
        if (k.Level >= 1) k.Banner(56, 2, 20);
        if (k.Level >= 2) for (int x = 12; x < 54; x += 8) k.Px(x, 14, P.Brass[3]);
        k.Finish();
        if (k.Frame == 1) { k.Line(63, 33.5f, 66, 33.5f, P.Aether[3]); k.Px(66, 34, P.White); }
    }

    private static void PxGatlingTower(K k)
    {
        // A squat iron-clad tower with a crank-turned gatling gun: its barrels spin as it fires.
        k.Ground(8, 2, 56, 8);
        for (int y = 2; y < 30; y++)
            for (int x = 14; x <= 50; x++)
                k.Px(x, y, (x - 14) % 6 == 0 && y % 4 == 2 ? P.Brass[2] : x < 18 ? P.Iron[3] : (y % 8 == 0 ? P.Iron[1] : P.Iron[2]));
        k.Rect(12, 30, 52, 32, P.Iron[3]);
        k.Door(26, 3, 8, 10);
        // the gatling: a cluster of barrels that turns frame to frame
        k.Rect(34, 20, 42, 27, P.Iron[1]);
        for (int b = 0; b < 3; b++)
        {
            int y = 21 + ((b + k.Frame) % 3) * 2;
            k.HLine(42, 58, y, b == 0 ? P.Iron[3] : P.Iron[2]);
        }
        k.Rect(56, 20, 58, 27, P.Brass[2]);
        if (k.Level >= 1) k.Banner(2, 2, 22);
        k.Finish();
        if (k.Frame % 2 == 1) { k.Px(60, 23, P.Glow[3]); k.Px(61, 24, P.White); }
    }

    private static void PxGunEmplacement(K k)
    {
        // A big concrete emplacement with a heavy gun under a curved shield.
        k.Ground(2, 2, 62, 8);
        for (int y = 2; y < 18; y++)
        {
            int inset = (y - 2) / 3;
            for (int x = 4 + inset; x <= 60 - inset; x++)
                k.Px(x, y, ((x * 7 + y * 3) % 29 == 0) ? Concrete[1] : x < 10 + inset ? Concrete[3] : Concrete[2]);
        }
        k.Ellipse(30, 18, 14, 9, P.Olive, l => l.y >= 18);   // the gun shield
        int recoil = k.Frame == 1 ? 3 : 0;
        k.Rect(38 - recoil, 18, 62 - recoil, 22, P.Olive[1]); k.HLine(38 - recoil, 62 - recoil, 22, P.Olive[3]);
        k.Rect(58 - recoil, 17, 62 - recoil, 23, P.Olive[0]);   // muzzle brake
        k.Px(25, 22, P.Coal[1]); k.Px(26, 22, P.Coal[1]);
        Sandbags(k, 2, 20, 2, 1);
        if (k.Level >= 1) k.Banner(2, 8, 22);
        if (k.Level >= 2) for (int i = 0; i < 3; i++) k.Ellipse(50 + i * 4, 4, 1.6f, 2.4f, P.Brass);   // shells
        k.Finish();
        if (k.Frame == 1) { k.Ellipse(66, 20, 3, 2.5f, P.Fire); k.Px(68, 20, P.White); }
    }

    private static void PxSniperNest(K k)
    {
        // A tall concrete observation tower with sandbags on top; a long scoped rifle rests on them.
        k.Ground(10, 2, 54, 8);
        ConcreteBlock(k, 20, 2, 44, 44);
        k.Door(28, 3, 8, 10);
        k.Rect(30, 24, 34, 28, P.Coal[1]);
        k.Rect(16, 44, 48, 46, Concrete[3]);
        Sandbags(k, 18, 46, 47, 1);
        int recoil = k.Frame == 1 ? 1 : 0;
        k.Line(36 - recoil, 50, 60 - recoil, 50, P.Iron[1]);
        k.Rect(42 - recoil, 51, 47 - recoil, 52, P.Coal[2]);   // the scope
        k.Ellipse(36, 52, 2.5f, 2.5f, P.Olive);                // the sniper's helmet behind it
        if (k.Level >= 1) k.Banner(46, 2, 20);
        k.Finish();
        if (k.Frame == 1) { k.Px(61, 50, P.Glow[3]); k.Px(62, 51, P.White); }
    }

    private static void PxAirshipMast(K k)
    {
        // A lattice mooring mast with a little airship tethered to it, a hangar shed and gas cylinders.
        k.Ground(2, 2, 94, 40, P.Dirt);
        for (int y = 4; y < 70; y++)
        {
            int half = 6 - y / 14;
            k.Px(70 - half, y, P.Iron[2]); k.Px(70 + half, y, P.Iron[1]);
            if (y % 6 == 0) k.HLine(70 - half, 70 + half, y, P.Iron[2]);
        }
        k.Ellipse(70, 72, 3, 3, P.Brass);
        int sway = Mathf.RoundToInt(k.Wave() * 2);
        k.Ellipse(46 + sway, 68, 20, 7, P.Linen);
        k.HLine(28 + sway, 64 + sway, 68, k.Team[2]);
        k.Rect(42 + sway, 57, 52 + sway, 61, P.Wood[2]);
        k.Line(66 + sway, 68, 68, 70, P.Linen[1]);
        // the hangar
        Corrugated(k, 6, 6, 44, 30);
        k.Ellipse(25, 30, 20, 8, P.Iron, l => l.y >= 30);
        k.Rect(16, 7, 34, 22, P.Coal[1]);
        for (int i = 0; i < 2 + k.Level; i++) k.Rect(48 + i * 5, 4, 51 + i * 5, 12, P.Steel[2]);   // gas cylinders
        k.Finish();
    }

    private static void PxAirfield(K k)
    {
        // A grass strip with a curved-roof hangar, a windsock and a biplane waiting by the line.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 34; y < 46; y++) k.Px(x, y, (x / 6 + y) % 2 == 0 ? P.Grass[2] : P.Grass[3]);
        for (int x = 8; x < 90; x += 10) k.HLine(x, x + 4, 40, P.White);
        Corrugated(k, 8, 50, 50, 64, P.Olive);
        k.Ellipse(29, 64, 21, 9, P.Olive, l => l.y >= 64);
        k.Rect(18, 51, 40, 62, P.Coal[1]);
        // the windsock
        k.VLine(84, 50, 68, P.Iron[2]);
        int flap = k.Frame % 2;
        k.Rect(78 - flap, 65, 84, 68, P.Fire[2]); k.Rect(74 - flap, 65, 77 - flap, 67, P.White);
        // a parked biplane
        var body = k.Team;
        k.Line(56, 18, 82, 20, body[2], 3);
        k.Line(62, 22, 78, 22, P.Linen[2], 2); k.Line(64, 30, 78, 30, P.Linen[2], 2);
        k.VLine(66, 22, 30, P.Wood[1]); k.VLine(76, 22, 30, P.Wood[1]);
        k.VLine(84, 15 - flap, 25 + flap, P.Wood[3]);
        k.Ellipse(60, 14, 2, 2, P.Coal); k.Ellipse(76, 14, 2, 2, P.Coal);
        for (int i = 0; i < 1 + k.Level; i++) { k.Barrel(4 + i * 7, 4); k.Px(6 + i * 7, 8, P.Red[2]); }
        k.Finish();
    }
}
