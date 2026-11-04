using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>A world's government, laws and taxes (saved on WorldState). Old saves: customary rule, normal taxes, no laws = no effect.</summary>
[Serializable]
public class CivicsWorld
{
    [Tooltip("Set up the first time this world ticks (customary rule, or a colonial administration on a new colony).")]
    public bool set;
    [Tooltip("The government's id (see Settlement.Governments); empty = customary rule.")]
    public string gov = "";
    [Tooltip("Policies (laws) in force, by id.")]
    public List<string> policies = new List<string>();
    [Tooltip("Tax level as an offset from normal: -2 none .. +3 crushing (0 = normal, as before).")]
    public int taxOffset;
    [Tooltip("Tithes for the faith: 0 none, 1 tithe, 2 great tithe.")]
    public int tithe;
    [Tooltip("0 to 100: how firmly the people accept the government (50 = neutral).")]
    public float legitimacy = 50f;
    [Tooltip("Play time when the anarchy after a change of government ends.")]
    public float anarchyUntil;
    [Tooltip("Play time when the government may be changed again.")]
    public float switchReadyAt;
    [Tooltip("Play time until a general strike (after a revolt) ends.")]
    public float strikeUntil;
    [Tooltip("Play time when rebels gathering here rise (0: none gathering).")]
    public float revoltAt;
    [Tooltip("No new revolt before this play time.")]
    public float revoltReadyAt;
    [Tooltip("Play time when a pardon may be granted again.")]
    public float pardonReadyAt;
    public int revolts, changes;
    [Tooltip("The government before the last change (shown in the history).")]
    public string previous = "";

    // Worked out on a timer; not saved.
    [NonSerialized] public CivicsTotals totals;
    [NonSerialized] public float totalsAt = -99f;
    [NonSerialized] public float lastTick = -1f;
}

/// <summary>The policy categories (each government has slots in some of them).</summary>
public enum CivCategory { Economy, Military, Society, Faith, Science, Expansion }

/// <summary>
/// What a government, a policy, a tax level or the state of things does. Multipliers are 1 for no
/// change; additive values 0. Descriptions are written from the numbers, so they never go stale.
/// </summary>
public class CivEffect
{
    // Production by kind of resource (see Settlement.CivGroupOf), and on everything.
    public float food = 1, wood = 1, stone = 1, metal = 1, wealth = 1, arcane = 1, industry = 1, cosmic = 1, all = 1;
    public float research = 1, growth = 1, culture = 1, faith = 1, great = 1, tax = 1, trade = 1, sell = 1, buy = 1;
    public float threat = 1, attack = 1, health = 1, upkeep = 1, disaster = 1, weariness = 1, anarchy = 1, policyCost = 1;
    public float colonyAll = 1, colonyGrowth = 1;
    /// <summary>Share of a building's cost paid back when it's finished.</summary>
    public float refund;
    /// <summary>Mood; legitimacy per minute; native standing per minute.</summary>
    public float mood, legit, natives;
    /// <summary>How harsh it is: harsh rule breeds revolts when people are miserable.</summary>
    public int harsh;
    /// <summary>Revolts can't happen (martial law).</summary>
    public bool noRevolt;

    public void Add(CivEffect e, float weight = 1f)
    {
        if (e == null) return;
        food *= W(e.food, weight); wood *= W(e.wood, weight); stone *= W(e.stone, weight); metal *= W(e.metal, weight);
        wealth *= W(e.wealth, weight); arcane *= W(e.arcane, weight); industry *= W(e.industry, weight); cosmic *= W(e.cosmic, weight); all *= W(e.all, weight);
        research *= W(e.research, weight); growth *= W(e.growth, weight); culture *= W(e.culture, weight); faith *= W(e.faith, weight);
        great *= W(e.great, weight); tax *= W(e.tax, weight); trade *= W(e.trade, weight); sell *= W(e.sell, weight); buy *= W(e.buy, weight);
        threat *= W(e.threat, weight); attack *= W(e.attack, weight); health *= W(e.health, weight); upkeep *= W(e.upkeep, weight);
        disaster *= W(e.disaster, weight); weariness *= W(e.weariness, weight); anarchy *= W(e.anarchy, weight); policyCost *= W(e.policyCost, weight);
        colonyAll *= W(e.colonyAll, weight); colonyGrowth *= W(e.colonyGrowth, weight);
        refund += e.refund * weight; mood += e.mood * weight; legit += e.legit * weight; natives += e.natives * weight;
        harsh += e.harsh; noRevolt |= e.noRevolt;
    }

    private static float W(float f, float weight) => weight == 1f ? f : 1f + (f - 1f) * weight;

