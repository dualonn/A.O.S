using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Rich veins: old mines in the wild on a seam of a rare resource. Clear their guardians and build
/// the normal mine for that resource on top: it makes several times a full deposit's output, but sits
/// far from home where raiders roam. (Tools > Age of Stickman > Setup Rich Veins.)
/// </summary>
public static partial class PlaceholderArt
{
    /// <summary>The rare deposits that have rich veins, and how their seams look.</summary>
    private static readonly (string deposit, Ore ore)[] Veins =
    {
        ("GoldOre", Ore.Gold), ("Runestone", Ore.Rune), ("Quicksilver", Ore.Quicksilver), ("Aether", Ore.Aether), ("Uranium", Ore.Uranium), ("Silicon", Ore.Silicon),
    };

    /// <summary>
    /// Rarity of the ordinary deposits once rich veins exist (they were 0.68, 0.66, 0.64). Quicksilver
    /// comes after the others in the deposit list and takes the hill tiles they give up, so it moves more.
    /// </summary>
    private static readonly (string deposit, float rarity)[] VeinDepositRarity =
    {
        ("GoldOre", 0.71f), ("Runestone", 0.69f), ("Quicksilver", 0.70f),
    };

    /// <summary>Separate "deep mine" buildings from an earlier version of this setup.</summary>
    private static readonly string[] OldDeepMines = { "DeepGoldMine", "RuneDelve", "DeepQuicksilverMine" };

    [MenuItem("Tools/Age of Stickman/Setup Rich Veins")]
    public static void SetupRichVeins()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        foreach (var id in OldDeepMines)
        {
            db.buildings.RemoveAll(b => b == null || b.id == id);
            AssetDatabase.DeleteAsset($"Assets/Data/Buildings/{id}.asset");
            AssetDatabase.DeleteAsset($"Assets/Art/Generated/Buildings/{id}");
        }
        foreach (var (id, _) in Veins)
        {
            var dep = db.deposits.Find(d => d != null && d.id == id);
            if (dep == null) continue;   // a later age's deposit, not set up yet
            dep.richVeins = true;
            EditorUtility.SetDirty(dep);
        }
        // With rich veins out in the wild, the ordinary rare deposits near home thin out a little.
        // (Built mines keep the output they were placed with.)
        foreach (var (id, rarity) in VeinDepositRarity)
        {
            var dep = db.deposits.Find(d => d != null && d.id == id);
            if (dep == null) continue;
            dep.rarity = rarity;
            EditorUtility.SetDirty(dep);
        }
        GenerateVeinSiteArt(art);
        EditorUtility.SetDirty(db);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Rich veins set up.");
    }

    /// <summary>The vein site sprites (one per ore) and the vein map icon.</summary>
    private static void GenerateVeinSiteArt(ArtLibrary art)
    {
        art.siteVeins = new List<Sprite>();
        art.siteVeinIds = new List<string>();
        foreach (var (deposit, ore) in Veins)
        {
            var o = ore;
            art.siteVeins.Add(SitePx($"Vein{deposit}", 3, k => PxVein(k, o)));
            art.siteVeinIds.Add(deposit);
        }
        var icon = IconPixel("World/Sites/Icon_Vein.png", IcVein);
        while (art.siteIcons.Count < (int)SiteKind.Vein) art.siteIcons.Add(null);
        if (art.siteIcons.Count == (int)SiteKind.Vein) art.siteIcons.Add(icon);
        else art.siteIcons[(int)SiteKind.Vein] = icon;
    }

    /// <summary>Seam colours: dark, mid, bright glint.</summary>
    private static Color32[] VeinColours(Ore ore) => ore switch
    {
        Ore.Gold => new[] { P.Gold[1], P.Gold[2], P.Gold[3] },
        Ore.Rune => new[] { P.Rune[1], P.Rune[2], P.Rune[3] },
        Ore.Quicksilver => new[] { P.Red[1], P.Red[2], P.Steel[3] },
        Ore.Aether => new[] { P.Aether[1], P.Aether[2], P.Aether[3] },
        Ore.Uranium => new[] { P.Ray[1], P.Ray[2], P.Ray[3] },
        Ore.Silicon => new[] { P.Linen[3], P.White, P.Neon[3] },
        _ => new[] { P.Rock[1], P.Rock[2], P.Rock[3] },
    };

    /// <summary>Thick glittering seams running through the rock of a mound.</summary>
    private static void Seams(K k, Ore ore, int x0, int x1, int y0, int y1, int count)
    {
        var vein = VeinColours(ore);
        for (int s = 0; s < count; s++)
        {
            int y = y0 + k.R(0, Mathf.Max(1, y1 - y0));
            int x = x0 + k.R(0, Mathf.Max(1, (x1 - x0) / 2));
            int len = 8 + k.R(0, 12);
            for (int i = 0; i < len; i++)
            {
                int px = x + i, py = y + Mathf.RoundToInt(Mathf.Sin((i + s * 3) * 0.5f) * 1.5f);
                if (k.Get(px, py).a < 200) continue;
                k.Px(px, py, vein[i % 3 == 0 ? 0 : 1]);
                if (k.Get(px, py + 1).a > 200 && i % 2 == 0) k.Px(px, py + 1, vein[1]);
                if (i % 5 == 2) k.Px(px, py + 1, vein[2]);
            }
        }
    }

    /// <summary>An abandoned mine on a rich seam: a sagging timber adit, cobwebs, a wrecked cart.</summary>
    private static void PxVein(K k, Ore ore)
    {
        var rock = ore == Ore.Rune ? Slate : P.Rock;
        k.Ground(6, 2, 90, 24, P.Dirt);
        k.Mound(48, 42, 46, 30, rock);
        Seams(k, ore, 10, 86, 24, 58, 7);
        // the old adit: dark, its lintel cracked and sagging
        k.MineEntrance(36, 22, 22, 20);
        k.Line(36, 42, 58, 39, P.Wood[1], 2);
        k.Px(47, 41, P.Wood[0]); k.Px(48, 40, P.Wood[0]);
        // cobwebs in the corners
        k.Line(38, 40, 43, 36, P.Bone[2]); k.Line(38, 36, 42, 40, P.Bone[1]);
        k.Line(56, 40, 51, 36, P.Bone[2]);
        // broken rails and an upturned cart spilling ore
        k.Rails(38, 46, 6);
        k.Rails(50, 54, 4);
        var vein = VeinColours(ore);
        k.Rect(64, 6, 76, 12, P.Wood[1]); k.HLine(64, 76, 12, P.Wood[2]);
        k.Ellipse(66, 5, 2, 2, P.Iron); k.Ellipse(74, 13, 2, 2, P.Iron);
        for (int i = 0; i < 9; i++) k.Px(58 + k.R(0, 8), 4 + k.R(0, 4), vein[i % 3]);
        Bones(k, 18, 8);
        k.Rocks(80, 6, 2);
        k.Finish();
    }

    private static void IcVein(PixelCanvas c)
    {
        // a pick over a glittering nugget
        c.Ellipse(new Vector2(10, 7), 6, 4.5f, P.Gold);
        c.Put(8, 9, P.Gold[3]); c.Put(12, 6, P.Gold[3]);
        c.Line(new Vector2(4, 3), new Vector2(15, 16), P.Wood[2], 2);
        c.Line(new Vector2(9, 18), new Vector2(18, 12), P.Iron[3], 2);
    }
}
