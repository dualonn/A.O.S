using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Culture and faith shared by all your worlds (saved on SettlementState).</summary>
[Serializable]
public class CultureState
{
    [Tooltip("Culture points to spend on traditions.")]
    public float culture;
    [Tooltip("Faith points to spend on a religion, blessings and great people.")]
    public float faith;
    public float cultureEarned, faithEarned;

    [Tooltip("Traditions adopted (ids); only the slotted ones work.")]
    public List<string> traditions = new List<string>();
    public List<string> slotted = new List<string>();

    [Tooltip("Your religion's name; empty until one is founded.")]
    public string religion = "";
    public List<string> tenets = new List<string>();

    [Tooltip("Towards the next great person, and how many have been born or hired.")]
    public float greatProgress;
    public int greatBorn;
    public int greatHired;
    public int nextPersonId = 1;
    public List<CultureGreatPerson> people = new List<CultureGreatPerson>();

    [Tooltip("Play time until a Blessing of Arms (or a general's rally) wears off.")]
    public float armsUntil;
}

/// <summary>A named great person: waiting to be used or settled, or settled in a building.</summary>
[Serializable]
public class CultureGreatPerson
{
    public int id;
    public string name;
    [Tooltip("Artist, Scientist, General, Merchant, Prophet or Engineer.")]
    public string kind;
    [Tooltip("Where they're settled: world index and building instance id (0: not settled).")]
    public int world = -1;
    public int building;
    public float bornAt;

    public bool Settled => building != 0;
}

/// <summary>A world's mood (saved on WorldState). Old saves start content.</summary>
[Serializable]
public class WorldCulture
{
    public bool started;
    [Tooltip("0 to 100: how content the people of this world are. 50 is neutral.")]
    public float mood = 60f;
    [Tooltip("Raids lost lately weigh on people; fades with time.")]
    public float weariness;
    [Tooltip("Play time when the next festival may be held here.")]
    public float festivalReadyAt;
    public bool unrestWarned;

    // Worked out once a second while the world ticks; not saved.
    [NonSerialized] public float target = 60f, cultureRate, faithRate, tick, prodBonus = 1f, taxBonus = 1f, coinBonus = 1f;
    [NonSerialized] public List<string> reasons;
}

/// <summary>A culture or faith building: what the setup makes and what it gives.</summary>
public struct CultureBuildingSpec
{
    public string id, name, age, desc;
    public int ageOrder, size, tall, workers;
    public bool faith;
}

/// <summary>A tradition (culture) or tenet (faith): a small global perk.</summary>
public class CulturePerk
{
    public string id, name, desc;
    public int age;
    public string[] res;          // resources the 'prod' bonus applies to (none: no resource bonus)
    public float prod = 1f, all = 1f, growth = 1f, culture = 1f, faith = 1f, tax = 1f, research = 1f,
        attack = 1f, health = 1f, threat = 1f, great = 1f, weariness = 1f, festival = 1f, mood;
}

/// <summary>
/// Culture and faith. Each world has a mood (food, room, culture and faith buildings, luxuries,
/// seasons, festivals, war weariness) that nudges how fast people move in and how hard they work.
/// Culture buildings and wonders make culture points for traditions (cheap perks in slots that
/// grow with the ages); faith buildings make faith for a religion with tenets, blessings and
/// great people. Great people are born now and then, to be used once or settled in a building.
/// </summary>
public partial class Settlement
{
    // ---------- Buildings ----------

    public static readonly CultureBuildingSpec[] CultureBuildingSpecs =
    {
        new CultureBuildingSpec { id = "CulStoryCircle", name = "Story Circle", age = "StoneAge", ageOrder = 0, size = 2, tall = 0, workers = 1,
            desc = "Logs round a fire and a painted stone, where the old stories are told. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulBardsHall", name = "Bards' Hall", age = "IronAge", ageOrder = 2, size = 2, tall = 1, workers = 2,
            desc = "A long hall of song and harp, where bards keep the deeds of the people. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulAmphitheatre", name = "Amphitheatre", age = "ClassicalAge", ageOrder = 3, size = 3, tall = 0, workers = 3,
            desc = "Stone tiers round a stage: comedies, tragedies and choruses for the whole city. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulPlayhouse", name = "Playhouse", age = "RenaissanceAge", ageOrder = 5, size = 3, tall = 1, workers = 4,
            desc = "A round timber theatre, a flag up when there's a play on. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulOperaHouse", name = "Opera House", age = "EnlightenmentAge", ageOrder = 6, size = 3, tall = 1, workers = 5,
            desc = "Gilded boxes, a painted ceiling and a full orchestra. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulMuseum", name = "Museum", age = "IndustrialAge", ageOrder = 7, size = 3, tall = 1, workers = 5,
            desc = "Columns, an iron-and-glass roof and halls of treasures from every age. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulCinema", name = "Picture Palace", age = "ModernAge", ageOrder = 8, size = 3, tall = 1, workers = 6,
            desc = "Moving pictures under a marquee of a hundred bulbs. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulBroadcastPalace", name = "Broadcast Palace", age = "ElectricAge", ageOrder = 9, size = 3, tall = 2, workers = 6,
            desc = "A chrome tower that sends music and news to every home on the air. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulDataArtsHub", name = "Data-Arts Hub", age = "InformationAge", ageOrder = 10, size = 3, tall = 1, workers = 6,
            desc = "A glass cube of screens and studios where art is made of light and numbers. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulIllusionTheatre", name = "Theatre of Illusions", age = "ArcaneAge", ageOrder = 11, size = 3, tall = 2, workers = 6,
            desc = "Mages stage dreams that walk off the stage among the crowd. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulStarDome", name = "Star Dome", age = "StellarAge", ageOrder = 13, size = 3, tall = 1, workers = 6,
            desc = "A dome where the whole sky is played as music. Makes culture and cheers the town." },
        new CultureBuildingSpec { id = "CulCosmicChoir", name = "Cosmic Choir", age = "GalacticAge", ageOrder = 15, size = 3, tall = 2, workers = 6,
            desc = "Voices of a thousand worlds, sung through crystal into the void. Makes culture and cheers the town." },

        new CultureBuildingSpec { id = "FthShrine", name = "Shrine", age = "StoneAge", ageOrder = 0, size = 2, tall = 0, workers = 1, faith = true,
            desc = "A carved spirit post and an offering stone. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthTemple", name = "Temple", age = "IronAge", ageOrder = 2, size = 2, tall = 1, workers = 2, faith = true,
            desc = "Columns, a sacred flame and a priest at the door. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthCathedral", name = "Cathedral", age = "MedievalAge", ageOrder = 4, size = 3, tall = 2, workers = 4, faith = true,
            desc = "A rose window, a bell tower and a choir at vespers. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthBasilica", name = "Basilica", age = "EnlightenmentAge", ageOrder = 6, size = 3, tall = 2, workers = 5, faith = true,
            desc = "A great dome on a drum of columns, gold inside. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthLightningChapel", name = "Lightning Chapel", age = "ElectricAge", ageOrder = 9, size = 3, tall = 2, workers = 6, faith = true,
            desc = "A chapel of brass and neon whose spire calls down the storm. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthArcaneSanctum", name = "Arcane Sanctum", age = "ArcaneAge", ageOrder = 11, size = 3, tall = 2, workers = 6, faith = true,
            desc = "Floating rings of runes round a well of mana. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthSunTemple", name = "Sun Temple", age = "StellarAge", ageOrder = 13, size = 3, tall = 1, workers = 6, faith = true,
            desc = "A stepped temple under a captured spark of a star. Makes faith (and a little culture), and comforts the town." },
        new CultureBuildingSpec { id = "FthCosmicCathedral", name = "Cosmic Cathedral", age = "GalacticAge", ageOrder = 15, size = 3, tall = 2, workers = 6, faith = true,
            desc = "Spires of starmetal round a window onto the birth of stars. Makes faith (and a little culture), and comforts the town." },
    };