    /// <summary>Each effect as a short line, good ones first: (text, good).</summary>
    public List<(string text, bool good)> Lines()
    {
        var list = new List<(string, bool)>();
        void P(float f, string what, bool lowerIsBetter = false)
        {
            if (Mathf.Abs(f - 1f) < 0.004f) return;
            int pct = Mathf.RoundToInt((f - 1f) * 100f);
            list.Add(($"{what} {(pct > 0 ? "+" : "")}{pct}%", lowerIsBetter ? f < 1f : f > 1f));
        }
        P(all, "Everything produced");
        P(food, "Food"); P(wood, "Wood"); P(stone, "Stone"); P(metal, "Metals and tools"); P(wealth, "Gold and coin made");
        P(arcane, "Mana and arcane goods"); P(industry, "Fuel, powder and industry"); P(cosmic, "Star goods");
        P(colonyAll, "Colonies produce"); P(colonyGrowth, "Colonies grow");
        P(research, "Research speed"); P(growth, "People move in"); P(culture, "Culture"); P(faith, "Faith"); P(great, "Great people come");
        P(tax, "Taxes"); P(trade, "Trade routes"); P(sell, "Market sale prices");
        P(buy, "Market buy prices", true);
        P(attack, "Troops' attack"); P(health, "Troops' health"); P(upkeep, "Army upkeep", true); P(threat, "Raid strength", true);
        P(disaster, "Disaster damage", true); P(weariness, "War weariness fades");
        P(anarchy, "Anarchy when changing government", true); P(policyCost, "Policy costs", true);
        if (refund > 0.004f) list.Add(($"Buildings {refund:P0} cheaper (paid back when finished)", true));
        if (mood != 0) list.Add(($"Mood {(mood > 0 ? "+" : "")}{mood:0}", mood > 0));
        if (legit != 0) list.Add(($"Legitimacy {(legit > 0 ? "+" : "")}{legit:0.#}/min", legit > 0));
        if (natives != 0) list.Add(($"Natives' goodwill {(natives > 0 ? "+" : "")}{natives:0.#}/min", natives > 0));
        if (noRevolt) list.Add(("No revolts", true));
        if (harsh > 0) list.Add(($"Harsh rule ({harsh}): risks revolt when people are miserable", false));
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    public string Text(string sep = "\n")
    {
        var sb = new StringBuilder();
        foreach (var (text, good) in Lines())
        {
            if (sb.Length > 0) sb.Append(sep);
            sb.Append(good ? "<color=#9e9>" : "<color=#f99>").Append(text).Append("</color>");
        }
        return sb.Length > 0 ? sb.ToString() : "<color=#aaa>No effect.</color>";
    }
}

/// <summary>A cached sum of everything in force on one world.</summary>
public class CivicsTotals
{
    public readonly CivEffect fx = new CivEffect();
    public readonly float[] prod = new float[Settlement.CivGroupCount];
    public bool anarchy;
}

/// <summary>A form of government: its identity, bonuses, drawbacks and policy slots.</summary>
public class CivGovernment
{
    public string id, name, ruler, desc;
    public int age;
    /// <summary>Research that opens it early; without it, it opens an age later.</summary>
    public string tech;
    public CivEffect fx = new CivEffect();
    /// <summary>Policy slots per category (Economy, Military, Society, Faith, Science, Expansion).</summary>
    public int[] slots = new int[6];
    /// <summary>Emblem: field colour, second colour, metal (hex), and the charge drawn on it.</summary>
    public string field = "556", field2 = "334", metal = "dc9", charge = "star";
    /// <summary>Leads a federation of worlds (member worlds share its laws).</summary>
    public bool federal;
    /// <summary>Only on a world that isn't your homeworld / only joins a federation elsewhere.</summary>
    public bool colonyOnly, member;
    /// <summary>Never offered (customary rule, colonial administration are given, not chosen).</summary>
    public bool given;
}

/// <summary>A policy (law): a trade-off in one category.</summary>
public class CivPolicy
{
    public string id, name, desc;
    public CivCategory cat;
    public int age;
    public string tech;
    public CivEffect fx = new CivEffect();
    /// <summary>Only one policy of a group at a time (e.g. Free Trade or Mercantilism).</summary>
    public string group;
    /// <summary>Only under these governments (null: any).</summary>
    public string[] govs;
    public bool colonyOnly;
}

public partial class Settlement
{
    public const string CivCustomary = "Customary", CivColonial = "ColonialAdmin", CivFederation = "StellarFederation", CivMember = "FederalMember";

    // Resource groups for production effects.
    public const int CivGroupCount = 9;
    private const int GFood = 0, GWood = 1, GStone = 2, GMetal = 3, GWealth = 4, GArcane = 5, GIndustry = 6, GCosmic = 7, GOther = 8;
    private static Dictionary<string, int> civGroups;

    public static int CivGroupOf(string resourceId)
    {
        if (civGroups == null)
        {
            civGroups = new Dictionary<string, int>();
            void G(int g, params string[] ids) { foreach (var id in ids) civGroups[id] = g; }
            G(GFood, "Food");
            G(GWood, "Wood", "Charcoal");
            G(GStone, "Stone");
            G(GMetal, "Bronze", "Iron", "Steel", "Tools", "Gears", "Quicksilver", "Uranium");
            G(GWealth, "Gold", "Coin");
            G(GArcane, "Mana", "Manastone", "Essence", "Runestone", "Aether", "Quintessence");
            G(GIndustry, "Coal", "Oil", "Fuel", "Gunpowder", "Saltpetre", "PowerCells", "Silicon", "Microchips");
            G(GCosmic, "AstralCores", "Starmetal", "Heliolite", "Sunfire", "Riftstone", "WarpCells", "Stardust", "Primordium", "GenesisMatter");
        }
        return resourceId != null && civGroups.TryGetValue(resourceId, out int g) ? g : GOther;
    }

