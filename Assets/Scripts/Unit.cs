using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum Team
{
    Player,
    Enemy
}

/// <summary>
/// Shared behaviour for every fighting unit: health, targeting, melee attacks, facing and death.
/// Troop and Enemy add their own movement on top.
/// </summary>
public abstract class Unit : MonoBehaviour
{
    [Tooltip("Optional. When set, its stats replace the values below at spawn.")]
    public UnitDefinition definition;

    [Header("Stats")]
    public int maxHealth = 100;
    public int health = 100;

    [Tooltip("World units per second.")]
    public float moveSpeed = 1f;

    public int attackPower = 10;

    [Tooltip("Seconds between attacks.")]
    [FormerlySerializedAs("attackSpeed")]
    public float attackCooldown = 1f;

    public float attackRange = 2f;

    [Tooltip("Seconds from the start of a swing until it lands (damage is dealt then).")]
    public float hitDelay = 0.58f;

    [Tooltip("How far away (world units) the unit notices enemies.")]
    public float detectionRange = 2.5f;

    [Tooltip("How hard this unit's hits shove targets back (world units per second at the default size).")]
    public float knockback = 2.5f;

    [Header("Death")]
    [Tooltip("Seconds the body stays after the death animation starts, before fading.")]
    public float corpseTime = 2f;
    public float fadeTime = 0.8f;

    [Header("References")]
    [FormerlySerializedAs("pAnim")]
    [FormerlySerializedAs("eAnim")]
    public Animator anim;

    public Slider healthSlider;

    [Tooltip("How close other units may stand (world units). 0 = work it out from the unit's scale.")]
    public float personalSpace;

    [Header("Runtime (read only)")]
    public Unit currentTarget;

    private const float ScanInterval = 0.1f;
    private static readonly ContactFilter2D ScanFilter = ContactFilter2D.noFilter;

    protected readonly List<Unit> targetsInRange = new List<Unit>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private float nextScanTime;
    private float nextAttackTime;
    private bool isDead;

    // The swing in progress: damage lands at swingHitTime.
    private Unit swingTarget;
    private System.Action swingAction;
    private bool swingPending;
    private float swingHitTime;

    /// <summary>Time after the hit for the follow-through before the next swing can start.</summary>
    private const float FollowThrough = 0.2f;

    // Loose melee
    private Unit engagedWith;
    private float engageAngle;
    private float wobbleSeed;
    private Vector2 knockVelocity;
    private readonly PathFollower pathFollower = new PathFollower();
    private const float SeparationStrength = 4f;
    private const float KnockbackDrag = 14f;

    /// <summary>Every living unit, for pushing apart. Small counts, so a plain list is fine.</summary>
    private static readonly List<Unit> All = new List<Unit>();

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    /// <summary>Every living unit (read only).</summary>
    public static IReadOnlyList<Unit> Living => All;

    protected virtual void OnEnable() => All.Add(this);

    protected virtual void OnDisable() => All.Remove(this);

    public abstract Team Team { get; }
    public bool IsAlive => !isDead && health > 0;