    private static Dictionary<string, CultureBuildingSpec> cultureSpecById;

    public static bool CultureSpecOf(string id, out CultureBuildingSpec spec)
    {
        if (cultureSpecById == null)
        {
            cultureSpecById = new Dictionary<string, CultureBuildingSpec>();
            foreach (var s in CultureBuildingSpecs) cultureSpecById[s.id] = s;
        }
        spec = default;
        return id != null && cultureSpecById.TryGetValue(id, out spec);
    }

    /// <summary>A culture or faith building (for the build menu's Culture section).</summary>
    public static bool IsCultureBuilding(string id) => CultureSpecOf(id, out _);

    /// <summary>Culture (or faith) a building of this age makes each second, before upgrades.</summary>
    private static float CultureBaseRate(int age) => 0.1f * Mathf.Pow(1f + Mathf.Max(0, age), 1.25f);

    private static float AgeScaleOf(int age) => Mathf.Pow(1f + Mathf.Max(0, age), 1.25f);

    private int AgeOrderOf(BuildingDefinition def) => def != null && def.age != null ? def.age.order : 0;

    // ---------- Traditions and tenets ----------

    public static readonly CulturePerk[] Traditions =
    {
        new CulturePerk { id = "OralTradition", name = "Oral Tradition", age = 0, desc = "Culture +15%.", culture = 1.15f },
        new CulturePerk { id = "HearthKin", name = "Hearth and Kin", age = 0, desc = "+4 mood on every world.", mood = 4f },
        new CulturePerk { id = "HuntersLore", name = "Hunters' Lore", age = 0, desc = "Food +5%.", res = new[] { "Food" }, prod = 1.05f },
        new CulturePerk { id = "Craftsmanship", name = "Craftsmanship", age = 1, desc = "Wood and stone +6%.", res = new[] { "Wood", "Stone" }, prod = 1.06f },
        new CulturePerk { id = "AncestorFeasts", name = "Ancestor Feasts", age = 1, desc = "Festivals cost 20% less and cheer people 25% more.", festival = 1.25f },
        new CulturePerk { id = "WarriorCode", name = "Warrior Code", age = 2, desc = "Troops hit 5% harder.", attack = 1.05f },
        new CulturePerk { id = "Hospitality", name = "Hospitality", age = 2, desc = "People move in 10% faster.", growth = 1.1f },
        new CulturePerk { id = "CivicPride", name = "Civic Pride", age = 3, desc = "Taxes +10%.", tax = 1.1f },
        new CulturePerk { id = "Philosophy", name = "Philosophy", age = 3, desc = "Research 4% faster.", research = 1.04f },
        new CulturePerk { id = "Chivalry", name = "Chivalry", age = 4, desc = "Troops have 5% more health.", health = 1.05f },
        new CulturePerk { id = "Patronage", name = "Patronage of the Arts", age = 4, desc = "Great people come 20% sooner.", great = 1.2f },
        new CulturePerk { id = "Humanism", name = "Humanism", age = 5, desc = "+3 mood and culture +10%.", mood = 3f, culture = 1.1f },
        new CulturePerk { id = "EnlightenedRule", name = "Enlightened Rule", age = 6, desc = "Research 3% faster and taxes +5%.", research = 1.03f, tax = 1.05f },
        new CulturePerk { id = "Industriousness", name = "Industriousness", age = 7, desc = "Everything you produce +3%.", all = 1.03f },
        new CulturePerk { id = "CivilDefence", name = "Civil Defence", age = 8, desc = "Raids are 6% weaker.", threat = 0.94f },
        new CulturePerk { id = "MassMedia", name = "Mass Media", age = 9, desc = "Culture +20%.", culture = 1.2f },
        new CulturePerk { id = "OpenNetworks", name = "Open Networks", age = 10, desc = "Research 5% faster.", research = 1.05f },
        new CulturePerk { id = "ArcanePatronage", name = "Arcane Patronage", age = 11, desc = "Mana and essence +8%.", res = new[] { "Mana", "Essence", "Manastone" }, prod = 1.08f },
        new CulturePerk { id = "StarLore", name = "Star Lore", age = 12, desc = "Great people come 25% sooner.", great = 1.25f },
        new CulturePerk { id = "StoicResolve", name = "Stoic Resolve", age = 13, desc = "War weariness fades twice as fast.", weariness = 2f },
        new CulturePerk { id = "UnityOfWorlds", name = "Unity of Worlds", age = 15, desc = "+4 mood and everything you produce +2%.", mood = 4f, all = 1.02f },
    };

