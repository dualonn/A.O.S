using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Industrial Age, with a steampunk layer: coal and steam, rifles and grenades, steam crawlers,
/// and its one special class, clockwork automatons, fired with aether. Steam Engine (Enlightenment research) opens it. Resources:
/// Coal (a common hill deposit), Gears (Steel and Coal at a Machine Works) and Aether (a rare glowing
/// crystal deposit, with rich veins in the wild). New deposits go last in the placement order, so
/// nothing already on the map moves. Enlightenment buildings rank up into Industrial ones; towers are
/// enclosed from here on (no crew in the open). Safe to run again. (Tools > Age of Stickman > Setup Industrial Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Industrial Age")]
    public static void SetupIndustrial() => SetupIndustrialCore(redrawUnits: true);

    /// <summary>Delete research, buildings and units that were cut, with their art.</summary>
    private static void RemoveContent(GameDatabase db, string[] techs, string[] buildings, string[] units)
    {
        foreach (var id in techs)
        {
            var t = db.technologies.Find(x => x != null && x.id == id);
            if (t == null) continue;
            db.technologies.Remove(t);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(t));
        }
        foreach (var id in buildings)
        {
            var b = db.GetBuilding(id);
            if (b == null) continue;
            foreach (var other in db.buildings)
                if (other != null && other.rankUpTo == b) { other.rankUpTo = null; EditorUtility.SetDirty(other); }
            db.buildings.Remove(b);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(b));
            AssetDatabase.DeleteAsset($"{Root}/Buildings/{id}");
        }
        foreach (var id in units)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{id}.asset");
            if (u == null) continue;
            foreach (var a in db.ages) if (a != null) { a.playerUnits.Remove(u); a.enemyUnits.Remove(u); EditorUtility.SetDirty(a); }
            AssetDatabase.DeleteAsset($"Assets/Data/Units/{id}.asset");
            AssetDatabase.DeleteAsset($"Assets/Prefabs/Units/{id}Unit.prefab");
            AssetDatabase.DeleteAsset($"{Root}/Units/{id}");
        }
    }

    /// <summary>Set up the Industrial Age; 'redrawUnits' also redraws its troops and chiefs.</summary>
    public static void SetupIndustrialCore(bool redrawUnits)
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age enlightenment = db.ages.Find(a => a != null && a.name == "EnlightenmentAge");
        var age = AgeAsset(db, "IndustrialAge", "Industrial Age", 7,
            "Coal smoke and iron rails, rifles and steam, clockwork men and the violet crackle of aether.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"),
            gunpowder = Res(db, "Gunpowder"), quicksilver = Res(db, "Quicksilver");
        var coal = Res(db, "Coal");   // arrives in the Age of Enlightenment, with coke smelting
        var gears = MedievalResource(db, "Gears", "Gears", age, "Coal");
        var aether = MedievalResource(db, "Aether", "Aether", age, "Gears");
        // Measured on three worlds (see the summary in the commit): Coal about as common as iron,
        // Aether rarer than gold, with rich veins in the wild.
        var aetherDeposit = NewDeposit(db, "Aether", "Aether", aether, true, 0.72f, 0f, 1f, -1f);
        aetherDeposit.richVeins = true;
        EditorUtility.SetDirty(aetherDeposit);

        // ---------- Research ----------
        var steam = Tech(db, "SteamEngine", "Steam Engine", enlightenment,
            "Fire, water and a piston: a machine that works without tiring. Opens the way to the Industrial Age.",
            120f, Cost(food, 1700, gold, 450, coin, 600, steel, 200, coal, 150), "Galvanism");
        steam.requirements.minimumAge = enlightenment;
        steam.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(steam);

        Technology ITech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
        {
            var t = Tech(db, id, name, age, description, time, cost, after);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>(needs);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            EditorUtility.SetDirty(t);
            return t;
        }
        // Coal mining moved to the Age of Enlightenment (Coke Smelting); drop the old Industrial item.
        var oldCoalMining = db.technologies.Find(t => t != null && t.id == "CoalMining");
        if (oldCoalMining != null)
        {
            db.technologies.Remove(oldCoalMining);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(oldCoalMining));
        }
        var coalMining = db.GetTechnology("CokeSmelting");
        var bessemer = ITech("BessemerProcess", "Bessemer Process", "Air blown through molten iron makes steel by the ton. Blast Furnaces rank up into Bessemer Works; steel comes 10% faster.",
            100f, Cost(steel, 200, coal, 150, gold, 100), "SteamEngine");
        bessemer.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var machining = ITech("PrecisionMachining", "Precision Machining", "Lathes and dies cut gears true to a hair. Unlocks the Machine Works.",
            100f, Cost(steel, 250, coal, 200, tools, 100), bessemer.id, bessemer);
        var rifling = ITech("Rifling", "Rifling", "Spiral grooves that make a ball fly true. Jäger Ranges rank up into the Rifle Range; all troops hit 4% harder.",
            100f, Cost(steel, 200, gunpowder, 120, gears, 20), machining.id, machining);
        rifling.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var grenades = ITech("Grenades", "Grenades", "Iron shells packed with powder, thrown by the biggest men in the army. Musketeer Barracks rank up into the Grenadier Depot.",
            90f, Cost(iron, 400, gunpowder, 150), rifling.id, rifling);
        var dragoons = ITech("Dragoons", "Dragoons", "Mounted riflemen who ride to the fight and shoot from the saddle. Hussar Stables rank up into Dragoon Stables.",
            90f, Cost(food, 1000, steel, 150, gunpowder, 60), grenades.id, rifling);
        var ironclad = ITech("IroncladWorks", "Ironclad Works", "Riveted iron plate over brick. Unlocks the Steam Gun Turret and ironclad walls; buildings have 8% more health.",
            110f, Cost(stone, 1500, steel, 300, gears, 40), dragoons.id, machining);
        ironclad.buildingHealthMultiplier = 1.08f;
        var railways = ITech("Railways", "Railways", "Iron roads and steam engines to pull the loads. Everything you produce comes in 5% faster; troops march 4% faster.",
            110f, Cost(steel, 400, coal, 300, wood, 800), ironclad.id, coalMining);
        railways.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        railways.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var jointStock = ITech("JointStock", "Joint-Stock Companies", "Shares in every venture. Royal Banks rank up into the Exchange; everyone pays a little more in taxes.",
            90f, Cost(gold, 500, coin, 400), railways.id);
        jointStock.coinPerPersonPerSecond = 0.002f;
        var antisepsis = ITech("Antisepsis", "Antisepsis", "Boiled linen and carbolic. Surgeons' Halls rank up into the Infirmary; all troops have 4% more health.",
            90f, Cost(food, 1000, gold, 200, coal, 100), jointStock.id);
        antisepsis.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var aetherTheory = ITech("AetherTheory", "Aether Theory", "The fifth element, caught in glass. Unlocks the Aether Mine; research goes 5% faster.",
            100f, Cost(quicksilver, 150, gold, 300, gears, 30), antisepsis.id, machining);
        aetherTheory.researchSpeedMultiplier = 1.05f;
        var crawlers = ITech("SteamCrawlers", "Steam Crawlers", "An iron hull on endless treads with a gun in its nose. Artillery Parks rank up into the Engine Works.",
            120f, Cost(steel, 400, gears, 80, coal, 200), aetherTheory.id, machining);
        var automatons = ITech("ClockworkAutomatons", "Clockwork Automatons", "Brass men wound with springs and woken by a galvanic jolt, aether in their hearts. Galvanic Labs rank up into the Automaton Workshop.",
            120f, Cost(gears, 120, aether, 60, steel, 200), crawlers.id, aetherTheory);
        // The age's one special class is the automaton: the aether engineer and his lab are gone.
        RemoveContent(db, techs: new[] { "AetherEngineering" }, buildings: new[] { "AetherLab", "AutomatonFoundry" }, units: new[] { "AetherEngineer" });

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { steam },
            cost = Cost(food, 6000, wood, 4500, stone, 4500, iron, 1500, gold, 800, coin, 900, steel, 500, gunpowder, 300, coal, 300),
            population = 550,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var rifleman = MedievalUnit("Rifleman", "Rifleman", age, "Arquebusier", 300, 58, 2.4f, 7.0f, 0.9f);
        var grenadier = MedievalUnit("Grenadier", "Grenadier", age, "Alchemist", 380, 52, 2.6f, 4.6f, 0.95f);
        var dragoon = MedievalUnit("Dragoon", "Dragoon", age, "Cuirassier", 640, 50, 2.2f, 4.4f, 1.7f);
        var surgeon = MedievalUnit("FieldSurgeon", "Field Surgeon", age, "Physician", 300, 34, 1.0f, 0.8f, 1.0f);
        surgeon.healer = true; surgeon.healRange = 11f;
        var crawler = MedievalUnit("SteamCrawler", "Steam Crawler", age, "FieldCannon", 900, 120, 4.2f, 8.5f, 0.6f);
        crawler.populationCost = 3;
        var automaton = MedievalUnit("Automaton", "Clockwork Automaton", age, "ManAtArms", 900, 60, 1.2f, 1.0f, 0.75f);
        automaton.populationCost = 2;
        foreach (var u in new[] { rifleman, grenadier, dragoon, surgeon })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.01f), Rate(coin, 0.06f) };
            EditorUtility.SetDirty(u);
        }
        // machines eat coal and aether, not bread
        crawler.upkeep = new List<ResourceRate> { Rate(coal, 0.04f), Rate(coin, 0.08f) };
        automaton.upkeep = new List<ResourceRate> { Rate(coal, 0.02f), Rate(aether, 0.004f), Rate(coin, 0.04f) };
        EditorUtility.SetDirty(crawler); EditorUtility.SetDirty(automaton);
        age.playerUnits = new List<UnitDefinition> { rifleman, grenadier, dragoon, surgeon, crawler, automaton };

        var saboteur = MedievalUnit("Saboteur", "Saboteur", age, "Brigand", 600, 54, 1.1f, 0.8f, 1.1f);
        var banditRifle = MedievalUnit("BanditRifleman", "Bandit Rifleman", age, "BrigandMusketeer", 300, 54, 2.5f, 6.6f, 0.9f);
        var brute = MedievalUnit("SteamBrute", "Steam Brute", age, "Landsknecht", 900, 66, 1.4f, 0.9f, 0.8f);
        age.enemyUnits = new List<UnitDefinition> { saboteur, banditRifle, brute };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var terrace = RankUp(db, "GeorgianTownhouse", "Terrace", "Terrace", age, railways,
            "A row of narrow brick houses with chimney pots. Houses 72 people.",
            Cost(stone, 400, wood, 200, steel, 40), Cost(stone, 200, wood, 100, steel, 30), 900);
        terrace.housing = 72;
        terrace.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Attic Rooms", description = "+12 people.", requirements = new Requirements { cost = Cost(stone, 160, wood, 120) }, upgradeTime = 25f, extraHousing = 12 },
            new BuildingUpgrade { name = "Gas Lighting", description = "+12 people.", requirements = new Requirements { cost = Cost(steel, 40, coal, 60) }, upgradeTime = 25f, extraHousing = 12 },
        };

        var exchange = RankUp(db, "RoyalBank", "Exchange", "Exchange", age, jointStock,
            "Columns, a glass-and-iron vault and a ticking clock: shares change hands all day, and coin pours in.",
            Cost(stone, 400, gold, 150, steel, 40), Cost(stone, 200, gold, 80), 800);
        exchange.workers = 3;
        exchange.production = new List<ResourceRate> { Rate(coin, 1.8f) };
        exchange.inputs = new List<ResourceRate> { Rate(gold, 0.07f) };
        exchange.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Ticker Tape", "+25% coin.", Cost(gears, 30, coin, 200), 1.25f),
            Upgrade("Trading Floor", "+25% coin.", Cost(gold, 150, steel, 60), 1.25f),
        };

        var depot = RankUp(db, "MusketeerBarracks", "GrenadierDepot", "Grenadier Depot", age, grenades,
            "Brick barracks with an iron-bound magazine. Trains Grenadiers, whose bombs burst among the enemy.",
            Cost(stone, 400, wood, 220, steel, 100, gunpowder, 60), Cost(stone, 200, steel, 60), 1100);
        Trains(depot, grenadier, 4, steel, gold, food, coin);

        var rifleRange = RankUp(db, "JagerRange", "RifleRange", "Rifle Range", age, rifling,
            "A covered firing shed and iron targets far down the range. Trains Riflemen: long reach, true aim.",
            Cost(wood, 360, stone, 240, steel, 80, gunpowder, 60), Cost(wood, 180, steel, 40), 800);
        Trains(rifleRange, rifleman, 4, steel, gold, food, coin);

        var dragoonStables = RankUp(db, "HussarStables", "DragoonStables", "Dragoon Stables", age, dragoons,
            "Long brick stables with iron-framed doors. Trains Dragoons, who ride fast and shoot from the saddle.",
            Cost(stone, 380, wood, 220, steel, 100, food, 400), Cost(stone, 190, steel, 60), 800);
        Trains(dragoonStables, dragoon, 3, steel, gold, food, coin);

        var infirmary = RankUp(db, "SurgeonsHall", "Infirmary", "Infirmary", age, antisepsis,
            "A tall brick infirmary with clean linen boiled by steam. Trains Field Surgeons, the best healers yet.",
            Cost(stone, 420, wood, 200, gold, 80, coal, 60), Cost(stone, 210, gold, 50), 800);
        infirmary.trainsUnit = surgeon;
        infirmary.maxUnits = 3;
        infirmary.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Surgical Ward", description = "+1 surgeon.", requirements = new Requirements { cost = Cost(food, 300, stone, 160) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Ether", description = "Surgeons heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(aether, 30, gold, 80) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var turret = RankUp(db, "CasemateBattery", "SteamGunTurret", "Steam Gun Turret", age, ironclad,
            "An enclosed iron turret on a brick base. Its gunners work in safety inside; the shells burst among the raiders.",
            Cost(stone, 450, steel, 120, gears, 30), Cost(stone, 220, steel, 60, gears, 20), 1300);
        turret.workers = 2;
        turret.towerRange = 12f;
        turret.towerDamage = 150;
        turret.towerCooldown = 3.0f;
        turret.operatorIdle = null;
        turret.operatorShoot = new List<Sprite>();
        turret.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Cannonball.png");
        turret.towerProjectileArc = 0.04f;
        turret.towerProjectileSpeed = 18f;
        turret.towerSplash = 1.8f;
        turret.towerMuzzleSmoke = true;
        turret.towerMuzzle = new Vector2(62f / TilePx, 35.5f / TilePx);   // the barrel's mouth in the turret art
        turret.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Rifled Barrel", description = "+2 range.", requirements = new Requirements { cost = Cost(steel, 60, gears, 20) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "High Explosive", description = "+50 damage.", requirements = new Requirements { cost = Cost(gunpowder, 80, steel, 40) }, upgradeTime = 25f, extraTowerDamage = 50 },
            new BuildingUpgrade { name = "Armour Plate", description = "+700 health.", requirements = new Requirements { cost = Cost(steel, 120, stone, 200) }, upgradeTime = 30f, extraHealth = 700 },
        };

        var ironWall = RankUp(db, "StarFortWall", "IroncladWall", "Ironclad Wall", age, ironclad,
            "Brick faced with riveted iron plate.", Cost(stone, 30, iron, 6), Cost(stone, 24, iron, 6), 3000);
        var ironGate = RankUp(db, "StarFortGate", "IroncladGate", "Ironclad Gate", age, ironclad,
            "Iron-plated doors between armoured posts.", Cost(stone, 60, iron, 20), Cost(stone, 48, iron, 16), 4200);
        ironWall.upgrades = new List<BuildingUpgrade>();
        ironGate.upgrades = new List<BuildingUpgrade>();

        var bessemerWorks = RankUp(db, "BlastFurnace", "BessemerWorks", "Bessemer Works", age, bessemer,
            "A steel mill with a roaring converter: steel by the ton.",
            Cost(stone, 400, steel, 120, iron, 200), Cost(stone, 200, steel, 60), 900);
        bessemerWorks.workers = 5;
        bessemerWorks.production = new List<ResourceRate> { Rate(steel, 0.24f) };   // works 0.1, blast furnace 0.15
        bessemerWorks.inputs = new List<ResourceRate> { Rate(iron, 0.18f), Rate(coal, 0.12f) };
        bessemerWorks.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Second Converter", "+30% steel.", Cost(steel, 100, stone, 200), 1.3f),
            Upgrade("Rolling Mill", "+30% steel.", Cost(gears, 40, steel, 80), 1.3f),
        };

        var engineWorks = RankUp(db, "ArtilleryPark", "EngineWorks", "Engine Works", age, crawlers,
            "A great engine shed with rails out of its doors. Builds Steam Crawlers: armoured, slow, and their shells burst.",
            Cost(stone, 500, steel, 200, gears, 60), Cost(stone, 250, steel, 100, gears, 30), 1100);
        engineWorks.trainsUnit = crawler;
        engineWorks.maxUnits = 2;
        engineWorks.workers = 5;
        engineWorks.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Slip", description = "+1 crawler.", requirements = new Requirements { cost = Cost(steel, 160, gears, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Armoured Hulls", description = "Crawlers are 25% tougher.", requirements = new Requirements { cost = Cost(steel, 200, gears, 40) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };

        var machineWorks = NewBuilding(db, "Steelworks", "MachineWorks", "Machine Works", age, machining, new Vector2Int(3, 3),
            "A machine shop of lathes and belts: turns Steel and Coal into Gears.",
            Cost(stone, 360, wood, 240, steel, 100), 600);
        machineWorks.workers = 5;
        machineWorks.production = new List<ResourceRate> { Rate(gears, 0.1f) };
        machineWorks.inputs = new List<ResourceRate> { Rate(steel, 0.06f), Rate(coal, 0.1f) };
        machineWorks.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Line Shafting", "+30% gears.", Cost(steel, 80, wood, 160), 1.3f),
            Upgrade("Steam Hammers", "+30% gears.", Cost(gears, 40, coal, 120), 1.3f),
        };

        var aetherMine = NewBuilding(db, "QuicksilverMine", "AetherMine", "Aether Mine", age, aetherTheory, new Vector2Int(3, 3),
            "A drift into the hill, lit violet. Must be built on or next to Aether, or right on a rich aether vein found in the wild.",
            Cost(wood, 300, stone, 240, steel, 80, gears, 20), 500);
        aetherMine.requiredDeposit = aetherDeposit;
        aetherMine.canBuildOnHills = true;
        aetherMine.workers = 4;
        aetherMine.production = new List<ResourceRate> { Rate(aether, 0.08f) };
        aetherMine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Brass Pumps", "+30% aether.", Cost(gears, 40, steel, 60), 1.3f),
            Upgrade("Resonance Drills", "+30% aether.", Cost(aether, 40, quicksilver, 60), 1.3f),
        };

        // The magic line ends here: runes, alchemy, galvanism, then lightning that wakes clockwork men.
        var automatonWorkshop = RankUp(db, "GalvanicLab", "AutomatonWorkshop", "Automaton Workshop", age, automatons,
            "A brass-domed workshop under crackling coils. Builds Clockwork Automatons: slow, heavy and very hard to stop.",
            Cost(stone, 400, steel, 160, gears, 100, aether, 40), Cost(steel, 120, gears, 60, aether, 20), 850);
        automatonWorkshop.trainsUnit = automaton;
        automatonWorkshop.maxUnits = 3;
        automatonWorkshop.workers = 4;
        automatonWorkshop.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Assembly Line", description = "+1 automaton.", requirements = new Requirements { cost = Cost(gears, 100, steel, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Aether Cores", description = "Automatons hit 25% harder.", requirements = new Requirements { cost = Cost(aether, 80, gears, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var all = new[] { terrace, exchange, depot, rifleRange, dragoonStables, infirmary, turret, ironWall, ironGate, bessemerWorks, engineWorks, machineWorks, aetherMine, automatonWorkshop };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art ----------
        aetherDeposit.sprites = PixelDeposits("Aether", 3, Ore.Aether);
        EditorUtility.SetDirty(aetherDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        // Redraw only what this age adds.
        GenerateWallArt(7);
        if (redrawUnits)
        {
            GenerateWarriorsAndCrews(db, new HashSet<string>
                { "Rifleman", "Grenadier", "Dragoon", "FieldSurgeon", "SteamCrawler", "Automaton", "Saboteur", "BanditRifleman", "SteamBrute" });
            SetupBosses("IndustrialAge");
        }
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Industrial Age set up.");
    }
}
