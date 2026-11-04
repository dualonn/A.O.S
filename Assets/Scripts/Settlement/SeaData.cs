using System;
using System.Collections.Generic;
using UnityEngine;

// ===================================================================================================
// The sea economy: fishing grounds, sea life, coastal trade and the buildings of the shore.
//
// - Fish shoals (and oyster beds) are map features on the water: rich spots that drift about, are
//   fished down by the boats that work them and regrow when left alone. A fished-out shoal moves on,
//   and new ones turn up as you explore, so the sea never runs dry for good (like the ores).
// - Every fishing building sends visible boats out to the shoals in its reach and back (on the world
//   you're looking at); its catch rises with the fish it finds.
// - Whales, sea serpents and krakens (and alien rays, void eels and leviathans) swim the open sea.
//   Whalers hunt them; serpents and monsters attack boats and ships; a kraken is a rare boss.
// - Ports in two towns open a sea route: merchant ships (round ships, cogs, galleons, steamships,
//   container ships, hover-barges and void barges) sail to and fro. Pirates prey on them; warships
//   and lighthouses keep them safe.
// - Salt pans, pearl divers, lighthouses, oil rigs, tidal generators, sea walls, and stilt and
//   floating homes that make ocean worlds a place to live.
// Data assets come from Tools > Age of Stickman > Setup Sea; the per-world state is WorldState.sea.
// ===================================================================================================

/// <summary>What a sea building does.</summary>
public enum SeaRole { None, Fishing, Whaling, Port, Light, Salt, Pearl, Oil, Tidal, Housing, Kelp, Ranch, Wall }

/// <summary>Where a sea building may stand.</summary>
public enum SeaSite
{
    River,      // the rivers-and-sea setup's own rules (fishing docks): only boats and catch from here
    Shore,      // on land, beside the water
    OpenShore,  // on land, beside open sea (lots of water round it)
    Straddle,   // across the shoreline: some of it over the water, some on the bank
    Floating,   // all over water, close to land or to another floating building
    Offshore,   // all over open water, out from the bank
    WallLine,   // a wall line along the shore (sea walls)
}

/// <summary>One sea building's numbers beyond its BuildingDefinition.</summary>
public struct SeaBuilding
{
    public SeaRole role;
    public SeaSite site;
    public int tier;          // its place in its line (boats, ships, lighthouse beams...)
    public int reach;         // how far its boats go (tiles), or a lighthouse's radius
    public int boats;         // boats it sends out (fishing, whaling, pearl diving)
    public int boatKind;      // which fishing boat art (SeaLibrary.fishingBoats)
    public float floor, top;  // output with no fish in reach, and with plenty
    public float need;        // richness (shoals in reach) for full output
    public float drain;       // fish taken from the shoals a second (when fully staffed)
    public int housingReach;  // floating buildings: how far from land (or another floater) they may stand
}

/// <summary>A creature of the sea (gameplay numbers; art and units are in the SeaLibrary).</summary>
public class SeaSpecies
{
    public string id, name, plural, note;
    public WildRole role;
    /// <summary>True for the rare boss monsters (one at a time, with a life that lasts between fights).</summary>
    public bool monster;
    /// <summary>0: the home world's seas; 1: its deep, strange waters; 2: other worlds.</summary>
    public int realm;
    public int countMin = 1, countMax = 2;
    public int minAge;
    public float food, coin, oil, mana;
}

public static class SeaCatalog
{
    // ---------- Buildings ----------

