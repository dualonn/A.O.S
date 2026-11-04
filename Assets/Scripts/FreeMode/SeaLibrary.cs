using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sea's art for the scene: fishing boats (dugout to void skiff), merchant ships (round ship to
/// void barge), shoal markers, and the sea creatures (frames, and the units they become in a fight),
/// plus the pirates' ships. Made by Tools > Age of Stickman > Setup Sea (Resources/SeaLibrary). Without
/// it the sea still works; boats and ships are drawn as plain shapes.
/// </summary>
public class SeaLibrary : ScriptableObject
{
    public const string ResourcePath = "SeaLibrary";

    [Serializable]
    public class Look
    {
        [Tooltip("Fishing boats: idle x2, sailing x2, hauling x2. Ships: idle, sailing x3. Facing right, at unit pixel density.")]
        public List<Sprite> frames = new List<Sprite>();
        public float fps = 4f;
        [Tooltip("Where smoke comes out (world units from the pivot, facing right); zero for none.")]
        public Vector2 smoke;
    }

    [Serializable]
    public class Creature
    {
        public string species;
        [Tooltip("Idle, swim x4, attack x4, death x3, facing right.")]
        public List<Sprite> frames = new List<Sprite>();
        [Tooltip("The unit it becomes when it fights (an Enemy that keeps to the water); none for creatures that only flee.")]
        public UnitDefinition unit;
    }

    [Tooltip("By SeaCatalog fishing boat kind: dugout, sailing smack, steam trawler, diesel trawler, harvester drone, void skiff.")]
    public List<Look> fishingBoats = new List<Look>();
    [Tooltip("By port tier: round ship, cog, merchant galleon, steamship, container ship, hover-barge, void barge.")]
    public List<Look> merchantShips = new List<Look>();
    [Tooltip("Shoal glints (4 frames), oyster beds (4 frames), and the marker seen when zoomed out.")]
    public List<Sprite> shoalFish = new List<Sprite>(), shoalOysters = new List<Sprite>();
    public Sprite shoalBlob, oysterBlob;
    public List<Creature> creatures = new List<Creature>();
    [Tooltip("Pirates' ships by band: sloop, brig, steam corsair, hover-raider, void reaver.")]
    public List<UnitDefinition> pirates = new List<UnitDefinition>();

    [NonSerialized] private Dictionary<string, Creature> index;

    public Creature Get(string species)
    {
        if (species == null) return null;
        if (index == null || index.Count != creatures.Count)
        {
            index = new Dictionary<string, Creature>();
            foreach (var c in creatures) if (c != null && c.species != null) index[c.species] = c;
        }
        return index.TryGetValue(species, out var found) ? found : null;
    }

    private static SeaLibrary cached;
    private static bool looked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cached = null; looked = false; }

    public static SeaLibrary Load()
    {
        if (cached == null && !looked)
        {
            looked = true;
            cached = Resources.Load<SeaLibrary>(ResourcePath);
        }
        return cached;
    }
}
