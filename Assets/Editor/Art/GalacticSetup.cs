using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Galactic Age: the galaxy itself as a home, cosmic magic and megastructures together. Aegis Knights with singularity blades, Starweavers spinning little galaxies, Comet Riders, Lifeweavers, the hovering Gate Colossus, Celestial Avatars (the construct's last form) and Dreadnought Arks; star gates link your worlds. The Hive comes to eat whole worlds. Stardust (with rich veins) is distilled with mana into Quintessence. Galactic Cartography (Interstellar research) opens it; every Interstellar line ranks up.
/// Safe to run again. (Tools > Age of Stickman > Setup Galactic Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Galactic Age")]
    public static void SetupGalactic()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age prev = db.ages.Find(a => a != null && a.name == "InterstellarAge");
        var age = AgeAsset(db, "GalacticAge", "Galactic Age", 15,
            "The galaxy itself as your home: singularity blades and galaxy staffs, riders on comets, gates that link the worlds, and arks the size of cities.");

        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores"), prevRefined = Res(db, "WarpCells");
        var raw = MedievalResource(db, "Stardust", "Stardust", age, "WarpCells");
        var refined = MedievalResource(db, "Quintessence", "Quintessence", age, "Stardust");
        var deposit = NewDeposit(db, "Stardust", "Stardust", raw, true, 0.72f, 0f, 1f, -1f);
        deposit.richVeins = true;
        EditorUtility.SetDirty(deposit);

        var gate = Tech(db, "GalacticCartography", "Galactic Cartography", prev,
            "The whole galaxy, charted: every star, every fold, every gate the precursors left. Opens the way to the Galactic Age.",
            221f, Cost(food, 4940, gold, 1430, coin, 2080, prevRefined, 320, starmetal, 460), "RiftTaps");
        gate.requirements.minimumAge = prev;
        gate.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(gate);

        Technology T(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
        {
            var t = Tech(db, id, name, age, description, time, cost, after);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>(needs);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            t.maxLevel = 1;
            EditorUtility.SetDirty(t);
            return t;
        }
        var resTech = T("StardustHarvest", "Stardust Harvest", "The dust of dead stars, still warm. Unlocks the Stardust Collector and the Quintessence Distillery.",
            150f, Cost(steel, 1040, cores, 180, mana, 230), "GalacticCartography");
        var blades = T("SingularityBlades", "Singularity Blades", "A blade with a tiny black hole for an edge. Riftblade Halls rank up into the Aegis Hall; all troops hit 4% harder.",
            150f, Cost(steel, 1040, refined, 91), resTech.id, resTech);
        blades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var ranged = T("Starweaving", "Starweaving", "Staffs that spin a little galaxy and throw it. Gravity Academies rank up into the Starweaver Conclave.",
            150f, Cost(gold, 1040, refined, 117, chips, 220), blades.id, blades);
        var air = T("CometRiding", "Comet Riding", "Riders who surf on comets, steering by the tail. Leviathan Roosts rank up into the Comet Hangar; troops move 4% faster.",
            150f, Cost(cells, 460, refined, 117, mana, 180), ranged.id, resTech);
        air.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var healing = T("Lifeweaving", "Lifeweaving", "Life woven back together thread by thread. Temples of Paths rank up into the Temple of Life; all troops have 4% more health.",
            140f, Cost(food, 4420, gold, 780, refined, 91), air.id);
        healing.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var lances = T("SingularityLances", "Singularity Lances", "Bolts with a heart of collapsed star. Phase Lances rank up into Singularity Lances, phase into celestial walls; buildings have 8% more health.",
            155f, Cost(stone, 4420, refined, 210, starmetal, 180), healing.id, ranged);
        lances.buildingHealthMultiplier = 1.08f;
        var longTower = T("SupernovaArrays", "Supernova Arrays", "Captured supernova light, aimed. Gravity Lenses rank up into Supernova Arrays.",
            155f, Cost(gold, 1170, refined, 230, chips, 180), lances.id, resTech);
        var splashTower = T("QuasarBatteries", "Quasar Batteries", "A beam of quasar fire that bursts where it lands. Rift Mortars rank up into Quasar Batteries.",
            155f, Cost(cells, 460, refined, 230, starmetal, 180), longTower.id, resTech);
        var forging = T("StarAnvils", "Star Anvils", "Anvils of neutron-star matter. Fold Forges rank up into the Stellar Anvil; steel comes 10% faster.",
            150f, Cost(steel, 1170, refined, 180), splashTower.id, healing);
        forging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var works = T("CelestialWorks", "Celestial Works", "Workers of living starlight. Prism Works rank up into Celestial Works; everything you produce comes in 5% faster.",
            155f, Cost(gears, 780, refined, 180, cores, 130), forging.id, forging);
        works.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var heavyTech = T("GateColossi", "Gate Colossi", "A gate ring that walks on nothing and fires stars through itself. Pyramid Forges rank up into the Gate Foundry.",
            165f, Cost(steel, 1430, refined, 290, cells, 290), works.id, ranged);
        var specialTech = T("CelestialAvatars", "Celestial Avatars", "A construct made of the night sky itself, crowned with stars. Prism Vaults rank up into the Avatar Sanctum.",
            170f, Cost(starmetal, 580, refined, 290, chips, 290), heavyTech.id, works);
        var shipTech = T("DreadnoughtArks", "Dreadnought Arks", "Arks the size of cities, made to carry a people and now carrying armies. Fold Yards rank up into the Ark Yard.",
            165f, Cost(steel, 1690, refined, 290, cores, 180), specialTech.id, air);
        var housingTech = T("CelestialSpires", "Celestial Spires", "Towers that hold a city and reach into orbit. Rift Spires rank up into Celestial Spires.",
            140f, Cost(stone, 4940, refined, 180, steel, 460), shipTech.id);
        var moneyTech = T("GalacticTrade", "Galactic Trade", "Coin good on every world. Fold Exchanges rank up into the Galactic Exchange; everyone pays a little more in taxes.",
            140f, Cost(gold, 1690, coin, 1690, refined, 117), housingTech.id);
        moneyTech.coinPerPersonPerSecond = 0.002f;
        var sight = T("StarMinds", "Star Minds", "Thinking with the whole galaxy's light. Research goes 5% faster.",
            150f, Cost(gold, 1430, refined, 180, chips, 230), moneyTech.id);
        sight.researchSpeedMultiplier = 1.05f;
        var powerTech = T("DysonRelays", "Dyson Relays", "Power beamed down from swarms of mirrors round the sun. Rift Taps rank up into Dyson Relays.",
            160f, Cost(stone, 4420, refined, 290, starmetal, 290), sight.id, resTech);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { gate },
            cost = Cost(food, 16900, wood, 10400, stone, 14300, iron, 3900, gold, 2340, coin, 2860, steel, 2340, prevRefined, 390, starmetal, 460),
            population = 1300,
        };
        EditorUtility.SetDirty(age);
        var melee = MedievalUnit("AegisKnight", "Aegis Knight", age, "Riftblade", 1404, 195, 0.9f, 0.9f, 1.15f);
        var rangedU = MedievalUnit("Starweaver", "Starweaver", age, "Gravimancer", 780, 169, 1.6f, 8.5f, 1.0f);
        var flyer = MedievalUnit("CometRider", "Comet Rider", age, "WhaleRider", 1495, 136, 1.1f, 6.5f, 3.0f);
        flyer.movement = Movement.Air; flyer.flyHeight = 1.0f; flyer.populationCost = 2;
        var healer = MedievalUnit("Lifeweaver", "Lifeweaver", age, "Riftmender", 754, 104, 1.0f, 0.8f, 1.1f);
        healer.healer = true; healer.healRange = 18f;
        var heavy = MedievalUnit("GateColossus", "Gate Colossus", age, "GravityEngine", 3770, 390, 3.2f, 11.5f, 0.9f);
        heavy.populationCost = 3; heavy.movement = Movement.Ground; heavy.flyHeight = 0f;
        var special = MedievalUnit("CelestialAvatar", "Celestial Avatar", age, "PrismSentinel", 3640, 195, 0.95f, 1.2f, 1.05f);
        special.populationCost = 2;
        var ship = MedievalUnit("DreadnoughtArk", "Dreadnought Ark", age, "FoldCruiser", 3250, 253, 2.4f, 7.5f, 1.45f);
        ship.populationCost = 3; ship.movement = Movement.Air; ship.flyHeight = 1.9f;
        foreach (var x in new[] { melee, rangedU, flyer, healer })
        {
            x.upkeep = new List<ResourceRate> { Rate(food, 0.016f), Rate(coin, 0.11f) };
            EditorUtility.SetDirty(x);
        }
        foreach (var x in new[] { heavy, special, ship })
        {
            x.upkeep = new List<ResourceRate> { Rate(refined, 0.012f), Rate(coin, 0.11f) };
            EditorUtility.SetDirty(x);
        }
        age.playerUnits = new List<UnitDefinition> { melee, rangedU, flyer, healer, heavy, special, ship };
        var eMelee = MedievalUnit("HiveDrone", "Hive Drone", age, "RiftReaver", 1560, 156, 1.0f, 0.9f, 1.2f);
        var eRanged = MedievalUnit("HiveSpitter", "Hive Spitter", age, "RiftWitch", 806, 143, 1.7f, 8f, 1.0f);
        var eAir = MedievalUnit("HiveSwarmer", "Hive Swarmer", age, "VoidRayRider", 1300, 117, 1.3f, 6.5f, 2.8f);
        eAir.movement = Movement.Air; eAir.flyHeight = 1.0f;
        foreach (var x in new[] { eMelee, eRanged, eAir }) EditorUtility.SetDirty(x);
        age.enemyUnits = new List<UnitDefinition> { eMelee, eRanged, eAir };
        EditorUtility.SetDirty(age);
        var starShot = PixelProjectile("CosmicBolt", 12, 9, c =>
        {
            var g = PixelPalette.Cosmic;
            c.LineRaw(new Vector2(0, 4), new Vector2(6, 4), g[2]);
            c.Ellipse(new Vector2(9, 4.5f), 2.6f, 2.6f, new[] { g[1], g[2], g[3], PixelPalette.White });
            c.Put(9, 8, g[3]); c.Put(9, 1, g[3]);
        });
        var nova = PixelProjectile("SupernovaRay", 22, 7, c =>
        {
            var g = PixelPalette.Cosmic;
            c.LineRaw(new Vector2(0, 3), new Vector2(21, 3), PixelPalette.White); c.LineRaw(new Vector2(0, 2), new Vector2(21, 2), g[3]); c.LineRaw(new Vector2(0, 4), new Vector2(21, 4), g[3]);
            c.LineRaw(new Vector2(4, 1), new Vector2(21, 1), g[2]); c.LineRaw(new Vector2(4, 5), new Vector2(21, 5), g[2]);
        });
        var quasar = PixelProjectile("QuasarBurst", 14, 14, c =>
        {
            var g = PixelPalette.Cosmic;
            c.Ellipse(new Vector2(7, 7), 6f, 6f, new[] { g[1], g[2], g[3], PixelPalette.White });
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Put(Mathf.RoundToInt(7 + Mathf.Cos(a) * 6.5f), Mathf.RoundToInt(7 + Mathf.Sin(a) * 6.5f), PixelPalette.White); }
        });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = false;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var house = RankUp(db, "RiftSpire", "CelestialSpire", "Celestial Spire", age, housingTech,
            "A tower of dark glass reaching into orbit, a city on every floor. Houses 280 people.",
            Cost(stone, 1300, refined, 78, steel, 290), Cost(stone, 650, refined, 39), 2665);
        house.housing = 280;
        house.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Orbital Gardens", description = "+35 people.", requirements = new Requirements { cost = Cost(stone, 910, refined, 52) }, upgradeTime = 25f, extraHousing = 35 },
            new BuildingUpgrade { name = "Sky Cities", description = "+40 people.", requirements = new Requirements { cost = Cost(refined, 104, gold, 290) }, upgradeTime = 30f, extraHousing = 40 },
        };
        var bank = RankUp(db, "FoldExchange", "GalacticExchange", "Galactic Exchange", age, moneyTech,
            "A hall under a dome painted with the whole galaxy, every world's coin trading at once.",
            Cost(stone, 1170, gold, 580, refined, 52), Cost(stone, 580, gold, 290), 2145);
        bank.workers = 3;
        bank.production = new List<ResourceRate> { Rate(coin, 8.3f) };
        bank.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        bank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Every World's Coin", "+25% coin.", Cost(refined, 65, coin, 910), 1.25f),
            Upgrade("The Long Ledger", "+25% coin.", Cost(refined, 104, gold, 460), 1.25f),
        };
        var meleeHall = RankUp(db, "RiftbladeHall", "AegisHall", "Aegis Hall", age, blades,
            "A hall of dark glass, a blade with a black-hole edge set over its door. Trains Aegis Knights.",
            Cost(stone, 1170, steel, 430, refined, 65), Cost(stone, 580, refined, 32), 2600);
        Trains(meleeHall, melee, 4, steel, gold, food, coin);
        var rangedHall = RankUp(db, "GravityAcademy", "StarweaverConclave", "Starweaver Conclave", age, ranged,
            "A dome where little galaxies turn over the students' heads. Trains Starweavers.",
            Cost(stone, 1170, steel, 430, refined, 65), Cost(stone, 580, refined, 32), 2015);
        Trains(rangedHall, rangedU, 4, steel, gold, food, coin);
        var airHall = RankUp(db, "LeviathanRoost", "CometHangar", "Comet Hangar", age, air,
            "A launch ring with a captive comet hanging in it. Trains Comet Riders: flyers that strike from the sky.",
            Cost(stone, 1170, steel, 430, refined, 65), Cost(stone, 580, refined, 32), 2015);
        Trains(airHall, flyer, 3, steel, gold, food, coin);
        var temple = RankUp(db, "TempleOfPaths", "TempleOfLife", "Temple of Life", age, healing,
            "A temple of living crystal where a great tree grows out of starlight. Trains Lifeweavers.",
            Cost(stone, 1170, gold, 360, refined, 52), Cost(stone, 580, refined, 26), 2015);
        temple.trainsUnit = healer;
        temple.maxUnits = 3;
        temple.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Loom", description = "+1 lifeweaver.", requirements = new Requirements { cost = Cost(refined, 52, gold, 210) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Endless Thread", description = "Lifeweavers heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 104, gold, 290) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };
        var lance = RankUp(db, "PhaseLance", "SingularityLance", "Singularity Lance", age, lances,
            "A black spire with a point of collapsed star at its tip that fires bolts of night.",
            Cost(stone, 840, refined, 91, starmetal, 39), Cost(stone, 430, refined, 46), 3640);
        Crewless(lance, 13f, 101, 0.4f, starShot, 0f, 34f, 0.9f, 32f, 54f);
        lance.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Reach", description = "+1.5 range.", requirements = new Requirements { cost = Cost(refined, 78, stone, 430) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Harder Strikes", description = "+22 damage.", requirements = new Requirements { cost = Cost(refined, 104, cores, 39) }, upgradeTime = 25f, extraTowerDamage = 22 },
        };
        var longT = RankUp(db, "GravityLens", "SupernovaArray", "Supernova Array", age, longTower,
            "A ring of mirrors round a captured supernova. It burns raiders from very far away.",
            Cost(stone, 980, refined, 160, gold, 290), Cost(stone, 490, refined, 78), 2535);
        Crewless(longT, 25f, 676, 2.8f, nova, 0f, 64f, 1.5f, 32f, 52f);
        longT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Further Still", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 130, gold, 210) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Heavier Blows", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 180, cores, 78) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var splashT = RankUp(db, "RiftMortar", "QuasarBattery", "Quasar Battery", age, splashTower,
            "A squat tower with a quasar's twin beams spinning on top. Each shot bursts wide.",
            Cost(stone, 1100, refined, 130, cells, 210), Cost(stone, 550, refined, 65), 4160);
        Crewless(splashT, 16.5f, 650, 3.1f, quasar, 0.2f, 16f, 3.1f, 32f, 42f);
        splashT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 130, cells, 170) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Wider Bursts", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 180, cores, 78) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var wall = RankUp(db, "PhaseWall", "CelestialWall", "Celestial Wall", age, lances,
            "A wall of night-black stone, the stars showing through it as if it were a window onto space.",
            Cost(stone, 18, refined, 2), Cost(stone, 15, refined, 2), 8840);
        var gateB = RankUp(db, "PhaseGate", "CelestialGate", "Celestial Gate", age, lances,
            "A gate of night-black stone that opens onto stars.",
            Cost(stone, 34, refined, 6), Cost(stone, 30, refined, 4), 11700);
        wall.upgrades = new List<BuildingUpgrade>();
        gateB.upgrades = new List<BuildingUpgrade>();
        var forge = RankUp(db, "FoldForge", "StellarAnvil", "Stellar Anvil", age, forging,
            "A forge round an anvil of neutron-star matter. Steel without end.",
            Cost(stone, 980, steel, 580, refined, 78), Cost(stone, 490, refined, 39), 2470);
        forge.workers = 5;
        forge.production = new List<ResourceRate> { Rate(steel, 1.49f) };
        forge.inputs = new List<ResourceRate> { Rate(iron, 0.48f), Rate(refined, 0.012f) };
        forge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Heavier Anvil", "+30% steel.", Cost(refined, 104, stone, 580), 1.3f),
            Upgrade("Star-Hot Hearths", "+30% steel.", Cost(steel, 580, cores, 52), 1.3f),
        };
        var worksB = RankUp(db, "PrismWorks", "CelestialWorks", "Celestial Works", age, works,
            "A hall where workers of living starlight make gears. Gears without end.",
            Cost(stone, 840, gears, 360, refined, 52), Cost(stone, 430, refined, 26), 2145);
        worksB.workers = 4;
        worksB.production = new List<ResourceRate> { Rate(gears, 0.91f) };
        worksB.inputs = new List<ResourceRate> { Rate(steel, 0.2f), Rate(refined, 0.008f) };
        worksB.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Starlight", "+30% gears.", Cost(refined, 78, gears, 210), 1.3f),
            Upgrade("Unending Night Shift", "+30% gears.", Cost(steel, 430, cores, 52), 1.3f),
        };
        var heavyHall = RankUp(db, "PyramidForge", "GateFoundry", "Gate Foundry", age, heavyTech,
            "A cradle where gate rings are raised and taught to walk. Builds Gate Colossi: hovering rings that fire stars through themselves.",
            Cost(stone, 1170, steel, 840, refined, 140), Cost(stone, 580, refined, 72), 3120);
        heavyHall.trainsUnit = heavy;
        heavyHall.maxUnits = 2;
        heavyHall.workers = 6;
        heavyHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Cradle", description = "+1 gate colossus.", requirements = new Requirements { cost = Cost(steel, 720, refined, 117) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Wider Rings", description = "Gate Colossuss are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 170, starmetal, 170) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var specialHall = RankUp(db, "PrismVault", "AvatarSanctum", "Avatar Sanctum", age, specialTech,
            "A sanctum open to the night where avatars of the sky step down from the stars.",
            Cost(stone, 1170, steel, 840, refined, 140), Cost(stone, 580, refined, 72), 3120);
        specialHall.trainsUnit = special;
        specialHall.maxUnits = 4;
        specialHall.workers = 4;
        specialHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Night", description = "+1 celestial avatar.", requirements = new Requirements { cost = Cost(steel, 720, refined, 117) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Brighter Crowns", description = "Celestial Avatars hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 170, starmetal, 170) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var shipHall = RankUp(db, "FoldYard", "ArkYard", "Ark Yard", age, shipTech,
            "A yard of gantries round an ark the size of a city. Builds Dreadnought Arks, and launches ships to other worlds (open the star map).",
            Cost(stone, 1170, steel, 840, refined, 140), Cost(stone, 580, refined, 72), 3120);
        shipHall.trainsUnit = ship;
        shipHall.maxUnits = 2;
        shipHall.workers = 4;
        shipHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Gantry", description = "+1 dreadnought ark.", requirements = new Requirements { cost = Cost(steel, 720, refined, 117) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Ark Shields", description = "Dreadnought Arks are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 170, starmetal, 170) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        shipHall.launchesShips = true;
        var power = RankUp(db, "RiftTap", "DysonRelay", "Dyson Relay", age, powerTech,
            "A dish that catches power beamed down from mirrors round the sun. Power cells without end.",
            Cost(stone, 1950, refined, 210, steel, 720), Cost(stone, 980, refined, 104), 2860);
        power.workers = 5;
        power.production = new List<ResourceRate> { Rate(cells, 1.17f) };
        power.inputs = new List<ResourceRate> { Rate(mana, 0.02f) };
        power.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Mirrors", "+30% power cells.", Cost(stone, 1170, refined, 104), 1.3f),
            Upgrade("Tighter Beam", "+30% power cells.", Cost(refined, 170, cores, 78), 1.3f),
        };
        var mine = NewBuilding(db, "RiftstoneQuarry", "StardustCollector", "Stardust Collector", age, resTech, new Vector2Int(3, 3),
            "A field of nets and funnels catching the dust of dead stars as it falls. Must be built on or next to Stardust, or right on a rich stardust vein.",
            Cost(steel, 520, stone, 720, mana, 91), 1300);
        mine.requiredDeposit = deposit;
        mine.canBuildOnHills = true;
        mine.workers = 5;
        mine.production = new List<ResourceRate> { Rate(raw, 0.13f) };
        mine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Finer Nets", "+30% stardust.", Cost(steel, 430, mana, 91), 1.3f),
            Upgrade("Star Survey", "+30% stardust.", Cost(chips, 140, gold, 290), 1.3f),
        };
        var refinery = NewBuilding(db, "WarpCellPlant", "QuintessenceDistillery", "Quintessence Distillery", age, resTech, new Vector2Int(3, 3),
            "Stardust is distilled with mana into Quintessence, the fifth element, the stuff of stars.",
            Cost(stone, 1170, steel, 430, gold, 290), 1690);
        refinery.workers = 5;
        refinery.production = new List<ResourceRate> { Rate(refined, 0.09f) };
        refinery.inputs = new List<ResourceRate> { Rate(raw, 0.1f), Rate(mana, 0.03f) };
        refinery.runsWithoutInputs = 0f;
        refinery.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Purer Stills", "+30% quintessence.", Cost(stone, 840, gold, 290), 1.3f),
            Upgrade("Mana Condensers", "+30% quintessence.", Cost(mana, 140, chips, 117), 1.3f),
        };

        // The Star Gate: the age's megastructure. Two worlds that both have one trade and travel almost at once.
        var gateTech = T("StarGates", "Star Gates", "Rings vast enough to fold space through. Unlocks the Star Gate: ships and cargo between two of your worlds that both have one arrive in seconds.",
            240f, Cost(steel, 2600, refined, 520, starmetal, 520, cores, 260), powerTech.id, shipTech);
        var starGate = NewBuilding(db, "Starport", "StarGate", "Star Gate", age, gateTech, new Vector2Int(4, 4),
            "A silver ring on two pylons, vast enough to fold space through, another world's sky showing in it. Ships and cargo between two of your worlds that both have a Star Gate arrive in seconds.",
            Cost(steel, 3900, stone, 5200, refined, 780, starmetal, 650, cores, 390), 9000);
        starGate.starGate = true;
        starGate.launchesShips = false;
        starGate.trainsUnit = null;
        starGate.workers = 0;
        starGate.housing = 0;
        starGate.production = new List<ResourceRate>();
        starGate.inputs = new List<ResourceRate> { Rate(cells, 0.05f) };
        starGate.upgrades = new List<BuildingUpgrade>();

        var all = new[] { house, bank, meleeHall, rangedHall, airHall, temple, lance, longT, splashT, wall, gateB, forge, worksB, heavyHall, specialHall, shipHall, power, mine, refinery, starGate };
        foreach (var x in all) EditorUtility.SetDirty(x);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        deposit.sprites = PixelDeposits("Stardust", 3, Ore.Stardust);
        EditorUtility.SetDirty(deposit);
        foreach (var x in all)
            if (!x.IsWall) GenerateBuildingLooks(x);
        GenerateWallArt(15);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "AegisKnight", "Starweaver", "CometRider", "Lifeweaver", "GateColossus", "CelestialAvatar", "DreadnoughtArk", "HiveDrone", "HiveSpitter", "HiveSwarmer" });
        lance.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/CosmicBolt.png");
        EditorUtility.SetDirty(lance);
        SetupBosses("GalacticAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Galactic Age set up.");
    }
}
