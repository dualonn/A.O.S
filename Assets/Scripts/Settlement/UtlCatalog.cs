using System.Collections.Generic;
using UnityEngine;

/// <summary>What a utility building does in its network (see Utilities.cs).</summary>
public enum UtlRole
{
    None,
    /// <summary>Gives water to people round it (wells, cisterns, pumps, purifiers, extractors...). amount = people served.</summary>
    Water,
    /// <summary>Hands out water from the mains it is linked to (water towers). No water of its own.</summary>
    WaterNode,
    /// <summary>An aqueduct tile: a line from a river or lake, feeding the fountains along it.</summary>
    Aqueduct,
    /// <summary>A fountain: a big source while it touches a fed aqueduct, a little basin otherwise.</summary>
    Fountain,
    /// <summary>Makes power for the grid. amount = power.</summary>
    Generator,
    /// <summary>Carries power: poles, pylons, substations, wireless masts.</summary>
    PowerNode,
    /// <summary>Data: relays and computers. amount = compute.</summary>
    Data,
    /// <summary>Takes pollution out of the air round it. amount = pollution removed in its cell.</summary>
    Absorber,
    /// <summary>Cuts the pollution of every source within 'cover' tiles. amount = the share left (0.65).</summary>
    Recycler,
}

/// <summary>Numbers for one utility building (or an existing building that also takes part).</summary>
public class UtlSpec
{
    public UtlRole role;
    /// <summary>Tiles round it that it serves (water, power or data); for absorbers, cells round it (0 = its own cell).</summary>
    public int cover;
    /// <summary>How far (tiles) it links to the next node of its network (0 = doesn't link).</summary>
    public int link;
    /// <summary>People served (water), power made (generators), compute (data), pollution absorbed (absorbers).</summary>
    public float amount;
    /// <summary>Pollution it gives off at full work.</summary>
    public float pollution;
    /// <summary>Power it needs (0 = the usual for its age; -1 = none).</summary>
    public int demand;
    /// <summary>Must be built with water (river, lake, sea) within this many tiles (0 = anywhere).</summary>
    public int waterReach;
    /// <summary>Links through the air, to anything in reach (wired nodes link only to nodes both can reach).</summary>
    public bool wireless;
    /// <summary>Clean water: its whole network counts as purified.</summary>
    public bool clean;
    /// <summary>Fills an aqueduct it touches (water mills).</summary>
    public bool feeds;
    /// <summary>Steam plants: they need cooling water (a river close by, or a water supply) for full output.</summary>
    public bool cooling;
    /// <summary>Joins the underground water mains (pumps, towers, purifiers).</summary>
    public bool mains;
    /// <summary>Part of an older system: not built by Setup Utilities (wells, reactors, data banks).</summary>
    public bool existing;

    public UtlSpec(UtlRole role, int cover, int link, float amount, float pollution = 0f, int demand = 0)
    {
        this.role = role; this.cover = cover; this.link = link; this.amount = amount; this.pollution = pollution; this.demand = demand;
    }
}

/// <summary>
/// Utilities through the ages: water (wells and cisterns, Roman aqueducts and fountains, water mills,
/// steam pumps and water towers, purification, atmospheric extractors, arcane fonts and matter
/// synthesis), the power grid (steam dynamos and dams to fusion, mana reactors, stellar taps and
/// vacuum engines, carried by poles, pylons, substations and wireless masts), data networks
/// (relays, data centres, psionic and quantum networks) and the pollution industry makes (parks,
/// scrubbers, recycling and terraformers against it). Every number lives here; ids start "Utl".
/// </summary>
public static class UtlCatalog
{
    // ---------- Buildings (Setup Utilities) ----------
    public const string Cistern = "UtlCistern", Aqueduct = "UtlAqueduct", Fountain = "UtlFountain", Waterwheel = "UtlWaterwheel",
        PumpStation = "UtlPumpStation", WaterTower = "UtlWaterTower", Purifier = "UtlPurifier", Extractor = "UtlAtmoExtractor",
        Font = "UtlFontOfWaters", Synth = "UtlMatterSynth";

