using System;
using System.Collections.Generic;
using UnityEngine;

// Deeper warfare: veterancy, morale, commanders, siege and battle reports.
// Saved state lives in SettlementState.war (WarSave); everything here has safe defaults so old
// saves load unchanged (no regiments, no commanders, no reports until battles happen).

/// <summary>Everything about warfare that gets saved.</summary>
[Serializable]
public class WarSave
{
    [Tooltip("The experience of each training building's regiment (by building instance id; ids are unique across worlds).")]
    public List<WarRegiment> regiments = new List<WarRegiment>();

    [Tooltip("Heroes' records as commanders: whether they command, their battles and their command traits.")]
    public List<WarHeroRecord> heroes = new List<WarHeroRecord>();

    [Tooltip("The latest battle reports, newest last.")]
    public List<WarReport> reports = new List<WarReport>();

    [Tooltip("Battles fought in all (live and on other worlds).")]
    public int battles;
    public int victories;
    public int raidersRouted;
}

/// <summary>A training building's regiment: its experience outlives the troops (they are trained again).</summary>
[Serializable]
public class WarRegiment
{
    public int building;
    public int world;
    public float xp;
    public int kills;
    public int battles;
    public int fallen;
    [Tooltip("Drill upgrades bought (capped at WarCatalog.MaxDrill).")]
    public int drill;
    [Tooltip("The highest rank it has reached (so rank-up news is only told once).")]
    public int bestRank;
}

/// <summary>A hero's record as a commander.</summary>
[Serializable]
public class WarHeroRecord
{
    public int hero;
    [Tooltip("Commands the army of the world it's on.")]
    public bool commanding;
    [Tooltip("Battle tallies, indexed by WarCounter.")]
    public List<int> counters = new List<int>();
    [Tooltip("Command traits earned (ids from WarCatalog.Traits), in the order earned.")]
    public List<string> traits = new List<string>();

    public int Get(WarCounter c) => (int)c < counters.Count ? counters[(int)c] : 0;

    public void Add(WarCounter c, int n)
    {
        while (counters.Count <= (int)c) counters.Add(0);
        counters[(int)c] += n;
    }
}

/// <summary>What happened in one battle (a raid fought out).</summary>
[Serializable]
public class WarReport
{
    public float at;
    public string world;
    public int raiders;
    public int siege;
    public int slain;
    public int fled;
    public int lost;
    public int heroesDown;
    public int ranksGained;
    public string best;
    public int bestKills;
    public bool victory;
    public bool routed;
    public bool simulated;
    public string boss;

    public string Line()
    {
        if (simulated)
            return $"{(victory ? "Held" : "Broken")}: {raiders} raiders{(lost > 0 ? $", about {lost} of ours fell" : "")} (fought out of sight).";
        var sb = new System.Text.StringBuilder();
        sb.Append(victory ? (routed ? "Routed" : "Victory") : "Raided");
        sb.Append($": {raiders} raider{(raiders == 1 ? "" : "s")}");
        if (siege > 0) sb.Append($" ({siege} siege)");
        if (!string.IsNullOrEmpty(boss)) sb.Append($" led by {boss}");
        sb.Append($"; {slain} slain");
        if (fled > 0) sb.Append($", {fled} fled");
        sb.Append($". We lost {lost}");
        if (heroesDown > 0) sb.Append($" ({heroesDown} hero{(heroesDown == 1 ? "" : "es")} down)");
        sb.Append('.');
        if (!string.IsNullOrEmpty(best) && bestKills > 0) sb.Append($" Best: {best}, {bestKills} kill{(bestKills == 1 ? "" : "s")}.");
        if (ranksGained > 0) sb.Append($" {ranksGained} rank{(ranksGained == 1 ? "" : "s")} won.");
        return sb.ToString();
    }
}

/// <summary>What a commander's battles are counted by (indexes into WarHeroRecord.counters).</summary>
public enum WarCounter
{
    Battles, Victories, Kills, MeleeKills, RangedKills, CavalryKills, GunKills, SiegeKills, AirKills, NavalKills,
    BeastKills, BossKills, Routs, Defences, CampsBurnt, NativeKills, AlienBattles, HardWins
}

/// <summary>What sort of fighter a unit is (for command traits and tallies).</summary>
public enum WarCat { Any, Melee, Ranged, Cavalry, Guns, Siege, Air, Naval, Beast, Healer }