    public static readonly CulturePerk[] Tenets =
    {
        new CulturePerk { id = "HarvestRites", name = "Harvest Rites", desc = "Food +8%.", res = new[] { "Food" }, prod = 1.08f },
        new CulturePerk { id = "SacredGroves", name = "Sacred Groves", desc = "Wood +8%.", res = new[] { "Wood" }, prod = 1.08f },
        new CulturePerk { id = "HolyLabour", name = "Holy Labour", desc = "Everything you produce +3%.", all = 1.03f },
        new CulturePerk { id = "Pilgrimage", name = "Pilgrimage", desc = "Taxes +12%.", tax = 1.12f },
        new CulturePerk { id = "WarriorFaith", name = "Warrior Faith", desc = "Troops hit 6% harder.", attack = 1.06f },
        new CulturePerk { id = "ShieldOfFaith", name = "Shield of Faith", desc = "Troops have 6% more health.", health = 1.06f },
        new CulturePerk { id = "Charity", name = "Charity", desc = "+5 mood on every world.", mood = 5f },
        new CulturePerk { id = "FertilityRites", name = "Fertility Rites", desc = "People move in 12% faster.", growth = 1.12f },
        new CulturePerk { id = "MonasticLearning", name = "Monastic Learning", desc = "Research 5% faster.", research = 1.05f },
        new CulturePerk { id = "VowsOfPeace", name = "Vows of Peace", desc = "Raids are 7% weaker.", threat = 0.93f },
        new CulturePerk { id = "Mysticism", name = "Mysticism", desc = "Mana and essence +10%.", res = new[] { "Mana", "Essence", "Manastone" }, prod = 1.1f },
        new CulturePerk { id = "MissionaryZeal", name = "Missionary Zeal", desc = "Faith +25%.", faith = 1.25f },
        new CulturePerk { id = "SaintsAndMiracles", name = "Saints and Miracles", desc = "Great people come 25% sooner.", great = 1.25f },
    };

    public static CulturePerk TraditionById(string id) => Array.Find(Traditions, p => p.id == id);
    public static CulturePerk TenetById(string id) => Array.Find(Tenets, p => p.id == id);

    // ---------- State ----------

    public CultureState Culture
    {
        get
        {
            if (State.culture == null) State.culture = new CultureState();
            var c = State.culture;
            c.traditions ??= new List<string>();
            c.slotted ??= new List<string>();
            c.tenets ??= new List<string>();
            c.people ??= new List<CultureGreatPerson>();
            c.religion ??= "";
            return c;
        }
    }

    public WorldCulture CultureOf(WorldState w)
    {
        if (w.culture == null) w.culture = new WorldCulture();
        if (w.culture.reasons == null) w.culture.reasons = new List<string>();
        return w.culture;
    }

    /// <summary>The mood of the world you're on (0 to 100; 50 is neutral).</summary>
    public float MoodHere => MoodOf(CurrentWorld);

    public float MoodOf(WorldState w)
    {
        var wc = CultureOf(w);
        return wc.started ? wc.mood : 60f;
    }

    public static string MoodName(float m) =>
        m < 25 ? "Unrest" : m < 40 ? "Discontent" : m < 65 ? "Content" : m < 82 ? "Happy" : "Joyful";

    public static Color MoodColour(float m) =>
        m < 25 ? new Color(1f, 0.45f, 0.4f) : m < 40 ? new Color(1f, 0.75f, 0.4f) : m < 65 ? new Color(0.9f, 0.9f, 0.8f) : m < 82 ? new Color(0.6f, 0.95f, 0.55f) : new Color(1f, 0.85f, 0.4f);

    /// <summary>Mood's effect on work: -10% in unrest to +8% when joyful.</summary>
    public static float MoodProduction(float m) => m >= 50 ? 1f + (m - 50f) / 50f * 0.08f : 1f - (50f - m) / 50f * 0.1f;

    /// <summary>Mood's effect on how fast people move in: -25% to +25%.</summary>
    public static float MoodGrowth(float m) => m >= 50 ? 1f + (m - 50f) / 50f * 0.25f : 1f - (50f - m) / 50f * 0.25f;

    /// <summary>Why the people of the world you're on feel as they do (one line each).</summary>
    public List<string> MoodReasonsHere => CultureOf(CurrentWorld).reasons;

    public float CultureRateTotal { get { float r = 0; foreach (var w in State.worlds) r += CultureOf(w).cultureRate; return r; } }
    public float FaithRateTotal { get { float r = 0; foreach (var w in State.worlds) r += CultureOf(w).faithRate; return r; } }

    public int TraditionSlots => Mathf.Min(6, 1 + Mathf.Max(0, State.ageIndex) / 3);

    public bool HasReligion => !string.IsNullOrEmpty(Culture.religion);

    public int TenetSlots => State.ageIndex >= 4 ? 3 : 2;

    // ---------- Perk totals (cached) ----------

    private class CulturePerkTotals
    {
        public readonly Dictionary<string, float> prod = new Dictionary<string, float>();
        public float all = 1, growth = 1, culture = 1, faith = 1, tax = 1, research = 1, attack = 1, health = 1, threat = 1, great = 1, weariness = 1, festival = 1, mood;
        public int scientists, generals;

        public void Add(CulturePerk p)
        {
            if (p == null) return;
            if (p.res != null)
                foreach (var r in p.res) prod[r] = (prod.TryGetValue(r, out var f) ? f : 1f) * p.prod;
            all *= p.all; growth *= p.growth; culture *= p.culture; faith *= p.faith; tax *= p.tax; research *= p.research;
            attack *= p.attack; health *= p.health; threat *= p.threat; great *= p.great; weariness *= p.weariness; festival *= p.festival; mood += p.mood;
        }
    }

    private CulturePerkTotals cuTotals;
    private float cuTotalsAt = -99f;

    private void CultureDirty() => cuTotalsAt = -99f;

    private CulturePerkTotals CuTotals()
    {
        if (cuTotals != null && Mathf.Abs(State.playTime - cuTotalsAt) < 1f) return cuTotals;
        cuTotalsAt = State.playTime;
        var t = new CulturePerkTotals();
        var c = Culture;
        foreach (var id in c.slotted) if (c.traditions.Contains(id)) t.Add(TraditionById(id));
        if (HasReligion) foreach (var id in c.tenets) t.Add(TenetById(id));
        foreach (var p in c.people)
        {
            if (!p.Settled || !SettledStands(p)) continue;
            if (p.kind == GreatScientist) t.scientists++;
            if (p.kind == GreatGeneral) t.generals++;
        }
        t.research *= 1f + 0.04f * Mathf.Min(3, t.scientists);
        t.attack *= 1f + 0.04f * Mathf.Min(3, t.generals);
        t.health *= 1f + 0.04f * Mathf.Min(3, t.generals);
        cuTotals = t;
        return t;
    }

