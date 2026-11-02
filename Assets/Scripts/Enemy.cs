using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a free-roaming raider needs from the scene it is attacking.
/// Implemented by the free mode controller.
/// </summary>
public interface IRaidTargets
{
    /// <summary>The standing building a raider at 'from' should go for, or null if none.</summary>
    BuildingView NearestTarget(Vector3 from);

    bool IsStanding(BuildingView building);

    /// <summary>The point on a building's footprint closest to 'from'.</summary>
    Vector3 ClosestPointOn(BuildingView building, Vector3 from);

    /// <summary>A raider's swing connects with a building.</summary>
    void HitBuilding(BuildingView building, Enemy raider);

    /// <summary>The wall or gate standing on this tile, if any (raiders smash what blocks them).</summary>
    BuildingView WallAt(Vector2Int cell);

    /// <summary>Where a raider at 'from' leaves the map.</summary>
    Vector3 ExitPointFor(Vector3 from);

    void RaiderEscaped(Enemy raider);

    void RaiderKilled(Enemy raider);
}

/// <summary>
/// An enemy unit. Two ways to move:
/// - Path mode (levels): walks the waypoint path, stops to fight troops it meets.
/// - Raid mode (free mode): roams in, fights troops, smashes and loots buildings,
///   then runs off once it can't carry more. Killing it recovers the loot.
/// </summary>
public class Enemy : Unit
{
    /// <summary>Raised when an enemy reaches the end of a level path (hook lives/damage here later).</summary>
    public static event Action<Enemy> ReachedEnd;

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ReachedEnd = null;

    [Tooltip("Optional. Defaults to the scene object named \"Path\".")]
    public WayPoints path;

    [Header("Raiding (free mode)")]
    [Tooltip("Resources stolen with each hit on a building.")]
    public int lootPerHit = 4;

    [Tooltip("Once carrying this much, the raider runs off with it.")]
    public int lootCapacity = 20;

    public override Team Team => Team.Enemy;

    public bool IsRaider => raid != null;
    public List<ResourceAmount> Loot { get; } = new List<ResourceAmount>();
    public int LootCarried { get; private set; }

    /// <summary>How far along the path this enemy is. Higher is closer to the end.</summary>
    public float PathProgress
    {
        get
        {
            if (waypoints == null || waypointIndex >= waypoints.Count)
            {
                return float.MaxValue;
            }
            float toNext = Vector2.Distance(transform.position, waypoints[waypointIndex]);
            return waypointIndex * 1000f - toNext;
        }
    }

    private List<Vector3> waypoints;
    private int waypointIndex;

    private IRaidTargets raid;
    private BuildingView buildingTarget;
    private bool retreating;
    private Vector3 exitPoint;

    /// <summary>Switch to raid mode. Call right after spawning.</summary>
    public void StartRaid(IRaidTargets targets)
    {
        raid = targets;
    }

    private void Start()
    {
        if (raid != null)
        {
            return;
        }
        if (path == null)
        {
            var pathObject = GameObject.Find("Path");
            if (pathObject != null)
            {
                path = pathObject.GetComponent<WayPoints>();
            }
        }
        waypoints = path != null ? path.positions : new List<Vector3>();
        if (waypoints.Count == 0)
        {
            Debug.LogWarning($"{name}: no waypoints found, enemy will stand still.", this);
        }
    }

    protected override void Idle()
    {
        if (raid != null)
        {
            Raid();
        }
        else
        {
            FollowPath();
        }
    }

    // ---------- Raid mode ----------

    private void Raid()
    {
        if (retreating)
        {
            if (MoveTowards(exitPoint, moveSpeed * 1.15f))
            {
                raid.RaiderEscaped(this);
                Destroy(gameObject);
            }
            return;
        }

        if (buildingTarget == null || !raid.IsStanding(buildingTarget))
        {
            buildingTarget = raid.NearestTarget(transform.position);
            if (buildingTarget == null)
            {
                Retreat();
                return;
            }
        }

        Vector3 point = raid.ClosestPointOn(buildingTarget, transform.position);
        float distance = Vector2.Distance(transform.position, point);
        if (distance > attackRange * 0.6f)
        {
            MoveTowards(point, moveSpeed);
            // Walled out: smash whatever wall is in the way, then carry on.
            if (BlockingWall != null)
            {
                var wall = raid.WallAt(BlockingWall.Value);
                if (wall != null && raid.IsStanding(wall))
                {
                    buildingTarget = wall;
                }
            }
            return;
        }

        SetMoving(false);
        Face(point);
        if (CanSwing)
        {
            var target = buildingTarget;
            System.Action hit = () =>
            {
                if (target != null && raid.IsStanding(target))
                {
                    raid.HitBuilding(target, this);
                }
            };
            // Archers shoot fire arrows at buildings; melee raiders smash them.
            StartSwingAt(IsRanged ? () => Shoot(point, hit) : hit);
        }
    }

    /// <summary>Add stolen goods; run off once full.</summary>
    public void Carry(List<ResourceAmount> stolen)
    {
        foreach (var item in stolen)
        {
            int index = Loot.FindIndex(l => l.resource == item.resource);
            if (index >= 0)
            {
                var l = Loot[index];
                l.amount += item.amount;
                Loot[index] = l;
            }
            else
            {
                Loot.Add(item);
            }
            LootCarried += item.amount;
        }
        if (LootCarried >= lootCapacity)
        {
            Retreat();
        }
    }

    private void Retreat()
    {
        if (retreating)
        {
            return;
        }
        retreating = true;
        exitPoint = raid.ExitPointFor(transform.position);
    }

    // Running away with loot: don't stop to fight, just run.
    protected override bool ShouldEngage() => !retreating;

    protected override void Die()
    {
        base.Die();
        raid?.RaiderKilled(this);
    }

    // ---------- Path mode (levels) ----------

    private void FollowPath()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            SetMoving(false);
            return;
        }

        if (MoveTowards(waypoints[waypointIndex], moveSpeed))
        {
            waypointIndex++;
            if (waypointIndex >= waypoints.Count)
            {
                ReachedEnd?.Invoke(this);
                Destroy(gameObject);
            }
        }
    }
}