/// <summary>A command trait: earned by a commander once a tally is reached (and the age allows it).</summary>
public class WarTrait
{
    public string id, name, text;
    public int minAge;
    public WarCounter counter;
    public int threshold;
    public WarCat cat;
    public float hp = 1f, atk = 1f, speed = 1f, cooldown = 1f;
    public float morale, fear, breakShift, radius;
    public bool noBreak;
}

/// <summary>Ranks, command traits, doctrines and siege tiers: the fixed rules.</summary>
public static class WarCatalog
{
    public static readonly string[] RankNames = { "Recruit", "Regular", "Veteran", "Elite", "Legend" };
    public const int MaxRank = 4;
    /// <summary>A troop's experience to reach each rank.</summary>
    public static readonly float[] TroopXp = { 0f, 25f, 75f, 160f, 300f };
    /// <summary>A regiment's experience to reach each rank.</summary>
    public static readonly float[] RegimentXp = { 0f, 150f, 500f, 1200f, 2500f };
    public static readonly float[] RankHealth = { 1f, 1.06f, 1.12f, 1.2f, 1.3f };
    public static readonly float[] RankAttack = { 1f, 1.05f, 1.1f, 1.16f, 1.24f };
    public static readonly float[] RankMorale = { 0f, 5f, 10f, 16f, 24f };
    public static readonly string[] RankColours = { "#bbb", "#d9a05a", "#cfd6df", "#f6c544", "#ffb0ff" };

    /// <summary>Drill upgrades per training building.</summary>
    public const int MaxDrill = 3;
    public static readonly string[] DrillNames = { "Drill Yard", "Drill Sergeants", "War College" };

    public static string RankName(int r) => RankNames[Mathf.Clamp(r, 0, MaxRank)];
    public static string RankText(int r) => $"<color={RankColours[Mathf.Clamp(r, 0, MaxRank)]}>{RankName(r)}</color>";

    public static int RankOf(float xp, float[] table)
    {
        int r = 0;
        for (int i = 1; i < table.Length; i++) if (xp >= table[i]) r = i;
        return r;
    }

    // ---------- Morale ----------
    public const float BaseMorale = 62f, RaiderMorale = 56f;
    public const float BreakAt = 20f, RallyTo = 45f;
    public const float WaverAt = 35f, InspiredAt = 85f;

    public static string MoraleName(float m, bool routing) =>
        routing ? "<color=#f66>Routing!</color>" : m >= InspiredAt ? "<color=#8f8>Inspired</color>" : m >= 60f ? "<color=#cfc>Steady</color>"
        : m >= WaverAt ? "<color=#fe8>Shaken</color>" : "<color=#f96>Wavering</color>";

    // ---------- Commanders ----------
    public const float CommandRadius = 9f;
    public static int TraitSlots(int ageIndex) => Mathf.Min(8, 2 + ageIndex / 3);

