using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Civics: each world's government, laws (policies in slots by category), taxes and tithes, and how
/// firmly the people accept it all (legitimacy). Governments follow the ages, from a tribal council
/// to an ascended synod; changing one costs a spell of anarchy and can't be done again for a while.
/// Harsh rule over miserable people breeds revolts (rebels march like raiders, then a strike).
/// Old saves start under customary rule with normal taxes and no laws: exactly as before.
/// Hooks: mood, production, research and growth through SettlementHooks (the mood source also
/// ticks the world once a second); raids, taxes, upkeep, troops, trade, the market, disasters and
/// building costs through small calls in those systems.
/// </summary>
public partial class Settlement
{
    public const float CivAnarchySeconds = 90f, CivSwitchCooldown = 600f, CivPardonCooldown = 300f, CivRevoltCooldown = 1200f;
    public const float CivRevoltWarning = 60f, CivStrikeSeconds = 120f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void CivRegister()
    {
        SettlementHooks.Add(SettlementHooks.Mood, CivMoodHook);
        SettlementHooks.Add(SettlementHooks.Production, CivProductionHook);
        SettlementHooks.Add(SettlementHooks.Research, CivResearchHook);
        SettlementHooks.Add(SettlementHooks.Growth, CivGrowthHook);
    }

    private static void CivMoodHook(Settlement s, Action<float, string> add) => s.CivTick(add);

    private static float CivProductionHook(Settlement s, BuildingState b, BuildingDefinition def, ResourceType r)
    {
        if (s.State == null || r == null) return 1f;
        return s.CivTotalsHere.prod[CivGroupOf(r.id)];
    }

    private static float CivResearchHook(Settlement s)
    {
        // Research is shared: the homeworld's government sets its pace.
        if (s.State == null || s.State.worlds.Count == 0) return 1f;
        return s.CivTotals(s.State.worlds[0], 0).fx.research;
    }

    private static float CivGrowthHook(Settlement s)
    {
        if (s.State == null) return 1f;
        var fx = s.CivTotalsHere.fx;
        return fx.growth * (s.State.currentWorld != 0 ? fx.colonyGrowth : 1f);
    }

    // ---------- State ----------

    public CivicsWorld CivOf(WorldState w)
    {
        if (w.civics == null) w.civics = new CivicsWorld();
        var c = w.civics;
        c.policies ??= new List<string>();
        c.gov ??= "";
        c.previous ??= "";
        return c;
    }

    public CivicsWorld CivHere => CivOf(CurrentWorld);

    public CivGovernment GovOf(WorldState w) => GovernmentById(CivOf(w).gov);

    public CivGovernment GovHere => GovOf(CurrentWorld);

    public bool CivIsCustomary(WorldState w) => GovOf(w).id == CivCustomary;

    public bool CivInAnarchy(WorldState w) => State != null && CivOf(w).anarchyUntil > State.playTime;

    private int CivIndexOf(WorldState w) => State.worlds.IndexOf(w);

    /// <summary>The world leading a Stellar Federation (null if none).</summary>
    public WorldState CivFederationCapital()
    {
        foreach (var w in State.worlds)
            if (GovOf(w).federal && !CivInAnarchy(w)) return w;
        return null;
    }

    /// <summary>The world whose laws are in force here (a federal member uses its federation's).</summary>
    public WorldState CivLawsOf(WorldState w)
    {
        if (GovOf(w).member)
        {
            var cap = CivFederationCapital();
            if (cap != null && cap != w) return cap;
        }
        return w;
    }

    public List<string> CivPoliciesOf(WorldState w) => CivOf(CivLawsOf(w)).policies;

    /// <summary>Policy slots in a category: the government's, +1 Faith with a religion, +1 Expansion with a second world.</summary>
    public int CivSlots(WorldState w, CivCategory c)
    {
        var gov = GovOf(CivLawsOf(w));
        int n = gov.slots != null && (int)c < gov.slots.Length ? gov.slots[(int)c] : 0;
        if (c == CivCategory.Faith && HasReligion) n++;
        if (c == CivCategory.Expansion && State.worlds.Count > 1) n++;
        return n;
    }

