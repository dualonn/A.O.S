using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A merchant ship on a sea route: a round ship, cog, galleon, steamship, container ship,
/// hover-barge or void barge sailing to and fro between two ports along the route's way over the
/// water (worked out once by the Settlement; it never searches itself). It's one of yours, so
/// pirates and sea creatures attack it, but it never fights back. Sunk, its cargo is lost.
/// </summary>
public class SeaShip : Unit
{
    public override Team Team => Team.Player;

    public SeaRoute Route { get; private set; }
    public int Tier { get; private set; }

    private SeaWorks works;
    private List<Vector3> path;
    private int next;
    private float pauseLeft, animTime, nextPuff;
    private SpriteRenderer body, barBack, barFill;
    private SeaLibrary.Look look;
    private bool lost;
    private float scale = 0.42f;

    private const float LoadSeconds = 3f;

    public static SeaShip Spawn(SeaWorks owner, SeaRoute route, List<Vector3> way, int tier, int health, float unitScale)
    {
        var go = new GameObject($"Ship {route.id}");
        go.transform.SetParent(owner.transform, false);
        var s = go.AddComponent<SeaShip>();
        s.Setup(owner, route, way, tier, health, unitScale);
        return s;
    }

    private void Setup(SeaWorks owner, SeaRoute route, List<Vector3> way, int tier, int health, float unitScale)
    {
        works = owner;
        Route = route;
        Tier = tier;
        path = way;
        scale = unitScale;
        maxHealth = this.health = health;
        attackPower = 0;
        detectionRange = 0.05f;
        attackRange = 0f;
        knockback = 0.2f;
        corpseTime = 1.5f;
        personalSpace = 1f;

        var lib = SeaLibrary.Load();
        look = lib != null && tier < lib.merchantShips.Count ? lib.merchantShips[tier] : null;
        body = new GameObject("Body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        body.sortingOrder = BuildingView.SortingOrder;
        if (look != null && look.frames.Count > 0)
        {
            body.sprite = look.frames[0];
            body.transform.localScale = Vector3.one * scale;
        }
        else
        {
            body.sprite = SimpleSprites.Square;
            body.color = new Color(0.55f, 0.4f, 0.25f);
            body.transform.localScale = new Vector3(1.6f, 0.5f, 1f);
        }
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.9f;
        col.offset = new Vector2(0f, 0.4f);
        barBack = Bar("BarBack", new Color(0, 0, 0, 0.6f), 6);
        barFill = Bar("BarFill", new Color(0.4f, 0.9f, 0.4f), 7);
        PlaceAlong(Mathf.Clamp01(route.progress));
    }

    private SpriteRenderer Bar(string name, Color color, int order)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = SimpleSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        sr.transform.localPosition = new Vector3(-0.5f, 1.6f, 0f);
        sr.transform.localScale = new Vector3(1f, 0.1f, 1f);
        sr.enabled = false;
        return sr;
    }

    private Vector3 Way(int k) => Route.toB ? path[k] : path[path.Count - 1 - k];

    private void PlaceAlong(float share)
    {
        if (path == null || path.Count < 2) return;
        float total = 0f;
        for (int i = 0; i + 1 < path.Count; i++) total += Vector2.Distance(path[i], path[i + 1]);
        float want = share * total, gone = 0f;
        for (int k = 0; k + 1 < path.Count; k++)
        {
            Vector3 p = Way(k), q = Way(k + 1);
            float d = Vector2.Distance(p, q);
            if (gone + d >= want || k + 2 == path.Count)
            {
                SetPosition(Vector3.Lerp(p, q, d > 0.0001f ? Mathf.Clamp01((want - gone) / d) : 1f));
                next = k + 1;
                return;
            }
            gone += d;
        }
    }

    private void SetPosition(Vector3 p)
    {
        p.z = BuildingView.DepthFor(p.y);
        transform.position = p;
    }

    /// <summary>A new way (a bridge cut the old one): used from the next trip on.</summary>
    public void SetPath(List<Vector3> way)
    {
        if (way == null || way.Count < 2) return;
        pendingPath = way;
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
            return;
        }
        if (next >= path.Count) { Arrive(); return; }
        var s = Settlement.Instance;
        float speed = (s != null ? s.SeaShipSpeed(Tier) * s.UnitSpeedFactor : SeaCatalog.ShipSpeed[Tier]) * (works != null ? works.LightSpeedAt(transform.position) : 1f);
        Vector3 pos = transform.position;
        Vector3 target = Way(next);
        target.z = pos.z;
        Vector3 moved = Vector3.MoveTowards(pos, target, speed * dt);
        if (Mathf.Abs(target.x - pos.x) > 0.02f) body.flipX = target.x < pos.x;
        SetPosition(moved);
        Frame(true);
        Effects(moved);
        if (Vector2.Distance(moved, target) < 0.1f) next++;
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
        string note = works != null ? works.ShipHome(this) : "";
        Route.toB = !Route.toB;
        Route.progress = 0f;
        next = 1;
        pauseLeft = LoadSeconds;
        if (pendingPath != null) { path = pendingPath; pendingPath = null; }
        if (!string.IsNullOrEmpty(note)) TradeWorks.Popup(transform.position + Vector3.up * 1.4f, note);
    }

    private void Frame(bool moving)
    {
        if (look == null || look.frames.Count == 0) return;
        int f = moving && look.frames.Count > 1 ? 1 + (int)(animTime * look.fps) % (look.frames.Count - 1) : 0;
        var sprite = look.frames[Mathf.Clamp(f, 0, look.frames.Count - 1)];
        if (body.sprite != sprite) body.sprite = sprite;
    }

    private void Effects(Vector3 at)
    {
        if (look == null || look.smoke == Vector2.zero || Time.time < nextPuff) return;
        nextPuff = Time.time + 0.5f;
        var off = look.smoke;
        if (body.flipX) off.x = -off.x;
        Puff.Spawn(at + (Vector3)off, 0.25f, Tier >= 5 ? new Color(0.7f, 0.9f, 1f, 0.45f) : new Color(0.8f, 0.8f, 0.8f, 0.7f), 1, 0.9f);
    }

    private void UpdateBar()
    {
        bool show = health < maxHealth;
        if (barBack.enabled != show) barBack.enabled = barFill.enabled = show;
        if (!show) return;
        float share = health / (float)Mathf.Max(1, maxHealth);
        barFill.transform.localScale = new Vector3(share, 0.1f, 1f);
        barFill.color = Color.Lerp(Color.red, Color.green, share);
    }

    protected override void Die()
    {
        if (!lost)
        {
            lost = true;
            if (works != null) works.ShipSunk(this);
        }
        if (barBack != null) barBack.enabled = barFill.enabled = false;
        if (body != null) body.color = new Color(0.5f, 0.45f, 0.45f, 0.8f);
        base.Die();
    }
}
