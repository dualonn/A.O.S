using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// People: classes, needs, luxuries, comfortable homes and schooling, world by world.
///
/// Everyone lives in a home of some age, and the home's age decides their class (Tribesfolk in
/// huts, Burghers in townhouses, Engineers in skyline towers, the Ascended in world trees). Each
/// class needs a couple of goods (pottery, cloth, ale, books, coffee, electronics, holo-media,
/// or the gold, mana and chips of the existing chains) and likes a couple more. Once a good can be
/// made, people use a little of it every second: needs met cheer them, needs unmet weigh on them,
/// luxuries please them. Well-supplied homes grow more comfortable (plain, comfortable,
/// prosperous, grand), which shows on the house and gives a little more room. Schools teach
/// people to read and more: schooled people speed research and staff advanced buildings better,
/// as do the higher classes.
///
/// Everything eases in: on an old save (or a new world) wants start at nothing and come in over
/// about fifteen minutes, each new good's want over ten more, and missing goods only cost a few
/// points of mood (never production, never people). Bonuses are bonuses only. The census runs
/// once a second for each world, from the mood calculation (so worlds in the background keep
/// theirs too).
/// </summary>
public partial class Settlement
{
    // How much one person uses of a good they need / like, each second (shared goods: a quarter, above a reserve).
    public const float PplNeedRate = 0.0006f, PplLuxRate = 0.0004f, PplSharedShare = 0.25f, PplSharedReserve = 100f;
    private const float PplRampSeconds = 900f, PplGoodRampSeconds = 600f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void PplRegister()
    {
        SettlementHooks.Add(SettlementHooks.Mood, PplMoodHook);
        SettlementHooks.Add(SettlementHooks.Production, PplProductionHook);
        SettlementHooks.Add(SettlementHooks.Research, PplResearchHook);
        SettlementHooks.Add(SettlementHooks.Growth, PplGrowthHook);
        pplWorkshopAge = null;
        pplSchoolSpec = null;
    }

    // ---------- State ----------

    public PeopleWorldState PplState(WorldState w)
    {
        if (w.people == null) w.people = new PeopleWorldState();
        var p = w.people;
        p.houses ??= new List<PeopleHouse>();
        p.goods ??= new List<PeopleGood>();
        if (p.classPop == null || p.classPop.Length != PeopleCatalog.ClassCount)
        {
            int n = PeopleCatalog.ClassCount;
            p.classPop = new float[n]; p.needSat = new float[n]; p.luxSat = new float[n];
            p.houseCount = new int[n]; p.grandCount = new int[n]; p.skilled = new float[n];
        }
        p.reasons ??= new List<(float, string)>();
        if (p.taxFactor <= 0f) p.taxFactor = 1f;
        if (p.growth <= 0f) p.growth = 1f;
        return p;
    }

    public PeopleWorldState PeopleHere => PplState(CurrentWorld);

    private static Dictionary<string, int> pplWorkshopAge;
    private static Dictionary<string, PeopleSchoolSpec> pplSchoolSpec;

    private static bool PplWorkshopAge(string id, out int age)
    {
        if (pplWorkshopAge == null)
        {
            pplWorkshopAge = new Dictionary<string, int>();
            foreach (var w in PeopleCatalog.Workshops) pplWorkshopAge[w.id] = w.ageOrder;
        }
        return pplWorkshopAge.TryGetValue(id, out age);
    }

    private static bool PplSchool(string id, out PeopleSchoolSpec spec)
    {
        if (pplSchoolSpec == null)
        {
            pplSchoolSpec = new Dictionary<string, PeopleSchoolSpec>();
            foreach (var s in PeopleCatalog.Schools) pplSchoolSpec[s.id] = s;
        }
        return pplSchoolSpec.TryGetValue(id, out spec);
    }

    public static bool PeopleIsSchool(string id) => id != null && PplSchool(id, out _);

    // ---------- Which goods can be had ----------

    // Shared by all worlds (research and the age are): worked out again when either changes.
    private bool[] pplAvailable;
    private int pplAvailResearch = -1, pplAvailAge = -1;
    private SettlementState pplAvailFor;

    /// <summary>Can this good be made yet (its resource reached, and something that makes it buildable)?</summary>
    public bool PeopleGoodAvailable(int good)
    {
        if (pplAvailable == null || pplAvailFor != State || pplAvailResearch != State.researched.Count || pplAvailAge != State.ageIndex)
        {
            pplAvailFor = State; pplAvailResearch = State.researched.Count; pplAvailAge = State.ageIndex;
            pplAvailable = new bool[PeopleCatalog.Goods.Length];
            for (int i = 0; i < PeopleCatalog.Goods.Length; i++)
            {
                var r = Database.GetResource(PeopleCatalog.Goods[i].id);
                if (r == null || (r.unlockedIn != null && r.unlockedIn.order > State.ageIndex)) continue;
                foreach (var def in Database.buildings)
                {
                    if (def == null || !IsAgeUnlocked(def) || !def.production.Exists(p => p.resource == r && p.perSecond > 0)) continue;
                    if (def.requirements != null && def.requirements.technologies.Exists(t => t != null && !HasResearched(t))) continue;
                    pplAvailable[i] = true;
                    break;
                }
            }
        }
        return good >= 0 && good < pplAvailable.Length && pplAvailable[good];
    }

