using System;
using UnityEngine;

/// <summary>
/// What a world keeps in the save about its utilities (see Utilities.cs). The networks themselves
/// (water, power, data) and the pollution in the air are worked out again from the buildings, so
/// only the phase-in for old saves is stored.
/// </summary>
[Serializable]
public class UtlWorldSave
{
    [Tooltip("0 = utilities never looked at this world yet (an old save, or a new world); 1 = set up.")]
    public int version;

    [Tooltip("Play time until which an old save's works don't feel the lack of power, water or clean air.")]
    public float graceUntil;

    [Tooltip("Times the grid has run short (brownouts) on this world.")]
    public int brownouts;
}
