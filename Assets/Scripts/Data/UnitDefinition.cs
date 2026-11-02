using UnityEngine;

/// <summary>
/// The base stats and identity of one unit type (e.g. Stone Age Clubman).
/// Prefabs point at a definition; tech upgrades can later modify these values at runtime.
/// </summary>
[CreateAssetMenu(fileName = "NewUnit", menuName = "Age of Stickman/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    public Sprite icon;
    public Age age;
    public TroopClass troopClass;
    public GameObject prefab;

    [Header("Cost")]
    public int cost;
    public int populationCost = 1;

    [Tooltip("Paid per second for each of these troops alive (Food, later Coin too). Unpaid upkeep stops training.")]
    public System.Collections.Generic.List<ResourceRate> upkeep = new System.Collections.Generic.List<ResourceRate>();

    [Header("Stats")]
    public int maxHealth = 100;

    [Tooltip("World units per second.")]
    public float moveSpeed = 1f;

    public int attackPower = 10;

    [Tooltip("Seconds between attacks.")]
    public float attackCooldown = 1f;

    [Tooltip("How close (world units) the unit must be to hit its target.")]
    public float attackRange = 2f;

    [Tooltip("Seconds from the start of a swing until it lands (match the attack animation's impact frame).")]
    public float hitDelay = 0.58f;

    [Tooltip("How far away (world units) the unit notices enemies.")]
    public float detectionRange = 2.5f;

    [Header("Ranged (leave the projectile empty for melee)")]
    [Tooltip("Arrow/stone sprite. When set, the unit shoots: damage lands when the projectile arrives.")]
    public Sprite projectile;

    public float projectileSpeed = 9f;
}
