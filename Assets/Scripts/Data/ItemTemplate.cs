using UnityEngine;

/// <summary>Where a piece of gear is worn.</summary>
public enum GearSlot { Head, Body, Weapon, Shield, Trinket }

/// <summary>How a template looks on a hero (the overlay art drawn for it).</summary>
public enum GearLook
{
    None,
    // Head
    Cap, Hood, DomeHelm, ConeHelm, CrestedHelm,
    // Body
    HideVest, Jerkin, Scale, Mail, Plate,
    // Weapon
    Club, Spear, Sword, Axe, Bow, ShortSword,
    // Shield
    HideShield, WickerShield, RoundShield, TowerShield,
    // Trinket
    Charm, Horn, Amulet, Ring, Wreath,
    // Unit kits only (no items use these yet)
    Headband, Feather, BearPelt, Crown, PlumedHelm, Sling, Javelin, Ballista, OvalShield, Bare,
    // Medieval
    GreatHelm, KettleHat, Cowl, Robe, Surcoat, Crossbow, Pike, Lance, Mace, RuneStaff, KiteShield,
    // Renaissance
    Morion, FeatheredCap, PlagueMask, Doublet, Cuirass, Arquebus, Pistol, Halberd, Zweihander, Flask, Cannon
}

/// <summary>
/// A kind of gear ("Bronze Helm", "War Bow"). Actual items are rolled from a template: a power
/// level (how strong, matched loosely to your progress), a rarity, and bonus traits, which also
/// give each one its name. So a handful of templates makes endless different items.
/// </summary>
[CreateAssetMenu(fileName = "NewItemTemplate", menuName = "Age of Stickman/Item Template")]
public class ItemTemplate : GameData
{
    public string displayName;
    public GearSlot slot;
    public GearLook look;
    public Sprite icon;

    [Tooltip("The earliest power level it drops at (0 = Stone Age, 1 = Bronze, ...).")]
    public float minPower;

    [Header("Base stats at power 1 (they grow with power)")]
    [Tooltip("Extra health share (0.1 = +10% health).")]
    public float health;
    [Tooltip("Extra attack share.")]
    public float attack;
    [Tooltip("Extra speed share.")]
    public float speed;
    [Tooltip("Faster attacks: share taken off the cooldown.")]
    public float haste;
    [Tooltip("Extra range share (bows, spears).")]
    public float range;
    [Tooltip("Armour points: each hit is cut by armour / (armour + 100).")]
    public float armour;
}
