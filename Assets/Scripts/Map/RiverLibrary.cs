using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rivers-and-sea things the scene needs that aren't buildings: the raiders' boats by age band
/// (they carry a raid in from the sea), and a splash. Made by Tools > Age of Stickman > Setup Rivers
/// (Assets/Resources/RiverLibrary.asset).
/// </summary>
public class RiverLibrary : ScriptableObject
{
    [Tooltip("Raiders' boats, by age band: dugout canoes, longboats, steam launches, hover-skiffs.")]
    public List<UnitDefinition> raidBoats = new List<UnitDefinition>();

    [Tooltip("A little splash where troops climb aboard or wade ashore.")]
    public Sprite splash;

    private static RiverLibrary cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    public static RiverLibrary Load()
    {
        if (cached == null) cached = Resources.Load<RiverLibrary>("RiverLibrary");
        return cached;
    }
}
