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

    [Tooltip("How far away (world units) the unit notices enemies.")]
    public float detectionRange = 2.5f;

    [Header("References")]
    [FormerlySerializedAs("pAnim")]
    [FormerlySerializedAs("eAnim")]
    public Animator anim;

    public Slider healthSlider;

    [Header("Runtime (read only)")]
    public Unit currentTarget;

    private const float ScanInterval = 0.1f;
    private static readonly ContactFilter2D ScanFilter = new ContactFilter2D().NoFilter();

    protected readonly List<Unit> targetsInRange = new List<Unit>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private float nextScanTime;
    private float nextAttackTime;
    private bool isDead;

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
        }
        ApplyResearchUpgrades();
        health = maxHealth;

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

        if (healthSlider != null)
        {
            healthSlider.value = health;
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

    private void Engage(Unit target)
    {
        Vector3 targetPos = target.transform.position;
        Face(targetPos);

        if (Vector2.Distance(transform.position, targetPos) > attackRange)
        {
            MoveTowards(targetPos, moveSpeed);
            return;
        }

        SetMoving(false);
        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            if (anim != null)
            {
                anim.SetTrigger("Attack");
            }
            target.TakeDamage(attackPower);
        }
    }

    /// <summary>Step towards a point. Returns true once the point is reached.</summary>
    protected bool MoveTowards(Vector3 destination, float speed)
    {
        destination.z = transform.position.z;
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );
        bool arrived = transform.position == destination;
        SetMoving(!arrived);
        if (!arrived)
        {
            Face(destination);
        }
        return arrived;
    }

    protected void SetMoving(bool moving)
    {
        if (anim != null)
        {
            anim.SetBool("IsMoving", moving);
        }
    }

    protected void Face(Vector3 point)
    {
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
        {
            transform.eulerAngles = new Vector3(0, dx < 0 ? 180 : 0, 0);
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
        {
            return;
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
        SetMoving(false);
        if (anim != null)
        {
            anim.SetBool("Death", true);
        }
        if (healthSlider != null)
        {
            healthSlider.value = 0;
        }
        // Stop being detected or hovered while the death animation plays.
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
        Destroy(gameObject, 3f);
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
