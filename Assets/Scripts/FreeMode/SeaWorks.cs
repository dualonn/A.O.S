using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sea on the world you're looking at (the Settlement keeps the numbers, SeaSim.cs):
/// - Shoals and oyster beds glint on the water (and show as soft markers when zoomed out).
/// - Fishing buildings send their boats out to the shoals in reach and home again (paths over the
///   water worked out once per building and shoal, a few a frame). Serpents and monsters sink them.
/// - Sea creatures swim where their pods are; serpents and monsters turn into real units that
///   fight when your ships come near, and back again once things are quiet.
/// - Merchant ships sail the sea routes (SeaShip); now and then pirates lie in wait on a route,
///   and idle warships nearby sail out to meet them.
/// - Lighthouse beams burn raiders', pirates' and monsters' hulls, and speed ships along.
/// Created by the sea panel; nothing here is saved.
/// </summary>
public class SeaWorks : MonoBehaviour
{
    public static SeaWorks Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private FreeModeController controller;
    private Settlement S => controller != null ? controller.Settlement : null;
    private MapData Map => S != null ? S.Map : null;
    private SeaLibrary lib;
    private float Scale => controller != null ? controller.unitScale : 0.42f;

    /// <summary>The ships on the map make the sea routes' trips (the Settlement leaves them be).</summary>
    public bool DrivesShips => isActiveAndEnabled && controller != null && S != null && S.IsFounded;

