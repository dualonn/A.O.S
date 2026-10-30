using UnityEngine;

/// <summary>Sits on a unit's hover hitbox and tells the unit when the mouse is over it.</summary>
public class MouseTouching : MonoBehaviour
{
    private Unit unit;

    void Awake()
    {
        unit = GetComponentInParent<Unit>();
    }

    void OnMouseEnter()
    {
        if (unit != null)
        {
            unit.SetHovered(true);
        }
    }

    void OnMouseExit()
    {
        if (unit != null)
        {
            unit.SetHovered(false);
        }
    }
}
