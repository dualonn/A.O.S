using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One age of civilisation (Stone Age, Bronze Age, ...).
/// Lists the units available to each side in this age; the number of troop classes
/// grows naturally as later ages list more units.
/// </summary>
[CreateAssetMenu(fileName = "NewAge", menuName = "Age of Stickman/Age")]
public class Age : ScriptableObject
{
    public string displayName;

    [Tooltip("0 for the first age, 1 for the next, and so on.")]
    public int order;

    [TextArea]
    public string description;

    [Tooltip("What it takes to advance INTO this age. Ignored for the first age.")]
    public Requirements advanceRequirements = new Requirements();

    public List<UnitDefinition> playerUnits = new List<UnitDefinition>();
    public List<UnitDefinition> enemyUnits = new List<UnitDefinition>();

    /// <summary>The distinct troop classes the player can field in this age.</summary>
    public List<TroopClass> PlayerClasses
    {
        get
        {
            var classes = new List<TroopClass>();
            foreach (var unit in playerUnits)
            {
                if (unit != null && unit.troopClass != null && !classes.Contains(unit.troopClass))
                {
                    classes.Add(unit.troopClass);
                }
            }
            return classes;
        }
    }
}
