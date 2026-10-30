using System;
using System.Collections.Generic;

[Serializable]
public struct ResourceAmount
{
    public ResourceType resource;
    public int amount;
}

[Serializable]
public struct ResourceRate
{
    public ResourceType resource;

    [UnityEngine.Tooltip("Amount produced per second at full efficiency.")]
    public float perSecond;
}

[Serializable]
public struct BuildingCount
{
    public BuildingDefinition building;
    public int count;
}

/// <summary>
/// What it takes to build, research or advance: conditions that must be met,
/// plus a cost that is spent. Checked by Settlement.Meets.
/// </summary>
[Serializable]
public class Requirements
{
    [UnityEngine.Tooltip("The settlement must be in this age or later.")]
    public Age minimumAge;

    public List<Technology> technologies = new List<Technology>();
    public List<BuildingCount> buildings = new List<BuildingCount>();
    public int population;

    [UnityEngine.Tooltip("Spent when the action starts.")]
    public List<ResourceAmount> cost = new List<ResourceAmount>();
}
