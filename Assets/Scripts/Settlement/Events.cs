using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything the random events keep in the save.</summary>
[Serializable]
public class EventLog
{
    [Tooltip("Events waiting for you to choose (each on its own world).")]
    public List<PendingEvent> pending = new List<PendingEvent>();
    [Tooltip("Temporary effects in force (a festival, a drought...).")]
    public List<EventEffect> effects = new List<EventEffect>();
    [Tooltip("Things to fetch on the map: a fallen meteor, a lost caravan.")]
    public List<EventPickup> pickups = new List<EventPickup>();
    [Tooltip("What happened, newest last.")]
    public List<string> log = new List<string>();
    [Tooltip("Kinds of event seen lately, so they don't repeat close together.")]
    public List<string> recent = new List<string>();
    [Tooltip("For each world: seconds until its next event.")]
    public List<float> timers = new List<float>();
    public int nextId = 1;
}

[Serializable]
public class PendingEvent
{
    public int id;
    public string kind;
    public int world;
    public int seed;
    public float expiresAt;
}

[Serializable]
public class EventEffect
{
    public string label;
    public int world;
    [Tooltip("prod (a resource, or every one if empty), growth or threat.")]
    public string what;
    public string resource;
    public float factor = 1f;
    public float until;
}

[Serializable]
public class EventPickup
{
    public int id;
    public int world;
    public Vector2 position;
    public float until;
    public string label;
    [Tooltip("meteor or cart: which sprite it shows.")]
    public string look;
    public List<ResourceStock> goods = new List<ResourceStock>();
    public bool item;
}

/// <summary>A choice on an event card: what it costs, what it does.</summary>
public class EventChoice
{
    public string label;
    public string result;
    public List<ResourceAmount> cost = new List<ResourceAmount>();
    public string why;   // set when it can't be chosen
    public Func<string> apply;
}

/// <summary>An event as shown on its card.</summary>
public class EventView
{
    public PendingEvent source;
    public string title, text;
    public List<EventChoice> choices = new List<EventChoice>();
}

/// <summary>
/// Random events: every few minutes something happens on each of your worlds: a merchant
/// caravan (or a star trader), a festival, a good harvest or a drought, sickness, a meteor, people
/// fleeing a war, a hero looking for a lord, a stranger with a bargain, gold in the hills, a wildfire,
/// an eclipse, a rival chief's challenge, a lost caravan, scholars, and among the stars solar flares,
/// derelicts and comets. Each is a card with a choice; ignore it and it waits a while, then passes.
/// Worlds you aren't looking at get the simple ones, settled for you, and the news comes to you.
/// </summary>
public partial class Settlement
{
    /// <summary>A new event card has arrived (for the world you're on).</summary>
    public event Action<PendingEvent> EventArrived;

    private const float FirstEventAfter = 300f, EventGapMin = 240f, EventGapMax = 420f, EventCardLife = 240f;

    private EventLog Ev
    {
        get
        {
            if (State.events == null) State.events = new EventLog();
            return State.events;
        }
    }

    /// <summary>Events waiting on the world you're looking at.</summary>
    public List<PendingEvent> PendingHere => Ev.pending.FindAll(e => e.world == State.currentWorld);

    public IReadOnlyList<string> EventHistory => Ev.log;

    public List<EventPickup> PickupsHere => Ev.pickups.FindAll(p => p.world == State.currentWorld);

    // ---------- The director ----------

    private float TimerFor(int world)
    {
        while (Ev.timers.Count <= world) Ev.timers.Add(FirstEventAfter);
        return Ev.timers[world];
    }

    private void SetTimer(int world, float value)
    {
        TimerFor(world);
        Ev.timers[world] = value;
    }

    private void TickEvents(float dt)
    {
        float now = State.playTime;
        // Cards that waited too long pass; effects and pickups wear off.
        for (int i = Ev.pending.Count - 1; i >= 0; i--)
        {
            var e = Ev.pending[i];
            if (now < e.expiresAt) continue;
            Ev.pending.RemoveAt(i);
            var view = Describe(e);
            if (view != null) LogEvent(WorldName(e.world), $"{view.title}: the moment passed.");
        }
        int before = Ev.effects.Count;
        Ev.effects.RemoveAll(x => now >= x.until);
        if (Ev.effects.Count != before) RaiseChanged();
        for (int i = Ev.pickups.Count - 1; i >= 0; i--)
            if (now >= Ev.pickups[i].until)
            {
                var p = Ev.pickups[i];
                Ev.pickups.RemoveAt(i);
                if (p.world == State.currentWorld) Notify($"Nobody reached the {p.label} in time. It's gone.");
            }

        int w = State.currentWorld;
        float t = TimerFor(w) - dt;
        if (t > 0) { SetTimer(w, t); return; }
        // Never during a raid, or just before one, and never more than one card waiting here.
        bool raid = FreeModeController.Instance != null && FreeModeController.Instance.WaveInProgress;
        if (raid || State.secondsUntilWave < 25f || PendingHere.Count > 0)
        {
            SetTimer(w, 30f);
            return;
        }
        SetTimer(w, UnityEngine.Random.Range(EventGapMin, EventGapMax));
        StartEvent(PickEvent(false), false);
    }

