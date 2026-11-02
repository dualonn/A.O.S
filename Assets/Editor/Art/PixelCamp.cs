using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// The raider camp in pixel art: a trampled clearing, two dark hide tents, a fire, a loot pile and
/// a ragged red-and-black banner. Each piece is its own sprite on the same canvas (pivot on the
/// footprint corner) so the game can sort them with units: the pieces stand where the old ones did
/// (back tent 1.55 tiles up, front tent 0.95, fire 0.95, banner 1.6, loot 0.25).
/// </summary>
public static partial class PlaceholderArt
{
    private const int CampOx = 10, CampOy = 10, CampW = 116, CampH = 126;

    [MenuItem("Tools/Age of Stickman/Generate Camp Art")]
    public static void GenerateCampArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        ResetDir("World/Camp");
        art.campGround = CampPx("Camp_Ground", PxCampGround, 0);
        art.campBackTent = CampPx("Camp_BackTent", k => PxRaiderTent(k, 24, 50, 22, 28), 0);
        art.campFrontTent = CampPx("Camp_FrontTent", k => PxRaiderTent(k, 70, 30, 19, 24), 0);
        art.campLoot = CampPx("Camp_Loot", PxCampLoot, 0);
        art.campBanner = new List<Sprite>();
        art.campFire = new List<Sprite>();
        for (int f = 0; f < 4; f++)
        {
            art.campBanner.Add(CampPx($"Camp_Banner_F{f}", PxCampBanner, f));
            art.campFire.Add(CampPx($"Camp_Fire_F{f}", PxCampFire, f));
        }
        art.campFrames = new List<Sprite>();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Camp art generated.");
    }

    private static Sprite CampPx(string name, System.Action<BuildingKit> draw, int frame)
    {
        var c = new PixelCanvas(CampW, CampH);
        var k = new BuildingKit(c, CampOx, CampOy, 0, frame, 4, name.GetHashCode());
        draw(k);
        return SavePixel(c, $"World/Camp/{name}.png", PixelsPerTile, new Vector2(CampOx / (float)CampW, CampOy / (float)CampH));
    }

    private static void PxCampGround(BuildingKit k)
    {
        k.Ground(2, 4, 94, 70, P.Dirt);
        // the fire's stone ring and a few bones
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2;
            k.Ellipse(48 + Mathf.Cos(a) * 9, 30 + Mathf.Sin(a) * 4.5f, 1.8f, 1.5f, P.Stone);
        }
        k.Line(20, 12, 25, 14, P.Bone[3]); k.Line(60, 58, 64, 56, P.Bone[2]);
    }

    private static void PxRaiderTent(BuildingKit k, int cx, int y, int half, int h)
    {
        var hide = new[] { P.Hex("2a1c18"), P.Hex("4a3026"), P.Hex("6b4a3a"), P.Hex("8a6450") };
        k.Tent(cx, y, half, h, hide);
        // red war paint stripe and poles poking out the top
        for (int x = cx - half / 2; x <= cx + half / 2; x++) if (k.Get(x, y + h / 2).a > 200 && k.Get(x, y + h / 2).r > 40) k.Px(x, y + h / 2, P.Red[1]);
        k.Line(cx - 1, y + h - 2, cx - 4, y + h + 5, P.Wood[1]);
        k.Line(cx + 1, y + h - 2, cx + 4, y + h + 5, P.Wood[1]);
        k.Finish();
    }

    private static void PxCampLoot(BuildingKit k)
    {
        // a chest, a sack and spears leaning together
        k.Line(8, 6, 14, 26, P.Wood[3]); k.Line(22, 6, 15, 26, P.Wood[3]);
        k.Px(14, 27, P.Steel[3]); k.Px(15, 27, P.Steel[3]);
        k.Rect(4, 4, 16, 11, P.Wood[2]); k.HLine(4, 16, 11, P.Wood[3]); k.HLine(4, 16, 8, P.Gold[2]); k.Px(10, 7, P.Gold[3]);
        k.Ellipse(23, 8, 5, 4.5f, P.Linen);
        k.Px(23, 12, P.Leather[1]);
        for (int i = 0; i < 4; i++) k.Px(6 + i * 3, 12 + (i % 2), P.Gold[3]);   // spilled coins
        k.Finish();
    }

    private static void PxCampFire(BuildingKit k)
    {
        k.Line(41, 29, 55, 33, P.Wood[1], 2); k.Line(41, 33, 55, 29, P.Wood[2], 2);
        k.Fire(42, 31, 12, 12);
        k.Finish();
        k.Smoke(49, 46, 1.1f);
    }

    private static void PxCampBanner(BuildingKit k)
    {
        // a skull-capped pole with a ragged red banner
        k.VLine(82, 51, 98, P.Wood[1]); k.VLine(83, 51, 98, P.Wood[0]);
        k.Ellipse(83, 100, 2.4f, 2.2f, P.Bone);
        k.Px(82, 100, P.Ink); k.Px(84, 100, P.Ink);
        for (int i = 0; i < 12; i++)
        {
            int dy = Mathf.RoundToInt(Mathf.Sin(k.T * 6.28f + i * 0.6f) * 1.3f);
            int len = 14 - (i % 3 == 2 ? 3 : 0);
            for (int j = 0; j < len; j++)
            {
                Color32 col = j < 2 ? P.Red[1] : (i >= 4 && i <= 7 && j >= 4 && j <= 9) ? P.Coal[1] : P.Red[2];
                k.Px(84 + 1 + i, 96 - j + dy, col);
            }
        }
        k.Finish();
    }
}
