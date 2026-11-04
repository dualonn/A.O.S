using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One unit's war state (not saved: troops are trained again): its experience and rank, its
/// morale, whether it's routing, and the commander's aura on it. Lives on Unit.War.
/// </summary>
public class WarMind
{
    public float xp;
    public int rank;
    public int kills;
    public int raidKills, raidStamp;
    public int regiment;               // the training building's instance id (0 = none)
    public bool started;

    public float morale = -1f;         // -1: not worked out yet
    public bool routing;
    public float routUntil, noBreakUntil;
    public Vector3 fleeTo;
    public int routs;

    public UnitStatModifier raw;       // the last modifier the unit was given (building, war cry...)
    public readonly UnitStatModifier combined = new UnitStatModifier();
    public float auraHp = 1f, auraAtk = 1f, auraSpd = 1f, auraCd = 1f;
    public float breakShift;
    public bool noBreak, inAura;
    public int appliedKey = int.MinValue;

    public WarCat cat;
    public bool catKnown;
    public int bossFlag;               // 0: not looked yet, 1: no, 2: a raider chief
    public SpriteRenderer pip, banner;
    public int pipRank;

    /// <summary>While routing: run for it instead of fighting (true = handled this frame).</summary>
    public bool Flee(Unit u)
    {
        if (!routing || u == null) return false;
        u.WarStep(fleeTo, u.moveSpeed * 1.15f);
        return true;
    }

    public float MoraleAttack => morale < 0f ? 1f : morale < WarCatalog.WaverAt ? 0.9f : morale >= WarCatalog.InspiredAt ? 1.08f : 1f;
}

