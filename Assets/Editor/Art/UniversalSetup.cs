using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Universal Age: beyond one galaxy, the forces of creation and time themselves. Genesis Knights with worldblades, Reality Weavers, Seraphs on wings of light, Chronomancers who heal by turning back time, World Engines (a pilot inside a little planet), Chrono Titans and Genesis Ships. The Unmade come from the end of time to undo everything. Primordium (with rich veins) is refined with mana into Genesis Matter. Universal Cartography (Galactic research) opens it; every Galactic line ranks up.
/// Safe to run again. (Tools > Age of Stickman > Setup Universal Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Universal Age")]
    public static void SetupUniversal()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age prev = db.ages.Find(a => a != null && a.name == "GalacticAge");
        var age = AgeAsset(db, "UniversalAge", "Universal Age", 16,
            "Beyond one galaxy: the forces of creation and time. Blades that make worlds, wings of light, healers who turn back time, and the Unmade coming from the end of everything.");

        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores"), prevRefined = Res(db, "Quintessence");
        var raw = MedievalResource(db, "Primordium", "Primordium", age, "Quintessence");
        var refined = MedievalResource(db, "GenesisMatter", "Genesis Matter", age, "Primordium");
        var deposit = NewDeposit(db, "Primordium", "Primordium", raw, true, 0.72f, 0f, 1f, -1f);
        deposit.richVeins = true;
        EditorUtility.SetDirty(deposit);

        var gate = Tech(db, "UniversalCartography", "Universal Cartography", prev,
            "Past the edge of the galaxy, a map of everything: other galaxies, the first light, the shape of time. Opens the way to the Universal Age.",
            246f, Cost(food, 5510, gold, 1600, coin, 2320, prevRefined, 360, starmetal, 510), "StarGates");
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
        var resTech = T("PrimordialMining", "Primordial Mining", "Stone left over from the first moment of the universe, still warm with creation. Unlocks the Primordium Rift and the Genesis Crucible.",
            150f, Cost(steel, 1160, cores, 200, mana, 260), "UniversalCartography");
        var blades = T("Worldblades", "Worldblades", "A blade that cuts out of nothing a little of what was there before. Aegis Halls rank up into the Genesis Hall; all troops hit 4% harder.",
            150f, Cost(steel, 1160, refined, 102), resTech.id, resTech);
        blades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var ranged = T("RealityWeaving", "Reality Weaving", "Weaving the threads of what is into something else, and throwing it. Starweaver Conclaves rank up into the Loom of Worlds.",
            150f, Cost(gold, 1160, refined, 130, chips, 250), blades.id, blades);
        var air = T("SeraphFlight", "Seraph Flight", "Six wings of light and a halo for a heart. Comet Hangars rank up into the Seraph Spire; troops move 4% faster.",
            150f, Cost(cells, 510, refined, 130, mana, 200), ranged.id, resTech);
        air.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var healing = T("Chronomancy", "Chronomancy", "A wound is only something that happened. Undo the moment, undo the wound. Temples of Life rank up into the Chronosanctum; all troops have 4% more health.",
            140f, Cost(food, 4930, gold, 870, refined, 102), air.id);
        healing.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var lances = T("GenesisLances", "Genesis Lances", "Bolts of raw creation. Singularity Lances rank up into Genesis Lances, celestial into primordial walls; buildings have 8% more health.",
            155f, Cost(stone, 4930, refined, 230, starmetal, 200), healing.id, ranged);
        lances.buildingHealthMultiplier = 1.08f;
        var longTower = T("BigBangArrays", "First Light Arrays", "The light of the first moment, caught and aimed. Supernova Arrays rank up into First Light Arrays.",
            155f, Cost(gold, 1300, refined, 260, chips, 200), lances.id, resTech);
        var splashTower = T("EntropyBatteries", "Paradox Batteries", "A shot that lands before it is fired, and bursts everywhere it might have landed. Quasar Batteries rank up into Paradox Batteries.",
            155f, Cost(cells, 510, refined, 260, starmetal, 200), longTower.id, resTech);
        var forging = T("CreationForges", "Creation Forges", "Steel made the way the first stars made iron. Stellar Anvils rank up into the Creation Forge; steel comes 10% faster.",
            150f, Cost(steel, 1300, refined, 200), splashTower.id, healing);
        forging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var works = T("ClockworkOfTime", "Clockwork of Time", "Workshops where the hours run faster than outside. Celestial Works rank up into the Timeworks; everything you produce comes in 5% faster.",
            155f, Cost(gears, 870, refined, 200, cores, 140), forging.id, forging);
        works.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var heavyTech = T("WorldEngines", "World Engines", "A pilot inside a planet the size of a house, with its own sky and rings, that fires suns. Gate Foundries rank up into the World Engine Cradle.",
            165f, Cost(steel, 1600, refined, 320, cells, 320), works.id, ranged);
        var specialTech = T("ChronoTitans", "Chrono Titans", "A titan of bronze and stopped time, an hourglass for a heart. Avatar Sanctums rank up into the Titan Vault.",
            170f, Cost(starmetal, 650, refined, 320, chips, 320), heavyTech.id, works);
        var shipTech = T("GenesisShips", "Genesis Ships", "A living ship shaped like a seed, made to plant worlds, armed to defend them. Ark Yards rank up into the Seed Yard.",
            165f, Cost(steel, 1880, refined, 320, cores, 200), specialTech.id, air);
        var housingTech = T("WorldTrees", "World Trees", "Trees the size of cities, grown from seeds of creation, a people living in every branch. Celestial Spires rank up into World Trees.",
            140f, Cost(stone, 5510, refined, 200, steel, 510), shipTech.id);
        var moneyTech = T("UniversalTrade", "Universal Trade", "One coin for everything that is. Galactic Exchanges rank up into the Universal Exchange; everyone pays a little more in taxes.",
            140f, Cost(gold, 1880, coin, 1880, refined, 130), housingTech.id);
        moneyTech.coinPerPersonPerSecond = 0.002f;
        var sight = T("Omniscience", "Omniscience", "Seeing a little of everything, everywhere, at once. Research goes 5% faster.",
            150f, Cost(gold, 1600, refined, 200, chips, 260), moneyTech.id);
        sight.researchSpeedMultiplier = 1.05f;
        var powerTech = T("ZeroPointTaps", "Zero-Point Taps", "Power drawn from nothing at all, which turns out to be full of it. Dyson Relays rank up into Zero-Point Taps.",
            160f, Cost(stone, 4930, refined, 320, starmetal, 320), sight.id, resTech);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { gate },
            cost = Cost(food, 18850, wood, 11600, stone, 15950, iron, 4350, gold, 2610, coin, 3190, steel, 2610, prevRefined, 440, starmetal, 510),
            population = 1450,
        };
        EditorUtility.SetDirty(age);
        var melee = MedievalUnit("GenesisKnight", "Genesis Knight", age, "AegisKnight", 1566, 217, 0.9f, 0.9f, 1.15f);
        var rangedU = MedievalUnit("RealityWeaver", "Reality Weaver", age, "Starweaver", 870, 188, 1.6f, 8.5f, 1.0f);
        var flyer = MedievalUnit("Seraph", "Seraph", age, "CometRider", 1667, 152, 1.1f, 6.5f, 3.0f);
        flyer.movement = Movement.Air; flyer.flyHeight = 1.0f; flyer.populationCost = 2;
        var healer = MedievalUnit("Chronomancer", "Chronomancer", age, "Lifeweaver", 841, 116, 1.0f, 0.8f, 1.1f);
        healer.healer = true; healer.healRange = 18f;
        var heavy = MedievalUnit("WorldEngine", "World Engine", age, "GateColossus", 4205, 435, 3.2f, 11.5f, 0.9f);
        heavy.populationCost = 3; heavy.movement = Movement.Ground; heavy.flyHeight = 0f;
        var special = MedievalUnit("ChronoTitan", "Chrono Titan", age, "CelestialAvatar", 4060, 217, 0.95f, 1.2f, 1.05f);
        special.populationCost = 2;
        var ship = MedievalUnit("GenesisShip", "Genesis Ship", age, "DreadnoughtArk", 3625, 282, 2.4f, 7.5f, 1.45f);
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
        var eMelee = MedievalUnit("UnmadeHusk", "Unmade Husk", age, "HiveDrone", 1740, 174, 1.0f, 0.9f, 1.2f);
        var eRanged = MedievalUnit("UnmadeHerald", "Unmade Herald", age, "HiveSpitter", 899, 159, 1.7f, 8f, 1.0f);
        var eAir = MedievalUnit("UnmadeWraith", "Unmade Wraith", age, "HiveSwarmer", 1450, 130, 1.3f, 6.5f, 2.8f);
        eAir.movement = Movement.Air; eAir.flyHeight = 1.0f;
        foreach (var x in new[] { eMelee, eRanged, eAir }) EditorUtility.SetDirty(x);
        age.enemyUnits = new List<UnitDefinition> { eMelee, eRanged, eAir };
        EditorUtility.SetDirty(age);
        var genesisShot = PixelProjectile("GenesisBolt", 12, 9, c =>
        {
            var g = PixelPalette.Pearl;
            c.LineRaw(new Vector2(0, 4), new Vector2(6, 4), PixelPalette.Prism[0]);
            c.LineRaw(new Vector2(1, 3), new Vector2(6, 3), PixelPalette.Prism[1]); c.LineRaw(new Vector2(1, 5), new Vector2(6, 5), PixelPalette.Prism[2]);
            c.Ellipse(new Vector2(9, 4.5f), 2.6f, 2.6f, new[] { g[1], g[2], g[3], PixelPalette.White });
        });
        var firstLight = PixelProjectile("FirstLightRay", 22, 7, c =>
        {
            var g = PixelPalette.Pearl;
            c.LineRaw(new Vector2(0, 3), new Vector2(21, 3), PixelPalette.White);
            for (int i = 0; i < 4; i++) { c.LineRaw(new Vector2(2 + i, 2), new Vector2(21, 2), PixelPalette.Prism[i]); c.LineRaw(new Vector2(2 + i, 4), new Vector2(21, 4), PixelPalette.Prism[3 - i]); }
            c.LineRaw(new Vector2(6, 1), new Vector2(21, 1), g[2]); c.LineRaw(new Vector2(6, 5), new Vector2(21, 5), g[2]);
        });
        var paradox = PixelProjectile("ParadoxBurst", 14, 14, c =>
        {
            var g = PixelPalette.Chrono;
            c.Ellipse(new Vector2(7, 7), 6f, 6f, new[] { g[1], g[2], g[3], PixelPalette.White });
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; c.Put(Mathf.RoundToInt(7 + Mathf.Cos(a) * 6.5f), Mathf.RoundToInt(7 + Mathf.Sin(a) * 6.5f), PixelPalette.Prism[i % 4]); }
            c.LineRaw(new Vector2(7, 7), new Vector2(7, 11), g[0]); c.LineRaw(new Vector2(7, 7), new Vector2(10, 7), g[0]);
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
        var house = RankUp(db, "CelestialSpire", "WorldTree", "World Tree", age, housingTech,
            "A tree the size of a city, grown from a seed of creation, its branches full of homes and its leaves full of light. Houses 320 people.",
            Cost(stone, 1450, refined, 87, steel, 320), Cost(stone, 720, refined, 44), 2972);
        house.housing = 320;
        house.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "New Branches", description = "+35 people.", requirements = new Requirements { cost = Cost(stone, 1020, refined, 58) }, upgradeTime = 25f, extraHousing = 35 },
            new BuildingUpgrade { name = "Canopy Cities", description = "+40 people.", requirements = new Requirements { cost = Cost(refined, 116, gold, 320) }, upgradeTime = 30f, extraHousing = 40 },
        };
        var bank = RankUp(db, "GalacticExchange", "UniversalExchange", "Universal Exchange", age, moneyTech,
            "A hall where every coin that ever was is good, the ceiling a window onto a thousand galaxies.",
            Cost(stone, 1300, gold, 650, refined, 58), Cost(stone, 650, gold, 320), 2392);
        bank.workers = 3;
        bank.production = new List<ResourceRate> { Rate(coin, 9.3f) };
        bank.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        bank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Every Coin That Was", "+25% coin.", Cost(refined, 72, coin, 1020), 1.25f),
            Upgrade("The Last Ledger", "+25% coin.", Cost(refined, 116, gold, 510), 1.25f),
        };
        var meleeHall = RankUp(db, "AegisHall", "GenesisHall", "Genesis Hall", age, blades,
            "A pearl-white hall with a worldblade set over its door, a little new world turning on its point. Trains Genesis Knights.",
            Cost(stone, 1300, steel, 480, refined, 72), Cost(stone, 650, refined, 36), 2900);
        Trains(meleeHall, melee, 4, steel, gold, food, coin);
        var rangedHall = RankUp(db, "StarweaverConclave", "LoomOfWorlds", "Loom of Worlds", age, ranged,
            "A great loom where weavers pull threads of light out of the air and weave them into what they want. Trains Reality Weavers.",
            Cost(stone, 1300, steel, 480, refined, 72), Cost(stone, 650, refined, 36), 2247);
        Trains(rangedHall, rangedU, 4, steel, gold, food, coin);
        var airHall = RankUp(db, "CometHangar", "SeraphSpire", "Seraph Spire", age, air,
            "A white spire ringed with halos, where Seraphs fold their six wings to land. Trains Seraphs: flyers that strike from the sky.",
            Cost(stone, 1300, steel, 480, refined, 72), Cost(stone, 650, refined, 36), 2247);
        Trains(airHall, flyer, 3, steel, gold, food, coin);
        var temple = RankUp(db, "TempleOfLife", "Chronosanctum", "Chronosanctum", age, healing,
            "A temple round a great hourglass where the sand runs up as often as down. Trains Chronomancers.",
            Cost(stone, 1300, gold, 410, refined, 58), Cost(stone, 650, refined, 29), 2247);
        temple.trainsUnit = healer;
        temple.maxUnits = 3;
        temple.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Hourglass", description = "+1 chronomancer.", requirements = new Requirements { cost = Cost(refined, 58, gold, 230) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Borrowed Hours", description = "Chronomancers heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 116, gold, 320) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };
        var lance = RankUp(db, "SingularityLance", "GenesisLance", "Genesis Lance", age, lances,
            "A pearl spire with a seed of creation at its tip that fires bolts of rainbow light.",
            Cost(stone, 940, refined, 102, starmetal, 44), Cost(stone, 480, refined, 51), 4060);
        Crewless(lance, 13.5f, 113, 0.4f, genesisShot, 0f, 34f, 0.9f, 32f, 54f);
        lance.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Reach", description = "+1.5 range.", requirements = new Requirements { cost = Cost(refined, 87, stone, 480) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Harder Strikes", description = "+22 damage.", requirements = new Requirements { cost = Cost(refined, 116, cores, 44) }, upgradeTime = 25f, extraTowerDamage = 22 },
        };
        var longT = RankUp(db, "SupernovaArray", "FirstLightArray", "First Light Array", age, longTower,
            "A prism the size of a house, splitting the light of the first moment into a beam. It burns raiders from very far away.",
            Cost(stone, 1090, refined, 170, gold, 320), Cost(stone, 550, refined, 87), 2827);
        Crewless(longT, 26f, 754, 2.8f, firstLight, 0f, 68f, 1.5f, 32f, 52f);
        longT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Further Still", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 140, gold, 230) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Heavier Blows", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 200, cores, 87) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var splashT = RankUp(db, "QuasarBattery", "ParadoxBattery", "Paradox Battery", age, splashTower,
            "A tower with a clock for a face that fires before it aims. Each shot bursts wide.",
            Cost(stone, 1230, refined, 140, cells, 230), Cost(stone, 610, refined, 72), 4640);
        Crewless(splashT, 17f, 725, 3.1f, paradox, 0.2f, 16f, 3.1f, 32f, 42f);
        splashT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 140, cells, 190) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Wider Bursts", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 200, cores, 87) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var wall = RankUp(db, "CelestialWall", "PrimordialWall", "Primordial Wall", age, lances,
            "A wall of pearl-white stone from the first moment, rainbow light running in its seams.",
            Cost(stone, 18, refined, 2), Cost(stone, 15, refined, 2), 9860);
        var gateB = RankUp(db, "CelestialGate", "PrimordialGate", "Primordial Gate", age, lances,
            "A gate of pearl-white stone under an arch of light.",
            Cost(stone, 34, refined, 6), Cost(stone, 30, refined, 4), 13050);
        wall.upgrades = new List<BuildingUpgrade>();
        gateB.upgrades = new List<BuildingUpgrade>();
        var forge = RankUp(db, "StellarAnvil", "CreationForge", "Creation Forge", age, forging,
            "A forge round a little star, beaten into metal the way the first stars made iron. Steel without end.",
            Cost(stone, 1090, steel, 650, refined, 87), Cost(stone, 550, refined, 44), 2755);
        forge.workers = 5;
        forge.production = new List<ResourceRate> { Rate(steel, 1.67f) };
        forge.inputs = new List<ResourceRate> { Rate(iron, 0.48f), Rate(refined, 0.012f) };
        forge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Younger Star", "+30% steel.", Cost(refined, 116, stone, 650), 1.3f),
            Upgrade("First Iron", "+30% steel.", Cost(steel, 650, cores, 58), 1.3f),
        };
        var worksB = RankUp(db, "CelestialWorks", "Timeworks", "Timeworks", age, works,
            "A workshop under a great clock where the hours run faster than outside. Gears without end.",
            Cost(stone, 940, gears, 410, refined, 58), Cost(stone, 480, refined, 29), 2392);
        worksB.workers = 4;
        worksB.production = new List<ResourceRate> { Rate(gears, 1.01f) };
        worksB.inputs = new List<ResourceRate> { Rate(steel, 0.2f), Rate(refined, 0.008f) };
        worksB.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Faster Hours", "+30% gears.", Cost(refined, 87, gears, 230), 1.3f),
            Upgrade("Stolen Minutes", "+30% gears.", Cost(steel, 480, cores, 58), 1.3f),
        };
        var heavyHall = RankUp(db, "GateFoundry", "WorldEngineCradle", "World Engine Cradle", age, heavyTech,
            "A cradle where little planets are grown, given rings and a sky, and a pilot. Builds World Engines: planets that fire suns.",
            Cost(stone, 1300, steel, 940, refined, 160), Cost(stone, 650, refined, 80), 3480);
        heavyHall.trainsUnit = heavy;
        heavyHall.maxUnits = 2;
        heavyHall.workers = 6;
        heavyHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Cradle", description = "+1 world engine.", requirements = new Requirements { cost = Cost(steel, 800, refined, 130) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Brighter Suns", description = "World Engines are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 190, starmetal, 190) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var specialHall = RankUp(db, "AvatarSanctum", "TitanVault", "Titan Vault", age, specialTech,
            "A bronze vault where time stands still, Chrono Titans waiting in it for the moment they're needed.",
            Cost(stone, 1300, steel, 940, refined, 160), Cost(stone, 650, refined, 80), 3480);
        specialHall.trainsUnit = special;
        specialHall.maxUnits = 4;
        specialHall.workers = 4;
        specialHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Titan", description = "+1 chrono titan.", requirements = new Requirements { cost = Cost(steel, 800, refined, 130) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Stopped Hearts", description = "Chrono Titans hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 190, starmetal, 190) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var shipHall = RankUp(db, "ArkYard", "SeedYard", "Seed Yard", age, shipTech,
            "A garden of gantries where living ships grow like seeds. Builds Genesis Ships, and launches ships to other worlds (open the star map).",
            Cost(stone, 1300, steel, 940, refined, 160), Cost(stone, 650, refined, 80), 3480);
        shipHall.trainsUnit = ship;
        shipHall.maxUnits = 2;
        shipHall.workers = 4;
        shipHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Seed", description = "+1 genesis ship.", requirements = new Requirements { cost = Cost(steel, 800, refined, 130) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Living Hulls", description = "Genesis Ships are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 190, starmetal, 190) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        shipHall.launchesShips = true;
        var power = RankUp(db, "DysonRelay", "ZeroPointTap", "Zero-Point Tap", age, powerTech,
            "A ring round nothing at all, drawing power out of the empty middle. Power cells without end.",
            Cost(stone, 2180, refined, 230, steel, 800), Cost(stone, 1090, refined, 116), 3190);
        power.workers = 5;
        power.production = new List<ResourceRate> { Rate(cells, 1.30f) };
        power.inputs = new List<ResourceRate> { Rate(mana, 0.02f) };
        power.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Emptier Nothing", "+30% power cells.", Cost(stone, 1300, refined, 116), 1.3f),
            Upgrade("Deeper Zero", "+30% power cells.", Cost(refined, 190, cores, 87), 1.3f),
        };
        var mine = NewBuilding(db, "StardustCollector", "PrimordiumRift", "Primordium Rift", age, resTech, new Vector2Int(3, 3),
            "A crack in the world where stone from the first moment of everything wells up, still glowing. Must be built on or next to Primordium, or right on a rich primordium vein.",
            Cost(steel, 580, stone, 800, mana, 102), 1450);
        mine.requiredDeposit = deposit;
        mine.canBuildOnHills = true;
        mine.workers = 5;
        mine.production = new List<ResourceRate> { Rate(raw, 0.13f) };
        mine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Deeper Crack", "+30% primordium.", Cost(steel, 480, mana, 102), 1.3f),
            Upgrade("First Seams", "+30% primordium.", Cost(chips, 160, gold, 320), 1.3f),
        };
        var refinery = NewBuilding(db, "QuintessenceDistillery", "GenesisCrucible", "Genesis Crucible", age, resTech, new Vector2Int(3, 3),
            "Primordium is melted with mana into Genesis Matter: the stuff of new worlds.",
            Cost(stone, 1300, steel, 480, gold, 320), 1885);
        refinery.workers = 5;
        refinery.production = new List<ResourceRate> { Rate(refined, 0.09f) };
        refinery.inputs = new List<ResourceRate> { Rate(raw, 0.1f), Rate(mana, 0.03f) };
        refinery.runsWithoutInputs = 0f;
        refinery.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Hotter Crucible", "+30% genesis matter.", Cost(stone, 940, gold, 320), 1.3f),
            Upgrade("Mana of Making", "+30% genesis matter.", Cost(mana, 160, chips, 130), 1.3f),
        };

        // Cosmic Harmony: the last research, taken again and again (to a cap): a little better at everything each time.
        var harmony = T("CosmicHarmony", "Cosmic Harmony", "Living in tune with everything that is. Each level: troops hit 1% harder and are 1% tougher, and everything you produce comes in 1% faster. Up to 25 levels.",
            200f, Cost(refined, 300, starmetal, 300, cores, 200), powerTech.id, sight);
        harmony.maxLevel = 25;
        harmony.levelCostGrowth = 1.15f;
        harmony.levelTimeGrowth = 1.08f;
        harmony.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.01f, healthMultiplier = 1.01f } };
        harmony.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.01f } };
        EditorUtility.SetDirty(harmony);

        // The Genesis Engine: the age's megastructure, a machine that makes matter out of nothing.
        var engineTech = T("GenesisEngines", "Genesis Engines", "A machine that makes matter out of nothing. Unlocks the Genesis Engine: while one stands, every one of your worlds produces 5% more (up to 5 engines).",
            260f, Cost(steel, 3200, refined, 640, starmetal, 640, cores, 320), harmony.id, shipTech);
        var engine = NewBuilding(db, "StarGate", "GenesisEngine", "Genesis Engine", age, engineTech, new Vector2Int(4, 4),
            "A vast gold armillary turning round a heart of new creation, making matter out of nothing. While it stands, every one of your worlds produces 5% more (up to 5 engines).",
            Cost(steel, 4500, stone, 6000, refined, 900, starmetal, 750, cores, 450), 10000);
        engine.starGate = false;
        engine.genesisEngine = true;
        engine.launchesShips = false;
        engine.trainsUnit = null;
        engine.workers = 0;
        engine.housing = 0;
        engine.production = new List<ResourceRate>();
        engine.inputs = new List<ResourceRate> { Rate(cells, 0.06f) };
        engine.upgrades = new List<BuildingUpgrade>();

        var all = new[] { house, bank, meleeHall, rangedHall, airHall, temple, lance, longT, splashT, wall, gateB, forge, worksB, heavyHall, specialHall, shipHall, power, mine, refinery, engine };
        foreach (var x in all) EditorUtility.SetDirty(x);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        deposit.sprites = PixelDeposits("Primordium", 3, Ore.Primordium);
        EditorUtility.SetDirty(deposit);
        foreach (var x in all)
            if (!x.IsWall) GenerateBuildingLooks(x);
        GenerateWallArt(16);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "GenesisKnight", "RealityWeaver", "Seraph", "Chronomancer", "WorldEngine", "ChronoTitan", "GenesisShip", "UnmadeHusk", "UnmadeHerald", "UnmadeWraith" });
        lance.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/GenesisBolt.png");
        EditorUtility.SetDirty(lance);
        SetupBosses("UniversalAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Universal Age set up.");
    }
}