    private BuildingState SettledBuilding(CultureGreatPerson p)
    {
        if (!p.Settled || p.world < 0 || p.world >= State.worlds.Count) return null;
        return State.worlds[p.world].buildings.Find(b => b.instanceId == p.building);
    }

    private bool SettledStands(CultureGreatPerson p)
    {
        var b = SettledBuilding(p);
        return b != null && b.IsBuilt && b.health > 0;
    }

    // ---------- Hooks into the rest of the game ----------

    /// <summary>Mood, traditions, tenets and great people on a resource's production here.</summary>
    public float CultureProductionFactor(ResourceType r)
    {
        if (State == null) return 1f;
        var wc = CultureOf(CurrentWorld);
        var t = CuTotals();
        float f = MoodProduction(MoodOf(CurrentWorld)) * t.all * wc.prodBonus;
        if (r != null)
        {
            if (t.prod.Count > 0 && t.prod.TryGetValue(r.id, out var p)) f *= p;
            if (r.id == "Coin" || r.id == "Gold") f *= wc.coinBonus;
        }
        return f;
    }

    public float CultureGrowthFactor => State == null ? 1f : MoodGrowth(MoodHere) * CuTotals().growth;

    public float CultureResearchFactor => State == null ? 1f : CuTotals().research;

    public float CultureTaxFactor => State == null ? 1f : CuTotals().tax * CultureOf(CurrentWorld).taxBonus;

    public float CultureThreatFactor => State == null ? 1f : CuTotals().threat;

    /// <summary>Traditions, tenets, generals and blessings of arms on your troops.</summary>
    public void CultureUnitModifier(UnitStatModifier m)
    {
        if (m == null || State == null) return;
        var t = CuTotals();
        m.attackMultiplier *= t.attack * (Culture.armsUntil > State.playTime ? 1.1f : 1f);
        m.healthMultiplier *= t.health;
    }

    /// <summary>Raiders broke through: people here grow weary of war.</summary>
    public void CultureRaidLost(float severity)
    {
        var wc = CultureOf(CurrentWorld);
        wc.weariness = Mathf.Min(30f, wc.weariness + 5f + Mathf.Clamp01(severity) * 15f);
    }

    // ---------- The tick ----------

    private static readonly string[] LuxuryIds = { "Gold", "Coin", "Mana", "Essence", "Aether", "Runestone", "Quintessence", "Stardust" };

    /// <summary>Once a second for this world: culture and faith made, the mood worked out and moved.</summary>
    private void TickCulture(float dt)
    {
        var wc = CultureOf(CurrentWorld);
        wc.tick += dt;
        if (wc.tick < 1f) return;
        float step = Mathf.Min(wc.tick, 10f);
        wc.tick = 0f;
        var c = Culture;
        var t = CuTotals();

        // Culture and faith from buildings, wonders and the great people settled in them.
        float culture = 0f, faith = 0f, cultureMood = 0f, faithMood = 0f, wonderMood = 0f, greatMood = 0f;
        int cultureCount = 0, faithCount = 0;
        wc.prodBonus = 1f; wc.taxBonus = 1f; wc.coinBonus = 1f;
        var foodKinds = new HashSet<string>();
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            if (def.production.Count > 0 && def.production.Exists(r => r.resource != null && r.resource == Database.food && r.perSecond > 0))
                foodKinds.Add(def.id);
            if (def.wonder)
            {
                culture += 0.3f * CultureBaseRate(AgeOrderOf(def));
                wonderMood += 3f;
                continue;
            }
            if (!CultureSpecOf(def.id, out var spec)) continue;
            var stats = StatsOf(def, b.upgradeLevel);
            float work = StaffingOf(b) * Mathf.Clamp01(b.health / Mathf.Max(1f, stats.maxHealth)) * stats.output;
            float rate = CultureBaseRate(AgeOrderOf(def)) * work;
            var person = c.people.Find(p => p.building == b.instanceId && p.world == State.currentWorld);
            float doubled = person != null && (person.kind == GreatArtist || person.kind == GreatProphet) ? 2f : 1f;
            if (person != null)
            {
                if (person.kind == GreatArtist || person.kind == GreatProphet) greatMood += 3f;
                if (person.kind == GreatMerchant) { wc.taxBonus *= 1.15f; wc.coinBonus *= 1.1f; }
                if (person.kind == GreatEngineer) wc.prodBonus *= 1.05f;
            }
            float liftMood = (spec.faith ? 2f : 3f) + b.upgradeLevel;
            if (StaffingOf(b) <= 0f) liftMood *= 0.5f;
            if (spec.faith)
            {
                faith += rate * doubled;
                culture += rate * 0.25f;
                faithMood += liftMood;
                faithCount++;
            }
            else
            {
                culture += rate * doubled;
                cultureMood += liftMood;
                cultureCount++;
            }
        }
        float moodNow = MoodOf(CurrentWorld);
        culture *= t.culture * (moodNow >= 82f ? 1.2f : 1f);
        faith *= t.faith;
        wc.cultureRate = culture;
        wc.faithRate = faith;
        c.culture += culture * step;
        c.faith += faith * step;
        c.cultureEarned += culture * step;
        c.faithEarned += faith * step;
        c.greatProgress += (culture + faith) * step * t.great;

        // War weariness fades.
        wc.weariness = Mathf.Max(0f, wc.weariness - step / 15f * t.weariness);