    /// <summary>A world in the background: the simple events, settled for you.</summary>
    private void TickBackgroundEvents(float dt)
    {
        int w = State.currentWorld;
        float t = TimerFor(w) - dt;
        if (t > 0) { SetTimer(w, t); return; }
        SetTimer(w, UnityEngine.Random.Range(EventGapMin, EventGapMax) * 1.5f);
        StartEvent(PickEvent(true), true);
    }

    private static readonly string[] SimpleKinds = { "Harvest", "Drought", "GoldRush", "Wildfire", "Plague", "Refugees", "Scholars" };

    /// <summary>Choose what happens next, weighted by age, land, world, how well you're doing and what happened lately.</summary>
    private string PickEvent(bool simpleOnly)
    {
        var kinds = new List<string>();
        var weights = new List<float>();
        float total = 0f;
        foreach (var kind in AllEventKinds)
        {
            if (simpleOnly && Array.IndexOf(SimpleKinds, kind) < 0) continue;
            if (Ev.recent.Contains(kind)) continue;
            float wgt = EventWeight(kind);
            if (wgt <= 0) continue;
            kinds.Add(kind); weights.Add(wgt); total += wgt;
        }
        if (kinds.Count == 0) return null;
        float pick = UnityEngine.Random.value * total;
        for (int i = 0; i < kinds.Count; i++)
        {
            pick -= weights[i];
            if (pick <= 0) return kinds[i];
        }
        return kinds[kinds.Count - 1];
    }

    private static readonly string[] AllEventKinds =
    {
        "Merchant", "Festival", "Harvest", "Drought", "Plague", "Meteor", "Refugees", "WanderingHero", "Stranger",
        "GoldRush", "Wildfire", "Eclipse", "Rival", "LostCaravan", "Scholars", "SolarFlare", "Derelict", "Comet",
    };

    private bool SpaceAge => State.ageIndex >= 12 || HasStarport;

    private Biome HomeBiome
    {
        get
        {
            var tc = State.buildings.Find(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);
            return tc != null && Map != null ? Map.BiomeAt(tc.cell.x, tc.cell.y) : Biome.Temperate;
        }
    }

    private float EventWeight(string kind)
    {
        float pop = State.population;
        var biome = HomeBiome;
        bool forest = biome == Biome.Temperate || biome == Biome.Jungle || biome == Biome.Swamp || biome == Biome.Fungal;
        bool alien = CurrentWorld.profile != WorldProfile.HomeId;
        switch (kind)
        {
            case "Merchant": return 1.4f;
            case "Festival": return pop >= 20 ? (GetAmountId("Coin") > 200 * AgeScale ? 1.4f : 0.9f) : 0f;
            case "Harvest": return biome == Biome.Barren ? 0.2f : 1f;
            case "Drought": return biome == Biome.Desert ? 1.6f : biome == Biome.Tundra ? 0.4f : 0.7f;
            case "Plague": return pop < 25 ? 0f : Mathf.Min(1.2f, 0.3f + pop / 250f);
            case "Meteor": return alien ? 0.9f : 0.6f;
            case "Refugees": return Housing - pop >= 5 ? 0.8f : 0f;
            case "WanderingHero": return State.heroes.Count < HeroSlots && TrainerHere() != null ? 0.7f : 0f;
            case "Stranger": return alien ? 0.9f : 0.6f;
            case "GoldRush": return HasTrait(RichVeins) ? 1.2f : 0.6f;
            case "Wildfire": return !forest ? 0f : HasTrait(Scorching) ? 1.2f : 0.6f;
            case "Eclipse": return State.ageIndex >= 1 ? 0.5f : 0f;
            case "Rival": return HeroHere() != null ? 0.6f : 0f;
            case "LostCaravan": return 0.6f;
            case "Scholars": return State.ageIndex >= 1 && State.researchJobs.Count > 0 ? 0.8f : 0f;
            case "SolarFlare": return SpaceAge ? 0.6f : 0f;
            case "Derelict": return HasStarport ? 0.6f : 0f;
            case "Comet": return SpaceAge ? 0.6f : 0f;
        }
        return 0f;
    }

