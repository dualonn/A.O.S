using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The wildlife of the world you're looking at (Settlement.Wild holds the herds):
/// - Herds are drawn as animals milling about where the herd is. They're plain sprites, not
///   units, so nothing in a battle ever scans them.
/// - When your people come close, grazers bolt; predators (and big beasts you crowd) become real
///   WildBeast units that fight, and go back to being animals once you've gone.
/// - A beast on the prowl that reaches a town fights there (villagers hide indoors).
/// - Beast Pens' tamed beasts are kept in the field as war beasts, and heroes' companions trot
///   along at their heels.
/// </summary>
public class WildlifeView : MonoBehaviour
{
    public static WildlifeView Instance { get; private set; }

    /// <summary>A wild beast is fighting inside one of your towns (villagers stay indoors).</summary>
    public static bool BeastInTown { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; BeastInTown = false; }

    private const int MaxShown = 8;          // animals drawn per herd (a big herd still counts in full)
    private const int MaxAwake = 8;          // at most this many of one herd fight at once
    private const float WalkFps = 8f;
    private const float CalmDistance = 16f;  // no one of yours this close for a while: the herd settles
    private const float CalmSeconds = 10f;

    private class Critter
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector2 pos, goal;
        public float anim, rest;
        public bool grazing;
    }

    private class HerdVis
    {
        public WildHerd herd;
        public WildSpecies sp;
        public WildlifeLibrary.Entry art;
        public readonly List<Critter> critters = new List<Critter>();
        public readonly List<WildBeast> beasts = new List<WildBeast>();
        public bool awake, shown;
        public float quietSince;
    }

    private class Follower
    {
        public Critter c;
        public Troop hero;
        public string species;
        public float scale;
    }

    private FreeModeController controller;
    private WildlifeLibrary lib;
    private readonly Dictionary<int, HerdVis> herds = new Dictionary<int, HerdVis>();
    private readonly Dictionary<int, List<(Troop troop, string species)>> pens = new Dictionary<int, List<(Troop, string)>>();
    private readonly Dictionary<int, Follower> followers = new Dictionary<int, Follower>();
    private readonly List<Vector2> people = new List<Vector2>();
    private readonly List<int> gone = new List<int>();
    private readonly HashSet<int> liveWork = new HashSet<int>(), seenWork = new HashSet<int>(), keepWork = new HashSet<int>();
    private readonly Dictionary<int, Troop> heroWork = new Dictionary<int, Troop>();
    private float nextSync, nextCheck, nextPens, nextFollow, nextHover;
    private TextMeshPro label;
    private Transform root;

    private Settlement S => controller.Settlement;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        Instance = this;
        lib = WildlifeLibrary.Load();
        root = new GameObject("Animals").transform;
        root.SetParent(transform, false);
        var labelObject = new GameObject("HerdLabel");
        labelObject.transform.SetParent(transform, false);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(10, 2f);
        label.sortingOrder = 8;
        label.text = "";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        BeastInTown = false;
    }

    private void Update()
    {
        if (controller == null || S == null || lib == null || !S.IsFounded) return;
        float now = Time.time;
        if (now >= nextSync) { nextSync = now + 0.5f; SyncHerds(); }
        if (now >= nextCheck) { nextCheck = now + 0.25f; CheckReactions(); }
        if (now >= nextPens) { nextPens = now + 0.4f; KeepPens(); }
        if (now >= nextFollow) { nextFollow = now + 0.5f; SyncFollowers(); }
        AnimateHerds(Time.deltaTime);
        AnimateFollowers(Time.deltaTime);
        if (now >= nextHover) { nextHover = now + 0.1f; UpdateHover(); }
    }

    // ---------- Herds ----------

    private void SyncHerds()
    {
        var list = S.Wild.herds;
        var live = liveWork;
        live.Clear();
        foreach (var h in list)
        {
            live.Add(h.id);
            if (!herds.TryGetValue(h.id, out var hv))
            {
                var sp = Settlement.WildSpeciesOf(h.species);
                if (sp == null) continue;
                hv = new HerdVis { herd = h, sp = sp, art = lib.Get(h.species) };
                herds[h.id] = hv;
            }
            hv.herd = h;
        }
        gone.Clear();
        foreach (var pair in herds) if (!live.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone)
        {
            var hv = herds[id];
            foreach (var c in hv.critters) Destroy(c.go);
            hv.critters.Clear();
            if (hv.beasts.Count == 0) herds.Remove(id);
            else hv.herd = null;   // hunted out while fighting: the last of them fight on
        }
    }

    private Rect ViewRect()
    {
        var cam = Camera.main;
        if (cam == null) return new Rect(-1e5f, -1e5f, 2e5f, 2e5f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        const float margin = 6f;
        return Rect.MinMaxRect(c.x - w - margin, c.y - h - margin, c.x + w + margin, c.y + h + margin);
    }

    private void AnimateHerds(float dt)
    {
        var view = ViewRect();
        foreach (var hv in herds.Values)
        {
            if (hv.herd == null || hv.awake || hv.art == null || hv.art.frames.Count < 12) continue;
            bool show = view.Contains(hv.herd.pos);
            if (!show)
            {
                if (hv.shown) foreach (var c in hv.critters) c.go.SetActive(false);
                hv.shown = false;
                continue;
            }
            int want = Mathf.Min(MaxShown, hv.herd.count);
            while (hv.critters.Count < want) hv.critters.Add(MakeCritter(hv.art, hv.herd.pos + Random.insideUnitCircle * 1.5f, 1f));
            while (hv.critters.Count > want)
            {
                Destroy(hv.critters[hv.critters.Count - 1].go);
                hv.critters.RemoveAt(hv.critters.Count - 1);
            }
            if (!hv.shown) foreach (var c in hv.critters) c.go.SetActive(true);
            hv.shown = true;
            bool fleeing = S.State.playTime < hv.herd.fleeUntil;
            float spread = 1.2f + 0.35f * want;
            float speed = (fleeing ? 2.4f : hv.herd.toTown ? 1.0f : 0.55f) * (hv.sp.flyer ? 1.3f : 1f);
            foreach (var c in hv.critters)
            {
                // Each one ambles to a spot near the herd, rests (grazing), and picks another.
                if (c.rest > 0f && !fleeing)
                {
                    c.rest -= dt;
                    c.anim += dt;
                    SetFrame(c, hv.art, c.grazing && hv.sp.role == WildRole.Grazer ? (int)(c.anim * 1.5f) % 2 == 0 ? 5 : 6 : 0);
                }
                else
                {
                    Vector2 to = c.goal - c.pos;
                    if (to.magnitude < 0.08f || (c.goal - hv.herd.pos).magnitude > spread * 1.6f)
                    {
                        c.goal = hv.herd.pos + Random.insideUnitCircle * spread;
                        if (!fleeing) { c.rest = Random.Range(1f, 5f); c.grazing = Random.value < 0.6f; }
                    }
                    else
                    {
                        Vector2 step = to.normalized * Mathf.Min(to.magnitude, speed * dt);
                        Vector2 next = c.pos + step;
                        if (hv.sp.flyer || S.Map.IsWalkable(new Vector3(next.x, next.y))) c.pos = next;
                        else c.goal = hv.herd.pos;
                        if (Mathf.Abs(step.x) > 0.001f) c.sr.flipX = step.x < 0;
                        c.anim += dt;
                        SetFrame(c, hv.art, 1 + (int)(c.anim * WalkFps * (fleeing ? 1.6f : 1f)) % 4);
                    }
                }
                Place(c);
            }
        }
    }

    private Critter MakeCritter(WildlifeLibrary.Entry art, Vector2 at, float scale)
    {
        var go = new GameObject(art.species);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.frames[0];
        sr.sortingOrder = BuildingView.SortingOrder;
        go.transform.localScale = Vector3.one * controller.unitScale * scale;
        var c = new Critter { go = go, sr = sr, pos = at, goal = at, rest = Random.Range(0f, 3f), anim = Random.Range(0f, 2f) };
        Place(c);
        return c;
    }

    private static void SetFrame(Critter c, WildlifeLibrary.Entry art, int frame)
    {
        if (frame >= 0 && frame < art.frames.Count && art.frames[frame] != null) c.sr.sprite = art.frames[frame];
    }

    private static void Place(Critter c) =>
        c.go.transform.position = new Vector3(c.pos.x, c.pos.y, BuildingView.DepthFor(c.pos.y));

    // ---------- Reactions: fleeing, fighting, calming ----------

    private void CheckReactions()
    {
        people.Clear();
        foreach (var u in Unit.Living)
        {
            if (u == null || !u.IsAlive || u.Team != Team.Player || u.Burrowed || u is WildBeast) continue;
            if (u is Troop t && t.Dungeon != null) continue;
            people.Add(u.transform.position);
        }
        var zones = controller.CurrentZones;
        bool inTown = false;
        foreach (var hv in herds.Values)
        {
            if (hv.awake)
            {
                KeepAwake(hv);
                if (hv.awake && hv.herd != null && TownAt(zones, hv.herd.pos, 3f)) inTown = true;
                continue;
            }
            var h = hv.herd;
            if (h == null || h.count <= 0) continue;
            float near = Nearest(h.pos, out Vector2 who);
            switch (hv.sp.role)
            {
                case WildRole.Grazer:
                    if (near < hv.sp.wake && S.State.playTime >= h.fleeUntil - 3f) Flee(hv, who);
                    break;
                case WildRole.Beast:
                    if (near < hv.sp.wake) Wake(hv, hv.sp.legendary ? 6f : 4.5f);
                    break;
                case WildRole.Predator:
                    if (near < hv.sp.wake) Wake(hv, 9f);
                    break;
            }
            // A prowler that reaches a town fights it out there.
            if (!hv.awake && h.toTown && TownAt(zones, h.pos, 4f)) Wake(hv, 7f);
        }
        BeastInTown = inTown;
    }

    private static bool TownAt(List<Settlement.SettlementZone> zones, Vector2 p, float reach)
    {
        foreach (var z in zones) if (!z.IsOutpost && z.Near(p, reach)) return true;
        return false;
    }

    private float Nearest(Vector2 p, out Vector2 who)
    {
        float best = float.MaxValue;
        who = p;
        foreach (var q in people)
        {
            float d = (q - p).sqrMagnitude;
            if (d < best) { best = d; who = q; }
        }
        return Mathf.Sqrt(best);
    }

    private void Flee(HerdVis hv, Vector2 from)
    {
        var h = hv.herd;
        Vector2 away = (h.pos - from).normalized;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
        for (int a = 0; a < 6; a++)
        {
            Vector2 t = h.pos + (Vector2)(Quaternion.Euler(0, 0, Random.Range(-40f, 40f)) * away) * Random.Range(8f, 12f);
            if (hv.sp.flyer || S.Map.IsWalkable(new Vector3(t.x, t.y)))
            {
                h.target = t;
                break;
            }
        }
        h.fleeUntil = S.State.playTime + 6f;
        if (hv.shown) GameAudio.Play("deer_flee", h.pos, 0.5f);
    }

    private void Wake(HerdVis hv, float radius)
    {
        var h = hv.herd;
        var def = hv.art?.wild;
        if (def == null || def.prefab == null) return;
        int n = Mathf.Min(MaxAwake, h.count);
        for (int i = 0; i < n; i++)
        {
            Vector2 at = i < hv.critters.Count ? hv.critters[i].pos : h.pos + Random.insideUnitCircle * 1.5f;
            if (!S.Map.IsWalkable(new Vector3(at.x, at.y))) at = h.pos;
            var go = controller.SpawnUnit(def.prefab, new Vector3(at.x, at.y, 0));
            var beast = go.GetComponent<WildBeast>();
            if (beast == null) { Destroy(go); continue; }
            beast.HerdId = h.id;
            beast.Species = h.species;
            beast.StartGuard(new Vector3(h.pos.x, h.pos.y, 0), radius);
            hv.beasts.Add(beast);
        }
        foreach (var c in hv.critters) Destroy(c.go);
        hv.critters.Clear();
        hv.shown = false;
        if (hv.beasts.Count == 0) return;
        hv.awake = true;
        hv.quietSince = Time.time;
        GameAudio.Play(hv.sp.legendary ? "legend_roar" : hv.sp.role == WildRole.Predator ? "beast_roar" : "beast_snort", h.pos, 0.8f);
    }

    private void KeepAwake(HerdVis hv)
    {
        hv.beasts.RemoveAll(b => b == null || !b.IsAlive);
        if (hv.beasts.Count == 0)
        {
            hv.awake = false;   // (a herd that's gone is dropped at the next sync)
            return;
        }
        // The herd is where its fighters are.
        Vector2 sum = Vector2.zero;
        foreach (var b in hv.beasts) sum += (Vector2)b.transform.position;
        Vector2 at = sum / hv.beasts.Count;
        if (hv.herd != null) { hv.herd.pos = at; hv.herd.target = at; }
        if (Nearest(at, out _) < CalmDistance) { hv.quietSince = Time.time; return; }
        if (Time.time - hv.quietSince < CalmSeconds) return;
        // Nobody about: back to grazing (or prowling off home).
        foreach (var b in hv.beasts) if (b != null) Destroy(b.gameObject);
        hv.beasts.Clear();
        hv.awake = false;
        if (hv.herd != null && hv.herd.toTown) S.HerdGoHome(hv.herd);
    }

    /// <summary>One of the wild beasts fell (its herd is one smaller).</summary>
    public void BeastDied(WildBeast beast)
    {
        foreach (var hv in herds.Values) hv.beasts.Remove(beast);
        S?.WildBeastKilled(beast.HerdId, beast.Species, beast.transform.position);
    }

    // ---------- Tamed beasts from the pens ----------

    private void KeepPens()
    {
        var seen = seenWork;
        seen.Clear();
        foreach (var view in controller.Buildings)
        {
            if (view == null || !Settlement.IsBeastPen(view.Definition)) continue;
            int id = view.State.instanceId;
            seen.Add(id);
            if (!pens.TryGetValue(id, out var list)) pens[id] = list = new List<(Troop, string)>();
            // The fallen are gone for good.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].troop != null && list[i].troop.IsAlive) continue;
                S.PenBeastLost(id, list[i].species);
                list.RemoveAt(i);
            }
            var pen = S.PenOf(id);
            var stats = Settlement.StatsOf(view.Definition, view.State.upgradeLevel);
            // One more into the field at a time, for each tamed beast not out yet.
            if (pen != null && S.IsWorking(view.State, view.Definition))
            {
                foreach (var sp in pen.beasts)
                {
                    int listed = 0, out_ = 0;
                    foreach (var s in pen.beasts) if (s == sp) listed++;
                    foreach (var e in list) if (e.species == sp) out_++;
                    if (out_ >= listed) continue;
                    var def = lib.Get(sp)?.tame;
                    if (def == null || def.prefab == null) continue;
                    var spawn = controller.Grid.AreaCenter(view.State.cell, view.Definition.size);
                    var troop = controller.SpawnUnit(def.prefab, spawn).GetComponent<Troop>();
                    if (troop == null) continue;
                    troop.SetRallyPoint(controller.GetRallyPoint(view));
                    troop.SetStance(view.State.stance, controller.TownZoneFor(view.transform.position), controller.SightRange);
                    troop.SetBuildingModifier(stats.trainedUnits);
                    list.Add((troop, sp));
                    break;
                }
            }
            // Orders and upgrades follow the pen, like any troop building's.
            var zone = controller.TownZoneFor(view.transform.position);
            var area = controller.SettlementFor(view.transform.position);
            float alert = S.Database.defendAlertRange;
            foreach (var (t, _) in list)
            {
                if (t == null) continue;
                if (t.Stance != view.State.stance)
                {
                    t.SetStance(view.State.stance, zone, controller.SightRange);
                    t.ReturnHome(controller.GetRallyPoint(view));
                }
                t.SetDefendZone(zone);
                t.SetDefendArea(area, alert);
                t.SetBuildingModifier(stats.trainedUnits);
            }
        }
        // A pen torn down: its beasts go back to the wild.
        gone.Clear();
        foreach (var pair in pens) if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone)
        {
            foreach (var (t, _) in pens[id]) if (t != null) Destroy(t.gameObject);
            pens.Remove(id);
        }
    }

    // ---------- Heroes' companions ----------

    private void SyncFollowers()
    {
        var heroTroops = heroWork;
        heroTroops.Clear();
        foreach (var u in Unit.Living)
            if (u is Troop t && t.Hero != null && t.IsAlive) heroTroops[t.Hero.id] = t;
        var keep = keepWork;
        keep.Clear();
        foreach (var comp in S.WildGlobal.companions)
        {
            if (!heroTroops.TryGetValue(comp.hero, out var troop)) continue;
            var art = lib.Get(comp.species);
            if (art == null || art.frames.Count < 12) continue;
            keep.Add(comp.hero);
            if (followers.TryGetValue(comp.hero, out var f) && f.species == comp.species)
            {
                f.hero = troop;
                continue;
            }
            if (f != null) Destroy(f.c.go);
            // Companions are drawn small enough to trot at heel (a sky whale is a calf, say).
            float height = art.frames[0].bounds.size.y * controller.unitScale;
            float scale = Mathf.Min(comp.legendary ? 0.85f : 0.75f, 0.9f / Mathf.Max(0.1f, height));
            Vector2 at = (Vector2)troop.transform.position + Vector2.left * 0.8f;
            followers[comp.hero] = new Follower { c = MakeCritter(art, at, scale), hero = troop, species = comp.species, scale = scale };
        }
        gone.Clear();
        foreach (var pair in followers) if (!keep.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone)
        {
            if (followers[id].c.go != null) Destroy(followers[id].c.go);
            followers.Remove(id);
        }
    }

    private void AnimateFollowers(float dt)
    {
        foreach (var f in followers.Values)
        {
            if (f.hero == null || !f.hero.IsAlive)
            {
                f.c.go.SetActive(false);
                continue;
            }
            f.c.go.SetActive(true);
            var art = lib.Get(f.species);
            Vector3 hp = f.hero.transform.position;
            bool heroLeft = Mathf.Abs(f.hero.transform.eulerAngles.y - 180f) < 1f;
            Vector2 goal = (Vector2)hp + new Vector2(heroLeft ? 0.9f : -0.9f, -0.25f);
            Vector2 to = goal - f.c.pos;
            float d = to.magnitude;
            if (d > 6f) f.c.pos = goal;   // left far behind (a dungeon, a jump): catch up at once
            else if (d > 0.15f)
            {
                float speed = Mathf.Max(1.4f, d * 2.2f);
                Vector2 step = to / d * Mathf.Min(d, speed * dt);
                f.c.pos += step;
                if (Mathf.Abs(step.x) > 0.001f) f.c.sr.flipX = step.x < 0;
                f.c.anim += dt;
                SetFrame(f.c, art, 1 + (int)(f.c.anim * WalkFps) % 4);
            }
            else
            {
                f.c.sr.flipX = heroLeft;
                SetFrame(f.c, art, 0);
            }
            Place(f.c);
        }
    }

    // ---------- Hover ----------

    private void UpdateHover()
    {
        Vector3 mouse;
        try { mouse = SettlementGrid.MouseWorld(); }
        catch { return; }
        HerdVis best = null;
        float bestD = float.MaxValue;
        foreach (var hv in herds.Values)
        {
            if (hv.herd == null || (!hv.shown && !hv.awake)) continue;
            float r = 1.4f + 0.3f * Mathf.Min(MaxShown, hv.herd.count);
            float d = ((Vector2)mouse - hv.herd.pos).sqrMagnitude;
            if (d < r * r && d < bestD) { bestD = d; best = hv; }
        }
        if (best == null)
        {
            if (label.text.Length > 0) label.text = "";
            return;
        }
        var h = best.herd;
        string what = best.sp.legendary ? $"<color=#ffd56a>{best.sp.name}</color>"
            : $"<color=#ffd98a>{(h.count > 1 ? $"{h.count} {best.sp.plural}" : best.sp.name)}</color>";
        string role = best.sp.role == WildRole.Grazer ? "Grazers" : best.sp.role == WildRole.Beast ? "Fights back" : "Predators";
        label.text = $"{what}  <size=70%><color=#aaa>{role}</color></size>\n<size=70%>{best.sp.note}</size>";
        label.transform.position = new Vector3(h.pos.x, h.pos.y + 2.2f, -1f);
    }
}
