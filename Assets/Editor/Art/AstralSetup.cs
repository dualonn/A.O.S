using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Astral Age, magic and sci-fi fully fused: the Arcane Age's mana, built into star-steel machines
/// that reach for the sky. Starknights in power armour with blades of starlight, technomancers with beam
/// staffs, skylancers on mana jetbikes, nova thrones, astral constructs and mana-drive starships. Void
/// raiders come down from beyond the sky. Starmetal, from fallen meteors (with rich veins), is forged with
/// mana into Astral Cores. Astral Navigation (Arcane research) opens it; every Arcane line ranks up.
/// Safe to run again. (Tools > Age of Stickman > Setup Astral Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Astral Age")]
    public static void SetupAstral()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age arcane = db.ages.Find(a => a != null && a.name == "ArcaneAge");
        var age = AgeAsset(db, "AstralAge", "Astral Age", 12,
            "Magic and machine as one: blades of starlight, mana-drive starships and hovering nova thrones, and the first look at the worlds beyond the sky.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), runestone = Res(db, "Runestone"),
            manastone = Res(db, "Manastone"), mana = Res(db, "Mana");
        var starmetal = MedievalResource(db, "Starmetal", "Starmetal", age, "Mana");
        var cores = MedievalResource(db, "AstralCores", "Astral Cores", age, "Starmetal");
        var starDeposit = NewDeposit(db, "Starmetal", "Starmetal", starmetal, false, 0.72f, 0f, 1f, -1f);
        starDeposit.richVeins = true;
        EditorUtility.SetDirty(starDeposit);

        // ---------- Research ----------
        var navigation = Tech(db, "AstralNavigation", "Astral Navigation", arcane,
            "The leylines do not stop at the ground: they run up into the sky, and out to the seven worlds. Opens the way to the Astral Age.",
            150f, Cost(food, 3000, gold, 900, coin, 1200, mana, 300, chips, 300), "Leylines");
        navigation.requirements.minimumAge = arcane;
        navigation.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(navigation);

        Technology STech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var meteors = STech("StarmetalWorking", "Starmetal", "Metal that fell from the sky, too hard for any forge but one fed with mana. Unlocks the Meteor Mine and the Core Forge.",
            130f, Cost(steel, 500, mana, 150, chips, 100), "AstralNavigation");
        var starblades = STech("Starblades", "Starblades", "A hilt that holds a blade of starlight. Spellblade Halls rank up into the Starknight Hall; all troops hit 4% harder.",
            130f, Cost(steel, 500, mana, 80, starmetal, 60), meteors.id, meteors);
        starblades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var technomancy = STech("Technomancy", "Technomancy", "Spells written as programs and run on staffs of steel. Arcane Academies rank up into the Technomancy College.",
            130f, Cost(gold, 500, mana, 100, chips, 120), starblades.id, starblades);
        var skylances = STech("Skylances", "Skylances", "Mana jetbikes, fast and nimble. Griffin Roosts rank up into the Skylancer Hangar; troops move 4% faster.",
            130f, Cost(steel, 600, mana, 100, cells, 80), technomancy.id, meteors);
        skylances.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var mending = STech("StarMending", "Star Mending", "Healing light, focused through a lens of star-steel. Temples of Light rank up into the Astral Sanctum; all troops have 4% more health.",
            120f, Cost(food, 2400, gold, 400, mana, 100), skylances.id);
        mending.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var lances = STech("AstralLances", "Astral Lances", "A beam of starlight that never misses. Arcane Obelisks rank up into Astral Lances, runeward into starward walls; buildings have 8% more health.",
            135f, Cost(stone, 2400, starmetal, 120, mana, 120), mending.id, technomancy);
        lances.buildingHealthMultiplier = 1.08f;
        var meteorCalling = STech("MeteorCalling", "Meteor Calling", "Not one falling star but a rain of them, aimed by a dish of star-steel. Starfall Spires rank up into Meteor Arrays.",
            135f, Cost(gold, 600, mana, 160, starmetal, 100), lances.id, meteors);
        var novas = STech("Novas", "Novas", "A spark of a sun, thrown. Fireball Spires rank up into Nova Batteries.",
            135f, Cost(mana, 160, starmetal, 100, cells, 100), meteorCalling.id, meteors);
        var forging = STech("StarForging", "Star Forging", "Steel folded with starmetal. Runeforges rank up into the Star Foundry; steel comes 10% faster.",
            130f, Cost(steel, 600, starmetal, 100, mana, 80), novas.id, mending);
        forging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var constructs = STech("Constructs", "Constructs", "Workers of star-steel with a mana core and a program for a soul. Animated Workshops rank up into Construct Works; everything you produce comes in 5% faster.",
            135f, Cost(steel, 600, gears, 400, cores, 60), forging.id, forging);
        constructs.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var runemechs = STech("NovaThrones", "Nova Thrones", "A technomancer's throne on a hovering disc, a cannon at its side that throws captured suns. Drake Pens rank up into the Throne Works.",
            145f, Cost(steel, 800, cores, 80, cells, 150), constructs.id, technomancy);
        var astralConstructs = STech("AstralConstructs", "Astral Constructs", "Golem, android and knight in one: a construct of star-steel, woken with a rune and a program. Golem Sanctums rank up into the Construct Vault.",
            150f, Cost(starmetal, 300, cores, 120, chips, 150), runemechs.id, constructs);
        var starships = STech("Starships", "Starships", "A hull of star-steel, a mana drive and a crystal to steer by. Skyship Docks rank up into the Starport.",
            145f, Cost(steel, 900, cores, 100, mana, 150), astralConstructs.id, skylances);
        var arcologies = STech("Arcologies", "Arcologies", "Towers that hold a town, with rings of gardens floating round them. Spires of Lights rank up into Star Arcologies.",
            120f, Cost(stone, 2800, starmetal, 100, mana, 80), starships.id);
        var exchange = STech("StarTrade", "Star Trade", "Coin minted from starmetal, traded on prices read in the stars. Arcane Treasuries rank up into the Star Exchange; everyone pays a little more in taxes.",
            120f, Cost(gold, 1000, coin, 900, starmetal, 60), arcologies.id);
        exchange.coinPerPersonPerSecond = 0.002f;
        var farsight = STech("Farsight", "Farsight", "Telescopes with a crystal for a lens: they see the seven worlds, and what moves on them. Research goes 5% faster.",
            130f, Cost(gold, 800, cores, 60, chips, 120), exchange.id);
        farsight.researchSpeedMultiplier = 1.05f;
        var reactors = STech("AstralReactors", "Astral Reactors", "A leyline wound into a ring of star-steel and run until it sings. Leyline Nexuses rank up into Astral Reactors.",
            145f, Cost(starmetal, 200, cores, 80, mana, 200), farsight.id, meteors);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { navigation },
            cost = Cost(food, 11000, wood, 7000, stone, 9000, iron, 2600, gold, 1400, coin, 1800, steel, 1400, mana, 400, runestone, 200),
            population = 1000,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var starknight = MedievalUnit("Starknight", "Starknight", age, "Spellblade", 950, 135, 0.9f, 0.9f, 1.15f);
        var technomancer = MedievalUnit("Technomancer", "Technomancer", age, "Arcanist", 540, 115, 1.6f, 8.0f, 1.0f);
        var skylancer = MedievalUnit("Skylancer", "Skylancer", age, "GriffinRider", 1050, 95, 1.1f, 6.5f, 2.9f);
        skylancer.movement = Movement.Air; skylancer.flyHeight = 0.9f; skylancer.populationCost = 2;
        var starmender = MedievalUnit("Starmender", "Starmender", age, "Lightbringer", 520, 70, 1.0f, 0.8f, 1.1f);
        starmender.healer = true; starmender.healRange = 16f;
        var runemech = MedievalUnit("NovaThrone", "Nova Throne", age, "WarDrake", 2600, 270, 3.2f, 10.5f, 0.9f);
        runemech.populationCost = 3; runemech.movement = Movement.Ground; runemech.flyHeight = 0f;
        var construct = MedievalUnit("AstralConstruct", "Astral Construct", age, "RunicGolem", 2500, 135, 0.95f, 1.2f, 1.05f);
        construct.populationCost = 2;
        var frigate = MedievalUnit("StarFrigate", "Star Frigate", age, "Skyship", 2200, 175, 2.4f, 6.5f, 1.4f);
        frigate.populationCost = 3; frigate.movement = Movement.Air; frigate.flyHeight = 1.8f;
        foreach (var u in new[] { starknight, technomancer, skylancer, starmender })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.014f), Rate(coin, 0.09f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { runemech, construct, frigate })   // machines run on astral cores
        {
            u.upkeep = new List<ResourceRate> { Rate(cores, 0.012f), Rate(coin, 0.09f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { starknight, technomancer, skylancer, starmender, runemech, construct, frigate };
        var reaver = MedievalUnit("Voidreaver", "Voidreaver", age, "Hexblade", 1080, 108, 1.0f, 0.9f, 1.2f);
        var caster = MedievalUnit("VoidCaster", "Void Caster", age, "Warlock", 560, 100, 1.7f, 7.5f, 1.0f);
        var skiff = MedievalUnit("VoidSkiff", "Void Skiff", age, "WyvernRider", 900, 80, 1.3f, 6.0f, 2.7f);
        skiff.movement = Movement.Air; skiff.flyHeight = 0.9f;
        foreach (var u in new[] { reaver, caster, skiff }) EditorUtility.SetDirty(u);
        age.enemyUnits = new List<UnitDefinition> { reaver, caster, skiff };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var arcology = RankUp(db, "SpireOfLights", "StarArcology", "Star Arcology", age, arcologies,
            "A tower of star-steel and glass that holds a whole town, rings of gardens floating round it. Houses 180 people.",
            Cost(stone, 800, starmetal, 60, steel, 160), Cost(stone, 400, starmetal, 30), 1650);
        arcology.housing = 180;
        arcology.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Garden Ring", description = "+25 people.", requirements = new Requirements { cost = Cost(stone, 500, mana, 40) }, upgradeTime = 25f, extraHousing = 25 },
            new BuildingUpgrade { name = "Sky Decks", description = "+30 people.", requirements = new Requirements { cost = Cost(starmetal, 60, gold, 150) }, upgradeTime = 30f, extraHousing = 30 },
        };
        var bourse = RankUp(db, "ArcaneTreasury", "StarExchange", "Star Exchange", age, exchange,
            "A hall under a dome of glass, prices glowing on a star-map overhead. Coin from starmetal.",
            Cost(stone, 700, gold, 350, starmetal, 40), Cost(stone, 350, gold, 150), 1350);
        bourse.workers = 3;
        bourse.production = new List<ResourceRate> { Rate(coin, 4.8f) };
        bourse.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        bourse.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Star Ledger", "+25% coin.", Cost(cores, 40, coin, 500), 1.25f),
            Upgrade("Seven-World Trade", "+25% coin.", Cost(starmetal, 80, gold, 250), 1.25f),
        };
        var knightHall = RankUp(db, "SpellbladeHall", "StarknightHall", "Starknight Hall", age, starblades,
            "A hall of star-steel, its door flanked by blades of light. Trains Starknights.",
            Cost(stone, 700, steel, 250, starmetal, 50), Cost(stone, 350, starmetal, 25), 1700);
        Trains(knightHall, starknight, 4, steel, gold, food, coin);
        var college = RankUp(db, "ArcaneAcademy", "TechnomancyCollege", "Technomancy College", age, technomancy,
            "A tower of glass and star-steel, its spells running as light down the walls. Trains Technomancers.",
            Cost(stone, 600, gold, 250, cores, 30), Cost(stone, 300, mana, 40), 1300);
        Trains(college, technomancer, 4, steel, gold, food, coin);
        var hangar = RankUp(db, "GriffinRoost", "SkylancerHangar", "Skylancer Hangar", age, skylances,
            "A hangar with a launch rail on its roof. Builds Skylancers: mana jetbikes that strafe from the air.",
            Cost(steel, 600, starmetal, 60, cells, 60), Cost(steel, 300, starmetal, 30), 1300);
        Trains(hangar, skylancer, 3, steel, gold, food, coin);
        var sanctum = RankUp(db, "TempleOfLight", "AstralSanctum", "Astral Sanctum", age, mending,
            "A white dome with a great lens at its crown, focusing starlight. Trains Starmenders.",
            Cost(stone, 700, gold, 200, starmetal, 40), Cost(stone, 350, mana, 30), 1300);
        sanctum.trainsUnit = starmender;
        sanctum.maxUnits = 3;
        sanctum.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Lens", description = "+1 starmender.", requirements = new Requirements { cost = Cost(starmetal, 40, gold, 120) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Focused Light", description = "Starmenders heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(cores, 50, gold, 150) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var starBolt = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/StarBolt.png");
        var meteor = PixelProjectile("Meteor", 16, 11, c =>
        {
            var f = PixelPalette.Fire;
            c.LineRaw(new Vector2(0, 2), new Vector2(8, 5), f[1]); c.LineRaw(new Vector2(1, 4), new Vector2(9, 6), f[2]);
            c.Ellipse(new Vector2(12, 6), 3.4f, 3.4f, PixelPalette.Iron);
            c.Put(13, 7, f[3]); c.Put(11, 5, f[2]);
        });
        var nova = PixelProjectile("Nova", 12, 12, c =>
        {
            var g = PixelPalette.Gold;
            c.Ellipse(new Vector2(6, 6), 5f, 5f, new[] { PixelPalette.Arcane[2], g[2], g[3], PixelPalette.White });
            c.Put(6, 11, g[3]); c.Put(6, 0, g[3]); c.Put(0, 6, g[3]); c.Put(11, 6, g[3]);
        });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, bool smoke, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = smoke;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var lance = RankUp(db, "ArcaneObelisk", "AstralLance", "Astral Lance", age, lances,
            "A needle of star-steel, a lens at its tip that fires bolts of starlight.",
            Cost(stone, 500, starmetal, 70, cores, 20), Cost(stone, 250, starmetal, 35), 2300);
        Crewless(lance, 11.5f, 58, 0.42f, starBolt, 0f, 30f, 0.9f, false, 32f, 54f);
        lance.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Long Lens", description = "+1.5 range.", requirements = new Requirements { cost = Cost(starmetal, 60, stone, 250) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Brighter Star", description = "+18 damage.", requirements = new Requirements { cost = Cost(cores, 40, mana, 60) }, upgradeTime = 25f, extraTowerDamage = 18 },
        };
        var array = RankUp(db, "StarfallSpire", "MeteorArray", "Meteor Array", age, meteorCalling,
            "A great dish of star-steel turned to the sky. It calls meteors down onto raiders from very far away.",
            Cost(stone, 600, starmetal, 120, gold, 150), Cost(stone, 300, starmetal, 60), 1600);
        Crewless(array, 22f, 430, 3.0f, meteor, 0f, 36f, 1.6f, false, 32f, 54f);
        array.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Wider Dish", description = "+2 range.", requirements = new Requirements { cost = Cost(starmetal, 100, gold, 120) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Iron Stars", description = "+130 damage.", requirements = new Requirements { cost = Cost(cores, 60, mana, 120) }, upgradeTime = 30f, extraTowerDamage = 130 },
        };
        var battery = RankUp(db, "FireballSpire", "NovaBattery", "Nova Battery", age, novas,
            "A squat star-steel tower with a sun caught in a cage on top. Each shot is a nova that bursts wide.",
            Cost(stone, 700, starmetal, 100, cells, 120), Cost(stone, 350, starmetal, 50), 2700);
        Crewless(battery, 15f, 400, 3.2f, nova, 0.2f, 16f, 2.8f, false, 32f, 40f);
        battery.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(starmetal, 100, cells, 100) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Hotter Suns", description = "+130 damage.", requirements = new Requirements { cost = Cost(cores, 60, mana, 120) }, upgradeTime = 30f, extraTowerDamage = 130 },
        };
        var starWall = RankUp(db, "RunewardWall", "StarwardWall", "Starward Wall", age, lances,
            "Panels of dark star-steel, seamed with gold, starlight glittering in them.", Cost(stone, 14, starmetal, 2), Cost(stone, 12, starmetal, 2), 5400);
        var starGate = RankUp(db, "RunewardGate", "StarwardGate", "Starward Gate", age, lances,
            "A star-steel gate that slides aside for friends.", Cost(stone, 28, starmetal, 6), Cost(stone, 24, starmetal, 4), 7200);
        starWall.upgrades = new List<BuildingUpgrade>();
        starGate.upgrades = new List<BuildingUpgrade>();

        var foundry = RankUp(db, "Runeforge", "StarFoundry", "Star Foundry", age, forging,
            "A foundry of star-steel where starmetal is folded into the steel. Steel without end.",
            Cost(stone, 600, steel, 350, starmetal, 60), Cost(stone, 300, starmetal, 30), 1550);
        foundry.workers = 5;
        foundry.production = new List<ResourceRate> { Rate(steel, 0.85f) };
        foundry.inputs = new List<ResourceRate> { Rate(iron, 0.4f), Rate(starmetal, 0.015f) };
        foundry.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Star Crucibles", "+30% steel.", Cost(starmetal, 80, mana, 40), 1.3f),
            Upgrade("Folding Presses", "+30% steel.", Cost(steel, 350, cores, 30), 1.3f),
        };
        var works = RankUp(db, "AnimatedWorkshop", "ConstructWorks", "Construct Works", age, constructs,
            "A glass-roofed works where constructs of star-steel build gears day and night.",
            Cost(stone, 500, gears, 200, cores, 30), Cost(stone, 250, cores, 15), 1350);
        works.workers = 4;
        works.production = new List<ResourceRate> { Rate(gears, 0.5f) };
        works.inputs = new List<ResourceRate> { Rate(steel, 0.16f), Rate(cores, 0.008f) };
        works.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Constructs", "+30% gears.", Cost(cores, 50, gears, 120), 1.3f),
            Upgrade("Sleepless Shifts", "+30% gears.", Cost(steel, 250, mana, 60), 1.3f),
        };
        var mechForge = RankUp(db, "DrakePens", "ThroneWorks", "Throne Works", age, runemechs,
            "A star-steel hall with a launch ring before it, a throne hovering in the ring. Builds Nova Thrones: hovering siege thrones that fire novas.",
            Cost(stone, 700, steel, 500, cores, 80), Cost(stone, 350, cores, 40), 2100);
        mechForge.trainsUnit = runemech;
        mechForge.maxUnits = 2;
        mechForge.workers = 6;
        mechForge.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Ring", description = "+1 nova throne.", requirements = new Requirements { cost = Cost(steel, 400, cores, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Ward Shields", description = "Nova thrones are 25% tougher.", requirements = new Requirements { cost = Cost(starmetal, 120, mana, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var vault = RankUp(db, "GolemSanctum", "ConstructVault", "Construct Vault", age, astralConstructs,
            "A sealed vault of star-steel; astral constructs stand waiting in its glowing alcoves.",
            Cost(stone, 1200, starmetal, 200, cores, 80), Cost(stone, 600, starmetal, 100), 1600);
        vault.trainsUnit = construct;
        vault.maxUnits = 4;
        vault.workers = 4;
        vault.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Alcove", description = "+1 construct.", requirements = new Requirements { cost = Cost(starmetal, 150, cores, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Better Programs", description = "Constructs hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(cores, 100, chips, 120) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var starport = RankUp(db, "SkyshipDock", "Starport", "Starport", age, starships,
            "A landing pad with a control tower and a docking cradle. Builds Star Frigates: mana-drive gunships.",
            Cost(steel, 800, starmetal, 120, cores, 60), Cost(steel, 400, cores, 30), 1500);
        starport.trainsUnit = frigate;
        starport.maxUnits = 2;
        starport.workers = 4;
        starport.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Pad", description = "+1 frigate.", requirements = new Requirements { cost = Cost(steel, 600, cores, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Ward Shields", description = "Frigates are 25% tougher.", requirements = new Requirements { cost = Cost(cores, 80, mana, 100) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var reactor = RankUp(db, "LeylineNexus", "AstralReactor", "Astral Reactor", age, reactors,
            "A ring of star-steel round a captured leyline, singing as it spins. Power cells without end.",
            Cost(stone, 1200, starmetal, 150, steel, 400), Cost(stone, 600, starmetal, 80), 1800);
        reactor.workers = 5;
        reactor.production = new List<ResourceRate> { Rate(cells, 0.62f) };
        reactor.inputs = new List<ResourceRate> { Rate(mana, 0.03f) };
        reactor.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Faster Ring", "+30% power cells.", Cost(starmetal, 100, mana, 80), 1.3f),
            Upgrade("Second Leyline", "+30% power cells.", Cost(cores, 60, runestone, 60), 1.3f),
        };
        var mine = NewBuilding(db, "ManastoneMine", "MeteorMine", "Meteor Mine", age, meteors, new Vector2Int(3, 3),
            "A crane over a meteor crater, cutting starmetal from the fallen star. Must be built on or next to Starmetal, or right on a rich starmetal vein.",
            Cost(steel, 300, stone, 450, mana, 40), 800);
        mine.requiredDeposit = starDeposit;
        mine.canBuildOnHills = false;
        mine.workers = 5;
        mine.production = new List<ResourceRate> { Rate(starmetal, 0.13f) };
        mine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Mana Saws", "+30% starmetal.", Cost(steel, 250, mana, 50), 1.3f),
            Upgrade("Crater Survey", "+30% starmetal.", Cost(chips, 80, gold, 150), 1.3f),
        };
        var coreForge = NewBuilding(db, "ManaWell", "CoreForge", "Core Forge", age, meteors, new Vector2Int(3, 3),
            "A forge where starmetal is shaped round a drop of mana into an Astral Core: the heart of every Astral machine.",
            Cost(stone, 700, steel, 250, gold, 150), 1100);
        coreForge.workers = 5;
        coreForge.production = new List<ResourceRate> { Rate(cores, 0.08f) };
        coreForge.inputs = new List<ResourceRate> { Rate(starmetal, 0.1f), Rate(mana, 0.04f) };
        coreForge.runsWithoutInputs = 0f;
        coreForge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Finer Moulds", "+30% cores.", Cost(stone, 500, gold, 150), 1.3f),
            Upgrade("Mana Quench", "+30% cores.", Cost(mana, 80, chips, 60), 1.3f),
        };

        var all = new[] { arcology, bourse, knightHall, college, hangar, sanctum, lance, array, battery, starWall, starGate, foundry, works, mechForge, vault, starport, reactor, mine, coreForge };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds) ----------
        starDeposit.sprites = PixelDeposits("Starmetal", 3, Ore.Starmetal);
        EditorUtility.SetDirty(starDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(12);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Starknight", "Technomancer", "Skylancer", "Starmender", "NovaThrone", "AstralConstruct", "StarFrigate", "Voidreaver", "VoidCaster", "VoidSkiff" });
        lance.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/StarBolt.png");
        EditorUtility.SetDirty(lance);
        SetupBosses("AstralAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Astral Age set up.");
    }
}