    private void StartEvent(string kind, bool background)
    {
        if (kind == null) return;
        Ev.recent.Add(kind);
        while (Ev.recent.Count > 5) Ev.recent.RemoveAt(0);
        var e = new PendingEvent
        {
            id = Ev.nextId++, kind = kind, world = State.currentWorld,
            seed = UnityEngine.Random.Range(1, int.MaxValue), expiresAt = State.playTime + EventCardLife,
        };
        // Things that land on the map arrive with the news.
        if (kind == "Meteor") SpawnPickup(e, "meteor", 6f, 14f);
        if (kind == "LostCaravan") SpawnPickup(e, "cart", 10f, 20f);
        if (background)
        {
            // Settled for you: the first choice you can afford, otherwise the last.
            var view = Describe(e);
            if (view == null || view.choices.Count == 0) return;
            var choice = view.choices.Find(c => c.why == null && CanAfford(c.cost)) ?? view.choices[view.choices.Count - 1];
            ApplyChoice(view, choice);
            return;
        }
        Ev.pending.Add(e);
        var v = Describe(e);
        if (v != null) Notify($"<color=#fd6>{v.title}</color>! (A choice is waiting under the top bar.)");
        EventArrived?.Invoke(e);
        RaiseChanged();
    }

    /// <summary>Choose on an event card. Returns what happened (or why not).</summary>
    public string ResolveEvent(int id, int choiceIndex)
    {
        var e = Ev.pending.Find(x => x.id == id);
        if (e == null) return null;
        var view = Describe(e);
        if (view == null || choiceIndex < 0 || choiceIndex >= view.choices.Count) return null;
        var choice = view.choices[choiceIndex];
        if (choice.why != null) return choice.why;
        if (!CanAfford(choice.cost)) return "Not enough resources";
        Ev.pending.Remove(e);
        string outcome = ApplyChoice(view, choice);
        RaiseChanged();
        return outcome;
    }

    /// <summary>Put an event card away for good (the moment passes).</summary>
    public void DismissEvent(int id)
    {
        var e = Ev.pending.Find(x => x.id == id);
        if (e == null) return;
        Ev.pending.Remove(e);
        var view = Describe(e);
        if (view != null) LogEvent(WorldName(e.world), $"{view.title}: you let it pass.");
        RaiseChanged();
    }

    private string ApplyChoice(EventView view, EventChoice choice)
    {
        Spend(choice.cost);
        string outcome = choice.apply != null ? choice.apply() : "";
        LogEvent(CurrentWorld.name, $"{view.title}: {outcome}");
        Notify($"{view.title}: {outcome}");
        if (!offWorld && view.source != null) GameAudio.EventResolved(view.source.kind);
        return outcome;
    }

    private void LogEvent(string world, string line)
    {
        Ev.log.Add(State.worlds.Count > 1 ? $"<color=#9cf>[{world}]</color> {line}" : line);
        while (Ev.log.Count > 40) Ev.log.RemoveAt(0);
    }

    private string WorldName(int world) => world >= 0 && world < State.worlds.Count ? State.worlds[world].name : "";

    // ---------- Effects ----------

    private void AddEffect(string label, string what, string resource, float factor, float seconds)
    {
        Ev.effects.Add(new EventEffect { label = label, world = State.currentWorld, what = what, resource = resource, factor = factor, until = State.playTime + seconds });
    }

    /// <summary>Events' effect on a resource's production on the world you're on.</summary>
    public float EventProductionFactor(ResourceType r)
    {
        if (State.events == null || State.events.effects.Count == 0) return 1f;
        float f = 1f;
        foreach (var x in State.events.effects)
            if (x.world == State.currentWorld && x.what == "prod" && (string.IsNullOrEmpty(x.resource) || (r != null && x.resource == r.id))) f *= x.factor;
        return f;
    }

    public float EventGrowthFactor => EffectFactor("growth");

    public float EventThreatFactor => EffectFactor("threat");

    private float EffectFactor(string what)
    {
        if (State.events == null || State.events.effects.Count == 0) return 1f;
        float f = 1f;
        foreach (var x in State.events.effects)
            if (x.world == State.currentWorld && x.what == what) f *= x.factor;
        return f;
    }

