using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mastery: research taken in levels once you reach the Classical Age. Each level is a small,
/// global bonus (the same again each time) and costs more than the last. Levels are capped for
/// now; they'd only go on for ever if the danger out in the world grew for ever too.
/// (Tools > Age of Stickman > Setup Mastery Research.)
/// </summary>
public static partial class PlaceholderArt
{
    private const int MasteryLevels = 5;

    [MenuItem("Tools/Age of Stickman/Setup Mastery Research")]
    public static void SetupMastery()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var classical = AssetDatabase.LoadAssetAtPath<Age>("Assets/Data/Ages/ClassicalAge.asset");
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin");
        string after = db.technologies.FindLast(t => t != null && !t.IsLevelled)?.id;

        var harvests = Mastery(db, "MasteryHarvests", "Better Harvests", classical,
            "Crop rotation, sharper saws, better kilns: everything you produce comes in 3% faster. Per level.",
            Cost(food, 300, wood, 300, coin, 40), after);
        harvests.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.03f } };

        var drill = Mastery(db, "MasteryArms", "Arms Drill", classical,
            "Long hours on the drill field: all troops hit 3% harder. Per level.",
            Cost(food, 200, iron, 200, tools, 20, coin, 40), harvests.id);
        drill.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.03f } };

        var hardy = Mastery(db, "MasteryHardy", "Hardy Folk", classical,
            "Good food and hard training: all troops have 3% more health. Per level.",
            Cost(food, 400, gold, 40, coin, 20), drill.id);
        hardy.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.03f } };

        var stonecraft = Mastery(db, "MasteryStonecraft", "Stonecraft", classical,
            "Better mortar and deeper footings: all buildings have 4% more health. Per level.",
            Cost(stone, 400, wood, 200, tools, 20), hardy.id);
        stonecraft.buildingHealthMultiplier = 1.04f;

        var scholarship = Mastery(db, "MasteryScholarship", "Scholarship", classical,
            "Libraries, scribes and teachers: all research goes 3% faster. Per level.",
            Cost(gold, 60, coin, 60), stonecraft.id);
        scholarship.researchSpeedMultiplier = 1.03f;

        foreach (var t in new[] { harvests, drill, hardy, stonecraft, scholarship }) EditorUtility.SetDirty(t);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("Mastery research set up.");
    }

    private static Technology Mastery(GameDatabase db, string id, string display, Age age, string description,
        List<ResourceAmount> cost, string after)
    {
        var tech = Tech(db, id, display, age, description, 90f, cost, after);
        tech.maxLevel = MasteryLevels;
        tech.levelCostGrowth = 1.5f;
        tech.levelTimeGrowth = 1.25f;
        tech.extraLevelsPerAge = 2;   // 5 now, 7 in the next age, 9 after...
        tech.requirements.minimumAge = age;
        tech.unitModifiers = new List<UnitStatModifier>();
        tech.productionModifiers = new List<ProductionModifier>();
        tech.buildingHealthMultiplier = 1f;
        tech.researchSpeedMultiplier = 1f;
        return tech;
    }
}
