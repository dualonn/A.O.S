using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One upgrade tier for a specific building type. Bought per building (instance), in order.
/// Effects add up across tiers.
/// </summary>
[Serializable]
public class BuildingUpgrade
{
    public string name;

    [TextArea]
    public string description;

    public Requirements requirements = new Requirements();

    [Tooltip("Seconds to complete. The building keeps working meanwhile.")]
    public float upgradeTime = 15f;

    [Header("Effects (added to the building)")]
    [Tooltip("Multiplies output (1.25 = +25%).")]
    public float productionMultiplier = 1f;

    public int extraHousing;
    public int extraHealth;
    public float extraDefense;
    public int extraUnits;

    [Tooltip("Grow the catchment by this many tiles (re-scores the site).")]
    public int extraCatchment;

    [Tooltip("Applied to troops this building trains (e.g. speed).")]
    public UnitStatModifier trainedUnits = new UnitStatModifier();

    public float extraTowerRange;
    public int extraTowerDamage;

    [Tooltip("Raises a tower's operator (when the upgrade makes the tower taller).")]
    public float operatorLift;
}

/// <summary>How a building looks at one upgrade level: a looping set of frames.</summary>
[Serializable]
public class BuildingLook
{
    public List<Sprite> frames = new List<Sprite>();
    public float fps = 6f;
}

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

    [Tooltip("Animated looks by upgrade level (index 0 = not upgraded). Uses the last one for higher levels.")]
    public List<BuildingLook> looks = new List<BuildingLook>();

    [Tooltip("Footprint in grid cells.")]
    public Vector2Int size = Vector2Int.one;

    [Tooltip("Area the art is drawn for, if bigger than the footprint (0 = same as size). It shares the footprint's bottom-left corner; the extra (roof peaks, flags, chimneys) hangs over the tiles behind.")]
    public Vector2Int artSize;

    public Vector2Int ArtSize => artSize.x > 0 && artSize.y > 0 ? artSize : size;

    [Tooltip("The age this building belongs to. It unlocks on reaching that age (plus any research in requirements).")]
    public Age age;

    [Header("Building it")]
    public Requirements requirements = new Requirements();

    [Header("Upgrades (unique to this building)")]
    public List<BuildingUpgrade> upgrades = new List<BuildingUpgrade>();

    [Header("Rank up (after the last upgrade)")]
    [Tooltip("The next age's version of this building (same or smaller footprint). Once fully upgraded and that age is reached, " +
             "the building can be rebuilt in place as it, for less than building one fresh.")]
    public BuildingDefinition rankUpTo;

    public Requirements rankUpRequirements = new Requirements();

    public float rankUpTime = 30f;

    [Tooltip("The settlement's founding building (Campfire). Nothing else can be built until one exists.")]
    public bool isTownCenter;

    [Header("Walls and gates (1x1, placed in lines)")]
    [Tooltip("Wall: blocks everyone. Gate: lets your units through, blocks raiders.")]
    public WallTile wallKind;

    [Tooltip("Wall: post, east end, west end, east-west run, north run, south-east run, south-west run, west half, east half. " +
             "Gate: east-west closed, east-west open, north-south closed, north-south open.")]
    public List<Sprite> pieces = new List<Sprite>();

    public bool IsWall => wallKind != WallTile.None;

    [Tooltip("Tiles up the footprint where the building stands up from the ground: units further forward draw " +
             "over it, units behind it draw under. 0 = the usual front wall. Flat buildings like farm fields use " +
             "a high value, so figures walk over the field.")]
    public float sortLine;

    [Header("Where it can go (OpenTTD-style)")]
    [Tooltip("The deposit this building works. Leave empty if it can go anywhere.")]
    public DepositType requiredDeposit;

    [Tooltip("How far (tiles) around the building it gathers from.")]
    public int catchmentRadius = 3;

    [Tooltip("Deposit tiles needed in the catchment just to build here.")]
    public int minDepositTiles = 2;

    [Tooltip("Deposit tiles in the catchment for full output. Fewer = proportionally less.")]
    public int fullOutputTiles = 10;

    [Tooltip("Can be built on hills (quarries, mines).")]
    public bool canBuildOnHills;

    [Tooltip("Output bonus when water is within the catchment (0.2 = +20%).")]
    public float waterBonus;

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

    [Tooltip("Used up while it works, per second at full output (e.g. a foundry burning Charcoal).")]
    public List<ResourceRate> inputs = new List<ResourceRate>();

    [Tooltip("Share of its output it still makes when its inputs run out (0 = stops completely).")]
    [Range(0, 1)] public float runsWithoutInputs;

    [Header("Defence")]
    [Tooltip("Strength added to the settlement's defence when waves are auto-resolved.")]
    public float defensePower;

    [Tooltip("Optional unit this building trains and keeps alive.")]
    public UnitDefinition trainsUnit;

    public int maxUnits;

    [Header("Tower (shoots raiders by itself)")]
    [Tooltip("0 = not a tower.")]
    public float towerRange;
    public int towerDamage = 10;
    public float towerCooldown = 1.5f;
    public Sprite towerProjectile;
    [Tooltip("How high the tower's shot is lobbed: 0 flies straight (bolts), 0.15 a bow's arc.")]
    public float towerProjectileArc = 0.15f;
    [Tooltip("How fast the tower's shot flies (tiles per second).")]
    public float towerProjectileSpeed = 11f;
    [Tooltip("Blast radius of the tower's shot in tiles (0 = hits only its target).")]
    public float towerSplash;
    [Tooltip("A puff of smoke from the tower with every shot (cannons).")]
    public bool towerMuzzleSmoke;
    [Tooltip("Crewless towers: where shots leave the gun, in tiles from the footprint's bottom-left (0,0 = from the roof). " +
             "The gun faces right; shots at targets to the left leave from the middle of the building at the same height.")]
    public Vector2 towerMuzzle;

    [Tooltip("The unit seen operating the tower (until the Renaissance, ranged buildings are crewed).")]
    public Sprite operatorIdle;
    public List<Sprite> operatorShoot = new List<Sprite>();

    [Tooltip("Shoot frame on which the projectile is released.")]
    public int operatorReleaseFrame = 2;

    [Tooltip("The top of the parapet the operator stands behind, in cells from the building's bottom-left corner.")]
    public Vector2 operatorOffset = new Vector2(1f, 1.3f);
}
