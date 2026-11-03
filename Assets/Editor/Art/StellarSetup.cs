using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Stellar Age: your own sun harnessed, by mirrors, collectors and old sun-magic together.
/// Solar Paladins with burning blades, Helion Magi with sun-staffs, Sunwings on wings of light,
/// Dawnkeepers, the hovering Sun Orb siege engine, Radiant Automatons (the construct's
/// successor) and Solar Barques under sails of light. The Eclipse cult raids in the dark between
/// the stars. Heliolite (with rich veins) is refined with mana into Sunfire. Stellar Harmonics
/// (Astral research) opens it; every Astral line ranks up. Safe to run again.
/// (Tools > Age of Stickman > Setup Stellar Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Stellar Age")]
    public static void SetupStellar()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age astral = db.ages.Find(a => a != null && a.name == "AstralAge");
        var age = AgeAsset(db, "StellarAge", "Stellar Age", 13,
            "Your own sun harnessed: burning blades and sun-staffs, wings of light, orbs of captured sunfire, and ships under sails of light.");

        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores");
        var heliolite = MedievalResource(db, "Heliolite", "Heliolite", age, "AstralCores");
        var sunfire = MedievalResource(db, "Sunfire", "Sunfire", age, "Heliolite");
        var helioDeposit = NewDeposit(db, "Heliolite", "Heliolite", heliolite, true, 0.72f, 0f, 1f, -1f);
        helioDeposit.richVeins = true;
        EditorUtility.SetDirty(helioDeposit);

        // ---------- Research ----------
        var gate = Tech(db, "StellarHarmonics", "Stellar Harmonics", astral,
            "The sun sings, and the old sun-hymns were its sheet music. Opens the way to the Stellar Age.",
            160f, Cost(food, 3400, gold, 1000, coin, 1400, cores, 200, starmetal, 300), "AstralReactors");
        gate.requirements.minimumAge = astral;
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
        var helio = T("Heliology", "Heliology", "Sunlight caught in stone. Unlocks the Heliolite Pit and the Sunfire Refinery.",
            140f, Cost(steel, 700, cores, 120, mana, 150), "StellarHarmonics");
        var sunblades = T("Sunblades", "Sunblades", "Blades that burn with the sun's own fire. Starknight Halls rank up into the Paladin Citadel; all troops hit 4% harder.",
            140f, Cost(steel, 700, sunfire, 60), helio.id, helio);
        sunblades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var heliomancy = T("Heliomancy", "Heliomancy", "Staffs that fire captured sunlight. Technomancy Colleges rank up into the Helion Academy.",
            140f, Cost(gold, 700, sunfire, 80, chips, 150), sunblades.id, sunblades);
        var wings = T("WingsOfLight", "Wings of Light", "Wings woven from light and mana. Skylancer Hangars rank up into the Sunwing Aerie; troops move 4% faster.",
            140f, Cost(cells, 300, sunfire, 80, mana, 120), heliomancy.id, helio);
        wings.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var dawn = T("DawnRites", "Dawn Rites", "Healing in the first light of morning. Astral Sanctums rank up into the Dawn Cathedral; all troops have 4% more health.",
            130f, Cost(food, 3000, gold, 500, sunfire, 60), wings.id);
        dawn.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var sunLances = T("SunLances", "Sun Lances", "Lances of focused sunlight. Astral Lances rank up into Sun Lances, starward into sunward walls; buildings have 8% more health.",
            145f, Cost(stone, 3000, sunfire, 140, starmetal, 120), dawn.id, heliomancy);
        sunLances.buildingHealthMultiplier = 1.08f;
        var mirrors = T("BurningMirrors", "Burning Mirrors", "A mirror the size of a field, aimed by a mage. Meteor Arrays rank up into Burning Mirrors.",
            145f, Cost(gold, 800, sunfire, 160, chips, 120), sunLances.id, helio);
        var flares = T("SolarFlares", "Solar Flares", "A piece of the sun, thrown. Nova Batteries rank up into Flare Cannons.",
            145f, Cost(cells, 300, sunfire, 160, starmetal, 120), mirrors.id, helio);
        var sunforging = T("SunForging", "Sun Forging", "Forges heated by focused sunlight. Star Foundries rank up into the Sun Forge; steel comes 10% faster.",
            140f, Cost(steel, 800, sunfire, 120), flares.id, dawn);
        sunforging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var radiant = T("RadiantWorks", "Radiant Works", "Constructs that run on sunfire. Construct Works rank up into Radiant Works; everything you produce comes in 5% faster.",
            145f, Cost(gears, 500, sunfire, 120, cores, 80), sunforging.id, sunforging);
        radiant.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var orbs = T("SunOrbs", "Sun Orbs", "A sphere of glass holding a captured sun, and a pilot brave enough to steer it. Throne Works rank up into the Orb Forge.",
            155f, Cost(steel, 1000, sunfire, 200, cells, 200), radiant.id, heliomancy);
        var automata = T("RadiantAutomata", "Radiant Automata", "A construct with a sun for a heart and a halo for a mind. Construct Vaults rank up into the Radiant Sanctum.",
            160f, Cost(starmetal, 400, sunfire, 200, chips, 200), orbs.id, radiant);
        var barques = T("SolarBarques", "Solar Barques", "Ships that sail on sunlight itself. Starports rank up into the Solar Dock.",
            155f, Cost(steel, 1200, sunfire, 200, cores, 120), automata.id, wings);
        var helionTowers = T("HelionTowers", "Helion Towers", "Towers that drink the sun all day and glow all night. Star Arcologies rank up into Helion Towers.",
            130f, Cost(stone, 3400, sunfire, 120, steel, 300), barques.id);
        var sunmarket = T("SunMarkets", "Sun Markets", "Trade in bottled sunlight. Star Exchanges rank up into the Sunmarket; everyone pays a little more in taxes.",
            130f, Cost(gold, 1200, coin, 1200, sunfire, 80), helionTowers.id);
        sunmarket.coinPerPersonPerSecond = 0.002f;
        var solarSight = T("SolarSight", "Solar Sight", "Seeing by the light of every star at once. Research goes 5% faster.",
            140f, Cost(gold, 1000, sunfire, 120, chips, 160), sunmarket.id);
        solarSight.researchSpeedMultiplier = 1.05f;
        var collectors = T("SolarCollectors", "Solar Collectors", "Fields of mirrors feeding a tower of light. Astral Reactors rank up into Solar Collectors.",
            150f, Cost(stone, 3000, sunfire, 200, starmetal, 200), solarSight.id, helio);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { gate },
            cost = Cost(food, 12000, wood, 7500, stone, 10000, iron, 2800, gold, 1600, coin, 2000, steel, 1600, cores, 300, starmetal, 300),
            population = 1100,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var paladin = MedievalUnit("SolarPaladin", "Solar Paladin", age, "Starknight", 1080, 150, 0.9f, 0.9f, 1.15f);
        var magus = MedievalUnit("HelionMagus", "Helion Magus", age, "Technomancer", 600, 130, 1.6f, 8.5f, 1.0f);
        var sunwing = MedievalUnit("Sunwing", "Sunwing", age, "Skylancer", 1150, 105, 1.1f, 6.5f, 3.0f);
        sunwing.movement = Movement.Air; sunwing.flyHeight = 1.0f; sunwing.populationCost = 2;
        var dawnkeeper = MedievalUnit("Dawnkeeper", "Dawnkeeper", age, "Starmender", 580, 80, 1.0f, 0.8f, 1.1f);
        dawnkeeper.healer = true; dawnkeeper.healRange = 17f;
        var sunOrb = MedievalUnit("SunOrb", "Sun Orb", age, "NovaThrone", 2900, 300, 3.2f, 11f, 0.9f);
        sunOrb.populationCost = 3; sunOrb.movement = Movement.Ground; sunOrb.flyHeight = 0f;
        var automaton = MedievalUnit("RadiantAutomaton", "Radiant Automaton", age, "AstralConstruct", 2800, 150, 0.95f, 1.2f, 1.05f);
        automaton.populationCost = 2;
        var barque = MedievalUnit("SolarBarque", "Solar Barque", age, "StarFrigate", 2500, 195, 2.4f, 7f, 1.4f);
        barque.populationCost = 3; barque.movement = Movement.Air; barque.flyHeight = 1.9f;
        foreach (var u in new[] { paladin, magus, sunwing, dawnkeeper })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.015f), Rate(coin, 0.1f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { sunOrb, automaton, barque })   // they burn sunfire
        {
            u.upkeep = new List<ResourceRate> { Rate(sunfire, 0.012f), Rate(coin, 0.1f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { paladin, magus, sunwing, dawnkeeper, sunOrb, automaton, barque };
        var zealot = MedievalUnit("EclipseZealot", "Eclipse Zealot", age, "Voidreaver", 1200, 120, 1.0f, 0.9f, 1.2f);
        var seer = MedievalUnit("EclipseSeer", "Eclipse Seer", age, "VoidCaster", 620, 110, 1.7f, 8f, 1.0f);
        var shade = MedievalUnit("ShadeGlider", "Shade Glider", age, "VoidSkiff", 1000, 90, 1.3f, 6.5f, 2.8f);
        shade.movement = Movement.Air; shade.flyHeight = 1.0f;
        foreach (var u in new[] { zealot, seer, shade }) EditorUtility.SetDirty(u);
        age.enemyUnits = new List<UnitDefinition> { zealot, seer, shade };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var tower = RankUp(db, "StarArcology", "HelionTower", "Helion Tower", age, helionTowers,
            "A white tower crowned with a sun-disc that drinks the day and glows all night. Houses 210 people.",
            Cost(stone, 900, sunfire, 60, steel, 200), Cost(stone, 450, sunfire, 30), 1850);
        tower.housing = 210;
        tower.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Sun Terraces", description = "+30 people.", requirements = new Requirements { cost = Cost(stone, 600, sunfire, 40) }, upgradeTime = 25f, extraHousing = 30 },
            new BuildingUpgrade { name = "Halls of Light", description = "+35 people.", requirements = new Requirements { cost = Cost(sunfire, 80, gold, 200) }, upgradeTime = 30f, extraHousing = 35 },
        };
        var market = RankUp(db, "StarExchange", "Sunmarket", "Sunmarket", age, sunmarket,
            "A golden hall where bottled sunlight is traded. Coin from sunfire.",
            Cost(stone, 800, gold, 400, sunfire, 40), Cost(stone, 400, gold, 200), 1500);
        market.workers = 3;
        market.production = new List<ResourceRate> { Rate(coin, 5.6f) };
        market.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        market.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Bottled Dawn", "+25% coin.", Cost(sunfire, 50, coin, 600), 1.25f),
            Upgrade("Solar Guilds", "+25% coin.", Cost(sunfire, 80, gold, 300), 1.25f),
        };
        var citadel = RankUp(db, "StarknightHall", "PaladinCitadel", "Paladin Citadel", age, sunblades,
            "A white citadel, a burning sword over its gate. Trains Solar Paladins.",
            Cost(stone, 800, steel, 300, sunfire, 50), Cost(stone, 400, sunfire, 25), 1900);
        Trains(citadel, paladin, 4, steel, gold, food, coin);
        var academy = RankUp(db, "TechnomancyCollege", "HelionAcademy", "Helion Academy", age, heliomancy,
            "A tower with a sun-disc for a crown and a yard of burning targets. Trains Helion Magi.",
            Cost(stone, 700, gold, 300, sunfire, 40), Cost(stone, 350, sunfire, 20), 1450);
        Trains(academy, magus, 4, steel, gold, food, coin);
        var aerie = RankUp(db, "SkylancerHangar", "SunwingAerie", "Sunwing Aerie", age, wings,
            "A tall perch of white stone where wings of light are woven. Trains Sunwings: flyers who strike from the sky.",
            Cost(stone, 700, sunfire, 60, mana, 80), Cost(stone, 350, sunfire, 30), 1450);
        Trains(aerie, sunwing, 3, steel, gold, food, coin);
        var cathedral = RankUp(db, "AstralSanctum", "DawnCathedral", "Dawn Cathedral", age, dawn,
            "A cathedral of white glass, lit gold from within. Trains Dawnkeepers.",
            Cost(stone, 800, gold, 250, sunfire, 40), Cost(stone, 400, sunfire, 20), 1450);
        cathedral.trainsUnit = dawnkeeper;
        cathedral.maxUnits = 3;
        cathedral.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Morning Choir", description = "+1 dawnkeeper.", requirements = new Requirements { cost = Cost(sunfire, 40, gold, 150) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "First Light", description = "Dawnkeepers heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(sunfire, 80, gold, 200) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var sunBolt = PixelProjectile("SunBolt", 12, 7, c =>
        {
            var s = PixelPalette.Solar;
            c.LineRaw(new Vector2(0, 3), new Vector2(7, 3), s[1]); c.LineRaw(new Vector2(2, 4), new Vector2(8, 4), s[2]);
            c.Ellipse(new Vector2(9, 3.5f), 2.4f, 2.4f, new[] { s[1], s[2], s[3], PixelPalette.White });
        });
        var flare = PixelProjectile("Flare", 14, 14, c =>
        {
            var s = PixelPalette.Solar;
            c.Ellipse(new Vector2(7, 7), 6f, 6f, new[] { PixelPalette.Fire[1], s[1], s[2], s[3] });
            c.Ellipse(new Vector2(7, 7), 3f, 3f, new[] { s[3], PixelPalette.White, PixelPalette.White, PixelPalette.White });
            c.Put(7, 13, s[3]); c.Put(7, 0, s[3]); c.Put(0, 7, s[3]); c.Put(13, 7, s[3]);
        });
        var ray = PixelProjectile("SolarRay", 20, 5, c =>
        {
            var s = PixelPalette.Solar;
            c.LineRaw(new Vector2(0, 2), new Vector2(19, 2), s[3]); c.LineRaw(new Vector2(0, 1), new Vector2(19, 1), s[2]); c.LineRaw(new Vector2(0, 3), new Vector2(19, 3), s[2]);
            c.Put(19, 2, PixelPalette.White);
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
        var sunLance = RankUp(db, "AstralLance", "SunLance", "Sun Lance", age, sunLances,
            "A white needle crowned with a lens of heliolite that fires bolts of sunlight.",
            Cost(stone, 600, sunfire, 70, starmetal, 30), Cost(stone, 300, sunfire, 35), 2600);
        Crewless(sunLance, 12f, 70, 0.4f, sunBolt, 0f, 32f, 0.9f, 32f, 54f);
        sunLance.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Clearer Lens", description = "+1.5 range.", requirements = new Requirements { cost = Cost(sunfire, 60, stone, 300) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Noon Light", description = "+20 damage.", requirements = new Requirements { cost = Cost(sunfire, 80, cores, 30) }, upgradeTime = 25f, extraTowerDamage = 20 },
        };
        var mirror = RankUp(db, "MeteorArray", "BurningMirror", "Burning Mirror", age, mirrors,
            "A great curved mirror on a turret, a mage at its foot aiming it. It burns raiders from very far away.",
            Cost(stone, 700, sunfire, 120, gold, 200), Cost(stone, 350, sunfire, 60), 1800);
        Crewless(mirror, 23f, 480, 2.8f, ray, 0f, 60f, 1.4f, 32f, 50f);
        mirror.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Wider Glass", description = "+2 range.", requirements = new Requirements { cost = Cost(sunfire, 100, gold, 150) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Noonday Focus", description = "+150 damage.", requirements = new Requirements { cost = Cost(sunfire, 140, cores, 60) }, upgradeTime = 30f, extraTowerDamage = 150 },
        };
        var cannon = RankUp(db, "NovaBattery", "FlareCannon", "Flare Cannon", age, flares,
            "A squat white tower with a ring of mirrors round a captive flare. Each shot bursts wide.",
            Cost(stone, 800, sunfire, 100, cells, 150), Cost(stone, 400, sunfire, 50), 3000);
        Crewless(cannon, 15.5f, 460, 3.1f, flare, 0.2f, 16f, 3f, 32f, 42f);
        cannon.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(sunfire, 100, cells, 120) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Hotter Flares", description = "+150 damage.", requirements = new Requirements { cost = Cost(sunfire, 140, cores, 60) }, upgradeTime = 30f, extraTowerDamage = 150 },
        };
        var sunWall = RankUp(db, "StarwardWall", "SunwardWall", "Sunward Wall", age, sunLances,
            "White stone inlaid with gold suns that glow at dusk.", Cost(stone, 16, sunfire, 2), Cost(stone, 14, sunfire, 2), 6200);
        var sunGate = RankUp(db, "StarwardGate", "SunwardGate", "Sunward Gate", age, sunLances,
            "A gate of white stone and gold that opens like a sunrise.", Cost(stone, 32, sunfire, 6), Cost(stone, 28, sunfire, 4), 8200);
        sunWall.upgrades = new List<BuildingUpgrade>();
        sunGate.upgrades = new List<BuildingUpgrade>();

        var forge = RankUp(db, "StarFoundry", "SunForge", "Sun Forge", age, sunforging,
            "A forge under a great mirror, its crucible heated by focused sunlight. Steel without end.",
            Cost(stone, 700, steel, 400, sunfire, 60), Cost(stone, 350, sunfire, 30), 1750);
        forge.workers = 5;
        forge.production = new List<ResourceRate> { Rate(steel, 1.0f) };
        forge.inputs = new List<ResourceRate> { Rate(iron, 0.44f), Rate(sunfire, 0.012f) };
        forge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Second Mirror", "+30% steel.", Cost(sunfire, 80, stone, 400), 1.3f),
            Upgrade("Noon Crucibles", "+30% steel.", Cost(steel, 400, cores, 40), 1.3f),
        };
        var works = RankUp(db, "ConstructWorks", "RadiantWorks", "Radiant Works", age, radiant,
            "A glass hall where radiant constructs work without rest. Gears without end.",
            Cost(stone, 600, gears, 250, sunfire, 40), Cost(stone, 300, sunfire, 20), 1500);
        works.workers = 4;
        works.production = new List<ResourceRate> { Rate(gears, 0.6f) };
        works.inputs = new List<ResourceRate> { Rate(steel, 0.18f), Rate(sunfire, 0.008f) };
        works.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Hands of Light", "+30% gears.", Cost(sunfire, 60, gears, 150), 1.3f),
            Upgrade("Day and Night", "+30% gears.", Cost(steel, 300, cores, 40), 1.3f),
        };
        var orbForge = RankUp(db, "ThroneWorks", "OrbForge", "Orb Forge", age, orbs,
            "A domed forge where suns are caught in glass. Builds Sun Orbs: hovering spheres of sunfire that burn whole raids.",
            Cost(stone, 800, steel, 600, sunfire, 100), Cost(stone, 400, sunfire, 50), 2300);
        orbForge.trainsUnit = sunOrb;
        orbForge.maxUnits = 2;
        orbForge.workers = 6;
        orbForge.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Kiln", description = "+1 sun orb.", requirements = new Requirements { cost = Cost(steel, 500, sunfire, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Tempered Glass", description = "Sun orbs are 25% tougher.", requirements = new Requirements { cost = Cost(sunfire, 120, starmetal, 120) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var radiantSanctum = RankUp(db, "ConstructVault", "RadiantSanctum", "Radiant Sanctum", age, automata,
            "A ring of white pillars round a pool of light, where automata stand up haloed.",
            Cost(stone, 1400, sunfire, 200, cores, 100), Cost(stone, 700, sunfire, 100), 1800);
        radiantSanctum.trainsUnit = automaton;
        radiantSanctum.maxUnits = 4;
        radiantSanctum.workers = 4;
        radiantSanctum.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Pool", description = "+1 automaton.", requirements = new Requirements { cost = Cost(sunfire, 150, cores, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Brighter Halos", description = "Automata hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(sunfire, 120, chips, 150) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var dock = RankUp(db, "Starport", "SolarDock", "Solar Dock", age, barques,
            "A tall dock with golden mooring arms, a barque's sails of light unfurling above it. Builds Solar Barques, and launches ships to other worlds (open the star map).",
            Cost(steel, 1000, sunfire, 120, cores, 80), Cost(steel, 500, sunfire, 60), 1700);
        dock.launchesShips = true;
        dock.trainsUnit = barque;
        dock.maxUnits = 2;
        dock.workers = 4;
        dock.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Mooring", description = "+1 barque.", requirements = new Requirements { cost = Cost(steel, 800, sunfire, 80) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Light Wards", description = "Barques are 25% tougher.", requirements = new Requirements { cost = Cost(sunfire, 100, cores, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var collector = RankUp(db, "AstralReactor", "SolarCollector", "Solar Collector", age, collectors,
            "A tower of light fed by a field of mirrors that turn with the sun. Power cells without end.",
            Cost(stone, 1400, sunfire, 150, steel, 500), Cost(stone, 700, sunfire, 80), 2000);
        collector.workers = 5;
        collector.production = new List<ResourceRate> { Rate(cells, 0.75f) };
        collector.inputs = new List<ResourceRate> { Rate(mana, 0.02f) };
        collector.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Mirrors", "+30% power cells.", Cost(stone, 800, sunfire, 80), 1.3f),
            Upgrade("Noon Tracking", "+30% power cells.", Cost(sunfire, 120, cores, 60), 1.3f),
        };
        var pit = NewBuilding(db, "MeteorMine", "HeliolitePit", "Heliolite Pit", age, helio, new Vector2Int(3, 3),
            "An open pit glowing orange from the heliolite in its walls. Must be built on or next to Heliolite, or right on a rich heliolite vein.",
            Cost(steel, 350, stone, 500, mana, 60), 900);
        pit.requiredDeposit = helioDeposit;
        pit.canBuildOnHills = true;
        pit.workers = 5;
        pit.production = new List<ResourceRate> { Rate(heliolite, 0.13f) };
        pit.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Cool Picks", "+30% heliolite.", Cost(steel, 300, mana, 60), 1.3f),
            Upgrade("Sun Dowsing", "+30% heliolite.", Cost(chips, 100, gold, 200), 1.3f),
        };
        var refinery = NewBuilding(db, "CoreForge", "SunfireRefinery", "Sunfire Refinery", age, helio, new Vector2Int(3, 3),
            "Heliolite is melted with mana under a lens into Sunfire, bottled light that burns forever.",
            Cost(stone, 800, steel, 300, gold, 200), 1200);
        refinery.workers = 5;
        refinery.production = new List<ResourceRate> { Rate(sunfire, 0.09f) };
        refinery.inputs = new List<ResourceRate> { Rate(heliolite, 0.1f), Rate(mana, 0.03f) };
        refinery.runsWithoutInputs = 0f;
        refinery.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Finer Lenses", "+30% sunfire.", Cost(stone, 600, gold, 200), 1.3f),
            Upgrade("Mana Cooling", "+30% sunfire.", Cost(mana, 100, chips, 80), 1.3f),
        };

        var all = new[] { tower, market, citadel, academy, aerie, cathedral, sunLance, mirror, cannon, sunWall, sunGate, forge, works, orbForge, radiantSanctum, dock, collector, pit, refinery };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds) ----------
        helioDeposit.sprites = PixelDeposits("Heliolite", 3, Ore.Heliolite);
        EditorUtility.SetDirty(helioDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(13);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "SolarPaladin", "HelionMagus", "Sunwing", "Dawnkeeper", "SunOrb", "RadiantAutomaton", "SolarBarque", "EclipseZealot", "EclipseSeer", "ShadeGlider" });
        sunLance.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/SunBolt.png");
        EditorUtility.SetDirty(sunLance);
        SetupBosses("StellarAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Stellar Age set up.");
    }
}
