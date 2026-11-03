using System;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>The seasons of a world's year (which ones it has depends on its climate).</summary>
public enum Season { Spring, Summer, Autumn, Winter, DrySeason, WetSeason, Rains, Still }

/// <summary>The weather over a world right now.</summary>
public enum WeatherKind { Clear, Rain, Storm, Snow, Fog, HeatWave, Sandstorm, AshFall, Aurora }

/// <summary>What kind of year a world has: from the land its people landed on.</summary>
public enum Climate { Temperate, Cold, Arid, Tropical, Volcanic, Airless }

/// <summary>
/// The weather's effect on fighting on the world you're looking at, kept here so units can read
/// it cheaply (Settlement updates it as the weather changes). All 1 when there's no weather.
/// </summary>
public static class WeatherFx
{
    /// <summary>How fast everyone on foot moves (rain, snow and sandstorms slow them).</summary>
    public static float Speed = 1f;
    /// <summary>Ranged damage (storms and sandstorms spoil aim).</summary>
    public static float RangedDamage = 1f;
    /// <summary>How far ranged units shoot from (fog and sandstorms make them come closer).</summary>
    public static float RangedReach = 1f;
    /// <summary>Fire bursts (flasks, fire shot): rain and storms damp them.</summary>
    public static float Fire = 1f;

    public static int Ranged(int damage) => RangedDamage >= 0.999f ? damage : Mathf.Max(1, Mathf.RoundToInt(damage * RangedDamage));

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Speed = 1f; RangedDamage = 1f; RangedReach = 1f; Fire = 1f; }
}

/// <summary>
/// Seasons and weather. Every world has a year (four seasons; a dry and a wet season in deserts;
/// rains in the tropics) and weather that comes and goes with it. Effects are small: a few percent
/// on farms, growth and fighting. Space research softens the bad ones: terraforming the seasons,
/// domes the weather, and planetary shields stop lightning. Worlds you're not on keep their year too.
/// </summary>
public partial class Settlement
{
    /// <summary>How long a season lasts (seconds of game time).</summary>
    public const float SeasonLength = 300f;

    /// <summary>A lightning bolt hit one of your buildings on the world you're looking at.</summary>
    public event Action<BuildingState> LightningStruck;

    /// <summary>The season or weather on the world you're looking at changed.</summary>
    public event Action WeatherChanged;

    public static Climate ClimateOf(WorldState w)
    {
        if (w == null) return Climate.Temperate;
        var p = WorldProfile.Get(w.profile);
        if (p.id == WorldProfile.HomeId) return Climate.Temperate;
        return p.homeBiome switch
        {
            Biome.Tundra => Climate.Cold,
            Biome.Desert => Climate.Arid,
            Biome.Jungle or Biome.Swamp or Biome.Fungal => Climate.Tropical,
            Biome.Volcanic => Climate.Volcanic,
            Biome.Barren => Climate.Airless,
            _ => Climate.Temperate,
        };
    }

    public Climate CurrentClimate => ClimateOf(CurrentWorld);

