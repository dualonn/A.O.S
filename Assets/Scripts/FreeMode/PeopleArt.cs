using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Art for people: the decorations comfortable homes put out (planters, lamps, benches, banners,
/// fountains, holo-plants), in four styles by age, three levels each. Made by
/// Tools > Age of Stickman > Setup People, kept in Resources.
/// </summary>
[CreateAssetMenu(fileName = "PeopleArt", menuName = "Age of Stickman/People Art")]
public class PeopleArt : ScriptableObject
{
    public const string ResourcePath = "PeopleArt";
    public const int Styles = 4;

    [Tooltip("Style * 3 + (level - 1): ancient, medieval, industrial, far future; comfortable, prosperous, grand.")]
    public List<Sprite> decor = new List<Sprite>();

    private static PeopleArt loaded;
    private static bool tried;

    public static PeopleArt Get()
    {
        if (!tried) { tried = true; loaded = Resources.Load<PeopleArt>(ResourcePath); }
        return loaded;
    }

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { loaded = null; tried = false; }

    /// <summary>The style of decoration for homes of an age.</summary>
    public static int StyleOf(int ageOrder) => ageOrder <= 3 ? 0 : ageOrder <= 6 ? 1 : ageOrder <= 10 ? 2 : 3;

    public Sprite DecorFor(int ageOrder, int level)
    {
        if (level <= 0) return null;
        int i = StyleOf(ageOrder) * 3 + Mathf.Clamp(level, 1, 3) - 1;
        return i < decor.Count ? decor[i] : null;
    }
}