    public static readonly Dictionary<string, SeaBuilding> Buildings = new Dictionary<string, SeaBuilding>
    {
        // The rivers setup's fishing docks: their boats go out to the shoals, which add up to 30% to the catch.
        { "RiverFishingDock", Fish(SeaSite.River, 0, 7, 1, 0, 1f, 1.3f, 1.2f, 0.10f) },
        { "RiverFishery", Fish(SeaSite.River, 1, 9, 1, 1, 1f, 1.3f, 1.2f, 0.16f) },
        { "RiverCannery", Fish(SeaSite.River, 2, 11, 2, 2, 1f, 1.3f, 1.4f, 0.24f) },
        { "RiverAquafarm", Fish(SeaSite.River, 4, 9, 1, 4, 1f, 1.2f, 1.2f, 0.12f) },
        // Fishing of our own.
        { "SeaFishWeir", Fish(SeaSite.Shore, 0, 3, 0, 0, 0.85f, 1.3f, 0.5f, 0.05f) },
        { "SeaFishPens", new SeaBuilding { role = SeaRole.Fishing, site = SeaSite.Straddle, tier = 1, floor = 1f, top = 1f, need = 1f } },
        { "SeaTrawlerPort", Fish(SeaSite.OpenShore, 3, 16, 2, 3, 0.3f, 1.25f, 2f, 0.5f) },
        { "SeaDeepHarvester", Fish(SeaSite.OpenShore, 4, 22, 2, 4, 0.4f, 1.3f, 2.4f, 0.55f) },
        { "SeaVoidTrawler", Fish(SeaSite.OpenShore, 5, 24, 2, 5, 0.6f, 1.3f, 2f, 0.45f) },
        { "SeaKelpFarm", new SeaBuilding { role = SeaRole.Kelp, site = SeaSite.Floating, tier = 0, floor = 1f, top = 1f, need = 1f, housingReach = 3 } },
        { "SeaPlanktonLattice", new SeaBuilding { role = SeaRole.Kelp, site = SeaSite.Floating, tier = 1, floor = 1f, top = 1f, need = 1f, housingReach = 5 } },
        { "SeaLeviathanRanch", new SeaBuilding { role = SeaRole.Ranch, site = SeaSite.OpenShore, tier = 0, floor = 1f, top = 1f, need = 1f } },
        // Whalers.
        { "SeaWhalingStation", new SeaBuilding { role = SeaRole.Whaling, site = SeaSite.OpenShore, tier = 0, reach = 18, boats = 1, boatKind = 1, floor = 0.15f, top = 1.2f, need = 3f } },
        { "SeaHarpoonWharf", new SeaBuilding { role = SeaRole.Whaling, site = SeaSite.OpenShore, tier = 1, reach = 24, boats = 2, boatKind = 2, floor = 0.3f, top = 1.25f, need = 3f } },
        // The shore's trades.
        { "SeaSaltPans", new SeaBuilding { role = SeaRole.Salt, site = SeaSite.OpenShore, floor = 1f, top = 1f, need = 1f } },
        { "SeaPearlDivers", new SeaBuilding { role = SeaRole.Pearl, site = SeaSite.Shore, reach = 7, boats = 1, boatKind = 0, floor = 0.25f, top = 1.2f, need = 1f, drain = 0.08f } },
        // Lighthouses: reach is the beam's radius.
        { "SeaBeacon", new SeaBuilding { role = SeaRole.Light, site = SeaSite.Shore, tier = 0, reach = 14 } },
        { "SeaLighthouse", new SeaBuilding { role = SeaRole.Light, site = SeaSite.Shore, tier = 1, reach = 18 } },
        { "SeaLampLighthouse", new SeaBuilding { role = SeaRole.Light, site = SeaSite.Shore, tier = 2, reach = 23 } },
        { "SeaStarSpire", new SeaBuilding { role = SeaRole.Light, site = SeaSite.Shore, tier = 3, reach = 30 } },
        // Ports: the tier is the merchant ship they send.
        { "SeaWharf", Port(0) }, { "SeaCogHarbour", Port(1) }, { "SeaTradingPort", Port(2) }, { "SeaSteamerDocks", Port(3) },
        { "SeaContainerPort", Port(4) }, { "SeaHoverQuay", Port(5) }, { "SeaVoidMooring", Port(6) },
        // Industry and power.
        { "SeaOilRig", new SeaBuilding { role = SeaRole.Oil, site = SeaSite.Offshore, floor = 0.6f, top = 1.2f, need = 1f, housingReach = 10 } },
        { "SeaTidalGenerator", new SeaBuilding { role = SeaRole.Tidal, site = SeaSite.Straddle, floor = 1f, top = 1f, need = 1f } },
        // Sea walls (wall lines: they hold back floodwater like levees).
        { "SeaWall", new SeaBuilding { role = SeaRole.Wall, site = SeaSite.WallLine } },
        { "SeaStormBarrier", new SeaBuilding { role = SeaRole.Wall, site = SeaSite.WallLine, tier = 1 } },
        // Homes over the water.
        { "SeaStiltHuts", new SeaBuilding { role = SeaRole.Housing, site = SeaSite.Straddle, tier = 0 } },
        { "SeaPierHouses", new SeaBuilding { role = SeaRole.Housing, site = SeaSite.Straddle, tier = 1 } },
        { "SeaFloatingHomes", new SeaBuilding { role = SeaRole.Housing, site = SeaSite.Floating, tier = 2, housingReach = 4 } },
        { "SeaDome", new SeaBuilding { role = SeaRole.Housing, site = SeaSite.Floating, tier = 3, housingReach = 7 } },
    };

