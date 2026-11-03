using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Research for living among the stars, from the Astral Age on: domes and planetary shields
/// against hostile worlds, and levelled branches (caps rise in every later age) for terraforming,
/// xenology, orbital defence and astro-mining, plus precursor science.
/// Safe to run again. (Tools > Age of Stickman > Setup Space Research.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Space Research")]
    public static void SetupSpaceResearch()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var astral = db.ages.Find(a => a != null && a.name == "AstralAge");
        if (astral == null) { Debug.LogError("Run Setup Astral Age first."); return; }
        ResourceType food = Res(db, "Food"), stone = Res(db, "Stone"), steel = Res(db, "Steel"), gold = Res(db, "Gold"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"), gears = Res(db, "Gears"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores"), essence = Res(db, "Essence");
        var starships = db.GetTechnology("Starships");

        Technology Make(string id, string name, string description, List<ResourceAmount> cost, string after, int levels, int perAge)
        {
            var t = Tech(db, id, name, astral, description, 110f, cost, after);
            t.maxLevel = levels;
            t.extraLevelsPerAge = perAge;
            t.levelCostGrowth = 1.45f;
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
        string anchor = "StarDrives";
        var domes = Make("Domes", "Atmosphere Domes",
            "Glass domes and air-makers over your settlements: thin and toxic air no longer slow work or keep people away.",
            Cost(steel, 800, chips, 200, mana, 120), anchor, 1, 0);
        var shields = Make("PlanetaryShields", "Planetary Shields",
            "A shimmering ward over a whole settlement: storms and quakes break against it.",
            Cost(cells, 400, cores, 80, mana, 160), domes.id, 1, 0);
        var terraform = Make("Terraforming", "Terraforming",
            "Seed the soil, thicken the air, coax the weather. Each level: +6% food and faster growth on other worlds, and long nights hurt less.",
            Cost(food, 3000, mana, 150, chips, 120), shields.id, 5, 5);
        var xeno = Make("Xenology", "Xenology",
            "Study the life of other worlds, and where its armour is thin. Each level: your troops hit native peoples 8% harder.",
            Cost(gold, 800, chips, 150, essence, 60), terraform.id, 5, 5);
        var orbital = Make("OrbitalDefence", "Orbital Defence",
            "Platforms in orbit over your Starport worlds: they drop strikes on raiders inside your settlements, and count against raids. More levels strike harder and more often.",
            Cost(steel, 1000, cells, 300, cores, 60), xeno.id, 5, 5);
        var mining = Make("AstroMining", "Astro-mining",
            "Mines in orbit over asteroid belts and gas giants (build them from the star map). Each further level: +25% from every orbital mine.",
            Cost(steel, 900, gears, 300, starmetal, 120), orbital.id, 5, 5);
        var precursor = Make("PrecursorScience", "Precursor Science",
            "Read the black glass of the precursors: their ruins and vaults on other worlds push your current research on.",
            Cost(gold, 1200, essence, 120, cores, 60), mining.id, 1, 0);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("Space research set up.");
    }
}
