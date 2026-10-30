using UnityEngine;

/// <summary>
/// Base for data assets that are referenced from save files.
/// The id is what gets saved, so keep it stable once players have saves (renaming the asset is fine).
/// </summary>
public abstract class GameData : ScriptableObject
{
    [Tooltip("Saved in save files. Filled from the asset name if left empty.")]
    public string id;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = name;
        }
    }
}
