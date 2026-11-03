using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a world is like to live on. A planet's traits (thin or toxic air, gravity, storms, long
/// nights, rich land...) change how its settlements produce, grow and fight, and some bring
/// hazards. Space research answers them: domes for the air, planetary shields against storms and
/// quakes, terraforming to make alien land kinder, xenology against native life, orbital defence
/// over your Starport worlds, and precursor science from the ruins out there.
/// </summary>
public partial class Settlement
{
    public const string ThinAir = "Thin Air", ToxicAir = "Toxic Air", LowGravity = "Low Gravity", HighGravity = "High Gravity",
        Storms = "Storms", Quakes = "Quakes", LongNights = "Long Nights", Fertile = "Fertile", ManaRich = "Mana-rich",
        StarmetalCraters = "Starmetal Craters", RichVeins = "Rich Veins", HostileFauna = "Hostile Fauna", AncientRuins = "Ancient Ruins",
        Scorching = "Scorching", RedDust = "Red Dust";

    /// <summary>Plain-language effects of each trait, for the star map.</summary>
    public static string TraitEffect(string trait) => trait switch
    {
        ThinAir => "production -10% (domes fix it)",
        ToxicAir => "people grow at half speed (domes fix it)",
        LowGravity => "units move 12% faster",
        HighGravity => "units move 10% slower; buildings 15% tougher",
        Storms => "storms damage buildings now and then (planetary shields stop it)",
        Quakes => "quakes damage buildings now and then (planetary shields stop it)",
        LongNights => "food -15% (terraforming helps)",
        Fertile => "food +20%, people grow faster",
        ManaRich => "mana +25%",
        StarmetalCraters => "starmetal +25%",
        RichVeins => "stone, iron and gold +15%",
        HostileFauna => "raids 20% stronger",
        AncientRuins => "more precursor ruins and vaults",
        Scorching => "food -10%, coal and fuel +20%",
        RedDust => "stone +15%",
        _ => "",
    };

    // The current world's traits, worked out again when you change worlds (or a background world ticks).
    private List<string> traitsCache;
    private WorldState traitsFor;

    private List<string> Traits
    {
        get
        {
            if (traitsFor != CurrentWorld || traitsCache == null)
            {
                traitsFor = CurrentWorld;
                traitsCache = PlanetOf(CurrentWorld)?.traits ?? new List<string>();
            }
            return traitsCache;
        }
    }

    public bool HasTrait(string t) => Traits.Contains(t);
    private bool Researched(string techId) => State.researched.Contains(techId);
    private int Levels(string techId) { int n = 0; foreach (var id in State.researched) if (id == techId) n++; return n; }

    /// <summary>The world you're on is an alien one (not the home world).</summary>
    private bool AlienHere => CurrentWorld.profile != WorldProfile.HomeId;

    private float genesisAt = -99f, genesisFactor = 1f;

    /// <summary>
    /// Each standing Genesis Engine, on any of your worlds, makes every world produce 5% more
    /// (up to 5 engines). Counted again once a second.
    /// </summary>
    public float GenesisFactor
    {
        get
        {
            if (Mathf.Abs(State.playTime - genesisAt) < 1f) return genesisFactor;
            genesisAt = State.playTime;
            int n = 0;
            foreach (var w in State.worlds)
                foreach (var b in w.buildings)
                    if (b.IsBuilt && b.health > 0 && Database.GetBuilding(b.definitionId)?.genesisEngine == true) n++;
            genesisFactor = 1f + 0.05f * Mathf.Min(5, n);
            return genesisFactor;
        }
    }

