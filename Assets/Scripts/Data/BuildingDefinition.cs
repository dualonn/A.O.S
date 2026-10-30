using System.Collections.Generic;
using UnityEngine;

/// <summary>A building type that can be placed on the free mode grid.</summary>
[CreateAssetMenu(fileName = "NewBuilding", menuName = "Age of Stickman/Building")]
public class BuildingDefinition : GameData
{
    [Header("Identity")]
    public string displayName;

    [TextArea]
    public string description;

    public Sprite icon;

    [Tooltip("Visual shown in the free mode scene.")]
    public GameObject prefab;

    [Tooltip("Footprint in grid cells.")]
    public Vector2Int size = Vector2Int.one;

    [Header("Building it")]
    public Requirements requirements = new Requirements();

    [Tooltip("Seconds to construct.")]
    public float buildTime = 10f;

    [Tooltip("0 means no limit.")]
    public int maxCount;

    [Header("While standing")]
    public int maxHealth = 100;

    [Tooltip("Population capacity this building provides.")]
    public int housing;

    [Tooltip("Population needed to run it at full efficiency.")]
    public int workers;

    public List<ResourceRate> production = new List<ResourceRate>();

    [Header("Defence")]
    [Tooltip("Strength added to the settlement's defence when waves are auto-resolved.")]
    public float defensePower;

    [Tooltip("Optional unit this building trains and keeps alive.")]
    public UnitDefinition trainsUnit;

    public int maxUnits;
}