    private PeopleGood PplGood(PeopleWorldState p, string id)
    {
        for (int i = 0; i < p.goods.Count; i++) if (p.goods[i].id == id) return p.goods[i];
        return null;
    }

    /// <summary>0 to 1: how far a good's want has eased in on this world (0 if it can't be had).</summary>
    private float PplGoodRamp(PeopleWorldState p, int good)
    {
        if (!PeopleGoodAvailable(good)) return 0f;
        var g = PplGood(p, PeopleCatalog.Goods[good].id);
        return g == null ? 0f : Mathf.Clamp01((State.playTime - g.since) / PplGoodRampSeconds);
    }

    private float PplSat(PeopleWorldState p, string id)
    {
        var g = PplGood(p, id);
        return g != null ? g.sat : 0f;
    }

    // ---------- The census ----------

    private readonly List<(int id, int tier, int housing)> pplHouseScratch = new List<(int, int, int)>();
    private readonly HashSet<int> pplSeen = new HashSet<int>();
    private float[] pplDemand;

    private static void PplMoodHook(Settlement s, Action<float, string> add)
    {
        if (s == null || s.State == null) return;
        var p = s.PplState(s.CurrentWorld);
        float step = s.State.playTime - p.lastAt;
        if (p.lastAt <= 0f || step < 0f || step > 30f) step = 1f;
        if (step >= 0.9f)
        {
            p.lastAt = s.State.playTime;
            try { s.PplCensus(p, Mathf.Min(step, 10f)); }
            catch (Exception e) { Debug.LogException(e); }
        }
        foreach (var (amount, text) in p.reasons) add(amount, text);
    }

    private void PplCensus(PeopleWorldState p, float step)
    {
        bool firstRun = !p.started;
        if (firstRun) { p.started = true; p.ramp = 0f; }
        p.ramp = Mathf.Min(1f, p.ramp + step / PplRampSeconds);
        int n = PeopleCatalog.ClassCount;
        float pop = Mathf.Max(0f, State.population);

        // Homes (by class) and schools.
        var cap = new float[n];
        for (int t = 0; t < n; t++) { p.houseCount[t] = 0; p.grandCount[t] = 0; }
        pplHouseScratch.Clear();
        float totalCap = 0f, seats = 0f, levelSeats = 0f;
        int topLevel = 0;
        string topSchool = null;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            var stats = StatsOf(def, b.upgradeLevel);
            if (stats.housing > 0)
            {
                int t = PeopleCatalog.TierOfAge(def.age != null ? def.age.order : 0);
                cap[t] += stats.housing;
                totalCap += stats.housing;
                p.houseCount[t]++;
                if (!def.isTownCenter) pplHouseScratch.Add((b.instanceId, t, stats.housing));
            }
            if (PplSchool(def.id, out var spec))
            {
                float eff = spec.seats * stats.output * StaffingOf(b) * Mathf.Clamp01(b.health / Mathf.Max(1f, stats.maxHealth));
                seats += eff;
                levelSeats += eff * spec.level;
                if (eff > 0f && spec.level > topLevel) { topLevel = spec.level; topSchool = def.displayName; }
            }
        }
        for (int t = 0; t < n; t++) p.classPop[t] = totalCap > 0f ? pop * cap[t] / totalCap : (t == 0 ? pop : 0f);

        // Goods: people use what's there (shared goods only above a reserve).
        if (pplDemand == null || pplDemand.Length != PeopleCatalog.Goods.Length) pplDemand = new float[PeopleCatalog.Goods.Length];
        for (int i = 0; i < pplDemand.Length; i++)
        {
            pplDemand[i] = 0f;
            if (PeopleGoodAvailable(i) && PplGood(p, PeopleCatalog.Goods[i].id) == null)
                p.goods.Add(new PeopleGood { id = PeopleCatalog.Goods[i].id, since = State.playTime, sat = 0f });
        }
        for (int t = 0; t < n; t++)
        {
            if (p.classPop[t] <= 0f) continue;
            var c = PeopleCatalog.Classes[t];
            foreach (var id in c.needs) PplAddDemand(id, p.classPop[t] * PplNeedRate);
            foreach (var id in c.lux) PplAddDemand(id, p.classPop[t] * PplLuxRate);
        }
        float blend = Mathf.Min(1f, step / 20f);
        for (int i = 0; i < pplDemand.Length; i++)
        {
            if (pplDemand[i] <= 0f) continue;
            var spec = PeopleCatalog.Goods[i];
            var r = Database.GetResource(spec.id);
            var g = PplGood(p, spec.id);
            if (r == null || g == null) continue;
            float want = pplDemand[i] * (spec.shared ? PplSharedShare : 1f) * step * Mathf.Max(0.1f, p.ramp);
            float spare = Mathf.Max(0f, GetAmount(r) - (spec.shared ? PplSharedReserve : 0f));
            float take = Mathf.Min(want, spare);
            if (take > 0f) AddResource(r, -take);
            float sat = want > 0f ? take / want : 1f;
            g.sat = Mathf.Clamp01(g.sat + (sat - g.sat) * blend);
        }