    /// <summary>Effects in force here, in plain words (for the event badge's tooltip).</summary>
    public List<string> EffectsHere()
    {
        var list = new List<string>();
        foreach (var x in Ev.effects)
            if (x.world == State.currentWorld)
                list.Add($"{x.label} ({Mathf.CeilToInt((x.until - State.playTime) / 60f)} min)");
        return list;
    }

    // ---------- Pickups ----------

    private void SpawnPickup(PendingEvent e, string look, float near, float far)
    {
        var tc = State.buildings.Find(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);
        if (tc == null || Map == null) return;
        var rng = new System.Random(e.seed);
        Vector2 centre = tc.cell + Vector2.one * 1.5f;
        for (int tries = 0; tries < 40; tries++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = near + (float)rng.NextDouble() * (far - near);
            Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if (!Map.Passable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Team.Player)) continue;
            var pickup = new EventPickup
            {
                id = e.id, world = e.world, position = new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f),
                until = State.playTime + 420f, look = look,
                label = look == "meteor" ? "fallen star" : "lost caravan", item = look == "cart" || rng.NextDouble() < 0.3,
            };
            var age = AgeScale;
            if (look == "meteor")
            {
                string ore = State.ageIndex >= 12 ? "Starmetal" : State.ageIndex >= 2 ? "Iron" : "Stone";
                pickup.goods.Add(new ResourceStock { id = ore, amount = Mathf.Round((ore == "Starmetal" ? 40 : 150) * age) });
                pickup.goods.Add(new ResourceStock { id = "Gold", amount = Mathf.Round(60 * age) });
            }
            else
            {
                pickup.goods.Add(new ResourceStock { id = "Food", amount = Mathf.Round(120 * age) });
                pickup.goods.Add(new ResourceStock { id = "Coin", amount = Mathf.Round(100 * age) });
                pickup.goods.Add(new ResourceStock { id = State.ageIndex >= 4 ? "Steel" : "Wood", amount = Mathf.Round(80 * age) });
            }
            Ev.pickups.Add(pickup);
            if (!offWorld && pickup.world == State.currentWorld) GameAudio.Play(pickup.look == "meteor" ? "meteor" : "wagon", pickup.position);
            return;
        }
    }

    /// <summary>One of your troops reached a pickup: take what's there.</summary>
    public string CollectPickup(int id)
    {
        var p = Ev.pickups.Find(x => x.id == id && x.world == State.currentWorld);
        if (p == null) return null;
        Ev.pickups.Remove(p);
        var parts = new List<string>();
        foreach (var g in p.goods)
        {
            var r = Database.GetResource(g.id);
            if (r == null) continue;
            AddResource(r, g.amount);
            parts.Add($"{g.amount:0} {r.displayName}");
        }
        string found = parts.Count > 0 ? string.Join(", ", parts) : "nothing much";
        if (p.item)
        {
            var item = GainLoot(0.4f, 0.5f, p.look == "meteor" ? "in the fallen star" : "in the lost caravan", Rarity.Uncommon);
            if (item != null) found += $", and {item.name}";
        }
        string line = $"Your troops reached the {p.label}: {found}.";
        if (!offWorld) GameAudio.Play("chest_open", p.position);
        LogEvent(CurrentWorld.name, line);
        Notify(line);
        RaiseChanged();
        return line;
    }

    // ---------- Helpers ----------

    /// <summary>How much bigger event amounts get each age.</summary>
    private float AgeScale => Mathf.Pow(1f + State.ageIndex, 1.25f);

    private float GetAmountId(string id)
    {
        var r = Database.GetResource(id);
        return r != null ? GetAmount(r) : 0f;
    }

    private List<ResourceAmount> CostOf(params (string id, float amount)[] parts)
    {
        var list = new List<ResourceAmount>();
        foreach (var (id, amount) in parts)
        {
            var r = Database.GetResource(id);
            if (r != null && amount > 0) list.Add(new ResourceAmount { resource = r, amount = Mathf.RoundToInt(amount) });
        }
        return list;
    }

    private string GiveEv(params (string id, float amount)[] parts)
    {
        var said = new List<string>();
        foreach (var (id, amount) in parts)
        {
            var r = Database.GetResource(id);
            if (r == null || amount <= 0) continue;
            AddResource(r, Mathf.Round(amount));
            said.Add($"+{Mathf.Round(amount)} {r.displayName}");
        }
        return string.Join(", ", said);
    }

    private BuildingState TrainerHere()
    {
        BuildingState best = null;
        int bestAge = -1;
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt || b.health <= 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.trainsUnit == null || def.trainsUnit.healer) continue;
            int age = def.age != null ? def.age.order : 0;
            if (age > bestAge) { bestAge = age; best = b; }
        }
        return best;
    }

    private HeroState HeroHere() => State.heroes.Find(h => h.world == State.currentWorld && !IsDown(h));

    private bool CanHeal =>
        State.researched.Contains("Herbalism") || State.buildings.Exists(b => b.IsBuilt && Database.GetBuilding(b.definitionId)?.trainsUnit?.healer == true);

    private static string Era(int age, string early, string middle, string late) => age < 4 ? early : age < 11 ? middle : late;

    // ---------- The events themselves ----------

    /// <summary>The card for an event: its words and choices (with what each costs and does).</summary>
    public EventView Describe(PendingEvent e)
    {
        var rng = new System.Random(e.seed);
        int age = State.ageIndex;
        float s = AgeScale;
        var v = new EventView { source = e };
        EventChoice C(string label, string result, List<ResourceAmount> cost, Func<string> apply, string why = null)
        {
            var c = new EventChoice { label = label, result = result, cost = cost ?? new List<ResourceAmount>(), apply = apply, why = why };
            v.choices.Add(c);
            return c;
        }
        switch (e.kind)
        {
            case "Merchant":
            {
                v.title = Era(age, "A Trade Caravan", "Merchant Wagons", SpaceAge ? "A Star Trader" : "A Merchant Convoy");
                v.text = Era(age, "Traders with ox-carts and strange goods have come to your fire.",
                    "A string of merchant wagons rolls in, their owners already setting out stalls.",
                    SpaceAge ? "A trading ship drops out of the sky and opens its hold on your landing field." : "A convoy of trucks pulls in, loaded with goods to swap.");
                // Trade what you have plenty of for what you have little of.
                var stock = State.resources.FindAll(x => x.amount > 50 && Database.GetResource(x.id) != null && x.id != "Coin");
                stock.Sort((a, b) => b.amount.CompareTo(a.amount));
                var scarce = State.resources.FindAll(x => Database.GetResource(x.id) != null && x.id != "Coin");
                scarce.Sort((a, b) => a.amount.CompareTo(b.amount));
                for (int i = 0; i < 2 && i < stock.Count && scarce.Count > 0; i++)
                {
                    var give = stock[i];
                    var get = scarce[Mathf.Min(i, scarce.Count - 1)];
                    if (get.id == give.id) continue;
                    float pay = Mathf.Round(give.amount * 0.2f);
                    float gain = Mathf.Round(Mathf.Max(20 * s, pay * 0.5f * Mathf.Clamp(give.amount / Mathf.Max(1f, get.amount + 50f), 0.3f, 2f)));
                    string gid = get.id, gname = Database.GetResource(get.id).displayName;
                    C($"Trade {pay:0} {Database.GetResource(give.id).displayName} for {gain:0} {gname}", $"+{gain:0} {gname}",
                        CostOf((give.id, pay)), () => "A good deal: " + GiveEv((gid, gain)) + ".");
                }
                if (scarce.Count > 0)
                {
                    var want = scarce[Mathf.Min(2, scarce.Count - 1)];
                    float price = Mathf.Round(90 * s), got = Mathf.Round(60 * s);
                    string wid = want.id, wname = Database.GetResource(want.id).displayName;
                    C($"Buy {got:0} {wname} for {price:0} Coin", $"+{got:0} {wname}", CostOf(("Coin", price)), () => "Bought: " + GiveEv((wid, got)) + ".");
                }
                if (v.choices.Count == 0) C("Wave them on", "", null, () => "There was nothing you needed.");
                break;
            }
            case "Festival":
            {
                v.title = Era(age, "The Feast of the Long Fire", "A Harvest Fair", "A Grand Festival");
                v.text = "Your people want a festival: music, food and dancing for days. It would cost, but they'd remember it.";
                C("Hold the festival", "people grow 40% faster and work 8% harder for 5 minutes", CostOf(("Food", 150 * s), ("Coin", 80 * s)), () =>
                {
                    AddEffect("Festival: people grow faster", "growth", null, 1.4f, 300f);
                    AddEffect("Festival: everyone works harder", "prod", null, 1.08f, 300f);
                    return "The whole settlement celebrated. People are flocking in, and everyone works with a song.";
                });
                C("Not this year", "", null, () => "The festival was put off. There was some grumbling.");
                break;
            }
            case "Harvest":
            {
                v.title = "A Bountiful Harvest";
                v.text = "The fields and the forage have given more than anyone remembers. What to do with it all?";
                C("Store it", $"+{Mathf.Round(250 * s)} Food", null, () => "The stores are full: " + GiveEv(("Food", 250 * s)) + ".");
                C("Hold a feast", "people grow 30% faster for 4 minutes", null, () =>
                {
                    AddEffect("Harvest feast: people grow faster", "growth", null, 1.3f, 240f);
                    return "A feast for everyone. Word spread, and people are coming to join you.";
                });
                break;
            }
            case "Drought":
            {
                bool swamp = HomeBiome == Biome.Swamp || HomeBiome == Biome.Fungal;
                v.title = swamp ? "Blight" : "Drought";
                v.text = swamp ? "A grey rot is spreading through the crops and the forage." : "No rain for weeks. The fields are cracking and the streams are low.";
                C(swamp ? "Burn the rot out" : "Dig wells and channels", "food -10% for 4 minutes", CostOf(("Stone", 100 * s), ("Wood", 100 * s)), () =>
                {
                    AddEffect(swamp ? "Blight (held back)" : "Drought (wells dug)", "prod", "Food", 0.9f, 240f);
                    return swamp ? "The worst of it was burnt out in time." : "The new wells keep most of the fields alive.";
                });
                C("Endure it", "food -30% for 4 minutes", null, () =>
                {
                    AddEffect(swamp ? "Blight" : "Drought", "prod", "Food", 0.7f, 240f);
                    return "Lean weeks ahead.";
                });
                break;
            }
            case "Plague":
            {
                bool heal = CanHeal;
                float lose = heal ? 0.04f : 0.08f, loseQ = heal ? 0.01f : 0.02f;
                v.title = Era(age, "The Coughing Sickness", "Plague", "An Outbreak");
                v.text = "A sickness is spreading through the houses." + (heal ? " Your healers know what to do." : " Without healers it will spread fast (medics or herbalism would help).");
                C("Quarantine the sick", $"lose about {loseQ:P0} of your people, work 10% slower for 3 minutes", CostOf(("Coin", 60 * s), ("Food", 80 * s)), () =>
                {
                    int n = Mathf.CeilToInt(State.population * loseQ);
                    State.population = Mathf.Max(1, State.population - n);
                    AddEffect("Quarantine", "prod", null, 0.9f, 180f);
                    return $"The sickness was contained. {n} died.";
                });
                C("Let it run its course", $"lose about {lose:P0} of your people", null, () =>
                {
                    int n = Mathf.CeilToInt(State.population * lose);
                    State.population = Mathf.Max(1, State.population - n);
                    return $"It burnt itself out, but {n} are gone.";
                });
                break;
            }
            case "Meteor":
            {
                v.title = age >= 12 ? "A Falling Star" : "A Star Falls";
                v.text = "Something blazed across the sky and came down not far from the town. It's still smoking. Send any troops to it before scavengers find it.";
                C("Send troops to it", "it's marked on the map (7 minutes)", null, () => "A party is on its way to the fallen star.");
                break;
            }
            case "Refugees":
            {
                int room = Mathf.Max(0, Housing - Mathf.FloorToInt(State.population));
                int n = Mathf.Clamp(4 + age * 2, 1, Mathf.Max(1, room));
                v.title = "Refugees";
                v.text = Era(age, "A band of people driven from their land by raiders asks to sit at your fire.",
                    "Families fleeing a war far away are at your gates, with what they could carry.",
                    "A ship full of people from a dying colony asks for a place to land.");
                C($"Take them in", $"+{n} people", CostOf(("Food", n * 15 * s)), () =>
                {
                    State.population += n;
                    return $"{n} new people have joined you.";
                }, room <= 0 ? "No room: build more housing" : null);
                C("Turn them away", "", null, () => "They went on, looking for somewhere else.");
                break;
            }
            case "WanderingHero":
            {
                var home = TrainerHere();
                var unit = home != null ? Database.GetBuilding(home.definitionId).trainsUnit : null;
                v.title = "A Wandering Hero";
                v.text = unit != null ? $"A {unit.displayName.ToLower()} of some fame is passing through, looking for a lord worth serving." : "A famous fighter is passing through.";
                C("Hire them", "a new hero (one level up)", CostOf(("Coin", 150 * s), ("Food", 80 * s)), () =>
                {
                    if (unit == null || home == null) return "They left before you could make an offer.";
                    var hero = new HeroState { id = State.nextHeroId++, unitId = unit.name, homeBuilding = home.instanceId, world = State.currentWorld, level = 1 };
                    hero.name = HeroName(hero.id);
                    State.heroes.Add(hero);
                    HeroChanged?.Invoke(hero);
                    return $"{hero.name} the {unit.displayName} has sworn to serve you!";
                }, State.heroes.Count >= HeroSlots ? "Every hero place is taken" : unit == null ? "No troop building for them" : null);
                C("Send them on", "", null, () => "They went looking for another lord.");
                break;
            }
            case "Stranger":
            {
                v.title = "A Mysterious Stranger";
                v.text = "A hooded figure sits by the road, offering a bargain to whoever will listen. Their eyes are the wrong colour.";
                double roll = rng.NextDouble();
                C("Hear them out", "a gift, or a curse", null, () =>
                {
                    if (roll < 0.4)
                    {
                        var item = GainLoot(0.6f, 0.6f, "from the stranger", Rarity.Rare);
                        return item != null ? $"They pressed {item.name} into your hand and were gone." : "They smiled, and were gone.";
                    }
                    if (roll < 0.7) return EventResearch(0.15f, "They told you something nobody knew.");
                    AddEffect("The stranger's curse", "prod", null, 0.9f, 180f);
                    return "Their bargain was a trick. Everything goes wrong for a while (work 10% slower for 3 minutes).";
                });
                C("Give them a meal", "people grow 20% faster for 4 minutes", CostOf(("Food", 60 * s)), () =>
                {
                    AddEffect("The stranger's blessing", "growth", null, 1.2f, 240f);
                    return "They ate, blessed your house, and walked away into the dusk.";
                });
                C("Send them away", "", null, () => "They went without a word.");
                break;
            }
            case "GoldRush":
            {
                v.title = "Gold in the Hills";
                v.text = "Someone found nuggets in a stream, and now everyone with a pan wants to go digging.";
                C("Let them dig", "gold +50% for 5 minutes", null, () =>
                {
                    AddEffect("Gold rush", "prod", "Gold", 1.5f, 300f);
                    return "The rush is on!";
                });
                C("Tax the prospectors", $"+{Mathf.Round(120 * s)} Coin now, gold +20% for 5 minutes", null, () =>
                {
                    AddEffect("Gold rush (taxed)", "prod", "Gold", 1.2f, 300f);
                    return "The tax men are doing well: " + GiveEv(("Coin", 120 * s)) + ".";
                });
                break;
            }
            case "Wildfire":
            {
                v.title = "Wildfire";
                v.text = "Fire is sweeping through the woods towards the lumber camps.";
                C("Fight the fire", "wood -15% for 2 minutes", CostOf(("Food", 80 * s)), () =>
                {
                    AddEffect("Wildfire (put out)", "prod", "Wood", 0.85f, 120f);
                    return "It was beaten back after a long night.";
                });
                C("Let it burn", "wood -45% for 4 minutes, a couple of buildings singed", null, () =>
                {
                    AddEffect("Wildfire", "prod", "Wood", 0.55f, 240f);
                    int hit = 0;
                    var built = State.buildings.FindAll(b => b.IsBuilt && b.health > 0);
                    for (int i = 0; i < 2 && built.Count > 0; i++)
                    {
                        var b = built[UnityEngine.Random.Range(0, built.Count)];
                        b.health = Mathf.Max(1f, b.health - Mathf.Round(StatsOf(b).maxHealth * 0.1f));
                        hit++;
                    }
                    return $"It burnt itself out. {hit} building{(hit == 1 ? " was" : "s were")} singed.";
                });
                break;
            }
            case "Eclipse":
            {
                v.title = "An Eclipse";
                v.text = "The sun has gone dark at midday. Your people are frightened, and the raiders take it as a sign.";
                C("Keep a vigil", "raids 10% stronger for 4 minutes", CostOf(("Coin", 50 * s)), () =>
                {
                    AddEffect("Eclipse (vigil kept)", "threat", null, 1.1f, 240f);
                    return "The vigil steadied everyone. The light came back.";
                });
                C("Ignore the omen", "raids 30% stronger for 5 minutes", null, () =>
                {
                    AddEffect("Eclipse: raiders bolder", "threat", null, 1.3f, 300f);
                    return "The raiders are emboldened.";
                });
                break;
            }
            case "Rival":
            {
                var hero = HeroHere();
                float chance = hero != null ? Mathf.Clamp(0.35f + 0.07f * hero.level, 0.15f, 0.9f) : 0f;
                bool win = rng.NextDouble() < chance;
                v.title = "A Rival's Challenge";
                v.text = hero != null ? $"A rival chief has come to challenge your champion, {hero.name}, to single combat." : "A rival chief has come to challenge your champion.";
                C("Accept the duel", $"about {chance:P0} to win: a prize if you do, the hero hurt if not", null, () =>
                {
                    if (hero == null) return "There was nobody to fight.";
                    if (win)
                    {
                        GainXp(hero, 40f * (1 + State.ageIndex));
                        var item = GainLoot(0.5f, 0.6f, "from the rival chief", Rarity.Rare);
                        return $"{hero.name} won! " + GiveEv(("Coin", 150 * s)) + (item != null ? $", and {item.name}" : "") + ".";
                    }
                    hero.downUntil = State.playTime + 180f;
                    HeroChanged?.Invoke(hero);
                    return $"{hero.name} lost, and needs three minutes to recover.";
                }, hero == null ? "No hero here" : null);
                C("Refuse", "", null, () => "The rival left, laughing.");
                break;
            }
            case "LostCaravan":
            {
                v.title = "A Lost Caravan";
                v.text = "A merchant caravan went missing out in the wild. Its goods are still out there, for whoever finds them first.";
                C("Send troops to find it", "it's marked on the map (7 minutes)", null, () => "Scouts are looking for the lost caravan.");
                break;
            }
            case "Scholars":
            {
                v.title = Era(age, "Wise Ones", "Travelling Scholars", "Visiting Scientists");
                v.text = "Learned visitors offer to stay a while and help with your work, if you'll feed and pay them.";
                C("Host them", "current research +25%", CostOf(("Food", 100 * s), ("Coin", 80 * s)), () => EventResearch(0.25f, "They worked through the nights."));
                C("Turn them away", "", null, () => "They went on to the next town.");
                break;
            }
            case "SolarFlare":
            {
                v.title = "A Solar Flare";
                v.text = "The sun has thrown off a storm of charged light. Every circuit on the world is humming.";
                C("Shield the grid", "nothing harmed", CostOf(("Steel", 120 * s)), () => "The shielding held.");
                C("Ride it out", "power cells -35% and microchips -20% for 4 minutes", null, () =>
                {
                    AddEffect("Solar flare: power down", "prod", "PowerCells", 0.65f, 240f);
                    AddEffect("Solar flare: chips down", "prod", "Microchips", 0.8f, 240f);
                    return "The grid is limping.";
                });
                break;
            }
            case "Derelict":
            {
                v.title = "A Derelict Drifts By";
                v.text = "An old ship, dark and silent, is drifting past in orbit. Your Starport could send a crew up.";
                double roll = rng.NextDouble();
                C("Salvage it", "steel, gears and chips; maybe something more", CostOf(("PowerCells", 40 * s)), () =>
                {
                    string got = GiveEv(("Steel", 120 * s), ("Gears", 60 * s), ("Microchips", 25 * s));
                    if (roll < 0.4)
                    {
                        var item = GainLoot(0.6f, 0.6f, "aboard the derelict", Rarity.Rare, true);
                        if (item != null) got += $", and {item.name}";
                    }
                    return "The crew came back loaded: " + got + ".";
                });
                C("Leave it", "", null, () => "It drifted on into the dark.");
                break;
            }
            case "Comet":
            {
                v.title = "A Comet";
                v.text = "A great comet is passing close, its tail across half the sky.";
                C("Watch it", "people grow 15% faster for 4 minutes; research +5%", null, () =>
                {
                    AddEffect("Comet: wonder", "growth", null, 1.15f, 240f);
                    return EventResearch(0.05f, "Everyone stayed up to watch it.");
                });
                C("Mine its tail", "stone and iron", CostOf(("PowerCells", 30 * s)), () => "Ice and rock by the tonne: " + GiveEv(("Stone", 200 * s), ("Iron", 120 * s)) + ".",
                    HasStarport ? null : "Needs a Starport");
                break;
            }
            default:
                return null;
        }
        return v;
    }

    private string EventResearch(float share, string intro)
    {
        if (!BoostResearch(share)) return intro + " With nothing being researched, it became trade instead: " + GiveEv(("Coin", 80 * AgeScale)) + ".";
        return $"{intro} Your research jumps ahead ({share:P0}).";
    }
}