    public static SeaWorks Create(FreeModeController owner)
    {
        if (owner == null) return null;
        var existing = FindAnyObjectByType<SeaWorks>();
        if (existing != null) return existing;
        var go = new GameObject("SeaWorks");
        var w = go.AddComponent<SeaWorks>();
        w.controller = owner;
        w.lib = SeaLibrary.Load();
        Instance = w;
        return w;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private float nextSync, nextDanger, nextLights, nextPirateCheck, nextEscort;

    private void Update()
    {
        var s = S;
        if (s == null || s.State == null || Map == null || !s.IsFounded) return;
        float now = Time.time;
        if (now >= nextSync)
        {
            nextSync = now + 0.5f;
            SyncShoals(s);
            SyncBoats(s);
            SyncPods(s);
            SyncShips(s);
        }
        AnimateShoals();
        TickBoats(s, Time.deltaTime);
        AnimatePods(s, Time.deltaTime);
        if (now >= nextDanger) { nextDanger = now + 0.25f; CheckPods(s); }
        if (now >= nextLights) { nextLights = now + 1f; BurnUnderLights(s); }
        if (now >= nextPirateCheck) { nextPirateCheck = now + 2f; TickPirates(s); }
        if (now >= nextEscort) { nextEscort = now + 2f; Escort(); }
    }

    private Rect ViewRect(float margin = 6f)
    {
        var cam = Camera.main;
        if (cam == null) return new Rect(-1e5f, -1e5f, 2e5f, 2e5f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        return Rect.MinMaxRect(c.x - w - margin, c.y - h - margin, c.x + w + margin, c.y + h + margin);
    }

    private static float Zoom => Camera.main != null ? Camera.main.orthographicSize : 10f;

    // =====================================================================
    // Shoals
    // =====================================================================

    private class ShoalMark { public SeaShoal shoal; public SpriteRenderer sr; public float phase; }
    private readonly Dictionary<int, ShoalMark> marks = new Dictionary<int, ShoalMark>();
    private readonly List<int> gone = new List<int>();
    private readonly HashSet<int> live = new HashSet<int>();

    private void SyncShoals(Settlement s)
    {
        live.Clear();
        foreach (var sh in s.Sea.shoals)
        {
            live.Add(sh.id);
            if (!marks.TryGetValue(sh.id, out var m))
            {
                var go = new GameObject("Shoal");
                go.transform.SetParent(transform, false);
                m = new ShoalMark { shoal = sh, sr = go.AddComponent<SpriteRenderer>(), phase = Random.value * 4f };
                m.sr.sortingOrder = BuildingView.SortingOrder - 2;
                marks[sh.id] = m;
            }
            m.shoal = sh;
        }
        gone.Clear();
        foreach (var pair in marks) if (!live.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone) { if (marks[id].sr != null) Destroy(marks[id].sr.gameObject); marks.Remove(id); }
    }

    private void AnimateShoals()
    {
        if (lib == null) return;
        var view = ViewRect();
        float zoom = Zoom;
        bool far = zoom > 18f;
        foreach (var m in marks.Values)
        {
            var sh = m.shoal;
            bool show = view.Contains(sh.pos);
            if (m.sr.enabled != show) m.sr.enabled = show;
            if (!show) continue;
            bool oysters = sh.kind == SeaCatalog.ShoalOysters;
            var frames = oysters ? lib.shoalOysters : lib.shoalFish;
            float full = Mathf.Clamp01(sh.fish / Mathf.Max(1f, sh.max));
            if (far)
            {
                m.sr.sprite = oysters ? (lib.oysterBlob != null ? lib.oysterBlob : lib.shoalBlob) : lib.shoalBlob;
                float k = Mathf.Clamp(zoom / 14f, 1f, 6f);
                m.sr.transform.localScale = Vector3.one * k * (0.7f + 0.5f * full);
                m.sr.color = new Color(1f, 1f, 1f, 0.35f + 0.5f * full);
            }
            else if (frames.Count > 0)
            {
                m.sr.sprite = frames[(int)((Time.time + m.phase) * 3f) % frames.Count];
                m.sr.transform.localScale = Vector3.one;
                m.sr.color = new Color(1f, 1f, 1f, 0.3f + 0.7f * full);
            }
            m.sr.transform.position = new Vector3(sh.pos.x, sh.pos.y, BuildingView.DepthFor(sh.pos.y) + 0.5f);
        }
    }

    // =====================================================================
    // Fishing boats
    // =====================================================================

    private class Boat
    {
        public int building;
        public BuildingDefinition def;
        public SeaBuilding info;
        public GameObject go;
        public SpriteRenderer sr;
        public SeaLibrary.Look look;
        public Vector3 home;
        public List<Vector3> path;
        public int stage;           // 0 at home, 1 out, 2 fishing, 3 back, 4 sunk
        public float wait, anim;
        public int shoal = -1;
        public Vector3 spot;
    }

    private readonly List<Boat> boats = new List<Boat>();
    private readonly Dictionary<long, List<Vector3>> boatPaths = new Dictionary<long, List<Vector3>>();
    private int boatPathWalls = int.MinValue, searchFrame = -1, searchesThisFrame;

    private bool TakeSearch()
    {
        if (searchFrame != Time.frameCount) { searchFrame = Time.frameCount; searchesThisFrame = 0; }
        if (searchesThisFrame >= 2) return false;
        searchesThisFrame++;
        return true;
    }

    private void SyncBoats(Settlement s)
    {
        if (boatPathWalls != Map.WallVersion) { boatPaths.Clear(); boatPathWalls = Map.WallVersion; }
        // Boats whose building is gone, ranked up or idle are taken in.
        for (int i = boats.Count - 1; i >= 0; i--)
        {
            var bt = boats[i];
            var b = s.TradePost(bt.building);
            bool keep = b != null && b.definitionId == bt.def.id && s.IsWorking(b, bt.def) && !b.Paused && s.StaffingOf(b) > 0f;
            if (keep) continue;
            if (bt.go != null) Destroy(bt.go);
            boats.RemoveAt(i);
        }
        foreach (var b in s.State.buildings)
        {
            var def = s.Database.GetBuilding(b.definitionId);
            if (!SeaCatalog.TryGet(def, out var info) || info.boats <= 0 || !s.IsWorking(b, def) || b.Paused || s.StaffingOf(b) <= 0f) continue;
            int want = Mathf.Min(3, info.boats + (b.upgradeLevel >= 2 ? 1 : 0));
            int have = 0;
            foreach (var bt in boats) if (bt.building == b.instanceId) have++;
            if (have >= want) continue;
            var centre = (Vector3)(b.cell + (Vector2)def.size * 0.5f);
            if (!RiverNav.NearestSailable(Map, centre, 6, out var home)) continue;
            boats.Add(NewBoat(b, def, info, home, have));
        }
    }

    private Boat NewBoat(BuildingState b, BuildingDefinition def, SeaBuilding info, Vector3 home, int index)
    {
        var bt = new Boat { building = b.instanceId, def = def, info = info, home = home, wait = 2f + index * 4f + Random.value * 3f };
        bt.go = new GameObject("FishingBoat");
        bt.go.transform.SetParent(transform, false);
        bt.go.transform.position = home;
        bt.sr = bt.go.AddComponent<SpriteRenderer>();
        bt.sr.sortingOrder = BuildingView.SortingOrder;
        bt.look = lib != null && info.boatKind < lib.fishingBoats.Count ? lib.fishingBoats[info.boatKind] : null;
        if (bt.look != null && bt.look.frames.Count > 0)
        {
            bt.sr.sprite = bt.look.frames[0];
            bt.go.transform.localScale = Vector3.one * Scale;
        }
        else
        {
            bt.sr.sprite = SimpleSprites.Square;
            bt.sr.color = new Color(0.6f, 0.42f, 0.25f);
            bt.go.transform.localScale = new Vector3(0.8f, 0.3f, 1f);
        }
        return bt;
    }

    private void TickBoats(Settlement s, float dt)
    {
        foreach (var bt in boats)
        {
            if (bt.go == null) continue;
            bt.anim += dt;
            switch (bt.stage)
            {
                case 0:
                    bt.wait -= dt;
                    Frame(bt, 0);
                    if (bt.wait <= 0f) SetOut(s, bt);
                    break;
                case 1:
                case 3:
                    if (SailAlong(bt, dt))
                    {
                        if (bt.stage == 1) { bt.stage = 2; bt.wait = Random.Range(8f, 14f); }
                        else { bt.stage = 0; bt.wait = Random.Range(3f, 8f); }
                    }
                    Frame(bt, 1);
                    break;
                case 2:
                    bt.wait -= dt;
                    Frame(bt, 2);
                    if (bt.wait <= 0f)
                    {
                        bt.path = Reverse(bt.path, bt.go.transform.position, bt.home);
                        bt.stage = 3;
                        if (Random.value < 0.15f) GameAudio.Detail("sea_net_haul", bt.go.transform.position, 0.4f);
                    }
                    break;
                case 4:
                    bt.wait -= dt;
                    if (bt.wait <= 0f)
                    {
                        bt.go.transform.position = bt.home;
                        bt.sr.enabled = true;
                        bt.stage = 0;
                        bt.wait = 4f;
                    }
                    break;
            }
        }
    }

    /// <summary>Head for the richest shoal in reach (or try their luck nearby when there's none).</summary>
    private void SetOut(Settlement s, Boat bt)
    {
        int kind = bt.info.role == SeaRole.Pearl ? SeaCatalog.ShoalOysters : SeaCatalog.ShoalFish;
        SeaShoal best = null;
        float reach = bt.info.reach + 2f;
        bool whaler = bt.info.role == SeaRole.Whaling;
        Vector3 goal = bt.home;
        if (whaler)
        {
            SeaPod pod = null;
            foreach (var p in s.Sea.pods)
            {
                var sp = SeaCatalog.SpeciesOf(p.species);
                if (sp == null || sp.monster || (sp.role == WildRole.Predator && bt.info.tier < 1)) continue;
                if ((p.pos - (Vector2)bt.home).sqrMagnitude <= reach * reach && (pod == null || p.count > pod.count)) pod = p;
            }
            if (pod != null) goal = pod.pos;
        }
        else
        {
            foreach (var sh in s.Sea.shoals)
                if (sh.kind == kind && (sh.pos - (Vector2)bt.home).sqrMagnitude <= reach * reach && (best == null || sh.fish > best.fish)) best = sh;
            if (best != null) goal = best.pos + Random.insideUnitCircle * 1.2f;
        }
        if (goal == bt.home)
        {
            var off = Random.insideUnitCircle * Mathf.Min(6f, bt.info.reach * 0.5f);
            goal = bt.home + new Vector3(off.x, off.y, 0);
        }
        if (!RiverNav.Sailable(Map, goal) && !RiverNav.NearestSailable(Map, goal, 3, out goal)) { bt.wait = 5f; return; }
        bt.shoal = best != null ? best.id : -1;
        long key = ((long)bt.building << 32) | (uint)(best != null ? best.id : (Mathf.FloorToInt(goal.x) * 73856093 ^ Mathf.FloorToInt(goal.y) * 19349663));
        if (!boatPaths.TryGetValue(key, out var path))
        {
            if (!RiverNav.ClearLine(Map, bt.home, goal))
            {
                if (!TakeSearch()) { bt.wait = 0.2f; return; }
                path = RiverNav.FindPath(Map, bt.home, goal, out bool reached, 5000);
                if (!reached) { bt.wait = 8f; boatPaths[key] = null; return; }
            }
            else path = new List<Vector3> { goal };
            if (boatPaths.Count > 400) boatPaths.Clear();
            boatPaths[key] = path;
        }
        if (path == null) { bt.wait = 8f; return; }
        bt.path = new List<Vector3>(path);
        bt.spot = goal;
        bt.stage = 1;
    }

    private static List<Vector3> Reverse(List<Vector3> path, Vector3 from, Vector3 to)
    {
        var back = new List<Vector3>();
        if (path != null)
            for (int i = path.Count - 2; i >= 0; i--) back.Add(path[i]);
        back.Add(to);
        return back;
    }

    private bool SailAlong(Boat bt, float dt)
    {
        var t = bt.go.transform;
        var s = S;
        float speed = (bt.info.boatKind >= 3 ? 2.2f : bt.info.boatKind >= 2 ? 1.7f : 1.2f) * LightSpeedAt(t.position) * (s != null ? s.UnitSpeedFactor : 1f);
        float step = speed * dt;
        while (bt.path != null && bt.path.Count > 0 && step > 0f)
        {
            Vector3 target = bt.path[0];
            target.z = t.position.z;
            float d = Vector2.Distance(t.position, target);
            if (Mathf.Abs(target.x - t.position.x) > 0.01f) bt.sr.flipX = target.x < t.position.x;
            if (d <= step) { t.position = target; step -= d; bt.path.RemoveAt(0); }
            else { t.position = Vector3.MoveTowards(t.position, target, step); step = 0f; }
        }
        var p = t.position;
        t.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
        return bt.path == null || bt.path.Count == 0;
    }

    /// <summary>Frames: 0 idle (bob), 1 sailing, 2 hauling nets.</summary>
    private void Frame(Boat bt, int mode)
    {
        if (bt.look == null || bt.look.frames.Count < 6) return;
        int f = mode * 2 + (int)(bt.anim * bt.look.fps) % 2;
        if (bt.sr.sprite != bt.look.frames[f]) bt.sr.sprite = bt.look.frames[f];
        if (mode == 1 && bt.look.smoke != Vector2.zero && Random.value < 0.06f)
        {
            var off = bt.look.smoke;
            if (bt.sr.flipX) off.x = -off.x;
            Puff.Spawn(bt.go.transform.position + (Vector3)off, 0.15f, new Color(0.8f, 0.8f, 0.8f, 0.6f), 1, 0.7f);
        }
    }

    private void SinkBoat(Settlement s, Boat bt, SeaSpecies by)
    {
        var pos = bt.go.transform.position;
        Puff.Spawn(pos, 0.5f, new Color(0.75f, 0.9f, 1f, 0.85f), 6, 0.8f);
        GameAudio.Play("river_boat_sink", pos, 0.6f);
        bt.sr.enabled = false;
        bt.stage = 4;
        bt.wait = 40f;
        var b = s.TradePost(bt.building);
        if (b != null) s.SeaBoatLost(b, bt.def, by);
    }

    // =====================================================================
    // Sea creatures
    // =====================================================================

    private class PodVis
    {
        public SeaPod pod;
        public SeaSpecies sp;
        public SeaLibrary.Creature art;
        public readonly List<SpriteRenderer> shown = new List<SpriteRenderer>();
        public readonly List<Enemy> units = new List<Enemy>();
        public bool awake;
        public float quietSince, anim;
    }

    private readonly Dictionary<int, PodVis> pods = new Dictionary<int, PodVis>();
    private readonly Dictionary<Enemy, PodVis> unitPod = new Dictionary<Enemy, PodVis>();

    private void SyncPods(Settlement s)
    {
        live.Clear();
        foreach (var p in s.Sea.pods)
        {
            live.Add(p.id);
            if (!pods.TryGetValue(p.id, out var pv))
            {
                var sp = SeaCatalog.SpeciesOf(p.species);
                if (sp == null) continue;
                pv = new PodVis { pod = p, sp = sp, art = lib != null ? lib.Get(p.species) : null, anim = Random.value * 5f };
                pods[p.id] = pv;
            }
            pv.pod = p;
        }
        gone.Clear();
        foreach (var pair in pods) if (!live.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone)
        {
            var pv = pods[id];
            foreach (var sr in pv.shown) if (sr != null) Destroy(sr.gameObject);
            pv.shown.Clear();
            if (pv.units.Count == 0) pods.Remove(id);
            else pv.pod = null;   // gone from the sea while fighting: the last of them fight on
        }
    }

    private void AnimatePods(Settlement s, float dt)
    {
        var view = ViewRect();
        foreach (var pv in pods.Values)
        {
            pv.anim += dt;
            if (pv.pod == null || pv.awake || pv.art == null || pv.art.frames.Count < 5)
            {
                foreach (var sr in pv.shown) if (sr != null && sr.enabled) sr.enabled = false;
                continue;
            }
            bool show = view.Contains(pv.pod.pos);
            int want = show ? Mathf.Min(pv.sp.monster ? 1 : 4, pv.pod.count) : 0;
            while (pv.shown.Count < want)
            {
                var go = new GameObject(pv.sp.name);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * Scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = BuildingView.SortingOrder - 1;
                pv.shown.Add(sr);
            }
            for (int i = 0; i < pv.shown.Count; i++)
            {
                var sr = pv.shown[i];
                bool on = i < want;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                // Each one circles the pod's spot; whales surface and dive (fading) now and then.
                float t = pv.anim * 0.25f + i * 1.7f;
                var off = new Vector2(Mathf.Cos(t) * (1.2f + i * 0.6f), Mathf.Sin(t * 0.7f) * 0.6f);
                var p = pv.pod.pos + off;
                float dive = pv.sp.monster ? 1f : Mathf.Clamp01(0.55f + Mathf.Sin(pv.anim * 0.35f + i * 2.1f) * 0.9f);
                sr.color = new Color(1f, 1f, 1f, dive);
                sr.flipX = Mathf.Sin(t) > 0f;   // swimming round: heading follows the circle
                int f = 1 + (int)(pv.anim * 3f + i) % 4;
                sr.sprite = pv.art.frames[Mathf.Clamp(f, 0, pv.art.frames.Count - 1)];
                sr.transform.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
            }
        }
    }

    /// <summary>Who's near the sea creatures: boats get sunk, warships wake the fighters, grazers dive away.</summary>
    private void CheckPods(Settlement s)
    {
        float now = Time.time;
        foreach (var pv in pods.Values)
        {
            // Fighting: keep the pod where its fighters are, count the fallen, settle down once things are quiet.
            if (pv.awake)
            {
                for (int i = pv.units.Count - 1; i >= 0; i--)
                {
                    var u = pv.units[i];
                    if (u != null && u.IsAlive) continue;
                    pv.units.RemoveAt(i);
                    if (u != null) unitPod.Remove(u);
                    if (pv.pod != null || u != null) s.SeaCreatureSlain(pv.pod != null ? pv.pod.id : -1, pv.sp.id);
                }
                if (pv.units.Count == 0) { pv.awake = false; continue; }
                Vector2 mid = Vector2.zero;
                foreach (var u in pv.units) mid += (Vector2)u.transform.position;
                mid /= pv.units.Count;
                if (pv.pod != null) { pv.pod.pos = mid; pv.pod.target = mid; }
                // A monster badly hurt dives away to lick its wounds.
                if (pv.sp.monster && pv.pod != null)
                {
                    var m = pv.units[0];
                    float life = m.health / (float)Mathf.Max(1, m.maxHealth);
                    s.SeaMonsterHurt(pv.pod.id, life);
                    if (life < 0.2f)
                    {
                        s.SeaDriveOff(pv.pod.id, 900f);
                        controller.ShowMessage($"{pv.sp.name} dives away, badly hurt. It will be back...");
                        Sleep(pv);
                        continue;
                    }
                }
                if (PlayerNear(mid, 18f, out _)) pv.quietSince = now;
                else if (now - pv.quietSince > 12f) Sleep(pv);
                continue;
            }
            if (pv.pod == null || Map == null) continue;
            var pos = pv.pod.pos;
            bool dangerous = pv.sp.role == WildRole.Predator && s.State.playTime >= pv.pod.fleeUntil;
            // Fishing boats in the jaws of a serpent or monster go down.
            if (dangerous)
                foreach (var bt in boats)
                {
                    if (bt.go == null || (bt.stage != 1 && bt.stage != 2)) continue;
                    if (((Vector2)bt.go.transform.position - pos).sqrMagnitude > (pv.sp.monster ? 9f : 4f)) continue;
                    if (Random.value < 0.25f) SinkBoat(s, bt, pv.sp);
                }
            float wake = pv.sp.monster ? 10f : 7f;
            if (!PlayerNear(pos, wake, out var near)) continue;
            if (dangerous && pv.art != null && pv.art.unit != null && pv.art.unit.prefab != null) Wake(s, pv);
            else if (!dangerous && pv.sp.role == WildRole.Grazer && near != null)
            {
                // Grazers dive and swim off from boats that crowd them.
                var away = (pos - (Vector2)near.transform.position).normalized * 10f;
                var t = pos + away;
                if (RiverNav.Sailable(Map, t)) { pv.pod.target = t; pv.pod.fleeUntil = s.State.playTime + 8f; }
            }
        }
    }

    /// <summary>One of yours afloat (a warship or a merchant ship) within 'range'?</summary>
    private static bool PlayerNear(Vector2 p, float range, out Unit found)
    {
        found = null;
        float r2 = range * range;
        foreach (var u in Unit.Living)
        {
            if (u == null || !u.IsAlive || u.Team != Team.Player) continue;
            if (u.Moves != Movement.Water && !(u is SeaShip)) continue;
            if (((Vector2)u.transform.position - p).sqrMagnitude <= r2) { found = u; return true; }
        }
        return false;
    }

    private void Wake(Settlement s, PodVis pv)
    {
        pv.awake = true;
        pv.quietSince = Time.time;
        foreach (var sr in pv.shown) if (sr != null) sr.enabled = false;
        int n = pv.sp.monster ? 1 : Mathf.Min(4, pv.pod.count);
        for (int i = 0; i < n; i++)
        {
            var at = (Vector3)(pv.pod.pos + Random.insideUnitCircle * 1.2f);
            if (!RiverNav.Sailable(Map, at)) at = pv.pod.pos;
            var go = controller.SpawnUnit(pv.art.unit.prefab, at);
            var e = go.GetComponent<Enemy>();
            if (e == null) { Destroy(go); continue; }
            if (pv.sp.monster) e.health = Mathf.Max(1, Mathf.RoundToInt(e.maxHealth * Mathf.Clamp(pv.pod.life, 0.2f, 1f)));
            e.StartGuard(at, pv.sp.monster ? 7f : 5f);
            pv.units.Add(e);
            unitPod[e] = pv;
        }
        if (pv.units.Count == 0) { pv.awake = false; return; }
        GameAudio.Play(pv.sp.monster ? "sea_monster_roar" : "beast_roar", pv.pod.pos, 0.8f);
        if (pv.sp.monster) controller.ShowMessage($"{pv.sp.name} rises from the deep!");
    }

    private void Sleep(PodVis pv)
    {
        foreach (var u in pv.units)
        {
            if (u == null) continue;
            unitPod.Remove(u);
            Destroy(u.gameObject);
        }
        pv.units.Clear();
        pv.awake = false;
    }

    // =====================================================================
    // Merchant ships and pirates
    // =====================================================================

    private readonly Dictionary<int, SeaShip> ships = new Dictionary<int, SeaShip>();
    private readonly List<int> scratch = new List<int>();

    private void SyncShips(Settlement s)
    {
        var routes = s.SeaRoutesHere;
        scratch.Clear();
        foreach (var pair in ships)
            if (pair.Value == null || !routes.Exists(r => r.id == pair.Key && !r.blocked && r.pathKnown)) scratch.Add(pair.Key);
        foreach (int id in scratch)
        {
            if (ships[id] != null && ships[id].IsAlive) Destroy(ships[id].gameObject);
            ships.Remove(id);
        }
        foreach (var r in routes)
        {
            if (r.blocked || !r.pathKnown || r.path == null || r.path.Count < 2) continue;
            ships.TryGetValue(r.id, out var ship);
            int tier = s.SeaShipTier(r);
            if (ship != null && ship.IsAlive && ship.Tier != tier && r.progress < 0.02f) { Destroy(ship.gameObject); ship = null; }
            if (ship != null && !ship.IsAlive) ship = null;
            if (ship != null || s.State.playTime < r.lostUntil) continue;
            var way = new List<Vector3>(r.path.Count);
            foreach (var p in r.path) way.Add(p);
            ships[r.id] = SeaShip.Spawn(this, r, way, tier, s.SeaShipHealth(tier), Scale);
        }
    }

    public SeaShip ShipOf(SeaRoute r) => ships.TryGetValue(r.id, out var sh) && sh != null && sh.IsAlive ? sh : null;

    public string ShipHome(SeaShip ship)
    {
        var s = S;
        if (s == null || ship.Route == null) return "";
        GameAudio.Play("sea_ship_bell", ship.transform.position, 0.6f);
        return s.SeaTripDone(ship.Route);
    }

    public void ShipSunk(SeaShip ship)
    {
        var s = S;
        if (s == null || ship.Route == null) return;
        GameAudio.Play("river_boat_sink", ship.transform.position, 0.8f);
        s.SeaShipLost(ship.Route, pirates.Count > 0 ? "Pirates" : "Something in the deep");
        ships.Remove(ship.Route.id);
    }

    /// <summary>Lighthouse beams speed ships and boats along a little.</summary>
    public float LightSpeedAt(Vector3 p)
    {
        var s = S;
        if (s == null) return 1f;
        foreach (var l in lights) if ((l.at - (Vector2)p).sqrMagnitude <= l.r * l.r) return 1.1f;
        return 1f;
    }

    private readonly List<Enemy> pirates = new List<Enemy>();
    private readonly List<(Enemy e, float until)> leaving = new List<(Enemy, float)>();
    private SeaShip prey;
    private float pirateUntil, nextPirates = -1f;

    private void TickPirates(Settlement s)
    {
        // Pirates that gave up sail off and are gone.
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            var (e, until) = leaving[i];
            if (e == null || !e.IsAlive) { leaving.RemoveAt(i); continue; }
            if (Time.time > until) { Destroy(e.gameObject); leaving.RemoveAt(i); }
        }
        // The band at sea: count the sunk, follow their prey, give up after a while.
        for (int i = pirates.Count - 1; i >= 0; i--)
        {
            var e = pirates[i];
            if (e != null && e.IsAlive) continue;
            pirates.RemoveAt(i);
            if (e != null)
            {
                s.Sea.piratesSunk++;
                if (s.State.ageIndex >= 1 && s.Database.coin != null)
                {
                    float bounty = 25f + 12f * s.State.ageIndex;
                    s.AddResource(s.Database.coin, bounty);
                    TradeWorks.Popup(e.transform.position + Vector3.up, $"+{bounty:0} coin bounty");
                }
            }
        }
        if (pirates.Count > 0)
        {
            bool preyGone = prey == null || !prey.IsAlive;
            if (preyGone || Time.time > pirateUntil)
            {
                foreach (var e in pirates)
                {
                    var away = e.transform.position + (Vector3)(Random.insideUnitCircle.normalized * 25f);
                    e.StartGuard(away, 2f);
                    leaving.Add((e, Time.time + 40f));
                }
                pirates.Clear();
            }
            else foreach (var e in pirates) if (e.currentTarget == null) e.StartGuard(prey.transform.position, 4f);
            return;
        }
        // A new band, now and then, while ships are at sea (from the Medieval Age).
        if (nextPirates < 0f) nextPirates = Time.time + Random.Range(240f, 420f);
        if (Time.time < nextPirates) return;
        nextPirates = Time.time + Random.Range(300f, 600f) * (1f + 0.3f * lights.Count);
        if (s.State.ageIndex < 4 || lib == null || lib.pirates.Count == 0) return;
        var target = PickPrey(s);
        if (target == null) return;
        if (LightTierAt(target.transform.position) >= 0 && Random.value < 0.5f) return;   // the beam shows them up: they keep off
        int tier = Mathf.Clamp(SeaCatalog.PirateTier(s.State.ageIndex), 0, lib.pirates.Count - 1);
        var def = lib.pirates[tier];
        if (def == null || def.prefab == null) return;
        if (!AheadOf(target, out var lair)) return;
        int count = 1 + (s.State.ageIndex >= 7 ? 1 : 0) + (Random.value < 0.4f ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            var at = lair + (Vector3)(Random.insideUnitCircle * 1.5f);
            if (!RiverNav.Sailable(Map, at)) at = lair;
            var go = controller.SpawnUnit(def.prefab, at);
            var e = go.GetComponent<Enemy>();
            if (e == null) { Destroy(go); continue; }
            e.StartGuard(at, 5f);
            pirates.Add(e);
        }
        if (pirates.Count == 0) return;
        prey = target;
        pirateUntil = Time.time + 150f;
        controller.ShowMessage(pirates.Count == 1 ? "A pirate ship lies in wait on a sea route!" : $"{pirates.Count} pirate ships lie in wait on a sea route!");
        GameAudio.UI("sea_pirate_horn");
    }

    private SeaShip PickPrey(Settlement s)
    {
        SeaShip best = null;
        foreach (var sh in ships.Values)
            if (sh != null && sh.IsAlive && sh.Route.progress > 0.1f && sh.Route.progress < 0.6f && (best == null || Random.value < 0.5f)) best = sh;
        return best;
    }

    /// <summary>A spot on the water a little way ahead of a ship, just off its course.</summary>
    private bool AheadOf(SeaShip ship, out Vector3 at)
    {
        at = ship.transform.position;
        var r = ship.Route;
        if (r.path == null || r.path.Count < 2) return false;
        float want = Mathf.Clamp01(r.progress + 0.15f);
        int i = Mathf.Clamp(Mathf.RoundToInt(want * (r.path.Count - 1)), 0, r.path.Count - 1);
        var p = (Vector3)(r.toB ? r.path[i] : r.path[r.path.Count - 1 - i]);
        var off = Random.insideUnitCircle * 3f;
        at = p + new Vector3(off.x, off.y, 0);
        if (RiverNav.Sailable(Map, at)) return true;
        at = p;
        return RiverNav.Sailable(Map, at);
    }

    /// <summary>Idle warships near pirates or a fighting sea creature sail out to meet them.</summary>
    private void Escort()
    {
        if (pirates.Count == 0 && unitPod.Count == 0) return;
        foreach (var u in Unit.Living)
        {
            if (!(u is Troop t) || !t.IsAlive || t.Moves != Movement.Water || t.HasOrder || t.currentTarget != null) continue;
            Enemy nearest = null;
            float best = 30f * 30f;
            foreach (var e in pirates) Consider(e);
            foreach (var e in unitPod.Keys) Consider(e);
            if (nearest != null) t.SetRallyPoint(nearest.transform.position);

            void Consider(Enemy e)
            {
                if (e == null || !e.IsAlive) return;
                float d = (e.transform.position - t.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = e; }
            }
        }
    }

    // =====================================================================
    // Lighthouses
    // =====================================================================

    private readonly List<(Vector2 at, float r, int tier)> lights = new List<(Vector2, float, int)>();

    private int LightTierAt(Vector3 p)
    {
        int best = -1;
        foreach (var l in lights) if (l.tier > best && (l.at - (Vector2)p).sqrMagnitude <= l.r * l.r) best = l.tier;
        return best;
    }

    /// <summary>Gather the beams, and burn the hulls of hostile boats and creatures under them.</summary>
    private void BurnUnderLights(Settlement s)
    {
        lights.Clear();
        foreach (var b in s.State.buildings)
        {
            var def = s.Database.GetBuilding(b.definitionId);
            if (!SeaCatalog.TryGet(def, out var info) || info.role != SeaRole.Light || !s.IsWorking(b, def) || b.Paused) continue;
            lights.Add((b.cell + (Vector2)def.size * 0.5f, info.reach + 2 * Mathf.Min(3, b.upgradeLevel), info.tier));
        }
        if (lights.Count == 0) return;
        foreach (var u in Unit.Living)
        {
            if (!(u is Enemy e) || !e.IsAlive || e.Team != Team.Enemy || e.Moves != Movement.Water) continue;
            int tier = LightTierAt(e.transform.position);
            if (tier < 0) continue;
            e.TakeDamage(4 + 4 * tier);
            if (Random.value < 0.3f) Puff.Spawn(e.transform.position + Vector3.up * 0.5f, 0.25f, new Color(1f, 0.7f, 0.3f, 0.8f), 2, 0.5f);
        }
    }

    // =====================================================================
    // For the panel
    // =====================================================================

    /// <summary>Where the monster is now (its fighter if it's up, else the pod).</summary>
    public bool MonsterPosition(SeaPod monster, out Vector3 at)
    {
        at = default;
        if (monster == null) return false;
        if (pods.TryGetValue(monster.id, out var pv) && pv.units.Count > 0 && pv.units[0] != null) { at = pv.units[0].transform.position; return true; }
        at = monster.pos;
        return true;
    }
}