    private static SeaBuilding Fish(SeaSite site, int tier, int reach, int boats, int kind, float floor, float top, float need, float drain) =>
        new SeaBuilding { role = SeaRole.Fishing, site = site, tier = tier, reach = reach, boats = boats, boatKind = kind, floor = floor, top = top, need = need, drain = drain };

    private static SeaBuilding Port(int tier) => new SeaBuilding { role = SeaRole.Port, site = SeaSite.OpenShore, tier = tier };

    public static bool TryGet(BuildingDefinition def, out SeaBuilding info)
    {
        info = default;
        return def != null && def.id != null && Buildings.TryGetValue(def.id, out info);
    }

    public static SeaRole RoleOf(BuildingDefinition def) => TryGet(def, out var i) ? i.role : SeaRole.None;

    /// <summary>Is this one of ours (the rivers setup's docks only borrow the boats and shoals)?</summary>
    public static bool IsOurs(BuildingDefinition def) => TryGet(def, out var i) && i.site != SeaSite.River;

    // ---------- Merchant ships, by port tier ----------

    public static readonly string[] ShipNames =
        { "Round ship", "Cog", "Merchant galleon", "Steamship", "Container ship", "Hover-barge", "Void barge" };
    /// <summary>Tiles a second, what a trip carries (a factor) and toughness.</summary>
    public static readonly float[] ShipSpeed = { 1.0f, 1.25f, 1.5f, 2.3f, 2.9f, 4.0f, 5.0f };
    public static readonly float[] ShipCapacity = { 2f, 2.8f, 4f, 7f, 10.5f, 14f, 19f };
    public static readonly int[] ShipHealth = { 260, 420, 900, 1800, 2800, 3800, 5200 };
    public static readonly int[] PortRoutes = { 1, 1, 2, 2, 3, 3, 4 };
    public const int MaxPortRoutes = 5, ShipTierCount = 7;

    /// <summary>Fishing boat art kinds: dugout, sailing smack, steam trawler, diesel trawler, harvester drone, void skiff.</summary>
    public const int FishingBoatKinds = 6;

    /// <summary>The pirates' ships by band: sloop, brig, steam corsair, hover-raider, void reaver.</summary>
    public static int PirateTier(int ageIndex) => ageIndex <= 5 ? (ageIndex <= 4 ? 0 : 1) : ageIndex <= 8 ? 2 : ageIndex <= 11 ? 3 : 4;

    // ---------- Research (small global bonuses) ----------

    public const string TechNets = "SeaKnottedNets", TechSalt = "SeaSaltCuring", TechBeacons = "SeaBeaconFires", TechPearls = "SeaPearlDiving",
        TechCoastal = "SeaCoastalTrade", TechWhaling = "SeaWhaling", TechCogs = "SeaHanseLeague", TechReckoning = "SeaDeadReckoning",
        TechInsurance = "SeaMarineInsurance", TechSteamPackets = "SeaSteamPackets", TechManagement = "SeaFisheryManagement",
        TechContainers = "SeaContainers", TechOffshore = "SeaOffshoreDrilling", TechTidal = "SeaTidalPower", TechSonar = "SeaSonar",
        TechLeviathans = "SeaLeviathanLore", TechVoidTides = "SeaVoidTides", TechPlankton = "SeaPlanktonBlooms", TechVoidTrawling = "SeaVoidTrawling",
        TechSeaWalls = "SeaBreakwaters", TechStilts = "SeaStiltHouses";

    /// <summary>Levels of Fishery Management (each: shoals regrow 12% faster).</summary>
    public const int ManagementLevels = 5;

    // ---------- Creatures ----------

