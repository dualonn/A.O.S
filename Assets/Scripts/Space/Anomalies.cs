using System.Collections.Generic;
using UnityEngine;

/// <summary>Strange things out among the stars, found when a system is scanned.</summary>
public enum AnomalyKind { None, Derelict, Nebula, Wormhole, AncientGate, AlienSignal, Comet, BlackHole, VoidRift, PrecursorBeacon, CrystalStorm }

/// <summary>An anomaly's story and its two choices.</summary>
public class AnomalyInfo
{
    public AnomalyKind kind;
    public string title, text, choiceA, choiceB;
}

public static partial class Galaxy
{
    /// <summary>About one system in four holds something strange (never the home system).</summary>
    public static AnomalyKind AnomalyIn(StarSystem s)
    {
        if (s.IsHome) return AnomalyKind.None;
        float r = (Hash(s.key.x, s.key.y, 77, Seed) & 0xFFFF) / 65536f;
        if (r > 0.27f) return AnomalyKind.None;
        int kinds = System.Enum.GetValues(typeof(AnomalyKind)).Length - 1;
        int k = 1 + Hash(s.key.x, s.key.y, 78, Seed) % kinds;
        if (s.star == StarType.VoidTouched) return AnomalyKind.VoidRift;
        if (s.star == StarType.Neutron && k % 2 == 0) return AnomalyKind.BlackHole;
        return (AnomalyKind)k;
    }

    public static AnomalyInfo Describe(AnomalyKind kind) => kind switch
    {
        AnomalyKind.Derelict => new AnomalyInfo { kind = kind, title = "A Derelict Ship",
            text = "A ship of no make you know drifts dark among the asteroids, its hull scored by something with teeth.",
            choiceA = "Board it (rich, but risky)", choiceB = "Strip its hull from outside (safe)" },
        AnomalyKind.Nebula => new AnomalyInfo { kind = kind, title = "A Glowing Nebula",
            text = "A cloud of charged gas, bright as a sunset, hums on every frequency your instruments have.",
            choiceA = "Harvest its gas (power cells and mana)", choiceB = "Study it (speeds your current research)" },
        AnomalyKind.Wormhole => new AnomalyInfo { kind = kind, title = "A Wormhole",
            text = "Space folds in on itself here; light from a far-off star leaks through the fold.",
            choiceA = "Chart it (a shortcut to a distant system)", choiceB = "Study its edge (research and essence)" },
        AnomalyKind.AncientGate => new AnomalyInfo { kind = kind, title = "An Ancient Gate",
            text = "A ring of black stone the size of a moon, runes along its rim that match your old shrines.",
            choiceA = "Wake it (it shows you the stars round it)", choiceB = "Strip it (starmetal and cores)" },
        AnomalyKind.AlienSignal => new AnomalyInfo { kind = kind, title = "An Alien Signal",
            text = "A voice on the void, counting slowly, then asking a question in seven languages, one of them yours.",
            choiceA = "Answer it (a gift, or trouble)", choiceB = "Record it (research and lore)" },
        AnomalyKind.Comet => new AnomalyInfo { kind = kind, title = "A Great Comet",
            text = "A mountain of ice and metal falling round the star on a thousand-year orbit.",
            choiceA = "Mine it (stone, iron and water-ice)", choiceB = "Study its tail (research)" },
        AnomalyKind.BlackHole => new AnomalyInfo { kind = kind, title = "A Black Hole",
            text = "Where the star should be, nothing: a ring of fire round a hole in the sky.",
            choiceA = "Observe it from a safe distance (big research)", choiceB = "Sling a probe round it (star drive data)" },
        AnomalyKind.VoidRift => new AnomalyInfo { kind = kind, title = "A Void Rift",
            text = "A tear in space, magenta at the edges. Things move on the other side and look back.",
            choiceA = "Seal it (essence, safety)", choiceB = "Reach in (a strange prize, if it doesn't reach back)" },
        AnomalyKind.PrecursorBeacon => new AnomalyInfo { kind = kind, title = "A Precursor Beacon",
            text = "A beacon left by whoever built the gates, still blinking after a million years.",
            choiceA = "Decode it (lore, and the stars it points at)", choiceB = "Take it apart (precursor alloys)" },
        AnomalyKind.CrystalStorm => new AnomalyInfo { kind = kind, title = "A Crystal Storm",
            text = "A storm of floating crystals, singing as they collide, some as big as houses.",
            choiceA = "Net the crystals (mana and manastone)", choiceB = "Listen to the song (research)" },
        _ => null,
    };
}

/// <summary>
/// Anomalies: when a probe scans a system that holds one, it shows on the star map with two
/// choices. Each choice gives something (resources to the world you're on, an item, research,
/// lore, charts of far stars) and some carry a risk. What you found is kept in the codex.
/// </summary>
public partial class Settlement
{
    public AnomalyKind PendingAnomaly(StarSystem s)
    {
        if (!IsScanned(s) || State.anomaliesDone.Contains(s.key)) return AnomalyKind.None;
        return Galaxy.AnomalyIn(s);
    }

