using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// The native peoples' own crafts: one trinket each, bought from them once you trade (never a
/// random drop). Only these items and their icons are made. (Tools > Age of Stickman > Setup Diplomacy.)
/// </summary>
public static partial class PlaceholderArt
{
    private struct NativeGoodSpec
    {
        public string id, name;
        public GearLook look;
        public float health, attack, speed, haste, range, armour, dodge, regen, xenobane;
        public System.Action<PixelCanvas> icon;
    }

    private static readonly NativeGoodSpec[] NativeGoods =
    {
        new NativeGoodSpec { id = "SandglassCharm", name = "Sandglass Charm", look = GearLook.Charm, speed = 0.05f, haste = 0.04f, icon = IcSandglass },
        new NativeGoodSpec { id = "FrostfangTotem", name = "Frostfang Totem", look = GearLook.Charm, attack = 0.06f, health = 0.04f, icon = IcFrostfang },
        new NativeGoodSpec { id = "BogWitchFetish", name = "Bog-Witch Fetish", look = GearLook.Charm, health = 0.04f, regen = 1.5f, icon = IcBogFetish },
        new NativeGoodSpec { id = "EmberHeart", name = "Ember Heart", look = GearLook.Amulet, attack = 0.08f, icon = IcEmberHeart },
        new NativeGoodSpec { id = "ShardLens", name = "Shard Lens", look = GearLook.Amulet, range = 0.1f, attack = 0.02f, icon = IcShardLens },
        new NativeGoodSpec { id = "TidepearlNecklace", name = "Tidepearl Necklace", look = GearLook.Amulet, health = 0.07f, dodge = 0.03f, icon = IcTidepearl },
        new NativeGoodSpec { id = "WildwoodTalisman", name = "Wildwood Talisman", look = GearLook.Charm, speed = 0.06f, regen = 1f, icon = IcWildwood },
    };

    [MenuItem("Tools/Age of Stickman/Setup Diplomacy")]
    public static void SetupDiplomacy()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Directory.CreateDirectory("Assets/Data/Items");
        Directory.CreateDirectory(Path.Combine(Root, "Icons/Gear"));
        foreach (var spec in NativeGoods)
        {
            string path = $"Assets/Data/Items/{spec.id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<ItemTemplate>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<ItemTemplate>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.id = spec.id;
            t.displayName = spec.name;
            t.slot = GearSlot.Trinket;
            t.look = spec.look;
            t.minPower = 0;
            t.xeno = true;          // native work of another world (rolls the alien traits)
            t.tradeOnly = true;     // only bought from its people, never a random find
            t.health = spec.health; t.attack = spec.attack; t.speed = spec.speed; t.haste = spec.haste;
            t.range = spec.range; t.armour = spec.armour;
            t.dodge = spec.dodge; t.regen = spec.regen; t.xenobane = spec.xenobane;
            t.icon = IconPixel($"Icons/Gear/{spec.id}.png", spec.icon);
            EditorUtility.SetDirty(t);
            db.itemTemplates.RemoveAll(x => x == null || x.id == spec.id);
            db.itemTemplates.Add(t);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Diplomacy set up: {NativeGoods.Length} native goods.");
    }

    /// <summary>The icon drawer for a native good, or null (used when every gear icon is redrawn).</summary>
    private static System.Action<PixelCanvas> NativeGoodIcon(string id)
    {
        foreach (var spec in NativeGoods) if (spec.id == id) return spec.icon;
        return null;
    }

    // ---------- Icons (20 px) ----------

    private static void Cord(PixelCanvas c)
    {
        c.Line(new Vector2(5, 17), new Vector2(10, 11), P.Leather[2]);
        c.Line(new Vector2(15, 17), new Vector2(10, 11), P.Leather[2]);
    }

    private static void IcSandglass(PixelCanvas c)
    {
        // a little hourglass of desert glass on a cord
        Cord(c);
        c.Line(new Vector2(7, 10), new Vector2(13, 10), P.Gold[2]); c.Line(new Vector2(7, 2), new Vector2(13, 2), P.Gold[2]);
        c.Polygon(new[] { new Vector2(8, 9), new Vector2(12, 9), new Vector2(10, 6) }, _ => P.Gold[3]);
        c.Polygon(new[] { new Vector2(8, 3), new Vector2(12, 3), new Vector2(10, 6) }, _ => P.Gold[1]);
    }

    private static void IcFrostfang(PixelCanvas c)
    {
        // a great white fang bound with blue cord
        Cord(c);
        c.Polygon(new[] { new Vector2(8, 11), new Vector2(13, 11), new Vector2(10, 1) }, l => l.x < 10 ? (Color32?)P.White : P.Bone[2]);
        c.Line(new Vector2(8, 10), new Vector2(13, 10), P.Blue[2]);
    }

    private static void IcBogFetish(PixelCanvas c)
    {
        // a knot of reeds and bone with a green bead
        Cord(c);
        c.Ellipse(new Vector2(10, 7), 3.5f, 4, P.Leaf);
        c.Line(new Vector2(7, 4), new Vector2(13, 10), P.Bone[3]);
        c.Ellipse(new Vector2(10, 6), 1.4f, 1.4f, new[] { P.Green[1], P.Green[2], P.Green[3], P.White });
    }

    private static void IcEmberHeart(PixelCanvas c)
    {
        // a glowing coal set in black iron
        Cord(c);
        c.Ellipse(new Vector2(10, 6), 4.5f, 4.5f, P.Coal);
        c.Ellipse(new Vector2(10, 6), 2.8f, 2.8f, P.Fire);
    }

    private static void IcShardLens(PixelCanvas c)
    {
        // a crystal shard ground into a lens
        Cord(c);
        c.Ellipse(new Vector2(10, 6), 4.5f, 4.5f, P.Aether);
        c.Ellipse(new Vector2(9, 7), 1.5f, 1.5f, new[] { P.White, P.White, P.White, P.White });
    }

    private static void IcTidepearl(PixelCanvas c)
    {
        // a pearl in a shell
        Cord(c);
        c.Ellipse(new Vector2(10, 6), 5, 3.5f, P.Water);
        c.Ellipse(new Vector2(10, 7), 2, 2, new[] { P.Bone[2], P.Bone[3], P.White, P.White });
    }

    private static void IcWildwood(PixelCanvas c)
    {
        // a carved wooden leaf
        Cord(c);
        c.Polygon(new[] { new Vector2(10, 11), new Vector2(14, 6), new Vector2(10, 1), new Vector2(6, 6) }, l => l.x < 10 ? (Color32?)P.Wood[3] : P.Wood[2]);
        c.Line(new Vector2(10, 10), new Vector2(10, 2), P.Leaf[2]);
    }
}
