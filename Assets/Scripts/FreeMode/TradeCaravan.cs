using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A caravan on a trade route: an ox wagon, horse caravan, wagon train, steam train, maglev or
/// hover freighter going to and fro between two trading posts along the route's way (worked out
/// once by TradeWorks; it never searches for a path itself). It's one of yours, so raiders and
/// beasts that come across it fight it, loose WorldBox-style, but it never fights back: it just
/// keeps going. Killed, its goods are lost.
/// </summary>
public class TradeCaravan : Unit
{
    public override Team Team => Team.Player;

    public TradeRoute Route { get; private set; }
    public int Tier { get; private set; }

    private TradeWorks works;
    private List<Vector3> path;
    private int next;            // index of the next waypoint (counting in the direction of travel)
    private float pauseLeft;
    private SpriteRenderer body;
    private SpriteRenderer barBack, barFill;
    private TradeLibrary.Vehicle look;
    private float animTime, nextPuff;
    private bool lost;

    private const float LoadSeconds = 2.5f;

    public static TradeCaravan Spawn(TradeWorks owner, TradeRoute route, List<Vector3> way, int tier, int health)
    {
        var go = new GameObject($"Caravan {route.id}");
        go.transform.SetParent(owner.transform, false);
        var c = go.AddComponent<TradeCaravan>();   // (Awake runs here, with the default stats)
        c.Setup(owner, route, way, tier, health);
        return c;
    }

    private void Setup(TradeWorks owner, TradeRoute route, List<Vector3> way, int tier, int health)
    {
        works = owner;
        Route = route;
        Tier = tier;
        path = way;
        maxHealth = this.health = health;
        attackPower = 0;
        detectionRange = 0.05f;   // looks for nobody: it never picks a fight
        attackRange = 0f;
        knockback = 0.6f;
        corpseTime = 1.5f;
        personalSpace = tier >= TradeCatalog.TierTrain ? 1.1f : 0.7f;

        var lib = TradeLibrary.Load();
        look = lib != null && tier < lib.vehicles.Count ? lib.vehicles[tier] : null;
        body = new GameObject("Body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sortingOrder = BuildingView.SortingOrder;
        if (look != null && look.frames.Count > 0) body.sprite = look.frames[0];
        else
        {
            body.sprite = SimpleSprites.Square;
            body.color = new Color(0.65f, 0.45f, 0.25f);
            body.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            body.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
        }

        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = tier >= TradeCatalog.TierTrain ? 0.8f : 0.5f;
        col.offset = new Vector2(0f, 0.35f);

        barBack = Bar("BarBack", new Color(0, 0, 0, 0.6f), 6);
        barFill = Bar("BarFill", new Color(0.4f, 0.9f, 0.4f), 7);

        // Pick up where the route's caravan was (the trip carries on after a reload or a world switch).
        float along = Mathf.Clamp01(route.progress);
        PlaceAlong(along);
    }

    private SpriteRenderer Bar(string name, Color color, int order)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = SimpleSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        sr.transform.localPosition = new Vector3(-0.4f, Tier >= TradeCatalog.TierHover ? 1.7f : 1.35f, 0f);
        sr.transform.localScale = new Vector3(0.8f, 0.1f, 1f);
        sr.enabled = false;
        return sr;
    }

    /// <summary>Put the caravan 'share' of the way along its trip (in its direction of travel).</summary>
    private void PlaceAlong(float share)
    {
        if (path == null || path.Count < 2) return;
        float total = 0f;
        for (int i = 0; i + 1 < path.Count; i++) total += Vector2.Distance(path[i], path[i + 1]);
        float want = share * total, gone = 0f;
        int n = path.Count;
        for (int k = 0; k + 1 < n; k++)
        {
            Vector3 p = Way(k), q = Way(k + 1);
            float d = Vector2.Distance(p, q);
            if (gone + d >= want || k + 2 == n)
            {
                float t = d > 0.0001f ? Mathf.Clamp01((want - gone) / d) : 1f;
                SetPosition(Vector3.Lerp(p, q, t));
                next = k + 1;
                return;
            }
            gone += d;
        }
    }

    /// <summary>The k-th waypoint counting from where this trip starts.</summary>
    private Vector3 Way(int k) => Route.toB ? path[k] : path[path.Count - 1 - k];

    private void SetPosition(Vector3 p)
    {
        p.z = BuildingView.DepthFor(p.y);
        transform.position = p;
    }

    /// <summary>Take a newly worked-out way (used from the next trip on).</summary>
    public void SetPath(List<Vector3> way)
    {
        if (way == null || way.Count < 2) return;
        if (pauseLeft > 0f) { path = way; next = 1; }
        else pendingPath = way;
    }
    private List<Vector3> pendingPath;

