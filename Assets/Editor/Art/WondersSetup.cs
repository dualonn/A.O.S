using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Great Wonders (one per age, each the only one of its kind in the game) and the art for natural
/// wonders. Draws only its own art. Safe to run again. (Tools > Age of Stickman > Setup Wonders.)
/// </summary>
public static partial class PlaceholderArt
{
    private struct WonderSpec
    {
        public string id, name, age, desc, techId, techName, techDesc;
        public int size, tallTiles, health;
        public float researchTime, buildTime;
        public (string res, int amount)[] cost, techCost;
        public System.Action<Technology, GameDatabase> effect;
    }

    /// <summary>A production bonus for one resource (null: everything). A resource that doesn't exist gives none.</summary>
    private static ProductionModifier Prod(GameDatabase db, string res, float m)
    {
        if (res == null) return new ProductionModifier { resource = null, multiplier = m };
        var r = db.GetResource(res);
        return r == null ? null : new ProductionModifier { resource = r, multiplier = m };
    }

    private static readonly WonderSpec[] WonderSpecs =
    {
        new WonderSpec { id = "StoneCircle", name = "Stone Circle", age = "StoneAge", size = 4, tallTiles = 1, health = 2500, buildTime = 240, researchTime = 60,
            techId = "WonderStoneCircle", techName = "Sky Watching", techDesc = "Counting the days by the sun and the stars. Unlocks a Great Wonder: the Stone Circle.",
            desc = "A ring of great standing stones that marks the turning of the year. Research goes 10% faster and food comes 10% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 600), ("Wood", 800), ("Stone", 900) }, techCost = new[] { ("Food", 150), ("Stone", 100) },
            effect = (t, db) => { t.researchSpeedMultiplier = 1.1f; t.productionModifiers.Add(Prod(db, "Food", 1.1f)); } },
        new WonderSpec { id = "GreatZiggurat", name = "Great Ziggurat", age = "BronzeAge", size = 5, tallTiles = 1, health = 4000, buildTime = 300, researchTime = 80,
            techId = "WonderZiggurat", techName = "Temple Building", techDesc = "Priests, scribes and a great deal of mud brick. Unlocks a Great Wonder: the Great Ziggurat.",
            desc = "A man-made mountain of mud brick, stepped to the sky, a temple on its top. +20% stone and +15% bronze. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 1000), ("Wood", 900), ("Stone", 1600), ("Bronze", 300) }, techCost = new[] { ("Food", 250), ("Bronze", 80) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, "Stone", 1.2f)); t.productionModifiers.Add(Prod(db, "Bronze", 1.15f)); } },
        new WonderSpec { id = "HangingGardens", name = "Hanging Gardens", age = "IronAge", size = 5, tallTiles = 1, health = 4500, buildTime = 330, researchTime = 100,
            techId = "WonderGardens", techName = "Terrace Gardening", techDesc = "Lifting water to the top of a mountain of terraces. Unlocks a Great Wonder: the Hanging Gardens.",
            desc = "Terraces of trees and flowers piled into a green mountain, watered by screws turned day and night. +25% food, and all troops have 5% more health. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 1600), ("Wood", 1400), ("Stone", 1600), ("Iron", 400) }, techCost = new[] { ("Food", 400), ("Iron", 120) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, "Food", 1.25f)); t.unitModifiers.Add(new UnitStatModifier { healthMultiplier = 1.05f }); } },
        new WonderSpec { id = "GreatLibrary", name = "Great Library", age = "ClassicalAge", size = 5, tallTiles = 1, health = 5000, buildTime = 360, researchTime = 120,
            techId = "WonderLibrary", techName = "Scholarship", techDesc = "Copy every scroll that comes into the harbour. Unlocks a Great Wonder: the Great Library.",
            desc = "A marble hall of a hundred thousand scrolls, every book ever written and a few not yet. Research goes 15% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 2400), ("Wood", 2000), ("Stone", 2600), ("Gold", 600) }, techCost = new[] { ("Food", 600), ("Gold", 150) },
            effect = (t, db) => { t.researchSpeedMultiplier = 1.15f; } },
        new WonderSpec { id = "GrandCathedral", name = "Grand Cathedral", age = "MedievalAge", size = 5, tallTiles = 2, health = 8000, buildTime = 420, researchTime = 140,
            techId = "WonderCathedral", techName = "Flying Buttresses", techDesc = "Stone that leans on stone, and goes up and up. Unlocks a Great Wonder: the Grand Cathedral.",
            desc = "Four lifetimes of masonry under a spire you can see three valleys away. Buildings have 20% more health and troops 6% more. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 4000), ("Wood", 3500), ("Stone", 6000), ("Gold", 900), ("Iron", 900) }, techCost = new[] { ("Stone", 1500), ("Gold", 300) },
            effect = (t, db) => { t.buildingHealthMultiplier = 1.2f; t.unitModifiers.Add(new UnitStatModifier { healthMultiplier = 1.06f }); } },
        new WonderSpec { id = "GrandObservatory", name = "Grand Observatory", age = "RenaissanceAge", size = 5, tallTiles = 2, health = 9000, buildTime = 450, researchTime = 150,
            techId = "WonderObservatory", techName = "Great Lenses", techDesc = "Lenses ground for twenty years. Unlocks a Great Wonder: the Grand Observatory.",
            desc = "A dome on bronze rails round the greatest telescope ever made. Research goes 8% faster and all troops reach 8% further. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 5000), ("Wood", 4000), ("Stone", 6000), ("Gold", 1400), ("Iron", 1200) }, techCost = new[] { ("Gold", 500), ("Iron", 400) },
            effect = (t, db) => { t.researchSpeedMultiplier = 1.08f; t.unitModifiers.Add(new UnitStatModifier { rangeMultiplier = 1.08f }); } },
        new WonderSpec { id = "RoyalAcademy", name = "Royal Academy", age = "EnlightenmentAge", size = 5, tallTiles = 1, health = 9500, buildTime = 480, researchTime = 160,
            techId = "WonderAcademy", techName = "The Encyclopaedia", techDesc = "Everything known, in one book of twenty volumes. Unlocks a Great Wonder: the Royal Academy.",
            desc = "A palace given over to argument: philosophers, surgeons and engineers under one dome. Everyone pays more in taxes and research goes 5% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 6000), ("Wood", 4500), ("Stone", 7000), ("Gold", 1800), ("Steel", 800) }, techCost = new[] { ("Gold", 700), ("Steel", 300) },
            effect = (t, db) => { t.coinPerPersonPerSecond = 0.004f; t.researchSpeedMultiplier = 1.05f; } },
        new WonderSpec { id = "GreatEngine", name = "Great Engine", age = "IndustrialAge", size = 5, tallTiles = 2, health = 11000, buildTime = 520, researchTime = 170,
            techId = "WonderEngine", techName = "The Great Engine", techDesc = "A cathedral of iron and steam. Unlocks a Great Wonder: the Great Engine.",
            desc = "The largest machine ever built, driving every mill in the city through a forest of shafts and belts. Everything you produce comes in 10% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 7000), ("Wood", 5000), ("Stone", 7000), ("Steel", 1800), ("Gears", 1200), ("Coal", 1500) }, techCost = new[] { ("Steel", 800), ("Gears", 400) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, null, 1.1f)); } },
        new WonderSpec { id = "Skyscraper", name = "Skyscraper", age = "ModernAge", size = 4, tallTiles = 4, health = 12000, buildTime = 540, researchTime = 180,
            techId = "WonderSkyscraper", techName = "Steel Frames", techDesc = "Buildings held up by their bones, not their walls. Unlocks a Great Wonder: the Skyscraper.",
            desc = "A hundred floors of steel and glass, so tall its top is in the clouds. Everyone pays a good deal more in taxes. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 8000), ("Stone", 9000), ("Steel", 2600), ("Gears", 1400), ("Oil", 1200) }, techCost = new[] { ("Steel", 1000), ("Oil", 400) },
            effect = (t, db) => { t.coinPerPersonPerSecond = 0.007f; } },
        new WonderSpec { id = "TeslaTower", name = "Tesla Tower", age = "ElectricAge", size = 4, tallTiles = 4, health = 12500, buildTime = 560, researchTime = 190,
            techId = "WonderTesla", techName = "Wireless Power", techDesc = "Power through the air itself. Unlocks a Great Wonder: the Tesla Tower.",
            desc = "A copper tower of lightning that sends power through the air. +30% power cells, and all troops hit 5% harder. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 8500), ("Stone", 9000), ("Steel", 2800), ("Gears", 1500), ("PowerCells", 900) }, techCost = new[] { ("Steel", 1100), ("PowerCells", 300) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, "PowerCells", 1.3f)); t.unitModifiers.Add(new UnitStatModifier { attackMultiplier = 1.05f }); } },
        new WonderSpec { id = "DataArk", name = "Data Ark", age = "InformationAge", size = 5, tallTiles = 2, health = 13000, buildTime = 580, researchTime = 200,
            techId = "WonderDataArk", techName = "The Data Ark", techDesc = "Everything you know, kept safe under a mountain. Unlocks a Great Wonder: the Data Ark.",
            desc = "A vault of crystal memory under a mountain, cooled by an underground river. Research goes 12% faster and +20% microchips. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 9000), ("Stone", 10000), ("Steel", 3000), ("PowerCells", 1200), ("Microchips", 900) }, techCost = new[] { ("PowerCells", 500), ("Microchips", 300) },
            effect = (t, db) => { t.researchSpeedMultiplier = 1.12f; t.productionModifiers.Add(Prod(db, "Microchips", 1.2f)); } },
        new WonderSpec { id = "ArchmagesSpire", name = "Archmage's Spire", age = "ArcaneAge", size = 4, tallTiles = 4, health = 14000, buildTime = 600, researchTime = 210,
            techId = "WonderSpire", techName = "The High Arts", techDesc = "Magic that builds itself a tower. Unlocks a Great Wonder: the Archmage's Spire.",
            desc = "A tower that grows a floor for every spell ever written, past the clouds. +30% mana, and all troops hit 6% harder. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 10000), ("Stone", 11000), ("Gold", 3000), ("Microchips", 1000), ("Mana", 900) }, techCost = new[] { ("Gold", 1200), ("Mana", 300) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, "Mana", 1.3f)); t.unitModifiers.Add(new UnitStatModifier { attackMultiplier = 1.06f }); } },
        new WonderSpec { id = "SpaceElevator", name = "Space Elevator", age = "AstralAge", size = 4, tallTiles = 5, health = 15000, buildTime = 640, researchTime = 220,
            techId = "WonderElevator", techName = "Orbital Tethers", techDesc = "A thread from the ground to the stars. Unlocks a Great Wonder: the Space Elevator.",
            desc = "A tether from the ground to a station in orbit. Ships fly faster and reach further, and +20% starmetal. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 11000), ("Stone", 12000), ("Steel", 4000), ("Starmetal", 900), ("AstralCores", 500) }, techCost = new[] { ("Starmetal", 400), ("AstralCores", 150) },
            effect = (t, db) => { t.driveSpeed = 0.5f; t.navigationRange = 10f; t.productionModifiers.Add(Prod(db, "Starmetal", 1.2f)); } },
        new WonderSpec { id = "GreatSunforge", name = "Sunforge", age = "StellarAge", size = 5, tallTiles = 2, health = 16000, buildTime = 660, researchTime = 230,
            techId = "WonderSunforge", techName = "Captured Sun", techDesc = "A piece of the sun, kept in a forge. Unlocks a Great Wonder: the Sunforge.",
            desc = "A forge round a captured piece of the sun that melts anything. +25% steel and +20% sunfire. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 12000), ("Stone", 13000), ("Steel", 4500), ("Starmetal", 1000), ("Sunfire", 600) }, techCost = new[] { ("Starmetal", 450), ("Sunfire", 200) },
            effect = (t, db) => { t.productionModifiers.Add(Prod(db, "Steel", 1.25f)); t.productionModifiers.Add(Prod(db, "Sunfire", 1.2f)); } },
        new WonderSpec { id = "FoldNexus", name = "Fold Nexus", age = "InterstellarAge", size = 5, tallTiles = 2, health = 17000, buildTime = 680, researchTime = 240,
            techId = "WonderNexus", techName = "The Nexus", techDesc = "A knot in space where a thousand folds meet. Unlocks a Great Wonder: the Fold Nexus.",
            desc = "A knot of a thousand folds of space, doors to everywhere. Ships fly much faster, and troops move 8% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 13000), ("Stone", 14000), ("Steel", 5000), ("Starmetal", 1100), ("WarpCells", 600) }, techCost = new[] { ("Starmetal", 500), ("WarpCells", 200) },
            effect = (t, db) => { t.driveSpeed = 1f; t.unitModifiers.Add(new UnitStatModifier { speedMultiplier = 1.08f }); } },
        new WonderSpec { id = "GalacticSenate", name = "Galactic Senate", age = "GalacticAge", size = 6, tallTiles = 2, health = 18000, buildTime = 700, researchTime = 250,
            techId = "WonderSenate", techName = "The Senate of Worlds", techDesc = "Every world sends someone to argue. Unlocks a Great Wonder: the Galactic Senate.",
            desc = "A domed hall where every world sends someone to argue in a thousand tongues. Everyone pays much more in taxes, and everything you produce comes in 8% faster. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 14000), ("Stone", 15000), ("Steel", 5500), ("Starmetal", 1200), ("Quintessence", 700) }, techCost = new[] { ("Starmetal", 550), ("Quintessence", 220) },
            effect = (t, db) => { t.coinPerPersonPerSecond = 0.008f; t.productionModifiers.Add(Prod(db, null, 1.08f)); } },
        new WonderSpec { id = "CradleOfCreation", name = "Cradle of Creation", age = "UniversalAge", size = 6, tallTiles = 3, health = 20000, buildTime = 760, researchTime = 270,
            techId = "WonderCradle", techName = "The First Seed", techDesc = "A machine that makes new worlds. Unlocks a Great Wonder: the Cradle of Creation.",
            desc = "A machine that makes new worlds out of a seed of Genesis Matter. Everything you produce comes in 12% faster, research goes 8% faster, and troops hit 8% harder and are 8% tougher. A Great Wonder: there can only be one.",
            cost = new[] { ("Food", 16000), ("Stone", 17000), ("Steel", 6000), ("Starmetal", 1400), ("GenesisMatter", 800) }, techCost = new[] { ("Starmetal", 600), ("GenesisMatter", 250) },
            effect = (t, db) =>
            {
                t.productionModifiers.Add(Prod(db, null, 1.12f));
                t.researchSpeedMultiplier = 1.08f;
                t.unitModifiers.Add(new UnitStatModifier { attackMultiplier = 1.08f, healthMultiplier = 1.08f });
            } },
    };

    private static List<ResourceAmount> WCost(GameDatabase db, (string res, int amount)[] items)
    {
        var list = new List<ResourceAmount>();
        foreach (var (res, amount) in items)
        {
            var r = db.GetResource(res);
            if (r != null && amount > 0) list.Add(new ResourceAmount { resource = r, amount = amount });
        }
        return list;
    }

    [MenuItem("Tools/Age of Stickman/Setup Wonders")]
    public static void SetupWonders()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        Directory.CreateDirectory("Assets/Data/Wonders");
        if (db.wonderEffects == null) db.wonderEffects = new List<Technology>();
        var made = new List<BuildingDefinition>();
        foreach (var spec in WonderSpecs)
        {
            var age = db.ages.Find(a => a != null && a.name == spec.age);
            if (age == null) { Debug.LogWarning($"Wonders: no {spec.age}, skipping the {spec.name}."); continue; }

            // Its power: a technology outside the research tree, held while the wonder stands.
            string effectPath = $"Assets/Data/Wonders/{spec.id}Power.asset";
            var effect = AssetDatabase.LoadAssetAtPath<Technology>(effectPath);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<Technology>();
                AssetDatabase.CreateAsset(effect, effectPath);
            }
            effect.id = $"{spec.id}Power";
            effect.displayName = spec.name;
            effect.age = age;
            int cut = spec.desc.IndexOf(" A Great Wonder");
            string what = cut > 0 ? spec.desc.Substring(0, cut) : spec.desc;
            int firstStop = what.IndexOf(". ");
            effect.description = firstStop > 0 ? what.Substring(firstStop + 2) : what;
            effect.unitModifiers = new List<UnitStatModifier>();
            effect.productionModifiers = new List<ProductionModifier>();
            effect.buildingHealthMultiplier = 1f;
            effect.researchSpeedMultiplier = 1f;
            effect.coinPerPersonPerSecond = 0f;
            effect.navigationRange = 0f;
            effect.driveSpeed = 0f;
            effect.maxLevel = 1;
            spec.effect(effect, db);
            effect.productionModifiers.RemoveAll(m => m == null);
            EditorUtility.SetDirty(effect);
            if (!db.wonderEffects.Contains(effect)) db.wonderEffects.Add(effect);

            // The research that unlocks it.
            var tech = Tech(db, spec.techId, spec.techName, age, spec.techDesc, spec.researchTime, WCost(db, spec.techCost), null);
            tech.requirements.minimumAge = age;
            tech.requirements.technologies = new List<Technology>();
            tech.unitModifiers = new List<UnitStatModifier>();
            tech.productionModifiers = new List<ProductionModifier>();
            tech.maxLevel = 1;
            EditorUtility.SetDirty(tech);

            // The wonder itself: big, slow to build, very tough, one of a kind.
            var def = Building(db, spec.id, spec.name, age, null, new Vector2Int(spec.size, spec.size),
                new Vector2Int(spec.size, spec.size + spec.tallTiles), spec.desc, WCost(db, spec.cost), spec.buildTime, spec.health, 0, tech);
            def.wonder = true;
            def.wonderEffect = effect;
            def.maxCount = 1;
            def.housing = 0;
            def.production = new List<ResourceRate>();
            def.inputs = new List<ResourceRate>();
            def.trainsUnit = null;
            def.towerRange = 0;
            def.upgrades = new List<BuildingUpgrade>();
            def.rankUpTo = null;
            def.canBuildOnHills = false;
            EditorUtility.SetDirty(def);
            made.Add(def);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        foreach (var def in made) GenerateBuildingLooks(def);
        GenerateNaturalWonderArt(art);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log($"Wonders set up: {made.Count} Great Wonders, {art.naturalWonders.Count} natural wonders.");
    }

    // ---------- Natural wonders (sites, 4x4) ----------

    private static void GenerateNaturalWonderArt(ArtLibrary art)
    {
        // Their own folder: the wild-sites setup clears World/Sites.
        ResetDir("World/NaturalWonders");
        art.naturalWonders = new List<Sprite>();
        var drawers = new System.Action<BuildingKit>[] { PxFalls, PxCrystalSpire, PxElderTree, PxCaldera, PxSkyArch, PxSingingDunes, PxAuroraLake, PxGreatRift };
        string[] names = { "ThunderingFalls", "CrystalSpire", "ElderTree", "FireCaldera", "SkyArch", "SingingDunes", "AuroraLake", "GreatRift" };
        for (int i = 0; i < drawers.Length; i++)
        {
            int tiles = 4;
            int w = tiles * TilePx + MarginSide * 2, h = tiles * TilePx + MarginBottom + MarginTop + 32;
            var c = new PixelCanvas(w, h);
            var k = new BuildingKit(c, MarginSide, MarginBottom, 0, 0, 1, names[i].GetHashCode());
            drawers[i](k);
            art.naturalWonders.Add(SavePixel(c, $"World/NaturalWonders/{names[i]}.png", PixelsPerTile, new Vector2(MarginSide / (float)w, MarginBottom / (float)h)));
        }
    }
}
