using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A natural resource that appears on the map (forest, berry bushes, stone, copper ore...).
/// Buildings that need it must be placed with enough of it in their catchment.
/// Deposits never run out.
/// </summary>
[CreateAssetMenu(fileName = "NewDeposit", menuName = "Age of Stickman/Deposit")]
public class DepositType : GameData
{
    public string displayName;

    [Tooltip("What buildings working this deposit produce (for display).")]
    public ResourceType resource;

    [Tooltip("Sprites scattered on deposit tiles; one is picked per tile.")]
    public List<Sprite> sprites = new List<Sprite>();

    [Tooltip("Rich veins of it turn up in the wild: old mines where its normal building works several times faster.")]
    public bool richVeins;

    [Header("Generation")]
    [Tooltip("Where it grows: on grass, or on hills.")]
    public bool onHills;

    [Tooltip("Size of patches: bigger = larger, fewer patches.")]
    public float patchScale = 0.08f;

    [Tooltip("0-1: higher = rarer.")]
    [Range(0, 1)]
    public float rarity = 0.6f;

    [Tooltip("Optional: only grows where moisture is above this (forests, berries).")]
    [Range(0, 1)]
    public float minMoisture;

    [Tooltip("Optional: only grows where moisture is below this.")]
    [Range(0, 1)]
    public float maxMoisture = 1f;
}
