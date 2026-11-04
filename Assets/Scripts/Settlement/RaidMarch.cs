using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Raids that come from somewhere. Every raider camp in reach of your towns gathers fighters over
/// time; how fast depends on how much attention you draw (your stores, your people, how close you
/// are, and whether you attacked them), the season and the new-game raid frequency. When a camp is
/// ready, a war party gathers there and marches across the map at one of your towns, arriving in
/// waves. Towers, lookouts, troops, heroes and (later) aircraft and satellites spot it on the way;
/// unscouted parties are only noticed when they're almost there. Burn a camp and its raids stop
/// (its marching party scatters); while no camp is in reach, smaller bands roam in from the wilds.
///
/// The war party's make-up is WaveDirector.Generate's, so raids stay as strong as before.
/// State.secondsUntilWave now holds the estimated time until the next party arrives, so systems
/// that keep clear of raids (disasters, events, quests, hints) still work; anything that changes
/// it from outside is read as "hold raids off a while" or, if it pulls it close, "a raid now".
/// </summary>
public partial class Settlement
{
    /// <summary>Off: the old countdown (WaveDirector) runs instead.</summary>
    private static readonly bool RaidMarchEnabled = true;

    private const int RaidMaxParties = 2;
    private const float RaidArriveMargin = 4f;     // tiles past a town's safe zone: where a war party comes in
    private const float RaidCloseNotice = 16f;     // unscouted parties are noticed this many tiles out
    private const float RaidInterceptReach = 10f;  // troops this close to a marching party bring it to battle
    private const float RaidBreather = 60f;        // no party musters for this long after a raid is over
    private const float RaidLoadGrace = 60f, RaidSwitchGrace = 45f;
    private const float RaidTechReachCap = 40f;

    // Runtime only: the world in view and its calm after loading or switching worlds.
    private int raidLiveWorld = -1;
    private float raidGraceUntil;
    private RaidMarchView raidView;
    private Dictionary<string, UnitDefinition> raidUnitIndex;

    /// <summary>This world's raid threat (camps' moods, marching war parties).</summary>
    public RaidWorld RaidState => CurrentWorld.raid ?? (CurrentWorld.raid = new RaidWorld());

    /// <summary>The world in view (called from WaveDirector.Update). False = run the old countdown.</summary>
    public bool RaidMarchTick(float dt)
    {
        if (!RaidMarchEnabled) return false;
        if (raidLiveWorld != State.currentWorld)
        {
            raidGraceUntil = State.playTime + (raidLiveWorld < 0 ? RaidLoadGrace : RaidSwitchGrace);
            raidLiveWorld = State.currentWorld;
        }
        var c = FreeModeController.Instance;
        if (c != null && raidView == null) raidView = c.gameObject.AddComponent<RaidMarchView>();
        RaidStep(dt, true);
        return true;
    }

    /// <summary>A world in the background (called from TickBackgroundRaids): the same model, no units.</summary>
    private bool RaidMarchBackground(float dt)
    {
        if (!RaidMarchEnabled) return false;
        RaidStep(dt, false);
        return true;
    }

    private bool RaidGrace => !offWorld && State.playTime < raidGraceUntil;

    // ---------- The loop ----------

    private void RaidStep(float dt, bool live)
    {
        var r = RaidState;
        float now = State.playTime;
        if (!r.started) RaidInit(r);

        // Someone else moved the old countdown: a camp burnt (+90 s), an anomaly calling raiders in...
        if (r.lastWritten >= 0f && Mathf.Abs(State.secondsUntilWave - r.lastWritten) > 0.5f)
        {
            float delta = State.secondsUntilWave - r.lastWritten;
            r.lastWritten = State.secondsUntilWave;   // handled once (not again every step until the next estimate)
            RaidOutsideNudge(r, delta, now);
        }

        if (RaidsOff)
        {
            r.parties.Clear();
            RaidWrite(r, 9999f);
            return;
        }
        if (RaidsPaused && r.parties.Count > 0)
        {
            if (live) Notify("The war parties turn back: your tribute holds them off.");
            r.parties.Clear();
        }

        var controller = live ? FreeModeController.Instance : null;
        if (live)
        {
            bool fighting = controller != null && controller.WaveInProgress;
            if (r.wasLive && !fighting) r.holdUntil = Mathf.Max(r.holdUntil, now + RaidBreather);   // a breather after every raid
            r.wasLive = fighting;
        }

        // Camps gather fighters; anger fades.
        if (now >= r.sourcesAt)
        {
            r.sourcesAt = now + 2f;
            RaidRefreshSources(r, now, live);
            RaidMusterCheck(r, now);
        }
        int campSources = 0;
        foreach (var src in r.sources)
        {
            if (src.mood == null) continue;
            campSources++;
            src.mood.ready = Mathf.Min(1f, src.mood.ready + src.rate * dt);
            src.mood.anger = Mathf.Max(0f, src.mood.anger - dt / 600f);
        }
        if (campSources == 0 && r.sources.Count > 0) r.wildsReady = Mathf.Min(1f, r.wildsReady + r.sources[0].rate * dt);
        else r.wildsReady = Mathf.Max(0f, r.wildsReady - dt / 300f);

        // War parties march (a handful at most: cheap every frame).
        foreach (var p in r.parties)
        {
            if (p.gatherLeft > 0f) p.gatherLeft -= dt;
            else p.travelled = Mathf.Min(p.length, p.travelled + p.speed * dt);
        }

        if (now >= r.checkAt || !live)
        {
            r.checkAt = now + 0.5f;
            RaidCheckParties(r, now, controller, live);
        }
        if (now >= r.estimateAt || !live)
        {
            r.estimateAt = now + 0.5f;
            RaidWrite(r, RaidEstimate(r, now));
        }
    }