    public static readonly List<WarTrait> Traits = new List<WarTrait>
    {
        T("WarbandChief", "Warband Chief", 0, WarCounter.Battles, 3, WarCat.Any, "Leads from the front: +6 morale in the aura.", morale: 6),
        T("BloodedSpear", "Blooded Spear", 0, WarCounter.MeleeKills, 40, WarCat.Melee, "Hand-to-hand fighters in the aura hit 6% harder.", atk: 1.06f),
        T("KeenEyed", "Keen-Eyed", 0, WarCounter.RangedKills, 30, WarCat.Ranged, "Archers and throwers in the aura hit 6% harder.", atk: 1.06f),
        T("ChariotLord", "Chariot Lord", 1, WarCounter.CavalryKills, 30, WarCat.Cavalry, "Riders in the aura are 8% quicker and hit 6% harder.", atk: 1.06f, speed: 1.08f),
        T("Shieldwall", "Shieldwall Master", 1, WarCounter.MeleeKills, 120, WarCat.Melee, "Hand-to-hand fighters in the aura have 10% more health.", hp: 1.1f),
        T("Beastmaster", "Beastmaster", 1, WarCounter.BeastKills, 25, WarCat.Beast, "War beasts in the aura: +8% health, +6% attack.", hp: 1.08f, atk: 1.06f),
        T("Unbroken", "The Unbroken", 2, WarCounter.HardWins, 3, WarCat.Any, "Won battles that should have been lost: troops in the aura break later.", breakShift: -8),
        T("IronDiscipline", "Iron Discipline", 2, WarCounter.Victories, 8, WarCat.Any, "Drilled to hold: +4 morale and troops break later.", morale: 4, breakShift: -5),
        T("Skirmisher", "Skirmish Captain", 2, WarCounter.RangedKills, 150, WarCat.Ranged, "Ranged troops in the aura shoot 6% faster.", cooldown: 0.94f),
        T("Drillmaster", "Legion Drillmaster", 3, WarCounter.Kills, 400, WarCat.Melee, "Hand-to-hand fighters: +8% attack, +5% health.", atk: 1.08f, hp: 1.05f),
        T("SiegeEngineer", "Siege Engineer", 3, WarCounter.CampsBurnt, 4, WarCat.Siege, "Siege engines in the aura hit 15% harder.", atk: 1.15f),
        T("Strategos", "Strategos", 3, WarCounter.Battles, 15, WarCat.Any, "Sees the whole field: aura reaches 3 tiles further, +4 morale.", morale: 4, radius: 3),
        T("CavalryRaider", "Cavalry Raider", 4, WarCounter.CavalryKills, 150, WarCat.Cavalry, "Riders in the aura hit 12% harder.", atk: 1.12f),
        T("Castellan", "Castellan", 4, WarCounter.Defences, 10, WarCat.Any, "Holds the walls: +6% health and +6 morale.", hp: 1.06f, morale: 6),
        T("Chiefslayer", "Chiefslayer", 4, WarCounter.BossKills, 4, WarCat.Any, "Raiders near the commander lose heart (-8 morale).", fear: 8),
        T("PikeAndShot", "Pike and Shot", 5, WarCounter.Kills, 900, WarCat.Any, "Every troop in the aura hits 5% harder.", atk: 1.05f),
        T("ArtilleryMarshal", "Artillery Marshal", 5, WarCounter.GunKills, 120, WarCat.Guns, "Guns and cannon in the aura hit 12% harder.", atk: 1.12f),
        T("Condottiero", "Condottiero", 5, WarCounter.Victories, 20, WarCat.Any, "Paid in victories: +5% speed, +4 morale.", speed: 1.05f, morale: 4),
        T("LineTactician", "Line Tactician", 6, WarCounter.GunKills, 300, WarCat.Guns, "Volleys by the drum: guns in the aura fire 8% faster.", cooldown: 0.92f),
        T("GrandBattery", "Grand Battery", 6, WarCounter.SiegeKills, 60, WarCat.Siege, "Siege engines: +12% attack, +10% health.", atk: 1.12f, hp: 1.1f),
        T("TrenchWarden", "Trench Warden", 7, WarCounter.Defences, 25, WarCat.Any, "Dug in: +10% health in the aura.", hp: 1.1f),
        T("IronHorse", "Iron Horse Logistician", 7, WarCounter.Battles, 35, WarCat.Any, "Rail and timetables: +8% speed.", speed: 1.08f),
        T("Blitz", "Blitz Commander", 8, WarCounter.CavalryKills, 400, WarCat.Cavalry, "Fast movers in the aura: +10% speed, +8% attack.", speed: 1.1f, atk: 1.08f),
        T("AirMarshal", "Air Marshal", 8, WarCounter.AirKills, 100, WarCat.Air, "Flyers in the aura hit 12% harder.", atk: 1.12f),
        T("StormVanguard", "Storm Vanguard", 9, WarCounter.Kills, 2500, WarCat.Any, "Every troop in the aura hits 6% harder.", atk: 1.06f),
        T("NetworkCentric", "Network-Centric Command", 10, WarCounter.Battles, 60, WarCat.Any, "Linked by radio: aura reaches 5 tiles further, +6 morale.", radius: 5, morale: 6),
        T("DroneOverseer", "Drone Overseer", 10, WarCounter.AirKills, 300, WarCat.Air, "Flyers in the aura strike 10% faster.", cooldown: 0.9f),
        T("RuneMarshal", "Rune Marshal", 11, WarCounter.Victories, 50, WarCat.Any, "Warding runes: +8% health in the aura.", hp: 1.08f),
        T("Wardcaster", "Wardcaster", 11, WarCounter.Routs, 80, WarCat.Any, "Raiders near the commander lose heart (-10 morale).", fear: 10),
        T("StarAdmiral", "Star Admiral", 12, WarCounter.NavalKills, 60, WarCat.Naval, "Ships in the aura: +12% attack, +8% health.", atk: 1.12f, hp: 1.08f),
        T("Sunforged", "Sunforged Warlord", 13, WarCounter.BossKills, 12, WarCat.Any, "Every troop in the aura hits 8% harder.", atk: 1.08f),
        T("VoidAdmiral", "Void Admiral", 14, WarCounter.AlienBattles, 10, WarCat.Any, "Has fought under strange suns: +8% health, +6% attack.", hp: 1.08f, atk: 1.06f),
        T("XenoHunter", "Xeno Hunter", 14, WarCounter.NativeKills, 200, WarCat.Any, "Knows the natives' ways: +5% attack, +4 morale.", atk: 1.05f, morale: 4),
        T("Hegemon", "Galactic Hegemon", 15, WarCounter.Victories, 100, WarCat.Any, "+10 morale and the aura reaches 4 tiles further.", morale: 10, radius: 4),
        T("Timeless", "Timeless Commander", 16, WarCounter.HardWins, 10, WarCat.Any, "Troops in the aura never break; +5% attack.", atk: 1.05f, noBreak: true),
    };