    public int CivUsed(WorldState w, CivCategory c)
    {
        int n = 0;
        foreach (var id in CivPoliciesOf(w))
        {
            var p = PolicyById(id);
            if (p != null && p.cat == c) n++;
        }
        return n;
    }

    private float CivAgeScale => Mathf.Pow(1f + Mathf.Max(0, State.ageIndex), 1.25f);

    private bool CivCoinUnlocked
    {
        get
        {
            var coin = Database.coin;
            return coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= State.ageIndex);
        }
    }

    public int CivTaxLevel(WorldState w) => Mathf.Clamp(2 + CivOf(w).taxOffset, 0, CivTaxNames.Length - 1);

    // ---------- Totals (cached a couple of seconds per world) ----------

    public CivicsTotals CivTotalsHere => CivTotals(CurrentWorld, State.currentWorld);

    private void CivDirty(WorldState w)
    {
        if (w?.civics != null) w.civics.totalsAt = -99f;
        foreach (var x in State.worlds) if (x.civics != null && GovOf(x).member) x.civics.totalsAt = -99f;
    }

    public CivicsTotals CivTotals(WorldState w, int index)
    {
        var c = CivOf(w);
        if (c.totals != null && Mathf.Abs(State.playTime - c.totalsAt) < 2f) return c.totals;
        c.totalsAt = State.playTime;
        var t = c.totals ?? new CivicsTotals();
        c.totals = t;
        CivSum(w, index, t.fx, out t.anarchy);
        var fx = t.fx;
        float colony = index != 0 ? fx.colonyAll : 1f;
        float strike = c.strikeUntil > State.playTime ? 0.8f : 1f;
        float lift = fx.all * colony * strike * CivLegitProduction(w);
        t.prod[GFood] = fx.food * lift; t.prod[GWood] = fx.wood * lift; t.prod[GStone] = fx.stone * lift;
        t.prod[GMetal] = fx.metal * lift; t.prod[GWealth] = fx.wealth * lift; t.prod[GArcane] = fx.arcane * lift;
        t.prod[GIndustry] = fx.industry * lift; t.prod[GCosmic] = fx.cosmic * lift; t.prod[GOther] = lift;
        return t;
    }

    /// <summary>Everything in force on a world, added up into 'fx' (anarchy suspends government and laws).</summary>
    private void CivSum(WorldState w, int index, CivEffect fx, out bool anarchy)
    {
        Reset(fx);
        var c = CivOf(w);
        anarchy = c.anarchyUntil > State.playTime;
        if (anarchy)
        {
            fx.all = 0.75f; fx.research = 0.8f; fx.tax = 0.5f; fx.culture = 0.8f; fx.mood = -8f;
        }
        else
        {
            var gov = GovOf(w);
            fx.Add(gov.fx);
            foreach (var id in CivPoliciesOf(w))
            {
                var p = PolicyById(id);
                if (p != null) fx.Add(p.fx);
            }
        }
        // Taxes and tithes (taxes only once there's coin to pay them in).
        int level = CivTaxLevel(w);
        if (CivCoinUnlocked)
        {
            fx.tax *= CivTaxRate[level];
            fx.mood += CivTaxMood[level];
            fx.harsh += CivTaxHarsh[level];
        }
        if (c.tithe > 0)
        {
            fx.faith *= 1f + 0.15f * c.tithe;
            fx.tax *= 1f - 0.08f * c.tithe;
            fx.mood += CivTitheMood(w, c.tithe);
        }
        fx.mood += CivLegitMood(w);
    }

    private static void Reset(CivEffect fx)
    {
        fx.food = fx.wood = fx.stone = fx.metal = fx.wealth = fx.arcane = fx.industry = fx.cosmic = fx.all = 1f;
        fx.research = fx.growth = fx.culture = fx.faith = fx.great = fx.tax = fx.trade = fx.sell = fx.buy = 1f;
        fx.threat = fx.attack = fx.health = fx.upkeep = fx.disaster = fx.weariness = fx.anarchy = fx.policyCost = 1f;
        fx.colonyAll = fx.colonyGrowth = 1f;
        fx.refund = fx.mood = fx.legit = fx.natives = 0f;
        fx.harsh = 0;
        fx.noRevolt = false;
    }

    private float CivTitheMood(WorldState w, int tithe)
    {
        if (tithe <= 0) return 0f;
        var gov = GovOf(w);
        if (gov.id == "Theocracy" || gov.id == "AscendedSynod" || CivPoliciesOf(w).Contains("StateReligion")) return 0f;
        return tithe == 1 ? -1f : -3f;
    }

    // ---------- Legitimacy ----------

    public static string CivLegitName(float l) => l >= 80 ? "Revered" : l >= 65 ? "Respected" : l >= 35 ? "Accepted" : l >= 20 ? "Doubted" : "Despised";

    public static Color CivLegitColour(float l) =>
        l >= 80 ? new Color(1f, 0.85f, 0.4f) : l >= 65 ? new Color(0.6f, 0.95f, 0.55f) : l >= 35 ? new Color(0.9f, 0.9f, 0.8f) : l >= 20 ? new Color(1f, 0.75f, 0.4f) : new Color(1f, 0.45f, 0.4f);

    /// <summary>Legitimacy only counts once you've chosen a government (customary rule is just the old ways).</summary>
    private bool CivLegitCounts(WorldState w) => !CivIsCustomary(w) && !CivInAnarchy(w);

    private float CivLegitProduction(WorldState w)
    {
        if (!CivLegitCounts(w)) return 1f;
        float l = CivOf(w).legitimacy;
        return l >= 80 ? 1.04f : l >= 65 ? 1.02f : l >= 35 ? 1f : l >= 20 ? 0.97f : 0.94f;
    }

    private float CivLegitMood(WorldState w)
    {
        if (!CivLegitCounts(w)) return 0f;
        float l = CivOf(w).legitimacy;
        return l >= 80 ? 2f : l < 20 ? -3f : 0f;
    }

    /// <summary>Legitimacy per minute as things stand (positive: growing).</summary>
    public float CivLegitRate(WorldState w)
    {
        var c = CivOf(w);
        var fx = CivTotals(w, CivIndexOf(w)).fx;
        float mood = MoodOf(w);
        float culture = Mathf.Min(2f, CultureOf(w).cultureRate * 0.5f);
        return fx.legit + culture + (mood - 50f) * 0.08f - fx.harsh * 0.6f - (c.legitimacy - 50f) * 0.06f;
    }

    // ---------- The tick (once a second per world, from the mood hook) ----------

    private void CivTick(Action<float, string> add)
    {
        if (State == null) return;
        var w = CurrentWorld;
        var c = CivOf(w);
        int index = State.currentWorld;
        float now = State.playTime;
        if (!c.set)
        {
            c.set = true;
            // A brand-new colony (no starport yet, hardly anything built) starts under a governor.
            if (index != 0 && !w.starportKitUsed && w.buildings.Count <= 3) c.gov = CivColonial;
            c.legitimacy = 50f;
        }
        float step = c.lastTick < 0f ? 1f : Mathf.Clamp(now - c.lastTick, 0f, 10f);
        c.lastTick = now;

        // Anarchy ends: the new government takes power.
        if (c.anarchyUntil > 0f && now >= c.anarchyUntil)
        {
            c.anarchyUntil = 0f;
            CivDirty(w);
            var g = GovOf(w);
            Notify($"<color=#fd8>The {g.name} takes power.</color> The anarchy is over: its bonuses and your laws are in force.");
            if (!offWorld) GameAudio.UI("civ_government");
        }

        var t = CivTotals(w, index);
        var fx = t.fx;
        var wc = CultureOf(w);
        var culture = Culture;

        // Culture, faith and great people sped up or slowed by the laws.
        if (step > 0f)
        {
            if (fx.culture != 1f) culture.culture = Mathf.Max(0f, culture.culture + wc.cultureRate * (fx.culture - 1f) * step);
            if (fx.faith != 1f) culture.faith = Mathf.Max(0f, culture.faith + wc.faithRate * (fx.faith - 1f) * step);
            if (fx.great != 1f) culture.greatProgress = Mathf.Max(0f, culture.greatProgress + (wc.cultureRate + wc.faithRate) * (fx.great - 1f) * step);
            if (fx.weariness != 1f) wc.weariness = Mathf.Clamp(wc.weariness - step / 15f * (fx.weariness - 1f), 0f, 30f);
            if (fx.natives != 0f && w.diplomacy != null && w.diplomacy.met && NativesHere != null)
                w.diplomacy.standing = Mathf.Clamp(w.diplomacy.standing + fx.natives / 60f * step, -100f, 100f);
            // Legitimacy drifts (customary rule stays at 50: it's simply the old ways).
            if (CivIsCustomary(w)) c.legitimacy = 50f;
            else if (!t.anarchy) c.legitimacy = Mathf.Clamp(c.legitimacy + CivLegitRate(w) / 60f * step, 0f, 100f);
        }

        CivTickRevolt(w, c, fx, now, step);

        // Mood reasons, one line each.
        if (t.anarchy) add(-8f, "Anarchy (a change of government)");
        else
        {
            var gov = GovOf(w);
            if (gov.fx.mood != 0f) add(gov.fx.mood, $"Government: {gov.name}");
            foreach (var id in CivPoliciesOf(w))
            {
                var p = PolicyById(id);
                if (p != null && p.fx.mood != 0f) add(p.fx.mood, $"Policy: {p.name}");
            }
        }
        if (CivCoinUnlocked)
        {
            int level = CivTaxLevel(w);
            if (CivTaxMood[level] != 0f) add(CivTaxMood[level], $"Taxes: {CivTaxNames[level].ToLowerInvariant()}");
        }
        float tm = CivTitheMood(w, c.tithe);
        if (tm != 0f) add(tm, $"Tithes: {CivTitheNames[c.tithe].ToLowerInvariant()}");
        float lm = CivLegitMood(w);
        if (lm != 0f) add(lm, $"Legitimacy: {CivLegitName(c.legitimacy).ToLowerInvariant()}");
        if (c.strikeUntil > now) add(-3f, "Revolt: a general strike");
    }

    // ---------- Revolts ----------

    /// <summary>How likely a revolt is here: 0 none, 1 simmering, 2 rebels could rise.</summary>
    public int CivRevoltRisk(WorldState w)
    {
        var c = CivOf(w);
        var fx = CivTotals(w, CivIndexOf(w)).fx;
        if (fx.noRevolt || fx.harsh <= 0) return 0;
        float mood = MoodOf(w);
        float legit = CivIsCustomary(w) ? 50f : c.legitimacy;
        if (mood < 25f && (legit < 35f || fx.harsh >= 2)) return 2;
        if (mood < 40f && legit < 50f) return 1;
        return 0;
    }

    private void CivTickRevolt(WorldState w, CivicsWorld c, CivEffect fx, float now, float step)
    {
        if (c.revoltAt > 0f)
        {
            if (now < c.revoltAt) return;
            c.revoltAt = 0f;
            if (fx.noRevolt || MoodOf(w) >= 35f)
            {
                Notify("The rebels who were gathering have melted away.");
                return;
            }
            // (wait for the raid in progress to end before rising)
            var live = FreeModeController.Instance;
            if (!offWorld && live != null && live.WaveInProgress) { c.revoltAt = now + 30f; return; }
            CivRevolt(w, c);
            return;
        }
        if (now < c.revoltReadyAt || State.population < 20f || CivRevoltRisk(w) < 2) return;
        if (UnityEngine.Random.value > step / 240f) return;   // about four minutes of misery, on average
        c.revoltAt = now + CivRevoltWarning;
        Notify($"<color=#f97>Rebels are gathering on {w.name}!</color> In a minute they'll rise. Lower taxes, ease harsh laws, hold a festival or grant a pardon (Government panel).");
        if (!offWorld) GameAudio.UI("plague_bell", 0.6f);
    }

    private void CivRevolt(WorldState w, CivicsWorld c)
    {
        float now = State.playTime;
        c.revolts++;
        c.revoltReadyAt = now + CivRevoltCooldown;
        c.strikeUntil = now + CivStrikeSeconds;
        c.legitimacy = Mathf.Max(0f, c.legitimacy - 5f);
        CivDirty(w);
        var gov = GovOf(w);
        XChronicle(XChronicleKind.War, $"The people rose against the {gov.name} on {w.name}.");
        if (!offWorld) GameAudio.UI("civ_revolt");
        // Rebels: a band about half a raid's size (none in a peaceful game: just the strike).
        var wave = Waves != null ? Waves.Generate() : null;
        if (wave != null && wave.units.Count > 0)
        {
            if (wave.boss != null) { wave.units.Remove(wave.boss); wave.boss = null; }
            int keep = Mathf.Max(1, Mathf.CeilToInt(wave.units.Count * 0.55f));
            if (wave.units.Count > keep) wave.units.RemoveRange(keep, wave.units.Count - keep);
            wave.strength = 0f;
            foreach (var u in wave.units) wave.strength += UnitPower(u);
        }
        if (wave == null || wave.units.Count == 0)
        {
            Notify($"<color=#f97>Revolt!</color> The people of {w.name} down tools: a general strike ({CivStrikeSeconds / 60f:0} min, work -20%).");
            return;
        }
        Notify($"<color=#f97>Revolt!</color> {wave.units.Count} rebels rise against the {gov.name}, and the rest strike ({CivStrikeSeconds / 60f:0} min, work -20%).");
        if (offWorld)
        {
            float severity = Waves.AutoResolve(wave);
            Notify(severity <= 0 ? "The rebels were put down." : "The rebels did damage before they were put down.");
        }
        else Waves.Launch(wave);
    }

    // ---------- Choosing a government ----------

    public bool CivGovUnlocked(CivGovernment g, WorldState w, out string why)
    {
        why = null;
        if (g.given) { why = g.colonyOnly ? "Given to new colonies." : "The old ways (you can always go back to them)."; return g.id == CivCustomary; }
        if (g.colonyOnly && CivIndexOf(w) == 0) { why = "Only on a colony, not your homeworld."; return false; }
        int age = State.ageIndex;
        if (age < g.age) { why = $"Needs the {Database.GetAge(g.age)?.displayName ?? "a later age"}."; return false; }
        if (!string.IsNullOrEmpty(g.tech) && age < g.age + 1 && !Researched(g.tech))
        {
            why = $"Research {Database.GetTechnology(g.tech)?.displayName ?? g.tech} (or reach the next age).";
            return false;
        }
        if (g.member)
        {
            var cap = CivFederationCapital();
            if (cap == null || cap == w) { why = "Needs a Stellar Federation on another of your worlds."; return false; }
        }
        if (g.federal)
            foreach (var other in State.worlds)
                if (other != w && GovOf(other).federal) { why = $"{other.name} already leads the Federation: join it as a Federal Member."; return false; }
        return true;
    }

    /// <summary>Free the first time (from customary rule or a colonial administration).</summary>
    public bool CivSwitchIsFree(WorldState w) => GovOf(w).given && CivOf(w).changes == 0;

    public List<ResourceAmount> CivSwitchCost(WorldState w)
    {
        var cost = new List<ResourceAmount>();
        if (CivSwitchIsFree(w)) return cost;
        if (CivCoinUnlocked && Database.coin != null)
            cost.Add(new ResourceAmount { resource = Database.coin, amount = Mathf.RoundToInt(60f * CivAgeScale) });
        return cost;
    }

    public float CivSwitchCulture(WorldState w) => CivSwitchIsFree(w) ? 0f : Mathf.Round(80f * CivAgeScale);

    public float CivAnarchyFor(WorldState w, CivGovernment g)
    {
        if (CivSwitchIsFree(w) || g.id == CivCustomary && CivIsCustomary(w)) return 0f;
        float f = g.fx.anarchy;
        foreach (var id in CivOf(w).policies) { var p = PolicyById(id); if (p != null) f *= p.fx.anarchy; }
        return CivAnarchySeconds * Mathf.Clamp(f, 0.3f, 2f);
    }

    public bool CivSetGovernment(string id, out string why)
    {
        var w = CurrentWorld;
        var c = CivOf(w);
        var g = GovernmentById(id);
        why = null;
        if (g.id == c.gov || (g.id == CivCustomary && CivIsCustomary(w))) { why = "That's your government already."; return false; }
        if (!CivGovUnlocked(g, w, out why)) return false;
        float now = State.playTime;
        if (CivInAnarchy(w)) { why = "Wait for the anarchy to end."; return false; }
        if (now < c.switchReadyAt) { why = $"Too soon after the last change: wait {(c.switchReadyAt - now) / 60f:0.0} min."; return false; }
        float culture = CivSwitchCulture(w);
        var cost = CivSwitchCost(w);
        if (Culture.culture < culture) { why = $"Needs {culture:0} culture (you have {Culture.culture:0})."; return false; }
        if (!CanAfford(cost)) { why = $"Needs {CostText(cost)}."; return false; }
        bool free = CivSwitchIsFree(w);
        float anarchy = CivAnarchyFor(w, g);
        Culture.culture -= culture;
        Spend(cost);
        c.previous = GovOf(w).id;
        c.gov = g.id == CivCustomary ? "" : g.id;
        c.changes++;
        c.switchReadyAt = now + (free ? 120f : CivSwitchCooldown);
        c.anarchyUntil = anarchy > 0f ? now + anarchy : 0f;
        c.legitimacy = free ? 55f : 40f;
        CivTrimPolicies(w);
        CivDirty(w);
        XChronicle(XChronicleKind.Founding, $"{w.name} took up a new government: the {g.name}.");
        if (anarchy > 0f)
        {
            Notify($"<color=#fb7>Anarchy!</color> The old order falls on {w.name}. The {g.name} takes power in {anarchy / 60f:0.0} min; until then work and research slow and taxes dwindle.");
            GameAudio.UI("civ_anarchy");
        }
        else
        {
            Notify($"<color=#fd8>The {g.name} takes power on {w.name}.</color>");
            GameAudio.UI("civ_government");
        }
        return true;
    }

    /// <summary>After a change of government: laws that no longer fit (or have no slot) lapse.</summary>
    private void CivTrimPolicies(WorldState w)
    {
        var c = CivOf(w);
        var gov = GovOf(w);
        if (gov.member) { c.policies.Clear(); return; }   // the Federation's laws apply instead
        var kept = new List<string>();
        var lapsed = new List<string>();
        foreach (var id in c.policies)
        {
            var p = PolicyById(id);
            if (p == null) continue;
            int used = 0;
            foreach (var k in kept) if (PolicyById(k)?.cat == p.cat) used++;
            bool fits = (p.govs == null || Array.IndexOf(p.govs, gov.id) >= 0) && used < CivSlots(w, p.cat);
            if (fits) kept.Add(id); else lapsed.Add(p.name);
        }
        c.policies.Clear();
        c.policies.AddRange(kept);
        if (lapsed.Count > 0) Notify($"Laws lapsed under the new government: {string.Join(", ", lapsed)}.");
    }

    // ---------- Policies ----------

    public float CivPolicyCost(CivPolicy p, WorldState w)
    {
        var fx = CivTotals(w, CivIndexOf(w)).fx;
        float legit = CivLegitCounts(w) && CivOf(w).legitimacy >= 65f ? 0.8f : 1f;
        return Mathf.Round(25f * Mathf.Pow(1f + Mathf.Max(p.age, State.ageIndex), 1.25f) * Mathf.Clamp(fx.policyCost, 0.5f, 2f) * legit);
    }

    public bool CivPolicyUnlocked(CivPolicy p, WorldState w, out string why)
    {
        why = null;
        int age = State.ageIndex;
        if (age < p.age) { why = $"Needs the {Database.GetAge(p.age)?.displayName ?? "a later age"}."; return false; }
        if (!string.IsNullOrEmpty(p.tech) && age < p.age + 1 && !Researched(p.tech))
        {
            why = $"Research {Database.GetTechnology(p.tech)?.displayName ?? p.tech} (or reach the next age).";
            return false;
        }
        var gov = GovOf(CivLawsOf(w));
        if (p.govs != null && Array.IndexOf(p.govs, gov.id) < 0)
        {
            var names = new List<string>();
            foreach (var g in p.govs) names.Add(GovernmentById(g).name);
            why = $"Only under: {string.Join(", ", names)}.";
            return false;
        }
        return true;
    }

    /// <summary>The policy in force here that rules this one out (same group), or null.</summary>
    public CivPolicy CivRival(CivPolicy p, WorldState w)
    {
        if (string.IsNullOrEmpty(p.group)) return null;
        foreach (var id in CivPoliciesOf(w))
        {
            var q = PolicyById(id);
            if (q != null && q != p && q.group == p.group) return q;
        }
        return null;
    }

    public bool CivAdopt(string id, out string why)
    {
        var w = CurrentWorld;
        var p = PolicyById(id);
        why = null;
        if (p == null) { why = "No such policy."; return false; }
        if (GovOf(w).member && CivLawsOf(w) != w) { why = $"{CivLawsOf(w).name} sets the Federation's laws: change them there."; return false; }
        var c = CivOf(w);
        if (c.policies.Contains(id)) { why = "Already in force."; return false; }
        if (CivInAnarchy(w)) { why = "No laws can be passed during anarchy."; return false; }
        if (!CivPolicyUnlocked(p, w, out why)) return false;
        var rival = CivRival(p, w);
        if (rival != null) { why = $"Repeal {rival.name} first (they can't both be law)."; return false; }
        int slots = CivSlots(w, p.cat);
        if (CivUsed(w, p.cat) >= slots)
        {
            why = slots == 0 ? $"The {GovOf(w).name} has no {CivCategoryName(p.cat)} slots." : $"All {slots} {CivCategoryName(p.cat)} slots are full: repeal one first.";
            return false;
        }
        float cost = CivPolicyCost(p, w);
        if (Culture.culture < cost) { why = $"Needs {cost:0} culture (you have {Culture.culture:0})."; return false; }
        Culture.culture -= cost;
        c.policies.Add(id);
        CivDirty(w);
        Notify($"<color=#fd8>New law on {w.name}: {p.name}.</color>");
        GameAudio.UI("civ_policy");
        return true;
    }

    public bool CivRepeal(string id, out string why)
    {
        var w = CurrentWorld;
        why = null;
        if (GovOf(w).member && CivLawsOf(w) != w) { why = $"{CivLawsOf(w).name} sets the Federation's laws."; return false; }
        var c = CivOf(w);
        if (!c.policies.Remove(id)) { why = "Not in force."; return false; }
        if (CivLegitCounts(w)) c.legitimacy = Mathf.Max(0f, c.legitimacy - 2f);
        CivDirty(w);
        Notify($"{PolicyById(id)?.name ?? "A law"} is repealed on {w.name}.");
        GameAudio.UI("civ_policy", 0.8f);
        return true;
    }

    // ---------- Taxes, tithes and pardons ----------

    public void CivSetTax(int level)
    {
        var w = CurrentWorld;
        var c = CivOf(w);
        level = Mathf.Clamp(level, 0, CivTaxNames.Length - 1);
        int before = CivTaxLevel(w);
        if (level == before) return;
        c.taxOffset = level - 2;
        if (level > before && CivLegitCounts(w)) c.legitimacy = Mathf.Max(0f, c.legitimacy - 2f * (level - before));
        CivDirty(w);
        GameAudio.UI("civ_tax");
    }

    public void CivSetTithe(int level)
    {
        var w = CurrentWorld;
        var c = CivOf(w);
        c.tithe = Mathf.Clamp(level, 0, CivTitheNames.Length - 1);
        CivDirty(w);
        GameAudio.UI("faith_blessing");
    }

    public List<ResourceAmount> CivPardonCost()
    {
        var cost = new List<ResourceAmount>();
        if (CivCoinUnlocked && Database.coin != null)
            cost.Add(new ResourceAmount { resource = Database.coin, amount = Mathf.RoundToInt(40f * CivAgeScale) });
        else if (Database.food != null)
            cost.Add(new ResourceAmount { resource = Database.food, amount = Mathf.RoundToInt(80f * CivAgeScale) });
        return cost;
    }

    /// <summary>A pardon (and a handout): legitimacy up, gathering rebels go home.</summary>
    public bool CivPardon(out string why)
    {
        var w = CurrentWorld;
        var c = CivOf(w);
        why = null;
        float now = State.playTime;
        if (now < c.pardonReadyAt) { why = $"Another pardon so soon would look weak: wait {(c.pardonReadyAt - now) / 60f:0.0} min."; return false; }
        var cost = CivPardonCost();
        if (!CanAfford(cost)) { why = $"Needs {CostText(cost)}."; return false; }
        Spend(cost);
        c.pardonReadyAt = now + CivPardonCooldown;
        if (!CivIsCustomary(w)) c.legitimacy = Mathf.Min(100f, c.legitimacy + 12f);
        bool calmed = c.revoltAt > 0f;
        c.revoltAt = 0f;
        c.revoltReadyAt = Mathf.Max(c.revoltReadyAt, now + 240f);
        CivDirty(w);
        Notify(calmed ? "A pardon is proclaimed: the rebels go home." : "A pardon is proclaimed, and bread handed out: the people think better of their rulers.");
        GameAudio.UI("civ_pardon");
        return true;
    }

    // ---------- Hooks called from other systems ----------

    /// <summary>Taxes (government, laws, the tax rate, tithes).</summary>
    public float CivicsTaxFactor => State == null ? 1f : CivTotalsHere.fx.tax;

    /// <summary>Raid strength on the world being ticked.</summary>
    public float CivicsThreatFactor => State == null ? 1f : CivTotalsHere.fx.threat;

    /// <summary>Army upkeep on the world being ticked.</summary>
    public float CivicsUpkeepFactor => State == null ? 1f : CivTotalsHere.fx.upkeep;

    /// <summary>Trade route income on the world being ticked.</summary>
    public float CivicsTradeFactor => State == null ? 1f : CivTotalsHere.fx.trade;

    /// <summary>Market prices (selling and buying) from the world you're on.</summary>
    public float CivicsSellFactor => State == null ? 1f : CivTotalsHere.fx.sell;
    public float CivicsBuyFactor => State == null ? 1f : CivTotalsHere.fx.buy;

    /// <summary>Disaster damage on the world being ticked.</summary>
    public float CivicsDisasterFactor => State == null ? 1f : CivTotalsHere.fx.disaster;

    /// <summary>Troops' attack and health.</summary>
    public void CivicsUnitModifier(UnitStatModifier m)
    {
        if (m == null || State == null) return;
        var fx = CivTotalsHere.fx;
        m.attackMultiplier *= fx.attack;
        m.healthMultiplier *= fx.health;
    }

    /// <summary>A building was finished: laws that make building cheaper pay part of its cost back.</summary>
    public void CivicsBuilt(BuildingDefinition def)
    {
        if (def == null || def.requirements == null || State == null) return;
        float refund = Mathf.Clamp(CivTotalsHere.fx.refund, 0f, 0.3f);
        if (refund <= 0f) return;
        foreach (var item in def.requirements.cost)
            if (item.resource != null && item.amount > 0) AddResource(item.resource, item.amount * refund);
    }

    // ---------- Text for the panel ----------

    public string CivGovTooltip(CivGovernment g)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>{g.name}</b>  <color=#aaa>({Database.GetAge(g.age)?.displayName}{(string.IsNullOrEmpty(g.ruler) ? "" : $" · ruled by the {g.ruler.ToLowerInvariant()}")})</color>\n");
        sb.Append(g.desc).Append('\n');
        sb.Append(g.fx.Text()).Append('\n');
        if (!g.member) sb.Append($"<color=#ccc>Policy slots: {CivSlotsText(g)}</color>");
        return sb.ToString();
    }

    public static string CivSlotsText(CivGovernment g)
    {
        var parts = new List<string>();
        for (int i = 0; i < 6; i++)
            if (g.slots != null && i < g.slots.Length && g.slots[i] > 0) parts.Add($"{CivCategoryName((CivCategory)i)} {g.slots[i]}");
        return parts.Count > 0 ? string.Join(", ", parts) : "none";
    }

    public string CivPolicyTooltip(CivPolicy p)
    {
        var w = CurrentWorld;
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>{p.name}</b>  <color=#aaa>({CivCategoryName(p.cat)} · {Database.GetAge(p.age)?.displayName})</color>\n");
        sb.Append(p.desc).Append('\n').Append(p.fx.Text());
        var rival = CivRival(p, w);
        if (rival != null) sb.Append($"\n<color=#fb7>Can't be law alongside {rival.name}.</color>");
        else if (!string.IsNullOrEmpty(p.group))
        {
            var others = new List<string>();
            foreach (var q in Policies) if (q != p && q.group == p.group) others.Add(q.name);
            if (others.Count > 0) sb.Append($"\n<color=#aaa>Rules out: {string.Join(", ", others)}.</color>");
        }
        if (!CivPolicyUnlocked(p, w, out string why)) sb.Append($"\n<color=#f99>{why}</color>");
        return sb.ToString();
    }
}
