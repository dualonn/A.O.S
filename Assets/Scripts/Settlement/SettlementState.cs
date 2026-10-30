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
    public float population;

    [Tooltip("The map is regenerated from this seed, so only the seed is saved.")]
    public int mapSeed;

    [Tooltip("Explored zones of the map (zone indices); tiles are regenerated from the seed on load.")]
    public List<int> exploredZones = new List<int>();
    public List<ResourceStock> resources = new List<ResourceStock>();
    public List<BuildingState> buildings = new List<BuildingState>();
    public List<string> researched = new List<string>();

    public string researchingId;
    public float researchProgress;

    public float secondsUntilWave;
    public int wavesSurvived;
    public int nextBuildingId = 1;
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

    public bool IsBuilt => buildProgress >= 1f;
}