    public const string SteamDynamo = "UtlSteamDynamo", HydroDam = "UtlHydroDam", CoalPlant = "UtlCoalPlant", OilPlant = "UtlOilPlant",
        WindTurbine = "UtlWindTurbine", NuclearPlant = "UtlNuclearPlant", SolarFarm = "UtlSolarFarm", ManaReactor = "UtlManaReactor",
        AetherReactor = "UtlAetherReactor", StellarTap = "UtlStellarTap", DysonCollector = "UtlDysonCollector", VoidEngine = "UtlVoidEngine";

    public const string PowerPole = "UtlPowerPole", Pylon = "UtlPylon", Substation = "UtlSubstation", TeslaMast = "UtlTeslaMast",
        LeyObelisk = "UtlLeyObelisk", BeamRelay = "UtlBeamRelay";

    public const string DataRelay = "UtlDataRelay", DataCentre = "UtlDataCentre", MindwebSpire = "UtlMindwebSpire",
        QuantumNode = "UtlQuantumNode", Noosphere = "UtlNoosphere";

    public const string Garden = "UtlGarden", CityPark = "UtlCityPark", Recycler = "UtlRecycler", Scrubber = "UtlScrubber",
        SylvanGrove = "UtlSylvanGrove", Terraformer = "UtlTerraformer";

    // ---------- Research (small global bonuses; they don't count towards an age) ----------
    public const string TechHydraulics = "UtlHydraulics", TechSanitation = "UtlSanitation", TechSmokeFilters = "UtlSmokeFilters",
        TechAlternating = "UtlAlternatingCurrent", TechScrubbers = "UtlCatalyticScrubbers", TechSmartGrid = "UtlSmartGrid",
        TechLeyAttune = "UtlLeyAttunement", TechAtmoEng = "UtlAtmosphericEngineering", TechQuantumNet = "UtlQuantumNetworking";

    public static readonly string[] Techs =
        { TechHydraulics, TechSanitation, TechSmokeFilters, TechAlternating, TechScrubbers, TechSmartGrid, TechLeyAttune, TechAtmoEng, TechQuantumNet };

    /// <summary>Generators' own links and reach.</summary>
    public const int GeneratorCover = 6, GeneratorLink = 9;

    /// <summary>Water mains: pumps, towers and purifiers this far apart are joined underground.</summary>
    public const int MainsLink = 18;

    /// <summary>Fountains on a fed aqueduct, and the aqueduct's own water (plus each mill feeding it).</summary>
    public const int FountainFedCover = 11, FountainBasinCover = 4;
    public const float FountainBasinPeople = 30f, AqueductPeople = 400f, MillFeedPeople = 300f;

    /// <summary>Power: unpowered Electric Age (and later) works run this much slower; powered ones faster.</summary>
    public const float UnpoweredFactor = 0.85f, PoweredBonus = 0.10f;

    /// <summary>Old saves: seconds of play before the unpowered penalty, smog and thirst start biting.</summary>
    public const float LegacyGrace = 3600f;

    /// <summary>Pollution grid cell size (tiles).</summary>
    public const int Cell = 8;

    public const int IndustrialAge = 7, ModernAge = 8, ElectricAge = 9, InformationAge = 10;

