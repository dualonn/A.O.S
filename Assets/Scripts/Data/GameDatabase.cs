using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lists every age, resource, building and technology in the game, plus free mode tuning.
/// Lives at Resources/GameDatabase so the settlement can load it from any scene.
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "Age of Stickman/Game Database")]
public class GameDatabase : ScriptableObject
{
    public const string ResourcePath = "GameDatabase";

    [Tooltip("In order: first age first.")]
    public List<Age> ages = new List<Age>();
    public List<ResourceType> resources = new List<ResourceType>();
    public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
    public List<Technology> technologies = new List<Technology>();

    [Tooltip("Kinds of gear that can drop (items are rolled from these).")]
    public List<ItemTemplate> itemTemplates = new List<ItemTemplate>();

    [Header("Free mode: start")]
    public List<ResourceAmount> startingResources = new List<ResourceAmount>();
    public int startingPopulation = 3;

    [Header("Free mode: map")]
    [Tooltip("Size of the starting map, in tiles. It grows as the player explores.")]
    [UnityEngine.Serialization.FormerlySerializedAs("gridSize")]
    public Vector2Int mapSize = new Vector2Int(160, 112);

    [Tooltip("The map never grows beyond this many tiles.")]
    public Vector2Int mapMaxSize = new Vector2Int(640, 448);

    [Tooltip("The map grows in square chunks of this many tiles.")]
    public int chunkSize = 16;

    [Tooltip("Your units and buildings explore unexplored land within this many tiles of them.")]
    public int growWhenWithin = 12;

    public List<DepositType> deposits = new List<DepositType>();

    [Tooltip("Bigger = larger, smoother landforms.")]
    public float terrainScale = 0.03f;

    [Tooltip("Share of the map that is lakes (rivers come on top).")]
    [Range(0, 0.5f)] public float waterShare = 0.1f;

    [Tooltip("Share of the map that is hills.")]
    [Range(0, 0.5f)] public float hillShare = 0.12f;

    public int riverCount = 3;

    [Tooltip("Units move at this fraction of their speed on hills.")]
    [Range(0.1f, 1)] public float hillSpeed = 0.5f;

    [Header("Free mode: population")]
    public ResourceType food;

    [Tooltip("The Coin resource (taxes pay into it).")]
    public ResourceType coin;

    [Tooltip("Basic taxes: once Coin is unlocked, every person pays this much per second (research adds more).")]
    public float taxPerPersonPerSecond = 0.004f;
    public float foodPerPersonPerSecond = 0.02f;

    [Tooltip("Seconds for one new person to arrive while there is food and housing.")]
    public float secondsPerNewPerson = 8f;

    [Header("Free mode: waves")]
    public float firstWaveDelay = 180f;
    public Vector2 waveIntervalRange = new Vector2(150f, 240f);

    [Tooltip("Waves are sized in 'current-age enemies': this many, plus the bonuses below.")]
    public float waveBaseUnits = 2f;

    [Tooltip("Extra enemies (current-age worth) per technology researched.")]
    public float waveUnitsPerTech = 0.3f;

    [Tooltip("Extra enemies (current-age worth) per wave survived.")]
    public float waveUnitsPerWaveSurvived = 0.2f;

    [Tooltip("Waves never grow beyond this many current-age enemies' worth.")]
    public float maxWaveUnits = 16f;

    [Header("Free mode: raider spawning")]
    [Tooltip("Buildings with at most this gap (tiles) between them belong to the same settlement.")]
    public float settlementMergeDistance = 8f;

    [Tooltip("Raiders never spawn within this many tiles of a settlement's buildings.")]
    public float settlementSafeRadius = 34f;

    [Tooltip("The safe zone grows this many tiles per building in the settlement...")]
    public float settlementSafeRadiusPerBuilding = 0.25f;

    [Tooltip("...up to this many extra tiles.")]
    public float settlementSafeRadiusGrowthMax = 10f;

    [Tooltip("Safe zone round an outpost: buildings off on their own with no housing or town centre.")]
    public float outpostSafeRadius = 12f;

    [Tooltip("Defending troops go after any raider that comes within this many tiles of their settlement's buildings.")]
    public float defendAlertRange = 14f;

    [Header("Free mode: heroes")]
    [Tooltip("Seconds of play before a fallen hero is back on their feet at home.")]
    public float heroRecoverySeconds = 120f;

    [Header("Free mode: raider camps")]
    [Tooltip("Chance that a newly explored zone holds a raider camp.")]
    [Range(0, 1)] public float campChance = 0.14f;

    [Tooltip("Camps never sit closer than this many tiles to your buildings.")]
    public float campMinDistance = 44f;

    [Tooltip("A camp sends the raids at any settlement within this many tiles of it.")]
    public float campRaidRange = 110f;

    [Tooltip("Seconds of play before raiders move back into a cleared camp (a little stronger).")]
    public float campReoccupySeconds = 1800f;

    [Tooltip("Camps get one age tougher every this many tiles from your town (never more than one age ahead of you).")]
    public float campTierDistance = 45f;

    [Tooltip("Raiders spawn in a band this many tiles wide just outside a settlement's safe zone.")]
    public float raidSpawnBand = 12f;

    [Header("Free mode: raids (enemies breaking through)")]
    [Tooltip("Share of each stockpile stolen by a full breakthrough.")]
    [Range(0, 1)]
    public float maxResourceLossShare = 0.3f;

    [Tooltip("Total building damage dealt by a full breakthrough, as a share of all building health.")]
    [Range(0, 1)]
    public float maxBuildingDamageShare = 0.4f;

    [Tooltip("Repairing a ruined building costs this share of its build cost.")]
    [Range(0, 1)]
    public float repairCostShare = 0.5f;

    public Age GetAge(int index) => index >= 0 && index < ages.Count ? ages[index] : null;

    public ResourceType GetResource(string id) => resources.Find(r => r != null && r.id == id);

    public BuildingDefinition GetBuilding(string id) => buildings.Find(b => b != null && b.id == id);

    public Technology GetTechnology(string id) => technologies.Find(t => t != null && t.id == id);

    private static GameDatabase cached;

    public static GameDatabase Load()
    {
        if (cached == null)
        {
            cached = Resources.Load<GameDatabase>(ResourcePath);
        }
        return cached;
    }
}