    private static int[] Slots(int eco = 0, int mil = 0, int soc = 0, int fai = 0, int sci = 0, int exp = 0) => new[] { eco, mil, soc, fai, sci, exp };

    // ---------- Governments, age by age ----------

    public static readonly CivGovernment[] Governments =
    {
        new CivGovernment { id = CivCustomary, name = "Customary Rule", ruler = "Elders", age = 0, given = true, field = "6b5a44", field2 = "4a3d2e", metal = "c9b48a", charge = "fire",
            desc = "The old ways, handed down round the fire. No bonuses and no drawbacks; choose a government when you're ready (the first choice is free).",
            slots = Slots(1, 0, 1) },
        new CivGovernment { id = CivColonial, name = "Colonial Administration", ruler = "Governor", age = 0, given = true, colonyOnly = true, field = "3d5a7a", field2 = "2a3d55", metal = "e0d08a", charge = "anchor",
            desc = "A governor sent from home runs the new colony by charter: settlers come gladly, but part of the taxes go home.",
            fx = new CivEffect { growth = 1.1f, legit = 2f, tax = 0.8f }, slots = Slots(1, 0, 0, 0, 0, 1) },

        // Stone Age
        new CivGovernment { id = "TribalCouncil", name = "Tribal Council", ruler = "Elder", age = 0, field = "6e7a3a", field2 = "4d5629", metal = "e8d49a", charge = "circle",
            desc = "The elders of every clan sit in a ring and talk until they agree. Content, steady, slow to war.",
            fx = new CivEffect { mood = 3f, growth = 1.05f, legit = 1f, attack = 0.97f }, slots = Slots(1, 1, 1) },
        new CivGovernment { id = "Chiefdom", name = "Chiefdom", ruler = "Chief", age = 0, field = "8a3a2a", field2 = "5e271c", metal = "e8c070", charge = "spear",
            desc = "The strongest hunter leads, and the band follows. Fierce in a fight, but the chief's word is law.",
            fx = new CivEffect { attack = 1.06f, threat = 0.95f, food = 1.03f, mood = -2f }, slots = Slots(1, 2, 0, 0, 0, 1) },

        // Bronze and Iron Ages
        new CivGovernment { id = "Despotism", name = "Despotism", ruler = "Despot", age = 1, field = "5a1e1e", field2 = "3a1212", metal = "d8b050", charge = "fist",
            desc = "One ruler, one will. Work gets done and the army marches, but people keep their heads down.",
            fx = new CivEffect { all = 1.04f, attack = 1.05f, mood = -4f, culture = 0.9f, harsh = 1 }, slots = Slots(1, 2, 1) },
        new CivGovernment { id = "Theocracy", name = "Theocracy", ruler = "High Priest", age = 2, tech = "TheCalendar", field = "e0c060", field2 = "a88838", metal = "fff4d0", charge = "sun",
            desc = "The temple rules in the gods' name. Faith runs deep and the law is sacred; learning waits on scripture.",
            fx = new CivEffect { faith = 1.25f, mood = 2f, legit = 2f, research = 0.92f }, slots = Slots(1, 0, 1, 2) },

        // Classical Age
        new CivGovernment { id = "Monarchy", name = "Monarchy", ruler = "King", age = 3, field = "2a3e8a", field2 = "1c2a5e", metal = "f0d060", charge = "crown",
            desc = "A crowned king and a royal line. Stable, rich in taxes, and the old families are slow to share.",
            fx = new CivEffect { legit = 2f, tax = 1.1f, health = 1.05f, growth = 0.95f }, slots = Slots(2, 1, 1, 1) },
        new CivGovernment { id = "Republic", name = "Republic", ruler = "Consul", age = 3, tech = "Philosophy", field = "f0ece0", field2 = "b8b4a8", metal = "8a2a2a", charge = "laurel",
            desc = "Citizens vote and senators argue. Ideas and trade flourish; armies are raised with reluctance.",
            fx = new CivEffect { research = 1.06f, trade = 1.1f, mood = 2f, attack = 0.95f }, slots = Slots(2, 0, 2, 0, 1, 1) },
        new CivGovernment { id = "Oligarchy", name = "Oligarchy", ruler = "Archon", age = 3, tech = "Coinage", field = "3a2a4a", field2 = "261c32", metal = "e8c060", charge = "coin",
            desc = "A few rich houses hold the city. The coffers fill; the many grumble.",
            fx = new CivEffect { tax = 1.2f, sell = 1.05f, mood = -3f, growth = 0.95f }, slots = Slots(3, 1) },

        // Medieval Age (and the fantasy layer)
        new CivGovernment { id = "Feudalism", name = "Feudal Realm", ruler = "Liege", age = 4, tech = "Feudalism", field = "7a5a2a", field2 = "54401c", metal = "e0e0e0", charge = "tower",
            desc = "Lords hold land of the crown and owe it knights. The fields are worked and the castles manned; serfs learn little.",
            fx = new CivEffect { food = 1.1f, health = 1.08f, upkeep = 0.9f, research = 0.95f, mood = -2f }, slots = Slots(1, 2, 1, 1, 0, 1) },
        new CivGovernment { id = "MerchantRepublic", name = "Merchant Republic", ruler = "Doge", age = 4, tech = "Guilds", field = "1e5a5a", field2 = "143e3e", metal = "f0d070", charge = "ship",
            desc = "The guilds and the great trading houses elect a doge. Every road and harbour pays; soldiers are hired, not born.",
            fx = new CivEffect { trade = 1.25f, sell = 1.08f, buy = 0.95f, tax = 1.1f, attack = 0.94f }, slots = Slots(3, 0, 1, 0, 1) },
        new CivGovernment { id = "ArcaneConclave", name = "Arcane Conclave", ruler = "Archmage", age = 4, field = "3a2a6a", field2 = "261c48", metal = "b8e0ff", charge = "eye",
            desc = "Robed magi rule from a tower of books. Their wards turn storm and fire, and their study never stops; the people find them distant.",
            fx = new CivEffect { arcane = 1.2f, research = 1.05f, disaster = 0.85f, mood = -2f, food = 0.95f }, slots = Slots(1, 0, 0, 1, 2) },

        // Renaissance and Enlightenment
        new CivGovernment { id = "AbsoluteMonarchy", name = "Absolute Monarchy", ruler = "Sun King", age = 5, field = "1c2a6a", field2 = "121c48", metal = "f8e070", charge = "sunking",
            desc = "\"The state is me.\" Palaces, standing armies and a court that runs like clockwork, paid for by everyone else.",
            fx = new CivEffect { all = 1.05f, legit = 3f, attack = 1.05f, mood = -4f, culture = 0.95f, harsh = 1 }, slots = Slots(2, 2, 1, 1, 0, 1) },
        new CivGovernment { id = "ConstitutionalMonarchy", name = "Constitutional Monarchy", ruler = "Crown and Parliament", age = 6, tech = "Encyclopedie", field = "8a1e2a", field2 = "5e141c", metal = "f0e0a0", charge = "book",
            desc = "The crown reigns, parliament governs. Liberty, learning and fair taxes; a costly army answerable to law.",
            fx = new CivEffect { mood = 3f, research = 1.05f, tax = 1.1f, legit = 1f, upkeep = 1.1f }, slots = Slots(2, 1, 2, 0, 1) },

        // Industrial to Information Ages
        new CivGovernment { id = "Democracy", name = "Democracy", ruler = "President", age = 7, field = "2a4a8a", field2 = "1c3260", metal = "f0f0f0", charge = "star",
            desc = "Every adult votes. People are happiest and most inventive; long wars wear on them, and soldiers must be paid well.",
            fx = new CivEffect { mood = 5f, research = 1.08f, growth = 1.1f, culture = 1.1f, weariness = 0.6f, upkeep = 1.15f, anarchy = 0.6f }, slots = Slots(2, 1, 3, 0, 2, 1) },
        new CivGovernment { id = "Collective", name = "People's Collective", ruler = "Chairman", age = 8, field = "a01e1e", field2 = "701414", metal = "f8d040", charge = "gear",
            desc = "Factories and farms belong to all, and the plan decides. Steel pours out; markets and smiles are scarce.",
            fx = new CivEffect { industry = 1.15f, metal = 1.15f, food = 1.05f, refund = 0.1f, mood = -3f, trade = 0.8f, sell = 0.9f }, slots = Slots(3, 1, 1, 0, 1, 1) },
        new CivGovernment { id = "CorporateState", name = "Corporate State", ruler = "Chief Executive", age = 9, tech = "ConsumerCredit", field = "2a2e36", field2 = "1c1f24", metal = "60e0f0", charge = "tower2",
            desc = "The great companies sit on the board of the nation. Money moves fast and far; art and comfort come second.",
            fx = new CivEffect { wealth = 1.2f, tax = 1.15f, trade = 1.2f, sell = 1.1f, mood = -3f, culture = 0.9f }, slots = Slots(4, 0, 1, 0, 1, 1) },
        new CivGovernment { id = "Technocracy", name = "Technocracy", ruler = "Director", age = 10, tech = "Computing", field = "1e3a4a", field2 = "142832", metal = "80f0c0", charge = "chip",
            desc = "Experts and their machines decide, by the numbers. Research races ahead; the old faiths and the warmth fade.",
            fx = new CivEffect { research = 1.15f, industry = 1.08f, faith = 0.75f, mood = -2f }, slots = Slots(2, 0, 1, 0, 3) },

        // The fantasy and starfaring ages
        new CivGovernment { id = "MageCouncil", name = "Mage Council", ruler = "Archmage", age = 11, tech = "Arcanism", field = "4a1e6a", field2 = "321448", metal = "d0a0ff", charge = "orb",
            desc = "Seven archmagi of the reborn arts rule by spell and leyline. Mana flows, wards hold back disaster; their spells eat into the budget.",
            fx = new CivEffect { arcane = 1.25f, research = 1.08f, disaster = 0.75f, upkeep = 1.1f, food = 0.95f }, slots = Slots(1, 1, 0, 2, 2) },
        new CivGovernment { id = "MachineConsensus", name = "Machine Consensus", ruler = "Consensus", age = 12, field = "30343c", field2 = "202328", metal = "ff6040", charge = "eye2",
            desc = "A mind of a million processors weighs every choice. Waste vanishes; people feel watched and small.",
            fx = new CivEffect { all = 1.06f, refund = 0.1f, mood = -4f, culture = 0.85f, growth = 0.95f }, slots = Slots(3, 1, 0, 0, 2, 1) },
        new CivGovernment { id = CivFederation, name = "Stellar Federation", ruler = "First Speaker", age = 13, tech = "Starships", federal = true, field = "142a5a", field2 = "0e1c3e", metal = "e0e8ff", charge = "stars",
            desc = "A union of worlds under one charter. Worlds that join as Federal Members share its laws; trade between the stars flows. Its councils are slow to change.",
            fx = new CivEffect { trade = 1.15f, mood = 2f, natives = 0.6f, anarchy = 1.5f, policyCost = 1.25f }, slots = Slots(2, 1, 2, 0, 1, 2) },
        new CivGovernment { id = CivMember, name = "Federal Member", ruler = "Delegate", age = 13, member = true, colonyOnly = true, field = "1e3a6a", field2 = "142848", metal = "c0d0ff", charge = "star",
            desc = "This world joins the Stellar Federation: the Federation's laws are in force here and its people prosper, paying federal dues. (Needs a Stellar Federation on another world.)",
            fx = new CivEffect { growth = 1.1f, legit = 2f, tax = 0.9f } },
        new CivGovernment { id = "HiveAccord", name = "Hive Accord", ruler = "Brood-Mind", age = 14, field = "5a4a12", field2 = "3e320c", metal = "f0e040", charge = "hex",
            desc = "Linked minds breed and work as one, cell by cell. Numbers grow without end and the army feeds itself; individual genius is rare.",
            fx = new CivEffect { growth = 1.25f, food = 1.1f, upkeep = 0.8f, culture = 0.8f, research = 0.95f }, slots = Slots(2, 2, 1, 0, 0, 1) },
        new CivGovernment { id = "GalacticConcordat", name = "Galactic Concordat", ruler = "Concord", age = 15, tech = "GalacticTrade", field = "2a1e4a", field2 = "1c1432", metal = "ffd0f0", charge = "ring",
            desc = "A treaty of a thousand suns. Markets span the galaxy and minds pool their learning; its fleets cost the earth.",
            fx = new CivEffect { trade = 1.3f, research = 1.1f, sell = 1.05f, upkeep = 1.1f }, slots = Slots(3, 1, 2, 0, 2, 2) },
        new CivGovernment { id = "AscendedSynod", name = "Ascended Synod", ruler = "Ascended", age = 16, field = "f0f0ff", field2 = "b0b0d0", metal = "ffd860", charge = "ascend",
            desc = "Those who have stepped beyond flesh guide the rest with light. Faith, art and calm without measure; earthly work matters less to them.",
            fx = new CivEffect { mood = 5f, faith = 1.25f, culture = 1.2f, legit = 3f, all = 0.97f }, slots = Slots(1, 1, 2, 2, 2, 1) },
    };

