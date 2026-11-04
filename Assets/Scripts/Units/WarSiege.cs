using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A raiders' siege engine (ram, catapult, trebuchet, bombard... up to orbital beacons). It
/// ignores troops and goes for your towers first, then any building, battering or bombarding
/// it for many times its attack. Walls in its way are smashed. Troops have to come out and kill it.
/// </summary>
public class SiegeEngine : Enemy
{
    public WarCatalog.SiegeKind kind;
    [Tooltip("Damage to buildings, as a multiple of its attack.")]
    public float structureMultiplier = 4f;
    [Tooltip("Calls its strike down from orbit instead of firing from where it stands.")]
    public bool fromSky;

    private BuildingView siegeTarget;
    private float nextPick;

    protected override bool ShouldEngage() => false;

    protected override Unit SelectTarget(List<Unit> candidates) => null;

    protected override void Idle()
    {
        var c = FreeModeController.Instance;
        if (c == null || Charmed || IsGuard || !IsRaider)
        {
            base.Idle();
            return;
        }
        if (siegeTarget == null || !c.IsStanding(siegeTarget) || Time.time >= nextPick)
        {
            nextPick = Time.time + 2f;
            siegeTarget = Pick(c);
        }
        if (siegeTarget == null)
        {
            base.Idle();   // nothing left standing: off home with whatever was taken
            return;
        }
        Vector3 point = c.ClosestPointOn(siegeTarget, transform.position);
        float reach = IsRanged ? attackRange * 0.85f : attackRange * 0.6f;
        if (Vector2.Distance(transform.position, point) > reach)
        {
            MoveTowards(point, moveSpeed);
            if (BlockingWall != null)
            {
                var wall = c.WallAt(BlockingWall.Value);
                if (wall != null && c.IsStanding(wall)) { siegeTarget = wall; nextPick = Time.time + 4f; }
            }
            return;
        }
        SetMoving(false);
        Face(point);
        if (!CanSwing) return;
        var target = siegeTarget;
        System.Action hit = () => Strike(c, target, point);
        if (fromSky) StartSwingAt(() => SkyStrike(point, hit));
        else StartSwingAt(IsRanged ? () => WarSiegeFx.Launch(this, kind, point, () => Shoot(point, hit)) : hit);
    }

    /// <summary>Towers and gatehouses first (they're what hurts), then the nearest building.</summary>
    private BuildingView Pick(FreeModeController c)
    {
        BuildingView best = null;
        float bestScore = float.MaxValue;
        Vector3 me = transform.position;
        foreach (var view in c.Buildings)
        {
            if (view == null || !c.IsStanding(view) || view.Definition.IsWall) continue;
            float d = Vector2.Distance(me, c.ClosestPointOn(view, me));
            float score = d * (view.Definition.towerRange > 0f ? 0.6f : 1f);
            if (score < bestScore) { bestScore = score; best = view; }
        }
        return best;
    }

    private void Strike(FreeModeController c, BuildingView target, Vector3 point)
    {
        var s = Settlement.Instance;
        if (s == null || target == null || !c.IsStanding(target)) return;
        float damage = attackPower * structureMultiplier;
        s.DamageBuilding(target.State, damage);
        target.Shake();
        float splash = definition != null ? definition.splashRadius : 0f;
        if (splash > 0f)
        {
            Puff.Blast(point, Mathf.Min(2.2f, splash), definition.splashFire);
            // Shot and shell throw rubble about: whatever's beside it takes some too.
            foreach (var view in c.Buildings)
            {
                if (view == null || view == target || !c.IsStanding(view)) continue;
                if (Vector2.Distance(point, c.ClosestPointOn(view, point)) <= splash) { s.DamageBuilding(view.State, damage * 0.4f); view.Shake(); }
            }
        }
        else
        {
            GameAudio.Play(Random.value < 0.3f ? "creak" : "blunt_hit", point, 0.8f);
            Puff.Spawn(point, 0.45f, new Color(0.6f, 0.5f, 0.4f, 0.8f), 4, 0.5f);
        }
    }

    /// <summary>A beacon's strike falls from the sky.</summary>
    private void SkyStrike(Vector3 point, System.Action hit)
    {
        Puff.Spawn(transform.position + Vector3.up * 0.9f, 0.4f, new Color(1f, 0.4f, 0.35f, 0.8f), 3, 0.4f);
        var sprite = definition != null ? definition.projectile : null;
        Projectile.FireAt(sprite, point + new Vector3(0.6f, 10f, 0f), point, 26f, hit, 0f);
        GameAudio.Play("warp", point, 0.6f);
    }
}

/// <summary>
/// Your own siege engines: trained at a siege workshop, they go for raider camps (and down into
/// dungeons with a party) and batter them for many times their attack. Rams don't fight troops at all;
/// engines that shoot will bombard raiders too.
/// </summary>
public class SiegeTroop : Troop
{
    public WarCatalog.SiegeKind kind;
    [Tooltip("Damage to camps, as a multiple of its attack.")]
    public float structureMultiplier = 4f;
    [Tooltip("Never fights troops (a ram): it only batters camps.")]
    public bool ramOnly;
    public bool fromSky;

    protected override Unit SelectTarget(List<Unit> candidates) => ramOnly ? null : base.SelectTarget(candidates);

    protected override void Idle()
    {
        var camp = TargetCamp;
        if (camp != null && camp.IsStanding && Dungeon == null)
        {
            Batter(camp);
            return;
        }
        base.Idle();
    }

    private void Batter(CampView camp)
    {
        Vector3 point = camp.ClosestPoint(transform.position);
        float reach = IsRanged ? attackRange * 0.85f : attackRange * 0.6f;
        if (Vector2.Distance(transform.position, point) > reach)
        {
            MoveTowards(point, moveSpeed);
            return;
        }
        SetMoving(false);
        Face(point);
        if (!CanSwing) return;
        float damage = attackPower * structureMultiplier;
        System.Action hit = () =>
        {
            if (camp == null || !camp.IsStanding) return;
            camp.TakeHit(damage);
            float splash = definition != null ? definition.splashRadius : 0f;
            if (splash > 0f) Puff.Blast(point, Mathf.Min(2f, splash), definition.splashFire);
            else Puff.Spawn(point, 0.45f, new Color(0.6f, 0.5f, 0.4f, 0.8f), 4, 0.5f);
        };
        if (fromSky)
        {
            var sprite = definition != null ? definition.projectile : null;
            StartSwingAt(() => Projectile.FireAt(sprite, point + new Vector3(0.6f, 10f, 0f), point, 26f, hit, 0f));
        }
        else StartSwingAt(IsRanged ? () => WarSiegeFx.Launch(this, kind, point, () => Shoot(point, hit)) : hit);
    }
}

/// <summary>The sound and smoke of a siege engine letting fly.</summary>
public static class WarSiegeFx
{
    public static void Launch(Unit u, WarCatalog.SiegeKind kind, Vector3 at, System.Action shoot)
    {
        if (u == null) return;
        GameAudio.Fire(u);
        if (kind == WarCatalog.SiegeKind.Onager || kind == WarCatalog.SiegeKind.Trebuchet) GameAudio.Play("siege_throw", u.transform.position);
        if (kind == WarCatalog.SiegeKind.Launcher) GameAudio.Play("rocket_launch", u.transform.position);
        if (u.definition != null && u.definition.muzzleSmoke)
        {
            Vector3 toward = (at - u.transform.position).normalized;
            Puff.Smoke(u.LaunchPoint + toward * 0.35f);
        }
        shoot();
    }
}
