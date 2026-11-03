using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Space travel, from the Astral Age on: the Starport launches probes and colony ships; Navigation
/// (how far ships reach) and Star Drives (how fast they fly) are levelled research whose caps rise
/// with every later age; the Colony Hub founds a settlement on a new world.
/// Safe to run again. (Tools > Age of Stickman > Setup Space Travel.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Space Travel")]
    public static void SetupSpaceTravel()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var astral = db.ages.Find(a => a != null && a.name == "AstralAge");
        if (astral == null) { Debug.LogError("Run Setup Astral Age first."); return; }
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), steel = Res(db, "Steel"),
            gold = Res(db, "Gold"), cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores");
        var starships = db.GetTechnology("Starships");

        Technology Levelled(string id, string name, string description, List<ResourceAmount> cost, string after, int levels, int perAge, float growth)
        {
            var t = Tech(db, id, name, astral, description, 100f, cost, after);
            t.maxLevel = levels;
            t.extraLevelsPerAge = perAge;
            t.levelCostGrowth = growth;
            t.levelTimeGrowth = 1.2f;
            t.requirements.minimumAge = astral;
            t.requirements.technologies = starships != null ? new List<Technology> { starships } : new List<Technology>();
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.researchSpeedMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            EditorUtility.SetDirty(t);
            return t;
        }
        string anchor = db.technologies.FindLast(t => t != null && t.IsLevelled)?.id;
        var navigation = Levelled("Navigation", "Navigation",
            "Star charts, beacons and the nerve to use them. Each level lets ships reach 8 light-years further from your settled systems. (More levels open in every later age.)",
            Cost(gold, 600, chips, 150, mana, 80), anchor, 6, 6, 1.4f);
        navigation.navigationRange = 8f;
        var drives = Levelled("StarDrives", "Star Drives",
            "Better drives: mana sails, then fusion, warp and fold. Each level makes ships fly a quarter faster. (More levels open in every later age.)",
            Cost(steel, 600, cells, 150, cores, 30), navigation.id, 6, 6, 1.4f);
        drives.driveSpeed = 0.25f;

        // The Starport launches ships.
        var starport = db.GetBuilding("Starport");
        if (starport != null)
        {
            starport.launchesShips = true;
            starport.description = "A landing pad with a control tower and a docking cradle. Builds Star Frigates, and launches probes and colony ships to other worlds (open the star map).";
            EditorUtility.SetDirty(starport);
        }

        // The Colony Hub: the town centre a colony ship's settlers build on a new world.
        var hub = NewBuilding(db, "Campfire", "ColonyHub", "Colony Hub", astral, starships, new Vector2Int(3, 3),
            "The landed colony ship, opened up into a town centre: a dome over the hold, a beacon and solar wings. Founds your settlement on a new world.",
            Cost(steel, 100, wood, 200, stone, 200), 3000);
        hub.isTownCenter = true;
        hub.foundsColonies = true;
        hub.housing = 30;
        hub.workers = 0;
        hub.upgrades = new List<BuildingUpgrade>();
        EditorUtility.SetDirty(hub);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        GenerateBuildingLooks(hub);
        GenerateIconArt();
        AssetDatabase.SaveAssets();
        Debug.Log("Space travel set up.");
    }
}