/// <summary>
/// The battlefield side of warfare, on the world you're watching: ranks and their pips, morale
/// (worked out a couple of times a second, not every frame), routs and rallies, the commander's
/// aura, siege engines joining raids, and the battle report when a raid is over.
/// </summary>
public class WarField : MonoBehaviour
{
    private static WarField instance;
    private static readonly UnitStatModifier Identity = new UnitStatModifier();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        subscribed = null;
        tracking = false;
        raidId = 0;
        report = new RaidTally();
        pendingRaiders = pendingSiege = 0;
        pendingBoss = null;
        lastCampsCleared = -1;
        pipSprites.Clear();
        bannerSprites.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("WarField");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WarField>();
    }

    // ---------- Hooks called from Unit ----------

    /// <summary>A unit's stats are being set (Unit.SetBuildingModifier): add its rank, morale and the commander's aura.</summary>
    public static UnitStatModifier Adjust(Unit u, UnitStatModifier mod)
    {
        if (u == null || mod == null) return mod;
        var w = Mind(u);
        w.raw = mod;
        float hp = WarCatalog.RankHealth[w.rank] * w.auraHp;
        float atk = WarCatalog.RankAttack[w.rank] * w.auraAtk * w.MoraleAttack;
        float spd = w.auraSpd, cd = w.auraCd;
        if (Mathf.Approximately(hp, 1f) && Mathf.Approximately(atk, 1f) && Mathf.Approximately(spd, 1f) && Mathf.Approximately(cd, 1f)) return mod;
        var c = w.combined;
        c.troopClass = null;
        c.healthMultiplier = mod.healthMultiplier * hp;
        c.attackMultiplier = mod.attackMultiplier * atk;
        c.speedMultiplier = mod.speedMultiplier * spd;
        c.cooldownMultiplier = mod.cooldownMultiplier * cd;
        c.rangeMultiplier = mod.rangeMultiplier;
        return c;
    }

    /// <summary>A hit landed (Unit.TakeDamage): who's in the fight, and the fear of big guns.</summary>
    public static void Hit(Unit victim, Unit attacker, int dealt)
    {
        var vw = victim != null ? victim.War : null;
        if (vw == null) return;
        if (tracking) vw.raidStamp = raidId;
        if (attacker != null)
        {
            var aw = attacker.War;
            if (aw != null && tracking) aw.raidStamp = raidId;
            var def = attacker.definition;
            if (vw.morale > 0f && ((def != null && def.splashRadius >= 1f) || attacker is SiegeEngine || attacker is SiegeTroop))
                vw.morale -= 3f;
        }
        else if (vw.morale > 0f && dealt >= victim.maxHealth / 4)
        {
            vw.morale -= 4f;   // a tower's heavy shot, an orbital strike
        }
    }

    /// <summary>A unit died (Unit.TakeDamage): the shock to its side, the killer's experience, the tallies.</summary>
    public static void Died(Unit victim, Unit attacker)
    {
        if (victim == null) return;
        var s = Settlement.Instance;
        bool important = (victim is Troop vt && vt.Hero != null) || victim.GetComponent<Boss>() != null;
        bool commander = victim is Troop ct && ct.Hero != null && s != null && s.IsCommander(ct.Hero);
        float radius = commander ? 14f : important ? 8f : 5f;
        float shock = commander ? 25f : important ? 18f : 7f;
        Team side = victim.Team;
        Vector3 at = victim.transform.position;
        var living = Unit.Living;
        for (int i = 0; i < living.Count; i++)
        {
            var u = living[i];
            if (u == null || u == victim || !u.IsAlive || u.War == null || u.War.morale < 0f) continue;
            float d = ((Vector2)(u.transform.position - at)).magnitude;
            if (d > radius) continue;
            if (u.Team == side) u.War.morale -= shock * (1f - 0.5f * d / radius);
            else u.War.morale += important ? 8f : 1.5f;
        }

        if (s == null) return;
        // Our side's losses.
        if (victim is Troop troop)
        {
            if (tracking)
            {
                report.lost++;
                if (troop.Hero != null) report.heroesDown++;
            }
            int reg = victim.War != null ? victim.War.regiment : 0;
            if (reg != 0) { var r = s.Regiment(reg, false); if (r != null) r.fallen++; }
        }
        // A raider down.
        if (victim is Enemy enemy)
        {
            if (tracking && enemy.InLiveWave) report.slain++;
            if (attacker is Troop killer && killer.IsAlive) Credit(s, killer, enemy, important);
        }
    }

    /// <summary>A boss roars (war cry): your troops nearby lose heart, raiders take it.</summary>
    public static void Roar(Vector3 at, float radius)
    {
        var living = Unit.Living;
        for (int i = 0; i < living.Count; i++)
        {
            var u = living[i];
            if (u == null || !u.IsAlive || u.War == null || u.War.morale < 0f) continue;
            if (((Vector2)(u.transform.position - at)).sqrMagnitude > radius * radius) continue;
            u.War.morale += u.Team == Team.Player ? -15f : 10f;
        }
    }

    // ---------- Minds, ranks and pips ----------

    public static WarMind Mind(Unit u)
    {
        var w = u.War;
        if (w == null) { w = new WarMind(); u.War = w; }
        if (!w.started) Begin(u, w);
        return w;
    }

    private static void Begin(Unit u, WarMind w)
    {
        w.started = true;
        var s = Settlement.Instance;
        if (u is Troop t && t.Hero == null && t.Home != null && t.Home.State != null && s != null)
        {
            w.regiment = t.Home.State.instanceId;
            int start = s.StartRank(w.regiment);
            if (start > 0)
            {
                w.rank = start;
                w.xp = WarCatalog.TroopXp[start];
                ShowPip(u, w);
            }
        }
    }

    public static WarCat CategoryOf(Unit u)
    {
        var w = u.War;
        if (w != null && w.catKnown) return w.cat;
        var c = Categorise(u);
        if (w != null) { w.cat = c; w.catKnown = true; }
        return c;
    }

    private static WarCat Categorise(Unit u)
    {
        var def = u.definition;
        if (u is SiegeTroop || u is SiegeEngine) return WarCat.Siege;
        if (def == null) return WarCat.Melee;
        if (def.healer) return WarCat.Healer;
        if (def.movement == Movement.Air) return WarCat.Air;
        if (def.movement == Movement.Water) return WarCat.Naval;
        var v = u.GetComponent<SpriteUnitVisual>();
        if (v != null && v.figure != null)
        {
            if (v.figure.beast != Beast.None) return WarCat.Beast;
            var m = v.figure.mount;
            if (m == MountKind.Cannon || m == MountKind.Crawler || m == MountKind.Tank || m == MountKind.ArmouredCar || m == MountKind.HoverTank || m == MountKind.Walker)
                return WarCat.Guns;
            if (m != MountKind.None) return WarCat.Cavalry;
            if (v.weaponClass == WeaponClass.Cannon || v.weaponClass == WeaponClass.Musket) return WarCat.Guns;
        }
        return def.projectile != null ? WarCat.Ranged : WarCat.Melee;
    }

    /// <summary>The killer learns from it; the commander's tallies grow.</summary>
    private static void Credit(Settlement s, Troop killer, Enemy victim, bool important)
    {
        var w = Mind(killer);
        w.kills++;
        if (tracking)
        {
            w.raidStamp = raidId;
            w.raidKills++;
            if (w.raidKills > report.bestKills)
            {
                report.bestKills = w.raidKills;
                report.best = Describe(s, killer, w);
            }
        }
        float mine = Mathf.Max(0.3f, Settlement.UnitPower(killer.definition));
        float theirs = Settlement.UnitPower(victim.definition);
        float xp = Mathf.Clamp(8f * theirs / mine, 2f, 30f) * (important ? 3f : 1f);
        GainXp(s, killer, w, xp, 1);
        var cat = CategoryOf(killer);
        s.WarCount(WarCounter.Kills);
        s.WarCount(WarCatalog.KillCounter(cat));
        if (important) s.WarCount(WarCounter.BossKills);
        if (victim.definition != null && victim.definition.isNative) s.WarCount(WarCounter.NativeKills);
        if (tracking && s.State != null && InTown(killer.transform.position)) report.inTown = true;
    }

    private static string Describe(Settlement s, Troop t, WarMind w)
    {
        if (t.Hero != null) return t.Hero.name;
        string name = t.definition != null ? t.definition.displayName : "a troop";
        string rank = w.rank > 0 ? $"{WarCatalog.RankName(w.rank)} " : "";
        string home = t.Home != null && t.Home.Definition != null ? $" of the {t.Home.Definition.displayName}" : "";
        return $"a {rank}{name}{home}";
    }

    /// <summary>Experience for a troop (and its regiment). Heroes level up their own way.</summary>
    public static void GainXp(Settlement s, Unit u, WarMind w, float xp, int kills)
    {
        if (!(u is Troop t) || t.Hero != null || s == null) return;
        xp *= s.WarXpFactor(w.regiment);
        w.xp += xp;
        s.WarRegimentGain(w.regiment, xp, kills);
        int now = WarCatalog.RankOf(w.xp, WarCatalog.TroopXp);
        if (now <= w.rank) return;
        w.rank = now;
        if (tracking) report.ranksGained++;
        ShowPip(u, w);
        u.SetBuildingModifier(w.raw ?? Identity);   // the rank's stats, now
        Puff.Spawn(u.transform.position + Vector3.up * 0.5f, 0.5f, new Color(1f, 0.85f, 0.35f, 0.85f), 5, 0.5f);
        if (now >= 3)
            FreeModeController.Instance?.ShowMessage($"{Describe(s, t, w)} is now {(now == 3 ? "an" : "a")} {WarCatalog.RankText(now)}!");
    }

    private static readonly Dictionary<int, Sprite> pipSprites = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> bannerSprites = new Dictionary<int, Sprite>();

    /// <summary>A small rank mark by the health bar: one to three chevrons, then a star.</summary>
    private static void ShowPip(Unit u, WarMind w)
    {
        if (w.pipRank == w.rank) return;
        w.pipRank = w.rank;
        var look = u.GetComponent<SpriteUnitVisual>();
        if (w.rank <= 0) { if (w.pip != null) w.pip.enabled = false; return; }
        float ppu = UnitPpu(look);
        if (w.pip == null)
        {
            var go = new GameObject("RankPip");
            go.transform.SetParent(u.transform, false);
            w.pip = go.AddComponent<SpriteRenderer>();
            w.pip.sortingOrder = 8;
            float bw = look != null ? look.barWidth : 0.5f, bh = look != null ? look.barHeight : 1f;
            go.transform.localPosition = new Vector3(-bw * 0.5f - 4f / ppu, bh + 1f / ppu, 0f);
        }
        w.pip.enabled = true;
        w.pip.sprite = PipSprite(w.rank, ppu);
    }

    /// <summary>Pixels per world unit of a unit's art (siege engines have no hero rig, so work it out from the unit scale).</summary>
    private static float UnitPpu(SpriteUnitVisual look)
    {
        if (look != null && look.pixelsPerUnit > 0f) return look.pixelsPerUnit;
        var c = FreeModeController.Instance;
        return 32f * (c != null ? c.unitScale : 0.42f);
    }

    private static Sprite PipSprite(int rank, float ppu)
    {
        int key = rank * 1000 + Mathf.RoundToInt(ppu * 10f);
        if (pipSprites.TryGetValue(key, out var s) && s != null) return s;
        var c = new PixelCanvas(9, 9);
        var ramp = rank == 1 ? PixelPalette.Bronze : rank == 2 ? PixelPalette.Steel : PixelPalette.Gold;
        if (rank >= 4)
        {
            // a star
            c.Put(4, 8, ramp[3]);
            for (int x = 3; x <= 5; x++) c.Put(x, 7, ramp[2]);
            for (int x = 1; x <= 7; x++) c.Put(x, 6, x == 4 ? ramp[3] : ramp[2]);
            for (int x = 2; x <= 6; x++) c.Put(x, 5, ramp[2]);
            for (int x = 2; x <= 6; x++) c.Put(x, 4, x == 4 ? ramp[3] : ramp[1]);
            c.Put(2, 3, ramp[1]); c.Put(3, 3, ramp[1]); c.Put(5, 3, ramp[1]); c.Put(6, 3, ramp[1]);
            c.Put(1, 2, ramp[1]); c.Put(7, 2, ramp[1]);
            c.Put(4, 5, PixelPalette.Berry[3]);
        }
        else
        {
            for (int i = 0; i < rank; i++)
            {
                int y = 6 - i * 2;
                for (int x = 1; x <= 7; x++)
                {
                    int dy = 3 - Mathf.Abs(x - 4);
                    c.Put(x, y - 3 + dy, x == 4 ? ramp[3] : ramp[2]);
                    c.Put(x, y - 4 + dy, ramp[1]);
                }
            }
        }
        c.Outline(PixelPalette.Ink, 0.9f);
        var tex = c.ToTexture();
        tex.name = "RankPip";
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
        pipSprites[key] = s;
        return s;
    }

    /// <summary>A commander carries a banner (everyone can see where the aura is).</summary>
    private static void ShowBanner(Troop t, WarMind w, bool on)
    {
        if (!on) { if (w.banner != null) w.banner.enabled = false; return; }
        var look = t.GetComponent<SpriteUnitVisual>();
        float ppu = UnitPpu(look);
        if (w.banner == null)
        {
            var go = new GameObject("CommandBanner");
            go.transform.SetParent(t.transform, false);
            w.banner = go.AddComponent<SpriteRenderer>();
            w.banner.sortingOrder = BuildingView.SortingOrder + 1;
            float bh = look != null ? look.barHeight : 1f;
            go.transform.localPosition = new Vector3(-5f / ppu, bh - 14f / ppu, 0f);
            int key = Mathf.RoundToInt(ppu * 10f);
            if (!bannerSprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                var c = new PixelCanvas(9, 22);
                for (int y = 0; y < 21; y++) c.Put(1, y, PixelPalette.Wood[y % 4 == 0 ? 1 : 2]);
                c.Put(1, 21, PixelPalette.Gold[3]);
                for (int y = 12; y < 20; y++)
                    for (int x = 2; x < 8; x++)
                    {
                        bool notch = x >= 6 && Mathf.Abs(y - 16) <= 7 - x;
                        if (notch) continue;
                        c.Put(x, y, y == 19 ? PixelPalette.Blue[3] : (x + y) % 5 == 0 ? PixelPalette.Blue[1] : PixelPalette.Blue[2]);
                    }
                c.Put(4, 16, PixelPalette.Gold[2]); c.Put(4, 15, PixelPalette.Gold[3]); c.Put(3, 16, PixelPalette.Gold[1]); c.Put(5, 16, PixelPalette.Gold[1]);
                c.Outline(PixelPalette.Ink, 0.8f);
                var tex = c.ToTexture();
                tex.name = "CommandBanner";
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.2f, 0f), ppu, 0, SpriteMeshType.FullRect);
                bannerSprites[key] = sprite;
            }
            w.banner.sprite = sprite;
        }
        w.banner.enabled = true;
    }

    // ---------- Waves: siege engines join, and what we track for the report ----------

    private static WaveDirector subscribed;
    private static bool tracking;
    private static int raidId;
    private static int pendingRaiders, pendingSiege;
    private static string pendingBoss;
    private static RaidTally report = new RaidTally();
    private static int lastCampsCleared = -1;

    private class RaidTally
    {
        public int raiders, siege, slain, lost, heroesDown, ranksGained, bestKills, routs, startTroops;
        public string best, boss;
        public bool inTown, routed;
    }

    private static void OnWaveLaunched(Wave wave)
    {
        var s = Settlement.Instance;
        if (s == null || wave == null || wave.units.Count == 0) return;
        AddSiege(s, wave);
        int siege = 0;
        var db = WarDatabase.Load();
        if (db != null) foreach (var u in wave.units) if (db.siege.Exists(t => t != null && t.raider == u)) siege++;
        pendingRaiders = wave.units.Count;
        pendingSiege = siege;
        pendingBoss = wave.boss != null ? wave.boss.displayName : null;
    }

    /// <summary>Bigger raids, and raids led by a chief, bring siege engines of their age to batter walls and towers.</summary>
    private static void AddSiege(Settlement s, Wave wave)
    {
        var db = WarDatabase.Load();
        var tier = db != null ? db.TierFor(s.State.ageIndex) : null;
        if (tier == null || tier.raider == null || tier.raider.prefab == null) return;
        if (s.State.wavesSurvived < 3) return;
        float chance = 0.18f + 0.025f * s.State.ageIndex + (wave.boss != null ? 0.55f : 0f) + Mathf.Min(0.2f, wave.units.Count * 0.01f);
        if (Random.value > chance) return;
        int count = 1 + (wave.boss != null ? 1 : 0) + (s.State.ageIndex >= 7 && wave.units.Count >= 10 ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            wave.units.Add(tier.raider);
            wave.strength += Settlement.UnitPower(tier.raider) * 0.35f;   // they batter buildings, not troops
        }
    }

    private static void OnAutoResolved(Wave wave, float severity) => Settlement.Instance?.WarAutoResolved(wave, severity);

    private void Subscribe(Settlement s)
    {
        if (s.Waves == subscribed) return;
        if (subscribed != null)
        {
            subscribed.WaveLaunched -= OnWaveLaunched;
            subscribed.WaveAutoResolved -= OnAutoResolved;
        }
        subscribed = s.Waves;
        if (subscribed != null)
        {
            subscribed.WaveLaunched += OnWaveLaunched;
            subscribed.WaveAutoResolved += OnAutoResolved;
        }
    }

    private void StartRaid()
    {
        tracking = true;
        raidId++;
        report = new RaidTally { raiders = pendingRaiders, siege = pendingSiege, boss = pendingBoss };
        int troops = 0;
        foreach (var u in Unit.Living) if (u is Troop t && t.IsAlive) troops++;
        report.startTroops = troops;
        pendingRaiders = pendingSiege = 0;
        pendingBoss = null;
        if (report.siege > 0)
        {
            FreeModeController.Instance?.ShowMessage(report.siege == 1
                ? "<color=#fb7>They bring a siege engine: it will go for your towers and buildings.</color>"
                : $"<color=#fb7>They bring {report.siege} siege engines: they will go for your towers and buildings.</color>");
            GameAudio.UI("siege_horn");
        }
    }

    private void EndRaid(Settlement s)
    {
        tracking = false;
        var r = report;
        // Survivors who were in it learn from it.
        int fought = 0;
        var living = Unit.Living;
        for (int i = 0; i < living.Count; i++)
        {
            if (!(living[i] is Troop t) || !t.IsAlive || t.War == null || t.War.raidStamp != raidId) continue;
            fought++;
            GainXp(s, t, t.War, 12f, 0);
        }
        var counted = new HashSet<int>();
        for (int i = 0; i < living.Count; i++)
            if (living[i] is Troop t && t.War != null && t.War.raidStamp == raidId && t.War.regiment != 0 && counted.Add(t.War.regiment))
            {
                var reg = s.Regiment(t.War.regiment, true);
                reg.battles++;
            }
        for (int i = 0; i < living.Count; i++) if (living[i] != null && living[i].War != null) living[i].War.raidKills = 0;

        if (r.raiders <= 0) r.raiders = r.slain;
        if (r.raiders <= 0 && fought == 0) return;   // nobody came to blows (a raid that never landed)
        bool victory = r.routed || r.slain * 2 >= r.raiders;
        bool hard = victory && r.lost >= Mathf.Max(3, Mathf.RoundToInt(r.startTroops * 0.4f));
        s.WarBattleOver(victory, r.inTown, hard);
        s.WarFileReport(new WarReport
        {
            raiders = r.raiders, siege = r.siege, slain = r.slain, fled = Mathf.Max(0, r.raiders - r.slain), lost = r.lost,
            heroesDown = r.heroesDown, ranksGained = r.ranksGained, best = r.best, bestKills = r.bestKills,
            victory = victory, routed = r.routed, boss = r.boss,
        });
    }

    // ---------- The morale tick ----------

    private const float TickSeconds = 0.4f;
    private const float Near = 5f, Cell = 5f;
    private float nextTick, lastTickTime, nextCleanup;
    private readonly Dictionary<long, List<Unit>> grid = new Dictionary<long, List<Unit>>();
    private readonly Stack<List<Unit>> spare = new Stack<List<Unit>>();
    private readonly List<Unit> heroes = new List<Unit>();
    private readonly List<Vector3> bosses = new List<Vector3>();
    private static readonly float[] catHp = new float[10], catAtk = new float[10], catSpd = new float[10], catCd = new float[10];

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    private void Update()
    {
        var s = Settlement.Instance;
        if (s == null || s.State == null) return;
        Subscribe(s);
        var c = FreeModeController.Instance;
        if (c == null || !c.enabled)
        {
            tracking = false;   // left the scene mid-raid: it's settled by the numbers (and reported that way)
            return;
        }
        bool live = c.WaveInProgress;
        if (live && !tracking) StartRaid();
        else if (!live && tracking) EndRaid(s);

        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + TickSeconds;
        float dt = Mathf.Clamp(Time.time - lastTickTime, 0f, 3f);
        lastTickTime = Time.time;
        UnityEngine.Profiling.Profiler.BeginSample("WarField.Tick");
        Tick(s, c, dt);
        UnityEngine.Profiling.Profiler.EndSample();

        // Camps burnt count towards the commander's tallies.
        int camps = s.State.campsCleared;
        if (lastCampsCleared >= 0 && camps > lastCampsCleared) s.WarCount(WarCounter.CampsBurnt, camps - lastCampsCleared);
        lastCampsCleared = camps;
        if (Time.unscaledTime >= nextCleanup)
        {
            nextCleanup = Time.unscaledTime + 30f;
            s.WarCleanupRegiments();
        }
    }

    private static bool InTown(Vector3 p)
    {
        var c = FreeModeController.Instance;
        if (c == null) return false;
        var zones = c.CurrentZones;
        for (int i = 0; i < zones.Count; i++) if (!zones[i].IsOutpost && zones[i].Near(p, 4f)) return true;
        return false;
    }

    private void Tick(Settlement s, FreeModeController c, float dt)
    {
        foreach (var list in grid.Values) { list.Clear(); spare.Push(list); }
        grid.Clear();
        heroes.Clear();
        bosses.Clear();
        var living = Unit.Living;
        for (int i = 0; i < living.Count; i++)
        {
            var u = living[i];
            if (u == null || !u.IsAlive) continue;
            Vector3 p = u.transform.position;
            long key = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
            if (!grid.TryGetValue(key, out var cell)) { cell = spare.Count > 0 ? spare.Pop() : new List<Unit>(); grid[key] = cell; }
            cell.Add(u);
            if (u is Troop t && t.Hero != null) heroes.Add(u);
        }
        foreach (var b in Boss.Live) if (b != null && b.IsAlive) bosses.Add(b.transform.position);

        // The commander on the field, and what its traits add up to.
        var commanderHero = s.CommanderHere();
        Troop commander = null;
        if (commanderHero != null)
            foreach (var h in heroes) if (h is Troop ht && ht.Hero == commanderHero) { commander = ht; break; }
        float radius = WarCatalog.CommandRadius, auraMorale = 12f, fear = 0f, breakShift = 0f;
        bool noBreak = false;
        for (int i = 0; i < 10; i++) { catHp[i] = catAtk[i] = catSpd[i] = catCd[i] = 1f; }
        catAtk[0] = 1.05f;   // any commander: +5% attack in the aura
        if (commanderHero != null)
        {
            var rec = s.WarHero(commanderHero.id, false);
            if (rec != null)
                foreach (var id in rec.traits)
                {
                    var t = WarCatalog.Trait(id);
                    if (t == null) continue;
                    int k = (int)t.cat;
                    catHp[k] *= t.hp; catAtk[k] *= t.atk; catSpd[k] *= t.speed; catCd[k] *= t.cooldown;
                    auraMorale += t.morale; fear += t.fear; breakShift += t.breakShift; radius += t.radius;
                    noBreak |= t.noBreak;
                }
            if (s.WarDoc(WarCatalog.DocStaff)) radius += 2f;
            if (s.WarDoc(WarCatalog.DocGeneralStaff)) radius += 2f;
        }
        Vector3 cmdPos = commander != null ? commander.transform.position : Vector3.zero;
        float r2 = radius * radius;

        // Army-wide leanings: the mood at home, doctrines, a commander on this world.
        float bias = Mathf.Clamp((s.MoodHere - 50f) * 0.2f, -10f, 10f);
        if (s.WarDoc(WarCatalog.DocEsprit)) bias += 5f;
        if (s.WarDoc(WarCatalog.DocNetwork)) bias += 4f;
        if (s.WarDoc(WarCatalog.DocPsionic)) bias += 3f;
        if (commanderHero != null && !s.IsDown(commanderHero)) bias += 4f;
        float townBonus = 8f + (s.WarDoc(WarCatalog.DocFieldworks) ? 4f : 0f);
        float armyBreak = (s.WarDoc(WarCatalog.DocDepth) ? -4f : 0f) + (s.WarDoc(WarCatalog.DocPsionic) ? -4f : 0f);
        float step = 9f * dt;
        int routingRaiders = 0, raidersAlive = 0;
        bool bossInRaid = false;

        for (int i = 0; i < living.Count; i++)
        {
            var u = living[i];
            if (u == null || !u.IsAlive) continue;
            var w = Mind(u);
            bool player = u.Team == Team.Player;
            var troop = u as Troop;
            var enemy = u as Enemy;
            if (w.bossFlag == 0) w.bossFlag = enemy != null && u.GetComponent<Boss>() != null ? 2 : 1;
            bool isBoss = w.bossFlag == 2;
            bool steady = (troop != null && troop.Hero != null) || isBoss || u is SiegeEngine || u is SiegeTroop || u.Peaceful
                          || (enemy != null && enemy.Charmed) || u.Moves == Movement.Water;
            if (enemy != null && enemy.InLiveWave)
            {
                raidersAlive++;
                if (w.routing) routingRaiders++;
                if (isBoss) bossInRaid = true;
            }

            // Who's about: friends and foes within reach (and where the foes are).
            Vector3 p = u.transform.position;
            int allies = 1, foes = 0;
            Vector2 foeSum = Vector2.zero;
            int gx = Mathf.FloorToInt(p.x / Cell), gy = Mathf.FloorToInt(p.y / Cell);
            for (int cy = gy - 1; cy <= gy + 1; cy++)
                for (int cx = gx - 1; cx <= gx + 1; cx++)
                {
                    if (!grid.TryGetValue(Key(cx, cy), out var cell)) continue;
                    for (int j = 0; j < cell.Count; j++)
                    {
                        var o = cell[j];
                        if (o == u || o.Peaceful) continue;
                        Vector2 d = o.transform.position - p;
                        if (d.sqrMagnitude > Near * Near) continue;
                        if (o.Team == u.Team) allies++;
                        else { foes++; foeSum += (Vector2)o.transform.position; }
                    }
                }

            // The commander's aura (stats and heart).
            bool inAura = player && commander != null && ((Vector2)(p - cmdPos)).sqrMagnitude <= r2;
            float hp = 1f, atk = 1f, spd = 1f, cd = 1f;
            if (inAura)
            {
                int k = (int)CategoryOf(u);
                hp = catHp[0] * catHp[k]; atk = catAtk[0] * catAtk[k]; spd = catSpd[0] * catSpd[k]; cd = catCd[0] * catCd[k];
            }
            w.inAura = inAura;
            w.auraHp = hp; w.auraAtk = atk; w.auraSpd = spd; w.auraCd = cd;
            w.noBreak = inAura && noBreak;
            w.breakShift = armyBreak + (inAura ? breakShift : 0f);
            if (troop != null && troop.Hero != null) ShowBanner(troop, w, troop == commander);

            if (steady)
            {
                w.morale = 75f;   // heroes, chiefs and engines hold (and don't break)
                w.routing = false;
            }
            else
            {
                float target;
                if (player)
                {
                    target = WarCatalog.BaseMorale + WarCatalog.RankMorale[w.rank] + bias;
                    if (w.regiment != 0) { var reg = s.Regiment(w.regiment, false); if (reg != null) target += 4f * reg.drill; }
                    if (inAura) target += auraMorale;
                    for (int h = 0; h < heroes.Count; h++)
                        if (heroes[h] != u && ((Vector2)(heroes[h].transform.position - p)).sqrMagnitude < 16f) { target += 8f; break; }
                    if (foes > 0 && InTown(p)) target += townBonus;
                }
                else
                {
                    target = WarCatalog.RaiderMorale;
                    for (int b = 0; b < bosses.Count; b++)
                        if (((Vector2)(bosses[b] - p)).sqrMagnitude < 49f) { target += 18f; break; }
                    if (commander != null && fear > 0f && ((Vector2)(p - cmdPos)).sqrMagnitude <= r2) target -= fear;
                }
                if (foes > 0)
                {
                    float ratio = foes / (float)allies;
                    target -= Mathf.Clamp((ratio - 1f) * 14f, 0f, 30f);
                    if (allies >= foes * 2) target += 6f;
                }
                target -= (1f - u.health / (float)Mathf.Max(1, u.maxHealth)) * 18f;
                target = Mathf.Clamp(target, 0f, 100f);
                if (w.morale < 0f) w.morale = target;
                else w.morale = Mathf.MoveTowards(w.morale, target, step);
                w.morale = Mathf.Clamp(w.morale, 0f, 100f);

                // Breaking and rallying.
                if (w.routing)
                {
                    if (Time.time >= w.routUntil)
                    {
                        w.routing = false;
                        w.morale = Mathf.Max(w.morale, WarCatalog.RallyTo);
                        w.noBreakUntil = Time.time + 12f;
                        Puff.Spawn(p + Vector3.up * 0.5f, 0.35f, new Color(1f, 1f, 1f, 0.6f), 3, 0.4f);
                    }
                }
                else if (foes > 0 && !w.noBreak && w.morale < WarCatalog.BreakAt + w.breakShift && Time.time >= w.noBreakUntil
                         && !(troop != null && troop.Dungeon != null) && u.Moves != Movement.Water)
                {
                    StartRout(s, c, u, w, troop, enemy, p, foes > 0 ? foeSum / foes : (Vector2)p);
                }
            }

            // Re-apply stats when rank, aura or morale's edge changed.
            int key = w.rank * 7919 + (inAura ? 104729 : 0) + Mathf.RoundToInt(hp * 1000f) * 31 + Mathf.RoundToInt(atk * 1000f) * 17
                      + Mathf.RoundToInt(spd * 1000f) * 13 + Mathf.RoundToInt(cd * 1000f) * 7 + Mathf.RoundToInt(w.MoraleAttack * 100f);
            if (key != w.appliedKey)
            {
                bool neutral = w.rank == 0 && !inAura && Mathf.Approximately(w.MoraleAttack, 1f);
                bool first = w.appliedKey == int.MinValue;
                w.appliedKey = key;
                if (!(first && neutral)) u.SetBuildingModifier(w.raw ?? Identity);
            }
        }

        // A raid breaks: most of it is dead or running, and no chief holds it together.
        if (tracking && !report.routed && !bossInRaid && report.raiders >= 4 && raidersAlive > 0
            && report.slain + routingRaiders >= report.raiders * 0.7f && routingRaiders > 0)
        {
            report.routed = true;
            for (int i = 0; i < living.Count; i++)
                if (living[i] is Enemy e && e.IsAlive && e.InLiveWave && e.IsRaider && !(e is SiegeEngine)) e.Retreat();
            c.ShowMessage("<color=#8f8>The raid breaks! The raiders are fleeing.</color>");
            GameAudio.UI("raid_routed");
            s.WarCount(WarCounter.Routs, raidersAlive);
            s.War.raidersRouted += raidersAlive;
        }
    }

    private static void StartRout(Settlement s, FreeModeController c, Unit u, WarMind w, Troop troop, Enemy enemy, Vector3 p, Vector2 foes)
    {
        w.routing = true;
        w.routs++;
        w.routUntil = Time.time + Random.Range(4f, 7f);
        Vector2 away = (Vector2)p - foes;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
        Vector3 to = p + (Vector3)(away.normalized * 6f);
        if (troop != null && troop.Home != null) to = c.GetRallyPoint(troop.Home);
        to.z = p.z;
        w.fleeTo = to;
        Puff.Spawn(p + Vector3.up * 0.6f, 0.3f, new Color(0.95f, 0.95f, 0.95f, 0.8f), 3, 0.5f);
        if (enemy != null)
        {
            if (tracking) report.routs++;
            s.WarCount(WarCounter.Routs);
            // Twice broken: a raider runs for good.
            if (w.routs >= 2 && enemy.IsRaider && !(enemy is SiegeEngine)) enemy.Retreat();
        }
    }
}