    /// <summary>First tick of a world (an old save, a new game, a new colony): the first raid keeps to the old countdown.</summary>
    private void RaidInit(RaidWorld r)
    {
        r.started = true;
        float wait = State.secondsUntilWave;
        if (wait <= 0f || wait > 5000f) wait = Database.firstWaveDelay;
        r.nextForcedAt = State.playTime + Mathf.Clamp(wait - 90f, 10f, 1500f);
    }

    private void RaidWrite(RaidWorld r, float seconds)
    {
        State.secondsUntilWave = seconds;
        r.lastWritten = seconds;
    }

    /// <summary>The longest raids can be held off by burning camps or deals.</summary>
    private const float RaidMaxHold = 900f;   // 15 minutes

    private void RaidOutsideNudge(RaidWorld r, float delta, float now)
    {
        if (delta > 0f)
        {
            // Raids hold off a while (a camp burnt, a deal made).
            // (burning camp after camp stacks up, but never past a few minutes: the camps left keep growing)
            float before = r.holdUntil;
            r.holdUntil = Mathf.Min(Mathf.Max(r.holdUntil, now) + delta, now + RaidMaxHold);
            if (r.nextForcedAt > 0f) r.nextForcedAt = Mathf.Min(r.nextForcedAt + Mathf.Max(0f, r.holdUntil - Mathf.Max(before, now)), now + RaidMaxHold + 60f);
        }
        else if (State.secondsUntilWave <= 60f)
        {
            RaidMusterSoon(r, now);   // "raiders are coming": the nearest camp sends a party now
        }
    }

    /// <summary>Best guess at the seconds until the next war party arrives.</summary>
    private float RaidEstimate(RaidWorld r, float now)
    {
        float best = float.MaxValue;
        foreach (var p in r.parties) best = Mathf.Min(best, p.SecondsLeft);
        if (best == float.MaxValue)
        {
            foreach (var src in r.sources)
            {
                float ready = src.mood != null ? src.mood.ready : r.wildsReady;
                if (src.rate <= 0f) continue;
                float t = Mathf.Max(0f, 1f - ready) / src.rate + 30f + src.distance / 0.9f;
                best = Mathf.Min(best, t);
            }
            if (r.nextForcedAt > 0f) best = Mathf.Min(best, Mathf.Max(0f, r.nextForcedAt - now) + 90f);
            if (now < r.holdUntil && best < float.MaxValue) best = Mathf.Max(best, r.holdUntil - now + 60f);
        }
        if (RaidGrace) best = Mathf.Max(best == float.MaxValue ? 0f : best, raidGraceUntil - now);
        return Mathf.Clamp(best == float.MaxValue ? 1800f : best, 5f, 3600f);
    }

    // ---------- Camps and attention ----------

    private RaidCampMood RaidMoodFor(RaidWorld r, CampState camp)
    {
        foreach (var m in r.camps) if (m.cell == camp.cell) return m;
        var mood = new RaidCampMood { cell = camp.cell, ready = Random.Range(0f, 0.35f), wasCleared = camp.cleared };
        r.camps.Add(mood);
        return mood;
    }

    /// <summary>What draws raiders to you anywhere on this world: stores, people and the season.</summary>
    private float RaidCommonAttention(out float wealthTerm)
    {
        float stock = 0f;
        foreach (var res in State.resources) if (res != null) stock += Mathf.Max(0f, res.amount);
        wealthTerm = 0.3f * Mathf.Clamp01(stock / (1500f + 900f * State.ageIndex));
        float people = 0.2f * Mathf.Clamp01(State.population / (30f + 25f * State.ageIndex));
        return 0.65f + wealthTerm + people + RaidSeasonTerm();
    }

    private float RaidSeasonTerm()
    {
        var season = SeasonOf(CurrentWorld);
        if (season == Season.Summer || season == Season.DrySeason) return 0.15f;   // raiding season
        if (season == Season.Winter) return -0.1f;                                  // snow keeps most at home
        return 0f;
    }

    private bool RaidingSeason
    {
        get { var s = SeasonOf(CurrentWorld); return s == Season.Summer || s == Season.DrySeason; }
    }

