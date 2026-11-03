using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Enchanting: the Essence resource (from salvaged gear and the wild's hoards) and the Rune Enchanting
/// research that unlocks enhancing, enchanting, re-rolling and salvaging in the hero gear panel.
/// Safe to run again. (Tools > Age of Stickman > Setup Enchanting.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Enchanting")]
    public static void SetupEnchanting()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age medieval = db.ages.Find(a => a != null && a.name == "MedievalAge");
        ResourceType gold = Res(db, "Gold"), runestone = Res(db, "Runestone"), coin = Res(db, "Coin");
        var essence = MedievalResource(db, "Essence", "Essence", medieval, "Runestone");

        var tech = Tech(db, Settlement.EnchantingTech, "Rune Enchanting", medieval,
            "Runes cut into a blade bind power to it. Heroes' gear can be enhanced, enchanted, re-rolled, and spare gear salvaged into Essence.",
            90f, Cost(gold, 250, runestone, 120, coin, 150), "RuneWarding");
        tech.requirements.minimumAge = medieval;
        tech.requirements.technologies = new List<Technology>();
        tech.unitModifiers = new List<UnitStatModifier>();
        tech.productionModifiers = new List<ProductionModifier>();
        tech.buildingHealthMultiplier = 1f;
        EditorUtility.SetDirty(tech);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        GenerateIconArt();   // gives Essence its icon
        Debug.Log("Enchanting set up.");
    }

    private static void IcEssence(PixelCanvas c)
    {
        // a swirl of violet sparkling dust
        c.Ellipse(new Vector2(10, 9), 6, 6, new[] { P.Purple[0], P.Purple[1], P.Purple[2], P.Purple[3] });
        c.Ellipse(new Vector2(10, 9), 3, 3, new[] { P.Purple[2], P.Purple[3], P.White, P.White });
        foreach (var (x, y) in new[] { (3, 15), (16, 4), (17, 14), (4, 4), (10, 17) }) c.Put(x, y, P.White);
    }
}
