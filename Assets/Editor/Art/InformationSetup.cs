using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Information Age, with a cyber layer: commandos and plasma rifles, drones and jet fighters, plasma
/// tanks, orbital uplinks and missile batteries, hard-light walls, nanotech and fusion, and its special
/// class, the android (the robot's successor). Computing (Electric research) opens it. Silicon is a new
/// hill deposit (with rich veins); Microchips are made from it. Every Electric line ranks up into an
/// Information building. The last of the real-world ages: fantasy ages come after it.
/// Safe to run again. (Tools > Age of Stickman > Setup Information Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Information Age")]
    public static void SetupInformation()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age electric = db.ages.Find(a => a != null && a.name == "ElectricAge");
        var age = AgeAsset(db, "InformationAge", "Information Age", 10,
            "Glass, light and code: drones and jets, plasma and nanites, androids that think, and a world wired together.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            aether = Res(db, "Aether"), fuel = Res(db, "Fuel"), uranium = Res(db, "Uranium"), cells = Res(db, "PowerCells");
        var silicon = MedievalResource(db, "Silicon", "Silicon", age, "PowerCells");
        var chips = MedievalResource(db, "Microchips", "Microchips", age, "Silicon");
        var siliconDeposit = NewDeposit(db, "Silicon", "Silicon", silicon, true, 0.7f, 0f, 1f, -1f);
        siliconDeposit.richVeins = true;
        EditorUtility.SetDirty(siliconDeposit);

        // ---------- Research ----------
        var computing = Tech(db, "Computing", "Computing", electric,
            "A machine that follows instructions faster than any clerk. Opens the way to the Information Age.",
            135f, Cost(food, 2400, gold, 700, coin, 900, steel, 500, cells, 200), "Television");
        computing.requirements.minimumAge = electric;
        computing.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(computing);

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
        var microchips = ITech("Microchips", "Microchips", "A city of switches on a sliver of sand. Unlocks the Silicon Quarry and the Chip Fab.",
            115f, Cost(steel, 400, cells, 150, gold, 200), "Computing");
        var assault = ITech("AssaultRifles", "Assault Rifles", "Light, fast and deadly at any range. Trooper Academies rank up into the Spec-Ops Base; all troops hit 4% harder.",
            115f, Cost(steel, 400, chips, 60), microchips.id, microchips);
        assault.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var plasma = ITech("PlasmaWeapons", "Plasma Weapons", "Bolts of star-hot plasma. Blaster Ranges rank up into the Plasma Range.",
            115f, Cost(steel, 400, cells, 120, chips, 60), assault.id, assault);
        var drones = ITech("Drones", "Drones", "Little flying machines that need no pilot. Hover Pools rank up into the Drone Hangar; troops move 4% faster.",
            115f, Cost(steel, 300, chips, 100, cells, 80), plasma.id, microchips);
        drones.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var nanomed = ITech("Nanomedicine", "Nanomedicine", "Machines smaller than a cell that repair the body. Medi-Labs rank up into the Nano Clinic; all troops have 4% more health.",
            105f, Cost(food, 1800, gold, 400, chips, 60), drones.id);
        nanomed.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var turrets = ITech("PlasmaTurrets", "Plasma Turrets", "Twin-barrelled plasma turrets and walls of solid light. Tesla Coils rank up into Plasma Turrets, force fields into hard light; buildings have 8% more health.",
            120f, Cost(steel, 500, chips, 120, cells, 120), nanomed.id, plasma);
        turrets.buildingHealthMultiplier = 1.08f;
        var satellites = ITech("Satellites", "Satellites", "Eyes in orbit, and a beam to call down. Laser Towers rank up into Orbital Uplinks.",
            120f, Cost(steel, 400, chips, 150, fuel, 200), turrets.id, microchips);
        var missiles = ITech("Missiles", "Guided Missiles", "Rockets that find their own way. Atomic Cannons rank up into Missile Batteries.",
            120f, Cost(steel, 500, chips, 120, fuel, 200), satellites.id, microchips);
        var nanotech = ITech("Nanotech", "Nanotech", "Clouds of nanites that knit steel atom by atom. Arc Furnaces rank up into the Nanoforge; steel comes 10% faster.",
            115f, Cost(steel, 500, chips, 100, cells, 80), missiles.id, nanomed);
        nanotech.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var assembly = ITech("NanoAssembly", "Nano-Assembly", "Things built from the bottom up. Automated Plants rank up into the Nanoassembler; everything you produce comes in 5% faster.",
            120f, Cost(steel, 500, gears, 300, chips, 100), nanotech.id, nanotech);
        assembly.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var plasmaTanks = ITech("PlasmaTanks", "Plasma Tanks", "A hover tank with a plasma cannon. Hover Tank Works rank up into Plasma Tank Works.",
            130f, Cost(steel, 700, chips, 150, cells, 150), assembly.id, plasma);
        var androids = ITech("Androids", "Androids", "The robot made in our image, quick and clever. Robot Foundries rank up into the Android Lab.",
            135f, Cost(steel, 500, chips, 200, cells, 150, aether, 100), plasmaTanks.id, assembly);
        var jets = ITech("JetEngines", "Jet Engines", "Faster than sound. Rocket Pads rank up into the Jet Hangar.",
            125f, Cost(steel, 500, fuel, 300, chips, 100), androids.id, drones);
        var smartCities = ITech("SmartCities", "Smart Cities", "Towers that run themselves. Skyline Towers rank up into Smart Towers.",
            105f, Cost(steel, 600, stone, 1800, chips, 80), jets.id);
        var finance = ITech("DigitalFinance", "Digital Finance", "Money as numbers on a screen. Sky Banks rank up into the Data Bank; everyone pays a little more in taxes.",
            105f, Cost(gold, 800, coin, 700, chips, 60), smartCities.id);
        finance.coinPerPersonPerSecond = 0.002f;
        var internet = ITech("Internet", "The Internet", "Every mind joined to every other. Research goes 5% faster.",
            115f, Cost(gold, 600, chips, 120, coin, 600), finance.id);
        internet.researchSpeedMultiplier = 1.05f;
        var fusion = ITech("FusionPower", "Fusion Power", "The power of the sun, held in a ring of magnets. Atomic Reactors rank up into Fusion Reactors.",
            130f, Cost(steel, 600, uranium, 100, chips, 150), internet.id, microchips);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { computing },
            cost = Cost(food, 9000, wood, 6000, stone, 7500, iron, 2200, gold, 1100, coin, 1500, steel, 1100, gears, 400, cells, 300),
            population = 850,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var commando = MedievalUnit("Commando", "Commando", age, "RayTrooper", 440, 80, 1.8f, 7.8f, 1.0f);
        var plasmaGunner = MedievalUnit("PlasmaGunner", "Plasma Gunner", age, "BlasterGunner", 450, 22, 0.35f, 6.2f, 0.95f);
        var drone = MedievalUnit("Drone", "Drone", age, "BlasterGunner", 500, 16, 0.4f, 5.5f, 2.4f);
        drone.movement = Movement.Air; drone.flyHeight = 1.0f;
        var nanomedic = MedievalUnit("Nanomedic", "Nanomedic", age, "RayNurse", 420, 50, 1.0f, 0.8f, 1.1f);
        nanomedic.healer = true; nanomedic.healRange = 14f;
        var plasmaTank = MedievalUnit("PlasmaTank", "Plasma Tank", age, "HoverTank", 2000, 210, 3.4f, 10f, 0.85f);
        plasmaTank.populationCost = 3;
        var android = MedievalUnit("Android", "Android", age, "GiantRobot", 1600, 90, 0.8f, 1.0f, 1.3f);
        android.populationCost = 2;
        var jet = MedievalUnit("JetFighter", "Jet Fighter", age, "Rocketeer", 900, 60, 1.2f, 6.5f, 3.0f);
        jet.populationCost = 2; jet.movement = Movement.Air; jet.flyHeight = 1.5f;
        foreach (var u in new[] { commando, plasmaGunner, nanomedic })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.012f), Rate(coin, 0.075f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { drone, plasmaTank, android, jet })   // machines run on power cells
        {
            u.upkeep = new List<ResourceRate> { Rate(cells, u == drone ? 0.015f : 0.035f), Rate(coin, 0.08f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { commando, plasmaGunner, drone, nanomedic, plasmaTank, android, jet };
        var thug = MedievalUnit("CyberThug", "Cyber Thug", age, "RoboRaider", 900, 80, 1.0f, 0.9f, 1.1f);
        var hacker = MedievalUnit("Hacker", "Hacker", age, "RayBandit", 480, 70, 1.8f, 7.0f, 1.0f);
        var rogueDrone = MedievalUnit("RogueDrone", "Rogue Drone", age, "RayBandit", 450, 14, 0.45f, 5.0f, 2.3f);
        rogueDrone.movement = Movement.Air; rogueDrone.flyHeight = 1.0f;
        foreach (var u in new[] { thug, hacker, rogueDrone }) EditorUtility.SetDirty(u);
        age.enemyUnits = new List<UnitDefinition> { thug, hacker, rogueDrone };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var smartTower = RankUp(db, "SkylineTower", "SmartTower", "Smart Tower", age, smartCities,
            "A dark-glass tower that runs itself, a garden on its roof. Houses 130 people.",
            Cost(stone, 650, steel, 180, chips, 30), Cost(stone, 320, steel, 90), 1350);
        smartTower.housing = 130;
        smartTower.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Sky Garden", description = "+20 people.", requirements = new Requirements { cost = Cost(steel, 140, food, 400) }, upgradeTime = 25f, extraHousing = 20 },
            new BuildingUpgrade { name = "Smart Lifts", description = "+20 people.", requirements = new Requirements { cost = Cost(chips, 40, cells, 40) }, upgradeTime = 25f, extraHousing = 20 },
        };
        var dataBank = RankUp(db, "SkyBank", "DataBank", "Data Bank", age, finance,
            "A black finance hub, walls of servers blinking. Coin moves at the speed of light.",
            Cost(stone, 550, gold, 260, chips, 40), Cost(stone, 270, gold, 130), 1100);
        dataBank.workers = 3;
        dataBank.production = new List<ResourceRate> { Rate(coin, 3.5f) };
        dataBank.inputs = new List<ResourceRate> { Rate(gold, 0.095f) };
        dataBank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("High-Frequency Trading", "+25% coin.", Cost(chips, 60, coin, 400), 1.25f),
            Upgrade("Server Farm", "+25% coin.", Cost(chips, 80, cells, 60), 1.25f),
        };
        var specOps = RankUp(db, "TrooperAcademy", "SpecOpsBase", "Spec-Ops Base", age, assault,
            "A low black compound with a radar mast. Trains Commandos.",
            Cost(stone, 550, steel, 180, chips, 40), Cost(stone, 270, steel, 90), 1400);
        Trains(specOps, commando, 4, steel, gold, food, coin);
        var plasmaRange = RankUp(db, "BlasterRange", "PlasmaRange", "Plasma Range", age, plasma,
            "A firing gallery facing holographic targets. Trains Plasma Gunners.",
            Cost(stone, 460, steel, 200, chips, 50), Cost(stone, 230, steel, 100), 1100);
        Trains(plasmaRange, plasmaGunner, 4, steel, gold, food, coin);
        var droneHangar = RankUp(db, "HoverPool", "DroneHangar", "Drone Hangar", age, drones,
            "A flat roof of landing circles. Builds Drones: pilotless flyers with a gun.",
            Cost(steel, 340, chips, 80, cells, 60), Cost(steel, 170, chips, 40), 1100);
        Trains(droneHangar, drone, 4, steel, gold, food, coin);
        var nanoClinic = RankUp(db, "MediLab", "NanoClinic", "Nano Clinic", age, nanomed,
            "A white clinic with a glowing cyan cross. Trains Nanomedics.",
            Cost(stone, 500, steel, 120, chips, 40), Cost(stone, 250, chips, 20), 1100);
        nanoClinic.trainsUnit = nanomedic;
        nanoClinic.maxUnits = 3;
        nanoClinic.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Med Drone", description = "+1 nanomedic.", requirements = new Requirements { cost = Cost(chips, 40, steel, 80) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Nanite Swarm", description = "Nanomedics heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(chips, 80, gold, 120) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var bullet = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Bullet.png");
        var plasmaBolt = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/PlasmaBolt.png");
        var beam = PixelProjectile("OrbitalBeam", 18, 4, c => { c.LineRaw(new Vector2(0, 1), new Vector2(17, 1), PixelPalette.Neon[3]); c.LineRaw(new Vector2(2, 2), new Vector2(15, 2), PixelPalette.White); });
        var missile = PixelProjectile("Missile", 12, 4, c => { c.Rect(2, 1, 9, 2, PixelPalette.White); c.Rect(9, 1, 11, 2, PixelPalette.Red[2]); c.Put(0, 1, PixelPalette.Fire[3]); c.Put(1, 2, PixelPalette.Fire[2]); });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, bool smoke, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = smoke;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var plasmaTurret = RankUp(db, "TeslaCoil", "PlasmaTurret", "Plasma Turret", age, turrets,
            "A black dome turret with twin plasma barrels. A stream of star-hot bolts.",
            Cost(steel, 360, chips, 60, cells, 80), Cost(steel, 180, chips, 30), 1900);
        Crewless(plasmaTurret, 10f, 40, 0.4f, plasmaBolt, 0f, 34f, 0.5f, false, 60f, 20.5f);
        plasmaTurret.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Targeting Computer", description = "+1.5 range.", requirements = new Requirements { cost = Cost(chips, 60, steel, 80) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Hotter Plasma", description = "+14 damage.", requirements = new Requirements { cost = Cost(cells, 100, chips, 40) }, upgradeTime = 25f, extraTowerDamage = 14 },
        };
        var uplink = RankUp(db, "LaserTower", "OrbitalUplink", "Orbital Uplink", age, satellites,
            "A mast and a dish aimed at the sky: it calls down a beam from a satellite overhead. Reaches further than anything.",
            Cost(steel, 360, chips, 120, gold, 100), Cost(steel, 180, chips, 60), 1300);
        Crewless(uplink, 19f, 320, 3.0f, beam, 0f, 70f, 0f, false, 32f, 57f);
        uplink.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Satellite", description = "+2 range.", requirements = new Requirements { cost = Cost(chips, 100, fuel, 100) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Focused Beam", description = "+100 damage.", requirements = new Requirements { cost = Cost(cells, 120, chips, 80) }, upgradeTime = 30f, extraTowerDamage = 100 },
        };
        var missileBattery = RankUp(db, "AtomicCannon", "MissileBattery", "Missile Battery", age, missiles,
            "A rack of guided missiles. Each salvo bursts wide among the raiders.",
            Cost(steel, 560, chips, 100, fuel, 120), Cost(steel, 280, chips, 50), 2300);
        Crewless(missileBattery, 14.5f, 320, 3.6f, missile, 0.25f, 18f, 2.4f, true, 46f, 15f);
        missileBattery.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Range", description = "+2 range.", requirements = new Requirements { cost = Cost(fuel, 120, chips, 40) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Cluster Warheads", description = "+100 damage.", requirements = new Requirements { cost = Cost(chips, 100, steel, 160) }, upgradeTime = 30f, extraTowerDamage = 100 },
        };
        var hlWall = RankUp(db, "ForceFieldWall", "HardLightWall", "Hard-Light Wall", age, turrets,
            "Black posts and a hexagonal grid of solid light.", Cost(steel, 12, chips, 2), Cost(steel, 10, chips, 2), 4300);
        var hlGate = RankUp(db, "ForceFieldGate", "HardLightGate", "Hard-Light Gate", age, turrets,
            "Light that steps aside for friends.", Cost(steel, 24, chips, 6), Cost(steel, 20, chips, 4), 5800);
        hlWall.upgrades = new List<BuildingUpgrade>();
        hlGate.upgrades = new List<BuildingUpgrade>();

        var nanoforge = RankUp(db, "ArcFurnace", "Nanoforge", "Nanoforge", age, nanotech,
            "A sleek forge where clouds of nanites knit steel. Steel without end.",
            Cost(steel, 360, chips, 80, cells, 60), Cost(steel, 180, chips, 40), 1250);
        nanoforge.workers = 5;
        nanoforge.production = new List<ResourceRate> { Rate(steel, 0.6f) };
        nanoforge.inputs = new List<ResourceRate> { Rate(iron, 0.34f), Rate(cells, 0.03f) };
        nanoforge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Denser Swarm", "+30% steel.", Cost(chips, 80, cells, 60), 1.3f),
            Upgrade("Atomic Printing", "+30% steel.", Cost(steel, 300, chips, 80), 1.3f),
        };
        var assembler = RankUp(db, "AutomatedPlant", "Nanoassembler", "Nanoassembler", age, assembly,
            "Parts slide out of a glowing assembler. Gears without end.",
            Cost(steel, 300, gears, 140, chips, 60), Cost(steel, 150, chips, 30), 1100);
        assembler.workers = 4;
        assembler.production = new List<ResourceRate> { Rate(gears, 0.36f) };
        assembler.inputs = new List<ResourceRate> { Rate(steel, 0.14f), Rate(cells, 0.02f) };
        assembler.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Parallel Lines", "+30% gears.", Cost(chips, 80, gears, 100), 1.3f),
            Upgrade("Self-Repair", "+30% gears.", Cost(steel, 200, chips, 60), 1.3f),
        };
        var plasmaTankWorks = RankUp(db, "HoverTankWorks", "PlasmaTankWorks", "Plasma Tank Works", age, plasmaTanks,
            "A great black hangar. Builds Plasma Tanks.",
            Cost(steel, 460, chips, 140, cells, 120), Cost(steel, 230, chips, 70), 1800);
        plasmaTankWorks.trainsUnit = plasmaTank;
        plasmaTankWorks.maxUnits = 2;
        plasmaTankWorks.workers = 6;
        plasmaTankWorks.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Bay", description = "+1 plasma tank.", requirements = new Requirements { cost = Cost(steel, 300, chips, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Reactive Armour", description = "Plasma tanks are 25% tougher.", requirements = new Requirements { cost = Cost(chips, 120, cells, 100) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var androidLab = RankUp(db, "RobotFoundry", "AndroidLab", "Android Lab", age, androids,
            "A white lab where androids are brought online in glass cradles. The robot, made in our image.",
            Cost(steel, 500, chips, 200, cells, 120, aether, 80), Cost(steel, 250, chips, 100, aether, 40), 1300);
        androidLab.trainsUnit = android;
        androidLab.maxUnits = 4;
        androidLab.workers = 4;
        androidLab.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Cradle", description = "+1 android.", requirements = new Requirements { cost = Cost(chips, 140, steel, 160) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Neural Lattice", description = "Androids hit 25% harder and are 20% faster.", requirements = new Requirements { cost = Cost(chips, 160, aether, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, speedMultiplier = 1.2f } },
        };
        var jetHangar = RankUp(db, "RocketPad", "JetHangar", "Jet Hangar", age, jets,
            "A runway and a hardened hangar. Builds Jet Fighters: the fastest things in the sky.",
            Cost(steel, 420, fuel, 200, chips, 80), Cost(steel, 210, chips, 40), 1200);
        jetHangar.trainsUnit = jet;
        jetHangar.maxUnits = 3;
        jetHangar.workers = 4;
        jetHangar.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Runway", description = "+1 jet.", requirements = new Requirements { cost = Cost(steel, 200, fuel, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Stealth Coating", description = "Jets are 25% tougher.", requirements = new Requirements { cost = Cost(chips, 120, steel, 120) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var fusionReactor = RankUp(db, "AtomicReactor", "FusionReactor", "Fusion Reactor", age, fusion,
            "A ring of magnets holding a star. Power cells by the thousand, from a trickle of uranium.",
            Cost(steel, 600, chips, 150, uranium, 60), Cost(steel, 300, chips, 80), 1500);
        fusionReactor.workers = 5;
        fusionReactor.production = new List<ResourceRate> { Rate(cells, 0.4f) };
        fusionReactor.inputs = new List<ResourceRate> { Rate(uranium, 0.04f) };
        fusionReactor.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Stronger Magnets", "+30% power cells.", Cost(steel, 300, chips, 80), 1.3f),
            Upgrade("Second Ring", "+30% power cells.", Cost(steel, 400, uranium, 60), 1.3f),
        };
        var quarry = NewBuilding(db, "UraniumMine", "SiliconQuarry", "Silicon Quarry", age, microchips, new Vector2Int(3, 3),
            "An open pit of pale quartz. Must be built on or next to Silicon, or right on a rich silicon vein.",
            Cost(steel, 260, stone, 300, gears, 60), 650);
        quarry.requiredDeposit = siliconDeposit;
        quarry.canBuildOnHills = true;
        quarry.workers = 5;
        quarry.production = new List<ResourceRate> { Rate(silicon, 0.14f) };
        quarry.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Bigger Excavators", "+30% silicon.", Cost(steel, 200, fuel, 80), 1.3f),
            Upgrade("Sifting Drones", "+30% silicon.", Cost(chips, 40, steel, 100), 1.3f),
        };
        var chipFab = NewBuilding(db, "AtomicReactor", "ChipFab", "Chip Fab", age, microchips, new Vector2Int(3, 3),
            "A white clean-room fab. Turns Silicon and Power Cells into Microchips.",
            Cost(steel, 400, stone, 400, gold, 150), 1000);
        chipFab.workers = 5;
        chipFab.production = new List<ResourceRate> { Rate(chips, 0.12f) };
        chipFab.inputs = new List<ResourceRate> { Rate(silicon, 0.12f), Rate(cells, 0.04f) };
        chipFab.runsWithoutInputs = 0f;
        chipFab.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Smaller Transistors", "+30% microchips.", Cost(gold, 150, chips, 40), 1.3f),
            Upgrade("Bigger Wafers", "+30% microchips.", Cost(steel, 300, silicon, 100), 1.3f),
        };

        var all = new[] { smartTower, dataBank, specOps, plasmaRange, droneHangar, nanoClinic, plasmaTurret, uplink, missileBattery, hlWall, hlGate, nanoforge, assembler, plasmaTankWorks, androidLab, jetHangar, fusionReactor, quarry, chipFab };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds) ----------
        siliconDeposit.sprites = PixelDeposits("Silicon", 3, Ore.Silicon);
        EditorUtility.SetDirty(siliconDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(10);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Commando", "PlasmaGunner", "Drone", "Nanomedic", "PlasmaTank", "Android", "JetFighter", "CyberThug", "Hacker", "RogueDrone" });
        SetupBosses("InformationAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Information Age set up.");
    }
}
