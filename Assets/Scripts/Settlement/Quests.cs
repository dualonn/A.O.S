using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>What a quest step asks for.</summary>
public enum QuestStepKind
{
    Travel,      // bring a hero to a spot on the map
    Slay,        // a named boss waits at a lair; it wakes when a hero comes near
    ClearCamp,   // burn a raider camp (and take back what they stole)
    ClearSite,   // clear a ruin, dungeon or lair already on the map
    Deliver,     // hand over resources (from the quest log)
    Survive,     // hold out through raids
    Explore,     // set foot in a land you haven't reached
    VisitWorld,  // take a hero to another world
}

/// <summary>Every quest in the save: offered, under way and finished.</summary>
[Serializable]
public class QuestLog
{
    [Tooltip("Quests under way (each on its own world; quests on worlds you aren't looking at wait).")]
    public List<QuestState> active = new List<QuestState>();
    [Tooltip("Quests offered and waiting for a yes or no.")]
    public List<QuestState> offers = new List<QuestState>();
    [Tooltip("Stories of finished quests, newest last.")]
    public List<QuestTale> tales = new List<QuestTale>();
    [Tooltip("Named relics won from quests, with their stories.")]
    public List<QuestRelic> relics = new List<QuestRelic>();
    [Tooltip("Chapters of the handwritten storylines that are done (arc:chapter).")]
    public List<string> arcsDone = new List<string>();
    [Tooltip("For each world: seconds until someone next comes with a quest.")]
    public List<float> timers = new List<float>();
    public int nextId = 1;
    public int completed;
    [Tooltip("Camps cleared when last looked (a freed prisoner may have a tale).")]
    public int lastCampsCleared = -1;
    [Tooltip("Play time before a site or camp can start another quest.")]
    public float hookCooldownUntil;
}

[Serializable]
public class QuestState
{
    public int id;
    [Tooltip("Handwritten storyline id (empty for a procedural quest).")]
    public string arc = "";
    public int chapter;
    [Tooltip("Look and creatures: spirit, wyrm, barrow, beast, expedition, conspiracy, machine, rift, derelict, precursor, bandit.")]
    public string theme = "beast";
    public string title, giver, intro;
    public int world;
    public int seed;
    [Tooltip("Age index its foes are drawn from.")]
    public int tier;
    public int step;
    public List<QuestStep> steps = new List<QuestStep>();
    public QuestReward reward = new QuestReward();
    public float expiresAt;
}

[Serializable]
public class QuestStep
{
    public QuestStepKind kind;
    public string text;
    [Tooltip("Where (map tiles): the lair, the spot to reach, the camp or site, the land to explore.")]
    public Vector2 spot;
    [Tooltip("The camp's or site's cell (ClearCamp, ClearSite).")]
    public Vector2Int cell;
    [Tooltip("Has its target been picked on the map yet?")]
    public bool ready;
    [Tooltip("Slay: the boss's unit asset name and its name.")]
    public string unit, name;
    [Tooltip("Deliver: what and how much.")]
    public string resource;
    public int amount;
    [Tooltip("Survive: raids; Travel/Slay: distance band (0 near, 1 middle, 2 far, -1 home); ClearSite: the SiteKind wanted.")]
    public int count;
    public int baseline = -1;
    public int biome = -1;
    [Tooltip("The hero who did it (for the experience).")]
    public int heroId;
    public bool done;
}

[Serializable]
public class QuestReward
{
    public float xp;
    [Tooltip("A named relic: its name, slot (GearSlot, -1 for none) and story.")]
    public string relic;
    public int relicSlot = -1;
    public string relicLore;
    [Tooltip("A boon for the realm (a quest-boon technology id), or 'any' for one not yet held.")]
    public string boon;
    public List<ResourceStock> goods = new List<ResourceStock>();
    [Tooltip("Someone rescued joins as a hero (if there is a hero place free).")]
    public string recruit;
    public string taleTitle, taleText;
}

[Serializable]
public class QuestTale
{
    public string title, text;
    public int world;
}

[Serializable]
public class QuestRelic
{
    public int uid;
    public string quest, lore;
}

/// <summary>
/// Hero quests and storylines: multi-step quests offered by wanderers, lore stones, ruins,
/// shrines, freed prisoners and native chiefs. Steps send heroes across the map, wake named
/// bosses in their lairs, burn camps, ask for resources, hold out through raids, explore new
/// lands and reach other worlds. Rewards: named relics with their own stories, experience, boons
/// for the realm, new heroes and tales. Handwritten storylines for each age, plus endless
/// procedural ones (see QuestStories.cs). Quests on worlds you aren't looking at simply wait.
/// </summary>
public partial class Settlement
{
    /// <summary>A quest was offered on the world you're on.</summary>
    public event Action<QuestState> QuestOffered;

