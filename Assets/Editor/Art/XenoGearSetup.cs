using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Xeno-artefacts: gear only found on other worlds (alien ruins, precursor vaults, lairs, native
/// chiefs and anomalies among the stars). They roll the alien traits too: dodging, regenerating,
/// and hitting the natives of other worlds harder. Only these items and their icons are made;
/// nothing else is redrawn. (Tools > Age of Stickman > Setup Xeno Gear.)
/// </summary>
public static partial class PlaceholderArt
{
    private struct XenoSpec
    {
        public string id, name;
        public GearSlot slot;
        public GearLook look;   // how it shows on a hero (existing looks that suit it)
        public float health, attack, speed, haste, range, armour, dodge, regen, xenobane;
        public System.Action<PixelCanvas> icon;
    }

    private static readonly XenoSpec[] XenoSpecs =
    {
        new XenoSpec { id = "GravityBoots", name = "Gravity Boots", slot = GearSlot.Trinket, look = GearLook.Ring, speed = 0.07f, dodge = 0.03f, icon = IcGravityBoots },
        new XenoSpec { id = "PhaseCloak", name = "Phase Cloak", slot = GearSlot.Body, look = GearLook.PhasePlate, health = 0.08f, armour = 6, dodge = 0.07f, icon = IcPhaseCloak },
        new XenoSpec { id = "StarheartAmulet", name = "Starheart Amulet", slot = GearSlot.Trinket, look = GearLook.Amulet, health = 0.06f, regen = 2.5f, icon = IcStarheart },
        new XenoSpec { id = "VoidLens", name = "Void Lens", slot = GearSlot.Head, look = GearLook.RuneVisor, range = 0.12f, attack = 0.03f, icon = IcVoidLens },
        new XenoSpec { id = "PrecursorGauntlet", name = "Precursor Gauntlet", slot = GearSlot.Weapon, look = GearLook.Claw, attack = 0.17f, xenobane = 0.15f, icon = IcPrecursorGauntlet },
        new XenoSpec { id = "ChitinCarapace", name = "Chitin Carapace", slot = GearSlot.Body, look = GearLook.HiveCarapace, health = 0.12f, armour = 14, speed = -0.02f, regen = 1f, icon = IcChitin },
        new XenoSpec { id = "WarpBlade", name = "Warp Blade", slot = GearSlot.Weapon, look = GearLook.PhaseBlade, attack = 0.13f, haste = 0.08f, dodge = 0.02f, icon = IcWarpBlade },
    };

    [MenuItem("Tools/Age of Stickman/Setup Xeno Gear")]
    public static void SetupXenoGear()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Directory.CreateDirectory("Assets/Data/Items");
        Directory.CreateDirectory(Path.Combine(Root, "Icons/Gear"));
        foreach (var spec in XenoSpecs)
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
            t.slot = spec.slot;
            t.look = spec.look;
            t.minPower = 0;      // matched to your progress like any drop
            t.xeno = true;       // only found on other worlds and among the stars
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
        Debug.Log($"Xeno gear set up: {XenoSpecs.Length} artefacts.");
    }

    /// <summary>The icon drawer for an alien-gear template, or null.</summary>
    private static System.Action<PixelCanvas> XenoIconFor(string id)
    {
        foreach (var spec in XenoSpecs) if (spec.id == id) return spec.icon;
        return null;
    }

    // ---------- Icons (20 px) ----------

    private static void IcGravityBoots(PixelCanvas c)
    {
        // a pair of boots on a ring of bent light
        c.Ellipse(Iv(10, 4), 8, 2.2f, P.Phase);
        foreach (int x in new[] { 4, 11 })
        {
            c.Rect(x, 6, x + 4, 14, P.Steel[1]);
            c.Rect(x, 5, x + 6, 7, P.Steel[2]);
            c.Rect(x + 1, 10, x + 3, 11, P.Phase[3]);
        }
    }

    private static void IcPhaseCloak(PixelCanvas c)
    {
        // a cloak half out of the world, teal seams of light
        c.Ellipse(Iv(10, 10), 7, 8, P.Phase);
        for (int y = 3; y < 18; y += 3) c.Line(Iv(5, y), Iv(15, y + 1), P.Phase[3]);
        c.Ellipse(Iv(10, 15), 3, 2, new[] { P.Steel[1], P.Steel[2], P.Steel[3], P.White });
    }

    private static void IcStarheart(PixelCanvas c)
    {
        // a chain and a little burning star in a silver setting
        c.Line(Iv(4, 16), Iv(10, 10), P.Steel[2]); c.Line(Iv(16, 16), Iv(10, 10), P.Steel[2]);
        c.Ellipse(Iv(10, 8), 4.5f, 4.5f, P.Steel);
        c.Ellipse(Iv(10, 8), 3, 3, P.Solar);
        c.Put(10, 9, P.White);
    }

    private static void IcVoidLens(PixelCanvas c)
    {
        // a visor with one deep violet lens
        c.Ellipse(Iv(10, 10), 8, 5, P.Steel);
        c.Ellipse(Iv(12, 10), 3.5f, 3.5f, P.Void);
        c.Put(13, 11, P.White);
        c.Line(Iv(2, 10), Iv(7, 10), P.Steel[3]);
    }

    private static void IcPrecursorGauntlet(PixelCanvas c)
    {
        // a heavy gauntlet of precursor alloy, a gem in its back
        c.Rect(5, 3, 14, 10, P.Starmetal[2]);
        for (int i = 0; i < 4; i++) c.Rect(5 + i * 2 + i / 2, 10, 6 + i * 2 + i / 2, 15, P.Starmetal[3]);
        c.Ellipse(Iv(10, 7), 2.2f, 2.2f, P.Riftstone);
        c.Rect(6, 1, 13, 3, P.Starmetal[1]);
    }

    private static void IcChitin(PixelCanvas c)
    {
        // a breastplate of alien chitin, amber glands between the plates
        c.Ellipse(Iv(10, 10), 7, 8, P.Hive);
        for (int y = 5; y < 17; y += 3) c.Line(Iv(4, y), Iv(16, y), P.Hive[0]);
        c.Put(8, 9, P.Hive[3]); c.Put(12, 12, P.Hive[3]);
    }

    private static void IcWarpBlade(PixelCanvas c)
    {
        // a blade that flickers in and out along its length
        c.Line(Iv(4, 4), Iv(7, 7), P.Steel[1], 2);
        c.Line(Iv(3, 8), Iv(8, 3), P.Steel[2]);
        for (int i = 0; i < 9; i++) if (i % 3 != 1) c.Line(Iv(7 + i, 7 + i), Iv(8 + i, 8 + i), i % 2 == 0 ? P.Phase[3] : P.Phase[2], 2);
        c.Put(17, 17, P.White);
    }
}
