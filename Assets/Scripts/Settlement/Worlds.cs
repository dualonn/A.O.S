using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One world you've settled or visited: its own map (regenerated from its seed and profile),
/// buildings, camps and sites, and its own economy, people and raids. Ages, research, heroes,
/// items, the journal and goals are shared across all worlds.
/// </summary>
[Serializable]
public class WorldState
{
    public string name = "Homeworld";

    [Tooltip("The kind of world: picks its generation profile (see WorldProfile).")]
    public string profile = WorldProfile.HomeId;

    [Tooltip("This world's map is regenerated from this seed.")]
    public int seed;

    [Tooltip("Where it is in the galaxy: the star system's coordinates, and which planet of that system.")]
    public Vector2Int system;
    public int planet = 2;   // the home world is the third of its system

    public float population;

    [Tooltip("Explored zones of this world's map, as (column, row).")]
    public List<Vector2Int> explored = new List<Vector2Int>();

    [Tooltip("Old saves only: explored zones as row * 128 + column.")]
    public List<int> exploredZones = new List<int>();

    public List<ResourceStock> resources = new List<ResourceStock>();
    public List<BuildingState> buildings = new List<BuildingState>();
    public List<CampState> camps = new List<CampState>();
    public List<SiteState> sites = new List<SiteState>();

    public float secondsUntilWave;
    public int wavesSurvived;

    [Tooltip("Seconds until the next storm or quake (on worlds that have them).")]
    public float hazardIn = 200f;

    [Tooltip("Troops stationed here from other worlds (brought by troopship).")]
    public List<GarrisonStock> garrison = new List<GarrisonStock>();

    [Tooltip("Where you stand with the people of this world (gifts, peace, trade, alliance).")]
    public DiplomacyState diplomacy = new DiplomacyState();

    [Tooltip("Biomes your explorers have reached on this world (as Biome values).")]
    public List<int> biomesVisited = new List<int>();

    [Tooltip("Seconds into this world's year (seasons follow from it).")]
    public float seasonTime;

    [Tooltip("The weather now (a WeatherKind), and seconds until it changes.")]
    public int weather;
    public float weatherLeft;

    [Tooltip("Seconds until lightning strikes again, in a storm.")]
    public float lightningIn = 30f;

    [Tooltip("A colony's first Starport is free (the colony ship brings a kit); true once it has been built.")]
    public bool starportKitUsed;

    [Tooltip("This world's herds, beast pens and wildlife timers (see Wildlife.cs).")]
    public WildWorldState wildlife = new WildWorldState();

    [Tooltip("This world's mood and war weariness (see Culture.cs).")]
    public WorldCulture culture = new WorldCulture();
}

/// <summary>
/// The per-world fields as old saves (version 1 and earlier) stored them, at the top level.
/// Only used once, to move an old save's single world into the worlds list.
/// </summary>
[Serializable]
public class LegacyWorldFields
{
    public float population;
    public List<Vector2Int> explored = new List<Vector2Int>();
    public List<int> exploredZones = new List<int>();
    public List<ResourceStock> resources = new List<ResourceStock>();
    public List<BuildingState> buildings = new List<BuildingState>();
    public List<CampState> camps = new List<CampState>();
    public List<SiteState> sites = new List<SiteState>();
    public float secondsUntilWave;
    public int wavesSurvived;
    public List<int> biomesVisited = new List<int>();

    public WorldState ToWorld(int seed) => new WorldState
    {
        seed = seed,
        population = population,
        explored = explored ?? new List<Vector2Int>(),
        exploredZones = exploredZones ?? new List<int>(),
        resources = resources ?? new List<ResourceStock>(),
        buildings = buildings ?? new List<BuildingState>(),
        camps = camps ?? new List<CampState>(),
        sites = sites ?? new List<SiteState>(),
        secondsUntilWave = secondsUntilWave,
        wavesSurvived = wavesSurvived,
        biomesVisited = biomesVisited ?? new List<int>(),
    };
}