    /// <summary>A quest started, moved on a step, finished or was given up.</summary>
    public event Action<QuestState> QuestChanged;

    public const int MaxActiveQuests = 3;
    private const float QuestFirstOffer = 360f, QuestGapMin = 420f, QuestGapMax = 720f, QuestOfferLife = 420f;

    public QuestLog Quests
    {
        get
        {
            if (State.quests == null) State.quests = new QuestLog();
            return State.quests;
        }
    }

    public List<QuestState> QuestOffersHere => Quests.offers.FindAll(q => q.world == State.currentWorld);

    public QuestState GetQuest(int id) => Quests.active.Find(q => q.id == id) ?? Quests.offers.Find(q => q.id == id);

    public static QuestStep CurrentStep(QuestState q) => q != null && q.step >= 0 && q.step < q.steps.Count ? q.steps[q.step] : null;

    // ---------- Ticking ----------

    private float questTickAccum;

    private void TickQuests(float dt)
    {
        var log = Quests;
        float now = State.playTime;
        for (int i = log.offers.Count - 1; i >= 0; i--)
        {
            var o = log.offers[i];
            if (now < o.expiresAt) continue;
            log.offers.RemoveAt(i);
            if (o.world == State.currentWorld) Notify($"Nobody took up <color=#fd6>{o.title}</color>. The chance has passed, for now.");
        }
        questTickAccum += dt;
        if (questTickAccum < 1f) return;
        float step = questTickAccum;
        questTickAccum = 0f;

        foreach (var q in log.active.ToArray())
        {
            var s = CurrentStep(q);
            if (s == null) { QuestComplete(q); continue; }
            if (s.kind == QuestStepKind.VisitWorld)
            {
                // Checked wherever you are: some hero has reached a world other than the quest's.
                var h = State.heroes.Find(x => x.world != q.world);
                if (h != null) QuestAdvance(q, h.id);
                continue;
            }
            if (q.world != State.currentWorld) continue;
            QuestCheckStep(q, s);
        }

        // Freed prisoners: a cleared camp sometimes has someone with a tale.
        if (log.lastCampsCleared < 0) log.lastCampsCleared = State.campsCleared;
        if (State.campsCleared > log.lastCampsCleared)
        {
            log.lastCampsCleared = State.campsCleared;
            if (UnityEngine.Random.value < 0.3f) QuestTryOffer("camp", null);
        }

        // Wanderers, storytellers and chiefs come by now and then.
        int w = State.currentWorld;
        while (log.timers.Count <= w) log.timers.Add(QuestFirstOffer);
        log.timers[w] -= step;
        if (log.timers[w] > 0) return;
        bool raid = FreeModeController.Instance != null && FreeModeController.Instance.WaveInProgress;
        if (raid || State.secondsUntilWave < 25f || !QuestRoomForOffer())
        {
            log.timers[w] = 45f;
            return;
        }
        log.timers[w] = UnityEngine.Random.Range(QuestGapMin, QuestGapMax);
        bool chief = OnAlienWorld && CurrentWorld.diplomacy != null && CurrentWorld.diplomacy.met && NativeChief() != null && UnityEngine.Random.value < 0.4f;
        QuestTryOffer(chief ? "chief" : "wanderer", null);
    }

    private bool QuestRoomForOffer() =>
        IsFounded && Quests.active.Count < MaxActiveQuests && QuestOffersHere.Count == 0;

    /// <summary>A site was used up: lore stones, ruins, shrines and dungeons sometimes start a quest.</summary>
    private void QuestSiteFinished(SiteState site)
    {
        if (offWorld || site == null || State.playTime < Quests.hookCooldownUntil) return;
        float chance = site.kind switch
        {
            SiteKind.LoreStone => 0.35f,
            SiteKind.Ruins => 0.25f,
            SiteKind.Shrine => 0.3f,
            SiteKind.Dungeon => 0.4f,
            SiteKind.NaturalWonder => 0.5f,
            SiteKind.Lair => 0.15f,
            _ => 0f,
        };
        if (UnityEngine.Random.value < chance) QuestTryOffer(site.kind == SiteKind.LoreStone ? "lore" : "site", site);
    }

