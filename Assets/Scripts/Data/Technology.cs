using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Multiplies the stats of player units, optionally only for one troop class.</summary>
[Serializable]
public class UnitStatModifier
{
    [Tooltip("Leave empty to affect every class.")]
    public TroopClass troopClass;

    public float healthMultiplier = 1f;
    public float attackMultiplier = 1f;
    public float speedMultiplier = 1f;

    [Tooltip("Below 1 attacks faster.")]
    public float cooldownMultiplier = 1f;

    [Tooltip("Multiplies attack range (useful for ranged units).")]
    public float rangeMultiplier = 1f;

    public bool Affects(UnitDefinition unit) =>
        troopClass == null || (unit != null && unit.troopClass == troopClass);
}

/// <summary>Multiplies a building output, optionally only for one resource.</summary>
[Serializable]
public class ProductionModifier
{
    [Tooltip("Leave empty to affect every resource.")]
    public ResourceType resource;

    public float multiplier = 1.1f;
}

/// <summary>Something to research. Unlocks buildings, units and ages that list it as a requirement.</summary>
[CreateAssetMenu(fileName = "NewTechnology", menuName = "Age of Stickman/Technology")]
public class Technology : GameData
{
    public string displayName;

    [TextArea]
    public string description;

    public Sprite icon;

    [Tooltip("The age this tech belongs to (for grouping in the research screen).")]
    public Age age;

    public Requirements requirements = new Requirements();

    [Tooltip("Seconds to research.")]
    public float researchTime = 30f;

    [Header("Effects (research applies globally, as small bonuses)")]
    public List<UnitStatModifier> unitModifiers = new List<UnitStatModifier>();
    public List<ProductionModifier> productionModifiers = new List<ProductionModifier>();

    [Tooltip("Multiplies every building's health (1.08 = +8%).")]
    public float buildingHealthMultiplier = 1f;

    [Tooltip("Taxes: every person pays this much Coin per second (adds up across techs).")]
    public float coinPerPersonPerSecond;

    [Tooltip("Research goes this many times as fast (1.03 = 3% faster).")]
    public float researchSpeedMultiplier = 1f;

    [Header("Levels (Mastery research you can take more than once)")]
    [Tooltip("How many times this can be researched. Each level adds its effects again.")]
    public int maxLevel = 1;

    [Tooltip("Each level costs this many times the one before.")]
    public float levelCostGrowth = 1.5f;

    [Tooltip("Each level takes this many times as long as the one before.")]
    public float levelTimeGrowth = 1.25f;

    /// <summary>Researched in levels (Mastery), rather than once.</summary>
    public bool IsLevelled => maxLevel > 1;
}
