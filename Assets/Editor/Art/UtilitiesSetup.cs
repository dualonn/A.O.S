using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using U = UtlCatalog;

/// <summary>
/// Utilities through the ages (see Utilities.cs and UtlCatalog.cs):
/// - Water: Cistern (Bronze), Aqueduct lines and Fountains (Classical), Water-wheel Pump
///   (Renaissance), Steam Pumping Station and Water Tower (Industrial), Purification Plant (Modern),
///   Atmospheric Water Extractor (Information), Font of Endless Water (Arcane), Matter Synthesiser (Galactic).
///   (The Stone Age well and the fire crews' pumps from Setup Disasters are water points too.)
/// - Power: Steam Dynamo House and Hydro Dam (Industrial), Coal Power Station, Oil-fired Plant and
///   Wind Turbine (Modern), Atomic Power Station (Electric), Solar Array (Information), Mana Reactor
///   (Arcane), Aether Reactor (Astral), Stellar Tap (Stellar), Dyson Collector (Interstellar), Vacuum
///   Engine (Universal); carried by Power Poles (ranking up into Steel Pylons), Substations, Tesla
///   Masts, Ley Obelisks and Beam Relays.
/// - Data: Data Relay Mast and Data Centre (Information), Mindweb Spire (Astral), Quantum Entanglement
///   Node (Interstellar), Noosphere Lattice (Universal).
/// - Clean air: Physic Garden (Renaissance), City Park (Industrial), Recycling Plant (Modern), Air
///   Scrubber Tower (Information), Sylvan Grove (Arcane), Atmospheric Terraformer (Stellar).
/// - Nine research items with small global bonuses (they don't count towards an age).
/// Creates or updates only its own assets and art, and is safe to run again.
/// (Tools > Age of Stickman > Setup Utilities.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Utilities")]
    public static void SetupUtilities()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Utilities: stop playing first.");
            return;
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age bronze = AgeNamed("BronzeAge"), classical = AgeNamed("ClassicalAge"), renaissance = AgeNamed("RenaissanceAge"),
            enlightenment = AgeNamed("EnlightenmentAge"), industrial = AgeNamed("IndustrialAge"), modern = AgeNamed("ModernAge"),
            electric = AgeNamed("ElectricAge"), information = AgeNamed("InformationAge"), arcane = AgeNamed("ArcaneAge"),
            astral = AgeNamed("AstralAge"), stellar = AgeNamed("StellarAge"), interstellar = AgeNamed("InterstellarAge"),
            galactic = AgeNamed("GalacticAge"), universal = AgeNamed("UniversalAge");
        if (bronze == null || classical == null || industrial == null || information == null)
        {
            Debug.LogError("Setup Utilities: the ages aren't set up yet.");
            return;
        }
        renaissance ??= classical; enlightenment ??= renaissance; modern ??= industrial; electric ??= modern;
        arcane ??= information; astral ??= arcane; stellar ??= astral; interstellar ??= stellar; galactic ??= interstellar; universal ??= galactic;

        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"), gold = Res(db, "Gold"),
            steel = Res(db, "Steel"), coin = Res(db, "Coin"), coal = Res(db, "Coal"), oil = Res(db, "Oil"), power = Res(db, "PowerCells"),
            chips = Res(db, "Microchips"), mana = Res(db, "Mana"), aether = Res(db, "Aether"), uranium = Res(db, "Uranium"),
            stardust = Res(db, "Stardust"), heliolite = Res(db, "Heliolite"), primordium = Res(db, "Primordium"), cores = Res(db, "AstralCores"),
            silicon = Res(db, "Silicon"), manastone = Res(db, "Manastone"), sunfire = Res(db, "Sunfire"), warp = Res(db, "WarpCells"),
            genesis = Res(db, "GenesisMatter"), quint = Res(db, "Quintessence"), essence = Res(db, "Essence"), tools = Res(db, "Tools");
        List<ResourceAmount> C(params object[] pairs)
        {
            // Like Cost(), but skips resources this game doesn't have.
            var list = new List<ResourceAmount>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            return list;
        }
        List<ResourceRate> In(ResourceType r, float perSecond) =>
            r != null ? new List<ResourceRate> { new ResourceRate { resource = r, perSecond = perSecond } } : new List<ResourceRate>();

        // ---------- Research (small global bonuses; worked out in Utilities.cs) ----------
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
        T(U.TechHydraulics, "Hydraulics", classical,
            "Levels, gradients and the siphon: fountains and cisterns reach 2 tiles further.", 70f, C(stone, 300, gold, 40));
        T(U.TechSanitation, "Sanitation", enlightenment,
            "Sewers, covered wells and the habit of boiling water: every water point serves 25% more people, and homes with water grow faster still.",
            110f, C(stone, 400, coin, 300));
        T(U.TechSmokeFilters, "Smoke Filters", industrial,
            "Baffles and water sprays in the flues catch the worst of the soot: pollution -15%.", 120f, C(steel, 150, coal, 100, coin, 200));
        T(U.TechAlternating, "Alternating Current", modern,
            "Transformers step the voltage up for the long lines and down for the works: every generator gives 10% more power.",
            130f, C(steel, 300, coin, 400));
        T(U.TechScrubbers, "Catalytic Scrubbers", electric,
            "Platinum honeycombs turn the fumes into steam and salt: pollution -15%.", 140f, C(steel, 300, power, 60, coin, 500));
        T(U.TechSmartGrid, "Smart Grid", information,
            "Meters that talk to the power stations, and demand that follows the supply: generators give 10% more, and pollution -10%.",
            150f, C(chips, 150, coin, 800));
        T(U.TechLeyAttune, "Ley Attunement", arcane,
            "The reactors are tuned to the ley lines' own rhythm: mana and aether reactors give 15% more power.", 150f, C(mana, 200, manastone, 60, gold, 200));
        T(U.TechAtmoEng, "Atmospheric Engineering", stellar,
            "Whole skies are balanced like an equation: pollution -25%.", 170f, C(heliolite, 100, cores, 40, steel, 500));
        T(U.TechQuantumNet, "Quantum Networking", interstellar,
            "Entangled pairs carry data with no delay at all: every network's compute +25%.", 180f, C(warp, 40, chips, 300, cores, 40));

        // ---------- Buildings ----------
        BuildingDefinition B(string id, string display, Age age, Vector2Int size, Vector2Int art, string desc, List<ResourceAmount> cost,
            int hp, int workers, float buildTime)
        {
            var b = Building(db, id, display, age, null, size, art, desc, cost, buildTime, hp, workers, null);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.runsWithoutInputs = 0f;
            b.upgrades = new List<BuildingUpgrade>();
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
            b.isTownCenter = false;
            if (tools != null && age != null && age.order >= 2 && age.order < 11 && size.x * size.y > 1) AddTools(b.requirements, tools);   // (not on lines and poles)
            EditorUtility.SetDirty(b);
            return b;
        }
        BuildingUpgrade Up(string name, string desc, List<ResourceAmount> cost, float output = 1.2f, float time = 25f) => new BuildingUpgrade
        {
            name = name, description = desc, requirements = new Requirements { cost = cost }, upgradeTime = time,
            productionMultiplier = output, trainedUnits = new UnitStatModifier(),
        };
        Vector2Int V(int x, int y) => new Vector2Int(x, y);
        var built = new List<BuildingDefinition>();
        BuildingDefinition Add(BuildingDefinition b) { built.Add(b); return b; }
        const string cleaner = " Each also cuts its pollution by 30%.";

        // Water
        Add(B(U.Cistern, "Cistern", bronze, V(2, 2), V(2, 2),
            "A plastered stone tank sunk into the ground, filled by the rain from roofs and gutters.", C(stone, 80, wood, 30), 300, 0, 15f));
        var aqueduct = Add(B(U.Aqueduct, "Aqueduct", classical, Vector2Int.one, Vector2Int.one,
            "A stone channel carried on arches, falling gently all the way from the river to the town. " +
            "Drag out a line from a river or lake (or a water mill or pump) to your fountains; people walk under it.",
            C(stone, 6), 400, 0, 3f));
        aqueduct.wallKind = WallTile.Conduit;
        aqueduct.canBuildOnHills = true;
        var fountain = Add(B(U.Fountain, "Fountain", classical, V(2, 2), V(2, 3),
            "A castellum where the aqueduct ends, sharing its water out to a basin and spouting bronze lions. Set it beside an aqueduct.",
            C(stone, 150, gold, 15), 500, 1, 20f));
        fountain.upgrades = new List<BuildingUpgrade> { Up("Lead Pipes", "Pipes to the bath house and the rich men's houses: 20% more people served.", C(stone, 100, iron, 30)) };
        var wheel = Add(B(U.Waterwheel, "Water-wheel Pump", renaissance, V(2, 2), V(2, 3),
            "A great undershot wheel turning in the stream, working a chain of buckets up into a tank. Fills an aqueduct it touches.",
            C(wood, 200, stone, 120, iron, 30), 700, 2, 25f));
        wheel.upgrades = new List<BuildingUpgrade> { Up("Second Wheel", "Another wheel and another chain of buckets.", C(wood, 150, iron, 40)) };
        var pump = Add(B(U.PumpStation, "Steam Pumping Station", industrial, V(3, 2), V(3, 3),
            "A brick engine house where a beam engine pumps from a bored well into the mains. Burns coal; fills an aqueduct it touches.",
            C(stone, 300, steel, 120, coal, 40), 1400, 3, 35f));
        pump.inputs = In(coal, 0.04f); pump.runsWithoutInputs = 0.4f;
        pump.upgrades = new List<BuildingUpgrade>
        {
            Up("Triple Expansion", "A bigger engine that wastes less steam.", C(steel, 150, coin, 200), 1.25f),
            Up("Second Bore", "Another well sunk beside the first.", C(stone, 200, steel, 100), 1.2f),
        };
        Add(B(U.WaterTower, "Water Tower", industrial, V(2, 2), V(2, 4),
            "A riveted iron tank on a brick tower, keeping the pressure up in the pipes of the quarter round it. Needs a pump on its mains.",
            C(steel, 120, stone, 100, wood, 60), 900, 0, 25f));
        var purifier = Add(B(U.Purifier, "Purification Plant", modern, V(3, 3), V(3, 3),
            "Settling tanks, sand filters and a chlorine house: clean water for the whole network it joins.",
            C(stone, 400, steel, 250, coin, 300), 2000, 4, 40f));
        purifier.upgrades = new List<BuildingUpgrade>
        {
            Up("Ozone Towers", "Ozone kills what chlorine misses.", C(steel, 200, coin, 300), 1.25f),
            Up("Membrane Filters", "Water so pure it tastes of nothing.", C(steel, 300, coin, 400), 1.2f),
        };
        Add(B(U.Extractor, "Atmospheric Water Extractor", information, V(2, 2), V(2, 3),
            "Chilled fins wring water out of the air itself: it works anywhere, even on dry worlds (less in thin air). Needs power.",
            C(steel, 200, chips, 40, power, 30), 1500, 1, 30f));
        var font = Add(B(U.Font, "Font of Endless Water", arcane, V(2, 2), V(2, 3),
            "A rune-carved basin that never empties, its water drawn from somewhere else entirely. Clean water anywhere; drinks a little mana.",
            C(stone, 300, mana, 80, gold, 60), 1800, 1, 30f));
        font.inputs = In(mana, 0.02f); font.runsWithoutInputs = 0.3f;
        Add(B(U.Synth, "Matter Synthesiser", galactic, V(3, 3), V(3, 4),
            "Water assembled molecule by molecule from raw energy, for a whole city and any world at all. Needs a great deal of power.",
            C(steel, 600, primordium, 60, cores, 30, power, 200), 5000, 3, 50f));

        // Power: generators
        var dynamo = Add(B(U.SteamDynamo, "Steam Dynamo House", industrial, V(3, 3), V(3, 4),
            "A boiler house and a roaring dynamo under a tall chimney: the first electric light. Burns coal; needs cooling water close by.",
            C(stone, 300, steel, 150, wood, 100), 1500, 4, 40f));
        dynamo.inputs = In(coal, 0.12f);
        dynamo.upgrades = new List<BuildingUpgrade>
        {
            Up("Compound Engine", "Steam used twice over: 20% more power." + cleaner, C(steel, 150, coin, 200)),
            Up("Feedwater Heater", "Hot water into the boiler: 20% more power." + cleaner, C(steel, 200, coin, 300)),
        };
        var dam = Add(B(U.HydroDam, "Hydro Dam", industrial, V(3, 2), V(3, 3),
            "A stone weir across the stream and a turbine hall on the bank: clean power, the more water round it the more. Build it on a riverbank.",
            C(stone, 600, steel, 150), 2500, 2, 50f));
        dam.upgrades = new List<BuildingUpgrade> { Up("Second Turbine", "Another turbine in the race: 20% more power.", C(steel, 200, stone, 200)) };
        var coalPlant = Add(B(U.CoalPlant, "Coal Power Station", modern, V(4, 3), V(4, 5),
            "Cooling towers, a turbine hall and a coal yard: a lot of power and a lot of smoke. Burns coal; needs cooling water.",
            C(stone, 600, steel, 400, coin, 400), 2500, 6, 50f));
        coalPlant.inputs = In(coal, 0.25f);
        coalPlant.upgrades = new List<BuildingUpgrade>
        {
            Up("Electrostatic Precipitators", "Charged plates catch the ash: 10% more power." + cleaner, C(steel, 300, coin, 400), 1.1f),
            Up("Supercritical Boilers", "Steam hotter than boiling: 20% more power." + cleaner, C(steel, 400, coin, 500)),
        };
        var oilPlant = Add(B(U.OilPlant, "Oil-fired Power Plant", modern, V(3, 3), V(3, 4),
            "Fuel tanks, a boiler and a striped chimney. Burns oil; needs cooling water.", C(steel, 400, stone, 300, coin, 300), 2200, 4, 45f));
        oilPlant.inputs = In(oil, 0.15f);
        oilPlant.upgrades = new List<BuildingUpgrade>
        {
            Up("Gas Turbines", "Jet engines bolted to the ground: 20% more power." + cleaner, C(steel, 300, coin, 400)),
            Up("Combined Cycle", "The turbines' heat raises steam too: 20% more power." + cleaner, C(steel, 400, coin, 500)),
        };
        var wind = Add(B(U.WindTurbine, "Wind Turbine", modern, Vector2Int.one, V(1, 3),
            "A tall white mast and three long blades. Clean, small, and best on hills and stormy worlds (weak in thin air).",
            C(steel, 60, coin, 40), 500, 0, 15f));
        wind.canBuildOnHills = true;
        var nuclear = Add(B(U.NuclearPlant, "Atomic Power Station", electric, V(4, 4), V(4, 5),
            "A chrome reactor dome, glowing blue, between two cooling towers: enormous power from a little uranium. Needs cooling water.",
            C(steel, 900, stone, 800, power, 200, coin, 800), 5000, 8, 70f));
        nuclear.inputs = In(uranium, 0.04f);
        nuclear.upgrades = new List<BuildingUpgrade>
        {
            Up("Breeder Core", "It makes some of its own fuel: 20% more power.", C(steel, 400, uranium, 60, coin, 600)),
            Up("Containment Field", "A galvanic field round the core: 20% more power.", C(power, 200, coin, 800)),
        };
        var solar = Add(B(U.SolarFarm, "Solar Array", information, V(3, 3), V(3, 3),
            "Rows of dark glass panels turning to follow the sun. Clean power; weaker on worlds of long nights, stronger on scorching ones.",
            C(steel, 200, silicon ?? steel, 120, chips, 60), 1200, 1, 30f));
        solar.canBuildOnHills = true;
        solar.upgrades = new List<BuildingUpgrade> { Up("Tracking Mounts", "Panels that follow the sun all day: 20% more power.", C(steel, 150, chips, 40)) };
        var manaR = Add(B(U.ManaReactor, "Mana Reactor", arcane, V(3, 3), V(3, 4),
            "A ring of runestones round a blazing crystal, wound in copper: mana turned into lightning.",
            C(stone, 500, mana, 200, manastone, 80, gold, 150), 3000, 4, 50f));
        manaR.inputs = In(mana, 0.06f);
        manaR.upgrades = new List<BuildingUpgrade> { Up("Second Crystal", "A twin crystal in the ring: 20% more power.", C(manastone, 80, mana, 150)) };
        var aetherR = Add(B(U.AetherReactor, "Aether Reactor", astral, V(3, 3), V(3, 5),
            "A spire where a bead of aether burns inside spinning golden rings.", C(steel, 600, aether, 150, cores, 40), 4000, 4, 55f));
        aetherR.inputs = In(aether, 0.05f);
        aetherR.upgrades = new List<BuildingUpgrade> { Up("Harmonic Rings", "Another ring, tuned a fifth higher: 20% more power.", C(aether, 100, cores, 30)) };
        var tap = Add(B(U.StellarTap, "Stellar Tap", stellar, V(3, 3), V(3, 5),
            "A mirrored dish that catches a beam sent down from collectors circling the sun. No fuel at all; weaker under long nights.",
            C(steel, 900, heliolite, 120, sunfire, 60, cores, 60), 5000, 3, 60f));
        tap.upgrades = new List<BuildingUpgrade> { Up("Second Beam", "Another collector in orbit: 20% more power.", C(heliolite, 100, cores, 40)) };
        var dyson = Add(B(U.DysonCollector, "Dyson Collector", interstellar, V(4, 4), V(4, 5),
            "The ground station of a swarm of mirrors round the star: a pillar of light and a ring of receivers. Burns a little stardust to focus it.",
            C(steel, 1500, stardust, 200, warp, 40, cores, 100), 7000, 4, 70f));
        dyson.inputs = In(stardust, 0.03f); dyson.runsWithoutInputs = 0.5f;
        dyson.upgrades = new List<BuildingUpgrade> { Up("Denser Swarm", "A million more mirrors: 20% more power.", C(stardust, 200, cores, 60)) };
        var vacuum = Add(B(U.VoidEngine, "Vacuum Engine", universal, V(3, 3), V(3, 5),
            "A black sphere in a cage of light, drawing power out of empty space itself. Feeds on a trickle of primordium.",
            C(primordium, 200, genesis, 50, quint, 80, cores, 150), 10000, 5, 80f));
        vacuum.inputs = In(primordium, 0.02f); vacuum.runsWithoutInputs = 0.4f;
        vacuum.upgrades = new List<BuildingUpgrade> { Up("Second Singularity", "A twin sphere, out of phase: 20% more power.", C(primordium, 150, quint, 60)) };

        // Power: carrying it
        var pole = Add(B(U.PowerPole, "Power Pole", industrial, Vector2Int.one, V(1, 2),
            "A creosoted wooden pole with a crossarm and glass insulators. Cables hang to the next pole.", C(wood, 8, iron, 2), 120, 0, 3f));
        pole.canBuildOnHills = true;
        var pylon = Add(B(U.Pylon, "Steel Pylon", modern, Vector2Int.one, V(1, 3),
            "A lattice tower carrying the high-voltage lines over field and river.", C(steel, 10), 300, 0, 4f));
        pylon.canBuildOnHills = true;
        RiverRankUp(pole, pylon, null, 0.8f, 4f);
        Add(B(U.Substation, "Substation", modern, V(2, 2), V(2, 2),
            "Transformers humming behind a fence: they power a whole quarter round them.", C(steel, 150, stone, 100, coin, 100), 900, 0, 20f));
        var tesla = Add(B(U.TeslaMast, "Tesla Mast", electric, Vector2Int.one, V(1, 3),
            "A copper mast topped with a crackling sphere: power sent through the air, no cables needed.", C(steel, 40, power, 10, coin, 40), 600, 0, 10f));
        tesla.canBuildOnHills = true;
        var obelisk = Add(B(U.LeyObelisk, "Ley Obelisk", arcane, Vector2Int.one, V(1, 3),
            "A rune-cut standing stone that sings power along the ley lines to its kin.", C(stone, 60, manastone, 15, mana, 20), 900, 0, 12f));
        obelisk.canBuildOnHills = true;
        Add(B(U.BeamRelay, "Beam Relay", stellar, V(2, 2), V(2, 3),
            "A crystal lens on a slender tower, passing power on in a pencil of light.", C(steel, 200, heliolite, 40, cores, 10), 2000, 0, 25f));

        // Data
        var relay = Add(B(U.DataRelay, "Data Relay Mast", information, Vector2Int.one, V(1, 3),
            "A mast of antennas and microwave dishes blinking red at the top.", C(steel, 40, chips, 10), 500, 0, 10f));
        relay.canBuildOnHills = true;
        var centre = Add(B(U.DataCentre, "Data Centre", information, V(3, 2), V(3, 3),
            "A windowless hall of humming server racks and chillers on the roof. Needs a lot of power.",
            C(steel, 400, chips, 120, power, 60, coin, 400), 2000, 4, 40f));
        centre.upgrades = new List<BuildingUpgrade>
        {
            Up("Liquid Cooling", "Racks packed twice as tight: 25% more compute.", C(chips, 100, steel, 150), 1.25f),
            Up("Optical Backbone", "Light instead of copper: 25% more compute.", C(chips, 150, coin, 400), 1.25f),
        };
        Add(B(U.MindwebSpire, "Mindweb Spire", astral, V(2, 2), V(2, 4),
            "A spire of violet crystal where seers sit linked mind to mind, thinking as one.", C(stone, 300, mana, 150, cores, 30, aether, 60), 2500, 2, 40f));
        Add(B(U.QuantumNode, "Quantum Entanglement Node", interstellar, V(2, 2), V(2, 3),
            "A frosted vacuum chamber holding entangled atoms: data anywhere in reach at once.", C(steel, 600, warp, 30, chips, 200, cores, 40), 3000, 2, 45f));
        var noo = Add(B(U.Noosphere, "Noosphere Lattice", universal, V(3, 3), V(3, 5),
            "A lattice of light rising into the sky that links every mind and machine on the world.", C(quint, 120, primordium, 120, cores, 120), 8000, 3, 70f));
        noo.maxCount = 1;

        // Clean air
        Add(B(U.Garden, "Physic Garden", renaissance, V(2, 2), V(2, 2),
            "Walled beds of herbs and fruit trees round a sundial: sweet air in the middle of town.", C(wood, 60, stone, 60, food, 40), 300, 1, 15f));
        var park = Add(B(U.CityPark, "City Park", industrial, V(3, 3), V(3, 3),
            "Lawns, plane trees and a bandstand: the lungs of the industrial city.", C(wood, 120, stone, 100, coin, 100), 400, 1, 20f));
        park.upgrades = new List<BuildingUpgrade> { Up("Boating Lake", "A lake and more trees round it: it cleans 20% more air.", C(stone, 100, coin, 150)) };
        Add(B(U.Recycler, "Recycling Plant", modern, V(3, 2), V(3, 3),
            "Sorting belts, balers and a furnace that burns clean: the works round it give off a third less pollution.",
            C(steel, 300, stone, 200, coin, 300), 1600, 4, 35f));
        var scrub = Add(B(U.Scrubber, "Air Scrubber Tower", information, V(2, 2), V(2, 4),
            "A tall tower of fans and filter beds drawing the smog in and breathing clean air out. Needs power.",
            C(steel, 250, chips, 40, power, 30), 1500, 1, 30f));
        scrub.upgrades = new List<BuildingUpgrade> { Up("Second Fan Bank", "More fans, bigger filters: it cleans 20% more air.", C(steel, 150, chips, 30)) };
        Add(B(U.SylvanGrove, "Sylvan Grove", arcane, V(2, 2), V(2, 3),
            "Enchanted trees with silver leaves round a dryad's pool: they drink in foul air by the barrel.", C(wood, 200, mana, 60, essence, 20), 800, 1, 20f));
        Add(B(U.Terraformer, "Atmospheric Terraformer", stellar, V(3, 3), V(3, 5),
            "A towering engine that cleans the sky for miles and, on toxic worlds, slowly makes the air fit to breathe (people grow faster there).",
            C(steel, 900, heliolite, 100, cores, 80, power, 200), 5000, 4, 60f));

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only this setup's own) ----------
        foreach (var b in built)
        {
            if (b == aqueduct) continue;
            GenerateUtlLooks(b);
        }
        aqueduct.pieces = UtlAqueductPieces(aqueduct.id);
        aqueduct.icon = aqueduct.pieces[2 | 8];
        aqueduct.looks = new List<BuildingLook>();
        EditorUtility.SetDirty(aqueduct);
        AssetDatabase.SaveAssets();
        Debug.Log($"Utilities set up: {built.Count} buildings (water, power, data, clean air) and {U.Techs.Length} research items.");
    }
}