    private void RaidRefreshSources(RaidWorld r, float now, bool live)
    {
        r.sources.Clear();
        var zones = SettlementZones();
        SettlementZone biggest = null;
        foreach (var z in zones) if (!z.IsOutpost && (biggest == null || z.buildings.Count > biggest.buildings.Count)) biggest = z;
        if (biggest == null) return;   // nothing to raid (only outposts)

        float common = RaidCommonAttention(out _);
        float range = Database.campRaidRange;
        float near = Mathf.Max(1f, range - Database.campMinDistance);
        int n = 0;
        foreach (var camp in State.camps)
        {
            var mood = RaidMoodFor(r, camp);
            if (camp.cleared)
            {
                if (!mood.wasCleared) RaidCampBurnt(r, camp, mood, live);
                continue;
            }
            if (mood.wasCleared)
            {
                // Raiders moved back in, and they remember.
                mood.wasCleared = false;
                mood.ready = 0.2f;
                mood.anger = Mathf.Max(mood.anger, 0.3f);
            }
            SettlementZone target = null;
            float d = float.MaxValue;
            foreach (var z in zones)
            {
                if (z.IsOutpost || !z.Near(camp.Centre, range)) continue;
                float dz = z.DistanceTo(camp.Centre);
                if (dz < d) { d = dz; target = z; }
            }
            if (target == null) continue;
            float attention = common + 0.3f * Mathf.Clamp01((range - d) / near) + 0.6f * mood.anger;
            r.sources.Add(new RaidSource { camp = camp, mood = mood, attention = Mathf.Clamp(attention, 0.4f, 2.2f), distance = d, target = target, at = camp.Centre });
            n++;
        }

        // Fewer camps in reach: fewer raids (burning them really helps); none: roaming bands from the wilds.
        float mean = (Database.waveIntervalRange.x + Database.waveIntervalRange.y) * 0.5f;
        float baseRate = 1f / Mathf.Max(30f, mean * Mathf.Max(0.05f, RaidIntervalFactor));
        if (n == 0)
        {
            r.sources.Add(new RaidSource { camp = null, mood = null, attention = Mathf.Clamp(common, 0.4f, 2f), rate = baseRate * 0.9f * Mathf.Clamp(common, 0.4f, 2f), distance = 45f, target = biggest, at = biggest.centre });
        }
        else
        {
            float share = Mathf.Min(1.25f, 0.75f + 0.2f * n) / n;
            foreach (var src in r.sources) src.rate = baseRate * share * src.attention;
        }

        // Keep the mood list tidy (camps are never removed today, but just in case).
        if (r.camps.Count > State.camps.Count + 4) r.camps.RemoveAll(m => !State.camps.Exists(c => c.cell == m.cell));

        if (live) RaidRallyNotes(r, now);
    }

    /// <summary>Drums at a camp your scouts can see: it's close to sending a war party.</summary>
    private void RaidRallyNotes(RaidWorld r, float now)
    {
        if (now - r.lastRallyNote < 180f) return;
        foreach (var src in r.sources)
        {
            if (src.mood == null || src.mood.rallyNoted || src.mood.ready < 0.8f) continue;
            if (r.parties.Exists(p => p.fromCamp && p.campCell == src.camp.cell)) continue;
            if (!RaidSeen(r, src.camp.Centre, true)) continue;
            src.mood.rallyNoted = true;
            r.lastRallyNote = now;
            string why = src.mood.anger > 0.35f ? " to pay you back" : "";
            Notify($"<color=#ffd27a>War drums</color> at the camp to the {RaidCompass(src.camp.Centre - src.target.centre, false)}: they're gathering fighters{why}.");
            if (!offWorld) GameAudio.UI("camp_war_drums");
            return;
        }
    }

    /// <summary>A camp burnt: its fighters are gone, and a war party still on its way loses heart.</summary>
    private void RaidCampBurnt(RaidWorld r, CampState camp, RaidCampMood mood, bool live)
    {
        mood.wasCleared = true;
        mood.ready = 0f;
        mood.rallyNoted = false;
        for (int i = r.parties.Count - 1; i >= 0; i--)
        {
            var p = r.parties[i];
            if (!p.fromCamp || p.campCell != camp.cell) continue;
            r.parties.RemoveAt(i);
            r.partiesScattered++;
            if (p.spotted || live) { Notify($"With their camp in ashes, the {RaidNoun()} from the {RaidCompass(p.origin - p.target, false)} scatters."); if (!offWorld) GameAudio.UI("war_party_scatter"); }
            XChronicle(XChronicleKind.War, $"{RaidANoun(true)} scattered when its camp was burnt.");
        }
    }

    // ---------- Mustering ----------

    private void RaidMusterCheck(RaidWorld r, float now)
    {
        if (RaidGrace || now < r.holdUntil || RaidsPaused || r.parties.Count >= RaidMaxParties || r.sources.Count == 0) return;
        bool forced = r.nextForcedAt > 0f && now >= r.nextForcedAt;
        RaidSource pick = null;
        float best = float.MinValue;
        foreach (var src in r.sources)
        {
            if (src.camp != null && r.parties.Exists(p => p.fromCamp && p.campCell == src.camp.cell)) continue;   // one party per camp
            float ready = src.mood != null ? src.mood.ready : r.wildsReady;
            if (!forced && ready < 1f) continue;
            float score = forced ? src.attention * (ready + 0.5f) : ready + src.attention * 0.01f;
            if (score > best) { best = score; pick = src; }
        }
        if (pick == null) return;
        if (RaidMuster(r, pick, now, RaidMotive.Plunder) != null && forced) r.nextForcedAt = 0f;
    }

    /// <summary>Something calls raiders in now (an anomaly): the nearest camp sends a party at once.</summary>
    private void RaidMusterSoon(RaidWorld r, float now)
    {
        if (RaidsOff || RaidsPaused || r.parties.Count >= RaidMaxParties) return;
        if (r.sources.Count == 0) RaidRefreshSources(r, now, false);
        RaidSource pick = null;
        foreach (var src in r.sources)
        {
            if (src.camp != null && r.parties.Exists(p => p.fromCamp && p.campCell == src.camp.cell)) continue;
            if (pick == null || src.distance < pick.distance) pick = src;
        }
        if (pick == null) return;
        var party = RaidMuster(r, pick, now, RaidMotive.Sent);
        if (party == null) return;
        party.gatherLeft = Mathf.Min(party.gatherLeft, 3f);
        party.travelled = Mathf.Max(party.travelled, party.length - 25f);
    }