    public static readonly Dictionary<string, UtlSpec> Specs = new Dictionary<string, UtlSpec>
    {
        // Water. amount = people served.
        { Cistern, new UtlSpec(UtlRole.Water, 7, 0, 70f) },
        { Aqueduct, new UtlSpec(UtlRole.Aqueduct, 0, 0, 0f, 0f, -1) },
        { Fountain, new UtlSpec(UtlRole.Fountain, FountainFedCover, 0, 180f) },
        { Waterwheel, new UtlSpec(UtlRole.Water, 9, 0, 300f) { waterReach = 1, feeds = true } },
        { PumpStation, new UtlSpec(UtlRole.Water, 5, MainsLink, 900f, 2f) { mains = true, feeds = true } },
        { WaterTower, new UtlSpec(UtlRole.WaterNode, 13, MainsLink, 0f) { mains = true, demand = -1 } },
        { Purifier, new UtlSpec(UtlRole.Water, 6, MainsLink, 1500f, 0f, 6) { mains = true, clean = true } },
        { Extractor, new UtlSpec(UtlRole.Water, 12, MainsLink, 500f, 0f, 8) { mains = true, clean = true } },
        { Font, new UtlSpec(UtlRole.Water, 15, MainsLink, 1200f, 0f, -1) { mains = true, clean = true } },
        { Synth, new UtlSpec(UtlRole.Water, 24, MainsLink * 2, 6000f, 0f, 30) { mains = true, clean = true } },
        // ...and the disaster crews' wells and pumps (Setup Disasters) are small water sources too.
        { DisCatalog.Well, new UtlSpec(UtlRole.Water, 6, 0, 40f, 0f, -1) { existing = true } },
        { DisCatalog.FireWatch, new UtlSpec(UtlRole.Water, 8, 0, 60f, 0f, -1) { existing = true } },
        { DisCatalog.FireStation, new UtlSpec(UtlRole.Water, 10, 0, 100f) { existing = true } },
        { DisCatalog.FireDrones, new UtlSpec(UtlRole.Water, 12, 0, 150f) { existing = true } },

        // Power. amount = power made at full work.
        { SteamDynamo, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 50f, 6f, -1) { cooling = true } },
        { HydroDam, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 70f, 0f, -1) { waterReach = 1 } },
        { CoalPlant, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 160f, 10f, -1) { cooling = true } },
        { OilPlant, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 130f, 7f, -1) { cooling = true } },
        { WindTurbine, new UtlSpec(UtlRole.Generator, 3, GeneratorLink, 15f, 0f, -1) },
        { NuclearPlant, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 450f, 1.5f, -1) { cooling = true } },
        { SolarFarm, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 60f, 0f, -1) },
        { ManaReactor, new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 500f, 0.5f, -1) },
        { AetherReactor, new UtlSpec(UtlRole.Generator, GeneratorCover + 2, GeneratorLink + 4, 800f, 0f, -1) },
        { StellarTap, new UtlSpec(UtlRole.Generator, GeneratorCover + 2, GeneratorLink + 8, 1200f, 0f, -1) },
        { DysonCollector, new UtlSpec(UtlRole.Generator, GeneratorCover + 4, GeneratorLink + 12, 2200f, 0f, -1) },
        { VoidEngine, new UtlSpec(UtlRole.Generator, GeneratorCover + 4, GeneratorLink + 16, 4000f, 0f, -1) },
        // ...and the reactors already in the game feed the grid as well as making power cells.
        { "AtomicReactor", new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 120f, 1f, -1) { existing = true } },
        { "SeaTidalGenerator", new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 45f, 0f, -1) { existing = true } },   // the sea's tidal generators
        { "FusionReactor", new UtlSpec(UtlRole.Generator, GeneratorCover, GeneratorLink, 220f, 0f, -1) { existing = true } },
        { "AstralReactor", new UtlSpec(UtlRole.Generator, GeneratorCover + 2, GeneratorLink + 4, 300f, 0f, -1) { existing = true } },
        { "SolarCollector", new UtlSpec(UtlRole.Generator, GeneratorCover + 2, GeneratorLink + 8, 380f, 0f, -1) { existing = true } },
        { "RiftTap", new UtlSpec(UtlRole.Generator, GeneratorCover + 2, GeneratorLink + 8, 500f, 0f, -1) { existing = true } },
        { "DysonRelay", new UtlSpec(UtlRole.Generator, GeneratorCover + 4, GeneratorLink + 12, 700f, 0f, -1) { existing = true } },
        { "ZeroPointTap", new UtlSpec(UtlRole.Generator, GeneratorCover + 4, GeneratorLink + 16, 1000f, 0f, -1) { existing = true } },

        // Carrying power.
        { PowerPole, new UtlSpec(UtlRole.PowerNode, 4, 7, 0f, 0f, -1) },
        { Pylon, new UtlSpec(UtlRole.PowerNode, 5, 13, 0f, 0f, -1) },
        { Substation, new UtlSpec(UtlRole.PowerNode, 11, 10, 0f, 0f, -1) },
        { TeslaMast, new UtlSpec(UtlRole.PowerNode, 8, 22, 0f, 0f, -1) { wireless = true } },
        { LeyObelisk, new UtlSpec(UtlRole.PowerNode, 12, 30, 0f, 0f, -1) { wireless = true } },
        { BeamRelay, new UtlSpec(UtlRole.PowerNode, 16, 48, 0f, 0f, -1) { wireless = true } },

        // Data. amount = compute.
        { DataRelay, new UtlSpec(UtlRole.Data, 9, 20, 0f, 0f, 2) { wireless = true } },
        { DataCentre, new UtlSpec(UtlRole.Data, 10, 20, 3f, 0.5f, 20) { wireless = true } },
        { MindwebSpire, new UtlSpec(UtlRole.Data, 14, 40, 4f, 0f, 10) { wireless = true } },
        { QuantumNode, new UtlSpec(UtlRole.Data, 16, 80, 6f, 0f, 25) { wireless = true } },
        { Noosphere, new UtlSpec(UtlRole.Data, 30, 100000, 12f, 0f, 40) { wireless = true } },
        { "DataBank", new UtlSpec(UtlRole.Data, 6, 12, 1f) { existing = true, wireless = true } },
        { "DataArk", new UtlSpec(UtlRole.Data, 12, 16, 6f) { existing = true, wireless = true } },
        { "SmartTower", new UtlSpec(UtlRole.Data, 8, 16, 0.5f) { existing = true, wireless = true } },
        { "OrbitalUplink", new UtlSpec(UtlRole.Data, 4, 60, 0.5f) { existing = true, wireless = true } },

        // Against pollution. amount = taken out of its cell (half that from the cells beside it).
        { Garden, new UtlSpec(UtlRole.Absorber, 0, 0, 3f, 0f, -1) },
        { CityPark, new UtlSpec(UtlRole.Absorber, 1, 0, 7f, 0f, -1) },
        { Recycler, new UtlSpec(UtlRole.Recycler, 14, 0, 0.65f, 0.5f, 6) },
        { Scrubber, new UtlSpec(UtlRole.Absorber, 1, 0, 14f, 0f, 8) },
        { SylvanGrove, new UtlSpec(UtlRole.Absorber, 1, 0, 12f, 0f, -1) },
        { Terraformer, new UtlSpec(UtlRole.Absorber, 3, 0, 30f, 0f, 40) },
    };

    public static bool TryGet(string id, out UtlSpec spec)
    {
        spec = null;
        return id != null && Specs.TryGetValue(id, out spec);
    }

    /// <summary>One of this system's own buildings (they stay out of the age progress, like walls).</summary>
    public static bool IsUtility(BuildingDefinition def) => def != null && def.id != null && def.id.StartsWith("Utl");

    public static bool IsUtlTech(string id) => id != null && id.StartsWith("Utl");

    public static bool IsConduit(BuildingDefinition def) => def != null && def.wallKind == WallTile.Conduit;

    /// <summary>Which overlay goes with a building (for the overlay shown while placing it): 1 water, 2 power, 3 data, 4 pollution, 0 none.</summary>
    public static int OverlayFor(BuildingDefinition def)
    {
        if (def == null) return 0;
        if (!TryGet(def.id, out var s)) return 0;
        switch (s.role)
        {
            case UtlRole.Water: case UtlRole.WaterNode: case UtlRole.Aqueduct: case UtlRole.Fountain: return 1;
            case UtlRole.Generator: case UtlRole.PowerNode: return 2;
            case UtlRole.Data: return 3;
            default: return 4;
        }
    }

    private static int AgeOf(BuildingDefinition def) => def != null && def.age != null ? def.age.order : 0;

    private static readonly Dictionary<string, int> demandCache = new Dictionary<string, int>();
    private static readonly Dictionary<string, float> pollutionCache = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        demandCache.Clear();
        pollutionCache.Clear();
    }

    /// <summary>
    /// Power a building draws from the grid: nothing before the Industrial Age; works more the later
    /// their age (and the bigger they are), homes half as much, walls and roads nothing.
    /// </summary>
    public static int Demand(BuildingDefinition def)
    {
        if (def == null) return 0;
        if (demandCache.TryGetValue(def.id, out int cached)) return cached;
        int d;
        if (TryGet(def.id, out var spec) && spec.demand != 0) d = Mathf.Max(0, spec.demand);
        else if (spec != null && (spec.role == UtlRole.Generator || spec.role == UtlRole.PowerNode)) d = 0;
        else
        {
            int age = AgeOf(def);
            if (def.IsWall || age < IndustrialAge || def.isTownCenter && age < ModernAge) d = 0;
            else
            {
                float baseDemand = age == IndustrialAge ? 2f : age == ModernAge ? 3f : 5f + (age - ElectricAge);
                float area = def.size.x * def.size.y;
                float size = Mathf.Clamp(0.6f + area / 12f, 0.6f, 2f);
                bool home = def.housing > 0 && def.production.Count == 0;
                if (home && age < ModernAge) d = 0;
                else d = Mathf.Max(1, Mathf.RoundToInt(baseDemand * size * (home ? 0.5f : 1f)));
            }
        }
        demandCache[def.id] = d;
        return d;
    }

    /// <summary>Does the building take the unpowered penalty (Electric Age and later), or only gain from power?</summary>
    public static bool NeedsPower(BuildingDefinition def) => AgeOf(def) >= ElectricAge;

    /// <summary>
    /// Pollution a building gives off at full work: its own number if it's a utility, otherwise
    /// worked out from what it does: mines and quarries, burning coal, charcoal and oil, smelting
    /// and refining. Pre-industrial works pollute a little; arcane and space-age works half as much.
    /// </summary>
    public static float PollutionOf(BuildingDefinition def)
    {
        if (def == null) return 0f;
        if (pollutionCache.TryGetValue(def.id, out float cached)) return cached;
        float p = 0f;
        if (TryGet(def.id, out var spec)) p = spec.pollution;
        else if (!def.IsWall && (def.production.Count > 0 || def.inputs.Count > 0))
        {
            int age = AgeOf(def);
            bool early = age < IndustrialAge;
            var dep = def.requiredDeposit;
            if (dep != null && dep.id != "Forest" && dep.id != "Berries") p += early ? 0.4f : 1.8f;
            foreach (var input in def.inputs)
            {
                string id = input.resource != null ? input.resource.id : "";
                if (id == "Coal" || id == "Charcoal") p += early ? 1.1f : 3.5f;
                else if (id == "Oil" || id == "Fuel") p += 3f;
                else if (id == "Uranium") p += 1f;
            }
            foreach (var rate in def.production)
            {
                string id = rate.resource != null ? rate.resource.id : "";
                if (id == "Steel" || id == "Gunpowder" || id == "Microchips" || id == "Fuel" || id == "Oil") p += early ? 0.6f : 1.6f;
                else if (id == "Iron" || id == "Bronze" || id == "Charcoal" || id == "Coal") p += early ? 0.4f : 1f;
            }
            if (age >= 11) p *= 0.5f;   // arcane and space-age works run clean
            p = Mathf.Clamp(p, 0f, 8f);
        }
        pollutionCache[def.id] = p;
        return p;
    }

    /// <summary>The words for how bad the air is.</summary>
    public static string SmogWord(float p) =>
        p < 3f ? "clear" : p < 8f ? "hazy" : p < 16f ? "smoggy" : p < 28f ? "choking" : "toxic";
}