    // ---------- Policies ----------

    private static CivPolicy Pol(string id, string name, CivCategory cat, int age, string desc, CivEffect fx, string tech = null, string group = null, string[] govs = null, bool colonyOnly = false) =>
        new CivPolicy { id = id, name = name, cat = cat, age = age, desc = desc, fx = fx, tech = tech, group = group, govs = govs, colonyOnly = colonyOnly };

    public static readonly CivPolicy[] Policies =
    {
        // Economy
        Pol("BarterCustoms", "Barter Customs", CivCategory.Economy, 0, "Fixed days for swapping goods with neighbours.", new CivEffect { trade = 1.1f, sell = 1.03f }),
        Pol("CommunalGranaries", "Communal Granaries", CivCategory.Economy, 0, "Everyone's harvest in one store, shared out by the elders.", new CivEffect { food = 1.06f, wood = 0.97f }),
        Pol("Corvee", "Corvée Labour", CivCategory.Economy, 1, "Every family owes days of work on the lord's walls and roads.", new CivEffect { refund = 0.12f, mood = -2f, harsh = 1 }, group: "labour"),
        Pol("TributeCollection", "Tribute Collection", CivCategory.Economy, 1, "Neighbours pay to be left alone.", new CivEffect { tax = 1.15f, wealth = 1.05f, natives = -0.3f }),
        Pol("Rationing", "Rationing", CivCategory.Economy, 2, "Fixed shares of bread and meat for everyone, the army first.", new CivEffect { upkeep = 0.8f, food = 1.04f, mood = -4f }),
        Pol("RoyalMint", "Royal Mint", CivCategory.Economy, 3, "Stamped coin of true weight; the mint keeps a share.", new CivEffect { wealth = 1.1f, tax = 1.05f }, tech: "Coinage"),
        Pol("GuildCharters", "Guild Charters", CivCategory.Economy, 4, "Masters, journeymen and apprentices, and a guild for every craft.", new CivEffect { metal = 1.08f, industry = 1.05f, wood = 1.04f, growth = 0.97f }, tech: "Guilds"),
        Pol("FreeTrade", "Free Trade", CivCategory.Economy, 5, "No tolls, no tariffs: let goods go where they're wanted.", new CivEffect { trade = 1.2f, sell = 1.05f, buy = 0.95f, tax = 0.95f }, tech: "Banking", group: "trade"),
        Pol("Mercantilism", "Mercantilism", CivCategory.Economy, 5, "Export much, import little, hoard bullion.", new CivEffect { tax = 1.15f, sell = 1.08f, buy = 1.08f, trade = 0.9f }, group: "trade"),
        Pol("CentralBank", "Central Bank", CivCategory.Economy, 6, "A national bank to lend, mint and steady the coin.", new CivEffect { tax = 1.1f, wealth = 1.1f }, tech: "CentralBanking"),
        Pol("PriceControls", "Price Controls", CivCategory.Economy, 7, "The state fixes the price of bread and coal.", new CivEffect { buy = 0.9f, sell = 0.9f, mood = 2f }),
        Pol("FiveYearPlans", "Five-Year Plans", CivCategory.Economy, 8, "Quotas for every factory, posted on every wall.", new CivEffect { industry = 1.12f, metal = 1.08f, mood = -3f, harsh = 1 }),
        Pol("ConsumerEconomy", "Consumer Economy", CivCategory.Economy, 9, "Shops full of shiny things, and credit to buy them.", new CivEffect { mood = 3f, tax = 1.1f, industry = 0.95f }, tech: "ConsumerCredit"),
        Pol("AutomatedIndustry", "Automated Industry", CivCategory.Economy, 10, "Lights-out factories run by robots.", new CivEffect { industry = 1.1f, all = 1.02f, growth = 0.95f, mood = -1f }, tech: "Automation"),
        Pol("ManaflowQuotas", "Manaflow Quotas", CivCategory.Economy, 11, "Every leyline tapped to its limit.", new CivEffect { arcane = 1.12f, disaster = 1.1f, mood = -2f }),
        Pol("PostScarcity", "Post-Scarcity", CivCategory.Economy, 15, "Replicators make what anyone needs. Money hardly matters.", new CivEffect { all = 1.05f, mood = 4f, tax = 0.75f }),

        // Military
        Pol("WarriorBands", "Warrior Bands", CivCategory.Military, 0, "Every young hunter trains with the spear.", new CivEffect { attack = 1.05f, upkeep = 1.05f }),
        Pol("Conscription", "Conscription", CivCategory.Military, 2, "Every household sends a son to the levy.", new CivEffect { upkeep = 0.75f, mood = -3f, growth = 0.95f, harsh = 1 }, group: "army"),
        Pol("ProfessionalArmy", "Professional Army", CivCategory.Military, 3, "Paid, drilled soldiers who serve for twenty years.", new CivEffect { attack = 1.06f, health = 1.06f, upkeep = 1.2f }, tech: "Discipline", group: "army"),
        Pol("FortressDoctrine", "Fortress Doctrine", CivCategory.Military, 4, "Walls first, sallies later.", new CivEffect { threat = 0.92f, attack = 0.97f }),
        Pol("ChivalricOrders", "Chivalric Orders", CivCategory.Military, 4, "Knights sworn to the crown and the faith.", new CivEffect { health = 1.08f, faith = 1.05f, upkeep = 1.1f }, tech: "Chivalry"),
        Pol("MartialLaw", "Martial Law", CivCategory.Military, 4, "Soldiers on every corner, curfew at dusk.", new CivEffect { noRevolt = true, legit = 3f, mood = -5f, culture = 0.9f }),
        Pol("StandingArmy", "Standing Army", CivCategory.Military, 5, "Regiments kept under arms in peace as in war.", new CivEffect { threat = 0.9f, upkeep = 1.15f }),
        Pol("TotalWar", "Total War", CivCategory.Military, 7, "Every factory, field and family for the war.", new CivEffect { attack = 1.1f, industry = 1.05f, mood = -6f, weariness = 0.5f, harsh = 1 }),
        Pol("CivilDefenceCorps", "Civil Defence Corps", CivCategory.Military, 8, "Wardens, sirens and shelters in every street.", new CivEffect { disaster = 0.85f, threat = 0.94f, tax = 0.95f }),
        Pol("BattleMages", "Battle Mages", CivCategory.Military, 11, "Spellcasters attached to every company.", new CivEffect { attack = 1.08f, arcane = 0.95f }),
        Pol("OrbitalWatch", "Orbital Watch", CivCategory.Military, 12, "Satellites and gunships keep the skies.", new CivEffect { threat = 0.85f, upkeep = 1.1f }),
        Pol("DroneLegions", "Drone Legions", CivCategory.Military, 13, "Machines fight so people don't have to.", new CivEffect { upkeep = 0.8f, attack = 1.04f, mood = -2f }),

        // Society
        Pol("KinshipBonds", "Kinship Bonds", CivCategory.Society, 0, "Clan before self: everyone has a place.", new CivEffect { mood = 3f, research = 0.98f }),
        Pol("Curfew", "Curfew", CivCategory.Society, 1, "Gates shut and fires out at dark.", new CivEffect { threat = 0.95f, mood = -2f, legit = 1f }),
        Pol("Isolation", "Closed Borders", CivCategory.Society, 2, "No strangers past the boundary stones.", new CivEffect { threat = 0.93f, trade = 0.8f, natives = -0.5f }, group: "borders"),
        Pol("OpenBorders", "Open Borders with Natives", CivCategory.Society, 3, "Native traders and travellers welcome in your towns.", new CivEffect { natives = 0.8f, trade = 1.05f, threat = 1.04f }, group: "borders"),
        Pol("BreadAndCircuses", "Bread and Circuses", CivCategory.Society, 3, "Free grain and games in the arena.", new CivEffect { mood = 5f, tax = 0.9f, culture = 1.05f }),
        Pol("Serfdom", "Serfdom", CivCategory.Society, 4, "Peasants bound to the land of their lord.", new CivEffect { food = 1.12f, growth = 0.92f, mood = -3f, harsh = 1 }, group: "labourers"),
        Pol("PressCensorship", "Press Censorship", CivCategory.Society, 5, "Every pamphlet read by the censor first.", new CivEffect { legit = 3f, culture = 0.85f, research = 0.97f }, tech: "PrintingPress", group: "press"),
        Pol("FreeLabour", "Free Labour", CivCategory.Society, 6, "Anyone may work where they will, for a wage.", new CivEffect { growth = 1.08f, all = 1.02f, food = 0.97f }, group: "labourers"),
        Pol("FreePress", "Free Press", CivCategory.Society, 6, "Newspapers print what they like.", new CivEffect { culture = 1.15f, research = 1.03f, legit = -1f }, group: "press"),
        Pol("PublicHealth", "Public Health", CivCategory.Society, 6, "Clean water, sewers and hospitals.", new CivEffect { growth = 1.1f, tax = 0.95f }),
        Pol("PublicSchools", "Public Schools", CivCategory.Society, 7, "Every child learns to read and reckon.", new CivEffect { research = 1.08f, great = 1.05f, tax = 0.95f }),
        Pol("UniversalSuffrage", "Universal Suffrage", CivCategory.Society, 7, "Everyone has a vote.", new CivEffect { legit = 3f, mood = 2f, anarchy = 0.5f }, govs: new[] { "Democracy", "ConstitutionalMonarchy", "Republic", CivFederation, "GalacticConcordat" }),
        Pol("WelfareState", "Welfare State", CivCategory.Society, 8, "Pensions, sick pay and homes for all.", new CivEffect { mood = 5f, tax = 0.85f }),
        Pol("NeuralCommons", "Neural Commons", CivCategory.Society, 13, "Thoughts shared freely on a common net.", new CivEffect { mood = 3f, research = 1.05f, culture = 0.95f }),
        Pol("GeneCrafting", "Gene Crafting", CivCategory.Society, 14, "Children made healthy, strong and many.", new CivEffect { growth = 1.2f, faith = 0.9f }),

        // Faith
        Pol("AncestorWorship", "Ancestor Worship", CivCategory.Faith, 0, "Offerings to the honoured dead at every hearth.", new CivEffect { faith = 1.1f, mood = 1f }),
        Pol("SacredGroves", "Sacred Groves", CivCategory.Faith, 1, "Some woods are the gods' and may not be cut.", new CivEffect { wood = 0.95f, disaster = 0.9f, mood = 1f, faith = 1.05f }),
        Pol("StateReligion", "State Religion", CivCategory.Faith, 2, "One faith, the throne's own.", new CivEffect { faith = 1.2f, legit = 2f, mood = 2f, research = 0.97f }, group: "worship"),
        Pol("PilgrimRoads", "Pilgrim Roads", CivCategory.Faith, 3, "Hostels and shrines along the holy ways.", new CivEffect { trade = 1.08f, faith = 1.08f }),
        Pol("HolyOrders", "Holy Orders", CivCategory.Faith, 4, "Monks, nuns and friars in their houses.", new CivEffect { great = 1.2f, faith = 1.1f, growth = 0.97f }, tech: "Monasteries"),
        Pol("Inquisition", "Inquisition", CivCategory.Faith, 4, "Heresy hunted, doubters silenced.", new CivEffect { legit = 3f, faith = 1.15f, research = 0.92f, mood = -3f, harsh = 1 }, group: "worship"),
        Pol("DivineMandate", "Divine Mandate", CivCategory.Faith, 2, "The ruler speaks for heaven.", new CivEffect { legit = 4f, faith = 1.1f }, govs: new[] { "Theocracy", "Monarchy", "AbsoluteMonarchy", "AscendedSynod" }),
        Pol("FreedomOfWorship", "Freedom of Worship", CivCategory.Faith, 5, "Each may pray as they please.", new CivEffect { mood = 3f, culture = 1.1f, faith = 0.9f }, group: "worship"),
        Pol("CultOfReason", "Cult of Reason", CivCategory.Faith, 6, "Temples of philosophy in place of the old gods.", new CivEffect { research = 1.05f, culture = 1.05f, faith = 0.85f }, group: "worship"),
        Pol("SecularState", "Secular State", CivCategory.Faith, 8, "Church and state apart.", new CivEffect { research = 1.06f, faith = 0.7f, mood = 1f }, group: "worship"),
        Pol("LeyWorship", "Ley Worship", CivCategory.Faith, 11, "Prayers sung along the leylines.", new CivEffect { arcane = 1.1f, faith = 1.1f }),
        Pol("StarCommunion", "Star Communion", CivCategory.Faith, 13, "The faithful pray to the living stars.", new CivEffect { faith = 1.2f, cosmic = 1.05f }),

        // Science
        Pol("OralLearning", "Keepers of Lore", CivCategory.Science, 0, "Chosen children learn every story by heart.", new CivEffect { research = 1.03f, culture = 1.03f }),
        Pol("ScribalSchools", "Scribal Schools", CivCategory.Science, 1, "Clay tablets, reed pens and stern masters.", new CivEffect { research = 1.05f, tax = 0.97f }, tech: "Writing"),
        Pol("RoyalAcademy", "Royal Academy", CivCategory.Science, 5, "Scholars paid by the crown.", new CivEffect { research = 1.08f, great = 1.15f, tax = 0.95f }),
        Pol("Observatories", "Weather Observatories", CivCategory.Science, 6, "Barometers and signal flags warn of storms and floods.", new CivEffect { disaster = 0.85f, research = 1.02f }),
        Pol("PatentLaw", "Patent Law", CivCategory.Science, 7, "Inventors own their inventions.", new CivEffect { industry = 1.06f, research = 1.04f }),
        Pol("OpenScience", "Open Science", CivCategory.Science, 10, "Every paper free for all to read.", new CivEffect { research = 1.1f, sell = 0.95f }),
        Pol("ArcaneGrants", "Arcane Research Grants", CivCategory.Science, 11, "Purses for the brightest apprentices.", new CivEffect { arcane = 1.08f, research = 1.05f, tax = 0.95f }),
        Pol("AIAdvisors", "AI Advisors", CivCategory.Science, 12, "Thinking machines at every ministry.", new CivEffect { research = 1.08f, mood = -2f }),

        // Expansion and space
        Pol("PioneerGrants", "Pioneer Grants", CivCategory.Expansion, 2, "Free land for those who'll clear it.", new CivEffect { growth = 1.1f, tax = 0.95f }),
        Pol("FrontierForts", "Frontier Forts", CivCategory.Expansion, 3, "A stockade at every new settlement.", new CivEffect { threat = 0.92f, refund = 0.05f }),
        Pol("HomesteadActs", "Homestead Acts", CivCategory.Expansion, 7, "A quarter section to every family that farms it.", new CivEffect { growth = 1.12f, food = 1.05f, tax = 0.95f }),
        Pol("ColonialCharters", "Colonial Charters", CivCategory.Expansion, 12, "Colonies run by chartered companies.", new CivEffect { colonyAll = 1.08f, colonyGrowth = 1.2f, tax = 0.95f }, tech: "Starships"),
        Pol("TerraformingMandate", "Terraforming Mandate", CivCategory.Expansion, 12, "Every world shall be made green.", new CivEffect { food = 1.1f, disaster = 0.9f, industry = 0.95f }, tech: "Terraforming"),
        Pol("XenoBureau", "Xeno-Relations Bureau", CivCategory.Expansion, 12, "Diplomats and linguists for every native people.", new CivEffect { natives = 1f, trade = 1.1f }, tech: "Xenology"),
        Pol("StarLaneTolls", "Star Lane Tolls", CivCategory.Expansion, 13, "Every hyperlane pays its toll.", new CivEffect { trade = 1.15f, tax = 1.05f, natives = -0.3f }),
        Pol("VoidAdmiralty", "Void Admiralty", CivCategory.Expansion, 14, "Fleets patrol between the stars.", new CivEffect { threat = 0.88f, upkeep = 1.1f }),
        Pol("DysonMandate", "Dyson Mandate", CivCategory.Expansion, 15, "Harness every sun.", new CivEffect { cosmic = 1.15f, mood = -2f }),
        Pol("UniversalAccord", "Universal Accord", CivCategory.Expansion, 16, "All peoples, all worlds, one accord.", new CivEffect { all = 1.04f, mood = 3f, legit = 2f }),
    };

