using UnityEngine;

/// <summary>
/// An arrow (or any thrown thing) flying in a shallow arc to a unit or point.
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

    /// <summary>Fire at a unit (tracks it in flight).</summary>
    public static Projectile Fire(Sprite sprite, Vector3 from, Unit target, float speed, int damage, Unit source)
    {
        var p = Create(sprite, from, target.transform.position + Vector3.up * 0.4f, speed);
        p.target = target;
        p.damage = damage;
        p.source = source;
        return p;
    }

    /// <summary>Fire at a point; 'onArrive' runs when it lands (e.g. hitting a building).</summary>
    public static Projectile FireAt(Sprite sprite, Vector3 from, Vector3 to, float speed, System.Action onArrive)
    {
        var p = Create(sprite, from, to, speed);
        p.onArrive = onArrive;
        return p;
    }

    private static Projectile Create(Sprite sprite, Vector3 from, Vector3 to, float speed)
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 9;
        var p = go.AddComponent<Projectile>();
        p.start = from;
        p.end = to;
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
        float arc = Mathf.Sin(t * Mathf.PI) * Vector2.Distance(start, end) * 0.15f;
        Vector3 pos = Vector3.Lerp(start, end, t) + Vector3.up * arc;
        Vector3 ahead = Vector3.Lerp(start, end, Mathf.Min(1f, t + 0.05f)) + Vector3.up * Mathf.Sin(Mathf.Min(1f, t + 0.05f) * Mathf.PI) * Vector2.Distance(start, end) * 0.15f;
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
            onArrive?.Invoke();
            Destroy(gameObject);
        }
    }
}
