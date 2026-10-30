using UnityEngine;

/// <summary>A stockpiled resource such as Food, Wood, Stone or Bronze.</summary>
[CreateAssetMenu(fileName = "NewResource", menuName = "Age of Stickman/Resource")]
public class ResourceType : GameData
{
    public string displayName;
    public Sprite icon;

    [Tooltip("The age this resource first appears in. Leave empty for the first age.")]
    public Age unlockedIn;
}