    // ---------- Taxes and tithes ----------

    public static readonly string[] CivTaxNames = { "None", "Low", "Normal", "High", "Heavy", "Crushing" };
    public static readonly float[] CivTaxRate = { 0f, 0.5f, 1f, 1.4f, 1.8f, 2.3f };
    public static readonly float[] CivTaxMood = { 5f, 2f, 0f, -4f, -8f, -13f };
    private static readonly int[] CivTaxHarsh = { 0, 0, 0, 0, 1, 2 };
    public static readonly string[] CivTitheNames = { "None", "Tithe", "Great Tithe" };

    public static CivGovernment GovernmentById(string id)
    {
        if (string.IsNullOrEmpty(id)) return Governments[0];
        foreach (var g in Governments) if (g.id == id) return g;
        return Governments[0];
    }

    public static CivPolicy PolicyById(string id)
    {
        foreach (var p in Policies) if (p.id == id) return p;
        return null;
    }

    public static string CivCategoryName(CivCategory c) => c == CivCategory.Expansion ? "Expansion and Space" : c.ToString();

    public static Color CivCategoryColour(CivCategory c) => c switch
    {
        CivCategory.Economy => new Color(0.95f, 0.82f, 0.45f),
        CivCategory.Military => new Color(0.95f, 0.5f, 0.45f),
        CivCategory.Society => new Color(0.6f, 0.85f, 0.95f),
        CivCategory.Faith => new Color(0.7f, 0.9f, 0.55f),
        CivCategory.Science => new Color(0.6f, 0.7f, 1f),
        _ => new Color(0.8f, 0.65f, 0.95f),
    };
}
