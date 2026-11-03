using UnityEngine;

/// <summary>
/// A short burst of chunky pixel blobs that swell, drift and fade: gun smoke at a muzzle, or the
/// flash and smoke of a cannonball or fire flask bursting.
/// </summary>
public class Puff : MonoBehaviour
{
    private SpriteRenderer[] blobs;
    private Vector3[] drift;
    private float start, duration, size;
    private Color color;

    private static Sprite blob;

    /// <summary>A small filled circle drawn at 8 px, point filtered so it stays pixel art.</summary>
    private static Sprite Blob
    {
        get
        {
            if (blob == null)
            {
                const int s = 8;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f;
                        px[y * s + x] = dx * dx + dy * dy <= (s / 2f) * (s / 2f) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                blob = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
                blob.name = "PuffBlob";
            }
            return blob;
        }
    }

    /// <summary>Gun smoke: a few grey blobs at the muzzle, drifting up.</summary>
    public static void Smoke(Vector3 at) => Spawn(at, 0.35f, new Color(0.85f, 0.85f, 0.82f, 0.85f), 4, 0.7f);

    /// <summary>A burst where a cannonball or flask lands: a hot flash inside a cloud of smoke.</summary>
    public static void Blast(Vector3 at, float radius, bool fire)
    {
        Spawn(at, radius * 0.9f, new Color(0.45f, 0.42f, 0.4f, 0.8f), 6, 0.8f);
        Spawn(at, radius * 0.6f, fire ? new Color(1f, 0.55f, 0.15f, 0.95f) : new Color(1f, 0.85f, 0.45f, 0.9f), 4, 0.35f);
    }

    public static void Spawn(Vector3 at, float size, Color color, int count, float duration)
    {
        var go = new GameObject("Puff");
        go.transform.position = new Vector3(at.x, at.y, -0.6f);
        var puff = go.AddComponent<Puff>();
        puff.start = Time.time;
        puff.duration = duration;
        puff.size = size;
        puff.color = color;
        puff.blobs = new SpriteRenderer[count];
        puff.drift = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var b = new GameObject("Blob");
            b.transform.SetParent(go.transform, false);
            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = Blob;
            sr.sortingOrder = 10;
            sr.color = color;
            puff.blobs[i] = sr;
            float a = Random.value * Mathf.PI * 2;
            puff.drift[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f + 0.5f, 0) * size * Random.Range(0.5f, 1f);
        }
        puff.Update();
    }

    private void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        for (int i = 0; i < blobs.Length; i++)
        {
            blobs[i].transform.localPosition = drift[i] * Mathf.Sqrt(t);
            // Grow in whole steps so the blobs stay crisp pixel art.
            float scale = Mathf.Round((0.6f + t) * size * 8f) / 8f / 0.25f;
            blobs[i].transform.localScale = new Vector3(scale, scale, 1f);
            blobs[i].color = new Color(color.r, color.g, color.b, color.a * (1f - t * t));
        }
    }
}
