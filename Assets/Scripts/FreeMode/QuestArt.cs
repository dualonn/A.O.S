using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Art and data for hero quests: a lair for each kind of story (spirit grove, wyrm's nest,
/// barrow, den, lost camp, warehouse, relay mast, rift, wreck, obelisk, outlaw hide-out), the
/// banner that marks a spot to reach, the quest icon, the named bosses quests wake, and the boons
/// they give. Made by Tools > Age of Stickman > Setup Quests, kept in Resources.
/// </summary>
[CreateAssetMenu(fileName = "QuestArt", menuName = "Age of Stickman/Quest Art")]
public class QuestArt : ScriptableObject
{
    public const string ResourcePath = "QuestArt";

    public List<string> lairIds = new List<string>();
    public List<Sprite> lairs = new List<Sprite>();
    [Tooltip("Marks a spot a hero must reach.")]
    public Sprite banner;
    [Tooltip("A small pennant over a camp or site a quest points at.")]
    public Sprite pennant;
    [Tooltip("Quest marker on the zoomed-out maps, and on the quest card.")]
    public Sprite icon;
    [Tooltip("The quest log's finished-quest (tale) icon.")]
    public Sprite taleIcon;

    [Tooltip("Named bosses quests wake (by unit asset name).")]
    public List<UnitDefinition> bosses = new List<UnitDefinition>();

    [Tooltip("Boons quests give the realm (kept with the wonders' effects so research lookups find them).")]
    public List<Technology> boons = new List<Technology>();

    private static QuestArt loaded;
    private static bool tried;

    public static QuestArt Get()
    {
        if (!tried) { tried = true; loaded = Resources.Load<QuestArt>(ResourcePath); }
        return loaded;
    }

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { loaded = null; tried = false; }

    public Sprite LairFor(string theme)
    {
        int i = lairIds.IndexOf(theme);
        return i >= 0 && i < lairs.Count ? lairs[i] : lairs.Count > 0 ? lairs[0] : null;
    }

    public UnitDefinition Boss(string id) => string.IsNullOrEmpty(id) ? null : bosses.Find(b => b != null && b.name == id);
}
