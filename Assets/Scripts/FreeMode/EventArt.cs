using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Art for the random events: an icon for each kind of event, the fallen star and lost cart that
/// wait on the map, and the merchant caravan (or star trader) that comes to visit. Made by
/// Tools > Age of Stickman > Setup Events, kept in Resources so the scene can find it.
/// </summary>
[CreateAssetMenu(fileName = "EventArt", menuName = "Age of Stickman/Event Art")]
public class EventArt : ScriptableObject
{
    public const string ResourcePath = "EventArt";

    public List<string> iconIds = new List<string>();
    public List<Sprite> icons = new List<Sprite>();
    public Sprite meteor, cart;
    public List<Sprite> caravanWalk = new List<Sprite>();
    public Sprite starTrader;

    private static EventArt loaded;
    private static bool tried;

    public static EventArt Get()
    {
        if (!tried) { tried = true; loaded = Resources.Load<EventArt>(ResourcePath); }
        return loaded;
    }

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { loaded = null; tried = false; }

    public Sprite IconFor(string kind)
    {
        int i = iconIds.IndexOf(kind);
        return i >= 0 && i < icons.Count ? icons[i] : null;
    }
}