    private static WarTrait T(string id, string name, int age, WarCounter counter, int threshold, WarCat cat, string text,
        float hp = 1f, float atk = 1f, float speed = 1f, float cooldown = 1f, float morale = 0, float fear = 0, float breakShift = 0,
        float radius = 0, bool noBreak = false) => new WarTrait
    {
        id = id, name = name, minAge = age, counter = counter, threshold = threshold, cat = cat, text = text,
        hp = hp, atk = atk, speed = speed, cooldown = cooldown, morale = morale, fear = fear, breakShift = breakShift, radius = radius, noBreak = noBreak,
    };

    public static WarTrait Trait(string id) => Traits.Find(t => t.id == id);

    public static string CounterName(WarCounter c) => c switch
    {
        WarCounter.Battles => "battles fought",
        WarCounter.Victories => "victories",
        WarCounter.Kills => "enemies slain",
        WarCounter.MeleeKills => "slain hand to hand",
        WarCounter.RangedKills => "slain by archers and throwers",
        WarCounter.CavalryKills => "slain by riders",
        WarCounter.GunKills => "slain by guns",
        WarCounter.SiegeKills => "slain by siege engines",
        WarCounter.AirKills => "slain by flyers",
        WarCounter.NavalKills => "slain by ships",
        WarCounter.BeastKills => "slain by war beasts",
        WarCounter.BossKills => "raider chiefs slain",
        WarCounter.Routs => "raiders routed",
        WarCounter.Defences => "towns held",
        WarCounter.CampsBurnt => "camps burnt",
        WarCounter.NativeKills => "natives of other worlds slain",
        WarCounter.AlienBattles => "battles on other worlds",
        WarCounter.HardWins => "hard-won victories",
        _ => c.ToString(),
    };

    public static WarCounter KillCounter(WarCat c) => c switch
    {
        WarCat.Ranged => WarCounter.RangedKills,
        WarCat.Cavalry => WarCounter.CavalryKills,
        WarCat.Guns => WarCounter.GunKills,
        WarCat.Siege => WarCounter.SiegeKills,
        WarCat.Air => WarCounter.AirKills,
        WarCat.Naval => WarCounter.NavalKills,
        WarCat.Beast => WarCounter.BeastKills,
        _ => WarCounter.MeleeKills,
    };

    // ---------- Doctrines (research: small army-wide bonuses) ----------
    public const string DocMuster = "DoctrineMusterRolls", DocEsprit = "DoctrineEsprit", DocCombined = "DoctrineCombinedArms",
        DocStaff = "DoctrineStaffWork", DocFieldworks = "DoctrineFieldworks", DocGeneralStaff = "DoctrineGeneralStaff",
        DocDepth = "DoctrineDefenceInDepth", DocNetwork = "DoctrineNetworkWarfare", DocPsionic = "DoctrinePsionicResolve",
        DocTotal = "DoctrineTotalCommand";

    // ---------- Siege ----------
    /// <summary>How siege engines fight: rams batter, throwers lob, guns fire, walkers stride in, beacons call down strikes.</summary>
    public enum SiegeKind { Ram, Onager, Trebuchet, Bombard, SteamTank, Howitzer, Launcher, Colossus, Mech, Beacon, Breaker }
}