    protected override bool ShouldEngage() => false;

    protected override void Idle()
    {
        if (path == null || path.Count < 2 || Route == null) return;
        float dt = Time.deltaTime;
        animTime += dt;
        UpdateBar();
        if (pauseLeft > 0f)
        {
            pauseLeft -= dt;
            Frame(false);
            if (pauseLeft <= 0f && pendingPath != null) { path = pendingPath; pendingPath = null; }
            return;
        }
        if (next >= path.Count)
        {
            Arrive();
            return;
        }
        Vector3 pos = transform.position;
        Vector3 target = Way(next);
        var nav = MapNavigator.Current;
        var s = Settlement.Instance;
        float speed = s != null ? s.TradeSpeed(Tier) : TradeCatalog.Speed[Tier];
        if (s != null) speed *= s.UnitSpeedFactor;
        Vector3 moved;
        if (Tier == TradeCatalog.TierHover || nav == null)
        {
            moved = Vector3.MoveTowards(pos, target, speed * dt);   // hover freighters skim over everything
        }
        else
        {
            var map = s != null ? s.Map : null;
            if (Tier == TradeCatalog.TierTrain || Tier == TradeCatalog.TierMaglev)
            {
                // Full speed on the track; off it (the last bit to the platform) at a crawl.
                bool onRail = map != null && map.WallAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)) == WallTile.Rail;
                speed *= onRail ? 1f : 0.35f;
            }
            else speed *= nav.SpeedFactor(pos);
            moved = nav.Step(pos, target, speed * dt, Team.Player);
        }
        if (Mathf.Abs(target.x - pos.x) > 0.02f) body.flipX = target.x < pos.x;
        SetPosition(moved);
        Frame(true);
        Effects(moved);
        if (Vector2.Distance(moved, target) < 0.12f) next++;
        Route.progress = Mathf.Clamp01(Progress());
    }

    private float Progress()
    {
        float total = 0f, gone = 0f;
        for (int k = 0; k + 1 < path.Count; k++)
        {
            float d = Vector2.Distance(path[k], path[k + 1]);
            total += d;
            if (k + 1 < next) gone += d;
        }
        if (next > 0 && next < path.Count) gone += Vector2.Distance(Way(next - 1), transform.position);
        return total > 0.01f ? gone / total : 1f;
    }

    private void Arrive()
    {
        string note = works != null ? works.TripDone(this) : "";
        Route.toB = !Route.toB;
        Route.progress = 0f;
        next = 1;
        pauseLeft = LoadSeconds;
        if (pendingPath != null) { path = pendingPath; pendingPath = null; }
        if (!string.IsNullOrEmpty(note)) TradeWorks.Popup(transform.position + Vector3.up * 1.2f, note);
    }

    private void Frame(bool moving)
    {
        if (look == null || look.frames.Count == 0) return;
        int f = moving ? 1 + (int)(animTime * look.fps) % Mathf.Max(1, look.frames.Count - 1) : 0;
        if (look.frames.Count == 1) f = 0;
        var sprite = look.frames[Mathf.Clamp(f, 0, look.frames.Count - 1)];
        if (body.sprite != sprite) body.sprite = sprite;
    }

    private void Effects(Vector3 at)
    {
        if (look == null || look.smoke == Vector2.zero || Time.time < nextPuff) return;
        nextPuff = Time.time + (Tier == TradeCatalog.TierTrain ? 0.35f : 0.6f);
        var off = look.smoke;
        if (body.flipX) off.x = -off.x;
        var color = Tier == TradeCatalog.TierTrain ? new Color(0.85f, 0.85f, 0.85f, 0.7f) : new Color(0.6f, 0.9f, 1f, 0.45f);
        Puff.Spawn(at + (Vector3)off, Tier == TradeCatalog.TierTrain ? 0.35f : 0.2f, color, 1, 0.9f);
    }

    private void UpdateBar()
    {
        bool show = health < maxHealth;
        if (barBack.enabled != show) barBack.enabled = barFill.enabled = show;
        if (!show) return;
        float share = health / (float)Mathf.Max(1, maxHealth);
        barFill.transform.localScale = new Vector3(0.8f * share, 0.1f, 1f);
        barFill.color = Color.Lerp(Color.red, Color.green, share);
    }

    protected override void Die()
    {
        if (!lost)
        {
            lost = true;
            if (works != null) works.CaravanLost(this);
        }
        if (barBack != null) barBack.enabled = barFill.enabled = false;
        if (body != null) body.color = new Color(0.55f, 0.45f, 0.4f);
        base.Die();
    }
}
