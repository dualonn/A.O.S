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
/// An enemy unit. Raid mode: roams in, fights troops, smashes and loots buildings, then runs
/// off once it can't carry more (killing it recovers the loot). Guard mode: stays around a
/// camp and fights anyone who comes near.
/// </summary>
public class Enemy : Unit
{
    [Header("Raiding (free mode)")]
    [Tooltip("Resources stolen with each hit on a building.")]
    public int lootPerHit = 4;

    [Tooltip("Once carrying this much, the raider runs off with it.")]
    public int lootCapacity = 20;

    public override Team Team => Team.Enemy;

    public bool IsRaider => raid != null;
    public List<ResourceAmount> Loot { get; } = new List<ResourceAmount>();
    public int LootCarried { get; private set; }

    private IRaidTargets raid;
    private BuildingView buildingTarget;
    private bool retreating;
    private Vector3 exitPoint;

    /// <summary>Switch to raid mode. Call right after spawning.</summary>
    public void StartRaid(IRaidTargets targets)
    {
        raid = targets;
    }

    // ---------- Guard mode (free mode raider camps) ----------

    private bool guarding;
    private Vector3 guardHome;
    private float guardRadius;
    private Vector3 wanderTarget;
    private float nextWander;

    public bool IsGuard => guarding;

    /// <summary>Take on the same job as another raider (a boss's summoned help).</summary>
    public void JoinSameJob(Enemy leader)
    {
        if (leader.guarding) StartGuard(leader.guardHome, leader.guardRadius);
        else if (leader.raid != null) StartRaid(leader.raid);
    }

    // ---------- War cry ----------

    private float warCryUntil;

    /// <summary>Driven on by a war cry: attack and speed times 'boost' for a few seconds.</summary>
    public void Rally(float boost, float seconds)
    {
        warCryUntil = Time.time + seconds;
        SetBuildingModifier(new UnitStatModifier { attackMultiplier = boost, speedMultiplier = boost });
    }

    protected override void Update()
    {
        base.Update();
        if (warCryUntil > 0 && Time.time >= warCryUntil)
        {
            warCryUntil = 0;
            SetBuildingModifier(new UnitStatModifier());
        }
    }

    /// <summary>Stay around 'home', fighting anyone who comes within 'radius' of it.</summary>
    public void StartGuard(Vector3 home, float radius)
    {
        guarding = true;
        guardHome = home;
        guardRadius = radius;
        detectionRange = Mathf.Max(detectionRange, radius * 1.6f);
        wanderTarget = transform.position;
    }

    private void Guard()
    {
        if (Time.time >= nextWander)
        {
            nextWander = Time.time + UnityEngine.Random.Range(3f, 7f);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * guardRadius * 0.45f;
            wanderTarget = guardHome + new Vector3(offset.x, offset.y, 0);
        }
        if (Vector2.Distance(transform.position, wanderTarget) > 0.2f)
        {
            MoveTowards(wanderTarget, moveSpeed * 0.5f);
        }
        else
        {
            SetMoving(false);
        }
    }

    protected override Unit SelectTarget(List<Unit> candidates)
    {
        if (guarding)
        {
            // Only see off intruders near the camp; don't chase them across the map.
            candidates = candidates.FindAll(u => Vector2.Distance(u.transform.position, guardHome) <= guardRadius * 1.6f);
            if (candidates.Count == 0) return null;
        }
        return base.SelectTarget(candidates);
    }

    protected override void Idle()
    {
        if (guarding)
        {
            Guard();
        }
        else if (raid != null)
        {
            Raid();
        }
        else
        {
            SetMoving(false); // spawned without orders: stand by
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
}
