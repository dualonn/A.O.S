using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Trade caravans and roads:
/// - Trading posts, one per era, each ranking up into the next: Trading Post (Bronze, ox wagons),
///   Caravanserai (Classical, horse caravans), Coaching Inn (Renaissance, wagon trains), Railway
///   Station (Industrial, steam trains), Maglev Terminal (Electric, maglev freighters) and Hover
///   Port (Astral, hover freighters). A post in two of your towns opens a trade route.
/// - Roads (dirt track, cobbled road, macadam road, asphalt road) and rail (rail track, maglev
///   guideway), dragged out in lines like walls: they join up seamlessly and speed up everyone.
/// - Four research items with small global trade bonuses (optional: they don't count towards an age).
/// - The caravans' art (Assets/Resources/TradeLibrary.asset).
/// Creates or updates only its own assets and art, and is safe to run again.
/// (Tools > Age of Stickman > Setup Caravans.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Caravans")]
    public static void SetupCaravans()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Caravans: stop playing first.");
            return;
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age bronze = AgeNamed("BronzeAge"), classical = AgeNamed("ClassicalAge"), renaissance = AgeNamed("RenaissanceAge"),
            enlightenment = AgeNamed("EnlightenmentAge"), industrial = AgeNamed("IndustrialAge"), modern = AgeNamed("ModernAge"),
            electric = AgeNamed("ElectricAge"), information = AgeNamed("InformationAge"), astral = AgeNamed("AstralAge");
        if (bronze == null || industrial == null || electric == null)
        {
            Debug.LogError("Setup Caravans: the ages aren't set up yet.");
            return;
        }
        astral ??= AgeNamed("ArcaneAge") ?? information ?? electric;
        information ??= electric;
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stoneR = Res(db, "Stone"), bronzeR = Res(db, "Bronze"),
            iron = Res(db, "Iron"), gold = Res(db, "Gold"), steel = Res(db, "Steel"), coin = Res(db, "Coin"), oil = Res(db, "Oil"),
            power = Res(db, "PowerCells"), chips = Res(db, "Microchips"), cores = Res(db, "AstralCores");
        List<ResourceAmount> C(params object[] pairs)
        {
            // Like Cost(), but skips resources this game doesn't have.
            var list = new List<ResourceAmount>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            return list;
        }

        // ---------- Research (small global bonuses; worked out in TradeRoutes.cs) ----------
        Technology T(string id, string name, Age age, string description, float time, List<ResourceAmount> cost)
        {
            var t = Tech(db, id, name, age, description, time, cost, null);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>();
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
        T(TradeCatalog.TechTolls, "Toll Roads", classical,
            "Milestones, toll gates and road wardens on the trade roads. Trade routes earn 10% more.", 70f, C(stoneR, 250, gold, 40));
        T(TradeCatalog.TechCredit, "Letters of Credit", renaissance,
            "A merchant's note is as good as gold in any of your towns. Trade routes earn 10% more, and caravans hire guards (20% tougher).",
            110f, C(coin, 300, gold, 120));
        T(TradeCatalog.TechTimetables, "Railway Timetables", industrial,
            "Every train to the minute, and every caravan on a schedule. Caravans and trains travel 15% faster.", 130f, C(coin, 500, steel, 150));
        T(TradeCatalog.TechLogistics, "Freight Logistics", information,
            "Containers, barcodes and a computer that knows where every crate is. Trade routes earn 10% more.", 160f, C(coin, 900, chips, 80));

        // ---------- Trading posts ----------
        BuildingDefinition Post(string id, string display, Age age, string desc, List<ResourceAmount> cost, int hp, float buildTime,
            List<BuildingUpgrade> upgrades)
        {
            var b = Building(db, id, display, age, null, new Vector2Int(3, 3), new Vector2Int(3, 4), desc, cost, buildTime, hp, 2, null);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.upgrades = upgrades;
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.defensePower = 0f;
            b.catchmentRadius = 0;
            b.waterBonus = 0f;
            b.sortLine = 0f;
            b.maxCount = 0;
            b.rankUpTo = null;
            EditorUtility.SetDirty(b);
            return b;
        }
        BuildingUpgrade Up(string name, string desc, List<ResourceAmount> cost) => new BuildingUpgrade
        {
            name = name, description = desc + " +1 route, and its routes earn 20% more.", requirements = new Requirements { cost = cost },
            upgradeTime = 30f, productionMultiplier = 1.2f, trainedUnits = new UnitStatModifier(),
        };
        const string how = " Build one in each of two towns (a town has housing or a town centre) and a trade route opens between them; " +
                           "caravans then go to and fro, and every trip brings coin and goods: more for long routes between big towns, and more on roads. " +
                           "Outposts the route passes produce more. Raiders attack caravans on the road.";
        var post = Post("TradePost", "Trading Post", bronze,
            "A mud-brick store and an awning over sacks of grain and pots of oil. Runs ox wagons on 1 route." + how,
            C(wood, 120, stoneR, 40, food, 40), 400, 25f, new List<BuildingUpgrade>
            {
                Up("Second Wagon", "Another ox team and wagon.", C(wood, 100, food, 60)),
                Up("Storehouse", "Room for more goods.", C(wood, 150, stoneR, 80, bronzeR, 20)),
            });
        var caravanserai = Post("TradeCaravanserai", "Caravanserai", classical,
            "A walled courtyard for caravans to rest in, with a great arched gate. Runs horse caravans on 2 routes." + how,
            C(stoneR, 300, wood, 200, gold, 30), 900, 35f, new List<BuildingUpgrade>
            {
                Up("Stables", "Fresh horses at every stop.", C(wood, 250, food, 150)),
                Up("Bazaar", "Merchants trade in the courtyard itself.", C(stoneR, 250, gold, 60)),
            });
        var inn = Post("TradeCoachingInn", "Coaching Inn", renaissance,
            "A timber-framed inn with a swinging sign and a coach house. Runs wagon trains on 2 routes." + how,
            C(wood, 400, stoneR, 300, gold, 80, coin, 100), 1300, 40f, new List<BuildingUpgrade>
            {
                Up("Post House", "The mail comes and goes from here.", C(wood, 300, coin, 150)),
                Up("Merchant Guild", "A guild hall upstairs to strike deals in.", C(stoneR, 300, gold, 120, coin, 150)),
            });
        var station = Post("TradeRailStation", "Railway Station", industrial,
            "A brick station with a clock tower and an iron canopy over the platform. Runs steam trains on 3 routes, " +
            "where rail track is laid along most of the way (wagon trains otherwise)." + how,
            C(stoneR, 600, steel, 200, wood, 300, coin, 300), 2400, 45f, new List<BuildingUpgrade>
            {
                Up("Goods Yard", "Sidings and a crane for freight.", C(steel, 200, coin, 300)),
                Up("Express Line", "Faster engines on the main line.", C(steel, 300, coin, 400)),
            });
        var terminal = Post("TradeMaglevTerminal", "Maglev Terminal", electric,
            "A streamlined terminal of chrome and neon over a humming guideway. Runs maglev freighters on 3 routes " +
            "where rail or guideway is laid along most of the way (wagon trains otherwise)." + how,
            C(steel, 500, power, 150, stoneR, 400, coin, 600), 3400, 45f, new List<BuildingUpgrade>
            {
                Up("Freight Deck", "A second platform for cargo.", C(steel, 300, power, 100)),
                Up("Galvanic Coils", "Stronger fields, longer trains.", C(power, 200, coin, 600)),
            });
        var hoverPort = Post("TradeHoverPort", "Hover Port", astral,
            "A landing pad of glowing rings and a dome hangar. Runs hover freighters on 4 routes: they skim straight over " +
            "land and water alike, and need no track." + how,
            C(steel, 800, power, 300, cores, 60, coin, 1200), 5000, 50f, new List<BuildingUpgrade>
            {
                Up("Second Pad", "Another freighter in the air.", C(steel, 500, power, 200)),
                Up("Grav Cradles", "Bigger pods, gently held.", C(power, 400, cores, 40, coin, 1000)),
            });
        TradeRankUp(post, caravanserai);
        TradeRankUp(caravanserai, inn);
        TradeRankUp(inn, station);
        TradeRankUp(station, terminal);
        TradeRankUp(terminal, hoverPort);

        // ---------- Roads and rail ----------
        BuildingDefinition Road(string id, string display, Age age, WallTile kind, string desc, List<ResourceAmount> cost, int hp, int art)
        {
            var b = Building(db, id, display, age, null, Vector2Int.one, Vector2Int.one, desc, cost, 2f, hp, 0, null);
            b.wallKind = kind;
            b.canBuildOnHills = true;
            b.defensePower = 0f;
            b.sortLine = 4f;   // flat on the ground: everything near draws over it
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
            b.pieces = TradeRoadPieces(id, art);
            b.icon = b.pieces[2 | 8];
            EditorUtility.SetDirty(b);
            return b;
        }
        const string roadHow = " Drag out a line like a wall. Everyone on a road moves 40% faster, caravans earn more on roads, and paths prefer them.";
        const string railHow = " Drag out a line between two stations: trains run where most of the way is track. Walkers cross it a little faster too.";
        var dirt = Road("TradeRoadDirt", "Dirt Track", bronze, WallTile.Road, "Packed earth worn into ruts by cartwheels." + roadHow, C(wood, 2), 150, 0);
        var cobble = Road("TradeRoadCobble", "Cobbled Road", classical, WallTile.Road, "Rounded setts laid in sand between kerb stones." + roadHow, C(stoneR, 4), 300, 1);
        var macadam = Road("TradeRoadMacadam", "Macadam Road", enlightenment ?? renaissance, WallTile.Road, "Layers of crushed stone, rolled hard and cambered to shed the rain." + roadHow, C(stoneR, 5), 450, 2);
        var asphalt = Road("TradeRoadAsphalt", "Asphalt Road", modern ?? industrial, WallTile.Road, "Smooth black tarmac with a dashed white line down the middle." + roadHow, C(oil, 2, stoneR, 3), 700, 3);
        var rail = Road("TradeRailTrack", "Rail Track", industrial, WallTile.Rail, "Iron rails spiked to wooden sleepers on a bed of ballast." + railHow, C(steel, 2, wood, 2), 500, 10);
        var guideway = Road("TradeMaglevGuideway", "Maglev Guideway", electric, WallTile.Rail, "A pale concrete beam with a humming strip of galvanic coils." + railHow, C(steel, 2, power, 1), 900, 11);
        TradeRankUp(dirt, cobble, 6f);
        TradeRankUp(cobble, macadam, 6f);
        TradeRankUp(macadam, asphalt, 6f);
        TradeRankUp(rail, guideway, 6f);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only this setup's own) ----------
        foreach (var b in new[] { post, caravanserai, inn, station, terminal, hoverPort }) GenerateTradeLooks(b);

        const string libPath = "Assets/Resources/TradeLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<TradeLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<TradeLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }
        lib.vehicles = new List<TradeLibrary.Vehicle>();
        for (int tier = 0; tier <= TradeCatalog.TierHover; tier++) lib.vehicles.Add(TradeVehicleArt(tier));
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("Caravans set up: 6 trading posts, 4 roads, 2 kinds of track, 4 research items and 6 caravans.");
    }

    /// <summary>'from' ranks up into 'to' for most of the cost of a new one.</summary>
    private static void TradeRankUp(BuildingDefinition from, BuildingDefinition to, float time = 40f)
    {
        from.rankUpTo = to;
        from.rankUpTime = time;
        from.rankUpRequirements = new Requirements
        {
            cost = to.requirements.cost.ConvertAll(c => new ResourceAmount { resource = c.resource, amount = Mathf.Max(1, Mathf.RoundToInt(c.amount * 0.6f)) }),
            technologies = new List<Technology>(),
        };
        EditorUtility.SetDirty(from);
    }
}