    public static readonly SeaSpecies[] Species =
    {
        new SeaSpecies { id = "SeaWhale", name = "Great Whale", plural = "whales", role = WildRole.Grazer, realm = 0, countMin = 1, countMax = 3,
            food = 320, coin = 25, oil = 40, note = "Slow giants of the open sea. Whalers hunt them for meat, and later for oil." },
        new SeaSpecies { id = "SeaSerpent", name = "Sea Serpent", plural = "sea serpents", role = WildRole.Predator, realm = 1, countMin = 1, countMax = 2, minAge = 3,
            food = 220, coin = 70, oil = 30, note = "Coils as long as a ship. It sinks fishing boats and harries merchants; warships and harpoon wharves can hunt it." },
        new SeaSpecies { id = "SeaKraken", name = "The Kraken", plural = "krakens", role = WildRole.Predator, realm = 1, monster = true, minAge = 4,
            food = 900, coin = 500, oil = 120, mana = 80, note = "A monster from the deep: it drags boats under and haunts the coast until your warships drive it off or sink it." },
        new SeaSpecies { id = "SeaGlassRay", name = "Glass Ray", plural = "glass rays", role = WildRole.Grazer, realm = 2, countMin = 2, countMax = 4,
            food = 260, coin = 40, note = "Rays you can see through, gliding in shoals. Whalers hunt them." },
        new SeaSpecies { id = "SeaVoidEel", name = "Void Eel", plural = "void eels", role = WildRole.Predator, realm = 2, countMin = 1, countMax = 3,
            food = 240, coin = 90, mana = 30, note = "Eels of black light that strike at hulls. Warships and harpoon wharves can hunt them." },
        new SeaSpecies { id = "SeaLeviathan", name = "The Leviathan", plural = "leviathans", role = WildRole.Predator, realm = 2, monster = true, minAge = 12,
            food = 2500, coin = 1500, mana = 300, note = "A legend of the alien deep, big as an island. It sinks whatever sails near it." },
    };

    private static Dictionary<string, SeaSpecies> speciesIndex;

    public static SeaSpecies SpeciesOf(string id)
    {
        if (speciesIndex == null)
        {
            speciesIndex = new Dictionary<string, SeaSpecies>();
            foreach (var s in Species) speciesIndex[s.id] = s;
        }
        return id != null && speciesIndex.TryGetValue(id, out var sp) ? sp : null;
    }

    // ---------- Shoals ----------

    /// <summary>Shoal kinds: a fish shoal (drifts), an oyster bed (stays put, near the shore).</summary>
    public const int ShoalFish = 0, ShoalOysters = 1;
    public const float ShoalFull = 100f;

    /// <summary>Sea power for the power grid (helper U): units of power one tidal generator gives at full staff and health.</summary>
    public const float TidalPowerPerGenerator = 6f, TidalPowerPerUpgrade = 2f;
}

/// <summary>A fishing ground (or oyster bed) on the water.</summary>
[Serializable]
public class SeaShoal
{
    public int id;
    public int kind;
    [Tooltip("Where it is, where it wanders round, and where it's drifting (tiles).")]
    public Vector2 pos, home, target;
    [Tooltip("Fish in it now, and the most it holds.")]
    public float fish = 60f, max = 100f;
    [Tooltip("Play time it was last fished.")]
    public float fishedAt = -999f;
}

/// <summary>Creatures of the sea swimming together (a pod of whales, a serpent, a monster).</summary>
[Serializable]
public class SeaPod
{
    public int id;
    public string species;
    public Vector2 pos, home, target;
    public int count = 1, max = 1;
    public float regrowIn = 120f;
    public float huntedAt = -999f;
    [Tooltip("Monsters: how much of its life is left (0..1), and when it goes back to the deep.")]
    public float life = 1f;
    public float leaveAt;
    [Tooltip("Monsters: heading for your coast.")]
    public bool toCoast;
    [Tooltip("Play time until which it keeps away (driven off).")]
    public float fleeUntil;
}

/// <summary>A sea route between ports in two of your towns.</summary>
[Serializable]
public class SeaRoute
{
    public int id;
    [Tooltip("Instance ids of the ports at either end.")]
    public int a, b;
    public int trips, plundered;
    public float coin, goods;
    [Tooltip("0..1 along the way, and which way the ship is heading.")]
    public float progress;
    public bool toB = true;
    public float lostUntil;
    [Tooltip("The way over the water (worked out once, again when a bridge cuts it).")]
    public List<Vector2> path = new List<Vector2>();
    public float length;
    [Tooltip("No way over the water between the two ports.")]
    public bool blocked;
    public bool pathKnown;
    public int sizeA, sizeB;
}

/// <summary>One world's sea: its fishing grounds, sea life and sea routes.</summary>
[Serializable]
public class SeaWorldState
{
    public List<SeaShoal> shoals = new List<SeaShoal>();
    public List<SeaPod> pods = new List<SeaPod>();
    public List<SeaRoute> routes = new List<SeaRoute>();
    public int nextId = 1;
    public float shoalIn = 2f, podIn = 30f, monsterIn = 2400f, relinkIn;
    public int trips, plundered, whalesTaken, monstersSlain, piratesSunk, boatsLost;
    public float coin, fishCaught;
}
