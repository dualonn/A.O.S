using UnityEngine;

/// <summary>
/// A role a unit can fill, such as Melee, Ranged or Cavalry.
/// New classes are added as assets, so later ages can introduce classes without code changes.
/// </summary>
[CreateAssetMenu(fileName = "NewTroopClass", menuName = "Age of Stickman/Troop Class")]
public class TroopClass : ScriptableObject
{
    public string displayName;

    [TextArea]
    public string description;

    public Sprite icon;
}
