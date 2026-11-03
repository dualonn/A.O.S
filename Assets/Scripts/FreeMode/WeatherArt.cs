using UnityEngine;

/// <summary>
/// Icons for the seasons and the weather, shown in the top bar (made by Tools > Age of Stickman >
/// Setup Seasons and Weather). Kept in Resources so the HUD can find it.
/// </summary>
public class WeatherArt : ScriptableObject
{
    public const string ResourcePath = "WeatherArt";

    [Tooltip("One per Season value, in order.")]
    public Sprite[] seasons = new Sprite[0];

    [Tooltip("One per WeatherKind value, in order.")]
    public Sprite[] weather = new Sprite[0];

    private static WeatherArt cached;

    public static WeatherArt Load()
    {
        if (cached == null) cached = Resources.Load<WeatherArt>(ResourcePath);
        return cached;
    }

    public Sprite SeasonIcon(Season s) => (int)s < seasons.Length ? seasons[(int)s] : null;
    public Sprite WeatherIcon(WeatherKind k) => (int)k < weather.Length ? weather[(int)k] : null;
}
