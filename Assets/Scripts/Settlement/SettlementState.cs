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
    public List<ResourceStock> resources = new List<ResourceStock>();
    public List<BuildingState> buildings = new List<BuildingState>();
    public List<string> researched = new List<string>();

    public string researchingId;
    public float researchProgress;

    public float secondsUntilWave;
    public int wavesSurvived;
    public int nextBuildingId = 1;
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

    public bool IsBuilt => buildProgress >= 1f;
}