        // The mood people are heading for, and why.
        var why = wc.reasons;
        why.Clear();
        float target = 55f;
        void Reason(float v, string text)
        {
            int n = Mathf.RoundToInt(v);
            if (n == 0) return;
            target += v;
            why.Add($"<color={(n > 0 ? "#9e9" : "#f99")}>{(n > 0 ? "+" : "")}{n}</color> {text}");
        }
        var food = Database.food;
        float pop = State.population;
        if (food != null && pop >= 1f)
        {
            float stock = GetAmount(food), need = pop * Database.foodPerPersonPerSecond;
            if (stock < 1f) Reason(-15f, "starving");
            else if (stock >= need * 120f) Reason(4f, "well fed");
        }
        if (foodKinds.Count > 1) Reason(2f * Mathf.Min(3, foodKinds.Count - 1), $"food variety ({foodKinds.Count} kinds)");
        if (Housing > 0)
        {
            float spare = (Housing - pop) / Housing;
            if (spare >= 0.1f) Reason(4f, "room to live");
            else if (pop >= Housing - 0.5f) Reason(-3f, "cramped homes");
        }
        Reason(-Mathf.Min(10f, Mathf.Floor(pop / 50f)), "a crowded city");
        Reason(Mathf.Min(18f, cultureMood), $"culture ({cultureCount} building{(cultureCount == 1 ? "" : "s")})");
        Reason(Mathf.Min(12f, faithMood), $"faith ({faithCount} building{(faithCount == 1 ? "" : "s")})");
        Reason(Mathf.Min(9f, wonderMood), "wonders");
        if (HasReligion) Reason(3f, $"a shared faith ({c.religion})");
        int lux = 0;
        foreach (var id in LuxuryIds) if (GetAmountId(id) >= 50f && IsResourceUnlocked(id)) lux++;
        Reason(2f * Mathf.Min(4, lux), "luxuries in store");
        switch (CurrentSeason)
        {
            case Season.Summer: case Season.Spring: Reason(2f, "fine weather"); break;
            case Season.Winter: Reason(-4f, "a hard winter"); break;
            case Season.Rains: Reason(-1f, "the rains"); break;
        }
        Reason(-wc.weariness, "war weariness");
        Reason(t.mood, "traditions and tenets");
        Reason(greatMood, "great people living here");
        float effects = 0f, festival = 0f;
        foreach (var x in Ev.effects)
        {
            if (x.world != State.currentWorld) continue;
            if (x.what == "mood") effects += x.factor;
            else if (x.what == "growth" && x.factor > 1f && x.label != null && x.label.StartsWith("Festival")) festival = 6f;
        }
        Reason(effects + festival, "festivals and blessings");
        wc.target = Mathf.Clamp(target, 0f, 100f);

        // Old saves (and new worlds) start content; then the mood drifts towards its target.
        if (!wc.started)
        {
            wc.started = true;
            wc.mood = Mathf.Max(wc.target, 60f);
        }
        float diff = wc.target - wc.mood;
        float move = Mathf.Max(0.3f, Mathf.Abs(diff) * 0.05f) * step;
        wc.mood = Mathf.Clamp(wc.mood + Mathf.Clamp(diff, -move, move), 0f, 100f);
        if (wc.mood < 25f && !wc.unrestWarned)
        {
            wc.unrestWarned = true;
            Notify("<color=#f97>Unrest!</color> People are unhappy and working less. (See the Culture panel: festivals, culture, faith and food help.)");
            if (!offWorld) GameAudio.UI("plague_bell", 0.7f);
        }
        else if (wc.mood > 32f) wc.unrestWarned = false;

        // A great person is born (announced from the world you're on).
        if (!offWorld)
        {
            TryGreatBirth();
            TickCultureShared();
        }
    }

    private bool IsResourceUnlocked(string id)
    {
        var r = Database.GetResource(id);
        return r != null && (r.unlockedIn == null || r.unlockedIn.order <= State.ageIndex);
    }

    // ---------- Traditions ----------

    public float TraditionCost(CulturePerk p) => Mathf.Round(50f * AgeScaleOf(p.age));

    public bool CultureAdopt(string id, out string why)
    {
        var p = TraditionById(id);
        var c = Culture;
        why = null;
        if (p == null) { why = "No such tradition."; return false; }
        if (c.traditions.Contains(id)) { why = "Already adopted."; return false; }
        if (State.ageIndex < p.age) { why = $"Needs the {Database.GetAge(p.age)?.displayName ?? "later age"}."; return false; }
        float cost = TraditionCost(p);
        if (c.culture < cost) { why = $"Needs {cost:0} culture (you have {c.culture:0})."; return false; }
        c.culture -= cost;
        c.traditions.Add(id);
        if (c.slotted.Count < TraditionSlots) c.slotted.Add(id);
        CultureDirty();
        Notify($"<color=#e9c>New tradition: {p.name}.</color> {p.desc}");
        GameAudio.UI("culture_tradition");
        return true;
    }

    /// <summary>Put an adopted tradition in a slot, or take it out (free).</summary>
    public bool CultureToggleSlot(string id, out string why)
    {
        var c = Culture;
        why = null;
        if (!c.traditions.Contains(id)) { why = "Adopt it first."; return false; }
        if (c.slotted.Remove(id)) { CultureDirty(); return true; }
        if (c.slotted.Count >= TraditionSlots) { why = $"All {TraditionSlots} slots are full: take one out first (more slots come with the ages)."; return false; }
        c.slotted.Add(id);
        CultureDirty();
        return true;
    }

    // ---------- Religion ----------

    private static readonly string[] FaithNamesA = { "the Sun", "the Long Fire", "the Mother", "the Sky Father", "the Deep Root", "the Seven Stars", "the Silver Moon", "the First Light", "the Turning Wheel", "the Quiet Flame", "the River", "the Endless Sky" };
    private static readonly string[] FaithNamesB = { "The Way of", "The Children of", "The Faith of", "The Covenant of", "The Light of", "The Order of" };

    public string RandomReligionName() =>
        $"{FaithNamesB[UnityEngine.Random.Range(0, FaithNamesB.Length)]} {FaithNamesA[UnityEngine.Random.Range(0, FaithNamesA.Length)]}";

    public float FoundingCost => Mathf.Round(60f * AgeScale);
    public float ExtraTenetCost => Mathf.Round(120f * AgeScale);

    public bool FaithFound(string name, List<string> tenets, out string why)
    {
        var c = Culture;
        why = null;
        if (HasReligion) { why = "You already have a religion."; return false; }
        if (tenets == null || tenets.Count != 2) { why = "Pick two tenets."; return false; }
        if (c.faith < FoundingCost) { why = $"Needs {FoundingCost:0} faith (you have {c.faith:0}). Build shrines to make faith."; return false; }
        c.faith -= FoundingCost;
        c.religion = string.IsNullOrEmpty(name) ? RandomReligionName() : name;
        c.tenets = new List<string>(tenets);
        CultureDirty();
        Notify($"<color=#ad9>A religion is founded: {c.religion}!</color> Tenets: {string.Join(", ", c.tenets.ConvertAll(t => TenetById(t)?.name))}.");
        GameAudio.UI("faith_founded");
        return true;
    }

    public bool FaithAddTenet(string id, out string why)
    {
        var c = Culture;
        why = null;
        if (!HasReligion) { why = "Found a religion first."; return false; }
        if (c.tenets.Contains(id) || TenetById(id) == null) { why = "Already a tenet."; return false; }
        if (c.tenets.Count >= TenetSlots) { why = c.tenets.Count >= 3 ? "Your faith has all its tenets." : "A third tenet comes in the Medieval Age."; return false; }
        if (c.faith < ExtraTenetCost) { why = $"Needs {ExtraTenetCost:0} faith."; return false; }
        c.faith -= ExtraTenetCost;
        c.tenets.Add(id);
        CultureDirty();
        Notify($"{c.religion} takes up a new tenet: {TenetById(id).name}.");
        GameAudio.UI("faith_blessing");
        return true;
    }

