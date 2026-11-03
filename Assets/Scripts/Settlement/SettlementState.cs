using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything about the free mode settlement that gets saved. Plain data only:
/// assets are referenced by id so this serialises straight to JSON.
/// </summary>
[Serializable]
public class SettlementState
{
    public int ageIndex;

    [Tooltip("The map is regenerated from this seed, so only the seed is saved.")]
    public int mapSeed;

    [Tooltip("Save format version (old saves are upgraded when loaded).")]
    public int saveVersion;

    [Tooltip("Every world you've settled or visited; each has its own map, economy and raids.")]
    public List<WorldState> worlds = new List<WorldState>();

    [Tooltip("The world you're looking at (index into worlds).")]
    public int currentWorld;

    public List<string> researched = new List<string>();

    public string researchingId;
    public float researchProgress;

    public int nextBuildingId = 1;

    [Tooltip("New buildings start with auto-repair on.")]
    public bool autoRepairNew;

    [Tooltip("Seconds of play so far (heroes' recovery times count against this).")]
    public float playTime;

    public List<HeroState> heroes = new List<HeroState>();
    public List<ItemInstance> inventory = new List<ItemInstance>();

    [Tooltip("Lore entries found (indexes into Lore.Entries), in the order found.")]
    public List<int> journal = new List<int>();

    [Tooltip("The expansion goals on offer (three at a time).")]
    public List<GoalState> goals = new List<GoalState>();
    public int goalsDone;
    public int campsCleared;
    public int sitesDone;
    public int chiefsKilled;
    public int nextItemId = 1;
    public int nextHeroId = 1;

    [Tooltip("Star systems your probes and ships have scanned (their worlds are known).")]
    public List<Vector2Int> scannedSystems = new List<Vector2Int>();

    [Tooltip("Ships in flight between the stars.")]
    public List<ShipState> ships = new List<ShipState>();
    public int nextShipId = 1;

    [Tooltip("Standing cargo orders between your worlds' Starports.")]
    public List<CargoRoute> routes = new List<CargoRoute>();
    public int nextRouteId = 1;

    [Tooltip("Systems whose anomaly you've dealt with.")]
    public List<Vector2Int> anomaliesDone = new List<Vector2Int>();
    [Tooltip("Charted wormholes, as pairs of system keys (each pair is linked).")]
    public List<Vector2Int> wormholes = new List<Vector2Int>();
    [Tooltip("Discoveries among the stars, newest last.")]
    public List<string> codex = new List<string>();

    [Tooltip("Mines in orbit over belts and gas giants.")]
    public List<OrbitalMineState> orbitalMines = new List<OrbitalMineState>();

    // ----- The current world's fields (each world keeps its own; see WorldState) -----

    /// <summary>The world you're looking at.</summary>
    public WorldState World
    {
        get
        {
            if (worlds.Count == 0) worlds.Add(new WorldState { seed = mapSeed });
            return worlds[Mathf.Clamp(currentWorld, 0, worlds.Count - 1)];
        }
    }

    public float population { get => World.population; set => World.population = value; }
    public List<Vector2Int> explored { get => World.explored; set => World.explored = value; }
    public List<int> exploredZones { get => World.exploredZones; set => World.exploredZones = value; }
    public List<ResourceStock> resources { get => World.resources; set => World.resources = value; }
    public List<BuildingState> buildings { get => World.buildings; set => World.buildings = value; }
    public List<CampState> camps { get => World.camps; set => World.camps = value; }
    public List<SiteState> sites { get => World.sites; set => World.sites = value; }
    public float secondsUntilWave { get => World.secondsUntilWave; set => World.secondsUntilWave = value; }
    public int wavesSurvived { get => World.wavesSurvived; set => World.wavesSurvived = value; }
    public List<int> biomesVisited { get => World.biomesVisited; set => World.biomesVisited = value; }
}

/// <summary>A promoted troop: named, saved, levels up, wears gear, and recovers when it falls.</summary>
[Serializable]
public class HeroState
{
    public int id;
    public string name;

    [Tooltip("The unit type it was promoted from.")]
    public string unitId;

    [Tooltip("Instance id of the building it lives at (it recovers there).")]
    public int homeBuilding;

    [Tooltip("0 = level 1.")]
    public int level;
    public float xp;

    [Tooltip("Item uid per gear slot (Head, Body, Weapon, Shield, Trinket); 0 = empty.")]
    public List<int> equipped = new List<int> { 0, 0, 0, 0, 0 };

    [Tooltip("Play time when it's back on its feet after falling.")]
    public float downUntil;

    [Tooltip("The path it chose at level 2: a passive bonus and an ability it uses in a fight.")]
    public HeroClass heroClass;
}

/// <summary>A hero's path, chosen once at level 2.</summary>
public enum HeroClass { None, Warrior, Ranger, Mage, Priest }

/// <summary>A raider camp out on the map: where raids come from, and somewhere to send your army.</summary>
[Serializable]
public class CampState
{
    [Tooltip("The map zone it sits in, as (column, row) (one camp per zone at most).")]
    public Vector2Int zoneXY;

    [Tooltip("Old saves only: the zone as a single index.")]
    public int zone;

    [Tooltip("Bottom-left tile of its 3x3 footprint.")]
    public Vector2Int cell;

    [Tooltip("Stronger each time raiders move back in after it was cleared.")]
    public int extraTier;

    public bool cleared;

    [Tooltip("Seconds until raiders move back into a cleared camp.")]
    public float reoccupyIn;

    [Tooltip("Its chief has been killed (a new one comes when raiders move back in).")]
    public bool bossDefeated;

    [Tooltip("Cleared and built over: raiders never move back in.")]
    public bool claimed;

    public const int Size = 3;

    public Vector2 Centre => cell + Vector2.one * (Size * 0.5f);
}

/// <summary>How a building's troops behave.</summary>
public enum TroopStance
{
    /// <summary>Hold the town: only fight raiders inside it, then return.</summary>
    Defend,

    /// <summary>Advance: march out and hunt raiders anywhere on the map.</summary>
    Attack
}

[Serializable]
public class ResourceStock
{
    public string id;
    public float amount;
}

[Serializable]
public class BuildingState
{
    public int instanceId;
    public string definitionId;
    public Vector2Int cell;
    public float health;

    [Tooltip("0 to 1. The building does nothing until this reaches 1.")]
    public float buildProgress;

    [Tooltip("0+: output multiplier from the site (deposit tiles in reach, water bonus). Set when placed.")]
    public float siteQuality = 1f;

    [Tooltip("How many upgrade tiers are finished.")]
    public int upgradeLevel;

    [Tooltip("0 when idle; counts up to 1 while the next tier is being built.")]
    public float upgradeProgress;

    public bool upgrading;

    [Tooltip("The work under way is a rank up (into the next age's version), not an upgrade.")]
    public bool rankingUp;

    [Tooltip("For buildings that train troops: what those troops do.")]
    public TroopStance stance = TroopStance.Defend;

    [Tooltip("Who gets workers first when people run short: -1 paused, 0 normal, 1 high.")]
    public int priority;

    [Tooltip("Repair damage (and ruins) automatically when no raid is on and it's affordable.")]
    public bool autoRepair;

    [Tooltip("Start the next upgrade, then the rank up, as soon as they're affordable.")]
    public bool queueUpgrades;

    public bool Paused => priority < 0;

    public bool IsBuilt => buildProgress >= 1f;
}
