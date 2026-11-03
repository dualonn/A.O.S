using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Information Age buildings in pixel art: dark glass and brushed steel, neon cyan lines, satellite
/// dishes and server racks, with holograms and nanotech for the cyber layer.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> InformationBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "SmartTower", (PxSmartTower, 4, 3f) }, { "DataBank", (PxDataBank, 4, 6f) }, { "SpecOpsBase", (PxSpecOpsBase, 4, 4f) },
            { "PlasmaRange", (PxPlasmaRange, 4, 6f) }, { "DroneHangar", (PxDroneHangar, 4, 6f) }, { "NanoClinic", (PxNanoClinic, 4, 4f) },
            { "PlasmaTurret", (PxPlasmaTurret, 4, 8f) }, { "OrbitalUplink", (PxOrbitalUplink, 4, 4f) }, { "MissileBattery", (PxMissileBattery, 4, 4f) },
            { "Nanoforge", (PxNanoforge, 4, 6f) }, { "Nanoassembler", (PxNanoassembler, 4, 6f) }, { "PlasmaTankWorks", (PxPlasmaTankWorks, 4, 5f) },
            { "AndroidLab", (PxAndroidLab, 4, 5f) }, { "JetHangar", (PxJetHangar, 4, 4f) }, { "FusionReactor", (PxFusionReactor, 4, 6f) },
            { "SiliconQuarry", (PxSiliconQuarry, 4, 5f) }, { "ChipFab", (PxChipFab, 4, 6f) },
        };

    private static readonly Color32[] DarkGlass = { P.Hex("101820"), P.Hex("1c2a36"), P.Hex("2c4252"), P.Hex("4a6a80") };
    private static readonly Color32[] Brushed = { P.Hex("4a5058"), P.Hex("767e88"), P.Hex("a6aeb8"), P.Hex("d4dae0") };

    /// <summary>A block of dark glass with a steel frame and a neon line along it.</summary>
    private static void GlassBlock(K k, int x0, int y0, int x1, int y1, bool lit = true)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, (x - x0) % 8 == 0 || (y - y0) % 6 == 0 ? Brushed[1] : ((x + y) % 11 == 0 && lit ? DarkGlass[3] : x < x0 + 2 ? DarkGlass[2] : DarkGlass[1]));
        k.HLine(x0, x1, y1, Brushed[3]);
        k.HLine(x0, x1, y0 + 1, P.Neon[2]);
    }

    private static void Dish(K k, int x, int y, int r)
    {
        k.Ellipse(x, y, r, r * 0.6f, Brushed, l => l.x <= x + 1);
        k.Line(x, y, x + r, y + r / 2, Brushed[2]);
        k.Px(x + r, y + r / 2, P.Red[3]);
    }

    private static void Holo(K k, int x0, int y0, int x1, int y1)
    {
        // a flickering hologram panel
        bool on = k.Frame % 2 == 0;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if ((y + k.Frame) % 2 == 0) k.Px(x, y, on ? new Color32(90, 216, 255, 170) : new Color32(90, 216, 255, 110));
    }

    // ---------- Housing and money ----------

    private static void PxSmartTower(K k)
    {
        // A tall dark-glass tower with lit floors, a rooftop garden and a hologram sign.
        k.Ground(2, 2, 62, 6);
        int floors = 8 + (k.Level >= 1 ? 1 : 0), top = 4 + floors * 7;
        GlassBlock(k, 8, 4, 56, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 5; i++) if ((f * 3 + i) % 4 != 0) k.Rect(11 + i * 9, 5 + f * 7, 15 + i * 9, 7 + f * 7, P.Glow[2]);
        k.Rect(6, top, 58, top + 2, Brushed[2]);
        for (int i = 0; i < 5; i++) k.Ellipse(12 + i * 10, top + 4, 3, 2, P.Leaf);
        Holo(k, 20, top + 8, 44, top + 14);
        k.Finish();
    }

    private static void PxDataBank(K k)
    {
        // A black finance hub: walls of server racks blinking behind glass, a dish on the roof.
        k.Rect(2, 2, 62, 4, Brushed[1]);
        GlassBlock(k, 6, 5, 58, 40);
        for (int x = 10; x < 56; x += 6)
            for (int y = 10; y < 36; y += 3)
                k.Px(x, y, (x + y + k.Frame) % 3 == 0 ? P.Ray[2] : (x * y + k.Frame) % 5 == 0 ? P.Red[3] : P.Neon[1]);
        k.Door(28, 5, 8, 9);
        Dish(k, 46, 46, 6);
        for (int i = 0; i < 2 + k.Level; i++) k.HLine(44 + i * 4, 47 + i * 4, 3, P.Gold[2]);
        k.Finish();
    }

    // ---------- Industry ----------

    private static void PxSiliconQuarry(K k)
    {
        // An open pit of pale quartz sand, conveyors and a sorting shed.
        k.Ground(2, 2, 94, 70, P.Sand);
        k.Pit(10, 20, 60, 56);
        for (int i = 0; i < 10; i++) k.Px(16 + k.R(0, 40), 26 + k.R(0, 26), i % 2 == 0 ? P.White : P.Neon[3]);
        k.Line(60, 40, 80, 20, Brushed[1], 3);
        GlassBlock(k, 70, 6, 92, 22);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, P.Sand);
        k.Finish();
    }

    private static void PxChipFab(K k)
    {
        // A white clean-room fab with a neon circuit painted on its wall and air scrubbers on the roof.
        k.Ground(2, 2, 94, 40, P.Dirt);
        ModernistWall(k, 6, 8, 82, 32);
        for (int x = 12; x < 78; x += 10) { k.HLine(x, x + 6, 20, P.Neon[2]); k.VLine(x + 6, 14, 20, P.Neon[2]); k.Px(x, 20, P.Neon[3]); }
        k.Door(40, 9, 10, 10);
        for (int i = 0; i < 3; i++) { int x = 20 + i * 20; k.Rect(x, 33, x + 8, 38, Brushed[2]); k.Ellipse(x + 4, 39, 3, 1.5f, Brushed); }
        if (k.Frame % 2 == 0) k.Px(76, 26, P.Ray[3]);
        k.Finish();
    }

    private static void PxNanoforge(K k)
    {
        // A sleek forge where clouds of nanites knit steel; a shimmering grey haze over the vat.
        k.Ground(2, 2, 94, 40, P.Dirt);
        GlassBlock(k, 6, 8, 70, 32);
        k.Ellipse(38, 34, 22, 6, Brushed, l => l.y >= 34);
        for (int i = 0; i < 20; i++) k.Px(18 + k.R(0, 40), 36 + k.R(0, 10), (i + k.Frame) % 3 == 0 ? P.Neon[3] : Brushed[2]);
        k.Rails(52, 94, 2);
        k.Rect(78, 6, 90, 10, Brushed[1]);
        k.Finish();
    }

    private static void PxNanoassembler(K k)
    {
        // A long low plant where finished parts slide out of a glowing assembler on a conveyor.
        k.Ground(2, 2, 94, 40, P.Dirt);
        GlassBlock(k, 6, 8, 80, 28);
        k.Rect(30, 9, 56, 22, P.Neon[0]);
        Holo(k, 32, 11, 54, 20);
        k.HLine(6, 90, 5, Brushed[1]);
        for (int i = 0; i < 4; i++) k.Crate(8 + ((i * 20 + k.Frame * 4) % 80), 6);
        k.Finish();
    }

    private static void PxFusionReactor(K k)
    {
        // A ring-shaped tokamak under a glass dome, plasma racing round inside it.
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Ellipse(48, 16, 34, 18, DarkGlass, l => l.y >= 16);
        k.Ellipse(48, 22, 20, 8, Brushed);
        k.Ellipse(48, 22, 13, 4, P.Coal);
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 6; i++)
        {
            float t = a + i * 0.4f;
            k.Px(Mathf.RoundToInt(48 + Mathf.Cos(t) * 16), Mathf.RoundToInt(22 + Mathf.Sin(t) * 6), i == 0 ? P.White : P.Neon[3 - i / 2]);
        }
        k.Rect(10, 4, 86, 8, Brushed[1]);
        k.Finish();
    }

    // ---------- Military ----------

    private static void PxSpecOpsBase(K k)
    {
        // A low black compound with a radar mast and a helipad marking.
        k.Ground(2, 2, 94, 30);
        GlassBlock(k, 6, 8, 64, 26);
        k.Door(30, 9, 8, 10);
        k.VLine(56, 26, 46, Brushed[2]); k.HLine(52, 60, 44, Brushed[2]);
        float a = k.T * Mathf.PI * 2f;
        k.Line(56, 46, 56 + Mathf.Cos(a) * 5, 46 + Mathf.Sin(a) * 2, P.Neon[2]);
        k.Ellipse(82, 14, 10, 6, Concrete); k.Line(78, 11, 78, 17, P.White); k.Line(86, 11, 86, 17, P.White); k.Line(78, 14, 86, 14, P.White);
        k.Banner(64, 26, 12);
        k.Finish();
    }

    private static void PxPlasmaRange(K k)
    {
        // A firing gallery facing holographic targets; cyan bolts streak downrange.
        k.Ground(2, 2, 94, 80, P.Dirt);
        GlassBlock(k, 10, 56, 60, 70);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) { int x = 12 + i * 22; Holo(k, x, 30, x + 6, 42); }
        k.Rect(6, 8, 90, 11, Brushed[1]);
        int f = k.Frame;
        k.Line(20 + f * 6, 14 + f * 6, 26 + f * 6, 20 + f * 6, P.Neon[3]);
        k.Finish();
    }

    private static void PxDroneHangar(K k)
    {
        // A flat hangar roof with landing circles; drones lift off and settle.
        k.Ground(2, 2, 94, 30);
        GlassBlock(k, 6, 6, 70, 24);
        k.Rect(4, 24, 72, 26, Brushed[2]);
        for (int i = 0; i < 3; i++)
        {
            int x = 16 + i * 22;
            k.Ellipse(x, 27, 6, 1.5f, P.Neon);
            int lift = (k.Frame + i) % 4;
            k.Ellipse(x, 30 + lift * 2, 4, 1.5f, Brushed);
            k.HLine(x - 6, x + 6, 31 + lift * 2, k.Frame % 2 == 0 ? Brushed[3] : Brushed[1]);
        }
        k.Finish();
    }

    private static void PxNanoClinic(K k)
    {
        // A white clinic with a glowing cyan cross and a medical drone on its roof.
        k.Ground(4, 2, 92, 26, P.Dirt);
        ModernistWall(k, 6, 8, 80, 32);
        k.Rect(52, 14, 64, 28, P.Neon[2]); k.Rect(56, 10, 60, 32, P.Neon[2]);
        k.Rect(53, 15, 63, 27, P.Neon[3]);
        GlassBlock(k, 12, 12, 40, 26);
        k.Door(44, 9, 6, 10);
        k.Ellipse(70, 36, 4, 1.5f, Brushed);
        k.Finish();
    }

    private static void PxPlasmaTurret(K k)
    {
        // A black dome turret with twin cyan-glowing barrels.
        k.Ground(10, 2, 54, 8);
        k.Rect(16, 2, 48, 12, Brushed[1]); k.HLine(16, 48, 12, Brushed[3]);
        k.Ellipse(32, 14, 14, 10, DarkGlass, l => l.y >= 14);
        k.HLine(20, 44, 16, P.Neon[2]);
        int recoil = k.Frame % 2 == 1 ? 1 : 0;
        k.Rect(40 - recoil, 18, 58 - recoil, 19, Brushed[2]); k.Rect(40 - recoil, 21, 58 - recoil, 22, Brushed[2]);
        k.Px(58 - recoil, 18, P.Neon[3]); k.Px(58 - recoil, 21, P.Neon[3]);
        k.Door(28, 2, 8, 8);
        k.Finish();
        if (k.Frame % 2 == 1) { k.Px(60, 20, P.Neon[3]); k.Px(61, 20, P.White); }
    }

    private static void PxOrbitalUplink(K k)
    {
        // A tall mast with a big dish aimed at the sky: it calls down a beam from a satellite overhead.
        k.Ground(10, 2, 54, 8);
        GlassBlock(k, 22, 2, 42, 20);
        k.VLine(32, 20, 44, Brushed[2]);
        k.Ellipse(32, 48, 12, 5, Brushed);
        k.Ellipse(32, 48, 8, 3, new[] { Brushed[0], Brushed[1], Brushed[1], Brushed[2] });
        k.VLine(32, 50, 56, Brushed[3]); k.Px(32, 57, k.Frame % 2 == 0 ? P.Red[3] : P.White);
        k.Door(28, 3, 8, 9);
        k.Finish();
    }

    private static void PxMissileBattery(K k)
    {
        // A launcher of missiles on a raised rack; one slides out with each salvo.
        k.Ground(2, 2, 62, 8);
        k.Rect(8, 2, 56, 10, Brushed[1]); k.HLine(8, 56, 10, Brushed[3]);
        k.Rect(14, 11, 50, 13, Brushed[2]);
        for (int i = 0; i < 4; i++)
        {
            int y = 14 + i * 4, x0 = 18 + (k.Frame == 1 && i == 0 ? 6 : 0);
            k.Rect(x0, y, x0 + 26, y + 2, P.White); k.Rect(x0 + 24, y, x0 + 27, y + 2, P.Red[2]);
            k.Px(x0, y + 1, P.Fire[2]);
        }
        k.Finish();
    }

    private static void PxPlasmaTankWorks(K k)
    {
        // A great black hangar; a plasma tank floats out on its glowing pad.
        k.Ground(2, 2, 94, 40, P.Dirt);
        GlassBlock(k, 4, 8, 68, 34);
        k.Rect(18, 9, 54, 26, P.Coal[1]);
        int bob = k.Frame % 2;
        k.Ellipse(78, 6, 13, 2, P.Neon);
        k.Ellipse(78, 12 + bob, 13, 4, DarkGlass); k.Line(78, 15 + bob, 94, 15 + bob, Brushed[2], 2);
        k.Finish();
    }

    private static void PxAndroidLab(K k)
    {
        // A white lab with a glass bay where an android stands in a cradle, being brought online.
        k.Ground(4, 2, 60, 12, P.Dirt);
        ModernistWall(k, 8, 2, 56, 36);
        GlassBlock(k, 18, 4, 46, 32, false);
        k.Rect(28, 6, 36, 26, P.Synth[2]); k.Ellipse(32, 28, 3.5f, 3.5f, P.Synth);
        k.HLine(30, 34, 28, P.Neon[k.Frame % 2 == 0 ? 3 : 1]);
        Holo(k, 38, 20, 44, 28);
        k.Finish();
    }

    private static void PxJetHangar(K k)
    {
        // A concrete runway and a hardened hangar; a jet fighter waits on the apron.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 34; y < 46; y++) k.Px(x, y, Concrete[2]);
        for (int x = 8; x < 90; x += 10) k.HLine(x, x + 4, 40, P.White);
        k.Ellipse(29, 50, 22, 14, Concrete, l => l.y >= 50);
        k.Rect(18, 50, 40, 58, P.Coal[1]);
        k.Line(56, 18, 84, 18, P.Chrome[2], 3);
        k.Polygon(new[] { new Vector2(62, 18), new Vector2(76, 18), new Vector2(60, 10) }, _ => P.Chrome[1]);
        k.Ellipse(78, 20, 3, 1.5f, P.Neon);
        if (k.Frame % 2 == 0) k.HLine(52, 55, 18, P.Fire[3]);
        k.Finish();
    }
}