        // How well each class is supplied.
        for (int t = 0; t < n; t++)
        {
            var c = PeopleCatalog.Classes[t];
            p.needSat[t] = PplAverage(p, c.needs, 1f);
            p.luxSat[t] = PplAverage(p, c.lux, 0f);
        }

        // Schooling: literacy creeps towards what the schools can teach.
        float targetLit = pop >= 1f ? Mathf.Min(1f, seats / pop) : 0f;
        p.literacy = Mathf.Clamp01(p.literacy + (targetLit - p.literacy) * Mathf.Min(1f, step / 180f));
        p.seats = seats;
        p.avgLevel = seats > 0f ? levelSeats / seats : 0f;
        p.topLevel = topLevel;
        p.topSchool = topSchool;
        int expected = Mathf.Max(1, PeopleCatalog.ExpectedLevel(State.ageIndex));
        p.quality = p.literacy * Mathf.Min(1f, Mathf.Max(1f, p.avgLevel) / expected);

        // Homes grow more comfortable when their class is well supplied (and plainer when not).
        int bonus = 0;
        float comfortSum = 0f, comfortCap = 0f;
        if (p.houseIndex == null || p.houseIndex.Count != p.houses.Count)
        {
            p.houseIndex = new Dictionary<int, PeopleHouse>();
            foreach (var h in p.houses) p.houseIndex[h.id] = h;
        }
        pplSeen.Clear();
        foreach (var (id, tier, housing) in pplHouseScratch)
        {
            pplSeen.Add(id);
            if (!p.houseIndex.TryGetValue(id, out var h))
            {
                h = new PeopleHouse { id = id };
                p.houses.Add(h);
                p.houseIndex[id] = h;
            }
            int target = PplComfortTarget(p, tier);
            if (h.level < target && UnityEngine.Random.value < step / 40f * p.ramp) h.level++;
            else if (h.level > target && UnityEngine.Random.value < step / 80f) h.level--;
            h.level = Mathf.Clamp(h.level, 0, 3);
            bonus += Mathf.RoundToInt(housing * 0.1f * h.level);
            comfortSum += h.level * housing;
            comfortCap += housing;
            if (h.level >= 3) p.grandCount[tier]++;
        }
        if (p.houses.Count > pplSeen.Count + 8)
        {
            p.houses.RemoveAll(h => !pplSeen.Contains(h.id));   // homes that are gone
            p.houseIndex = null;
        }
        p.bonusHousing = bonus;
        p.comfortAvg = comfortCap > 0f ? comfortSum / comfortCap : 0f;

        // Skill: the share of people of at least each class whose needs are met.
        float above = 0f;
        for (int t = n - 1; t >= 0; t--)
        {
            above += p.classPop[t] * p.needSat[t];
            p.skilled[t] = pop > 0f ? above / pop : 0f;
        }

        // Taxes (for whoever collects them) and growth.
        float taxRaw = 0f, luxAvg = 0f;
        for (int t = 0; t < n; t++)
        {
            if (p.classPop[t] <= 0f || pop <= 0f) continue;
            float share = p.classPop[t] / pop;
            taxRaw += share * (1f + 0.05f * t) * (0.85f + 0.15f * p.needSat[t]);
            if (PplHasAvailable(PeopleCatalog.Classes[t].lux)) luxAvg += share * p.luxSat[t];
        }
        if (pop <= 0f) taxRaw = 1f;
        taxRaw *= 1f + 0.03f * p.comfortAvg;
        p.taxFactor = 1f + (taxRaw - 1f) * p.ramp;
        p.growth = 1f + p.ramp * (0.1f * luxAvg + 0.05f * p.comfortAvg / 3f);

