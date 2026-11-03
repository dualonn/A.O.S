using UnityEngine;

/// <summary>
/// An arrow, bolt or anything thrown, flying to a unit or point. Each kind has its own arc:
/// arrows and javelins are lobbed, crossbow and rune bolts fly dead straight.
/// Damage is dealt when it arrives, to whoever it was aimed at if they're still alive.
/// </summary>
public class Projectile : MonoBehaviour
{
    private Unit target;
    private Vector3 start;
    private Vector3 end;
    private float duration;
    private float elapsed;
    private int damage;
    private Unit source;
    private System.Action onArrive;
    private float arc;
    private float splash;
    private bool fire;
    private Team team;

    /// <summary>A bow's arc: the height of the flight as a share of its length.</summary>
    public const float BowArc = 0.15f;

    /// <summary>Fire at a unit (tracks it in flight).</summary>
    /// <param name="splash">Blast radius (0 = hits only its target): everyone of the other side
    /// within it takes part of the damage too (cannonballs, fire flasks).</param>
    /// <param name="team">The shooter's side, when there's no shooting unit (towers are yours).</param>
    public static Projectile Fire(Sprite sprite, Vector3 from, Unit target, float speed, int damage, Unit source, float arc = BowArc,
        float splash = 0f, bool fire = false, Team team = Team.Player)
    {
        var p = Create(sprite, from, target.transform.position + Vector3.up * 0.4f, speed, arc);
        p.target = target;
        p.damage = damage;
        p.source = source;
        p.splash = splash;
        p.fire = fire;
        p.team = source != null ? source.Team : team;
        return p;
    }

    /// <summary>Share of the damage that others caught in a blast take.</summary>
    private const float SplashShare = 0.6f;

    /// <summary>Fire at a point; 'onArrive' runs when it lands (e.g. hitting a building).</summary>
    public static Projectile FireAt(Sprite sprite, Vector3 from, Vector3 to, float speed, System.Action onArrive, float arc = BowArc)
    {
        var p = Create(sprite, from, to, speed, arc);
        p.onArrive = onArrive;
        return p;
    }

    private static Projectile Create(Sprite sprite, Vector3 from, Vector3 to, float speed, float arc)
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 9;
        var p = go.AddComponent<Projectile>();
        p.start = from;
        p.end = to;
        p.arc = Mathf.Max(0f, arc);
        p.duration = Mathf.Max(0.12f, Vector2.Distance(from, to) / Mathf.Max(0.1f, speed));
        go.transform.position = from;
        return p;
    }

    private void Update()
    {
        if (target != null && target.IsAlive)
        {
            end = target.transform.position + Vector3.up * 0.4f;
        }
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float length = Vector2.Distance(start, end);
        Vector3 pos = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * length * arc);
        float t2 = Mathf.Min(1f, t + 0.05f);
        Vector3 ahead = Vector3.Lerp(start, end, t2) + Vector3.up * (Mathf.Sin(t2 * Mathf.PI) * length * arc);
        Vector3 dir = ahead - pos;
        if (dir.sqrMagnitude > 1e-6f)
        {
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
        transform.position = new Vector3(pos.x, pos.y, -0.5f);

        if (t >= 1f)
        {
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(damage, source);
            }
            if (splash > 0f)
            {
                Puff.Blast(end, splash, fire);
                int share = Mathf.Max(1, Mathf.RoundToInt(damage * SplashShare));
                var caught = new System.Collections.Generic.List<Unit>();
                foreach (var u in Unit.Living)
                    if (u != null && u != target && u.IsAlive && u.Team != team && Vector2.Distance(u.transform.position, end) <= splash) caught.Add(u);
                foreach (var u in caught) u.TakeDamage(share, source);
            }
            onArrive?.Invoke();
            Destroy(gameObject);
        }
    }
}
