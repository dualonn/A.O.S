using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Modern Age (about 1900-1945), with a dieselpunk layer: bolt-action rifles and machine guns,
/// armoured cars and tanks, concrete pillboxes, oil and fuel, and its special class, the diesel
/// war-walker (the automaton's successor). Internal Combustion (Industrial research) opens it.
/// Every Industrial line ranks up into a Modern building. Oil is a new dry-land deposit, placed last
/// so nothing on the map moves. Safe to run again. (Tools > Age of Stickman > Setup Modern Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Modern Age")]
    public static void SetupModern()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age industrial = db.ages.Find(a => a != null && a.name == "IndustrialAge");
        var age = AgeAsset(db, "ModernAge", "Modern Age", 8,
            "Barbed wire and diesel smoke: machine guns, tanks and walking war-machines, oil wells and concrete.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"),
            gunpowder = Res(db, "Gunpowder"), coal = Res(db, "Coal"), gears = Res(db, "Gears"), aether = Res(db, "Aether");
        var oil = MedievalResource(db, "Oil", "Oil", age, "Aether");
        var fuel = MedievalResource(db, "Fuel", "Fuel", age, "Oil");
        // Seeps of crude on dry ground, about as common as saltpetre (measured on three worlds).
        var oilDeposit = NewDeposit(db, "Oil", "Oil", oil, false, 0.78f, 0f, 0.45f, 0.2f);

        // ---------- Research ----------
        var combustion = Tech(db, "InternalCombustion", "Internal Combustion", industrial,
            "Fuel burned inside the cylinder: engines small enough for a carriage. Opens the way to the Modern Age.",
            125f, Cost(food, 1900, gold, 500, coin, 700, steel, 300, gears, 120), "ClockworkAutomatons");
        combustion.requirements.minimumAge = industrial;
        combustion.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(combustion);

        Technology MTech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var petroleum = MTech("Petroleum", "Petroleum", "Rock oil pumped from the ground and boiled into fuel. Unlocks the Oil Well and the Refinery.",
            100f, Cost(steel, 300, gears, 80, coal, 300), "InternalCombustion");
        var boltAction = MTech("BoltAction", "Bolt-Action Rifles", "Five rounds in the magazine and a bolt to work them. Grenadier Depots rank up into Infantry Barracks; all troops hit 4% harder.",
            100f, Cost(steel, 300, gunpowder, 200), petroleum.id);
        boltAction.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var machineGuns = MTech("MachineGuns", "Machine Guns", "Water-cooled and belt-fed: a hail of bullets. Rifle Ranges rank up into the Machine-Gun School.",
            110f, Cost(steel, 350, gunpowder, 250, gears, 60), boltAction.id, boltAction);
        var motors = MTech("MotorVehicles", "Motor Vehicles", "Carriages that need no horse. Dragoon Stables rank up into the Motor Pool; troops march 4% faster.",
            100f, Cost(steel, 300, fuel, 100, gears, 60), machineGuns.id, petroleum);
        motors.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var penicillin = MTech("Penicillin", "Penicillin", "A mould that kills infection. Infirmaries rank up into Field Hospitals; all troops have 4% more health.",
            95f, Cost(food, 1200, gold, 250), motors.id);
        penicillin.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var concrete = MTech("ReinforcedConcrete", "Reinforced Concrete", "Poured stone around steel rods. Unlocks the Pillbox and concrete walls; buildings have 8% more health.",
            110f, Cost(stone, 2000, steel, 400), penicillin.id);
        concrete.buildingHealthMultiplier = 1.08f;
        var openHearth = MTech("OpenHearth", "Open-Hearth Steel", "Great furnaces that make steel in batches of fifty tons. Bessemer Works rank up into the Steel Mill; steel comes 10% faster.",
            105f, Cost(steel, 300, coal, 400, fuel, 60), concrete.id);
        openHearth.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var assembly = MTech("AssemblyLine", "Assembly Line", "Each man does one job, over and over. Machine Works rank up into the Factory; everything you produce comes in 5% faster.",
            110f, Cost(steel, 300, gears, 150, coal, 200), openHearth.id);
        assembly.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var tanks = MTech("Tanks", "Tanks", "A landship of armour plate on caterpillar tracks. Engine Works rank up into the Tank Factory.",
            125f, Cost(steel, 500, fuel, 200, gears, 120), assembly.id, motors);
        var finance = MTech("MassFinance", "Mass Finance", "Savings banks, bonds and a central bank. Exchanges rank up into the Central Bank; everyone pays a little more in taxes.",
            90f, Cost(gold, 600, coin, 500), tanks.id);
        finance.coinPerPersonPerSecond = 0.002f;
        var urban = MTech("UrbanPlanning", "Urban Planning", "Streets laid out on paper, sewers under them, flats stacked high. Terraces rank up into Apartment Blocks.",
            95f, Cost(stone, 1500, steel, 200, wood, 800), finance.id);
        var radio = MTech("Radio", "Radio", "Voices through the air. Research goes 5% faster.",
            100f, Cost(gold, 400, gears, 100, coin, 400), urban.id);
        radio.researchSpeedMultiplier = 1.05f;
        var walkers = MTech("DieselWalkers", "Diesel Walkers", "The automaton grown huge, with a pilot in its chest and a machine gun in its arms. Automaton Workshops rank up into Walker Works.",
            125f, Cost(steel, 400, fuel, 150, gears, 150, aether, 60), radio.id, tanks);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { combustion },
            cost = Cost(food, 7000, wood, 5000, stone, 5500, iron, 1800, gold, 900, coin, 1100, steel, 700, coal, 500, gears, 200),
            population = 650,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var infantry = MedievalUnit("Infantryman", "Infantryman", age, "Rifleman", 340, 62, 2.2f, 7.2f, 0.95f);
        var mg = MedievalUnit("MachineGunner", "Machine Gunner", age, "Rifleman", 360, 15, 0.45f, 5.8f, 0.85f);
        var car = MedievalUnit("ArmouredCar", "Armoured Car", age, "Dragoon", 900, 16, 0.5f, 5.0f, 1.9f);
        car.populationCost = 2;
        var medic = MedievalUnit("Medic", "Medic", age, "FieldSurgeon", 340, 38, 1.0f, 0.8f, 1.05f);
        medic.healer = true; medic.healRange = 12f;
        var tank = MedievalUnit("Tank", "Tank", age, "SteamCrawler", 1400, 150, 3.8f, 9.0f, 0.7f);
        tank.populationCost = 3;
        var walker = MedievalUnit("DieselWalker", "Diesel Walker", age, "Automaton", 1300, 18, 0.5f, 5.0f, 0.8f);
        walker.populationCost = 2;
        foreach (var u in new[] { infantry, mg, medic })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.012f), Rate(coin, 0.065f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { car, tank, walker })   // engines run on fuel, not bread
        {
            u.upkeep = new List<ResourceRate> { Rate(fuel, u == tank ? 0.04f : 0.025f), Rate(coin, 0.07f) };
            EditorUtility.SetDirty(u);
        }
        walker.upkeep.Add(Rate(aether, 0.003f));
        age.playerUnits = new List<UnitDefinition> { infantry, mg, car, medic, tank, walker };

        var gangster = MedievalUnit("Gangster", "Gangster", age, "BanditRifleman", 520, 12, 0.45f, 4.5f, 1.0f);
        var trench = MedievalUnit("TrenchRaider", "Trench Raider", age, "Saboteur", 700, 62, 1.1f, 0.8f, 1.1f);
        var sniper = MedievalUnit("BanditSniper", "Bandit Sniper", age, "BanditRifleman", 330, 64, 2.4f, 7.5f, 0.9f);
        age.enemyUnits = new List<UnitDefinition> { gangster, trench, sniper };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var apartment = RankUp(db, "Terrace", "ApartmentBlock", "Apartment Block", age, urban,
            "A tall brick block of flats with steel-framed windows. Houses 90 people.",
            Cost(stone, 480, steel, 80, wood, 150), Cost(stone, 240, steel, 40), 1050);
        apartment.housing = 90;
        apartment.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Extra Storey", description = "+15 people.", requirements = new Requirements { cost = Cost(stone, 200, steel, 40) }, upgradeTime = 25f, extraHousing = 15 },
            new BuildingUpgrade { name = "Lifts", description = "+15 people.", requirements = new Requirements { cost = Cost(steel, 80, gears, 30) }, upgradeTime = 25f, extraHousing = 15 },
        };

        var centralBank = RankUp(db, "Exchange", "CentralBank", "Central Bank", age, finance,
            "An art-deco tower of stone and gilt. Coin pours out of it.",
            Cost(stone, 450, gold, 180, steel, 60), Cost(stone, 220, gold, 90), 900);
        centralBank.workers = 3;
        centralBank.production = new List<ResourceRate> { Rate(coin, 2.2f) };
        centralBank.inputs = new List<ResourceRate> { Rate(gold, 0.075f) };
        centralBank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("War Bonds", "+25% coin.", Cost(gold, 150, coin, 250), 1.25f),
            Upgrade("Vault Doors", "+25% coin.", Cost(steel, 100, gold, 100), 1.25f),
        };

        var infantryBarracks = RankUp(db, "GrenadierDepot", "InfantryBarracks", "Infantry Barracks", age, boltAction,
            "Rows of brick barracks and a sandbagged firing step. Trains Infantrymen with bolt-action rifles.",
            Cost(stone, 450, steel, 120, gunpowder, 80), Cost(stone, 220, steel, 70), 1200);
        Trains(infantryBarracks, infantry, 4, steel, gold, food, coin);

        var mgSchool = RankUp(db, "RifleRange", "MachineGunSchool", "Machine-Gun School", age, machineGuns,
            "A sandbagged firing line facing steel targets. Trains Machine Gunners: short reach, a hail of bullets.",
            Cost(stone, 380, steel, 140, gunpowder, 100), Cost(stone, 190, steel, 70), 900);
        Trains(mgSchool, mg, 4, steel, gold, food, coin);

        var motorPool = RankUp(db, "DragoonStables", "MotorPool", "Motor Pool", age, motors,
            "A corrugated garage and a fuel pump. Builds Armoured Cars: fast, with a machine gun on top.",
            Cost(steel, 260, gears, 80, fuel, 60), Cost(steel, 130, gears, 40), 950);
        Trains(motorPool, car, 3, steel, gold, food, coin);

        var fieldHospital = RankUp(db, "Infirmary", "FieldHospital", "Field Hospital", age, penicillin,
            "Ward tents and a concrete treatment hut. Trains Medics.",
            Cost(stone, 420, wood, 200, gold, 90), Cost(stone, 210, gold, 50), 900);
        fieldHospital.trainsUnit = medic;
        fieldHospital.maxUnits = 3;
        fieldHospital.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Ambulance", description = "+1 medic.", requirements = new Requirements { cost = Cost(fuel, 60, steel, 60) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Blood Bank", description = "Medics heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(food, 400, gold, 100) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var pillbox = RankUp(db, "SteamGunTurret", "Pillbox", "Pillbox", age, concrete,
            "A squat concrete pillbox. Its machine gun swings to meet the raiders and sprays them with bullets.",
            Cost(stone, 500, steel, 140, gears, 30), Cost(stone, 250, steel, 70), 1600);
        pillbox.workers = 2;
        pillbox.towerRange = 9.5f;
        pillbox.towerDamage = 34;
        pillbox.towerCooldown = 0.55f;
        pillbox.operatorIdle = null;
        pillbox.operatorShoot = new List<Sprite>();
        pillbox.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Bullet.png");
        pillbox.towerProjectileArc = 0f;
        pillbox.towerProjectileSpeed = 30f;
        pillbox.towerSplash = 0f;
        pillbox.towerMuzzleSmoke = false;
        pillbox.towerMuzzle = new Vector2(60f / TilePx, 14.5f / TilePx);
        pillbox.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Twin Guns", description = "+12 damage.", requirements = new Requirements { cost = Cost(steel, 100, gunpowder, 60) }, upgradeTime = 25f, extraTowerDamage = 12 },
            new BuildingUpgrade { name = "Range Finder", description = "+2 range.", requirements = new Requirements { cost = Cost(gears, 40, steel, 40) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Thicker Walls", description = "+800 health.", requirements = new Requirements { cost = Cost(stone, 400) }, upgradeTime = 30f, extraHealth = 800 },
        };

        var concreteWall = RankUp(db, "IroncladWall", "ConcreteWall", "Concrete Wall", age, concrete,
            "Poured concrete with barbed wire along the top.", Cost(stone, 34, steel, 4), Cost(stone, 28, steel, 4), 3400);
        var concreteGate = RankUp(db, "IroncladGate", "ConcreteGate", "Concrete Gate", age, concrete,
            "Steel doors between concrete posts.", Cost(stone, 66, steel, 16), Cost(stone, 52, steel, 12), 4700);
        concreteWall.upgrades = new List<BuildingUpgrade>();
        concreteGate.upgrades = new List<BuildingUpgrade>();

        var steelMill = RankUp(db, "BessemerWorks", "SteelMill", "Steel Mill", age, openHearth,
            "A long shed of open-hearth furnaces under a row of stacks. Steel by the trainload.",
            Cost(stone, 500, steel, 200, iron, 300), Cost(stone, 250, steel, 100), 1050);
        steelMill.workers = 6;
        steelMill.production = new List<ResourceRate> { Rate(steel, 0.34f) };
        steelMill.inputs = new List<ResourceRate> { Rate(iron, 0.24f), Rate(coal, 0.14f) };
        steelMill.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Oxygen Lances", "+30% steel.", Cost(steel, 140, gears, 40), 1.3f),
            Upgrade("Strip Mill", "+30% steel.", Cost(steel, 200, fuel, 60), 1.3f),
        };

        var factory = RankUp(db, "MachineWorks", "Factory", "Factory", age, assembly,
            "An assembly-line factory with great steel windows. Gears and parts by the crate.",
            Cost(stone, 450, steel, 160, gears, 60), Cost(stone, 220, steel, 80), 900);
        factory.workers = 6;
        factory.production = new List<ResourceRate> { Rate(gears, 0.17f) };
        factory.inputs = new List<ResourceRate> { Rate(steel, 0.09f), Rate(coal, 0.12f) };
        factory.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Conveyor Belts", "+30% gears.", Cost(steel, 120, gears, 40), 1.3f),
            Upgrade("Electric Motors", "+30% gears.", Cost(gears, 80, fuel, 60), 1.3f),
        };

        var tankFactory = RankUp(db, "EngineWorks", "TankFactory", "Tank Factory", age, tanks,
            "A huge assembly hall with tanks rolling out of its doors. Builds Tanks: heavy armour and a long gun.",
            Cost(stone, 600, steel, 300, gears, 120, fuel, 80), Cost(stone, 300, steel, 150, gears, 60), 1400);
        tankFactory.trainsUnit = tank;
        tankFactory.maxUnits = 2;
        tankFactory.workers = 6;
        tankFactory.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Line", description = "+1 tank.", requirements = new Requirements { cost = Cost(steel, 200, gears, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Sloped Armour", description = "Tanks are 25% tougher.", requirements = new Requirements { cost = Cost(steel, 260, fuel, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };

        var walkerWorks = RankUp(db, "AutomatonWorkshop", "WalkerWorks", "Walker Works", age, walkers,
            "A tall gantry shed where diesel walkers are bolted together. The automaton, grown huge, with a pilot in its chest.",
            Cost(stone, 450, steel, 220, gears, 140, aether, 50), Cost(steel, 150, gears, 80, aether, 25), 1000);
        walkerWorks.trainsUnit = walker;
        walkerWorks.maxUnits = 3;
        walkerWorks.workers = 4;
        walkerWorks.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Gantry", description = "+1 walker.", requirements = new Requirements { cost = Cost(steel, 160, gears, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Aether Pistons", description = "Walkers are 20% faster and 20% tougher.", requirements = new Requirements { cost = Cost(aether, 80, gears, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { speedMultiplier = 1.2f, healthMultiplier = 1.2f } },
        };

        var oilWell = NewBuilding(db, "QuicksilverMine", "OilWell", "Oil Well", age, petroleum, new Vector2Int(2, 2),
            "A nodding pump jack. Must be built on or next to Oil (black seeps on dry ground).",
            Cost(steel, 120, wood, 150, gears, 30), 400);
        oilWell.requiredDeposit = oilDeposit;
        oilWell.canBuildOnHills = false;
        oilWell.minDepositTiles = 2;
        oilWell.fullOutputTiles = 6;
        oilWell.catchmentRadius = 2;
        oilWell.workers = 2;
        oilWell.production = new List<ResourceRate> { Rate(oil, 0.2f) };
        oilWell.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Deeper Bore", "+30% oil.", Cost(steel, 80, gears, 30), 1.3f),
            Upgrade("Diesel Pump", "+30% oil.", Cost(fuel, 40, steel, 60), 1.3f),
        };

        var refinery = NewBuilding(db, "Steelworks", "Refinery", "Refinery", age, petroleum, new Vector2Int(3, 3),
            "Distillation towers and a flare stack: boils Oil into Fuel.",
            Cost(steel, 260, stone, 300, gears, 60), 700);
        refinery.workers = 4;
        refinery.production = new List<ResourceRate> { Rate(fuel, 0.14f) };
        refinery.inputs = new List<ResourceRate> { Rate(oil, 0.18f) };
        refinery.runsWithoutInputs = 0f;
        refinery.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Cracking Tower", "+30% fuel.", Cost(steel, 140, gears, 40), 1.3f),
            Upgrade("Catalysts", "+30% fuel.", Cost(gold, 80, steel, 80), 1.3f),
        };

        var all = new[] { apartment, centralBank, infantryBarracks, mgSchool, motorPool, fieldHospital, pillbox, concreteWall, concreteGate, steelMill, factory, tankFactory, walkerWorks, oilWell, refinery };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds, plus the crawler whose crew no longer shows legs) ----------
        oilDeposit.sprites = PixelDeposits("Oil", 3, Ore.Oil);
        EditorUtility.SetDirty(oilDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(8);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Infantryman", "MachineGunner", "ArmouredCar", "Medic", "Tank", "DieselWalker", "Gangster", "TrenchRaider", "BanditSniper", "SteamCrawler" });
        SetupBosses("ModernAge");
        GenerateIconArt();
        AssetDatabase.SaveAssets();
        Debug.Log("Modern Age set up.");
    }
}
