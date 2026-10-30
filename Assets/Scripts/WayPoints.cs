using System.Collections.Generic;
using UnityEngine;

/// <summary>An enemy path. Each child transform is a waypoint, in hierarchy order.</summary>
public class WayPoints : MonoBehaviour
{
    public List<Transform> points = new List<Transform>();
    public List<Vector3> positions = new List<Vector3>();

    // Awake (not Start) so the path is ready before any enemy's Start reads it.
    void Awake()
    {
        points.Clear();
        positions.Clear();
        foreach (Transform child in transform)
        {
            points.Add(child);
            positions.Add(child.position);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < transform.childCount - 1; i++)
        {
            Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i + 1).position);
        }
    }
}