    private void QuestTryOffer(string source, SiteState site)
    {
        if (!QuestRoomForOffer()) return;
        var q = QuestGenerate(source, site);
        if (q == null) return;
        q.id = Quests.nextId++;
        q.world = State.currentWorld;
        q.expiresAt = State.playTime + QuestOfferLife;
        Quests.offers.Add(q);
        Quests.hookCooldownUntil = State.playTime + 120f;
        Notify($"<color=#fd6>A quest: {q.title}</color>. {q.giver} has a task for your heroes (see the card under the top bar).");
        if (!offWorld) GameAudio.UI("quest_offer");
        QuestOffered?.Invoke(q);
        RaiseChanged();
    }

    // ---------- Your choices ----------

    public bool CanAcceptQuest(QuestState q, out string why)
    {
        why = null;
        if (q == null || !Quests.offers.Contains(q)) why = "No longer offered";
        else if (Quests.active.Count >= MaxActiveQuests) why = $"You already have {MaxActiveQuests} quests under way";
        return why == null;
    }

    public void AcceptQuest(int id)
    {
        var q = Quests.offers.Find(x => x.id == id);
        if (!CanAcceptQuest(q, out var why)) { if (why != null) Notify(why); return; }
        Quests.offers.Remove(q);
        Quests.active.Add(q);
        q.step = 0;
        var s = CurrentStep(q);
        if (s != null && q.world == State.currentWorld) QuestPrepare(q, s);
        Notify($"Quest begun: <color=#fd6>{q.title}</color>. {(s != null ? s.text : "")}");
        if (!offWorld) GameAudio.UI("quest_accept");
        QuestChanged?.Invoke(q);
        RaiseChanged();
    }

    public void DeclineQuest(int id)
    {
        var q = Quests.offers.Find(x => x.id == id);
        if (q == null) return;
        Quests.offers.Remove(q);
        Notify($"You turned down <color=#fd6>{q.title}</color>.");
        RaiseChanged();
    }

    public void AbandonQuest(int id)
    {
        var q = Quests.active.Find(x => x.id == id);
        if (q == null) return;
        Quests.active.Remove(q);
        Notify($"Quest given up: <color=#fd6>{q.title}</color>. {(string.IsNullOrEmpty(q.arc) ? "" : "Its tale may find you again.")}");
        QuestChanged?.Invoke(q);
        RaiseChanged();
    }

    /// <summary>Can the current step's resources be handed over now?</summary>
    public bool CanDeliver(QuestState q, out List<ResourceAmount> cost)
    {
        cost = new List<ResourceAmount>();
        var s = CurrentStep(q);
        if (s == null || s.kind != QuestStepKind.Deliver || q.world != State.currentWorld) return false;
        var r = Database.GetResource(s.resource);
        if (r == null) return true;   // (a resource this build doesn't have: let it pass)
        cost.Add(new ResourceAmount { resource = r, amount = s.amount });
        return CanAfford(cost);
    }

    public void QuestDeliver(int id)
    {
        var q = Quests.active.Find(x => x.id == id);
        if (q == null || !CanDeliver(q, out var cost)) return;
        Spend(cost);
        QuestAdvance(q, 0);
    }

    // ---------- Steps ----------

