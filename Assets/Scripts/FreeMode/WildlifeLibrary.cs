using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The art and units of every wild creature: 12 frames for the animals you see roaming (idle,
/// walk x4, attack x4, death x3, facing right), the wild unit a herd turns into when it fights, and
/// the war beast it becomes when tamed. Made by Tools > Age of Stickman > Setup Wildlife; lives at
/// Resources/WildlifeLibrary. Without it, herds still live in the simulation but aren't drawn.
/// </summary>
[CreateAssetMenu(fileName = "WildlifeLibrary", menuName = "Age of Stickman/Wildlife Library")]
public class WildlifeLibrary : ScriptableObject
{
    public const string ResourcePath = "WildlifeLibrary";

    [Serializable]
    public class Entry
    {
        [Tooltip("Species id (see Settlement.WildCatalogue).")]
        public string species;
        [Tooltip("Idle, walk x4, attack x4, death x3, facing right, at unit pixel density.")]
        public List<Sprite> frames = new List<Sprite>();
        [Tooltip("The wild unit (a WildBeast) a herd member becomes when it flees no more.")]
        public UnitDefinition wild;
        [Tooltip("The war beast it's tamed into (a Troop), if it can be tamed.")]
        public UnitDefinition tame;
        [Tooltip("Flies: drawn off the ground, with a shadow below.")]
        public bool flyer;
    }

    public List<Entry> entries = new List<Entry>();

    [NonSerialized] private Dictionary<string, Entry> index;
    [NonSerialized] private int indexCount = -1;

    public Entry Get(string species)
    {
        if (species == null) return null;
        if (index == null || indexCount != entries.Count)
        {
            index = new Dictionary<string, Entry>();
            foreach (var e in entries) if (e != null && e.species != null) index[e.species] = e;
            indexCount = entries.Count;
        }
        return index.TryGetValue(species, out var found) ? found : null;
    }

    private static WildlifeLibrary cached;
    private static bool looked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cached = null; looked = false; }

    public static WildlifeLibrary Load()
    {
        if (cached == null && !looked)
        {
            looked = true;
            cached = Resources.Load<WildlifeLibrary>(ResourcePath);
        }
        return cached;
    }
}
