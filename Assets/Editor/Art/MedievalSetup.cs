using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Medieval Age: castles, knights, steel and (the fantasy layer) carved runes.
/// Feudalism (Classical research) opens it. New resources: Steel (Iron + Charcoal at a Steelworks)
/// and Runestone (quarried from a new hill deposit, placed after the other deposits so nothing
/// already on the map moves). Classical buildings rank up into Medieval ones; new units, raiders
/// and two raider chiefs. Safe to run again. (Tools > Age of Stickman > Setup Medieval Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Medieval Age")]
    public static void SetupMedieval()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age classical = db.ages[3];
        var medieval = MedievalAgeAsset(db);

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            charcoal = Res(db, "Charcoal"), tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin");
        var steel = MedievalResource(db, "Steel", "Steel", medieval, "Coin");
        var runestone = MedievalResource(db, "Runestone", "Runestone", medieval, "Steel");

        // Gold was far rarer than the other ores (under 1% of hills against copper's 3.6%); this puts it
        // level with copper. Deposits only fill tiles the earlier ones left empty, so nothing moves.
        var goldOre = db.deposits.Find(d => d != null && d.id == "GoldOre");
        if (goldOre != null) { goldOre.rarity = 0.68f; EditorUtility.SetDirty(goldOre); }
        var runeDeposit = RunestoneDeposit(db, runestone);

        // ---------- Research ----------
        var feudalism = Tech(db, "Feudalism", "Feudalism", classical,
            "Lords, vassals and castles: the old empire gives way to kingdoms. Opens the way to the Medieval Age.",
            100f, Cost(food, 800, gold, 150, coin, 150), "Discipline");
        feudalism.requirements.minimumAge = classical;
        feudalism.requirements.technologies = new List<Technology>();

        Technology MTech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
        {
            var t = Tech(db, id, name, medieval, description, time, cost, after);
            t.requirements.minimumAge = medieval;
            t.requirements.technologies = new List<Technology>(needs);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            EditorUtility.SetDirty(t);
            return t;
        }
        var plough = MTech("HeavyPlough", "Heavy Plough", "Iron-shod ploughs that turn heavy soil: everything you produce comes in 6% faster.",
            70f, Cost(food, 600, wood, 400, tools, 40), "Feudalism");
        plough.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.06f } };
        var steelmaking = MTech("Steelmaking", "Steelmaking", "Iron and charcoal fired together into steel. Unlocks the Steelworks.",
            80f, Cost(iron, 300, charcoal, 200, tools, 40), plough.id);
        var runecraft = MTech("Runecraft", "Runecraft", "The old marks cut in the hills hold power. Unlocks the Rune Quarry, which works runestone.",
            80f, Cost(stone, 500, gold, 120), steelmaking.id);
        var chivalry = MTech("Chivalry", "Chivalry", "Armoured knights sworn to your cause. Unlocks the Knights' Hall; all troops hit 4% harder.",
            90f, Cost(food, 600, gold, 200, steel, 60), runecraft.id, steelmaking);
        chivalry.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var crossbows = MTech("Crossbows", "Crossbows", "A bow on a stock, spanned with a crank: anyone can learn to shoot one. Unlocks the Crossbow Range.",
            80f, Cost(wood, 500, steel, 60), chivalry.id, steelmaking);
        var castles = MTech("CastleBuilding", "Castle Building", "Curtain walls and round towers. Unlocks castle walls and the Castle Tower; buildings have 8% more health.",
            100f, Cost(stone, 900, steel, 60, tools, 40), crossbows.id);
        castles.buildingHealthMultiplier = 1.08f;
        var guilds = MTech("Guilds", "Guilds", "Merchants and craftsmen band together. Unlocks the Guildhall; everyone pays a little more in taxes.",
            70f, Cost(gold, 250, coin, 200), castles.id);
        guilds.coinPerPersonPerSecond = 0.002f;
        var monasteries = MTech("Monasteries", "Monasteries", "Monks who keep learning alive and tend the sick. Unlocks the Monastery; research goes 3% faster.",
            80f, Cost(food, 500, stone, 400, gold, 100), guilds.id);
        monasteries.researchSpeedMultiplier = 1.03f;
        var warding = MTech("RuneWarding", "Rune Warding", "Rune-wardens carry the hills' power into battle. Unlocks the Rune Shrine.",
            90f, Cost(runestone, 150, gold, 150), monasteries.id, runecraft);

        // Entering the Medieval Age: Feudalism. Its stores and people are what the Classical Age grows towards.
        medieval.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { feudalism },
            cost = Cost(food, 3000, wood, 2500, stone, 2500, iron, 600, gold, 400, coin, 400),
            population = 300,
        };
        EditorUtility.SetDirty(medieval);

        // ---------- Units ----------
        var manAtArms = MedievalUnit("ManAtArms", "Man-at-Arms", medieval, "Legionary", 420, 32, 1.0f, 0.6f, 0.95f);
        var pikeman = MedievalUnit("Pikeman", "Pikeman", medieval, "Legionary", 360, 30, 1.2f, 1.1f, 0.9f);
        var crossbowman = MedievalUnit("Crossbowman", "Crossbowman", medieval, "CompositeArcher", 220, 38, 2.0f, 5.6f, 0.9f);
        var knight = MedievalUnit("Knight", "Knight", medieval, "Horseman", 520, 38, 1.1f, 1.0f, 1.75f);
        var monk = MedievalUnit("Monk", "Monk", medieval, "Capsarius", 220, 20, 1.1f, 0.8f, 1.0f);
        monk.healer = true; monk.healRange = 9f;
        var warden = MedievalUnit("RuneWarden", "Rune Warden", medieval, "CompositeArcher", 260, 30, 1.6f, 5.0f, 0.9f);
        foreach (var u in new[] { manAtArms, pikeman, crossbowman, knight, monk, warden })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.01f), Rate(coin, 0.04f) };
            EditorUtility.SetDirty(u);
        }
        medieval.playerUnits = new List<UnitDefinition> { manAtArms, pikeman, crossbowman, knight, monk, warden };

        // Raiders of the age
        var brigand = MedievalUnit("Brigand", "Brigand", medieval, "Barbarian", 380, 30, 1.1f, 0.65f, 0.95f);
        var reaver = MedievalUnit("Reaver", "Reaver", medieval, "HorseRaider", 460, 34, 1.1f, 0.9f, 1.6f);
        var banditXbow = MedievalUnit("BanditCrossbow", "Bandit Crossbowman", medieval, "RaiderArcher", 200, 34, 2.0f, 5.4f, 0.9f);
        medieval.enemyUnits = new List<UnitDefinition> { brigand, reaver, banditXbow };
        EditorUtility.SetDirty(medieval);

        // ---------- Buildings ----------
        var townhouse = RankUp(db, "Insula", "Townhouse", "Townhouse", medieval, feudalism,
            "Half-timbered houses with jettied upper floors. Houses 40 people.",
            Cost(stone, 260, wood, 160, gold, 30), Cost(stone, 140, wood, 80, gold, 20, tools, 10), 600);
        townhouse.housing = 40;
        townhouse.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Third Storey", description = "+8 people.", requirements = new Requirements { cost = Cost(wood, 120, stone, 80) }, upgradeTime = 20f, extraHousing = 8 },
            new BuildingUpgrade { name = "Glazed Windows", description = "+8 people.", requirements = new Requirements { cost = Cost(stone, 100, gold, 20, tools, 10) }, upgradeTime = 25f, extraHousing = 8 },
        };

        var guildhall = RankUp(db, "Mint", "Guildhall", "Guildhall", medieval, guilds,
            "The merchants' hall: strikes coin from gold, faster than any mint.",
            Cost(stone, 220, wood, 120, gold, 60), Cost(stone, 120, gold, 40, tools, 10), 500);
        guildhall.workers = 3;
        guildhall.production = new List<ResourceRate> { Rate(coin, 0.9f) };
        guildhall.inputs = new List<ResourceRate> { Rate(gold, 0.05f) };
        guildhall.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Counting House", "Clerks and ledgers: +25% coin.", Cost(gold, 60, coin, 80), 1.25f),
            Upgrade("Charter", "A royal charter: +25% coin.", Cost(gold, 80, steel, 20), 1.25f),
        };

        var armsHall = RankUp(db, "LegionFort", "ArmsHall", "Men-at-Arms Hall", medieval, feudalism,
            "A stone keep and hall. Trains Men-at-Arms: mail, a great helm, sword and kite shield.",
            Cost(stone, 320, wood, 200, iron, 100, gold, 30), Cost(stone, 160, iron, 60, gold, 20), 800);
        Trains(armsHall, manAtArms, 4, steel, gold, food, coin);

        var crossbowRange = RankUp(db, "ArcherAcademy", "CrossbowRange", "Crossbow Range", medieval, crossbows,
            "A shooting range behind pavises. Trains Crossbowmen: slow to reload, but every bolt hits hard.",
            Cost(wood, 300, stone, 160, steel, 40), Cost(wood, 160, steel, 30), 560);
        Trains(crossbowRange, crossbowman, 4, steel, gold, food, coin);

        var knightsHall = RankUp(db, "Stables", "KnightsHall", "Knights' Hall", medieval, chivalry,
            "Stone stables for war horses. Trains Knights: armoured riders with lance and barded horse.",
            Cost(stone, 300, wood, 200, steel, 60, food, 200), Cost(stone, 150, steel, 40), 600);
        Trains(knightsHall, knight, 3, steel, gold, food, coin);

        var monastery = RankUp(db, "Valetudinarium", "Monastery", "Monastery", medieval, monasteries,
            "A church, a cloister and herb gardens. Trains Monks, who heal your troops better than any field medic.",
            Cost(stone, 320, wood, 180, gold, 40), Cost(stone, 160, gold, 30), 600);
        monastery.trainsUnit = monk;
        monastery.maxUnits = 3;
        monastery.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Infirmary", description = "+1 monk.", requirements = new Requirements { cost = Cost(food, 200, stone, 100) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Herbarium", description = "Monks heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(gold, 60, coin, 60) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var castleTower = RankUp(db, "BallistaTower", "CastleTower", "Castle Tower", medieval, castles,
            "A round stone tower. A crossbowman on its open top shoots at raiders in range.",
            Cost(stone, 300, wood, 100, steel, 40), Cost(stone, 160, steel, 30), 700);
        castleTower.workers = 2;
        castleTower.towerRange = 9.5f;
        castleTower.towerDamage = 70;
        castleTower.towerCooldown = 2.2f;
        castleTower.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Machicolations", description = "+1.5 range.", requirements = new Requirements { cost = Cost(stone, 120, steel, 20) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Steel Prods", description = "+25 damage.", requirements = new Requirements { cost = Cost(steel, 40, wood, 80) }, upgradeTime = 25f, extraTowerDamage = 25 },
            new BuildingUpgrade { name = "Thick Walls", description = "+400 health.", requirements = new Requirements { cost = Cost(stone, 200) }, upgradeTime = 30f, extraHealth = 400 },
        };

        var castleWall = RankUp(db, "FortifiedWall", "CastleWall", "Castle Wall", medieval, castles,
            "A tall curtain wall of dressed stone.", Cost(stone, 24), Cost(stone, 20), 1600);
        var castleGate = RankUp(db, "FortifiedGatehouse", "CastleGatehouse", "Castle Gatehouse", medieval, castles,
            "A gatehouse with towers. Opens for your people, holds against raiders.", Cost(stone, 50, iron, 8), Cost(stone, 40, iron, 6), 2240);
        castleWall.upgrades = new List<BuildingUpgrade>();
        castleGate.upgrades = new List<BuildingUpgrade>();

        var steelworks = NewBuilding(db, "Smithy", "Steelworks", "Steelworks", medieval, steelmaking, new Vector2Int(3, 3),
            "A blast house and water-driven bellows: fires Iron with Charcoal into Steel.",
            Cost(stone, 300, wood, 250, iron, 80), 420);
        steelworks.workers = 4;
        steelworks.production = new List<ResourceRate> { Rate(steel, 0.1f) };
        steelworks.inputs = new List<ResourceRate> { Rate(iron, 0.12f), Rate(charcoal, 0.1f) };
        steelworks.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Second Furnace", "+30% steel.", Cost(stone, 200, iron, 60), 1.3f),
            Upgrade("Trip Hammer", "+30% steel.", Cost(wood, 160, steel, 30, tools, 20), 1.3f),
        };

        var runeQuarry = NewBuilding(db, "IronMine", "RuneQuarry", "Rune Quarry", medieval, runecraft, new Vector2Int(3, 3),
            "Cuts runestone from the hills. Must be built on or next to a Runestone deposit.",
            Cost(wood, 220, stone, 160, iron, 60), 360);
        runeQuarry.requiredDeposit = runeDeposit;
        runeQuarry.canBuildOnHills = true;
        runeQuarry.workers = 4;
        runeQuarry.production = new List<ResourceRate> { Rate(runestone, 0.12f) };
        runeQuarry.inputs = new List<ResourceRate>();
        runeQuarry.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Rune Masons", "+30% runestone.", Cost(wood, 150, iron, 40), 1.3f),
            Upgrade("Glyph Saws", "+30% runestone.", Cost(steel, 40, tools, 20), 1.3f),
        };

        var pikeYard = NewBuilding(db, "SkirmisherCamp", "PikeYard", "Pike Yard", medieval, feudalism, new Vector2Int(3, 2),
            "A timbered barracks and drill yard. Trains Pikemen, whose long pikes strike first.",
            Cost(wood, 260, stone, 160, iron, 80), 520);
        Trains(pikeYard, pikeman, 4, steel, gold, food, coin);

        var runeShrine = NewBuilding(db, "Mint", "RuneShrine", "Rune Shrine", medieval, warding, new Vector2Int(2, 2),
            "A ring of carved stones. Trains Rune Wardens, who loose bolts of rune-light from their staves.",
            Cost(runestone, 120, stone, 200, gold, 60), 480);
        runeShrine.trainsUnit = warden;
        runeShrine.maxUnits = 2;
        runeShrine.workers = 2;
        runeShrine.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Deeper Carvings", description = "+1 warden.", requirements = new Requirements { cost = Cost(runestone, 80, gold, 40) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Ley Line", description = "Wardens hit 25% harder.", requirements = new Requirements { cost = Cost(runestone, 120, coin, 60) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        foreach (var b in new[] { townhouse, guildhall, armsHall, crossbowRange, knightsHall, monastery, castleTower, castleWall, castleGate, steelworks, runeQuarry, pikeYard, runeShrine })
            EditorUtility.SetDirty(b);

        // ---------- Mastery caps rise with the new age ----------
        foreach (var t in db.technologies)
            if (t != null && t.IsLevelled && t.extraLevelsPerAge == 0) { t.extraLevelsPerAge = 2; EditorUtility.SetDirty(t); }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art ----------
        runeDeposit.sprites = PixelDeposits("Runestone", 3, Ore.Rune);
        EditorUtility.SetDirty(runeDeposit);
        foreach (var b in new[] { townhouse, guildhall, armsHall, crossbowRange, knightsHall, monastery, castleTower, steelworks, runeQuarry, pikeYard, runeShrine })
            GenerateBuildingLooks(b);
        GenerateWallArt();
        GenerateWarriorsAndCrews(db);
        SetupBosses();
        GenerateIconArt();
        AssetDatabase.SaveAssets();
        Debug.Log("Medieval Age set up.");
    }

    // ---------- Helpers ----------

    private static Age MedievalAgeAsset(GameDatabase db)
    {
        const string path = "Assets/Data/Ages/MedievalAge.asset";
        var age = AssetDatabase.LoadAssetAtPath<Age>(path);
        if (age == null)
        {
            age = ScriptableObject.CreateInstance<Age>();
            AssetDatabase.CreateAsset(age, path);
        }
        age.displayName = "Medieval Age";
        age.order = 4;
        age.description = "Castles and kingdoms, knights and crossbows, steel and the old runes cut into the hills.";
        if (!db.ages.Contains(age)) db.ages.Add(age);
        EditorUtility.SetDirty(age);
        return age;
    }

    private static ResourceType MedievalResource(GameDatabase db, string id, string display, Age age, string after)
    {
        string path = $"{DataRoot}/Resources/{id}.asset";
        var res = AssetDatabase.LoadAssetAtPath<ResourceType>(path);
        if (res == null)
        {
            res = ScriptableObject.CreateInstance<ResourceType>();
            AssetDatabase.CreateAsset(res, path);
        }
        res.id = id;
        res.displayName = display;
        res.unlockedIn = age;
        EditorUtility.SetDirty(res);
        if (!db.resources.Contains(res))
        {
            int i = db.resources.FindIndex(r => r != null && r.id == after);
            db.resources.Insert(i < 0 ? db.resources.Count : i + 1, res);
        }
        return res;
    }

    private static DepositType RunestoneDeposit(GameDatabase db, ResourceType runestone)
    {
        const string path = "Assets/Data/Deposits/Runestone.asset";
        var dep = AssetDatabase.LoadAssetAtPath<DepositType>(path);
        if (dep == null)
        {
            dep = ScriptableObject.CreateInstance<DepositType>();
            AssetDatabase.CreateAsset(dep, path);
        }
        var iron = db.deposits.Find(d => d != null && d.id == "IronOre");
        dep.id = "Runestone";
        dep.displayName = "Runestone";
        dep.resource = runestone;
        dep.onHills = true;
        dep.patchScale = iron != null ? iron.patchScale : 0.22f;
        dep.rarity = 0.66f;   // measured: about 4% of hills, like iron (4.8%) and copper (3.6%)
        dep.minMoisture = 0f;
        dep.maxMoisture = 1f;
        // Always last: deposits are placed in list order, so a new one only fills empty tiles.
        db.deposits.Remove(dep);
        db.deposits.Add(dep);
        EditorUtility.SetDirty(dep);
        return dep;
    }

    /// <summary>A unit made from an existing one (its class, timing, projectile and so on), with new stats.</summary>
    private static UnitDefinition MedievalUnit(string id, string display, Age age, string template, int health, int attack,
        float cooldown, float range, float speed)
    {
        string path = $"Assets/Data/Units/{id}.asset";
        var src = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{template}.asset");
        var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<UnitDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        if (src != null) EditorUtility.CopySerialized(src, def);
        def.name = id;
        def.displayName = display;
        def.age = age;
        def.maxHealth = health;
        def.attackPower = attack;
        def.attackCooldown = cooldown;
        def.attackRange = range;
        def.moveSpeed = speed;
        def.isBoss = false;
        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>
    /// The Medieval building a Classical one ranks up into: starts as a copy of it (same footprint,
    /// tower crew offset, deposit rules...) with its own name, stats and costs.
    /// </summary>
    private static BuildingDefinition RankUp(GameDatabase db, string fromId, string id, string display, Age age, Technology needs,
        string description, List<ResourceAmount> cost, List<ResourceAmount> rankCost, int health)
    {
        var from = db.GetBuilding(fromId);
        var def = CopyBuilding(db, from, id, display, age, needs, description, cost, health, fromId);
        if (from != null)
        {
            from.rankUpTo = def;
            from.rankUpRequirements = new Requirements { cost = rankCost, technologies = new List<Technology> { needs } };
            from.rankUpTime = 40f;
            EditorUtility.SetDirty(from);
        }
        return def;
    }

    private static BuildingDefinition NewBuilding(GameDatabase db, string templateId, string id, string display, Age age, Technology needs,
        Vector2Int size, string description, List<ResourceAmount> cost, int health)
    {
        var def = CopyBuilding(db, db.GetBuilding(templateId), id, display, age, needs, description, cost, health, null);
        def.size = size;
        def.artSize = Vector2Int.zero;
        return def;
    }

    private static BuildingDefinition CopyBuilding(GameDatabase db, BuildingDefinition template, string id, string display, Age age,
        Technology needs, string description, List<ResourceAmount> cost, int health, string after)
    {
        string path = $"{DataRoot}/Buildings/{id}.asset";
        var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        if (template != null) EditorUtility.CopySerialized(template, def);
        def.name = id;
        def.id = id;
        def.displayName = display;
        def.description = description;
        def.age = age;
        def.maxHealth = health;
        def.requirements = new Requirements { cost = cost, technologies = new List<Technology> { needs } };
        def.rankUpTo = null;
        def.rankUpRequirements = new Requirements();
        def.looks = new List<BuildingLook>();
        def.icon = null;
        def.upgrades = new List<BuildingUpgrade>();
        def.production = new List<ResourceRate>();
        def.inputs = new List<ResourceRate>();
        def.trainsUnit = null;
        def.maxUnits = 0;
        def.housing = 0;
        def.requiredDeposit = null;
        if (template == null || template.towerRange <= 0) { def.towerRange = 0; def.operatorIdle = null; def.operatorShoot = new List<Sprite>(); }
        if (!def.IsWall) def.pieces = new List<Sprite>();
        if (!db.buildings.Contains(def))
        {
            string anchor = after ?? db.buildings.FindLast(b => b != null && b.age != null && b.age.order <= age.order)?.id;
            int i = db.buildings.FindIndex(b => b != null && b.id == anchor);
            db.buildings.Insert(i < 0 ? db.buildings.Count : i + 1, def);
        }
        return def;
    }

    /// <summary>A troop building: what it trains, how many, and its three upgrades.</summary>
    private static void Trains(BuildingDefinition b, UnitDefinition unit, int count, ResourceType steel, ResourceType gold, ResourceType food, ResourceType coin)
    {
        b.trainsUnit = unit;
        b.maxUnits = count;
        b.workers = Mathf.Max(3, b.workers);
        b.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Drill Yard", description = "+1 troop.", requirements = new Requirements { cost = Cost(food, 200, coin, 60) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Steel Harness", description = "Troops trained here are 20% tougher and hit 10% harder.",
                requirements = new Requirements { cost = Cost(steel, 60, coin, 60) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.2f, attackMultiplier = 1.1f } },
            new BuildingUpgrade { name = "Veteran Captains", description = "+1 troop, +200 health.", requirements = new Requirements { cost = Cost(gold, 60, steel, 40) },
                upgradeTime = 30f, extraUnits = 1, extraHealth = 200 },
        };
    }
}