    private RaidParty RaidMuster(RaidWorld r, RaidSource src, float now, RaidMotive asked)
    {
        var wave = Waves != null ? Waves.Generate() : null;
        if (wave == null || wave.units.Count == 0) return null;   // peaceful game, tribute paid, or nothing to send

        var mood = src.mood;
        float k = src.camp != null ? 0.9f + Mathf.Min(0.3f, 0.1f * src.camp.extraTier) + 0.25f * mood.anger : 0.8f;
        if (RaidingSeason) k += 0.1f;
        RaidScaleWave(wave, Mathf.Clamp(k, 0.6f, 1.4f));

        var town = src.target;
        Vector2 origin;
        if (src.camp != null) origin = src.camp.Centre;
        else
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = 0.5f * Mathf.Max(town.bounds.width, town.bounds.height);
            origin = town.centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + town.safeDistance + Random.Range(30f, 50f));
        }
        Vector2 target = RaidClosestBuildingPoint(town, origin);
        float dist = Vector2.Distance(origin, target);

        RaidCommonAttention(out float wealthTerm);
        RaidMotive motive = asked;
        if (motive != RaidMotive.Sent)
        {
            if (wave.boss != null) motive = RaidMotive.Chief;
            else if (mood != null && mood.anger >= 0.35f) motive = RaidMotive.Vengeance;
            else if (RaidingSeason) motive = RaidMotive.Season;
            else if (SeasonOf(CurrentWorld) == Season.Winter) motive = RaidMotive.Hunger;
            else if (wealthTerm >= 0.2f) motive = RaidMotive.Wealth;
            else if (src.camp == null) motive = RaidMotive.Wilds;
        }

        var p = new RaidParty
        {
            id = r.nextPartyId++,
            fromCamp = src.camp != null,
            campCell = src.camp != null ? src.camp.cell : default,
            origin = origin,
            target = target,
            length = Mathf.Max(0f, dist - town.safeDistance - RaidArriveMargin),
            speed = Mathf.Min(1.25f, 0.75f + 0.03f * State.ageIndex) * (SeasonOf(CurrentWorld) == Season.Winter ? 0.85f : 1f),
            gatherLeft = src.camp != null ? Random.Range(20f, 40f) : 0f,
            boss = wave.boss != null ? wave.boss.name : null,
            strength = wave.strength,
            motive = (int)motive,
            groups = wave.units.Count >= 16 ? 3 : wave.units.Count >= 8 ? 2 : 1,
            setOut = now,
            dir = RaidCompass(origin - town.centre, true),
            town = RaidTownName(town),
        };
        p.from = src.camp != null ? $"the camp to the {RaidCompass(origin - town.centre, false)}" : $"the wilds to the {RaidCompass(origin - town.centre, false)}";
        foreach (var u in wave.units) if (u != null) p.units.Add(u.name);
        r.parties.Add(p);
        r.partiesSent++;

        if (mood != null)
        {
            mood.ready = Random.Range(-0.2f, 0.05f);
            mood.anger *= 0.5f;   // they've had their say
            mood.sent++;
            mood.rallyNoted = false;
        }
        else r.wildsReady = Random.Range(-0.2f, 0.05f);

        if (wave.boss != null || wave.units.Count >= 14)
            XChronicle(XChronicleKind.War, wave.boss != null
                ? $"{wave.boss.displayName} rose among the raiders and led {RaidANoun()} out of {p.from}."
                : $"A great {RaidNoun()} of {wave.units.Count} set out from {p.from}.");

        if (r.globalSight || RaidSeen(r, origin, !offWorld)) RaidSpot(r, p);
        return p;
    }

    /// <summary>Make a wave bigger or smaller (keeping its chief), as a camp's strength and anger call for.</summary>
    private void RaidScaleWave(Wave wave, float k)
    {
        int count = wave.units.Count;
        int want = Mathf.Max(1, Mathf.RoundToInt(count * k));
        if (want < count) wave.units.RemoveRange(want, count - want);
        else if (want > count)
        {
            var extra = Waves.Generate();
            foreach (var u in extra.units)
            {
                if (wave.units.Count >= want) break;
                if (u != null && u != extra.boss) wave.units.Add(u);
            }
        }
        wave.strength = 0f;
        foreach (var u in wave.units) if (u != null) wave.strength += UnitPower(u);
    }

    private static Vector2 RaidClosestBuildingPoint(SettlementZone zone, Vector2 from)
    {
        Vector2 best = zone.centre;
        float bestD = float.MaxValue;
        foreach (var rect in zone.buildings)
        {
            var q = new Vector2(Mathf.Clamp(from.x, rect.xMin, rect.xMax), Mathf.Clamp(from.y, rect.yMin, rect.yMax));
            float d = (q - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = q; }
        }
        return best;
    }

    // ---------- Scouting ----------

    private static readonly (string id, float reach)[] RaidScoutBuildings =
    {
        ("LookoutPlatform", 22f), ("DisFireWatch", 18f), ("SeaBeacon", 12f), ("SeaLighthouse", 16f), ("SeaLampLighthouse", 20f),
        ("GrandObservatory", 30f), ("AirshipMast", 30f), ("Airfield", 40f), ("SkyshipDock", 40f), ("DroneHangar", 50f),
        ("OrbitalUplink", 60f), ("StrikeUplink", 60f),
    };

    private static readonly (string id, float reach)[] RaidScoutTechs =
    {
        ("Lookouts", 6f), ("HorsebackRiding", 5f), ("Astronomy", 3f), ("Farsight", 6f), ("Scrying", 5f), ("Navigation", 3f),
        ("Radio", 8f), ("Aviation", 6f), ("Drones", 6f), ("Internet", 4f), ("RiftSight", 6f), ("SolarSight", 5f), ("Omniscience", 8f),
    };

    private void RaidRefreshLookouts(RaidWorld r)
    {
        r.lookouts.Clear();
        float tech = 0f;
        foreach (var t in RaidScoutTechs) if (Researched(t.id)) tech += t.reach;
        r.techReach = Mathf.Min(RaidTechReachCap, tech);
        r.globalSight = Researched("Satellites");
        r.towers = 0;
        r.bestReach = 0f;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            float extra = 0f;
            foreach (var s in RaidScoutBuildings) if (s.id == def.id) { extra = s.reach; break; }
            float towerRange = def.towerRange > 0 ? StatsOf(def, b.upgradeLevel).towerRange : 0f;
            if (towerRange <= 0f && extra <= 0f) continue;
            if (def.id == "OrbitalUplink" || def.id == "StrikeUplink") r.globalSight = true;   // eyes in orbit
            float reach = (towerRange > 0f ? 18f + towerRange * 1.5f : 12f) + extra + r.techReach;
            r.lookouts.Add(new RaidLookout { at = (Vector2)b.cell + (Vector2)def.size * 0.5f, reach = reach });
            r.towers++;
            r.bestReach = Mathf.Max(r.bestReach, reach);
        }
    }

    /// <summary>Can your towers, towns, troops or heroes see this spot?</summary>
    private bool RaidSeen(RaidWorld r, Vector2 pos, bool withUnits)
    {
        if (r.globalSight) return true;
        foreach (var lk in r.lookouts) if ((lk.at - pos).sqrMagnitude <= lk.reach * lk.reach) return true;
        foreach (var z in SettlementZones())
            if (z.Near(pos, z.safeDistance + 8f + r.techReach * 0.5f)) return true;   // the town's own people keep watch
        if (withUnits)
        {
            var living = Unit.Living;
            for (int i = 0; i < living.Count; i++)
            {
                if (!(living[i] is Troop t) || !t.IsAlive) continue;
                float reach = t.Hero != null ? 24f + r.techReach * 0.5f : 12f;
                if (((Vector2)t.transform.position - pos).sqrMagnitude <= reach * reach) return true;
            }
        }
        return false;
    }

    private static bool RaidTroopNear(Vector2 pos)
    {
        var living = Unit.Living;
        for (int i = 0; i < living.Count; i++)
        {
            if (living[i] is Troop t && t.IsAlive && ((Vector2)t.transform.position - pos).sqrMagnitude <= RaidInterceptReach * RaidInterceptReach) return true;
        }
        return false;
    }

    private static bool RaidOnScreen(FreeModeController c, Vector2 pos)
    {
        var cam = Camera.main;
        if (cam == null || c.Grid == null || c.Grid.CurrentDetail != SettlementGrid.Detail.Tiles) return false;
        float h = cam.orthographicSize + 2f, w = cam.orthographicSize * cam.aspect + 2f;
        Vector3 at = cam.transform.position;
        return Mathf.Abs(pos.x - at.x) <= w && Mathf.Abs(pos.y - at.y) <= h;
    }

    /// <summary>Your scouts see a war party: warn once.</summary>
    private void RaidSpot(RaidWorld r, RaidParty p)
    {
        if (p.spotted) return;
        p.spotted = true;
        if (offWorld) return;   // news from other worlds comes when the raid does
        string lead = p.boss != null && RaidUnit(p.boss) is UnitDefinition b ? $", led by <color=#ff8a70>{b.displayName}</color>," : "";
        string doing = p.gatherLeft > 0f ? "gathering at" : "marching from";
        Notify($"<color=#ffa060>Scouts spot {RaidANoun()}</color> of about {RaidRound(p.units.Count)}{lead} {doing} {p.from}, bound for {p.town} (~{RaidEta(p.SecondsLeft)}). {RaidWhy(p)}");
        GameAudio.UI("raid_party_spotted");
    }

    // ---------- Arrival ----------

    private void RaidCheckParties(RaidWorld r, float now, FreeModeController c, bool live)
    {
        if (now >= r.lookoutsAt)
        {
            r.lookoutsAt = now + 5f;
            RaidRefreshLookouts(r);
        }
        bool grace = RaidGrace;
        for (int i = r.parties.Count - 1; i >= 0; i--)
        {
            var p = r.parties[i];
            Vector2 pos = p.Position;
            if (!p.spotted)
            {
                if (RaidSeen(r, pos, live)) RaidSpot(r, p);
                else if (p.gatherLeft <= 0f && p.length - p.travelled <= RaidCloseNotice) p.spotted = true;   // seen at last, almost upon you
            }
            bool arrived = p.gatherLeft <= 0f && p.travelled >= p.length;
            if (live && c != null && c.isActiveAndEnabled)
            {
                // Real raiders from here on: when they reach the town, when troops meet them, or when you watch them come.
                bool met = p.gatherLeft <= 0f && (RaidTroopNear(pos) || RaidOnScreen(c, pos));
                if (((arrived && !grace) || met) && c.CanTakeWarParty)
                {
                    r.parties.RemoveAt(i);
                    RaidLand(p, c);
                }
            }
            else if (arrived && !grace)
            {
                r.parties.RemoveAt(i);
                RaidResolveUnseen(p);
            }
        }
    }

    private Wave RaidWaveOf(RaidParty p)
    {
        var wave = new Wave();
        var boss = p.boss != null ? RaidUnit(p.boss) : null;
        bool bossAdded = false;
        foreach (var id in p.units)
        {
            var u = RaidUnit(id);
            if (u == null) continue;
            if (u == boss && !bossAdded) { bossAdded = true; continue; }
            wave.units.Add(u);
            wave.strength += UnitPower(u);
        }
        if (boss != null && bossAdded)
        {
            wave.boss = boss;
            wave.units.Insert(0, boss);
            wave.strength += UnitPower(boss);
        }
        return wave;
    }

    /// <summary>Find a unit type by asset name (enemies and chiefs of every age, and native peoples).</summary>
    private UnitDefinition RaidUnit(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (raidUnitIndex == null)
        {
            raidUnitIndex = new Dictionary<string, UnitDefinition>();
            foreach (var age in Database.ages)
            {
                if (age == null) continue;
                foreach (var u in age.enemyUnits) if (u != null) raidUnitIndex[u.name] = u;
                foreach (var u in age.bosses) if (u != null) raidUnitIndex[u.name] = u;
            }
            foreach (var u in Database.natives) if (u != null) raidUnitIndex[u.name] = u;
        }
        if (raidUnitIndex.TryGetValue(id, out var found)) return found;
        foreach (var u in NativeUnits()) if (u != null && u.name == id) return raidUnitIndex[id] = u;
        var chief = NativeChief();
        if (chief != null && chief.name == id) return raidUnitIndex[id] = chief;
        return null;
    }

    /// <summary>The war party comes on in the flesh, where it is now.</summary>
    private void RaidLand(RaidParty p, FreeModeController c)
    {
        var wave = RaidWaveOf(p);
        if (wave.units.Count == 0) return;
        CampState camp = p.fromCamp ? State.camps.Find(x => x.cell == p.campCell && !x.cleared) : null;
        c.LandWarParty(wave, RaidLandingSpot(p, c), camp, p.groups);
    }

    /// <summary>Open, walkable ground near the party, outside every safe zone.</summary>
    private Vector3 RaidLandingSpot(RaidParty p, FreeModeController c)
    {
        var zones = SettlementZones();
        Vector2 pos = p.Position;
        Vector2 back = (p.origin - p.target).normalized;
        for (float step = 0f; step <= 30f; step += 3f)
        {
            Vector2 centre = pos + back * step;
            for (int ring = 0; ring < 3; ring++)
            {
                for (int a = 0; a < 8; a++)
                {
                    float angle = a * Mathf.PI / 4f + ring * 0.4f;
                    Vector2 q = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * 1.5f);
                    if (Map.IsWalkable(q) && !InAnyZone(zones, q)) return q;
                    if (ring == 0) break;
                }
            }
        }
        return c.RaidSpawnPoint();
    }

    /// <summary>A raid nobody is watching: settled by strength, like before.</summary>
    private void RaidResolveUnseen(RaidParty p)
    {
        var wave = RaidWaveOf(p);
        if (wave.units.Count == 0 || Waves == null) return;
        float severity = Waves.AutoResolve(wave);
        GameAudio.UI("river_raid_horn", 0.5f);
        string who = $"{RaidANoun(true)} from {p.from}";
        Notify(severity <= 0
            ? $"{who} attacked {p.town} and was driven off ({wave.units.Count} of them)."
            : severity < 0.5f
                ? $"{who} broke through at {p.town} and did some damage before it was driven off."
                : $"{who} overran the defences of {p.town}! Heavy damage and losses.");
    }

    // ---------- Words ----------

    private static readonly string[] RaidNouns =
    {
        "war band", "war band", "war band", "raiding column", "warband", "raiding company", "raiding company",
        "raiding column", "strike force", "strike force", "strike team", "war host", "war host",
        "assault group", "assault group", "assault group", "assault group",
    };

    public string RaidNoun() => RaidNouns[Mathf.Clamp(State.ageIndex, 0, RaidNouns.Length - 1)];

    /// <summary>"a war band" / "an assault group".</summary>
    public string RaidANoun(bool capital = false)
    {
        string noun = RaidNoun();
        bool vowel = noun.Length > 0 && "aeiouAEIOU".IndexOf(noun[0]) >= 0;
        return (capital ? (vowel ? "An " : "A ") : (vowel ? "an " : "a ")) + noun;
    }

    private string RaidWhy(RaidParty p)
    {
        switch ((RaidMotive)p.motive)
        {
            case RaidMotive.Vengeance: return "They want revenge for the attack on their camp.";
            case RaidMotive.Chief: return "A chief has risen among them.";
            case RaidMotive.Season: return "It's raiding season.";
            case RaidMotive.Hunger: return "Winter has left them hungry.";
            case RaidMotive.Wealth: return "Word of your full storehouses has spread.";
            case RaidMotive.Wilds: return "Roaming raiders, with no camp near to hold them.";
            case RaidMotive.Sent: return "Something has stirred them up.";
            default: return "They're after plunder.";
        }
    }

    private static int RaidRound(int n) => n < 6 ? n : Mathf.RoundToInt(n / 5f) * 5;

    public static string RaidEta(float seconds)
    {
        if (seconds < 60f) return $"{Mathf.Max(10, Mathf.CeilToInt(seconds / 10f) * 10)}s";
        return $"{Mathf.RoundToInt(seconds / 60f)} min";
    }

    private static readonly string[] CompassLong = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
    private static readonly string[] CompassShort = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

    private static string RaidCompass(Vector2 dir, bool shortName)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt(((angle % 360) + 360) % 360 / 45f) % 8;
        return shortName ? CompassShort[index] : CompassLong[index];
    }

    private string RaidTownName(SettlementZone town)
    {
        var zones = SettlementZones();
        int towns = 0;
        foreach (var z in zones) if (!z.IsOutpost) towns++;
        if (towns <= 1) return "your town";
        SettlementZone capital = null;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !def.isTownCenter) continue;
            capital = NearestZone(zones, (Vector2)b.cell + (Vector2)def.size * 0.5f, true);
            break;
        }
        if (capital == null || capital == town) return "your capital";
        return $"your {RaidCompass(town.centre - capital.centre, false)} town";
    }

    // ---------- For the HUD, camps and the map ----------

    /// <summary>The top bar's raid chip: Quiet / Restless camps / War party NE ~1 min / Under attack.</summary>
    public string RaidChipText(bool fighting)
    {
        if (RaidsOff) return "<b>Peaceful</b>";
        if (fighting) return "<b><color=#ff6b5b>Under attack</color></b>";
        var r = RaidState;
        RaidParty next = null;
        foreach (var p in r.parties) if (p.spotted && (next == null || p.SecondsLeft < next.SecondsLeft)) next = p;
        if (next != null)
            return $"<b><color=#ffa060>{(next.gatherLeft > 0f ? "Rallying" : "War party")}</color></b> <size=75%>{next.dir} ~{RaidEta(next.SecondsLeft)}</size>";
        if (RaidsPaused) return "<b><color=#9cf>Bought off</color></b>";
        float top = 0f;
        bool camps = false;
        foreach (var src in r.sources)
        {
            if (src.mood != null) { camps = true; top = Mathf.Max(top, src.mood.ready + src.mood.anger * 0.5f); }
            else top = Mathf.Max(top, r.wildsReady);
        }
        if (top >= 0.6f) return camps ? "<b><color=#ffd27a>Restless camps</color></b>" : "<b><color=#ffd27a>Restless wilds</color></b>";
        return "<b><color=#9fd59a>Quiet</color></b>";
    }

    private static string RaidMoodWord(float ready) =>
        ready < 0.35f ? "quiet" : ready < 0.6f ? "stirring" : ready < 0.85f ? "restless" : "rallying";

    /// <summary>The raid chip's tooltip: spotted war parties, every camp in reach, and your scouting.</summary>
    public string RaidTooltip()
    {
        var sb = new StringBuilder();
        sb.Append("<b>Raid threat</b>\n");
        if (RaidsOff) return sb.Append("A peaceful game: no raids.").ToString();
        var r = RaidState;
        float now = State.playTime;
        if (RaidsPaused) sb.Append("<color=#9cf>Your tribute holds the raiders off for now.</color>\n");

        int unseen = 0;
        foreach (var p in r.parties)
        {
            if (!p.spotted) { unseen++; continue; }
            var boss = p.boss != null ? RaidUnit(p.boss) : null;
            sb.Append($"<color=#ffa060>{char.ToUpper(RaidNoun()[0])}{RaidNoun().Substring(1)}</color> of ~{RaidRound(p.units.Count)}");
            if (boss != null) sb.Append($", led by <color=#ff8a70>{boss.displayName}</color>");
            sb.Append(p.gatherLeft > 0f ? $"\n  gathering at {p.from}, sets out in {RaidEta(p.gatherLeft)}" : $"\n  from {p.from}");
            sb.Append($" → {p.town}, ~{RaidEta(p.SecondsLeft)}\n  <size=85%><color=#ccc>{RaidWhy(p)}</color></size>\n");
        }
        if (r.parties.Count == unseen) sb.Append("No war parties in sight.\n");

        int camps = 0;
        foreach (var src in r.sources) if (src.mood != null) camps++;
        if (camps > 0)
        {
            sb.Append($"\n<b>Camps in reach</b> ({camps})\n");
            var list = new List<RaidSource>();
            foreach (var src in r.sources) if (src.mood != null) list.Add(src);
            list.Sort((a, b) => b.mood.ready.CompareTo(a.mood.ready));
            for (int i = 0; i < list.Count && i < 5; i++)
            {
                var src = list[i];
                string age = Database.GetAge(CampTier(src.camp))?.displayName ?? "";
                string word = RaidMoodWord(src.mood.ready);
                string col = src.mood.ready >= 0.85f ? "#ff9a6a" : src.mood.ready >= 0.6f ? "#ffd27a" : "#b8c8b0";
                sb.Append($"{RaidCompass(src.at - src.target.centre, true)} camp, {Mathf.RoundToInt(src.distance)} tiles, {age}: <color={col}>{word}</color> {Mathf.RoundToInt(Mathf.Clamp01(src.mood.ready) * 100f)}%");
                if (src.mood.anger > 0.2f) sb.Append(" <color=#ff8a70>angry</color>");
                sb.Append('\n');
            }
            if (list.Count > 5) sb.Append($"<size=85%><color=#aaa>...and {list.Count - 5} more.</color></size>\n");
            sb.Append("<size=85%><color=#aaa>Burn a camp and its raids stop (it may be re-taken later, stronger). Attacking one without burning it makes it angry.</color></size>\n");
        }
        else if (r.sources.Count > 0)
            sb.Append($"\nNo camps in reach: small bands roam in from the wilds (<color=#ffd27a>{RaidMoodWord(r.wildsReady)}</color>).\n");

        sb.Append("\n<b>What draws them</b>: ");
        float common = RaidCommonAttention(out float wealthTerm);
        var why = new List<string>();
        if (wealthTerm >= 0.15f) why.Add("full storehouses");
        if (State.population >= 30f + 25f * State.ageIndex * 0.6f) why.Add("many people");
        if (RaidingSeason) why.Add("raiding season");
        if (SeasonOf(CurrentWorld) == Season.Winter) why.Add("winter slows them");
        sb.Append(why.Count > 0 ? string.Join(", ", why) : "not much").Append($" <color=#aaa>(x{common:0.00})</color>\n");

        sb.Append("<b>Scouting</b>: ");
        if (r.globalSight) sb.Append("<color=#8f8>eyes in orbit see every war party.</color>\n");
        else
        {
            sb.Append(r.towers > 0 ? $"{r.towers} towers and lookouts, seeing up to {Mathf.RoundToInt(r.bestReach)} tiles out" : "only your towns' own people");
            if (r.techReach > 0f) sb.Append($" (+{Mathf.RoundToInt(r.techReach)} from research)");
            sb.Append(".\n<size=85%><color=#aaa>Towers, lookouts, heroes, troops, and later aircraft and satellites spot war parties sooner; unscouted ones are seen only when close.</color></size>\n");
        }
        if (now < r.holdUntil) sb.Append($"<color=#9cf>Raids hold off for about {RaidEta(r.holdUntil - now)}.</color>\n");
        sb.Append("<size=85%><color=#aaa>Click: show the next war party, or the most restless camp. Troops that meet a war party on its way fight it there.</color></size>");
        return sb.ToString();
    }

    /// <summary>Somewhere to look (the raid chip's click): spotted war parties first, then camps by mood.</summary>
    public bool RaidFocusPoint(int index, out Vector2 point)
    {
        var r = RaidState;
        var spots = new List<Vector2>();
        foreach (var p in r.parties) if (p.spotted) spots.Add(p.Position);
        var list = new List<RaidSource>();
        foreach (var src in r.sources) if (src.mood != null) list.Add(src);
        list.Sort((a, b) => b.mood.ready.CompareTo(a.mood.ready));
        foreach (var src in list) spots.Add(src.at);
        point = default;
        if (spots.Count == 0) return false;
        point = spots[((index % spots.Count) + spots.Count) % spots.Count];
        return true;
    }

    /// <summary>A line for a camp's hover label: how restless it is.</summary>
    public string RaidCampHoverNote(CampState camp)
    {
        if (camp == null || camp.cleared || RaidsOff) return "";
        var r = RaidState;
        RaidCampMood mood = null;
        foreach (var m in r.camps) if (m.cell == camp.cell) { mood = m; break; }
        if (mood == null) return "";
        bool inReach = false;
        foreach (var src in r.sources) if (src.camp == camp) { inReach = true; break; }
        if (!inReach) return "\n<size=65%><color=#aaa>Too far from your towns to raid them.</color></size>";
        string word = RaidMoodWord(mood.ready);
        return $"\n<size=65%>{char.ToUpper(word[0])}{word.Substring(1)}: fighters {Mathf.RoundToInt(Mathf.Clamp01(mood.ready) * 100f)}%{(mood.anger > 0.2f ? ", <color=#ff8a70>angry</color>" : "")}</size>";
    }

    /// <summary>Troops hit a camp: it gets angry, and loses some of the fighters it was gathering.</summary>
    public void RaidCampHit(CampState camp, float fraction)
    {
        if (camp == null || !RaidMarchEnabled) return;
        var mood = RaidMoodFor(RaidState, camp);
        mood.anger = Mathf.Min(1f, mood.anger + 0.04f + fraction * 1.2f);
        mood.ready = Mathf.Max(0f, mood.ready - fraction * 0.5f);
    }

    /// <summary>A camp's guards were killed: fewer fighters to send.</summary>
    public void RaidCampLosses(CampState camp, int lost)
    {
        if (camp == null || lost <= 0 || !RaidMarchEnabled) return;
        var mood = RaidMoodFor(RaidState, camp);
        mood.ready = Mathf.Max(0f, mood.ready - 0.06f * lost);
        mood.anger = Mathf.Min(1f, mood.anger + 0.05f * lost);
    }
}
