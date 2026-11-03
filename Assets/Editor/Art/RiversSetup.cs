using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rivers and the sea:
/// - Fords and bridges (Stone to Electric Age: ford, wooden, stone, iron, steel, force bridge), dragged
///   out in straight lines over water like walls; each ranks up into the next.
/// - Fishing docks (fishing dock, fishery, cannery, aquafarm): food from the water round them.
/// - Harbours (raft landing, galley dock, longship yard, shipyard, steam dockyard, hover dock): train
///   warships that patrol the water and shoot, and run a ferry for troops sent across the water.
/// - The raiders' boats (canoes, longboats, steam launches, hover-skiffs) for raids from the sea.
/// - Research with small global bonuses (cheaper, longer bridges; faster, tougher boats; more fish).
/// Creates or updates only its own assets and art, and is safe to run again.
/// (Tools > Age of Stickman > Setup Rivers.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Rivers")]
    public static void SetupRivers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Rivers: stop playing first.");
            return;
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age stone = AgeNamed("StoneAge"), bronze = AgeNamed("BronzeAge"), classical = AgeNamed("ClassicalAge"), medieval = AgeNamed("MedievalAge"),
            renaissance = AgeNamed("RenaissanceAge"), enlightenment = AgeNamed("EnlightenmentAge"), industrial = AgeNamed("IndustrialAge"),
            modern = AgeNamed("ModernAge"), electric = AgeNamed("ElectricAge"), information = AgeNamed("InformationAge"), arcane = AgeNamed("ArcaneAge");
        if (stone == null || electric == null)
        {
            Debug.LogError("Setup Rivers: the ages aren't set up yet.");
            return;
        }
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stoneR = Res(db, "Stone"), bronzeR = Res(db, "Bronze"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), steel = Res(db, "Steel"), coal = Res(db, "Coal"), coin = Res(db, "Coin"), power = Res(db, "PowerCells"),
            chips = Res(db, "Microchips"), mana = Res(db, "Mana");
        List<ResourceAmount> C(params object[] pairs)
        {
            // Like Cost(), but skips resources this game doesn't have.
            var list = new List<ResourceAmount>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            return list;
        }

        // ---------- The naval troop class ----------
        Directory.CreateDirectory("Assets/Data/TroopClasses");
        const string classPath = "Assets/Data/TroopClasses/RiverNaval.asset";
        var naval = AssetDatabase.LoadAssetAtPath<TroopClass>(classPath);
        if (naval == null)
        {
            naval = ScriptableObject.CreateInstance<TroopClass>();
            AssetDatabase.CreateAsset(naval, classPath);
        }
        naval.displayName = "Naval";
        naval.description = "Boats and warships: they keep to the water, and shoot at what comes near the shore.";
        EditorUtility.SetDirty(naval);

        // ---------- Research ----------
        Technology T(string id, string name, Age age, string description, float time, List<ResourceAmount> cost, params Technology[] needs)
        {
            var t = Tech(db, id, name, age, description, time, cost, null);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>();
            foreach (var n in needs) if (n != null) t.requirements.technologies.Add(n);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            t.navigationRange = 0f;
            t.driveSpeed = 0f;
            t.maxLevel = 1;
            EditorUtility.SetDirty(t);
            return t;
        }
        UnitStatModifier Naval(float health = 1f, float attack = 1f, float speed = 1f, float range = 1f) =>
            new UnitStatModifier { troopClass = naval, healthMultiplier = health, attackMultiplier = attack, speedMultiplier = speed, rangeMultiplier = range };
        void Food(Technology t, float m) { if (food != null) t.productionModifiers.Add(new ProductionModifier { resource = food, multiplier = m }); }

        var reedBoats = T("RiverReedBoats", "Reed Boats", stone,
            "Bundles of reeds lashed into boats, and stones laid across the shallows. Unlocks the Fishing Dock, the Raft Landing and Fords. +3% food.",
            40f, C(food, 60, wood, 60));
        Food(reedBoats, 1.03f);
        var timber = T("RiverTimberBridges", "Timber Bridges", bronze,
            "Piles driven into the riverbed and planks across them. Unlocks the Wooden Bridge. Boats are 4% tougher.",
            60f, C(wood, 200, bronzeR, 30), reedBoats);
        timber.unitModifiers.Add(Naval(health: 1.04f));
        var galleys = T("RiverGalleys", "Oared Galleys", classical,
            "Long hulls driven by banks of oars, with a bronze ram at the bow. Unlocks the Galley Dock. Boats are 5% tougher.",
            90f, C(wood, 400, bronzeR, 80, gold, 30), timber);
        galleys.unitModifiers.Add(Naval(health: 1.05f));
        var bridgeworks = T(RiverCatalog.TechBridgeworks, "Arch Bridges", classical,
            "Stone arches that carry their own weight. Unlocks the Stone Bridge. Bridges cost 15% less and reach a tile further out.",
            90f, C(stoneR, 400, wood, 150), timber);
        var longships = T("RiverLongships", "Clinker Hulls", medieval,
            "Overlapping planks make a light, fast hull. Unlocks the Longship Yard and the Fishery. Boats are 5% faster.",
            110f, C(wood, 600, iron, 120), galleys);
        longships.unitModifiers.Add(Naval(speed: 1.05f));
        var charts = T(RiverCatalog.TechCharts, "Charts and Compass", renaissance,
            "Sea charts, a compass and the stars. Unlocks the Shipyard. Boats reach 5% further, and ferries sail 10% faster.",
            130f, C(gold, 200, wood, 500, iron, 150), longships);
        charts.unitModifiers.Add(Naval(range: 1.05f));
        var ferryCrews = T(RiverCatalog.TechFerryCrews, "Ferry Crews", enlightenment,
            "Trained crews and proper landing stages. Ferries carry 2 more troops and sail 20% faster. +3% food.",
            130f, C(coin, 300, wood, 500), charts);
        Food(ferryCrews, 1.03f);
        var steamHulls = T("RiverIronclads", "Steam Hulls", industrial,
            "Iron plate over oak, and a screw driven by steam. Unlocks the Steam Dockyard and the Cannery. Boats are 8% faster.",
            150f, C(steel, 300, coal, 200, wood, 300), charts);
        steamHulls.unitModifiers.Add(Naval(speed: 1.08f));
        var girders = T(RiverCatalog.TechIronGirders, "Iron Girders", industrial,
            "Riveted iron trusses span what stone can't. Unlocks the Iron Bridge. Bridges cost 10% less and reach a tile further out.",
            140f, C(steel, 250, stoneR, 300), bridgeworks);
        var suspension = T(RiverCatalog.TechSuspension, "Suspension Bridges", modern,
            "Steel cables slung from tall towers. Unlocks the Steel Bridge. Bridges reach 2 tiles further out.",
            160f, C(steel, 400, stoneR, 400), girders);
        var hoverHulls = T("RiverHoverHulls", "Hover Hulls", electric,
            "A hull riding a cushion of galvanic air over the waves. Unlocks the Hover Dock. Boats are 6% faster and 5% tougher.",
            170f, C(steel, 400, power, 150), steamHulls);
        hoverHulls.unitModifiers.Add(Naval(health: 1.05f, speed: 1.06f));
        var forceBridges = T("RiverForceBridges", "Force Bridges", electric,
            "A walkway of humming force, strung between two pylons. Unlocks the Force Bridge.",
            170f, C(steel, 300, power, 200), suspension);
        var aquaculture = T("RiverAquaculture", "Aquaculture", information,
            "Fish raised in pens, fed and watched by machines. Unlocks the Aquafarm. +4% food.",
            180f, C(steel, 300, chips, 120), steamHulls);
        Food(aquaculture, 1.04f);
        Technology tideWards = null;
        if (arcane != null)
        {
            tideWards = T("RiverTideWards", "Tide Wards", arcane,
                "Runes cut into every hull, so the sea itself turns blows aside. Boats are 6% tougher and hit 5% harder.",
                190f, C(mana, 200, steel, 300), hoverHulls);
            tideWards.unitModifiers.Add(Naval(health: 1.06f, attack: 1.05f));
        }

        // ---------- Boats ----------
        var arrow = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Arrow.png");
        var slingStone = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/SlingStone.png");
        var cannonball = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Cannonball.png");
        var spark = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/GalvanicSpark.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/RayBolt.png");
        UnitDefinition Boat(string id, string display, Age age, int hp, int atk, float cd, float range, float speed, Sprite shot,
            float shotSpeed, float arc, float splash, bool smoke, int pop, List<ResourceRate> upkeep)
        {
            string path = $"Assets/Data/Units/{id}.asset";
            var u = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (u == null)
            {
                u = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(u, path);
            }
            u.name = id;
            u.displayName = display;
            u.age = age;
            u.troopClass = naval;
            u.cost = 0;
            u.populationCost = pop;
            u.upkeep = upkeep ?? new List<ResourceRate>();
            u.maxHealth = hp;
            u.attackPower = atk;
            u.attackCooldown = cd;
            u.attackRange = range;
            u.hitDelay = 0.35f;
            u.detectionRange = 7f;
            u.moveSpeed = speed;
            u.projectile = shot;
            u.projectileSpeed = shotSpeed;
            u.projectileArc = arc;
            u.splashRadius = splash;
            u.splashFire = false;
            u.muzzleSmoke = smoke;
            u.movement = Movement.Water;
            u.flyHeight = 0f;
            u.healer = false;
            u.isNative = false;
            u.isBoss = false;
            u.summons = null;
            EditorUtility.SetDirty(u);
            return u;
        }
        var raft = Boat("RiverReedRaft", "Reed Raft", stone, 170, 9, 1.6f, 6f, 1.1f, slingStone, 10f, 0.2f, 0f, false, 1,
            new List<ResourceRate> { Rate(food, 0.02f) });
        var galley = Boat("RiverWarGalley", "War Galley", classical, 480, 24, 1.6f, 7f, 1.3f, arrow, 11f, Projectile.BowArc, 0f, false, 2,
            new List<ResourceRate> { Rate(food, 0.03f), Rate(coin, 0.02f) });
        var longship = Boat("RiverLongship", "Longship", medieval, 700, 32, 1.5f, 7.5f, 1.6f, arrow, 12f, Projectile.BowArc, 0f, false, 2,
            new List<ResourceRate> { Rate(food, 0.03f), Rate(coin, 0.03f) });
        var galleon = Boat("RiverGalleon", "Galleon", renaissance, 1500, 75, 3.0f, 10f, 1.3f, cannonball, 16f, 0.06f, 1.4f, true, 3,
            new List<ResourceRate> { Rate(food, 0.04f), Rate(coin, 0.06f) });
        var ironclad = Boat("RiverIronclad", "Ironclad", industrial, 2400, 120, 3.0f, 11f, 1.5f, cannonball, 18f, 0.05f, 1.6f, true, 3,
            new List<ResourceRate> { Rate(coal, 0.02f), Rate(coin, 0.08f) });
        var hoverBarge = Boat("RiverHoverBarge", "Hover-Barge", electric, 3200, 160, 2.4f, 12f, 2.0f, spark, 24f, 0f, 1.2f, false, 3,
            new List<ResourceRate> { Rate(power, 0.01f), Rate(coin, 0.08f) });
        // The raiders' boats only carry a raid in: tough enough to be worth sinking, no guns.
        UnitDefinition RaidBoat(string id, string display, Age age, int hp) =>
            Boat(id, display, age, hp, 1, 2f, 0.5f, 3.2f, null, 10f, 0f, 0f, false, 1, null);
        var canoe = RaidBoat("RiverRaiderCanoe", "Raiders' Canoe", stone, 260);
        var longboat = RaidBoat("RiverRaiderLongboat", "Raiders' Longboat", classical, 900);
        var launch = RaidBoat("RiverRaiderLaunch", "Raiders' Steam Launch", industrial, 2200);
        var skiff = RaidBoat("RiverRaiderSkiff", "Raiders' Hover-Skiff", information ?? electric, 4200);
        foreach (var (age, unit) in new[] { (stone, raft), (classical, galley), (medieval, longship), (renaissance, galleon), (industrial, ironclad), (electric, hoverBarge) })
        {
            if (age == null) continue;
            if (!age.playerUnits.Contains(unit)) age.playerUnits.Add(unit);
            EditorUtility.SetDirty(age);
        }

        // ---------- Bridges and fords ----------
        BuildingDefinition Bridge(string id, string display, Age age, Technology needs, string desc, List<ResourceAmount> cost, int hp, float buildTime, int tier)
        {
            var b = Building(db, id, display, age, null, Vector2Int.one, Vector2Int.one, desc, cost, buildTime, hp, 0, needs);
            b.wallKind = WallTile.Bridge;
            b.canBuildOnHills = false;
            b.defensePower = 0f;
            b.sortLine = 0.97f;   // figures walk over it
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.upgrades = new List<BuildingUpgrade>();
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.catchmentRadius = 0;
            b.waterBonus = 0f;
            b.maxCount = 0;
            b.looks = new List<BuildingLook>();
            b.rankUpTo = null;
            b.pieces = RiverBridgePieces(id, tier);
            b.icon = b.pieces[RiverWorks.PieceEastWest];
            EditorUtility.SetDirty(b);
            return b;
        }
        const string bridgeHow = " Drag out a straight line over the water; everyone can walk over it, raiders too, and they wear it down as they cross. Boats can't pass it.";
        var ford = Bridge("RiverFord", "Ford", stone, reedBoats,
            "Stepping stones on a gravel bar across the shallows. Reaches 1 tile out from each bank: enough for a narrow river." + bridgeHow,
            C(stoneR, 8), 220, 3f, 0);
        var woodBridge = Bridge("RiverBridgeWood", "Wooden Bridge", bronze, timber,
            "Planks on piles driven into the riverbed, with a rope rail. Reaches 3 tiles out from the bank." + bridgeHow,
            C(wood, 14), 320, 4f, 1);
        var stoneBridge = Bridge("RiverBridgeStone", "Stone Bridge", classical, bridgeworks,
            "Cobbled arches between stout piers. Reaches 4 tiles out from the bank, and stands up to a lot." + bridgeHow,
            C(stoneR, 18, wood, 4), 750, 5f, 2);
        var ironBridge = Bridge("RiverBridgeIron", "Iron Bridge", industrial, girders,
            "Riveted iron plate on lattice girders. Reaches 6 tiles out from the bank." + bridgeHow,
            C(steel, 6, stoneR, 10), 1500, 5f, 3);
        var steelBridge = Bridge("RiverBridgeSteel", "Steel Bridge", modern, suspension,
            "A road deck with crash rails and street lamps. Reaches 8 tiles out from the bank." + bridgeHow,
            C(steel, 10, stoneR, 12), 2300, 6f, 4);
        var forceBridge = Bridge("RiverBridgeEnergy", "Force Bridge", electric, forceBridges,
            "A glowing walkway of hard force between humming pylons. Reaches 11 tiles out from the bank." + bridgeHow,
            C(steel, 6, power, 3), 3200, 6f, 5);
        RiverRankUp(ford, woodBridge, timber);
        RiverRankUp(woodBridge, stoneBridge, bridgeworks);
        RiverRankUp(stoneBridge, ironBridge, girders);
        RiverRankUp(ironBridge, steelBridge, suspension);
        RiverRankUp(steelBridge, forceBridge, forceBridges);

        // ---------- Fishing ----------
        BuildingDefinition Fishing(string id, string display, Age age, Technology needs, string desc, List<ResourceAmount> cost, int hp, int workers,
            float perSecond, int radius, List<BuildingUpgrade> upgrades)
        {
            var b = Building(db, id, display, age, null, new Vector2Int(3, 2), new Vector2Int(3, 3), desc, cost, 20f, hp, workers, needs);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = new List<ResourceRate> { Rate(food, perSecond) };
            b.inputs = new List<ResourceRate>();
            b.catchmentRadius = radius;
            b.waterBonus = 0.1f;   // shows the catchment while placing; counted in the catch
            b.minDepositTiles = 0;
            b.fullOutputTiles = 10;
            b.upgrades = upgrades;
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.defensePower = 0f;
            b.sortLine = 0f;
            b.rankUpTo = null;
            EditorUtility.SetDirty(b);
            return b;
        }
        BuildingUpgrade Up(string name, string desc, List<ResourceAmount> cost, float output = 1f, int catchment = 0, int units = 0,
            UnitStatModifier trained = null, int health = 0) => new BuildingUpgrade
        {
            name = name, description = desc, requirements = new Requirements { cost = cost }, upgradeTime = 30f,
            productionMultiplier = output, extraCatchment = catchment, extraUnits = units, extraHealth = health,
            trainedUnits = trained ?? new UnitStatModifier(),
        };
        const string fishHow = " Build it on the bank: the more water round it (shoals count double, and open sea a little more), the bigger the catch.";
        var dock = Fishing("RiverFishingDock", "Fishing Dock", stone, reedBoats,
            "A reed hut on stilts, nets drying on poles and a dugout pulled up on the bank." + fishHow,
            C(wood, 50), 120, 2, 0.7f, 3, new List<BuildingUpgrade>
            {
                Up("Bigger Nets", "+25% catch.", C(wood, 60, food, 30), 1.25f),
                Up("Drying Racks", "+20% catch, fishes 1 tile further out.", C(wood, 80, stoneR, 30), 1.2f, 1),
            });
        var fishery = Fishing("RiverFishery", "Fishery", medieval, longships,
            "A timber smokehouse, barrels of salt herring and a sailing smack at the jetty." + fishHow,
            C(wood, 160, stoneR, 80, iron, 20), 400, 3, 1.6f, 4, new List<BuildingUpgrade>
            {
                Up("Smokehouse", "+25% catch.", C(wood, 150, stoneR, 60), 1.25f),
                Up("Herring Fleet", "+20% catch, fishes 1 tile further out.", C(wood, 200, iron, 40), 1.2f, 1),
            });
        var cannery = Fishing("RiverCannery", "Cannery", industrial, steamHulls,
            "A brick cannery on a stone quay, its crane swinging crates of tins onto a steam trawler." + fishHow,
            C(stoneR, 300, steel, 60, wood, 150), 900, 4, 3.2f, 4, new List<BuildingUpgrade>
            {
                Up("Steam Trawlers", "+25% catch, fishes 1 tile further out.", C(steel, 100, coal, 60), 1.25f, 1),
                Up("Tin Cans", "+20% catch.", C(steel, 120), 1.2f),
            });
        var aquafarm = Fishing("RiverAquafarm", "Aquafarm", information, aquaculture,
            "Glowing fish pens out on the water, watched over by a white research hall." + fishHow,
            C(steel, 200, chips, 30, stoneR, 300), 1500, 4, 5.5f, 5, new List<BuildingUpgrade>
            {
                Up("Kelp Pens", "+25% catch.", C(steel, 150, chips, 30), 1.25f),
                Up("Smart Feeders", "+20% catch, fishes 1 tile further out.", C(chips, 60, power, 40), 1.2f, 1),
            });
        RiverRankUp(dock, fishery, longships, 0.6f, 40f);
        RiverRankUp(fishery, cannery, steamHulls, 0.6f, 40f);
        RiverRankUp(cannery, aquafarm, aquaculture, 0.6f, 40f);

        // ---------- Harbours ----------
        BuildingDefinition Harbour(string id, string display, Age age, Technology needs, string desc, List<ResourceAmount> cost, int hp, UnitDefinition boat,
            int boats, List<BuildingUpgrade> upgrades)
        {
            var b = Building(db, id, display, age, null, new Vector2Int(3, 3), new Vector2Int(3, 4), desc, cost, 30f, hp, 3, needs);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.catchmentRadius = 4;
            b.waterBonus = 0f;
            b.trainsUnit = boat;
            b.maxUnits = boats;
            b.housing = 0;
            b.towerRange = 0f;
            b.defensePower = 0f;
            b.sortLine = 0f;
            b.upgrades = upgrades;
            b.rankUpTo = null;
            EditorUtility.SetDirty(b);
            return b;
        }
        const string harbourHow = " Build it on the shore of open water. Its boats patrol the water and shoot at raiders near the shore; " +
                                  "send troops to land the water cuts off and its ferry carries them across.";
        var raftLanding = Harbour("RiverRaftLanding", "Raft Landing", stone, reedBoats,
            "A log slipway, bundles of reeds drying and a raft being lashed together. Trains Reed Rafts with a slinger aboard; its ferry seats 3." + harbourHow,
            C(wood, 120, food, 40), 260, raft, 2, new List<BuildingUpgrade>
            {
                Up("Second Raft", "+1 raft, +1 ferry seat.", C(wood, 100, food, 40), units: 1),
                Up("Hide Sails", "Rafts are 20% faster, +1 ferry seat.", C(wood, 80, food, 60), trained: new UnitStatModifier { speedMultiplier = 1.2f }),
            });
        var galleyDock = Harbour("RiverGalleyDock", "Galley Dock", classical, galleys,
            "A stone quay and a boathouse, a galley on the slip. Trains War Galleys with archers on deck; its ferry seats 5." + harbourHow,
            C(wood, 300, stoneR, 120, bronzeR, 40), 700, galley, 2, new List<BuildingUpgrade>
            {
                Up("Second Slip", "+1 galley, +1 ferry seat.", C(wood, 250, bronzeR, 40), units: 1),
                Up("Bronze Rams", "Galleys hit 25% harder, +1 ferry seat.", C(bronzeR, 80, wood, 100), trained: new UnitStatModifier { attackMultiplier = 1.25f }),
                Up("Oar Banks", "Galleys are 20% faster, +1 ferry seat.", C(wood, 300, gold, 30), trained: new UnitStatModifier { speedMultiplier = 1.2f }),
            });
        var longshipYard = Harbour("RiverLongshipYard", "Longship Yard", medieval, longships,
            "A long timber hall and a dragon-prowed longship on its rollers. Trains Longships; its ferry seats 6." + harbourHow,
            C(wood, 400, iron, 80, stoneR, 100), 1000, longship, 3, new List<BuildingUpgrade>
            {
                Up("Extra Slip", "+1 longship, +1 ferry seat.", C(wood, 350, iron, 60), units: 1),
                Up("Shield Walls", "Longships are 25% tougher, +1 ferry seat.", C(wood, 200, iron, 100), trained: new UnitStatModifier { healthMultiplier = 1.25f }),
                Up("Veteran Crews", "Longships hit 20% harder, +1 ferry seat.", C(gold, 60, iron, 80), trained: new UnitStatModifier { attackMultiplier = 1.2f }),
            });
        var shipyard = Harbour("RiverShipyard", "Shipyard", renaissance, charts,
            "A galleon rising in its frame of props, a sheer-legs crane and stacks of oak. Trains Galleons with broadsides of cannon; its ferry seats 8." + harbourHow,
            C(wood, 600, iron, 150, gold, 60, stoneR, 200), 1500, galleon, 2, new List<BuildingUpgrade>
            {
                Up("Dry Dock", "+1 galleon, +1 ferry seat.", C(wood, 500, stoneR, 300), units: 1),
                Up("Bronze Cannon", "Galleons hit 25% harder, +1 ferry seat.", C(iron, 200, gold, 60), trained: new UnitStatModifier { attackMultiplier = 1.25f }),
                Up("Copper Sheathing", "Galleons are 20% faster, +1 ferry seat.", C(gold, 120, wood, 200), trained: new UnitStatModifier { speedMultiplier = 1.2f }),
            });
        var steamDockyard = Harbour("RiverSteamDockyard", "Steam Dockyard", industrial, steamHulls,
            "An ironclad in a dry dock under an iron gantry, brick sheds and a smoking chimney. Trains Ironclads; its ferry seats 10." + harbourHow,
            C(steel, 300, coal, 150, stoneR, 300), 2200, ironclad, 2, new List<BuildingUpgrade>
            {
                Up("Second Berth", "+1 ironclad, +1 ferry seat.", C(steel, 250, stoneR, 200), units: 1),
                Up("Armour Belt", "Ironclads are 30% tougher, +1 ferry seat.", C(steel, 300), trained: new UnitStatModifier { healthMultiplier = 1.3f }),
                Up("Breech Loaders", "Ironclads hit 25% harder, +1 ferry seat.", C(steel, 200, coal, 100), trained: new UnitStatModifier { attackMultiplier = 1.25f }),
            });
        var hoverDock = Harbour("RiverHoverDock", "Hover Dock", electric, hoverHulls,
            "A landing pad of glowing rings and a dome hangar, a hover-barge humming above the pad. Trains Hover-Barges; its ferry seats 12." + harbourHow,
            C(steel, 400, power, 120, stoneR, 300), 3000, hoverBarge, 3, new List<BuildingUpgrade>
            {
                Up("Extra Pad", "+1 hover-barge, +1 ferry seat.", C(steel, 300, power, 80), units: 1),
                Up("Galvanic Guns", "Hover-barges hit 25% harder, +1 ferry seat.", C(power, 150, steel, 150), trained: new UnitStatModifier { attackMultiplier = 1.25f }),
                Up("Field Hulls", "Hover-barges are 30% tougher, +1 ferry seat.", C(power, 120, steel, 200), trained: new UnitStatModifier { healthMultiplier = 1.3f }),
            });
        RiverRankUp(raftLanding, galleyDock, galleys, 0.6f, 40f);
        RiverRankUp(galleyDock, longshipYard, longships, 0.6f, 40f);
        RiverRankUp(longshipYard, shipyard, charts, 0.6f, 40f);
        RiverRankUp(shipyard, steamDockyard, steamHulls, 0.6f, 40f);
        RiverRankUp(steamDockyard, hoverDock, hoverHulls, 0.6f, 40f);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only this setup's own) ----------
        foreach (var b in new[] { dock, fishery, cannery, aquafarm, raftLanding, galleyDock, longshipYard, shipyard, steamDockyard, hoverDock })
            GenerateRiverLooks(b);
        var boats = new (UnitDefinition def, RiverBoatKind kind, bool enemy, WeaponClass weapon)[]
        {
            (raft, RiverBoatKind.ReedRaft, false, WeaponClass.Thrown), (galley, RiverBoatKind.WarGalley, false, WeaponClass.Bow),
            (longship, RiverBoatKind.Longship, false, WeaponClass.Bow), (galleon, RiverBoatKind.Galleon, false, WeaponClass.Cannon),
            (ironclad, RiverBoatKind.Ironclad, false, WeaponClass.Cannon), (hoverBarge, RiverBoatKind.HoverBarge, false, WeaponClass.Cannon),
            (canoe, RiverBoatKind.RaiderCanoe, true, WeaponClass.None), (longboat, RiverBoatKind.RaiderLongboat, true, WeaponClass.None),
            (launch, RiverBoatKind.RaiderLaunch, true, WeaponClass.None), (skiff, RiverBoatKind.RaiderSkiff, true, WeaponClass.None),
        };
        foreach (var (def, kind, enemy, weapon) in boats)
        {
            def.prefab = BuildRiverBoat(def.name, kind, enemy, def, weapon);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{def.name}/{def.name}_Idle.png");
            EditorUtility.SetDirty(def);
        }

        // ---------- The scene's library ----------
        const string libPath = "Assets/Resources/RiverLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<RiverLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<RiverLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }
        lib.raidBoats = new List<UnitDefinition> { canoe, longboat, launch, skiff };
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("Rivers set up: 6 bridges, 4 fishing docks, 6 harbours, 6 warships, 4 raiders' boats and their research.");
    }

    /// <summary>'from' ranks up into 'to' (a whole bridge can be selected and ranked up at once).</summary>
    private static void RiverRankUp(BuildingDefinition from, BuildingDefinition to, Technology tech, float costShare = 0.8f, float time = 6f)
    {
        from.rankUpTo = to;
        from.rankUpTime = time;
        from.rankUpRequirements = new Requirements
        {
            cost = to.requirements.cost.ConvertAll(c => new ResourceAmount { resource = c.resource, amount = Mathf.Max(1, Mathf.RoundToInt(c.amount * costShare)) }),
            technologies = tech != null ? new List<Technology> { tech } : new List<Technology>(),
        };
        EditorUtility.SetDirty(from);
    }

    private static List<Sprite> RiverBridgePieces(string id, int tier)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var list = new List<Sprite>();
        var pivot = new Vector2(RiverPieceMargin / (float)RiverPieceSize, RiverPieceMargin / (float)RiverPieceSize);
        for (int piece = 0; piece < RiverWorks.PieceCount; piece++)
            list.Add(SavePixel(RiverBridgePiece(tier, piece), $"{dir}/{id}_piece{piece}.png", PixelsPerTile, pivot));
        return list;
    }

    /// <summary>A boat unit prefab from its 12 frames (like BuildPixelUnit, without a figure: boats aren't redrawn as heroes).</summary>
    private static GameObject BuildRiverBoat(string name, RiverBoatKind kind, bool isEnemy, UnitDefinition definition, WeaponClass weapon)
    {
        string dir = $"Units/{name}";
        ResetDir(dir);
        float ppu = UnitPPU;
        var pivot = RiverBoatPivot(kind);
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };
        var frames = new Sprite[names.Length];
        PixelCanvas idle = null;
        for (int f = 0; f < names.Length; f++)
        {
            var c = RiverBoatFrame(kind, f);
            if (f == 0) idle = c;
            frames[f] = SavePixel(c, $"{dir}/{name}_{names[f]}.png", ppu, pivot);
        }
        var shape = RiverShape(kind);
        int top = shape.gy, left = shape.w, right = 0;
        for (int y = 0; y < idle.H; y++)
            for (int x = 0; x < idle.W; x++)
                if (idle.Get(x, y).a > 200) { top = Mathf.Max(top, y); left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
        float height = (top - shape.gy) / ppu, width = (right - left) / ppu;

        var go = new GameObject($"{name}Unit") { tag = isEnemy ? "enemy" : "troop" };
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        var visual = go.AddComponent<SpriteUnitVisual>();
        visual.idle = frames[0];
        visual.walk = new List<Sprite> { frames[1], frames[2], frames[3], frames[4] };
        visual.attack = new List<Sprite> { frames[5], frames[6], frames[7], frames[8] };
        visual.death = new List<Sprite> { frames[9], frames[10], frames[11] };
        visual.impactFrame = 2;
        visual.walkFps = 5f;
        visual.deathFrameTime = 0.3f;
        visual.figure = null;
        visual.pixelsPerUnit = ppu;
        visual.weaponClass = weapon;
        visual.barHeight = height + 3f / ppu;
        visual.barWidth = Mathf.Clamp(width * 0.6f, 14f / ppu, 44f / ppu);
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(width * 0.5f, height) * 0.4f;
        col.offset = new Vector2(0f, height * 0.35f);
        go.AddComponent<MouseTouching>();
        Unit unit = isEnemy ? go.AddComponent<Enemy>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = Mathf.Clamp(width * 0.45f, 0.5f, 1.2f);
        if (unit is Enemy e) { e.lootPerHit = 0; e.lootCapacity = 999; }

        string prefabPath = $"Assets/Prefabs/Units/{name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