    public void FaithRename(string name)
    {
        if (HasReligion && !string.IsNullOrEmpty(name)) Culture.religion = name;
    }

    // ---------- Faith buys things ----------

    public struct CultureOffer
    {
        public string key, name, desc;
        public float faith;
        public List<ResourceAmount> cost;
    }

    public List<CultureOffer> FaithOffers()
    {
        float s = AgeScale;
        var list = new List<CultureOffer>
        {
            new CultureOffer { key = "harvest", name = "Blessing of the Harvest", desc = "Food +15% on this world for 5 minutes.", faith = Mathf.Round(40 * s) },
            new CultureOffer { key = "toil", name = "Blessing of Toil", desc = "Everything here comes 6% faster for 5 minutes.", faith = Mathf.Round(70 * s) },
            new CultureOffer { key = "calm", name = "Calm the Unrest", desc = "+20 mood here for 4 minutes, and half the war weariness lifts.", faith = Mathf.Round(50 * s) },
            new CultureOffer { key = "arms", name = "Blessing of Arms", desc = "All your troops hit 10% harder for 4 minutes.", faith = Mathf.Round(60 * s) },
        };
        return list;
    }

    public bool FaithBuy(string key, out string why)
    {
        why = null;
        var offer = FaithOffers().Find(o => o.key == key);
        if (offer.key == null) { why = "Nothing to buy."; return false; }
        var c = Culture;
        if (c.faith < offer.faith) { why = $"Needs {offer.faith:0} faith (you have {c.faith:0})."; return false; }
        string label = offer.name;
        if (key != "arms" && Ev.effects.Exists(x => x.world == State.currentWorld && x.label == label)) { why = "That blessing is already on this world."; return false; }
        if (key == "arms" && c.armsUntil > State.playTime) { why = "Your troops are already blessed."; return false; }
        c.faith -= offer.faith;
        switch (key)
        {
            case "harvest": AddEffect(label, "prod", "Food", 1.15f, 300f); break;
            case "toil": AddEffect(label, "prod", null, 1.06f, 300f); break;
            case "calm":
                AddEffect(label, "mood", null, 20f, 240f);
                CultureOf(CurrentWorld).weariness *= 0.5f;
                break;
            case "arms": c.armsUntil = State.playTime + 240f; break;
        }
        Notify($"<color=#ad9>{offer.name}.</color> {offer.desc}");
        GameAudio.UI("faith_blessing");
        RaiseChanged();
        return true;
    }

    // ---------- Festivals ----------

    public List<CultureOffer> CultureFestivals()
    {
        float s = AgeScale * (1f / CuTotals().festival);
        int age = State.ageIndex;
        bool coin = IsResourceUnlocked("Coin");
        var list = new List<CultureOffer>
        {
            new CultureOffer { key = "feast", name = Era(age, "A Great Feast", "A Banquet", "A Street Feast"),
                desc = $"+{8 * CuTotals().festival:0} mood here for 4 minutes.", cost = CostOf(("Food", 100 * s)) },
            new CultureOffer { key = "games", name = Era(age, "Dances and Games", "A Tournament", "Festival of Lights"),
                desc = $"+{12 * CuTotals().festival:0} mood here for 5 minutes, and some culture.",
                cost = coin ? CostOf(("Food", 120 * s), ("Coin", 80 * s)) : CostOf(("Food", 120 * s), ("Wood", 100 * s)) },
        };
        if (age >= 3)
            list.Add(new CultureOffer { key = "grand", name = Era(age, "The Grand Festival", "A Carnival", "A Star Carnival"),
                desc = $"+{18 * CuTotals().festival:0} mood and people move in 15% faster here for 6 minutes, and a good deal of culture.",
                cost = CostOf(("Food", 220 * s), ("Coin", 150 * s), ("Gold", 50 * s)) });
        return list;
    }

    /// <summary>Seconds until a festival may be held on the world you're on (0: now).</summary>
    public float FestivalWait => Mathf.Max(0f, CultureOf(CurrentWorld).festivalReadyAt - State.playTime);

    public bool CultureHoldFestival(string key, out string why)
    {
        why = null;
        var f = CultureFestivals().Find(o => o.key == key);
        if (f.key == null) { why = "No such festival."; return false; }
        if (FestivalWait > 0f) { why = $"People are still getting over the last one ({FestivalWait / 60f:0.0} min)."; return false; }
        if (!CanAfford(f.cost)) { why = "Not enough: " + CostText(f.cost) + "."; return false; }
        Spend(f.cost);
        float boost = CuTotals().festival;
        var wc = CultureOf(CurrentWorld);
        var c = Culture;
        float secs;
        switch (key)
        {
            case "feast":
                secs = 240f;
                AddEffect($"{f.name}: people are cheerful", "mood", null, 8f * boost, secs);
                break;
            case "games":
                secs = 300f;
                AddEffect($"{f.name}: people are cheerful", "mood", null, 12f * boost, secs);
                c.culture += 25f * AgeScale;
                break;
            default:
                secs = 360f;
                AddEffect($"{f.name}: people are joyful", "mood", null, 18f * boost, secs);
                AddEffect($"{f.name}: people move in", "growth", null, 1.15f, secs);
                c.culture += 70f * AgeScale;
                break;
        }
        wc.festivalReadyAt = State.playTime + secs + 240f;
        Notify($"<color=#fd8>{f.name}!</color> {f.desc}");
        GameAudio.UI("festival");
        RaiseChanged();
        return true;
    }

    // ---------- Great people ----------

    public const string GreatArtist = "Artist", GreatScientist = "Scientist", GreatGeneral = "General",
        GreatMerchant = "Merchant", GreatProphet = "Prophet", GreatEngineer = "Engineer";

    public static readonly string[] GreatKinds = { GreatArtist, GreatScientist, GreatGeneral, GreatMerchant, GreatProphet, GreatEngineer };

    public const int MaxWaitingGreat = 3;

    public float GreatNeeded => Mathf.Round(120f * AgeScale * (1f + 0.35f * Mathf.Min(20, Culture.greatBorn)));