    /// <summary>This world's effect on a resource's production (traits, terraforming).</summary>
    public float WorldProductionFactor(ResourceType r)
    {
        if (r == null) return 1f;
        float f = GenesisFactor * SeasonProductionFactor(r);
        f *= NaturalWonderFactor(r);
        f *= CultureProductionFactor(r);   // mood, traditions, tenets, great people
        bool domes = Researched("Domes");
        if (HasTrait(ThinAir) && !domes) f *= 0.9f;
        string id = r.id;
        if (id == "Food")
        {
            if (HasTrait(LongNights)) f *= 0.85f + 0.05f * Mathf.Min(3, Levels("Terraforming"));
            if (HasTrait(Scorching)) f *= 0.9f;
            if (HasTrait(Fertile)) f *= 1.2f;
            if (AlienHere) f *= 1f + 0.06f * Levels("Terraforming");
        }
        if (id == "Mana" && HasTrait(ManaRich)) f *= 1.25f;
        if (id == "Starmetal" && HasTrait(StarmetalCraters)) f *= 1.25f;
        if ((id == "Stone" || id == "Iron" || id == "Gold") && HasTrait(RichVeins)) f *= 1.15f;
        if (id == "Stone" && HasTrait(RedDust)) f *= 1.15f;
        if ((id == "Coal" || id == "Fuel") && HasTrait(Scorching)) f *= 1.2f;
        f *= EventProductionFactor(r);   // random events (droughts, gold rushes, festivals...)
        return f;
    }

    /// <summary>How fast people move in on this world (1 = normal).</summary>
    public float WorldGrowthFactor
    {
        get
        {
            float f = SeasonGrowthFactor;
            if (HasTrait(ToxicAir) && !Researched("Domes")) f *= 0.5f;
            if (HasTrait(Fertile)) f *= 1.2f;
            if (AlienHere) f *= 1f + 0.06f * Levels("Terraforming");
            f *= EventGrowthFactor;   // random events (festivals, feasts...)
            f *= NaturalWonderGrowth;
            f *= CultureGrowthFactor;
            return f;
        }
    }

    /// <summary>Gravity: how fast units move on the world you're looking at.</summary>
    public float UnitSpeedFactor => (HasTrait(LowGravity) ? 1.12f : HasTrait(HighGravity) ? 0.9f : 1f) * WeatherFx.Speed;

    /// <summary>High gravity makes squat, tough buildings.</summary>
    public float WorldBuildingHealthFactor => HasTrait(HighGravity) ? 1.15f : 1f;

    /// <summary>Raids are fiercer where the wildlife joins in.</summary>
    public float WorldThreatFactor => (HasTrait(HostileFauna) ? 1.2f : 1f) * EventThreatFactor * CultureThreatFactor;

    /// <summary>Xenology: your troops hit the natives of other worlds harder.</summary>
    public float XenologyFactor => 1f + 0.08f * Levels("Xenology");

    /// <summary>Orbital defence: worlds with a Starport count for more against raids (and strike from orbit).</summary>
    public int OrbitalLevel => HasStarport ? Levels("OrbitalDefence") : 0;

    // ---------- Hazards: storms and quakes ----------

    private void TickHazards(float dt)
    {
        bool storms = HasTrait(Storms), quakes = HasTrait(Quakes);
        if (!storms && !quakes) return;
        CurrentWorld.hazardIn -= dt;
        if (CurrentWorld.hazardIn > 0) return;
        CurrentWorld.hazardIn = Random.Range(150f, 300f);
        string what = storms && (!quakes || Random.value < 0.5f) ? "A storm" : "A quake";
        if (Researched("PlanetaryShields"))
        {
            Notify($"{what} broke against the planetary shields. Nothing was damaged.");
            if (!offWorld) GameAudio.UI("force_field");
            return;
        }
        int hit = 0;
        var built = State.buildings.FindAll(b => b.IsBuilt && b.health > 0);
        for (int i = 0; i < 3 && built.Count > 0; i++)
        {
            var b = built[Random.Range(0, built.Count)];
            var stats = StatsOf(b);
            b.health = Mathf.Max(1f, b.health - Mathf.Round(stats.maxHealth * Random.Range(0.08f, 0.15f)));
            hit++;
        }
        if (hit > 0)
        {
            Notify($"{what} damaged {hit} building{(hit == 1 ? "" : "s")}. (Planetary shields would stop this.)");
            if (!offWorld) { if (what == "A quake") GameAudio.UI("earthquake"); else GameAudio.Thunder(); }
            RaiseChanged();
        }
    }
}
