using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Electric Age, retro-futuristic (the 1950s and 60s seen through a ray gun): ray troopers in bubble
/// helmets, hover cars and hover tanks, rocketeers on jetpacks, Tesla coils and laser towers, force-field
/// walls, atomic power, and its special class, the giant robot (the walker's successor). Atomic Theory
/// (Modern research) opens it. Uranium is a new rare hill deposit with rich veins; Power Cells are made
/// from it. Every Modern line ranks up into an Electric building. Safe to run again.
/// (Tools > Age of Stickman > Setup Electric Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Electric Age")]
    public static void SetupElectric()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age modern = db.ages.Find(a => a != null && a.name == "ModernAge");
        var age = AgeAsset(db, "ElectricAge", "Electric Age", 9,
            "Chrome and neon: ray guns, hover tanks, rocket packs, giant robots and the green glow of the atom.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            aether = Res(db, "Aether"), fuel = Res(db, "Fuel"), coal = Res(db, "Coal");
        var uranium = MedievalResource(db, "Uranium", "Uranium", age, "Fuel");
        var cells = MedievalResource(db, "PowerCells", "Power Cells", age, "Uranium");
        // Rarer than aether (it has rich veins in the wild too).
        var uraniumDeposit = NewDeposit(db, "Uranium", "Uranium", uranium, true, 0.74f, 0f, 1f, -1f);
        uraniumDeposit.richVeins = true;
        EditorUtility.SetDirty(uraniumDeposit);

        // ---------- Research ----------
        var atomic = Tech(db, "AtomicTheory", "Atomic Theory", modern,
            "Split the atom and power a city. Opens the way to the Electric Age.",
            130f, Cost(food, 2100, gold, 600, coin, 800, steel, 400, fuel, 200), "DieselWalkers");
        atomic.requirements.minimumAge = modern;
        atomic.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(atomic);

        Technology ETech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var atomicPower = ETech("AtomicPower", "Atomic Power", "Uranium dug from the hills and fed to a reactor. Unlocks the Uranium Mine and the Atomic Reactor.",
            110f, Cost(steel, 400, gears, 150, fuel, 150), "AtomicTheory");
        var rayGuns = ETech("RayGuns", "Ray Guns", "A bolt of light in place of a bullet. Infantry Barracks rank up into the Trooper Academy; all troops hit 4% harder.",
            110f, Cost(steel, 350, cells, 60, gears, 80), atomicPower.id, atomicPower);
        rayGuns.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var blasters = ETech("Blasters", "Blasters", "Ray guns that fire as fast as you can pull the trigger. Machine-Gun Schools rank up into the Blaster Range.",
            110f, Cost(steel, 350, cells, 80), rayGuns.id, rayGuns);
        var hover = ETech("Hovercraft", "Hovercraft", "Cars that float on a cushion of force. Motor Pools rank up into the Hover Pool; troops move 4% faster.",
            110f, Cost(steel, 400, cells, 80, gears, 100), blasters.id, atomicPower);
        hover.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var rayMedicine = ETech("RayMedicine", "Ray Medicine", "Healing lamps that knit flesh. Field Hospitals rank up into the Medi-Lab; all troops have 4% more health.",
            100f, Cost(food, 1500, gold, 300, cells, 40), hover.id);
        rayMedicine.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var tesla = ETech("TeslaCoils", "Tesla Coils", "Lightning on tap. Pillboxes rank up into Tesla Coils, concrete walls into force fields; buildings have 8% more health.",
            115f, Cost(steel, 400, cells, 100, gears, 100), rayMedicine.id, atomicPower);
        tesla.buildingHealthMultiplier = 1.08f;
        var lasers = ETech("Lasers", "Lasers", "A beam of light so tight it cuts steel. Sniper Nests rank up into Laser Towers.",
            110f, Cost(steel, 300, cells, 100, gold, 200), tesla.id, rayGuns);
        var atomicCannons = ETech("AtomicCannons", "Atomic Cannons", "Shells with a spark of the atom inside. Gun Emplacements rank up into Atomic Cannons.",
            120f, Cost(steel, 500, uranium, 80, cells, 80), lasers.id, atomicPower);
        var arc = ETech("ArcFurnaces", "Arc Furnaces", "Melt the steel with lightning. Steel Mills rank up into Arc Furnaces; steel comes 10% faster.",
            110f, Cost(steel, 400, cells, 80), atomicCannons.id, atomicPower);
        arc.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var automation = ETech("Automation", "Automation", "Robot arms that never tire. Factories rank up into the Automated Plant; everything you produce comes in 5% faster.",
            115f, Cost(steel, 400, gears, 200, cells, 60), arc.id);
        automation.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var hoverTanks = ETech("HoverTanks", "Hover Tanks", "A tank that floats. Tank Factories rank up into Hover Tank Works.",
            125f, Cost(steel, 600, cells, 150, gears, 150), automation.id, hover);
        var robotics = ETech("Robotics", "Robotics", "The walker loses its pilot and gains a brain. Walker Works rank up into the Robot Foundry.",
            130f, Cost(steel, 500, gears, 200, cells, 120, aether, 80), hoverTanks.id, automation);
        var rocketry = ETech("Rocketry", "Rocketry", "A rocket on your back. Airfields rank up into the Rocket Pad.",
            120f, Cost(steel, 400, fuel, 200, cells, 100), robotics.id, hover);
        var skyscrapers = ETech("Skyscrapers", "Skyscrapers", "Steel frames and glass curtains. Apartment Blocks rank up into Skyline Towers.",
            100f, Cost(steel, 500, stone, 1500, gold, 200), rocketry.id);
        var credit = ETech("ConsumerCredit", "Consumer Credit", "Buy now, pay later. Central Banks rank up into the Sky Bank; everyone pays a little more in taxes.",
            100f, Cost(gold, 700, coin, 600), skyscrapers.id);
        credit.coinPerPersonPerSecond = 0.002f;
        var television = ETech("Television", "Television", "Pictures through the air. Research goes 5% faster.",
            110f, Cost(gold, 500, cells, 80, coin, 500), credit.id);
        television.researchSpeedMultiplier = 1.05f;

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { atomic },
            cost = Cost(food, 8000, wood, 5500, stone, 6500, iron, 2000, gold, 1000, coin, 1300, steel, 900, gears, 300, fuel, 300),
            population = 750,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var trooper = MedievalUnit("RayTrooper", "Ray Trooper", age, "Infantryman", 390, 72, 2.0f, 7.5f, 0.95f);
        var blaster = MedievalUnit("BlasterGunner", "Blaster Gunner", age, "MachineGunner", 410, 18, 0.4f, 6.0f, 0.9f);
        var hoverCar = MedievalUnit("HoverCar", "Hover Car", age, "ArmouredCar", 1000, 20, 0.45f, 5.5f, 2.1f);
        hoverCar.populationCost = 2; hoverCar.movement = Movement.Air; hoverCar.flyHeight = 0.6f;   // floats over water and walls
        var nurse = MedievalUnit("RayNurse", "Ray Nurse", age, "Medic", 380, 44, 1.0f, 0.8f, 1.05f);
        nurse.healer = true; nurse.healRange = 13f;
        var hoverTank = MedievalUnit("HoverTank", "Hover Tank", age, "Tank", 1700, 180, 3.6f, 9.5f, 0.8f);
        hoverTank.populationCost = 3;
        var robot = MedievalUnit("GiantRobot", "Giant Robot", age, "DieselWalker", 1900, 85, 1.1f, 1.1f, 0.8f);
        robot.populationCost = 3;
        var rocketeer = MedievalUnit("Rocketeer", "Rocketeer", age, "Infantryman", 450, 40, 1.0f, 5.0f, 2.0f);
        rocketeer.movement = Movement.Air; rocketeer.flyHeight = 1.1f;
        foreach (var u in new[] { trooper, blaster, nurse, rocketeer })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.012f), Rate(coin, 0.07f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { hoverCar, hoverTank, robot })   // machines run on power cells
        {
            u.upkeep = new List<ResourceRate> { Rate(cells, u == hoverCar ? 0.02f : 0.035f), Rate(coin, 0.08f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { trooper, blaster, hoverCar, nurse, hoverTank, robot, rocketeer };
        var rayBandit = MedievalUnit("RayBandit", "Ray Bandit", age, "Gangster", 420, 40, 1.0f, 5.0f, 1.0f);
        var roboRaider = MedievalUnit("RoboRaider", "Robo-Raider", age, "TrenchRaider", 1000, 72, 1.2f, 1.0f, 0.95f);
        var jetRaider = MedievalUnit("JetRaider", "Jet Raider", age, "Gangster", 380, 34, 1.0f, 5.0f, 1.9f);
        jetRaider.movement = Movement.Air; jetRaider.flyHeight = 1.1f;
        foreach (var u in new[] { rayBandit, roboRaider, jetRaider }) EditorUtility.SetDirty(u);
        age.enemyUnits = new List<UnitDefinition> { rayBandit, roboRaider, jetRaider };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var skyline = RankUp(db, "ApartmentBlock", "SkylineTower", "Skyline Tower", age, skyscrapers,
            "A slim modernist tower on stilts with ribbons of glass. Houses 110 people.",
            Cost(stone, 560, steel, 140, gold, 60), Cost(stone, 280, steel, 70), 1200);
        skyline.housing = 110;
        skyline.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Penthouse", description = "+18 people.", requirements = new Requirements { cost = Cost(steel, 120, gold, 60) }, upgradeTime = 25f, extraHousing = 18 },
            new BuildingUpgrade { name = "Express Lifts", description = "+18 people.", requirements = new Requirements { cost = Cost(cells, 40, gears, 60) }, upgradeTime = 25f, extraHousing = 18 },
        };

        var skyBank = RankUp(db, "CentralBank", "SkyBank", "Sky Bank", age, credit,
            "A sleek bank under a swooping roof and a starburst sign. Coin by the sackful.",
            Cost(stone, 500, gold, 220, steel, 90), Cost(stone, 250, gold, 110), 1000);
        skyBank.workers = 3;
        skyBank.production = new List<ResourceRate> { Rate(coin, 2.8f) };
        skyBank.inputs = new List<ResourceRate> { Rate(gold, 0.085f) };
        skyBank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Credit Cards", "+25% coin.", Cost(gold, 180, coin, 300), 1.25f),
            Upgrade("Computer Ledgers", "+25% coin.", Cost(cells, 60, gears, 80), 1.25f),
        };

        var academy = RankUp(db, "InfantryBarracks", "TrooperAcademy", "Trooper Academy", age, rayGuns,
            "A low modernist academy with a rocket on a plinth. Trains Ray Troopers.",
            Cost(stone, 500, steel, 160, cells, 40), Cost(stone, 250, steel, 80), 1300);
        Trains(academy, trooper, 4, steel, gold, food, coin);

        var blasterRange = RankUp(db, "MachineGunSchool", "BlasterRange", "Blaster Range", age, blasters,
            "A firing hall facing chrome target robots. Trains Blaster Gunners.",
            Cost(stone, 420, steel, 180, cells, 50), Cost(stone, 210, steel, 90), 1000);
        Trains(blasterRange, blaster, 4, steel, gold, food, coin);

        var hoverPool = RankUp(db, "MotorPool", "HoverPool", "Hover Pool", age, hover,
            "An open garage where hover cars float on glowing pads. Builds Hover Cars, which glide over water and walls.",
            Cost(steel, 320, cells, 60, gears, 100), Cost(steel, 160, cells, 30), 1050);
        Trains(hoverPool, hoverCar, 3, steel, gold, food, coin);

        var mediLab = RankUp(db, "FieldHospital", "MediLab", "Medi-Lab", age, rayMedicine,
            "A white clinic with a healing-ray lamp on its roof. Trains Ray Nurses.",
            Cost(stone, 450, steel, 100, gold, 110), Cost(stone, 220, gold, 60), 1000);
        mediLab.trainsUnit = nurse;
        mediLab.maxUnits = 3;
        mediLab.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Ambulance Hover", description = "+1 nurse.", requirements = new Requirements { cost = Cost(cells, 40, steel, 80) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Regeneration Ray", description = "Nurses heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(cells, 60, gold, 120) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var spark = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/GalvanicSpark.png");
        var rayBolt = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/RayBolt.png");
        var laser = PixelProjectile("LaserBeam", 16, 3, c => { c.LineRaw(new Vector2(0, 1), new Vector2(15, 1), PixelPalette.Red[3]); c.LineRaw(new Vector2(4, 1), new Vector2(12, 1), PixelPalette.White); });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, bool smoke, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = smoke;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var teslaCoil = RankUp(db, "Pillbox", "TeslaCoil", "Tesla Coil", age, tesla,
            "A tall coil topped with a copper ball. Lightning leaps from it to anything that comes close, and jumps to those beside it.",
            Cost(steel, 300, cells, 80, gears, 60), Cost(steel, 150, cells, 40), 1700);
        Crewless(teslaCoil, 9f, 46, 0.7f, spark, 0f, 34f, 1.0f, false, 32f, 46f);
        teslaCoil.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Bigger Ball", description = "+1.5 range.", requirements = new Requirements { cost = Cost(steel, 100, cells, 40) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Overcharge", description = "+16 damage.", requirements = new Requirements { cost = Cost(cells, 80, gears, 40) }, upgradeTime = 25f, extraTowerDamage = 16 },
        };
        var laserTower = RankUp(db, "SniperNest", "LaserTower", "Laser Tower", age, lasers,
            "A slim white tower with a red-lensed emitter. Its beam crosses half the map in a blink.",
            Cost(steel, 320, cells, 100, gold, 80), Cost(steel, 160, cells, 50), 1200);
        Crewless(laserTower, 17.5f, 270, 3.0f, laser, 0f, 60f, 0f, false, 59f, 45f);
        laserTower.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Focusing Lens", description = "+2 range.", requirements = new Requirements { cost = Cost(gold, 80, cells, 40) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Ruby Rod", description = "+80 damage.", requirements = new Requirements { cost = Cost(cells, 100, gold, 100) }, upgradeTime = 30f, extraTowerDamage = 80 },
        };
        var atomicCannon = RankUp(db, "GunEmplacement", "AtomicCannon", "Atomic Cannon", age, atomicCannons,
            "A heavy chrome gun ringed with glowing coils. Its shells burst green and wide.",
            Cost(steel, 500, uranium, 60, cells, 80), Cost(steel, 250, uranium, 30), 2100);
        Crewless(atomicCannon, 13.5f, 270, 3.6f, rayBolt, 0.05f, 22f, 2.2f, true, 65f, 20f);
        atomicCannon.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Barrel", description = "+2 range.", requirements = new Requirements { cost = Cost(steel, 160, cells, 40) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Enriched Shells", description = "+90 damage.", requirements = new Requirements { cost = Cost(uranium, 80, cells, 60) }, upgradeTime = 30f, extraTowerDamage = 90 },
        };

        var ffWall = RankUp(db, "ConcreteWall", "ForceFieldWall", "Force-Field Wall", age, tesla,
            "Chrome pylons with a shimmering curtain of force between them.", Cost(steel, 12, cells, 2), Cost(steel, 10, cells, 2), 3800);
        var ffGate = RankUp(db, "ConcreteGate", "ForceFieldGate", "Force-Field Gate", age, tesla,
            "A force field that parts for friends.", Cost(steel, 24, cells, 6), Cost(steel, 20, cells, 4), 5200);
        ffWall.upgrades = new List<BuildingUpgrade>();
        ffGate.upgrades = new List<BuildingUpgrade>();

        var arcFurnace = RankUp(db, "SteelMill", "ArcFurnace", "Arc Furnace", age, arc,
            "Carbon electrodes arc into the melt: steel by the trainload, with lightning.",
            Cost(steel, 300, cells, 60, stone, 400), Cost(steel, 150, cells, 30), 1150);
        arcFurnace.workers = 6;
        arcFurnace.production = new List<ResourceRate> { Rate(steel, 0.46f) };
        arcFurnace.inputs = new List<ResourceRate> { Rate(iron, 0.3f), Rate(cells, 0.03f) };
        arcFurnace.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Bigger Electrodes", "+30% steel.", Cost(steel, 200, cells, 40), 1.3f),
            Upgrade("Continuous Casting", "+30% steel.", Cost(steel, 260, gears, 80), 1.3f),
        };

        var plant = RankUp(db, "Factory", "AutomatedPlant", "Automated Plant", age, automation,
            "Robot arms working behind a wall of glass. Gears and parts without end.",
            Cost(steel, 260, gears, 100, cells, 50), Cost(steel, 130, gears, 50), 1000);
        plant.workers = 4;
        plant.production = new List<ResourceRate> { Rate(gears, 0.26f) };
        plant.inputs = new List<ResourceRate> { Rate(steel, 0.12f), Rate(cells, 0.02f) };
        plant.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Faster Arms", "+30% gears.", Cost(gears, 100, cells, 40), 1.3f),
            Upgrade("Night Shift", "+30% gears.", Cost(steel, 160, cells, 40), 1.3f),
        };

        var hoverTankWorks = RankUp(db, "TankFactory", "HoverTankWorks", "Hover Tank Works", age, hoverTanks,
            "A huge domed hangar. Builds Hover Tanks: heavy armour and a ray cannon.",
            Cost(steel, 400, cells, 120, gears, 160), Cost(steel, 200, cells, 60), 1600);
        hoverTankWorks.trainsUnit = hoverTank;
        hoverTankWorks.maxUnits = 2;
        hoverTankWorks.workers = 6;
        hoverTankWorks.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Bay", description = "+1 hover tank.", requirements = new Requirements { cost = Cost(steel, 260, cells, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Deflector Shields", description = "Hover tanks are 25% tougher.", requirements = new Requirements { cost = Cost(cells, 140, gears, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };

        var robotFoundry = RankUp(db, "WalkerWorks", "RobotFoundry", "Robot Foundry", age, robotics,
            "A chrome-domed foundry where giant robots are switched on for the first time. The walker, with a brain of its own.",
            Cost(steel, 450, gears, 200, cells, 100, aether, 60), Cost(steel, 220, gears, 100, cells, 50), 1200);
        robotFoundry.trainsUnit = robot;
        robotFoundry.maxUnits = 3;
        robotFoundry.workers = 4;
        robotFoundry.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Assembly Bay", description = "+1 robot.", requirements = new Requirements { cost = Cost(steel, 200, gears, 120) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Positronic Brains", description = "Robots hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(cells, 140, aether, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };

        var rocketPad = RankUp(db, "Airfield", "RocketPad", "Rocket Pad", age, rocketry,
            "A launch pad and a gantry. Trains Rocketeers, who fly on rocket packs and fire ray pistols.",
            Cost(steel, 360, fuel, 150, cells, 80), Cost(steel, 180, cells, 40), 1000);
        rocketPad.trainsUnit = rocketeer;
        rocketPad.maxUnits = 4;
        rocketPad.workers = 4;
        rocketPad.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Bigger Pad", description = "+1 rocketeer.", requirements = new Requirements { cost = Cost(steel, 160, fuel, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Atomic Packs", description = "Rocketeers are 20% faster and hit 20% harder.", requirements = new Requirements { cost = Cost(uranium, 40, cells, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { speedMultiplier = 1.2f, attackMultiplier = 1.2f } },
        };

        var uraniumMine = NewBuilding(db, "AetherMine", "UraniumMine", "Uranium Mine", age, atomicPower, new Vector2Int(3, 3),
            "A shaft with yellow hazard bands, green ore glowing in the carts. Must be built on or next to Uranium, or right on a rich uranium vein.",
            Cost(steel, 300, stone, 300, gears, 80), 700);
        uraniumMine.requiredDeposit = uraniumDeposit;
        uraniumMine.canBuildOnHills = true;
        uraniumMine.workers = 5;
        uraniumMine.production = new List<ResourceRate> { Rate(uranium, 0.08f) };
        uraniumMine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Lead Shielding", "+30% uranium.", Cost(steel, 120, iron, 200), 1.3f),
            Upgrade("Deep Shaft", "+30% uranium.", Cost(steel, 200, gears, 60), 1.3f),
        };

        var reactor = NewBuilding(db, "Refinery", "AtomicReactor", "Atomic Reactor", age, atomicPower, new Vector2Int(3, 3),
            "A white containment dome and a cooling tower. Turns Uranium into Power Cells.",
            Cost(steel, 500, stone, 600, gears, 120), 1200);
        reactor.workers = 5;
        reactor.production = new List<ResourceRate> { Rate(cells, 0.2f) };
        reactor.inputs = new List<ResourceRate> { Rate(uranium, 0.06f) };
        reactor.runsWithoutInputs = 0f;
        reactor.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Control Rods", "+30% power cells.", Cost(steel, 200, gears, 80), 1.3f),
            Upgrade("Second Core", "+30% power cells.", Cost(steel, 300, uranium, 60), 1.3f),
        };

        var all = new[] { skyline, skyBank, academy, blasterRange, hoverPool, mediLab, teslaCoil, laserTower, atomicCannon, ffWall, ffGate, arcFurnace, plant, hoverTankWorks, robotFoundry, rocketPad, uraniumMine, reactor };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds, plus the automaton and iron baron, now with metal legs) ----------
        uraniumDeposit.sprites = PixelDeposits("Uranium", 3, Ore.Uranium);
        EditorUtility.SetDirty(uraniumDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(9);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "RayTrooper", "BlasterGunner", "HoverCar", "RayNurse", "HoverTank", "GiantRobot", "Rocketeer", "RayBandit", "RoboRaider", "JetRaider", "Automaton" });
        SetupBosses("ElectricAge");
        SetupBosses("IndustrialAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Electric Age set up.");
    }
}