    public float GreatHireCost => Mathf.Round(200f * AgeScale * (1f + 0.5f * Mathf.Min(10, Culture.greatHired)));

    public int GreatWaiting => Culture.people.FindAll(p => !p.Settled).Count;

    private void TryGreatBirth()
    {
        var c = Culture;
        float need = GreatNeeded;
        if (c.greatProgress < need) return;
        if (GreatWaiting >= MaxWaitingGreat) { c.greatProgress = need; return; }
        c.greatProgress -= need;
        float cu = Mathf.Max(0.01f, CultureRateTotal), fa = FaithRateTotal, both = cu + fa;
        float[] w = { cu, 0.4f * both, 0.3f * both, 0.3f * both, fa * 1.2f, 0.3f * both };
        float sum = 0f;
        foreach (var x in w) sum += x;
        float roll = UnityEngine.Random.value * sum;
        string kind = GreatKinds[0];
        for (int i = 0; i < w.Length; i++) { roll -= w[i]; if (roll <= 0f) { kind = GreatKinds[i]; break; } }
        var p = NewGreat(kind);
        c.greatBorn++;
        Notify($"<color=#fd6><b>A great person is born: {p.name}!</b></color> Use them once, or settle them in a culture or faith building (Culture panel).");
        GameAudio.UI("great_person");
    }

    private CultureGreatPerson NewGreat(string kind)
    {
        var c = Culture;
        var p = new CultureGreatPerson { id = c.nextPersonId++, kind = kind, name = GreatName(kind), bornAt = State.playTime };
        c.people.Add(p);
        RaiseChanged();
        return p;
    }

    private static readonly string[][] GreatFirstNames =
    {
        new[] { "Ura", "Tok", "Mira", "Brann", "Ishka", "Oru", "Kela", "Dun", "Asha", "Tarn" },                 // early
        new[] { "Aurelia", "Cassian", "Hild", "Matthias", "Isolde", "Lorenzo", "Beatrix", "Anselm", "Elif", "Tomas" }, // middle
        new[] { "Ada", "Nikola", "Grace", "Ludo", "Marisol", "Kenji", "Vera", "Otto", "Imani", "Felix" },       // modern
        new[] { "Zeph", "Lyra", "Orin", "Nova", "Kael", "Sirra", "Vex", "Aster", "Thalo", "Ione" },             // cosmic
    };

    private static readonly string[] GreatSurnames = { "Ashford", "Vell", "Marrow", "Quill", "Stonebrook", "Halloway", "Brightwater", "Thorne", "Okafor", "Lindqvist", "Varga", "Castell" };

    private string GreatName(string kind)
    {
        int age = State.ageIndex;
        int band = age < 3 ? 0 : age < 8 ? 1 : age < 11 ? 2 : 3;
        var firsts = GreatFirstNames[band];
        string first = firsts[UnityEngine.Random.Range(0, firsts.Length)];
        string title = GreatTitle(kind, age);
        if (band == 0) return $"{first} the {title}";
        return $"{first} {GreatSurnames[UnityEngine.Random.Range(0, GreatSurnames.Length)]}, {title.ToLower()}";
    }

    public static string GreatTitle(string kind, int age) => kind switch
    {
        GreatArtist => age < 3 ? "Storyteller" : age < 8 ? "Painter" : age < 11 ? "Composer" : "Dream-weaver",
        GreatScientist => age < 3 ? "Wise" : age < 8 ? "Philosopher" : age < 11 ? "Scientist" : "Cosmologist",
        GreatGeneral => age < 3 ? "Bold" : age < 8 ? "Marshal" : age < 11 ? "General" : "Fleet Admiral",
        GreatMerchant => age < 3 ? "Trader" : age < 8 ? "Merchant Prince" : age < 11 ? "Industrialist" : "Star Trader",
        GreatProphet => age < 3 ? "Seer" : age < 8 ? "Prophet" : age < 11 ? "Preacher" : "Oracle",
        GreatEngineer => age < 3 ? "Builder" : age < 8 ? "Master Mason" : age < 11 ? "Engineer" : "Worldsmith",
        _ => kind,
    };

    public static string GreatUseText(string kind) => kind switch
    {
        GreatArtist => "Masterpiece: a burst of culture, and +10 mood here for 5 minutes.",
        GreatScientist => "Breakthrough: all research under way jumps 35% ahead.",
        GreatGeneral => "Rally: war weariness lifts on every world, and troops hit 10% harder for 5 minutes.",
        GreatMerchant => "Trade mission: a fortune in coin (or goods, before coin).",
        GreatProphet => "Revelation: a burst of faith, and +8 mood here for 5 minutes.",
        GreatEngineer => "Master build: everything being built or upgraded on this world is all but done.",
        _ => "",
    };

    public static string GreatSettleText(string kind) => kind switch
    {
        GreatArtist => "Settle in a culture building: it makes twice the culture, +3 mood on its world.",
        GreatScientist => "Settle in a culture or faith building: research 4% faster (up to 3 scientists).",
        GreatGeneral => "Settle in a culture or faith building: troops hit 4% harder and are 4% tougher (up to 3).",
        GreatMerchant => "Settle in a culture or faith building: taxes +15% and coin and gold +10% on its world.",
        GreatProphet => "Settle in a faith building: it makes twice the faith, +3 mood on its world.",
        GreatEngineer => "Settle in a culture or faith building: everything on its world comes 5% faster.",
        _ => "",
    };

