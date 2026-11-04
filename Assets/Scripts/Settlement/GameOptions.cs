using System;
using UnityEngine;

// The choices made when a game was started (the new-game setup screen). Every default (0 / empty)
// is exactly how games played before there was a setup screen: old saves have no options and so
// play on as they always have.

/// <summary>How much land is explored when the game starts (the map itself has no edge).</summary>
public enum GameMapSize { Normal, Small, Large, Huge }

/// <summary>How strong raids are (and how much you start with).</summary>
public enum GameDifficulty { Normal, Easy, Hard, Brutal }

/// <summary>How often raids come.</summary>
public enum GameRaids { Normal, Rare, Frequent, Off }

[Serializable]
public class GameOptions
{
    [Tooltip("Starting explored land: Normal is 160x112 tiles; the map grows as you explore either way.")]
    public GameMapSize mapSize;

    public GameDifficulty difficulty;

    public GameRaids raids;

    [Tooltip("The age the game started in (earlier ages' research comes done).")]
    public int startAge;

    [Tooltip("The kind of land the home world has (a WorldProfile id; empty or Home = the classic home world).")]
    public string homeKind = "";

    [Tooltip("The map seed chosen or rolled at the start (0 = not recorded: an older game).")]
    public int seed;

    [Tooltip("Natural disasters (floods, wildfire, quakes...) never happen in this game.")]
    public bool noDisasters;

    public GameOptions Copy() => (GameOptions)MemberwiseClone();

    // ---------- Effects ----------

    /// <summary>Raid budget multiplier.</summary>
    public float RaidStrength => difficulty switch
    {
        GameDifficulty.Easy => 0.7f,
        GameDifficulty.Hard => 1.35f,
        GameDifficulty.Brutal => 1.75f,
        _ => 1f,
    };

    /// <summary>Starting stock multiplier.</summary>
    public float StartingStock => difficulty switch
    {
        GameDifficulty.Easy => 1.5f,
        GameDifficulty.Hard => 0.8f,
        GameDifficulty.Brutal => 0.6f,
        _ => 1f,
    };

    /// <summary>Time between raids multiplier.</summary>
    public float RaidInterval => raids switch
    {
        GameRaids.Rare => 1.6f,
        GameRaids.Frequent => 0.65f,
        _ => 1f,
    };

    /// <summary>Extra rings of zones explored at the start (negative: fewer).</summary>
    public int ExtraRings => mapSize switch
    {
        GameMapSize.Small => -1,
        GameMapSize.Large => 2,
        GameMapSize.Huge => 4,
        _ => 0,
    };

    // ---------- Names ----------

    public static string Name(GameMapSize v) => v switch
    {
        GameMapSize.Small => "Small (128 x 80)",
        GameMapSize.Large => "Large (224 x 176)",
        GameMapSize.Huge => "Huge (288 x 240)",
        _ => "Normal (160 x 112)",
    };

    public static string Name(GameDifficulty v) => v switch
    {
        GameDifficulty.Easy => "Easy",
        GameDifficulty.Hard => "Hard",
        GameDifficulty.Brutal => "Brutal",
        _ => "Normal",
    };

    public static string Name(GameRaids v) => v switch
    {
        GameRaids.Rare => "Rare",
        GameRaids.Frequent => "Frequent",
        GameRaids.Off => "Off (peaceful)",
        _ => "Normal",
    };

    public static string HomeName(string kind) =>
        string.IsNullOrEmpty(kind) || kind == WorldProfile.HomeId ? "Classic home world" : WorldProfile.Get(kind).displayName;

    /// <summary>A short line for the games list, e.g. "Hard · Frequent raids · Desert World".</summary>
    public string Describe(GameDatabase db)
    {
        var parts = new System.Collections.Generic.List<string>();
        parts.Add(Name(difficulty));
        if (raids == GameRaids.Off) parts.Add("no raids");
        if (noDisasters) parts.Add("no disasters");
        else if (raids != GameRaids.Normal) parts.Add($"{Name(raids).ToLowerInvariant()} raids");
        if (mapSize != GameMapSize.Normal) parts.Add($"{mapSize.ToString().ToLowerInvariant()} start");
        if (startAge > 0 && db != null && db.GetAge(startAge) != null) parts.Add($"began in the {db.GetAge(startAge).displayName}");
        if (!string.IsNullOrEmpty(homeKind) && homeKind != WorldProfile.HomeId) parts.Add(HomeName(homeKind));
        return string.Join(" · ", parts);
    }
}
