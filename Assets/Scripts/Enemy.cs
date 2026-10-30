using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An enemy unit. Walks the waypoint path, stops to fight player troops it meets,
/// then carries on from where it left the path.
/// </summary>
public class Enemy : Unit
{
    /// <summary>Raised when an enemy reaches the end of the path (hook lives/damage here later).</summary>
    public static event Action<Enemy> ReachedEnd;

    [Tooltip("Optional. Defaults to the scene object named \"Path\".")]
    public WayPoints path;

    public override Team Team => Team.Enemy;

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

    private void Start()
    {
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
