using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Siege engines by tier (made by Tools > Age of Stickman > Setup Warfare). Lives at
/// Resources/WarDatabase. Raids bring the raider engine of the latest tier your age has reached.
/// </summary>
[CreateAssetMenu(fileName = "WarDatabase", menuName = "Age of Stickman/War Database")]
public class WarDatabase : ScriptableObject
{
    public const string ResourcePath = "WarDatabase";

    [System.Serializable]
    public class SiegeTier
    {
        [Tooltip("The age (order) this tier belongs to.")]
        public int age;
        public UnitDefinition player;
        public UnitDefinition raider;
    }

    public List<SiegeTier> siege = new List<SiegeTier>();

    private static WarDatabase loaded;
    private static bool tried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { loaded = null; tried = false; }

    public static WarDatabase Load()
    {
        if (!tried) { tried = true; loaded = Resources.Load<WarDatabase>(ResourcePath); }
        return loaded;
    }

    /// <summary>The newest tier at or below this age (null before the first).</summary>
    public SiegeTier TierFor(int ageIndex)
    {
        SiegeTier best = null;
        foreach (var t in siege) if (t != null && t.age <= ageIndex && (best == null || t.age > best.age)) best = t;
        return best;
    }
}