    protected virtual void Awake()
    {
        if (definition != null)
        {
            maxHealth = definition.maxHealth;
            moveSpeed = definition.moveSpeed;
            attackPower = definition.attackPower;
            attackCooldown = definition.attackCooldown;
            attackRange = definition.attackRange;
            detectionRange = definition.detectionRange;
            hitDelay = definition.hitDelay;
        }
        ApplyResearchUpgrades();
        health = maxHealth;
        visual = GetComponent<IUnitVisual>();

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = health;
        }
    }

    protected virtual void Update()
    {
        if (isDead)
        {
            return;
        }

        if (healthSlider != null)
        {
            healthSlider.value = health;
        }
        visual?.SetHealth(health / (float)Mathf.Max(1, maxHealth));

        // A swing in progress lands when the club connects; the unit keeps moving meanwhile.
        if (swingPending && Time.time >= swingHitTime)
        {
            LandHit();
        }

        ApplyNudges();

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + ScanInterval;
            ScanForTargets();
        }
        targetsInRange.RemoveAll(u => u == null || !u.IsAlive);
        currentTarget = targetsInRange.Count > 0 ? SelectTarget(targetsInRange) : null;

        if (currentTarget != null && ShouldEngage())
        {
            Engage(currentTarget);
        }
        else
        {
            Idle();
        }
    }

    private IUnitVisual visual;

    // Stats after research, before any building upgrades (captured on first use).
    private bool hasBaseStats;
    private int baseMaxHealth;
    private int baseAttack;
    private float baseSpeed;
    private float baseCooldown;
    private float baseRange;
    private UnitStatModifier appliedBuildingMod;

    /// <summary>Ranged units shoot projectiles; damage lands when they arrive.</summary>
    public bool IsRanged => definition != null && definition.projectile != null;

    /// <summary>Where projectiles leave from (about chest height).</summary>
    public Vector3 LaunchPoint => transform.position + Vector3.up * 0.5f;

    /// <summary>Fire this unit's projectile at a point; 'onArrive' runs when it lands.</summary>
    protected void Shoot(Vector3 at, System.Action onArrive) =>
        Projectile.FireAt(definition.projectile, LaunchPoint, at, definition.projectileSpeed, onArrive);

    /// <summary>The nearest living unit of another team within 'range' of 'position', or null.</summary>
    public static Unit FindNearestEnemy(Team ofTeam, Vector3 position, float range)
    {
        Unit best = null;
        float bestDistance = range * range;
        foreach (var u in All)
        {
            if (u == null || !u.IsAlive || u.Team == ofTeam)
            {
                continue;
            }
            float d = ((Vector2)(u.transform.position - position)).sqrMagnitude;
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = u;
            }
        }
        return best;
    }

    /// <summary>
    /// Apply upgrades from the building that trained this unit. Safe to call every frame;
    /// re-applies only when the modifier changes, keeping the current health share.
    /// </summary>
    public void SetBuildingModifier(UnitStatModifier mod)
    {
        if (mod == null || isDead)
        {
            return;
        }
        if (appliedBuildingMod != null &&
            Mathf.Approximately(appliedBuildingMod.healthMultiplier, mod.healthMultiplier) &&
            Mathf.Approximately(appliedBuildingMod.attackMultiplier, mod.attackMultiplier) &&
            Mathf.Approximately(appliedBuildingMod.speedMultiplier, mod.speedMultiplier) &&
            Mathf.Approximately(appliedBuildingMod.cooldownMultiplier, mod.cooldownMultiplier))
        {
            return;
        }
        if (!hasBaseStats)
        {
            hasBaseStats = true;
            baseMaxHealth = maxHealth;
            baseAttack = attackPower;
            baseSpeed = moveSpeed;
            baseCooldown = attackCooldown;
            baseRange = attackRange;
        }
        attackRange = baseRange * mod.rangeMultiplier;
        float healthShare = health / (float)Mathf.Max(1, maxHealth);
        maxHealth = Mathf.RoundToInt(baseMaxHealth * mod.healthMultiplier);
        health = Mathf.Max(1, Mathf.RoundToInt(maxHealth * healthShare));
        attackPower = Mathf.RoundToInt(baseAttack * mod.attackMultiplier);
        moveSpeed = baseSpeed * mod.speedMultiplier;
        attackCooldown = baseCooldown * mod.cooldownMultiplier;
        appliedBuildingMod = Settlement.Combine(mod, new UnitStatModifier());
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
        }
    }

    /// <summary>Player units get the bonuses from everything researched in free mode.</summary>
    private void ApplyResearchUpgrades()
    {
        if (Team != Team.Player || Settlement.Instance == null)
        {
            return;
        }
        var mod = Settlement.Instance.GetUnitModifier(definition);
        maxHealth = Mathf.RoundToInt(maxHealth * mod.healthMultiplier);
        attackPower = Mathf.RoundToInt(attackPower * mod.attackMultiplier);
        moveSpeed *= mod.speedMultiplier;
        attackCooldown *= mod.cooldownMultiplier;
        attackRange *= mod.rangeMultiplier;
    }

    /// <summary>Pick which enemy in range to fight. Default: the closest.</summary>
    protected virtual Unit SelectTarget(List<Unit> candidates)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (var unit in candidates)
        {
            float distance = Vector2.Distance(transform.position, unit.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    /// <summary>Whether to fight the current target this frame (false lets a move order win).</summary>
    protected virtual bool ShouldEngage() => true;

    /// <summary>What to do when there is nothing to fight.</summary>
    protected abstract void Idle();

    /// <summary>
    /// Loose, WorldBox-style melee: each attacker picks its own side of the target, keeps
    /// shuffling around while fighting, and swings whenever it is close enough.
    /// </summary>
    private void Engage(Unit target)
    {
        Vector3 targetPos = target.transform.position;
        Vector2 fromTarget = transform.position - targetPos;
        float distance = fromTarget.magnitude;

        if (engagedWith != target)
        {
            // Approach from roughly the side we're already on, spread out a bit.
            engagedWith = target;
            float current = Mathf.Atan2(fromTarget.y, fromTarget.x) * Mathf.Rad2Deg;
            engageAngle = current + Random.Range(-55f, 55f);
            wobbleSeed = Random.Range(0f, 100f);
        }

        // Drift around the chosen side so fights don't look frozen.
        float angle = (engageAngle + Mathf.Sin(Time.time * 0.9f + wobbleSeed) * 30f) * Mathf.Deg2Rad;
        Vector3 spot = targetPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * attackRange * 0.7f;

        bool inReach = distance <= attackRange;
        float speed = inReach ? moveSpeed * 0.4f : moveSpeed;
        if (Vector2.Distance(transform.position, spot) > 0.12f)
        {
            MoveTowards(spot, speed, faceMovement: false);
        }
        else
        {
            SetMoving(false);
        }
        Face(targetPos);

        if (inReach && Time.time >= nextAttackTime)
        {
            StartSwing(target);
        }
    }

    /// <summary>Knockback from hits, and gently pushing apart from units that are too close.</summary>
    private void ApplyNudges()
    {
        Vector3 pos = transform.position;
        float space = PersonalSpace;
        Vector2 push = Vector2.zero;
        foreach (var other in All)
        {
            if (other == this || !other.IsAlive)
            {
                continue;
            }
            Vector2 away = pos - other.transform.position;
            float d = away.magnitude;
            if (d < space && d > 0.0001f)
            {
                push += away / d * (space - d);
            }
            else if (d <= 0.0001f)
            {
                push += Random.insideUnitCircle * 0.1f;
            }
        }
        Vector2 step = push * SeparationStrength * Time.deltaTime + knockVelocity * Time.deltaTime;
        Vector3 nudged = pos + new Vector3(step.x, step.y, 0);
        var nav = MapNavigator.Current;
        if (nav == null)
        {
            transform.position = nudged;
        }
        else if (!nav.IsWalkable(pos, Team))
        {
            // Ended up in water (or a wall) somehow: get back out.
            transform.position = nav.Step(pos, pos, moveSpeed * Time.deltaTime, Team);
        }
        else
        {
            // Never get shoved into water or through walls; slide along instead.
            transform.position = nav.Step(pos, nudged, step.magnitude, Team);
        }
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, KnockbackDrag * Time.deltaTime);
    }

    /// <summary>How close other units can stand, scaled with the unit's size.</summary>
    private float PersonalSpace => personalSpace > 0 ? personalSpace : 1.6f * Mathf.Abs(transform.lossyScale.y);

    /// <summary>True when a new swing can start.</summary>
    protected bool CanSwing => !isDead && !swingPending && Time.time >= nextAttackTime;

    /// <summary>
    /// Swing at something that isn't a unit (a building, say). 'onHit' runs when the swing connects.
    /// </summary>
    protected void StartSwingAt(System.Action onHit)
    {
        swingAction = onHit;
        StartSwing(null);
    }

    /// <summary>Start the attack animation; the damage lands when the swing connects.</summary>
    private void StartSwing(Unit target)
    {
        if (target != null)
        {
            swingAction = null;
        }
        swingTarget = target;
        swingPending = true;
        swingHitTime = Time.time + hitDelay;
        // A little randomness so groups don't swing in lockstep.
        float cooldown = attackCooldown * Random.Range(0.85f, 1.15f);
        nextAttackTime = Time.time + Mathf.Max(cooldown, hitDelay + FollowThrough);
        if (visual != null)
        {
            visual.PlayAttack(hitDelay);
        }
        else if (anim != null)
        {
            anim.SetTrigger("Attack");
        }
    }

    private void LandHit()
    {
        swingPending = false;
        if (swingAction != null)
        {
            var action = swingAction;
            swingAction = null;
            action();
            return;
        }
        var target = swingTarget;
        if (target == null || !target.IsAlive)
        {
            return;
        }
        if (IsRanged)
        {
            // Loose an arrow: it does the damage when it lands.
            Projectile.Fire(definition.projectile, LaunchPoint, target, definition.projectileSpeed, attackPower, this);
            return;
        }
        // Melee: missed if the target stepped well out of reach during the wind-up.
        if (Vector2.Distance(transform.position, target.transform.position) <= attackRange * 1.25f + 0.2f)
        {
            target.TakeDamage(attackPower, this);
        }
    }

    /// <summary>
    /// Set by MoveTowards when a wall (one this unit can't pass) is right in its way.
    /// Raiders use it to know what to smash.
    /// </summary>
    protected Vector2Int? BlockingWall { get; private set; }

    /// <summary>Step towards a point. Returns true once the point is reached.</summary>
    protected bool MoveTowards(Vector3 destination, float speed, bool faceMovement = true)
    {
        var nav = MapNavigator.Current;
        // Can't stand in water: aim for the nearest dry spot instead.
        if (nav != null && !nav.IsWalkable(destination, Team))
        {
            destination = nav.NearestWalkable(destination, Team);
        }
        destination.z = transform.position.z;
        // On the free mode map, route around water and walls, and slow down on hills.
        Vector3 next = pathFollower.NextPoint(transform.position, destination, Team);
        next.z = transform.position.z;
        BlockingWall = null;
        if (nav != null)
        {
            speed *= nav.SpeedFactor(transform.position);
            Vector3 before = transform.position;
            transform.position = nav.Step(transform.position, next, speed * Time.deltaTime, Team);
            // Barely moved and a wall is right ahead: report it.
            if ((transform.position - before).sqrMagnitude < (speed * Time.deltaTime * 0.5f) * (speed * Time.deltaTime * 0.5f)
                && nav.WallAhead(before, next, 0.8f, Team, out var wall))
            {
                BlockingWall = wall;
            }
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, next, speed * Time.deltaTime);
        }
        // Close enough counts as arrived (separation pushes can keep us a hair away).
        bool arrived = Vector2.Distance(transform.position, destination) < 0.05f;
        SetMoving(!arrived);
        if (!arrived && faceMovement)
        {
            Face(next);
        }
        return arrived;
    }

    protected void SetMoving(bool moving)
    {
        if (visual != null)
        {
            visual.SetMoving(moving);
        }
        else if (anim != null)
        {
            anim.SetBool("IsMoving", moving);
        }
    }

    public void Face(Vector3 point)
    {
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
        {
            transform.eulerAngles = new Vector3(0, dx < 0 ? 180 : 0, 0);
        }
    }

    public void TakeDamage(int amount, Unit attacker = null)
    {
        if (isDead)
        {
            return;
        }
        if (attacker != null)
        {
            Vector2 away = transform.position - attacker.transform.position;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Random.insideUnitCircle;
            }
            float size = PersonalSpace / 0.8f;
            knockVelocity += away.normalized * knockback * size;
        }
        health = Mathf.Max(0, health - amount);
        if (health <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        isDead = true;
        currentTarget = null;
        swingPending = false;
        swingTarget = null;
        swingAction = null;
        SetMoving(false);
        if (visual != null)
        {
            visual.PlayDeath();
        }
        else if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetBool("Death", true);
        }
        if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(false);
        }
        // Stop being detected or hovered while the death animation plays.
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
        StartCoroutine(FadeAndDestroy());
    }

    /// <summary>Let the death animation play, leave the body briefly, then fade it out.</summary>
    private System.Collections.IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(corpseTime);
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var startAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            startAlpha[i] = renderers[i].color.a;
        }
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            float k = 1f - t / fadeTime;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    var c = renderers[i].color;
                    c.a = startAlpha[i] * k;
                    renderers[i].color = c;
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Called by MouseTouching on the unit's hover hitbox.</summary>
    public virtual void SetHovered(bool hovered) { }

    /// <summary>Refresh the list of living enemies within detectionRange.</summary>
    private void ScanForTargets()
    {
        targetsInRange.Clear();
        overlapResults.Clear();
        Physics2D.OverlapCircle(transform.position, detectionRange, ScanFilter, overlapResults);
        foreach (var col in overlapResults)
        {
            var unit = col.GetComponentInParent<Unit>();
            if (unit != null && unit.Team != Team && unit.IsAlive && !targetsInRange.Contains(unit))
            {
                targetsInRange.Add(unit);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
