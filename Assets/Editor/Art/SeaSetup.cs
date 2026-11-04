using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The sea economy (see SeaData.cs), Stone Age to Galactic Age:
/// - Fishing: fish weirs, fish pens, trawler ports, deep-sea harvesters, kelp farms and plankton
///   lattices, leviathan ranches and void trawlers (the rivers setup's docks join in with boats).
/// - Whaling stations and harpoon wharves; salt pans; pearl divers.
/// - Lighthouses: beacon, pharos, lamp lighthouse, star spire.
/// - Ports for sea routes: wharf, cog harbour, trading port, steamer docks, container port, hover
///   quay, void mooring (each ranks up into the next).
/// - Offshore oil rigs, tidal generators, sea walls (hold back floods), stilt huts, pier houses,
///   floating homes and sea domes.
/// - Research with small global bonuses, the sea creatures and pirates' ships, and all the art.
/// Creates or updates only its own assets and art, and is safe to run again.
/// (Tools > Age of Stickman > Setup Sea.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Sea")]
    public static void SetupSea()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Sea: stop playing first.");
            return;
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (db == null) { Debug.LogError("Setup Sea: no GameDatabase."); return; }
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age stone = AgeNamed("StoneAge"), bronze = AgeNamed("BronzeAge"), ironAge = AgeNamed("IronAge"), classical = AgeNamed("ClassicalAge"),
            medieval = AgeNamed("MedievalAge"), renaissance = AgeNamed("RenaissanceAge"), enlightenment = AgeNamed("EnlightenmentAge"),
            industrial = AgeNamed("IndustrialAge"), modern = AgeNamed("ModernAge"), electric = AgeNamed("ElectricAge"),
            information = AgeNamed("InformationAge"), arcane = AgeNamed("ArcaneAge"), astral = AgeNamed("AstralAge"),
            stellar = AgeNamed("StellarAge"), galactic = AgeNamed("GalacticAge");
        if (stone == null || electric == null || galactic == null)
        {
            Debug.LogError("Setup Sea: the ages aren't set up yet.");
            return;
        }
        arcane ??= information; astral ??= arcane; stellar ??= astral;
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stoneR = Res(db, "Stone"), bronzeR = Res(db, "Bronze"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), steel = Res(db, "Steel"), coal = Res(db, "Coal"), coin = Res(db, "Coin"), power = Res(db, "PowerCells"),
            chips = Res(db, "Microchips"), mana = Res(db, "Mana"), oil = Res(db, "Oil"), essence = Res(db, "Essence"), starmetal = Res(db, "Starmetal"),
            stardust = Res(db, "Stardust"), aether = Res(db, "Aether");
        List<ResourceAmount> C(params object[] pairs)
        {
            var list = new List<ResourceAmount>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            return list;
        }
        List<ResourceRate> Make(params object[] pairs)
        {
            var list = new List<ResourceRate>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(Rate(r, (float)pairs[i + 1]));
            return list;
        }
        var naval = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/RiverNaval.asset");

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
        var nets = T(SeaCatalog.TechNets, "Knotted Nets", stone,
            "Nets of twisted fibre, knotted by the fire on winter nights. Unlocks the Fish Weir. Fishing buildings catch 5% more.", 35f, C(food, 40, wood, 40));
        var stilts = T(SeaCatalog.TechStilts, "Stilt Houses", stone,
            "Posts driven into the mud and a floor above the tide. Unlocks Stilt Huts. Buildings on the water may stand a tile further out.", 40f, C(wood, 70, food, 30), nets);
        var salt = T(SeaCatalog.TechSalt, "Salt Curing", bronze,
            "Sea water dried in the sun leaves salt, and salted fish keeps all winter. Unlocks Salt Pans.", 55f, C(food, 100, stoneR, 60), nets);
        var beacons = T(SeaCatalog.TechBeacons, "Beacon Fires", bronze,
            "A fire on the headland to bring the boats home. Unlocks the Beacon.", 55f, C(wood, 150, stoneR, 60), nets);
        var breakwaters = T(SeaCatalog.TechSeaWalls, "Breakwaters", ironAge ?? bronze,
            "Great stones heaped along the shore to break the waves. Unlocks the Sea Wall, which holds back floods.", 70f, C(stoneR, 300, iron, 30), beacons);
        var pearls = T(SeaCatalog.TechPearls, "Pearl Diving", classical,
            "Divers who hold their breath for minutes, and the oyster beds they know. Unlocks Pearl Divers and Fish Pens.", 80f, C(food, 200, gold, 30), salt);
        var coastal = T(SeaCatalog.TechCoastal, "Coastal Trade", classical,
            "Round-bellied merchant ships hugging the coast from port to port. Unlocks the Wharf and the Lighthouse. Sea trade earns 5% more.", 85f, C(wood, 300, gold, 40), beacons);
        var whaling = T(SeaCatalog.TechWhaling, "Whaling", medieval,
            "Open boats, long lines and hand harpoons. Unlocks the Whaling Station.", 100f, C(wood, 400, iron, 80), coastal);
        var hanse = T(SeaCatalog.TechCogs, "Hanseatic League", medieval,
            "Merchant towns banded together, with cogs, counting houses and their own laws. Unlocks the Cog Harbour. Sea trade earns 10% more.", 110f, C(gold, 120, wood, 400), coastal);
        var reckoning = T(SeaCatalog.TechReckoning, "Dead Reckoning", renaissance,
            "Log line, sandglass and chart: knowing where you are out of sight of land. Unlocks the Trading Port. Merchant ships sail 10% faster.", 130f, C(gold, 200, wood, 500), hanse);
        var insurance = T(SeaCatalog.TechInsurance, "Marine Insurance", enlightenment,
            "Underwriters in a coffee house share out the risk of every voyage. Unlocks the Lamp Lighthouse. A lost ship pays back 40% of its cargo, and ships are 15% tougher.", 140f, C(coin, 400, gold, 100), reckoning);
        var steamPackets = T(SeaCatalog.TechSteamPackets, "Steam Packets", industrial,
            "Steamships to a timetable, whatever the wind. Unlocks Steamer Docks, the Harpoon Wharf and the Storm Barrier. Merchant ships sail 10% faster.", 150f, C(steel, 300, coal, 200), reckoning);
        var management = T(SeaCatalog.TechManagement, "Fishery Management", industrial,
            "Counting the fish and leaving enough to breed. Each level: shoals and pods regrow 12% faster. Whalers never hunt a pod out.", 120f, C(food, 400, coin, 200), nets);
        management.maxLevel = SeaCatalog.ManagementLevels;
        management.levelCostGrowth = 1.5f;
        management.levelTimeGrowth = 1.25f;
        EditorUtility.SetDirty(management);
        var containers = T(SeaCatalog.TechContainers, "Containerisation", modern,
            "One steel box that fits every ship, train and truck. Unlocks the Container Port, the Trawler Port and Floating Homes. Sea trade earns 10% more.", 160f, C(steel, 500, coin, 400), steamPackets);
        var offshore = T(SeaCatalog.TechOffshore, "Offshore Drilling", modern,
            "Steel legs standing in the open sea over the oil beneath it. Unlocks the Oil Rig.", 160f, C(steel, 500, oil, 100), steamPackets);
        var tidal = T(SeaCatalog.TechTidal, "Tidal Power", electric,
            "Turbines turned by the tide, twice a day, every day. Unlocks the Tidal Generator and the Kelp Farm.", 170f, C(steel, 400, power, 100), offshore);
        var sonar = T(SeaCatalog.TechSonar, "Sonar", information,
            "Sound sent down into the deep, and the shoals it finds. Unlocks the Deep-Sea Harvester and the Hover Quay. Fishing reaches 3 tiles further and catches 5% more.", 180f, C(chips, 150, steel, 300), containers);
        var leviathans = T(SeaCatalog.TechLeviathans, "Leviathan Lore", arcane,
            "Old songs that calm the great beasts of the deep. Unlocks the Leviathan Ranch.", 190f, C(mana, 250, food, 600), tidal);
        var voidTides = T(SeaCatalog.TechVoidTides, "Void Tides", astral,
            "The sea between the stars has tides of its own. Unlocks the Void Mooring, the Sea Dome and the Star Spire.", 200f, C(mana, 200, steel, 500, starmetal ?? power, 100), sonar);
        var plankton = T(SeaCatalog.TechPlankton, "Plankton Blooms", stellar,
            "Light tuned to make the sea bloom green and gold. Unlocks the Plankton Lattice. Kelp farms grow 5% more.", 210f, C(power, 300, stardust ?? mana, 100), tidal, voidTides);
        var voidTrawling = T(SeaCatalog.TechVoidTrawling, "Void Trawling", galactic,
            "Nets of light drawn through the dark tides of other worlds. Unlocks the Void Trawler.", 220f, C(starmetal ?? steel, 300, aether ?? power, 150), voidTides);

        // ---------- Buildings ----------
        BuildingDefinition B(string id, string display, Age age, Technology needs, Vector2Int size, Vector2Int art, string desc,
            List<ResourceAmount> cost, int hp, int workers, List<ResourceRate> production, int housing, List<BuildingUpgrade> ups, float buildTime = 25f)
        {
            var b = Building(db, id, display, age ?? stone, null, size, art, desc, cost, buildTime, hp, workers, needs);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = production ?? new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.catchmentRadius = 0;
            b.waterBonus = 0f;
            b.minDepositTiles = 0;
            b.fullOutputTiles = 10;
            b.upgrades = ups ?? new List<BuildingUpgrade>();
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = housing;
            b.towerRange = 0f;
            b.defensePower = 0f;
            b.sortLine = 0f;
            b.rankUpTo = null;
            b.maxCount = 0;
            EditorUtility.SetDirty(b);
            return b;
        }
        BuildingUpgrade Up(string name, string desc, List<ResourceAmount> cost, float output = 1f, int health = 0, int housing = 0) => new BuildingUpgrade
        {
            name = name, description = desc, requirements = new Requirements { cost = cost }, upgradeTime = 30f,
            productionMultiplier = output, extraHealth = health, extraHousing = housing, trainedUnits = new UnitStatModifier(),
        };
        var s2 = new Vector2Int(2, 2); var s3x2 = new Vector2Int(3, 2); var s3 = new Vector2Int(3, 3); var s4x3 = new Vector2Int(4, 3);
        var tall = new Vector2Int(2, 3);
        const string shoalHow = " Its boats go out to the fish shoals in reach (silver glints on the water); the richer they are, the bigger the catch. Shoals fished hard thin out and move on, and regrow when left alone.";
        const string portHow = " Build one in two of your towns on the same water and merchant ships sail between them: coin and goods every voyage, more for long routes between big towns. Pirates prey on them; lighthouses and warships keep them safer. Each upgrade opens one more route.";
        const string lightHow = " Its beam guides your boats (they fish more and come home safely), speeds ships along, keeps pirates off your sea routes, and burns raiders' and pirates' hulls.";
        const string floatHow = " Stands out over the water, close to land or to another building on the water: raiders on foot can't reach it.";

        // Fishing.
        var weir = B("SeaFishWeir", "Fish Weir", stone, nets, s2, s2,
            "A fence of stakes and wicker out into the water that steers fish into a trap. A small, steady catch; better with a shoal close by." + shoalHow,
            C(wood, 30, stoneR, 10), 100, 1, Make(food, 0.45f), 0, new List<BuildingUpgrade>
            {
                Up("Wicker Traps", "+25% catch.", C(wood, 40), 1.25f),
                Up("Second Weir", "+20% catch, reaches 2 tiles further.", C(wood, 60, stoneR, 20), 1.2f),
            });
        var pens = B("SeaFishPens", "Fish Pens", classical, pearls, s3x2, s3x2,
            "Stone-walled pens let into the shore, where fish are kept and fattened. A steady catch that needs no shoals (more on an ocean world). Build it across the shoreline.",
            C(stoneR, 140, wood, 60), 450, 2, Make(food, 1.1f), 0, new List<BuildingUpgrade>
            {
                Up("Bigger Pens", "+25% catch.", C(stoneR, 120), 1.25f),
                Up("Breeding Tanks", "+20% catch.", C(stoneR, 100, gold, 20), 1.2f),
            });
        var trawler = B("SeaTrawlerPort", "Trawler Port", modern, containers, s3, s3,
            "A concrete quay with an ice plant and big diesel stern trawlers that fish shoals far out (16 tiles). A huge catch where the shoals are rich, little where they aren't." + shoalHow,
            C(steel, 260, stoneR, 200, coin, 150), 1600, 4, Make(food, 5f), 0, new List<BuildingUpgrade>
            {
                Up("Freezer Holds", "+25% catch, reaches 2 tiles further.", C(steel, 150, coin, 100), 1.25f),
                Up("Fish Finders", "+20% catch, reaches 2 tiles further, one more trawler.", C(steel, 150, oil, 60), 1.2f),
            });
        var harvester = B("SeaDeepHarvester", "Deep-Sea Harvester", information, sonar, s3, s3,
            "A white hub sending harvester drones out to shoals 22 tiles away, guided by sonar." + shoalHow,
            C(steel, 400, chips, 60, stoneR, 300), 2400, 4, Make(food, 8f), 0, new List<BuildingUpgrade>
            {
                Up("Swarm Drones", "+25% catch, reaches 2 tiles further.", C(chips, 80, power, 60), 1.25f),
                Up("Bycatch Filters", "+20% catch, one more drone.", C(chips, 60, steel, 150), 1.2f),
            });
        var kelp = B("SeaKelpFarm", "Kelp Farm", electric, tidal, s3, s3,
            "Rows of floats with kelp growing on lines beneath: a big, steady harvest from open water, best on an ocean world." + floatHow,
            C(steel, 120, wood, 100), 900, 2, Make(food, 3.5f), 0, new List<BuildingUpgrade>
            {
                Up("Longer Lines", "+25% harvest.", C(steel, 80), 1.25f),
                Up("Nutrient Pumps", "+20% harvest.", C(steel, 80, power, 40), 1.2f),
            });
        var lattice = B("SeaPlanktonLattice", "Plankton Lattice", stellar, plankton, s3, s3,
            "A lattice of glowing tubes where plankton blooms under tuned light: food for a world." + floatHow,
            C(steel, 300, power, 120), 2600, 2, Make(food, 9f), 0, new List<BuildingUpgrade>
            {
                Up("Bloom Lamps", "+25% harvest.", C(power, 150), 1.25f),
                Up("Seeded Strains", "+20% harvest.", C(power, 120, mana, 60), 1.2f),
            });
        var ranch = B("SeaLeviathanRanch", "Leviathan Ranch", arcane, leviathans, s4x3, s4x3,
            "A pen of rune-carved posts out in the sea where tamed leviathans graze, calmed by old songs. A vast catch, and mana from the singers. Build it by open sea; best on an ocean world.",
            C(mana, 200, steel, 300, stoneR, 300), 3500, 3, Make(food, 10f, mana, 0.3f), 0, new List<BuildingUpgrade>
            {
                Up("Song Circles", "+25% output.", C(mana, 200), 1.25f),
                Up("Second Pen", "+20% output.", C(mana, 150, steel, 200), 1.2f),
            });
        var voidTrawler = B("SeaVoidTrawler", "Void Trawler", galactic, voidTrawling, s3, s3,
            "A dock of star-steel whose trawlers draw nets of light through the dark tides, out to 24 tiles." + shoalHow,
            C(starmetal ?? steel, 300, steel, 400, aether ?? power, 120), 4200, 4, Make(food, 16f, essence, 0.15f), 0, new List<BuildingUpgrade>
            {
                Up("Tide Engines", "+25% catch, reaches 2 tiles further.", C(aether ?? power, 150), 1.25f),
                Up("Starlight Nets", "+20% catch, one more trawler.", C(starmetal ?? steel, 150), 1.2f),
            });
        // Whaling.
        var whalers = B("SeaWhalingStation", "Whaling Station", medieval, whaling, s3x2, s3x2,
            "A slipway with whaleboats, a try-works boiling blubber and barrels stacked high. Its boats hunt the whales of the open sea within 18 tiles: meat and coin. Build it by open sea.",
            C(wood, 220, iron, 40), 500, 3, Make(food, 1.4f, coin, 0.1f), 0, new List<BuildingUpgrade>
            {
                Up("Try-Works", "+25% output, hunts faster.", C(wood, 150, iron, 40), 1.25f),
                Up("Second Boat", "+20% output, hunts faster, reaches 2 tiles further.", C(wood, 200), 1.2f),
            });
        var harpoon = B("SeaHarpoonWharf", "Harpoon Wharf", industrial, steamPackets, s3x2, s3x2,
            "Steam catchers with harpoon guns at the bow, out to 24 tiles: they hunt sea serpents too, and bring in whale oil.",
            C(steel, 200, wood, 200, coal, 60), 1300, 3, Make(food, 2.6f, coin, 0.25f, oil, 0.12f), 0, new List<BuildingUpgrade>
            {
                Up("Explosive Harpoons", "+25% output, hunts faster.", C(steel, 150, coal, 60), 1.25f),
                Up("Factory Ship", "+20% output, hunts faster.", C(steel, 200), 1.2f),
            });
        // The shore's trades.
        var saltPans = B("SeaSaltPans", "Salt Pans", bronze, salt, s3x2, s3x2,
            "Shallow pans of brine drying in the sun, white salt raked into heaps. Salt preserves the catch: fishing and whaling bring 6% more food per pan (up to 3 pans), and the pans make a little food of their own. Build them by open sea.",
            C(stoneR, 60, wood, 40), 220, 2, Make(food, 0.25f), 0, new List<BuildingUpgrade>
            {
                Up("More Pans", "+25% output.", C(stoneR, 60), 1.25f),
            });
        var pearlDivers = B("SeaPearlDivers", "Pearl Divers", classical, pearls, s2, s2,
            "A diving platform over clear water. Its divers work the oyster beds in reach (pale shells on the water near the shore) for pearls: gold, and a little happiness in every town (+1 mood each, up to 3).",
            C(wood, 80, stoneR, 40), 260, 2, Make(gold, 0.12f), 0, new List<BuildingUpgrade>
            {
                Up("Diving Stones", "+25% pearls.", C(stoneR, 40, gold, 10), 1.25f),
                Up("Oyster Knives", "+20% pearls, reaches 2 tiles further.", C(bronzeR ?? iron, 30, gold, 20), 1.2f),
            });
        // Lighthouses.
        var beacon = B("SeaBeacon", "Beacon", bronze, beacons, s2, tall,
            "A cairn tower with a fire basket on top. Beam: 14 tiles." + lightHow, C(wood, 60, stoneR, 60), 300, 1, null, 0, new List<BuildingUpgrade>
            {
                Up("Pitch Fires", "Beam reaches 2 tiles further.", C(wood, 60), 1f, 60),
            });
        var lighthouse = B("SeaLighthouse", "Lighthouse", classical, coastal, s2, tall,
            "A tall stone pharos with a keeper's lodge and a great fire behind bronze mirrors. Beam: 18 tiles." + lightHow,
            C(stoneR, 220, wood, 60, bronzeR ?? iron, 20), 800, 1, null, 0, new List<BuildingUpgrade>
            {
                Up("Bronze Mirrors", "Beam reaches 2 tiles further.", C(bronzeR ?? iron, 40, gold, 20), 1f, 150),
                Up("Keeper's Watch", "Beam reaches 2 tiles further.", C(stoneR, 120), 1f, 150),
            });
        var lampLight = B("SeaLampLighthouse", "Lamp Lighthouse", enlightenment, insurance, s2, tall,
            "A banded tower with an oil lamp behind a ground-glass lens. Beam: 23 tiles." + lightHow,
            C(stoneR, 400, iron, 100, coin, 150), 1400, 1, null, 0, new List<BuildingUpgrade>
            {
                Up("Fresnel Lens", "Beam reaches 2 tiles further.", C(coin, 200, iron, 60), 1f, 200),
                Up("Fog Horn", "Beam reaches 2 tiles further.", C(iron, 100), 1f, 200),
            });
        var starSpire = B("SeaStarSpire", "Star Spire", astral, voidTides, s2, tall,
            "A slender crystal spire throwing a star-coloured beam out over the sea. Beam: 30 tiles." + lightHow,
            C(steel, 300, power, 120, mana, 100), 3200, 1, null, 0, new List<BuildingUpgrade>
            {
                Up("Focusing Rings", "Beam reaches 2 tiles further.", C(power, 150), 1f, 400),
                Up("Starfire Core", "Beam reaches 2 tiles further.", C(mana, 150, power, 100), 1f, 400),
            });
        // Ports.
        List<BuildingUpgrade> PortUps(string a, string b, List<ResourceAmount> ca, List<ResourceAmount> cb) => new List<BuildingUpgrade>
        {
            Up(a, "One more sea route.", ca, 1f, 100), Up(b, "One more sea route.", cb, 1f, 150),
        };
        var wharf = B("SeaWharf", "Wharf", classical, coastal, s3, s3,
            "A timber wharf and a warehouse of amphorae; round-bellied merchant ships tie up alongside." + portHow,
            C(wood, 220, stoneR, 100, gold, 20), 600, 2, null, 0, PortUps("Second Berth", "Warehouse", C(wood, 150), C(wood, 200, gold, 30)));
        var cogHarbour = B("SeaCogHarbour", "Cog Harbour", medieval, hanse, s3, s3,
            "A stone harbour wall, a treadwheel crane and cogs with high castles at the quay." + portHow,
            C(wood, 350, stoneR, 150, iron, 40), 1000, 3, null, 0, PortUps("Treadwheel Crane", "Counting House", C(wood, 250, iron, 40), C(gold, 60, stoneR, 200)));
        var tradingPort = B("SeaTradingPort", "Trading Port", renaissance, reckoning, s3, s3,
            "A customs house with a clock, warehouses and the masts of galleons at the quay." + portHow,
            C(wood, 500, stoneR, 300, gold, 80, iron, 100), 1600, 3, null, 0, PortUps("Bonded Warehouse", "Customs House", C(stoneR, 300, gold, 60), C(gold, 120, iron, 80)));
        var steamerDocks = B("SeaSteamerDocks", "Steamer Docks", industrial, steamPackets, s3, s3,
            "Brick warehouses, a coaling tower and steamships at the iron quay." + portHow,
            C(steel, 260, coal, 120, stoneR, 300), 2400, 4, null, 0, PortUps("Coaling Tower", "Wet Dock", C(steel, 200, coal, 100), C(steel, 250, stoneR, 200)));
        var containerPort = B("SeaContainerPort", "Container Port", modern, containers, s3, s3,
            "Stacks of containers and a gantry crane over a container ship." + portHow,
            C(steel, 500, stoneR, 400, coin, 300), 3200, 4, null, 0, PortUps("Second Gantry", "Automated Yard", C(steel, 300, coin, 200), C(steel, 300, chips ?? coin, 60)));
        var hoverQuay = B("SeaHoverQuay", "Hover Quay", information, sonar, s3, s3,
            "Glowing landing rings on the water and a dome terminal for hover-barges." + portHow,
            C(steel, 500, power, 150, chips ?? coin, 40), 3800, 4, null, 0, PortUps("Extra Ring", "Traffic Control", C(steel, 300, power, 80), C(chips ?? coin, 80, power, 100)));
        var voidMooring = B("SeaVoidMooring", "Void Mooring", astral, voidTides, s3, s3,
            "Black stone piers into a sea of starlight, and void barges moored to pylons of light." + portHow,
            C(steel, 600, power, 200, mana, 150), 5200, 4, null, 0, PortUps("Second Pylon", "Tide Charts", C(power, 200, steel, 300), C(mana, 200, power, 150)));
        // Industry and power.
        var oilRig = B("SeaOilRig", "Offshore Oil Rig", modern, offshore, s2, tall,
            "Steel legs standing in the open sea, a derrick and a gas flare. Drills the oil under the sea: more in deeper water. Build it at least 2 tiles out from the shore.",
            C(steel, 300, coin, 200), 1800, 3, Make(oil, 0.9f), 0, new List<BuildingUpgrade>
            {
                Up("Second Well", "+25% oil.", C(steel, 200), 1.25f),
                Up("Deep Drill", "+20% oil.", C(steel, 200, coin, 150), 1.2f),
            });
        var tidalGen = B("SeaTidalGenerator", "Tidal Generator", electric, tidal, s2, tall,
            $"A barrage across the shoreline with turbines turned by the tide: power cells, and {SeaCatalog.TidalPowerPerGenerator:0} power for the grid (+{SeaCatalog.TidalPowerPerUpgrade:0} an upgrade). Build it across the shoreline.",
            C(steel, 250, stoneR, 200), 1600, 1, Make(power, 0.3f), 0, new List<BuildingUpgrade>
            {
                Up("Bigger Turbines", "+25% output.", C(steel, 150), 1.25f),
                Up("Tide Gates", "+20% output.", C(steel, 150, stoneR, 150), 1.2f),
            });
        // Homes on the water.
        var stiltHuts = B("SeaStiltHuts", "Stilt Huts", stone, stilts, s2, s2,
            "Reed huts on posts over the shallows, a walkway between. Build them across the shoreline: room for 5.",
            C(wood, 45), 140, 0, null, 5, new List<BuildingUpgrade>
            {
                Up("Reed Walls", "+2 room.", C(wood, 30), 1f, 20, 2),
                Up("Raised Floors", "+2 room.", C(wood, 40), 1f, 40, 2),
            }, 15f);
        var pierHouses = B("SeaPierHouses", "Pier Houses", medieval, hanse, s3, s3,
            "Timber-framed houses on a pier with washing lines between. Build them across the shoreline: room for 36.",
            C(wood, 200, stoneR, 80), 700, 0, null, 36, new List<BuildingUpgrade>
            {
                Up("Upper Floors", "+6 room.", C(wood, 120), 1f, 100, 6),
                Up("Fish Market", "+6 room.", C(wood, 100, stoneR, 60), 1f, 100, 6),
            }, 25f);
        var floatingHomes = B("SeaFloatingHomes", "Floating Homes", modern, containers, s2, s2,
            "Houses on pontoons linked by gangways, solar panels on the roofs: room for 80." + floatHow,
            C(steel, 100, stoneR, 120, wood, 60), 1500, 0, null, 80, new List<BuildingUpgrade>
            {
                Up("Second Pontoon", "+16 room.", C(steel, 80, stoneR, 80), 1f, 200, 16),
            }, 25f);
        var seaDome = B("SeaDome", "Sea Dome", astral, voidTides, s3, s3,
            "A glass dome on a ring of floats with gardens and towers inside: room for 160." + floatHow,
            C(steel, 350, power, 100, stoneR, 300), 4000, 0, null, 160, new List<BuildingUpgrade>
            {
                Up("Hanging Gardens", "+30 room.", C(steel, 200, power, 60), 1f, 400, 30),
                Up("Second Ring", "+30 room.", C(steel, 250, power, 80), 1f, 400, 30),
            }, 40f);
        // Sea walls: wall lines that hold back floodwater.
        BuildingDefinition SeaWallDef(string id, string display, Age age, Technology needs, string desc, List<ResourceAmount> cost, int hp)
        {
            var b = Building(db, id, display, age, null, Vector2Int.one, Vector2Int.one, desc, cost, 4f, hp, 0, needs);
            b.wallKind = WallTile.Wall;
            b.canBuildOnHills = true;
            b.defensePower = 0.03f;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.upgrades = new List<BuildingUpgrade>();
            b.looks = new List<BuildingLook>();
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.rankUpTo = null;
            EditorUtility.SetDirty(b);
            return b;
        }
        const string wallHow = " Drag out a line along the shore (within 3 tiles of the water): <color=#9cf>floodwater can't get past it</color>. It blocks everyone, like a wall.";
        var seaWall = SeaWallDef("SeaWall", "Sea Wall", ironAge ?? bronze, breakwaters, "Great dressed stones heaped against the waves." + wallHow, C(stoneR, 5), 800);
        var stormBarrier = SeaWallDef("SeaStormBarrier", "Storm Barrier", industrial, steamPackets, "A curved wall of concrete and steel that throws the waves back." + wallHow, C(stoneR, 14, steel, 2), 2400);

        // Rank-ups (each line ranks up into the next).
        RiverRankUp(beacon, lighthouse, coastal, 0.6f, 30f);
        RiverRankUp(lighthouse, lampLight, insurance, 0.6f, 30f);
        RiverRankUp(lampLight, starSpire, voidTides, 0.6f, 30f);
        RiverRankUp(wharf, cogHarbour, hanse, 0.6f, 40f);
        RiverRankUp(cogHarbour, tradingPort, reckoning, 0.6f, 40f);
        RiverRankUp(tradingPort, steamerDocks, steamPackets, 0.6f, 40f);
        RiverRankUp(steamerDocks, containerPort, containers, 0.6f, 40f);
        RiverRankUp(containerPort, hoverQuay, sonar, 0.6f, 40f);
        RiverRankUp(hoverQuay, voidMooring, voidTides, 0.6f, 40f);
        RiverRankUp(whalers, harpoon, steamPackets, 0.6f, 40f);
        RiverRankUp(trawler, harvester, sonar, 0.6f, 40f);
        RiverRankUp(harvester, voidTrawler, voidTrawling, 0.6f, 40f);
        RiverRankUp(kelp, lattice, plankton, 0.6f, 40f);
        RiverRankUp(stiltHuts, pierHouses, hanse, 0.6f, 30f);
        RiverRankUp(seaWall, stormBarrier, steamPackets, 0.8f, 6f);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art: buildings (only this setup's own) ----------
        foreach (var b in new[] { weir, pens, trawler, harvester, kelp, lattice, ranch, voidTrawler, whalers, harpoon, saltPans, pearlDivers,
                     beacon, lighthouse, lampLight, starSpire, wharf, cogHarbour, tradingPort, steamerDocks, containerPort, hoverQuay, voidMooring,
                     oilRig, tidalGen, stiltHuts, pierHouses, floatingHomes, seaDome })
            GenerateSeaLooks(b);
        seaWall.pieces = DisWallPieces(seaWall.id, new PxWallStyle { ramp = P2.SeaStone, height = 16, kind = 2 }, false);
        seaWall.icon = seaWall.pieces[3];
        EditorUtility.SetDirty(seaWall);
        stormBarrier.pieces = DisWallPieces(stormBarrier.id, new PxWallStyle { ramp = ConcreteRamp, height = 22, kind = 6 }, false);
        stormBarrier.icon = stormBarrier.pieces[3];
        EditorUtility.SetDirty(stormBarrier);
        AssetDatabase.SaveAssets();

        // ---------- Units: sea creatures that fight, and pirates ----------
        var arrow = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Arrow.png");
        var cannonball = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Cannonball.png");
        var spark = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/GalvanicSpark.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/RayBolt.png");
        var ray = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/RayBolt.png") ?? spark;
        UnitDefinition Unit(string id, string display, Age age, int hp, int atk, float cd, float range, float speed, Sprite shot, float shotSpeed,
            float arc, float splash, bool smoke)
        {
            string path = $"Assets/Data/Units/{id}.asset";
            var u = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (u == null)
            {
                Directory.CreateDirectory("Assets/Data/Units");
                u = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(u, path);
            }
            u.name = id;
            u.displayName = display;
            u.age = age;
            u.troopClass = naval;
            u.cost = 0;
            u.populationCost = 0;
            u.upkeep = new List<ResourceRate>();
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
        var serpentU = Unit("SeaSerpent", "Sea Serpent", classical, 900, 34, 1.4f, 2.2f, 1.8f, null, 0f, 0f, 0f, false);
        var krakenU = Unit("SeaKraken", "The Kraken", medieval, 7000, 120, 1.8f, 3f, 1.2f, null, 0f, 0f, 0f, false);
        var eelU = Unit("SeaVoidEel", "Void Eel", astral, 2400, 90, 1.2f, 2.2f, 2.6f, null, 0f, 0f, 0f, false);
        var leviathanU = Unit("SeaLeviathan", "The Leviathan", astral, 30000, 320, 2f, 3.5f, 1.4f, null, 0f, 0f, 0f, false);
        foreach (var beast in new[] { serpentU, krakenU, eelU, leviathanU }) { beast.troopClass = null; EditorUtility.SetDirty(beast); }   // beasts, not boats
        var pirateDefs = new List<UnitDefinition>
        {
            Unit("SeaPirateSloop", "Pirate Sloop", medieval, 520, 22, 1.5f, 7f, 1.9f, arrow, 12f, Projectile.BowArc, 0f, false),
            Unit("SeaPirateBrig", "Pirate Brig", renaissance, 1300, 60, 3f, 9f, 1.8f, cannonball, 16f, 0.06f, 1.2f, true),
            Unit("SeaPirateCorsair", "Steam Corsair", industrial, 2100, 100, 3f, 10f, 2.8f, cannonball, 18f, 0.05f, 1.4f, true),
            Unit("SeaPirateHoverRaider", "Hover-Raider", information, 3000, 140, 2.4f, 11f, 3.8f, spark, 24f, 0f, 1.1f, false),
            Unit("SeaPirateVoidReaver", "Void Reaver", astral, 4600, 210, 2.4f, 12f, 4.8f, ray, 26f, 0f, 1.2f, false),
        };
        int[] pirateArt = { 0, 2, 3, 5, 6 };
        WeaponClass[] pirateWeapon = { WeaponClass.Bow, WeaponClass.Cannon, WeaponClass.Cannon, WeaponClass.Cannon, WeaponClass.Cannon };
        for (int i = 0; i < pirateDefs.Count; i++)
        {
            int tier = pirateArt[i];
            var shape = SeaShipShape(tier);
            var frames = new List<PixelCanvas>();
            for (int f = 0; f < 12; f++)
            {
                if (f >= 9) { frames.Add(RiverSink(frames[0], shape.w / 2, shape.gy, f - 9)); continue; }
                bool moving = f >= 1 && f <= 4;
                frames.Add(SeaShipFrame(tier, moving ? f : 0, moving, true, f >= 5 ? f - 5 : -1));
            }
            var def = pirateDefs[i];
            def.prefab = BuildSeaUnit(def.name, frames, SeaPivot(shape), shape.gy, true, def, pirateWeapon[i]);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sea/Units/{def.name}/{def.name}_Idle.png");
            EditorUtility.SetDirty(def);
        }

        // ---------- The scene's library ----------
        const string libPath = "Assets/Resources/SeaLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<SeaLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SeaLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }
        float ppu = UnitPPU;
        ResetDir("Sea/Boats");
        lib.fishingBoats = new List<SeaLibrary.Look>();
        for (int kind = 0; kind < SeaCatalog.FishingBoatKinds; kind++)
        {
            var shape = SeaBoatShape(kind);
            var look = new SeaLibrary.Look { fps = 3f, smoke = kind == 2 ? new Vector2(-0.27f, 0.72f) : Vector2.zero };
            for (int f = 0; f < 6; f++) look.frames.Add(SavePixel(SeaFishingBoat(kind, f), $"Sea/Boats/Fishing{kind}_F{f}.png", ppu, SeaPivot(shape)));
            lib.fishingBoats.Add(look);
        }
        ResetDir("Sea/Ships");
        lib.merchantShips = new List<SeaLibrary.Look>();
        for (int tier = 0; tier < SeaCatalog.ShipTierCount; tier++)
        {
            var shape = SeaShipShape(tier);
            var look = new SeaLibrary.Look { fps = 3f, smoke = tier == 3 ? new Vector2(0f, 1.15f) : tier == 4 ? new Vector2(-1.28f, 1f) : Vector2.zero };
            for (int f = 0; f < 4; f++) look.frames.Add(SavePixel(SeaShipFrame(tier, f, f > 0, false), $"Sea/Ships/Ship{tier}_F{f}.png", ppu, SeaPivot(shape)));
            lib.merchantShips.Add(look);
        }
        ResetDir("Sea/Shoals");
        lib.shoalFish = new List<Sprite>();
        lib.shoalOysters = new List<Sprite>();
        for (int f = 0; f < 4; f++)
        {
            lib.shoalFish.Add(SavePixel(SeaShoalFrame(false, f), $"Sea/Shoals/Fish_F{f}.png", PixelsPerTile, new Vector2(0.5f, 0.5f)));
            lib.shoalOysters.Add(SavePixel(SeaShoalFrame(true, f), $"Sea/Shoals/Oysters_F{f}.png", PixelsPerTile, new Vector2(0.5f, 0.5f)));
        }
        lib.shoalBlob = SavePixel(SeaShoalBlob(false), "Sea/Shoals/FishBlob.png", PixelsPerTile, new Vector2(0.5f, 0.5f));
        lib.oysterBlob = SavePixel(SeaShoalBlob(true), "Sea/Shoals/OysterBlob.png", PixelsPerTile, new Vector2(0.5f, 0.5f));
        lib.creatures = new List<SeaLibrary.Creature>();
        var fighters = new Dictionary<string, UnitDefinition> { { "SeaSerpent", serpentU }, { "SeaKraken", krakenU }, { "SeaVoidEel", eelU }, { "SeaLeviathan", leviathanU } };
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };
        foreach (var sp in SeaCatalog.Species)
        {
            var shape = SeaCreatureShape(sp.id);
            var pivot = SeaPivot(shape);
            var canvases = new List<PixelCanvas>();
            for (int f = 0; f < 12; f++) canvases.Add(SeaCreatureFrame(sp.id, f));
            var entry = new SeaLibrary.Creature { species = sp.id };
            if (fighters.TryGetValue(sp.id, out var unit))
            {
                unit.prefab = BuildSeaUnit(sp.id, canvases, pivot, shape.gy, true, unit, WeaponClass.Melee);
                unit.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sea/Units/{sp.id}/{sp.id}_Idle.png");
                EditorUtility.SetDirty(unit);
                entry.unit = unit;
                for (int f = 0; f < 12; f++) entry.frames.Add(AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sea/Units/{sp.id}/{sp.id}_{names[f]}.png"));
            }
            else
            {
                string dir = $"Sea/Creatures/{sp.id}";
                ResetDir(dir);
                for (int f = 0; f < 12; f++) entry.frames.Add(SavePixel(canvases[f], $"{dir}/{sp.id}_{names[f]}.png", ppu, pivot));
            }
            lib.creatures.Add(entry);
        }
        lib.pirates = pirateDefs;
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("Sea set up: 31 buildings (fishing, whaling, ports, lighthouses, salt, pearls, oil, tidal power, sea walls, homes on the water), " +
                  "21 research, 6 sea creatures, 5 pirates' ships, fishing boats and merchant ships.");
    }

    /// <summary>Colours this setup needs that the palette doesn't have.</summary>
    private static class P2
    {
        public static Color32[] SeaStone => new[] { PixelPalette.Hex("3a4048"), PixelPalette.Hex("5e6670"), PixelPalette.Hex("8a929a"), PixelPalette.Hex("b8c0c4") };
    }

    /// <summary>A sea unit prefab (a creature or a pirates' ship) from its 12 frames, like BuildRiverBoat.</summary>
    private static GameObject BuildSeaUnit(string name, List<PixelCanvas> canvases, Vector2 pivot, int gy, bool isEnemy, UnitDefinition definition, WeaponClass weapon)
    {
        string dir = $"Sea/Units/{name}";
        ResetDir(dir);
        float ppu = UnitPPU;
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };
        var frames = new Sprite[names.Length];
        for (int f = 0; f < names.Length; f++) frames[f] = SavePixel(canvases[f], $"{dir}/{name}_{names[f]}.png", ppu, pivot);
        var idle = canvases[0];
        int top = gy, left = idle.W, right = 0;
        for (int y = 0; y < idle.H; y++)
            for (int x = 0; x < idle.W; x++)
                if (idle.Get(x, y).a > 200) { top = Mathf.Max(top, y); left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
        float height = (top - gy) / ppu, width = Mathf.Max(1, right - left) / ppu;

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
        visual.barWidth = Mathf.Clamp(width * 0.6f, 14f / ppu, 50f / ppu);
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(width * 0.5f, height) * 0.4f;
        col.offset = new Vector2(0f, height * 0.35f);
        go.AddComponent<MouseTouching>();
        Unit unit = isEnemy ? go.AddComponent<Enemy>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = Mathf.Clamp(width * 0.45f, 0.5f, 1.6f);
        if (unit is Enemy e) { e.lootPerHit = 0; e.lootCapacity = 999; }

        string prefabPath = $"Assets/Prefabs/Units/{name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