        PplMoodReasons(p, pop);
        PplAnnounce(p, firstRun);
    }

    private void PplAddDemand(string id, float amount)
    {
        int i = PeopleCatalog.GoodIndex(id);
        if (i >= 0 && PeopleGoodAvailable(i)) pplDemand[i] += amount;
    }

    private float PplAverage(PeopleWorldState p, string[] ids, float none)
    {
        float sum = 0f;
        int count = 0;
        foreach (var id in ids)
        {
            int i = PeopleCatalog.GoodIndex(id);
            if (i < 0 || !PeopleGoodAvailable(i)) continue;
            sum += PplSat(p, id);
            count++;
        }
        return count > 0 ? sum / count : none;
    }

    private bool PplHasAvailable(string[] ids)
    {
        foreach (var id in ids) if (PeopleGoodAvailable(PeopleCatalog.GoodIndex(id))) return true;
        return false;
    }

    /// <summary>How comfortable a class's homes can get with what it's had lately (0..3).</summary>
    private int PplComfortTarget(PeopleWorldState p, int tier)
    {
        var c = PeopleCatalog.Classes[tier];
        bool needs = PplHasAvailable(c.needs), lux = PplHasAvailable(c.lux);
        if (!needs && !lux) return 0;
        float ns = p.needSat[tier], ls = p.luxSat[tier];
        int level = 0;
        if (ns >= 0.7f) level = 1;
        if (ns >= 0.9f && (!lux || ls >= 0.5f)) level = 2;
        if (ns >= 0.97f && lux && ls >= 0.85f && p.literacy >= 0.25f) level = 3;
        return level;
    }

    private readonly StringBuilder pplText = new StringBuilder();

    private void PplMoodReasons(PeopleWorldState p, float pop)
    {
        p.reasons.Clear();
        if (pop < 1f) return;
        int goods = PeopleCatalog.Goods.Length;
        var penalty = new float[goods];
        var bonus = new float[goods];
        float totalPenalty = 0f, totalBonus = 0f;
        for (int t = 0; t < PeopleCatalog.ClassCount; t++)
        {
            if (p.classPop[t] <= 0f) continue;
            float share = p.classPop[t] / pop;
            var c = PeopleCatalog.Classes[t];
            foreach (var id in c.needs)
            {
                int i = PeopleCatalog.GoodIndex(id);
                if (i < 0 || !PeopleGoodAvailable(i)) continue;
                float v = -5f * share * (1f - PplSat(p, id)) * PplGoodRamp(p, i) * p.ramp;
                penalty[i] += v; totalPenalty += v;
            }
            foreach (var id in c.lux)
            {
                int i = PeopleCatalog.GoodIndex(id);
                if (i < 0 || !PeopleGoodAvailable(i)) continue;
                float v = 3f * share * PplSat(p, id) * PplGoodRamp(p, i) * p.ramp;
                bonus[i] += v; totalBonus += v;
            }
        }
        float penaltyScale = totalPenalty < -10f ? -10f / totalPenalty : 1f;
        float bonusScale = totalBonus > 8f ? 8f / totalBonus : 1f;
        for (int i = 0; i < goods; i++)
        {
            float v = penalty[i] * penaltyScale;
            if (v > -0.5f) continue;
            p.reasons.Add((v, $"{PplLackers(p, PeopleCatalog.Goods[i].id, pop)} lack {PeopleCatalog.Goods[i].name.ToLowerInvariant()}"));
        }
        float lux = 0f;
        pplText.Clear();
        for (int i = 0; i < goods; i++)
        {
            if (bonus[i] * bonusScale < 0.3f) continue;
            lux += bonus[i] * bonusScale;
            if (pplText.Length > 0) pplText.Append(", ");
            pplText.Append(PeopleCatalog.Goods[i].name.ToLowerInvariant());
        }
        if (lux >= 0.5f) p.reasons.Add((lux, $"luxuries enjoyed ({pplText})"));
        if (p.comfortAvg * p.ramp >= 0.5f) p.reasons.Add((p.comfortAvg * p.ramp, "comfortable homes"));
        if (p.literacy * p.ramp * 2f >= 0.5f) p.reasons.Add((2f * p.literacy * p.ramp, $"schooling ({p.literacy:P0} schooled)"));
    }

    /// <summary>"Burghers and Gentry": the (up to two) biggest classes needing a good.</summary>
    private string PplLackers(PeopleWorldState p, string good, float pop)
    {
        int a = -1, b = -1;
        for (int t = 0; t < PeopleCatalog.ClassCount; t++)
        {
            if (p.classPop[t] <= 0f || Array.IndexOf(PeopleCatalog.Classes[t].needs, good) < 0) continue;
            if (a < 0 || p.classPop[t] > p.classPop[a]) { b = a; a = t; }
            else if (b < 0 || p.classPop[t] > p.classPop[b]) b = t;
        }
        if (a < 0) return "People";
        return b < 0 ? PeopleCatalog.Classes[a].name : $"{PeopleCatalog.Classes[a].name} and {PeopleCatalog.Classes[b].name}";
    }

    private void PplAnnounce(PeopleWorldState p, bool firstRun)
    {
        for (int t = 0; t < PeopleCatalog.ClassCount; t++)
        {
            int bit = 1 << t;
            if ((p.seenMask & bit) != 0 || p.classPop[t] < 0.5f) continue;
            p.seenMask |= bit;
            if (firstRun || t == 0) continue;   // (an old save's classes are already here: no fanfare)
            var c = PeopleCatalog.Classes[t];
            Notify($"<color=#fd8>The first {c.name} move in.</color> They need {PplList(c.needs)}{(c.lux.Length > 0 ? $", and like {PplList(c.lux)}" : "")}. (Census: research panel, People.)");
            if (!offWorld) GameAudio.UI("culture_tradition", 0.6f);
        }
        if (!p.introduced && p.ramp > 0.02f && State.population >= 15f)
        {
            bool any = false;
            for (int i = 0; i < PeopleCatalog.Goods.Length && !any; i++) any = PeopleGoodAvailable(i);
            if (any)
            {
                p.introduced = true;
                Notify("<color=#fd8>Your people want more than food now:</color> pottery, cloth, ale, books and more, by class. Wants come in gently. See the Census (research panel, People).");
            }
        }
    }

    private static string PplList(string[] ids)
    {
        if (ids.Length == 0) return "nothing more";
        var names = new List<string>();
        foreach (var id in ids) { int i = PeopleCatalog.GoodIndex(id); names.Add(i >= 0 ? PeopleCatalog.Goods[i].name.ToLowerInvariant() : id); }
        return names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
    }

    // ---------- Effects ----------

    /// <summary>Extra room from comfortable homes on the world being worked out (added to Housing).</summary>
    public int PeopleBonusHousing => State != null && State.worlds.Count > 0 ? PplState(CurrentWorld).bonusHousing : 0;

    /// <summary>
    /// How much more tax people here would pay by class (higher classes and comfortable homes pay
    /// more; 1 = as before). Not applied by itself: for a tax system to multiply in.
    /// </summary>
    public float ClassTaxFactor => State == null || State.worlds.Count == 0 ? 1f : PplState(CurrentWorld).taxFactor;

    private static float PplProductionHook(Settlement s, BuildingState b, BuildingDefinition def, ResourceType r)
    {
        if (def == null || s.State == null) return 1f;
        float f = 1f;
        int ageIndex = s.State.ageIndex;
        // Later ages make the old goods in bulk.
        if (PplWorkshopAge(def.id, out int wAge)) f *= 1f + 0.5f * Mathf.Max(0, ageIndex - wAge);
        if (def.workers <= 0 || def.housing > 0) return f;
        var p = s.CurrentWorld.people;
        if (p == null || p.skilled == null || p.ramp <= 0f) return f;
        int age = def.age != null ? def.age.order : 0;
        bool advanced = age >= 2 && age >= ageIndex - 2;
        f *= 1f + p.ramp * (0.08f * p.skilled[PeopleCatalog.TierOfAge(age)] + (advanced ? 0.1f : 0.04f) * p.quality);
        return f;
    }

    private static float PplResearchHook(Settlement s)
    {
        if (s.State == null) return 1f;
        float sum = 0f, pop = 0f;
        foreach (var w in s.State.worlds)
        {
            if (w.people == null || w.population <= 0f) continue;
            sum += w.population * w.people.quality * w.people.ramp;
            pop += w.population;
        }
        return pop > 0f ? 1f + 0.3f * sum / pop : 1f;
    }

    private static float PplGrowthHook(Settlement s)
    {
        if (s.State == null || s.State.worlds.Count == 0) return 1f;
        var p = s.CurrentWorld.people;
        return p != null && p.growth > 0f ? p.growth : 1f;
    }

    /// <summary>Production bonus from classes and schooling for a building here (for the UI).</summary>
    public float PeopleProductionFactor(BuildingDefinition def) => PplProductionHook(this, null, def, null);

    public float PeopleResearchFactor => PplResearchHook(this);

    // ---------- What the HUD shows ----------

    /// <summary>A short "120 Burghers, 40 Merchants" for the biggest classes here.</summary>
    public string PeopleClassSummary(int max = 2)
    {
        var p = PeopleHere;
        var order = new List<int>();
        for (int t = 0; t < PeopleCatalog.ClassCount; t++) if (p.classPop[t] >= 0.5f) order.Add(t);
        order.Sort((a, b) => p.classPop[b].CompareTo(p.classPop[a]));
        var parts = new List<string>();
        for (int i = 0; i < order.Count && i < max; i++) parts.Add($"{Mathf.RoundToInt(p.classPop[order[i]])} {PeopleCatalog.Classes[order[i]].name}");
        if (order.Count > max) parts.Add($"{order.Count - max} more");
        return string.Join(", ", parts);
    }

    /// <summary>0 to 1: needs met, over everyone here (1 if nobody needs anything yet).</summary>
    public float PeopleNeedsMet
    {
        get
        {
            var p = PeopleHere;
            float pop = 0f, sum = 0f;
            for (int t = 0; t < PeopleCatalog.ClassCount; t++)
            {
                if (p.classPop[t] <= 0f || !PplHasAvailable(PeopleCatalog.Classes[t].needs)) continue;
                pop += p.classPop[t];
                sum += p.classPop[t] * p.needSat[t];
            }
            return pop > 0f ? sum / pop : 1f;
        }
    }

    /// <summary>One line for the people tooltip.</summary>
    public string PeopleTooltipLine()
    {
        if (!IsFounded || State.population < 1f) return "";
        var p = PeopleHere;
        string classes = PeopleClassSummary();
        string needs = PeopleNeedsMet < 0.999f ? $" · needs {PeopleNeedsMet:P0} met" : "";
        string lit = p.literacy >= 0.005f ? $" · {p.literacy:P0} schooled" : "";
        string room = p.bonusHousing > 0 ? $" · +{p.bonusHousing} room from comfort" : "";
        return $"\nClasses: {classes}{needs}{lit}{room} <size=80%><color=#aaa>(Census: research panel)</color></size>";
    }

    /// <summary>A house, school or workshop's lines in the build menu tooltip.</summary>
    public string PeopleBuildingText(BuildingDefinition def)
    {
        if (def == null) return "";
        if (def.housing > 0 && !def.isTownCenter)
        {
            var c = PeopleCatalog.Classes[PeopleCatalog.TierOfAge(def.age != null ? def.age.order : 0)];
            return $"<color=#fd8>Home of {c.name}</color>: need {PplList(c.needs)}; like {PplList(c.lux)}\n";
        }
        if (PplSchool(def.id, out var s))
            return $"<color=#9cf>Schools {s.seats} people</color> (level {s.level}: {s.specialists}). Schooled people speed research and work better.\n";
        if (PeopleCatalog.IsWorkshop(def.id, out var w))
        {
            var users = new List<string>();
            foreach (var c in PeopleCatalog.Classes)
                if (Array.IndexOf(c.needs, w.good) >= 0 || Array.IndexOf(c.lux, w.good) >= 0) users.Add(c.name);
            string few = users.Count > 4 ? string.Join(", ", users.GetRange(0, 4)) + "..." : string.Join(", ", users);
            return $"<color=#fd8>Wanted by</color> {few}\n";
        }
        return "";
    }

    /// <summary>A selected house, school or workshop: who lives there, how many it teaches, and so on.</summary>
    public string PeopleSelectionText(BuildingState b, BuildingDefinition def)
    {
        if (b == null || def == null || !b.IsBuilt) return "";
        var p = PeopleHere;
        var sb = new StringBuilder();
        if (def.housing > 0 && !def.isTownCenter)
        {
            int t = PeopleCatalog.TierOfAge(def.age != null ? def.age.order : 0);
            var c = PeopleCatalog.Classes[t];
            int level = 0;
            foreach (var h in p.houses) if (h.id == b.instanceId) { level = h.level; break; }
            int extra = Mathf.RoundToInt(StatsOf(def, b.upgradeLevel).housing * 0.1f * level);
            sb.Append($"<color=#fd8>{c.name}</color> · {PeopleCatalog.ComfortNames[level]} home{(extra > 0 ? $" (+{extra} room)" : "")}\n");
            sb.Append(PplGoodsLine(p, c));
        }
        else if (PplSchool(def.id, out var s))
        {
            var stats = StatsOf(def, b.upgradeLevel);
            float eff = s.seats * stats.output * StaffingOf(b);
            sb.Append($"<color=#9cf>Schooling {eff:0} of {s.seats * stats.output:0} pupils</color> · level {s.level} ({s.specialists})\n");
            sb.Append($"<size=85%>{p.literacy:P0} of people here are schooled</size>\n");
        }
        else if (PeopleCatalog.IsWorkshop(def.id, out var w))
        {
            float mass = 1f + 0.5f * Mathf.Max(0, State.ageIndex - w.ageOrder);
            int gi = PeopleCatalog.GoodIndex(w.good);
            if (mass > 1.01f) sb.Append($"<color=#bdb>Mass production x{mass:0.#}</color> (later ages make it in bulk)\n");
            if (gi >= 0) sb.Append($"<size=85%>{PeopleCatalog.Goods[gi].name} supplied: {PplSat(p, w.good):P0}</size>\n");
        }
        return sb.ToString();
    }

    /// <summary>"Needs: cloth 100%, ale 40% · Likes: books -".</summary>
    private string PplGoodsLine(PeopleWorldState p, PeopleClass c)
    {
        string Part(string[] ids)
        {
            var parts = new List<string>();
            foreach (var id in ids)
            {
                int i = PeopleCatalog.GoodIndex(id);
                if (i < 0) continue;
                string name = PeopleCatalog.Goods[i].name;
                if (!PeopleGoodAvailable(i)) { parts.Add($"<color=#888>{name} (not yet)</color>"); continue; }
                float sat = PplSat(p, id);
                string col = sat >= 0.9f ? "#9e9" : sat >= 0.5f ? "#fd6" : "#f99";
                parts.Add($"<color={col}>{name} {sat:P0}</color>");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "<color=#888>nothing</color>";
        }
        return $"<size=85%>Needs: {Part(c.needs)} · Likes: {Part(c.lux)}</size>\n";
    }

    /// <summary>One class's line for the census.</summary>
    public string PeopleClassRow(int tier)
    {
        var p = PeopleHere;
        var c = PeopleCatalog.Classes[tier];
        float pop = Mathf.Max(1f, State.population);
        int grand = p.grandCount[tier], homes = p.houseCount[tier];
        string needs = PplHasAvailable(c.needs) ? $"needs <color={(p.needSat[tier] >= 0.9f ? "#9e9" : p.needSat[tier] >= 0.5f ? "#fd6" : "#f99")}>{p.needSat[tier]:P0}</color>" : "<color=#aaa>content</color>";
        string lux = PplHasAvailable(c.lux) ? $" · luxuries {p.luxSat[tier]:P0}" : "";
        return $"<b>{c.name}</b> {Mathf.RoundToInt(p.classPop[tier])} <color=#aaa>({p.classPop[tier] / pop:P0})</color> · {homes} home{(homes == 1 ? "" : "s")}{(grand > 0 ? $", {grand} grand" : "")} · {needs}{lux}\n{PplGoodsLine(p, c)}";
    }

    public string PeopleClassTooltip(int tier)
    {
        var p = PeopleHere;
        var c = PeopleCatalog.Classes[tier];
        var sb = new StringBuilder($"<b>{c.name}</b>\n<color=#ccc>{c.desc}</color>\n");
        sb.Append(PplGoodsLine(p, c));
        var house = Database.buildings.Find(d => d != null && d.housing > 0 && !d.isTownCenter && d.age != null && d.age.order == tier);
        if (house != null) sb.Append($"They live in: {house.displayName}\n");
        int target = PplComfortTarget(p, tier);
        sb.Append($"Homes can reach: {PeopleCatalog.ComfortNames[target]}\n");
        sb.Append($"<size=85%><color=#aaa>Comfortable: needs 70%+. Prosperous: needs 90%+ and luxuries 50%+. Grand: needs 97%+, luxuries 85%+ and a quarter schooled. Each step gives the home 10% more room.\n" +
                  $"Higher classes staff buildings of their age and later better, and pay more tax.</color></size>");
        return sb.ToString();
    }

    public string PeopleEducationText()
    {
        var p = PeopleHere;
        int expected = PeopleCatalog.ExpectedLevel(State.ageIndex);
        var sb = new StringBuilder();
        sb.Append($"<b>Schooling</b> {p.literacy:P0} of people · seats {p.seats:0} for {State.population:0} people\n");
        if (p.topLevel > 0)
            sb.Append($"<size=85%>Best school: {p.topSchool} (level {p.topLevel} of {expected} for this age) · {Mathf.RoundToInt(p.literacy * State.population)} {PeopleCatalog.SpecialistName(p.topLevel)}</size>\n");
        else if (expected > 0) sb.Append("<size=85%><color=#fd6>No working school here.</color></size>\n");
        else sb.Append("<size=85%><color=#aaa>Schools come in the Bronze Age (Writing).</color></size>\n");
        sb.Append($"<size=85%>Research <color=#9cf>+{PeopleResearchFactor - 1f:P0}</color> (all worlds) · advanced work <color=#9e9>+{p.ramp * 0.1f * p.quality:P0}</color></size>\n");
        return sb.ToString();
    }

    public string PeopleEducationTooltip() =>
        "<b>Schooling</b>\nSchools teach as many people as they have seats (more with upgrades and full staff); literacy creeps towards that.\n" +
        "Schooled people speed research on every world (up to +30%) and work advanced buildings better (up to +10%). " +
        "A school of your own age's level teaches best: older schools count for less as the ages pass, so rank them up.\n" +
        "A quarter schooled is needed before homes can be Grand.";

    /// <summary>What to build next, best first (a few lines).</summary>
    public List<string> PeopleSuggestions(int max = 4)
    {
        var list = new List<string>();
        var p = PeopleHere;
        float pop = Mathf.Max(1f, State.population);
        // Goods wanted but short.
        var worst = new List<(float score, int good)>();
        var notYet = new List<int>();
        for (int i = 0; i < PeopleCatalog.Goods.Length; i++)
        {
            var spec = PeopleCatalog.Goods[i];
            float wantShare = 0f;
            bool needed = false;
            for (int t = 0; t < PeopleCatalog.ClassCount; t++)
            {
                if (p.classPop[t] <= 0f) continue;
                bool need = Array.IndexOf(PeopleCatalog.Classes[t].needs, spec.id) >= 0, like = Array.IndexOf(PeopleCatalog.Classes[t].lux, spec.id) >= 0;
                if (need || like) wantShare += p.classPop[t] / pop * (need ? 1f : 0.5f);
                needed |= need;
            }
            if (wantShare <= 0f) continue;
            if (!PeopleGoodAvailable(i)) { if (needed) notYet.Add(i); continue; }
            float sat = PplSat(p, spec.id);
            if (sat < 0.85f) worst.Add((wantShare * (1f - sat), i));
        }
        worst.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var (score, i) in worst)
        {
            if (list.Count >= max) break;
            var spec = PeopleCatalog.Goods[i];
            string maker = spec.shared ? $"Make more {spec.name}" : $"Build a {PplWorkshopName(spec.id)}";
            string reserve = spec.shared ? $" (people only buy what's above {PplSharedReserve:0} in store)" : "";
            list.Add($"{maker}: {spec.name.ToLowerInvariant()} is {PplSat(p, spec.id):P0} supplied{reserve}");
        }
        // Schools.
        int expected = PeopleCatalog.ExpectedLevel(State.ageIndex);
        if (list.Count < max && expected > 0 && (p.seats < pop * 0.3f || p.avgLevel + 0.01f < expected))
        {
            string school = null;
            foreach (var s in PeopleCatalog.Schools) if (s.ageOrder <= State.ageIndex) school = s.name;
            list.Add(p.seats < pop * 0.3f
                ? $"Build a {school}: {p.literacy:P0} schooled (aim for a third or more)"
                : $"Rank your schools up to a {school} (the newest teach best)");
        }
        // Homes of older ages.
        if (list.Count < max)
        {
            int oldest = -1;
            for (int t = 0; t < State.ageIndex && t < PeopleCatalog.ClassCount; t++) if (p.houseCount[t] > 0) { oldest = t; break; }
            if (oldest >= 0 && oldest < State.ageIndex - 1)
            {
                var house = Database.buildings.Find(d => d != null && d.housing > 0 && !d.isTownCenter && d.age != null && d.age.order == oldest);
                if (house != null && house.rankUpTo != null)
                    list.Add($"Rank up {house.displayName}s into {house.rankUpTo.displayName}s: higher classes work better and pay more");
            }
        }
        // Goods still to come.
        foreach (int i in notYet)
        {
            if (list.Count >= max) break;
            list.Add($"<color=#aaa>{PeopleCatalog.Goods[i].name} {PplHowToGet(PeopleCatalog.Goods[i].id)}</color>");
        }
        if (list.Count == 0) list.Add("<color=#9e9>Your people have all they want. Grand homes, schooled people: well done.</color>");
        return list;
    }

    private static string PplWorkshopName(string good)
    {
        foreach (var w in PeopleCatalog.Workshops) if (w.good == good) return w.name;
        return good;
    }

    private string PplHowToGet(string good)
    {
        foreach (var w in PeopleCatalog.Workshops)
        {
            if (w.good != good) continue;
            var age = Database.ages.Find(a => a != null && a.name == w.age);
            if (age != null && age.order > State.ageIndex) return $"comes in the {age.displayName} ({w.name})";
            var tech = string.IsNullOrEmpty(w.tech) ? null : Database.GetTechnology(w.tech);
            if (tech != null && !HasResearched(tech)) return $"comes with {tech.displayName} research ({w.name})";
            if (Database.GetBuilding(w.id) == null) return "needs Tools > Setup People to be run";
            return $"is made at the {w.name}";
        }
        var r = Database.GetResource(good);
        if (r != null && r.unlockedIn != null && r.unlockedIn.order > State.ageIndex) return $"comes in the {r.unlockedIn.displayName}";
        return "comes with research";
    }

    /// <summary>A house's comfort (0..3) on the world you're looking at, for its decorations.</summary>
    public int PeopleComfortOf(int instanceId)
    {
        if (State == null || State.worlds.Count == 0) return 0;
        var p = PplState(CurrentWorld);
        if (p.houseIndex != null) return p.houseIndex.TryGetValue(instanceId, out var h) ? h.level : 0;
        foreach (var h in p.houses) if (h.id == instanceId) return h.level;
        return 0;
    }
}
