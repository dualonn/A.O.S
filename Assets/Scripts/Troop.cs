using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player unit. Fights enemies near it, can be given move orders, and returns to its
/// rally point (where it spawned or was last sent) when there is nothing to fight.
/// Click a troop, then click the ground to send it there.
/// </summary>
public class Troop : Unit
{
    public enum TargetPriority
    {
        First, // the enemy furthest along the path
        Last, // the enemy least far along the path
        Closest
    }

    public TargetPriority targetPriority = TargetPriority.First;

    [Tooltip("Index into UIManagement.images, used when the definition has no icon.")]
    public int ID;

    [Tooltip("Move orders are walked at moveSpeed times this.")]
    public float moveOrderSpeedMultiplier = 1.5f;

    /// <summary>The troop waiting for a destination click, if any.</summary>
    public static Troop Selected { get; private set; }

    public override Team Team => Team.Player;
    public bool IsHovered { get; private set; }

    private Vector3 rallyPoint;
    private bool hasMoveOrder;

    protected override void Awake()
    {
        base.Awake();
        rallyPoint = transform.position;
    }

    protected override void Update()
    {
        HandleInput();
        base.Update();
    }

    private void HandleInput()
    {
        if (!IsAlive)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsHovered)
            {
                Selected = this;
            }
            else if (Selected == this)
            {
                OrderMoveTo(MouseWorldPosition());
                Selected = null;
            }
        }

        // Debug: send every troop to a random spot.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OrderMoveTo(new Vector3(Random.Range(-14f, 13f), Random.Range(-9f, 8f), 0));
        }
    }

    /// <summary>Where the troop returns to when idle. Unlike a move order, it still fights on the way.</summary>
    public void SetRallyPoint(Vector3 point)
    {
        point.z = transform.position.z;
        rallyPoint = point;
    }

    public void OrderMoveTo(Vector3 destination)
    {
        destination.z = transform.position.z;
        rallyPoint = destination;
        hasMoveOrder = true;
    }

    // While walking to a move order, ignore enemies.
    protected override bool ShouldEngage() => !hasMoveOrder;

    protected override void Idle()
    {
        float speed = hasMoveOrder ? moveSpeed * moveOrderSpeedMultiplier : moveSpeed;
        if (MoveTowards(rallyPoint, speed))
        {
            hasMoveOrder = false;
        }
    }

    protected override Unit SelectTarget(List<Unit> candidates)
    {
        if (targetPriority == TargetPriority.Closest)
        {
            return base.SelectTarget(candidates);
        }

        Unit best = null;
        float bestProgress = 0;
        foreach (var unit in candidates)
        {
            float progress = unit is Enemy enemy ? enemy.PathProgress : 0;
            bool better =
                targetPriority == TargetPriority.First ? progress > bestProgress : progress < bestProgress;
            if (best == null || better)
            {
                best = unit;
                bestProgress = progress;
            }
        }
        return best;
    }

    public override void SetHovered(bool hovered)
    {
        IsHovered = hovered;
        if (UIManagement.Instance == null)
        {
            return;
        }
        if (hovered)
        {
            UIManagement.Instance.ShowTroop(this);
        }
        else
        {
            UIManagement.Instance.HideTroop(this);
        }
    }

    protected override void Die()
    {
        if (Selected == this)
        {
            Selected = null;
        }
        SetHovered(false);
        base.Die();
    }

    private static Vector3 MouseWorldPosition()
    {
        Camera cam = Camera.main;
        Vector3 mouse = Input.mousePosition;
        mouse.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        world.z = 0;
        return world;
    }
}