    /// <summary>The seasons of a climate's year, in order.</summary>
    private static Season[] SeasonsOf(Climate c) => c switch
    {
        Climate.Arid => new[] { Season.DrySeason, Season.DrySeason, Season.WetSeason },
        Climate.Tropical => new[] { Season.Rains, Season.DrySeason },
        Climate.Airless => new[] { Season.Still },
        _ => new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter },
    };

    public static Season SeasonOf(WorldState w)
    {
        var seasons = SeasonsOf(ClimateOf(w));
        int i = Mathf.FloorToInt(Mathf.Max(0f, w.seasonTime) / SeasonLength) % seasons.Length;
        return seasons[i];
    }

    /// <summary>Seconds left in the current season.</summary>
    public static float SeasonLeft(WorldState w) => SeasonLength - Mathf.Repeat(Mathf.Max(0f, w.seasonTime), SeasonLength);

    public Season CurrentSeason => SeasonOf(CurrentWorld);
    public WeatherKind CurrentWeather => (WeatherKind)CurrentWorld.weather;

    public static string SeasonName(Season s) => s switch
    {
        Season.DrySeason => "Dry Season",
        Season.WetSeason => "Wet Season",
        Season.Rains => "The Rains",
        Season.Still => "No Seasons",
        _ => s.ToString(),
    };

    public static string WeatherName(WeatherKind k) => k switch
    {
        WeatherKind.HeatWave => "Heat Wave",
        WeatherKind.AshFall => "Ash Fall",
        _ => k.ToString(),
    };

    // ---------- Effects ----------

    /// <summary>How much bad seasons are softened by terraforming (each level a quarter, up to three).</summary>
    private float SeasonSoftening => 1f - 0.25f * Mathf.Min(3, Levels("Terraforming"));

    /// <summary>How much bad weather is softened: domes halve it.</summary>
    private float WeatherSoftening => Researched("Domes") ? 0.5f : 1f;

    /// <summary>A penalty (factor below 1) softened by 'soften' (1 = full penalty, 0 = none).</summary>
    private static float Soft(float factor, float soften) => factor >= 1f ? factor : 1f - (1f - factor) * soften;

    /// <summary>The season and weather's effect on a resource's production on the world you're on.</summary>
    public float SeasonProductionFactor(ResourceType r)
    {
        if (r == null) return 1f;
        string id = r.id;
        float f = 1f;
        if (id == "Food")
        {
            bool cold = CurrentClimate == Climate.Cold;
            float s = CurrentSeason switch
            {
                Season.Spring => 1.05f,
                Season.Summer => cold ? 1.08f : 1.12f,
                Season.Autumn => 1.08f,
                Season.Winter => cold ? 0.85f : 0.9f,
                Season.DrySeason => CurrentClimate == Climate.Tropical ? 0.95f : 0.9f,
                Season.WetSeason => 1.12f,
                Season.Rains => 1.1f,
                _ => 1f,
            };
            f *= Soft(s, SeasonSoftening);
            float w = CurrentWeather switch
            {
                WeatherKind.HeatWave => 0.95f,
                WeatherKind.AshFall => 0.92f,
                WeatherKind.Rain => 1.03f,
                _ => 1f,
            };
            f *= Soft(w, WeatherSoftening);
        }
        if (id == "Wood" && CurrentSeason == Season.Autumn) f *= 1.05f;
        if (id == "Mana" && CurrentWeather == WeatherKind.Aurora) f *= 1.1f;
        return f;
    }

    /// <summary>The season's effect on how fast people move in.</summary>
    public float SeasonGrowthFactor => CurrentSeason switch
    {
        Season.Spring => 1.15f,
        Season.WetSeason or Season.Rains => 1.08f,
        Season.Winter => Soft(0.9f, SeasonSoftening),
        _ => 1f,
    };

    /// <summary>People eat a little more in winter.</summary>
    public float SeasonFoodUpkeepFactor => CurrentSeason == Season.Winter ? 1f + 0.1f * SeasonSoftening : 1f;

    /// <summary>The weather's effect on how fast everyone on foot moves (read through WeatherFx).</summary>
    private float WeatherSpeedOf(WeatherKind k) => Soft(k switch
    {
        WeatherKind.Rain => 0.94f,
        WeatherKind.Storm => 0.9f,
        WeatherKind.Snow => 0.88f,
        WeatherKind.Sandstorm => 0.85f,
        WeatherKind.HeatWave or WeatherKind.AshFall => 0.95f,
        _ => 1f,
    }, WeatherSoftening);

    /// <summary>Move the weather's effect on fighting into WeatherFx (for the world you're on).</summary>
    private void ApplyWeatherFx()
    {
        var k = CurrentWeather;
        float soft = WeatherSoftening;
        WeatherFx.Speed = WeatherSpeedOf(k);
        WeatherFx.RangedDamage = Soft(k == WeatherKind.Storm ? 0.88f : k == WeatherKind.Sandstorm ? 0.85f : 1f, soft);
        WeatherFx.RangedReach = Soft(k == WeatherKind.Fog ? 0.85f : k == WeatherKind.Sandstorm ? 0.85f : 1f, soft);
        WeatherFx.Fire = Soft(k == WeatherKind.Rain ? 0.6f : k == WeatherKind.Storm ? 0.5f : k == WeatherKind.Snow ? 0.75f : 1f, soft);
    }

    /// <summary>Plain-language effects of the season and weather, for the HUD's tooltip.</summary>
    public string SeasonWeatherTooltip()
    {
        var w = CurrentWorld;
        var season = CurrentSeason;
        var weather = CurrentWeather;
        var sb = new StringBuilder();
        sb.Append($"<b>{SeasonName(season)}</b>");
        if (season != Season.Still) sb.Append($"  <color=#aaa>{StarMapPanel.FormatTime(SeasonLeft(w))} left</color>");
        string se = season switch
        {
            Season.Spring => "Farms +5%, people move in 15% faster.",
            Season.Summer => CurrentClimate == Climate.Cold ? "Farms +8%." : "Farms +12%.",
            Season.Autumn => "Harvest: farms +8%, wood +5%.",
            Season.Winter => CurrentClimate == Climate.Cold ? "Farms -15%, people eat 10% more and move in 10% slower." : "Farms -10%, people eat 10% more and move in 10% slower.",
            Season.DrySeason => CurrentClimate == Climate.Tropical ? "Farms -5%." : "Farms -10%.",
            Season.WetSeason => "Farms +12%, people move in 8% faster.",
            Season.Rains => "Farms +10%, people move in 8% faster.",
            _ => "No air, no weather: the same all year round.",
        };
        sb.Append('\n').Append(se);
        if (Levels("Terraforming") > 0 && season != Season.Still) sb.Append(" <color=#9c9>(terraforming softens the bad parts)</color>");

        sb.Append($"\n\n<b>{WeatherName(weather)}</b>");
        if (w.weatherLeft > 0 && weather != WeatherKind.Clear) sb.Append($"  <color=#aaa>about {StarMapPanel.FormatTime(w.weatherLeft)} more</color>");
        string we = weather switch
        {
            WeatherKind.Rain => "Everyone on foot 6% slower; fire bursts weaker; farms +3%.",
            WeatherKind.Storm => "Everyone on foot 10% slower; ranged attacks 12% weaker; fire damped. Lightning can strike buildings.",
            WeatherKind.Snow => "Everyone on foot 12% slower; fire bursts weaker.",
            WeatherKind.Fog => "Ranged units have to come 15% closer to shoot.",
            WeatherKind.HeatWave => "Farms -5%; everyone on foot 5% slower.",
            WeatherKind.Sandstorm => "Everyone on foot 15% slower; ranged attacks 15% weaker and shorter.",
            WeatherKind.AshFall => "Farms -8%; everyone on foot 5% slower.",
            WeatherKind.Aurora => "Lights in the sky: mana +10%.",
            _ => "Fair weather: nothing in the way.",
        };
        sb.Append('\n').Append(we);
        if (weather != WeatherKind.Clear && weather != WeatherKind.Aurora)
        {
            if (Researched("Domes")) sb.Append(" <color=#9c9>(domes halve it)</color>");
            if (weather == WeatherKind.Storm && Researched("PlanetaryShields")) sb.Append(" <color=#9c9>(planetary shields stop lightning)</color>");
        }
        return sb.ToString();
    }

    // ---------- The year and the weather ----------

    /// <summary>Pick the next weather for a climate and season (weights per kind).</summary>
    private static WeatherKind RollWeather(Climate c, Season s)
    {
        (WeatherKind kind, float weight)[] table = c switch
        {
            Climate.Airless => new[] { (WeatherKind.Clear, 1f) },
            Climate.Cold => s == Season.Winter
                ? new[] { (WeatherKind.Snow, 5f), (WeatherKind.Clear, 2f), (WeatherKind.Aurora, 2f), (WeatherKind.Fog, 1f) }
                : s == Season.Summer
                    ? new[] { (WeatherKind.Clear, 4f), (WeatherKind.Rain, 1.5f), (WeatherKind.Aurora, 1f), (WeatherKind.Fog, 1f) }
                    : new[] { (WeatherKind.Clear, 3f), (WeatherKind.Snow, 2f), (WeatherKind.Aurora, 1.5f), (WeatherKind.Fog, 1f) },
            Climate.Arid => s == Season.WetSeason
                ? new[] { (WeatherKind.Clear, 3f), (WeatherKind.Rain, 3f), (WeatherKind.Storm, 1.5f), (WeatherKind.Sandstorm, 0.5f) }
                : new[] { (WeatherKind.Clear, 5f), (WeatherKind.HeatWave, 2f), (WeatherKind.Sandstorm, 2f) },
            Climate.Tropical => s == Season.Rains
                ? new[] { (WeatherKind.Rain, 5f), (WeatherKind.Storm, 2.5f), (WeatherKind.Clear, 1.5f), (WeatherKind.Fog, 1f) }
                : new[] { (WeatherKind.Clear, 4f), (WeatherKind.HeatWave, 1.5f), (WeatherKind.Fog, 1.5f), (WeatherKind.Rain, 1f) },
            Climate.Volcanic => new[] { (WeatherKind.Clear, 3f), (WeatherKind.AshFall, 4f), (WeatherKind.Storm, 1f), (WeatherKind.HeatWave, 1f) },
            _ => s switch
            {
                Season.Spring => new[] { (WeatherKind.Clear, 4f), (WeatherKind.Rain, 3f), (WeatherKind.Fog, 1f), (WeatherKind.Storm, 1f) },
                Season.Summer => new[] { (WeatherKind.Clear, 5f), (WeatherKind.Storm, 1.5f), (WeatherKind.HeatWave, 1f), (WeatherKind.Rain, 1f) },
                Season.Autumn => new[] { (WeatherKind.Clear, 3f), (WeatherKind.Rain, 3f), (WeatherKind.Fog, 2f), (WeatherKind.Storm, 1f) },
                _ => new[] { (WeatherKind.Clear, 3f), (WeatherKind.Snow, 4f), (WeatherKind.Fog, 1.5f) },
            },
        };
        float total = 0f;
        foreach (var e in table) total += e.weight;
        float pick = Random.value * total;
        foreach (var e in table)
        {
            pick -= e.weight;
            if (pick <= 0f) return e.kind;
        }
        return table[0].kind;
    }

    private static string WeatherNews(WeatherKind k) => k switch
    {
        WeatherKind.Storm => "A storm rolls in. Lightning may strike.",
        WeatherKind.Sandstorm => "A sandstorm sweeps over the land.",
        WeatherKind.HeatWave => "A heat wave settles in. Farms suffer.",
        WeatherKind.AshFall => "Ash is falling from the mountains.",
        WeatherKind.Aurora => "Lights dance in the sky. Mana flows a little stronger.",
        _ => null,
    };

    private static string SeasonNews(Season s) => s switch
    {
        Season.Spring => "Spring: the land wakes up, and more people come.",
        Season.Summer => "Summer: the fields are at their best.",
        Season.Autumn => "Autumn: harvest time.",
        Season.Winter => "Winter: farms slow down and people eat more.",
        Season.DrySeason => "The dry season begins.",
        Season.WetSeason => "The wet season: the land turns green.",
        Season.Rains => "The rains have come.",
        _ => null,
    };

    /// <summary>Move the year on, change the weather now and then, and let storms throw lightning.</summary>
    private void TickSeasons(float dt)
    {
        var w = CurrentWorld;
        var before = SeasonOf(w);
        w.seasonTime += dt;
        var season = SeasonOf(w);
        bool changed = false;
        if (season != before)
        {
            changed = true;
            if (!offWorld)
            {
                string news = SeasonNews(season);
                if (news != null) Notify(news);
            }
            w.weatherLeft = Mathf.Min(w.weatherLeft, Random.Range(5f, 25f));   // a new season soon brings its own weather
        }

        w.weatherLeft -= dt;
        if (w.weatherLeft <= 0f)
        {
            var old = (WeatherKind)w.weather;
            var next = RollWeather(ClimateOf(w), season);
            w.weather = (int)next;
            w.weatherLeft = next == WeatherKind.Clear ? Random.Range(60f, 150f) : Random.Range(40f, 110f);
            w.lightningIn = Random.Range(20f, 50f);
            if (next != old)
            {
                changed = true;
                if (!offWorld)
                {
                    string news = WeatherNews(next);
                    if (news != null) Notify(news);
                }
            }
        }

        if ((WeatherKind)w.weather == WeatherKind.Storm && !Researched("PlanetaryShields"))
        {
            w.lightningIn -= dt;
            if (w.lightningIn <= 0f)
            {
                w.lightningIn = Random.Range(35f, 80f);
                Lightning();
            }
        }

        if (!offWorld)
        {
            ApplyWeatherFx();
            if (changed) WeatherChanged?.Invoke();
        }
    }

    /// <summary>A bolt hits a random building: a little damage, never a ruin.</summary>
    private void Lightning()
    {
        var built = State.buildings.FindAll(b => b.IsBuilt && b.health > 1f && Database.GetBuilding(b.definitionId) is BuildingDefinition d && !d.IsWall);
        if (built.Count == 0) return;
        var b = built[Random.Range(0, built.Count)];
        var stats = StatsOf(b);
        b.health = Mathf.Max(1f, b.health - Mathf.Round(stats.maxHealth * Random.Range(0.03f, 0.06f)));
        if (!offWorld) LightningStruck?.Invoke(b);
        else Notify("Lightning struck one of your buildings.");
        RaiseChanged();
    }
}
