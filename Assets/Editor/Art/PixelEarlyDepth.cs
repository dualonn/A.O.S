using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// The buildings that fill out the first ages: a Stone Age hunting lodge, throwing ground and
/// lookout platform, a Bronze Age bowyer's range and shaman's hut, an Iron Age spear guard hall.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> EarlyDepthBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "HuntingLodge", (PxHuntingLodge, 4, 4f) }, { "ThrowingGround", (PxThrowingGround, 4, 3f) },
            { "LookoutPlatform", (PxLookoutPlatform, 4, 4f) }, { "BowyersRange", (PxBowyersRange, 4, 3f) },
            { "ShamansHut", (PxShamansHut, 4, 4f) }, { "SpearGuardHall", (PxSpearGuardHall, 4, 4f) },
        };

    private static void PxHuntingLodge(K k)
    {
        k.Ground(4, 2, 92, 30);
        int top = Hall(k, 8, 62, 16, 18, K.Mat.Logs, K.RoofType.Hide, 18, 12, windows: 2);
        // antlers over the door
        k.Line(35, top + 3, 30, top + 9, P.Bone[3]); k.Line(31, top + 7, 28, top + 8, P.Bone[2]);
        k.Line(37, top + 3, 42, top + 9, P.Bone[3]); k.Line(41, top + 7, 44, top + 8, P.Bone[2]);
        k.Rect(34, top + 1, 38, top + 3, P.Bone[2]);
        // a drying rack hung with meat and a stretched hide
        k.VLine(70, 4, 32, P.Wood[2]); k.VLine(90, 4, 32, P.Wood[2]); k.HLine(70, 90, 32, P.Wood[3]);
        for (int i = 0; i < 4; i++) k.Rect(73 + i * 4, 24 + (i % 2) * 2, 74 + i * 4, 31, i % 2 == 0 ? P.Red[1] : P.Red[2]);
        k.Rect(74, 8, 86, 20, P.Leather[2]); k.Rect(75, 9, 85, 19, P.Leather[3]);
        k.Line(70, 20, 74, 20, P.Leather[1]); k.Line(86, 20, 90, 20, P.Leather[1]);
        k.Fire(64, 3, 4, 6);
        if (k.Level >= 1) k.Banner(4, 16, 20, P.Leather);
        if (k.Level >= 2) k.SpearRack(48, 2, 3);
        if (k.Level >= 3) { k.Rect(14, 2, 22, 6, P.Bone[2]); k.Px(16, 7, P.Bone[3]); k.Px(20, 7, P.Bone[3]); }   // a skull on a stump
        k.Finish();
        k.Smoke(65, 12, 0.6f, 110);
    }

    private static void PxThrowingGround(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Tent(18, 6, 14, 18, P.Leather);
        k.SpearRack(34, 4, 4);
        // straw targets with spears stuck in them
        int targets = 2 + Mathf.Min(k.Level, 2);
        for (int i = 0; i < targets; i++)
        {
            int x = 56 + i * 11, y = 14 + (i % 2) * 6;
            k.VLine(x, 4, y, P.Wood[1]);
            k.Ellipse(x, y + 5, 4.5f, 5.5f, P.Straw);
            k.Ellipse(x, y + 5, 2, 2.5f, new[] { P.Red[1], P.Red[2], P.Red[2], P.Red[3] });
            if ((i + k.Frame) % 3 != 0) k.Line(x - 1, y + 5, x - 7, y + 9, P.Wood[3]);
        }
        k.Crate(42, 4);
        k.Finish();
    }

    private static void PxLookoutPlatform(K k)
    {
        int top = 46;   // matches the crew offset (1.45 cells)
        k.Ground(6, 2, 58, 12);
        // four rough poles lashed together, a ladder of branches
        foreach (int x in new[] { 15, 47 }) { k.VLine(x, 2, top + 6, P.Wood[1]); k.VLine(x + 1, 2, top - 8, P.Wood[2]); }
        for (int y = 12; y < top - 8; y += 10) { k.Line(16, y, 47, y + 8, P.Wood[1]); k.Px(16, y, P.Leather[2]); k.Px(47, y + 8, P.Leather[2]); }
        k.VLine(29, 2, top - 8, P.Wood[3]); k.VLine(33, 2, top - 8, P.Wood[3]);
        for (int y = 5; y < top - 8; y += 5) k.HLine(29, 33, y, P.Wood[2]);
        TowerDeck(k, 10, 54, top, K.Mat.Logs, false);
        // feathers and a hide streamer on the pole tops
        k.Px(15, top + 7, P.Red[2]); k.Px(47, top + 7, P.Red[2]);
        int flap = k.Frame % 2;
        k.Rect(48, top + 3 - flap, 52, top + 5 - flap, P.Leather[3]);
        if (k.Level >= 1) { k.Pot(4, 2, P.Mud); }
        if (k.Level >= 2) k.Fire(56, 3, 3, 5);
        k.Finish();
    }

    private static void PxBowyersRange(K k)
    {
        k.Ground(2, 2, 94, 36, P.Dirt);
        int top = Hall(k, 6, 48, 14, 18, K.Mat.Mud, k.Level >= 2 ? K.RoofType.Tile : K.RoofType.Thatch, 14, 8, windows: 1);
        // bows hanging on the wall
        for (int i = 0; i < 3; i++)
        {
            int x = 12 + i * 12;
            if (Mathf.Abs(x + 2 - 27) < 6) continue;
            k.Line(x, top - 4, x + 2, top - 9, P.Wood[2]); k.Line(x + 2, top - 9, x, top - 14, P.Wood[2]); k.VLine(x, top - 14, top - 4, P.Linen[3]);
        }
        // straw butts with painted rings, arrows in them
        foreach (int x in new[] { 70, 86 })
        {
            k.Ellipse(x, 14, 6, 9, P.Straw);
            k.Ellipse(x, 15, 3.5f, 5, new[] { P.Linen[2], P.Linen[3], P.Linen[3], P.Linen[3] });
            k.Ellipse(x, 15, 1.5f, 2, new[] { P.Red[1], P.Red[2], P.Red[2], P.Red[3] });
            k.Line(x + 1, 15, x - 6, 17 + (x + k.Frame) % 3, P.Wood[3]);
        }
        if (k.Level >= 1) k.Banner(54, 6, 20);
        if (k.Level >= 3) k.Barrel(58, 4);
        k.Finish();
    }

    private static void PxShamansHut(K k)
    {
        k.Ground(4, 2, 60, 18, P.Dirt);
        k.Wall(14, 6, 50, 24, K.Mat.Wattle);
        k.Cone(32, 24, 22, 22, K.RoofType.Hide);
        k.Door(28, 6, 8, 11, arched: true, open: true);
        // a skull on a pole, feathers, bundles of herbs drying under the eaves
        k.VLine(56, 4, 30, P.Wood[2]);
        k.Ellipse(56, 32, 2.5f, 2.5f, P.Bone); k.Px(55, 32, P.Coal[0]); k.Px(57, 32, P.Coal[0]);
        k.Px(54, 29, P.Red[2]); k.Px(58, 29, P.Blue[2]);
        for (int i = 0; i < 4; i++) { int x = 17 + i * 9; k.VLine(x, 20, 24, P.Green[2]); k.Px(x, 19, P.Green[3]); }
        // the fire's glow in the doorway
        if (k.Frame % 2 == 0) k.Px(32, 8, P.Fire[2]); else k.Px(31, 9, P.Fire[3]);
        if (k.Level >= 1) k.Pot(6, 2, P.Mud);
        if (k.Level >= 2) k.Banner(8, 8, 18, P.Leather);
        k.Finish();
        k.Smoke(32, 46, 0.6f, 130);
    }

    private static void PxSpearGuardHall(K k)
    {
        k.Ground(2, 2, 94, 30);
        int top = Hall(k, 8, 88, 16, 22, K.Mat.Logs, k.Level >= 2 ? K.RoofType.Shingle : K.RoofType.Thatch, 20, 14, windows: 2);
        // round shields hung down the wall, painted
        int shields = 4 + Mathf.Min(k.Level, 2);
        for (int i = 0; i < shields; i++)
        {
            int x = 16 + i * 12;
            if (Mathf.Abs(x - 48) < 7) continue;
            k.ShieldDecor(x, top - 6, i % 2 == 0 ? P.Red : P.Bronze);
        }
        k.SpearRack(2, 2, 3);
        k.SpearRack(80, 2, 3);
        if (k.Level >= 1) k.Banner(90, 16, 22);
        k.Finish();
        k.Smoke(30, top + 18, 0.6f, 110);
    }
}
