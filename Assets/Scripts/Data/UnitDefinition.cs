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

    [Tooltip("How high it's lobbed, as a share of the shot's length: 0 flies straight (bolts), 0.15 a bow's arc.")]
    public float projectileArc = 0.15f;

    [Header("Healer (medics)")]
    [Tooltip("Never fights: tends wounded troops instead, healing its attack power with each go.")]
    public bool healer;
    [Tooltip("How far (tiles) a healer looks for wounded troops.")]
    public float healRange = 8f;

    [Header("Boss (raider chiefs only)")]
    public bool isBoss;
    public BossAbility bossAbility;
    [Tooltip("Seconds between uses of the ability.")]
    public float abilityCooldown = 8f;
    [Tooltip("Slam: reach in tiles.")]
    public float abilityRadius = 2.2f;
    [Tooltip("Slam: damage as a share of attack. War cry: attack and speed boost for raiders (1.3 = +30%).")]
    public float abilityPower = 1.3f;
    [Tooltip("Summon: what it calls in, and how many each time.")]
    public UnitDefinition summons;
    public int summonCount = 2;
}