    /// <summary>Settle an anomaly: choice 0 or 1. Returns what happened.</summary>
    public string ResolveAnomaly(StarSystem s, int choice)
    {
        var kind = PendingAnomaly(s);
        if (kind == AnomalyKind.None) return null;
        State.anomaliesDone.Add(s.key);
        float roll = (Galaxy.Hash(s.key.x, s.key.y, 91 + choice, State.mapSeed) & 0xFFFF) / 65536f;
        string outcome = kind switch
        {
            AnomalyKind.Derelict => choice == 0
                ? (roll < 0.7f ? Give("Your crew found its hold intact", ("Steel", 1500), ("Gears", 600), ("Microchips", 200)) + Item(0.6f, Rarity.Rare)
                               : "Something was still alive in there. The boarding party barely got out.")
                : Give("Its plating is good scrap", ("Steel", 600), ("Iron", 800)),
            AnomalyKind.Nebula => choice == 0 ? Give("The tanks are full", ("PowerCells", 400), ("Mana", 200)) : Research(0.5f),
            AnomalyKind.Wormhole => choice == 0 ? ChartWormhole(s) : Research(0.3f) + Give("", ("Essence", 120)),
            AnomalyKind.AncientGate => choice == 0 ? WakeGate(s) : Give("Its stone is starmetal through and through", ("Starmetal", 400), ("AstralCores", 80)),
            AnomalyKind.AlienSignal => choice == 0
                ? (roll < 0.5f ? "They answered with a gift." + Item(1.2f, Rarity.Epic) : RaidHere("They answered by coming to look. Raiders are on their way to you!"))
                : Research(0.4f) + " The recording joins your archive.",
            AnomalyKind.Comet => choice == 0 ? Give("Ice and ore by the tonne", ("Stone", 3000), ("Iron", 1200), ("Food", 1000)) : Research(0.4f),
            AnomalyKind.BlackHole => choice == 0 ? Research(1f) : Give("The probe's last data sharpen your drives", ("PowerCells", 300)) + FreeLevel("StarDrives"),
            AnomalyKind.VoidRift => choice == 0 ? Give("The rift closes, leaving its dust", ("Essence", 250))
                : (roll < 0.45f ? "Your probe came back with something." + Item(1.6f, Rarity.Epic) : RaidHere("Something came back through with the probe. Raiders are coming!")),
            AnomalyKind.PrecursorBeacon => choice == 0 ? WakeGate(s) + " It sings of the ones who built the gates." : Give("Precursor alloys", ("Starmetal", 300), ("Gold", 800)),
            AnomalyKind.CrystalStorm => choice == 0 ? Give("The nets are full of singing crystal", ("Mana", 300), ("Manastone", 400)) : Research(0.4f),
            _ => "",
        };
        string line = $"{Galaxy.Describe(kind).title} at {s.name}: {outcome}";
        State.codex.Add(line);
        GameAudio.UI("anomaly");
        Notify(line);
        RaiseChanged();
        return outcome;
    }

    // ---------- Outcomes ----------

    private string Give(string intro, params (string id, int amount)[] amounts)
    {
        var parts = new List<string>();
        foreach (var (id, amount) in amounts)
        {
            var r = Database.GetResource(id);
            if (r == null) continue;
            int got = Mathf.RoundToInt(amount * XenoRewardFactor);
            AddResource(r, got);
            parts.Add($"{got} {r.displayName}");
        }
        return (intro.Length > 0 ? intro + ": " : "") + string.Join(", ", parts) + ".";
    }

    private string Item(float bonus, Rarity min)
    {
        var item = GainLoot(bonus, 0.5f, "out among the stars", min, true);
        return item != null ? $" Found: {item.name}." : "";
    }

    private string Research(float share)
    {
        if (!BoostResearch(share)) return Give("With nothing being researched, the data become trade goods", ("Coin", 800));
        return $"Your research jumps ahead ({share:P0}).";
    }

    private string FreeLevel(string techId)
    {
        var t = Database.GetTechnology(techId);
        if (t == null || IsFullyResearched(t)) return "";
        State.researched.Add(t.id);
        return $" {t.displayName} gains a level!";
    }

    private string RaidHere(string text)
    {
        State.secondsUntilWave = Mathf.Min(State.secondsUntilWave, 20f);
        return text;
    }

    /// <summary>A wormhole links this system with a distant one: ships can go between them as if they were neighbours.</summary>
    private string ChartWormhole(StarSystem s)
    {
        var far = Galaxy.Get(Galaxy.Key(new Vector2Int(Galaxy.Hash(s.key.x, s.key.y, 5, 1) % 9 - 4 + Galaxy.SectorOf(s.key).x * 3,
            Galaxy.Hash(s.key.x, s.key.y, 6, 1) % 9 - 4 + Galaxy.SectorOf(s.key).y * 3), 0));
        if (far == null || far == s) return "It closed before it could be charted.";
        State.wormholes.Add(s.key); State.wormholes.Add(far.key);
        MarkScanned(far);
        GameAudio.UI("warp");
        return $"It leads to {far.name}, {Galaxy.Distance(s, far):0} light-years away. Ships can pass through it.";
    }

    /// <summary>Scan every system in the sectors round this one.</summary>
    private string WakeGate(StarSystem s)
    {
        var sector = Galaxy.SectorOf(s.key);
        int n = 0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                foreach (var other in Galaxy.SystemsIn(sector + new Vector2Int(x, y)))
                    if (!IsScanned(other)) { MarkScanned(other); n++; }
        GameAudio.UI("warp");
        return $"It lights up the stars around it: {n} systems charted.";
    }

    /// <summary>The far end of a wormhole from this system, if one is charted here.</summary>
    public StarSystem WormholeFrom(StarSystem s)
    {
        for (int i = 0; i + 1 < State.wormholes.Count; i += 2)
        {
            if (State.wormholes[i] == s.key) return Galaxy.Get(State.wormholes[i + 1]);
            if (State.wormholes[i + 1] == s.key) return Galaxy.Get(State.wormholes[i]);
        }
        return null;
    }
}