    /// <summary>Pick the step's target on the map (on the quest's own world).</summary>
    private void QuestPrepare(QuestState q, QuestStep s)
    {
        if (s.ready || Map == null || q.world != State.currentWorld) return;
        var rng = new System.Random(q.seed ^ (q.step * 7919) ^ State.mapSeed);
        switch (s.kind)
        {
            case QuestStepKind.Travel:
            case QuestStepKind.Slay:
                s.spot = s.count < 0 ? QuestHome() : QuestPickSpot(s.count, rng);
                break;
            case QuestStepKind.ClearCamp:
            {
                CampState best = null;
                float bestD = float.MaxValue;
                var home = QuestHome();
                foreach (var c in State.camps)
                {
                    if (c.cleared) continue;
                    float d = Vector2.Distance(c.Centre, home);
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (best == null) { QuestToSlay(q, s, "bandit", rng); return; }
                s.cell = best.cell;
                s.spot = best.Centre;
                break;
            }
            case QuestStepKind.ClearSite:
            {
                SiteState best = null;
                float bestD = float.MaxValue;
                var home = QuestHome();
                foreach (var site in State.sites)
                {
                    if (site.done || (int)site.kind != s.count) continue;
                    float d = Vector2.Distance(site.Centre, home);
                    if (d < bestD) { bestD = d; best = site; }
                }
                if (best == null) { QuestToSlay(q, s, q.theme, rng); return; }
                s.cell = best.cell;
                s.spot = best.Centre;
                break;
            }
            case QuestStepKind.Explore:
                if (!QuestFindNewLand(out s.biome, out s.spot))
                {
                    s.kind = QuestStepKind.Travel;
                    s.count = 2;
                    s.spot = QuestPickSpot(2, rng);
                    s.text = "Send a hero to the far edge of the known lands.";
                }
                else s.text = $"Set foot in the {BiomeName((Biome)s.biome)}, to the {QuestDirection(QuestHome(), s.spot)}.";
                break;
            case QuestStepKind.Survive:
                s.baseline = State.wavesSurvived;
                break;
        }
        s.ready = true;
    }

    /// <summary>No camp or site to send them to: a named foe at a lair instead.</summary>
    private void QuestToSlay(QuestState q, QuestStep s, string theme, System.Random rng)
    {
        s.kind = QuestStepKind.Slay;
        s.count = 1;
        if (string.IsNullOrEmpty(s.name)) s.name = QuestBossName(theme, rng);
        if (string.IsNullOrEmpty(s.unit)) s.unit = QuestBossUnitFor(theme);
        s.spot = QuestPickSpot(1, rng);
        s.text = $"Hunt down {s.name} at {QuestPlaceName(theme, rng)}.";
        s.ready = true;
    }

    private void QuestCheckStep(QuestState q, QuestStep s)
    {
        if (!s.ready) QuestPrepare(q, s);
        switch (s.kind)
        {
            case QuestStepKind.ClearCamp:
            {
                var camp = State.camps.Find(c => c.cell == s.cell);
                if (camp == null || camp.cleared) QuestAdvance(q, 0);
                break;
            }
            case QuestStepKind.ClearSite:
            {
                var site = State.sites.Find(x => x.cell == s.cell);
                if (site == null || site.done) QuestAdvance(q, 0);
                break;
            }
            case QuestStepKind.Survive:
                if (s.baseline < 0) s.baseline = State.wavesSurvived;
                if (State.wavesSurvived - s.baseline >= s.count) QuestAdvance(q, 0);
                break;
            case QuestStepKind.Explore:
                if (s.biome >= 0 && State.biomesVisited.Contains(s.biome)) QuestAdvance(q, 0);
                break;
        }
    }

    /// <summary>The scene: a hero reached the step's spot.</summary>
    public void QuestReached(int id, HeroState hero)
    {
        var q = Quests.active.Find(x => x.id == id);
        var s = CurrentStep(q);
        if (s == null || s.kind != QuestStepKind.Travel || q.world != State.currentWorld) return;
        if (hero != null) Notify($"{hero.name} has reached {QuestSpotName(s)}.");
        GameAudio.UI("discover");
        QuestAdvance(q, hero != null ? hero.id : 0);
    }

    /// <summary>The scene: the step's boss has fallen.</summary>
    public void QuestBossSlain(int id, HeroState hero)
    {
        var q = Quests.active.Find(x => x.id == id);
        var s = CurrentStep(q);
        if (s == null || s.kind != QuestStepKind.Slay || q.world != State.currentWorld) return;
        Notify($"<color=#fd6>{s.name}</color> is slain!");
        GameAudio.UI("goal_complete");
        QuestAdvance(q, hero != null ? hero.id : s.heroId);
    }

    private void QuestAdvance(QuestState q, int heroId)
    {
        var s = CurrentStep(q);
        if (s == null) return;
        s.done = true;
        if (heroId != 0) s.heroId = heroId;
        var hero = heroId != 0 ? GetHero(heroId) : null;
        if (hero != null) GainXp(hero, 15f * (q.tier + 1));
        q.step++;
        var next = CurrentStep(q);
        if (next == null)
        {
            QuestComplete(q);
            return;
        }
        if (q.world == State.currentWorld) QuestPrepare(q, next);
        Notify($"<color=#fd6>{q.title}</color>: done. Next: {next.text}");
        if (!offWorld) GameAudio.UI("quest_step");
        QuestChanged?.Invoke(q);
        RaiseChanged();
    }

    // ---------- Rewards ----------

    private void QuestComplete(QuestState q)
    {
        var log = Quests;
        log.active.Remove(q);
        log.completed++;
        if (!string.IsNullOrEmpty(q.arc)) { string key = $"{q.arc}:{q.chapter}"; if (!log.arcsDone.Contains(key)) log.arcsDone.Add(key); }
        var said = new List<string>();
        var r = q.reward;

        // Experience: to the hero who saw it through, or shared by the heroes on that world.
        HeroState finisher = null;
        for (int i = q.steps.Count - 1; i >= 0 && finisher == null; i--) if (q.steps[i].heroId != 0) finisher = GetHero(q.steps[i].heroId);
        if (r.xp > 0)
        {
            if (finisher != null) { GainXp(finisher, r.xp); said.Add($"{finisher.name} gains {r.xp:0} experience"); }
            else
            {
                var here = State.heroes.FindAll(h => h.world == q.world);
                foreach (var h in here) GainXp(h, r.xp / here.Count);
                if (here.Count > 0) said.Add($"your heroes share {r.xp:0} experience");
            }
        }

        if (r.goods.Count > 0)
        {
            var parts = new List<(string, int)>();
            foreach (var g in r.goods) parts.Add((g.id, Mathf.RoundToInt(g.amount)));
            var got = GiveResources(parts.ToArray());
            if (got.Count > 0) said.Add(Describe(got));
        }

        if (!string.IsNullOrEmpty(r.relic))
        {
            var item = QuestMakeRelic(q, r.relic, r.relicSlot, r.relicLore);
            if (item != null) said.Add($"the relic <color=#{Items.RarityHex(item.rarity)}>{item.name}</color>");
        }

        if (!string.IsNullOrEmpty(r.boon))
        {
            var boon = QuestGrantBoon(r.boon);
            if (boon != null) said.Add($"the boon <color=#9df>{boon.displayName}</color> ({boon.description})");
            else said.Add(Describe(GiveResources(("Gold", Mathf.RoundToInt(40 * AgeScale)), ("Coin", Mathf.RoundToInt(60 * AgeScale)))));
        }

        if (!string.IsNullOrEmpty(r.recruit))
        {
            var hero = QuestRecruit(r.recruit);
            if (hero != null) said.Add($"{hero.name} joins you as a hero");
            else
            {
                var gift = GainLoot(0.5f, 0.6f, $"from {r.recruit}", Rarity.Epic);
                said.Add(gift != null ? $"{r.recruit} could not stay (no hero place free), but left you {gift.name}" : $"{r.recruit}'s thanks");
            }
        }

        if (!string.IsNullOrEmpty(r.taleTitle))
        {
            log.tales.Add(new QuestTale { title = r.taleTitle, text = r.taleText, world = q.world });
            while (log.tales.Count > 80) log.tales.RemoveAt(0);
        }
        // The deed is remembered: sometimes it brings an old story to light too.
        if (UnityEngine.Random.value < 0.5f)
        {
            int pick = Lore.Pick(q.seed & 0x7fff, State.ageIndex, -1, State.journal);
            if (pick >= 0) { State.journal.Add(pick); said.Add($"a page for the Journal: {Lore.Entries[pick].title}"); }
        }

        Notify($"<color=#fd6>Quest complete: {q.title}!</color> " + (said.Count > 0 ? "Rewards: " + string.Join("; ", said) + "." : ""));
        if (!offWorld) GameAudio.UI("quest_complete");
        QuestChanged?.Invoke(q);
        RaiseChanged();
    }

    /// <summary>A legendary named item for the inventory, with its story kept in the quest log.</summary>
    private ItemInstance QuestMakeRelic(QuestState q, string name, int slot, string lore)
    {
        var rng = new System.Random(q.seed ^ 0x5eed ^ State.nextItemId);
        bool alien = OnAlienWorld;
        float power = Progress + 0.6f;
        if (slot < 0) slot = rng.NextDouble() < 0.4 ? (int)GearSlot.Trinket : rng.NextDouble() < 0.5 ? (int)GearSlot.Body : (int)GearSlot.Head;
        ItemTemplate best = null;
        foreach (var t in Database.itemTemplates)
        {
            if (t == null || t.tradeOnly || (int)t.slot != slot || t.minPower > power + 0.25f) continue;
            if (t.xeno && !alien) continue;
            if (best == null || t.minPower > best.minPower || (t.minPower == best.minPower && rng.NextDouble() < 0.5)) best = t;
        }
        if (best == null) return GainLoot(0.6f, 0.8f, $"as {q.title}'s reward", Rarity.Epic);
        var item = new ItemInstance
        {
            uid = State.nextItemId++,
            template = best.id,
            power = Mathf.Round(power * 100f) / 100f,
            rarity = Rarity.Legendary,
        };
        for (int i = 0; i < 3; i++) item.traits.Add(new TraitRoll());
        Items.RerollTraits(item, best, rng);
        item.enchanted = true;
        item.enchant = Items.RollEnchantment(item, rng);
        item.name = name;
        State.inventory.Add(item);
        Quests.relics.Add(new QuestRelic { uid = item.uid, quest = q.title, lore = lore });
        if (!offWorld) GameAudio.UI("drop_legendary");
        return item;
    }

    /// <summary>The story of a quest relic (null for ordinary gear).</summary>
    public QuestRelic RelicStory(int uid) => State?.quests?.relics.Find(r => r.uid == uid);

    /// <summary>The boons quests can give (set up by Tools > Age of Stickman > Setup Quests).</summary>
    public List<Technology> QuestBoons()
    {
        var list = new List<Technology>();
        var art = QuestArt.Get();
        if (art == null) return list;
        foreach (var t in art.boons) if (t != null && Database.GetTechnology(t.id) != null) list.Add(t);
        return list;
    }

    public List<Technology> QuestBoonsHeld() => QuestBoons().FindAll(b => State.researched.Contains(b.id));

    private Technology QuestGrantBoon(string id)
    {
        var all = QuestBoons();
        var boon = all.Find(b => b.id == id && !State.researched.Contains(b.id));
        if (boon == null)
        {
            var free = all.FindAll(b => !State.researched.Contains(b.id));
            if (free.Count == 0) return null;   // every boon is held: something else instead
            boon = free[UnityEngine.Random.Range(0, free.Count)];
        }
        State.researched.Add(boon.id);
        return boon;
    }

    private HeroState QuestRecruit(string who)
    {
        if (State.heroes.Count >= HeroSlots) return null;
        var home = TrainerHere();
        var unit = home != null ? Database.GetBuilding(home.definitionId).trainsUnit : null;
        if (unit == null) return null;
        var hero = new HeroState
        {
            id = State.nextHeroId++, unitId = unit.name, homeBuilding = home.instanceId, world = State.currentWorld,
            level = Mathf.Clamp(1 + State.ageIndex / 2, 1, Mathf.Max(1, LevelCap - 2)),
        };
        hero.name = who;
        State.heroes.Add(hero);
        if (!offWorld) GameAudio.UI("hero_recruit");
        HeroChanged?.Invoke(hero);
        return hero;
    }

    // ---------- For the scene ----------

    /// <summary>Quest targets on the world you're on (the current step of each quest under way).</summary>
    public List<(QuestState quest, QuestStep step)> QuestTargetsHere()
    {
        var list = new List<(QuestState, QuestStep)>();
        if (State == null || State.quests == null) return list;
        foreach (var q in State.quests.active)
        {
            if (q.world != State.currentWorld) continue;
            var s = CurrentStep(q);
            if (s == null || s.kind == QuestStepKind.Deliver || s.kind == QuestStepKind.Survive || s.kind == QuestStepKind.VisitWorld) continue;
            if (!s.ready) QuestPrepare(q, s);
            if (s.ready) list.Add((q, s));
        }
        return list;
    }

    /// <summary>The boss a Slay step wakes: the quest's own (from Setup Quests), else a chief of its age.</summary>
    public UnitDefinition QuestBossDef(QuestState q, QuestStep s)
    {
        var art = QuestArt.Get();
        var def = art != null ? art.Boss(s.unit) : null;
        if (def != null && def.prefab != null) return def;
        var native = OnAlienWorld ? NativeChief() : null;
        if (native != null && native.prefab != null) return native;
        return QuestReferenceBoss(q.tier);
    }

    /// <summary>A chief of this age (or the latest earlier age that has one), for sizing quest bosses.</summary>
    public UnitDefinition QuestReferenceBoss(int tier)
    {
        for (int i = Mathf.Min(tier, Database.ages.Count - 1); i >= 0; i--)
        {
            var age = Database.GetAge(i);
            if (age == null) continue;
            var b = age.bosses.Find(x => x != null && x.prefab != null);
            if (b != null) return b;
        }
        return null;
    }

    /// <summary>The boss's followers: creatures that fit the story, or raiders of its age.</summary>
    public List<UnitDefinition> QuestMinionDefs(QuestState q)
    {
        var list = new List<UnitDefinition>();
        void Add(UnitDefinition u) { if (u != null && u.prefab != null && u.prefab.GetComponent<Enemy>() != null) list.Add(u); }
        switch (q.theme)
        {
            case "spirit": Add(Database.wolf); break;
            case "beast": Add(Database.wolf); Add(Database.giantSpider); break;
            case "barrow":
            case "expedition": Add(Database.skeleton); Add(Database.skeletonArcher); break;
            case "derelict":
            case "precursor":
                foreach (var u in NativeUnits()) Add(u);
                break;
        }
        if (list.Count == 0)
        {
            for (int i = Mathf.Min(q.tier, Database.ages.Count - 1); i >= 0 && list.Count == 0; i--)
            {
                var age = Database.GetAge(i);
                if (age != null) foreach (var u in age.enemyUnits) Add(u);
            }
        }
        return list;
    }

    /// <summary>How many followers wake with a quest boss (a few more each few ages, never more than five).</summary>
    public int QuestMinionCount(QuestState q) => Mathf.Min(5, 2 + q.tier / 4);

    /// <summary>How much tougher than its asset a quest boss is: sized to the chiefs of its age, a little stronger.</summary>
    public GearStats QuestBossBoost(QuestState q, UnitDefinition def)
    {
        var g = new GearStats();
        var reference = QuestReferenceBoss(q.tier);
        if (def == null || reference == null || def == reference) { g.health = 0.25f; g.attack = 0.1f; return g; }
        g.health = Mathf.Max(0f, reference.maxHealth * 1.35f / Mathf.Max(1, def.maxHealth) - 1f);
        g.attack = Mathf.Max(0f, reference.attackPower * 1.1f / Mathf.Max(1, def.attackPower) - 1f);
        return g;
    }

    // ---------- Words for the UI ----------

    public string QuestStepProgress(QuestState q, QuestStep s)
    {
        switch (s.kind)
        {
            case QuestStepKind.Survive:
                if (q.world != State.currentWorld || s.baseline < 0) return $"(0/{s.count})";
                return $"({Mathf.Clamp(State.wavesSurvived - s.baseline, 0, s.count)}/{s.count})";
            case QuestStepKind.Deliver:
            {
                var r = Database.GetResource(s.resource);
                float have = r != null && q.world == State.currentWorld ? GetAmount(r) : 0f;
                return $"({Mathf.FloorToInt(Mathf.Min(have, s.amount))}/{s.amount})";
            }
            case QuestStepKind.Travel:
            case QuestStepKind.Slay:
            case QuestStepKind.ClearCamp:
            case QuestStepKind.ClearSite:
            case QuestStepKind.Explore:
                if (!s.ready || q.world != State.currentWorld) return "";
                float d = Vector2.Distance(QuestHome(), s.spot);
                return d < 6f ? "(at home)" : $"({d:0} tiles {QuestDirection(QuestHome(), s.spot)})";
        }
        return "";
    }

    public string QuestRewardText(QuestState q)
    {
        var r = q.reward;
        var parts = new List<string>();
        if (r.xp > 0) parts.Add($"{r.xp:0} hero experience");
        if (!string.IsNullOrEmpty(r.relic)) parts.Add($"<color=#{Items.RarityHex(Rarity.Legendary)}>{r.relic}</color> (a legendary relic)");
        if (!string.IsNullOrEmpty(r.boon))
        {
            var b = QuestBoons().Find(x => x.id == r.boon);
            parts.Add(b != null ? $"<color=#9df>{b.displayName}</color> ({b.description})" : "<color=#9df>a boon for the realm</color>");
        }
        if (!string.IsNullOrEmpty(r.recruit)) parts.Add($"{r.recruit} may join you as a hero");
        foreach (var g in r.goods)
        {
            var res = Database.GetResource(g.id);
            if (res != null && (res.unlockedIn == null || res.unlockedIn.order <= State.ageIndex)) parts.Add($"{g.amount:0} {res.displayName}");
        }
        if (!string.IsNullOrEmpty(r.taleTitle)) parts.Add("a tale for the quest log");
        return string.Join(", ", parts);
    }

    public string QuestWorldName(QuestState q) => q.world >= 0 && q.world < State.worlds.Count ? State.worlds[q.world].name : "";

    private string QuestSpotName(QuestStep s)
    {
        int i = s.text != null ? s.text.IndexOf(" the ", StringComparison.Ordinal) : -1;
        return i >= 0 ? "the " + s.text.Substring(i + 5).TrimEnd('.') : "the spot";
    }

    // ---------- The map ----------

    /// <summary>The town centre on the world you're on.</summary>
    public Vector2 QuestHome()
    {
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def != null && def.isTownCenter) return b.cell + (Vector2)def.size * 0.5f;
        }
        return Map != null ? Map.Revealed.center : Vector2.zero;
    }

    /// <summary>A clear walkable spot in explored land at the given distance band from town.</summary>
    private Vector2 QuestPickSpot(int band, System.Random rng)
    {
        var home = QuestHome();
        float lo = band <= 0 ? 22f : band == 1 ? 40f : 65f, hi = band <= 0 ? 45f : band == 1 ? 75f : 130f;
        var zones = Map.RevealedZones();
        var fits = new List<Vector2Int>();
        Vector2Int farthest = default;
        float farD = -1f;
        foreach (var z in zones)
        {
            float d = Vector2.Distance(Map.ZoneRect(z.x, z.y).center, home);
            if (d >= lo && d <= hi) fits.Add(z);
            if (d > farD) { farD = d; farthest = z; }
        }
        if (fits.Count == 0 && farD >= 0) fits.Add(farthest);
        var others = new List<Vector2>();
        foreach (var q in Quests.active) foreach (var s in q.steps) if (s.ready) others.Add(s.spot);
        for (int attempt = 0; attempt < 40 && fits.Count > 0; attempt++)
        {
            var z = fits[rng.Next(fits.Count)];
            var rect = Map.ZoneRect(z.x, z.y);
            int x = rect.x + 2 + rng.Next(Mathf.Max(1, rect.width - 5));
            int y = rect.y + 2 + rng.Next(Mathf.Max(1, rect.height - 5));
            var area = new RectInt(x - 1, y - 1, 3, 3);
            bool clear = true;
            for (int yy = area.yMin; yy < area.yMax && clear; yy++)
                for (int xx = area.xMin; xx < area.xMax && clear; xx++)
                    if (!Map.IsWalkable(xx, yy)) clear = false;
            if (!clear || SiteAt(area) != null || CampAt(area) != null) continue;
            var spot = new Vector2(x + 0.5f, y + 0.5f);
            bool near = false;
            foreach (var o in others) if (Vector2.Distance(o, spot) < 8f) near = true;
            foreach (var b in State.buildings) if (Vector2.Distance(b.cell, spot) < 6f) { near = true; break; }
            if (near && attempt < 30) continue;
            return spot;
        }
        return home + new Vector2(lo, 0);
    }

    /// <summary>The nearest land you haven't set foot in yet (sampled out from town).</summary>
    private bool QuestFindNewLand(out int biome, out Vector2 spot)
    {
        biome = -1;
        spot = default;
        if (Map == null) return false;
        var home = QuestHome();
        for (float r = 30f; r <= 420f; r += 18f)
        {
            for (int a = 0; a < 16; a++)
            {
                float ang = (a + r * 0.37f) * Mathf.PI * 2f / 16f;
                var p = home + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                int b = (int)Map.BiomeAt((int)p.x, (int)p.y);
                if (State.biomesVisited.Contains(b)) continue;
                biome = b;
                spot = p;
                return true;
            }
        }
        return false;
    }

    public static string QuestDirection(Vector2 from, Vector2 to)
    {
        var d = to - from;
        if (d.sqrMagnitude < 1f) return "here";
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
        int i = Mathf.RoundToInt(ang / 45f);
        return names[((i % 8) + 8) % 8];
    }

    /// <summary>All of a quest, for its tooltip.</summary>
    public string QuestTooltip(QuestState q)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{q.title}</b>");
        if (State.worlds.Count > 1) sb.Append($" <color=#9cf>[{QuestWorldName(q)}]</color>");
        sb.Append($"\n<color=#aaa>{q.giver}</color>\n<i>{q.intro}</i>\n");
        for (int i = 0; i < q.steps.Count; i++)
        {
            var s = q.steps[i];
            string mark = s.done ? "<color=#8f8>+</color>" : i == q.step ? "<color=#fd6>></color>" : "<color=#888>-</color>";
            string text = s.done ? $"<color=#9a9>{s.text}</color>" : i == q.step ? $"{s.text} <color=#aaa>{QuestStepProgress(q, s)}</color>" : $"<color=#999>{s.text}</color>";
            sb.Append($"{mark} {text}\n");
        }
        sb.Append($"<color=#8f8>Reward: {QuestRewardText(q)}.</color>");
        return sb.ToString();
    }
}