    public bool GreatUse(int id, out string why)
    {
        var c = Culture;
        why = null;
        var p = c.people.Find(x => x.id == id);
        if (p == null || p.Settled) { why = "They're not free to help."; return false; }
        float s = AgeScale;
        string done;
        switch (p.kind)
        {
            case GreatArtist:
            {
                float lump = Mathf.Max(40f * s, CultureRateTotal * 90f);
                c.culture += lump;
                AddEffect($"{p.name}'s masterpiece", "mood", null, 10f, 300f);
                done = $"made a masterpiece: +{lump:0} culture.";
                break;
            }
            case GreatScientist:
                if (!BoostResearch(0.35f)) { why = "Start some research first."; return false; }
                done = "made a breakthrough: research under way jumped ahead.";
                break;
            case GreatGeneral:
                foreach (var w in State.worlds) CultureOf(w).weariness = 0f;
                c.armsUntil = Mathf.Max(c.armsUntil, State.playTime + 300f);
                done = "rallied your people: war weariness is gone and troops hit harder.";
                break;
            case GreatMerchant:
                done = "came home from a trade mission: " + (IsResourceUnlocked("Coin")
                    ? GiveEv(("Coin", 250 * s), ("Gold", 60 * s))
                    : GiveEv(("Food", 250 * s), ("Wood", 200 * s), ("Stone", 150 * s))) + ".";
                break;
            case GreatProphet:
            {
                float lump = Mathf.Max(40f * s, FaithRateTotal * 90f);
                c.faith += lump;
                AddEffect($"{p.name}'s revelation", "mood", null, 8f, 300f);
                done = $"had a revelation: +{lump:0} faith.";
                break;
            }
            case GreatEngineer:
            {
                int n = 0;
                foreach (var b in State.buildings)
                {
                    if (!b.IsBuilt) { b.buildProgress = Mathf.Max(b.buildProgress, 0.995f); n++; }
                    else if (b.upgrading) { b.upgradeProgress = Mathf.Max(b.upgradeProgress, 0.995f); n++; }
                }
                if (n == 0) { why = "Nothing is being built or upgraded here."; return false; }
                done = $"took charge of the works: {n} job{(n == 1 ? "" : "s")} all but done.";
                break;
            }
            default: done = "helped out."; break;
        }
        c.people.Remove(p);
        CultureDirty();
        Notify($"<color=#fd6>{p.name}</color> {done}");
        GameAudio.UI("great_person_used");
        RaiseChanged();
        return true;
    }

    /// <summary>The building a great person would settle in on this world (best free one), or null.</summary>
    public BuildingState GreatSettleTarget(CultureGreatPerson p)
    {
        BuildingState best = null;
        int bestAge = -1;
        var c = Culture;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def) || !CultureSpecOf(def.id, out var spec)) continue;
            if (p.kind == GreatArtist && spec.faith) continue;
            if (p.kind == GreatProphet && !spec.faith) continue;
            if (c.people.Exists(x => x.building == b.instanceId && x.world == State.currentWorld)) continue;
            int age = AgeOrderOf(def);
            if (age > bestAge) { bestAge = age; best = b; }
        }
        return best;
    }

    public bool GreatSettle(int id, out string why)
    {
        var c = Culture;
        why = null;
        var p = c.people.Find(x => x.id == id);
        if (p == null || p.Settled) { why = "They're already settled."; return false; }
        var b = GreatSettleTarget(p);
        if (b == null)
        {
            why = p.kind == GreatArtist ? "Needs a working culture building here with no great person in it."
                : p.kind == GreatProphet ? "Needs a working faith building here with no great person in it."
                : "Needs a working culture or faith building here with no great person in it.";
            return false;
        }
        p.world = State.currentWorld;
        p.building = b.instanceId;
        CultureDirty();
        var def = Database.GetBuilding(b.definitionId);
        Notify($"<color=#fd6>{p.name}</color> settles in the {Name(def)} on {CurrentWorld.name}.");
        GameAudio.UI("great_person_used");
        RaiseChanged();
        return true;
    }

    /// <summary>Hire a great person of your choice with faith (needs a religion).</summary>
    public bool GreatHire(string kind, out string why)
    {
        var c = Culture;
        why = null;
        if (!HasReligion) { why = "Found a religion first."; return false; }
        if (GreatWaiting >= MaxWaitingGreat) { why = $"At most {MaxWaitingGreat} great people can wait at once: use or settle one."; return false; }
        float cost = GreatHireCost;
        if (c.faith < cost) { why = $"Needs {cost:0} faith (you have {c.faith:0})."; return false; }
        c.faith -= cost;
        c.greatHired++;
        var p = NewGreat(kind);
        Notify($"<color=#fd6>{p.name}</color> answers the call of {c.religion}.");
        GameAudio.UI("great_person");
        return true;
    }

    /// <summary>A settled great person whose building is gone comes back to wait.</summary>
    private void CheckSettledPeople()
    {
        foreach (var p in Culture.people)
        {
            if (!p.Settled || SettledBuilding(p) != null) continue;
            p.world = -1;
            p.building = 0;
            Notify($"{p.name}'s building is gone: they're looking for a new home.");
            CultureDirty();
        }
    }

    private float settledCheckAt;

    /// <summary>Called from the main tick (the world you're on): settled people, every few seconds.</summary>
    private void TickCultureShared()
    {
        if (State.playTime < settledCheckAt) return;
        settledCheckAt = State.playTime + 5f;
        CheckSettledPeople();
    }

    // ---------- Text for the HUD ----------

    /// <summary>One line for the people tooltip.</summary>
    public string MoodTooltipLine()
    {
        if (!IsFounded) return "";
        float m = MoodHere;
        string col = ColorUtility.ToHtmlStringRGB(MoodColour(m));
        float prod = MoodProduction(m) - 1f, growth = MoodGrowth(m) - 1f;
        return $"\nMood: <color=#{col}>{MoodName(m)}</color> ({m:0}): work {(prod >= 0 ? "+" : "")}{prod:P0}, moving in {(growth >= 0 ? "+" : "")}{growth:P0} <size=80%><color=#aaa>(Culture panel)</color></size>";
    }

    /// <summary>A culture building's numbers for the build menu tooltip.</summary>
    public string CultureBuildingText(BuildingDefinition def, int level)
    {
        if (def == null) return "";
        if (def.wonder) return $"<color=#e9c>Culture +{0.3f * CultureBaseRate(AgeOrderOf(def)):0.##}/s, +3 mood on its world</color>\n";
        if (!CultureSpecOf(def.id, out var spec)) return "";
        float rate = CultureBaseRate(AgeOrderOf(def)) * StatsOf(def, level).output;
        int mood = (spec.faith ? 2 : 3) + level;
        return spec.faith
            ? $"<color=#ad9>Faith +{rate:0.##}/s</color>, <color=#e9c>culture +{rate * 0.25f:0.##}/s</color>, +{mood} mood on its world\n"
            : $"<color=#e9c>Culture +{rate:0.##}/s</color>, +{mood} mood on its world\n";
    }

    /// <summary>A selected culture building: its numbers and who's settled there.</summary>
    public string CultureSelectionText(BuildingState b, BuildingDefinition def)
    {
        if (b == null || def == null) return "";
        string text = CultureBuildingText(def, b.upgradeLevel);
        if (text.Length == 0) return "";
        var p = Culture.people.Find(x => x.building == b.instanceId && x.world == State.currentWorld);
        if (p != null) text += $"<color=#fd6>{p.name} lives here</color>\n";
        return text;
    }
}
