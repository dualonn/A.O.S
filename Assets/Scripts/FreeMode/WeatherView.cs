using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Shows the weather on the world you're looking at: a light tint over the view, a pool of
/// pixel particles (rain, snow, ash, sand) that follow the camera, lightning flashes, and a
/// gentle seasonal tint on the ground. Cheap on purpose: one pool of sprites, moved on real time,
/// so a fast game doesn't cost more. Sets itself up in any scene with a free mode map.
/// </summary>
public class WeatherView : MonoBehaviour
{
    private const int MaxParticles = 150;
    private const int OverlayOrder = 90, ParticleOrder = 91;

    private Settlement settlement;
    private Camera cam;
    private SpriteRenderer overlay, flash, bolt;
    private readonly List<Transform> particles = new List<Transform>();
    private readonly List<SpriteRenderer> particleRenderers = new List<SpriteRenderer>();
    private readonly List<Vector2> particleOffsets = new List<Vector2>();   // in view units (0..1)
    private Transform aurora;
    private readonly List<SpriteRenderer> auroraBands = new List<SpriteRenderer>();
    private Sprite rainSprite, snowSprite, flakeSprite, sandSprite, pixelSprite, boltSprite, bandSprite;
    private WeatherKind shown = (WeatherKind)(-1);
    private int activeCount;
    private float flashLeft, boltLeft;
    private Color groundTarget = Color.white, groundNow = Color.white;
    private readonly Dictionary<Tilemap, Color> baseColors = new Dictionary<Tilemap, Color>();
    private float tilemapScanIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

    private static void TryCreate()
    {
        if (FindAnyObjectByType<FreeModeController>() == null || FindAnyObjectByType<WeatherView>() != null) return;
        new GameObject("Weather").AddComponent<WeatherView>();
    }

    private void Start()
    {
        settlement = Settlement.Instance;
        cam = Camera.main;
        if (settlement == null || cam == null) { enabled = false; return; }
        MakeSprites();

        overlay = MakeRenderer("Tint", pixelSprite, OverlayOrder);
        flash = MakeRenderer("Flash", pixelSprite, OverlayOrder + 2);
        flash.color = new Color(1f, 1f, 1f, 0f);
        bolt = MakeRenderer("Bolt", boltSprite, ParticleOrder + 1);
        bolt.enabled = false;
        for (int i = 0; i < MaxParticles; i++)
        {
            var sr = MakeRenderer("Drop", rainSprite, ParticleOrder);
            sr.enabled = false;
            particles.Add(sr.transform);
            particleRenderers.Add(sr);
            particleOffsets.Add(new Vector2(Random.value, Random.value));
        }
        aurora = new GameObject("Aurora").transform;
        aurora.SetParent(transform, false);
        for (int i = 0; i < 3; i++)
        {
            var sr = MakeRenderer("Band", bandSprite, OverlayOrder + 1);
            sr.transform.SetParent(aurora, false);
            auroraBands.Add(sr);
        }
        aurora.gameObject.SetActive(false);

        settlement.LightningStruck += OnLightning;
        settlement.WeatherChanged += OnWeatherSound;
        OnWeatherSound();
    }

    private void OnDestroy()
    {
        if (settlement != null) { settlement.LightningStruck -= OnLightning; settlement.WeatherChanged -= OnWeatherSound; }
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // ---------- Pixel sprites, made once ----------

    private static Sprite MakeSprite(int w, int h, System.Func<int, int, Color32> px, Vector2 pivot)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var data = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
        tex.SetPixels32(data);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 32f, 0, SpriteMeshType.FullRect);
    }

    private void MakeSprites()
    {
        var mid = new Vector2(0.5f, 0.5f);
        pixelSprite = MakeSprite(1, 1, (x, y) => new Color32(255, 255, 255, 255), mid);
        // a slanted streak of rain
        rainSprite = MakeSprite(3, 7, (x, y) => x == (6 - y) / 3 ? new Color32(190, 210, 255, 200) : new Color32(0, 0, 0, 0), mid);
        snowSprite = MakeSprite(2, 2, (x, y) => new Color32(250, 252, 255, 230), mid);
        flakeSprite = MakeSprite(3, 3, (x, y) => (x == 1 || y == 1) ? new Color32(245, 248, 255, 220) : new Color32(0, 0, 0, 0), mid);
        sandSprite = MakeSprite(3, 1, (x, y) => new Color32(226, 186, 120, 190), mid);
        // a jagged bolt, bottom at its pivot
        var path = new int[48];
        int bx = 4;
        var rng = new System.Random(7);
        for (int y = 47; y >= 0; y--) { path[y] = bx; if (y % 4 == 0) bx = Mathf.Clamp(bx + rng.Next(-2, 3), 1, 7); }
        boltSprite = MakeSprite(9, 48, (x, y) => x == path[y] ? new Color32(255, 255, 255, 255)
            : Mathf.Abs(x - path[y]) == 1 ? new Color32(170, 200, 255, 200) : new Color32(0, 0, 0, 0), new Vector2(0.5f, 0f));
        // a soft wavy band for the aurora
        bandSprite = MakeSprite(32, 8, (x, y) =>
        {
            float c = 3.5f + Mathf.Sin(x * 0.4f) * 2f;
            float a = Mathf.Clamp01(1f - Mathf.Abs(y - c) / 3f);
            return new Color32(120, 255, 190, (byte)(a * 120));
        }, mid);
    }

    // ---------- Each frame ----------

    private void LateUpdate()
    {
        if (cam == null) return;
        var w = settlement.CurrentWorld;
        var weather = (WeatherKind)w.weather;
        bool underground = DungeonRun.Active.Count > 0;
        bool zoomedFar = cam.orthographicSize > 40f;

        Vector3 c = cam.transform.position;
        float hh = cam.orthographicSize, hw = hh * cam.aspect;
        transform.position = new Vector3(c.x, c.y, 0f);

        if (weather != shown) SetWeather(weather);
        float real = Time.unscaledDeltaTime;
        float moving = Time.timeScale > 0f ? 1f : 0f;

        // The tint over the whole view.
        overlay.enabled = !underground && overlay.color.a > 0.001f;
        overlay.transform.localScale = new Vector3(hw * 2f * 32f, hh * 2f * 32f, 1f);

        // Particles: spread over the view in view units, drifting, wrapping round its edges.
        bool showParticles = !underground && !zoomedFar && activeCount > 0;
        Vector2 drift = Drift(weather) * real * moving;
        float size = Mathf.Clamp(hh / 9f, 0.5f, 3f);   // stay roughly the same size on screen
        for (int i = 0; i < particles.Count; i++)
        {
            bool on = showParticles && i < activeCount;
            if (particleRenderers[i].enabled != on) particleRenderers[i].enabled = on;
            if (!on) continue;
            var o = particleOffsets[i] + drift * (0.8f + (i % 5) * 0.1f);
            if (weather == WeatherKind.Snow || weather == WeatherKind.AshFall) o.x += Mathf.Sin(Time.unscaledTime * 1.3f + i) * 0.0008f * moving;
            o.x = Mathf.Repeat(o.x, 1f); o.y = Mathf.Repeat(o.y, 1f);
            particleOffsets[i] = o;
            particles[i].localPosition = new Vector3((o.x - 0.5f) * hw * 2f, (o.y - 0.5f) * hh * 2f, 0f);
            particles[i].localScale = new Vector3(size, size, 1f);
        }

        // Aurora: three green bands waving across the top of the view.
        if (aurora.gameObject.activeSelf != (weather == WeatherKind.Aurora && !underground))
            aurora.gameObject.SetActive(weather == WeatherKind.Aurora && !underground);
        if (aurora.gameObject.activeSelf)
        {
            for (int i = 0; i < auroraBands.Count; i++)
            {
                var t = auroraBands[i].transform;
                t.localPosition = new Vector3(Mathf.Sin(Time.unscaledTime * 0.2f + i * 2f) * hw * 0.2f, hh * (0.55f + i * 0.13f), 0f);
                t.localScale = new Vector3(hw * 2.4f, hh * 0.6f, 1f);
                var col = auroraBands[i].color;
                col.a = 0.5f + 0.35f * Mathf.Sin(Time.unscaledTime * 0.7f + i);
                auroraBands[i].color = col;
            }
        }

        // Lightning: a white flash that fades, and the bolt itself for a moment.
        if (flashLeft > 0f)
        {
            flashLeft -= real;
            flash.enabled = !underground;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashLeft / 0.3f) * 0.45f);
            flash.transform.localScale = overlay.transform.localScale;
        }
        else if (flash.enabled) flash.enabled = false;
        if (boltLeft > 0f) { boltLeft -= real; if (boltLeft <= 0f) bolt.enabled = false; }
        if (weather == WeatherKind.Storm && !underground && Random.value < real * 0.04f) flashLeft = 0.25f;   // far-off lightning

        // The ground's seasonal tint, eased in.
        tilemapScanIn -= real;
        if (tilemapScanIn <= 0f) { tilemapScanIn = 2f; groundTarget = GroundTint(settlement.CurrentSeason, weather); }
        if (groundNow != groundTarget)
        {
            groundNow = Color.Lerp(groundNow, groundTarget, Mathf.Clamp01(real * 0.6f));
            if (((Vector4)(groundNow - groundTarget)).sqrMagnitude < 0.00002f) groundNow = groundTarget;
            ApplyGround();
        }
        else if (tilemapScanIn == 2f) ApplyGround();   // new map regions appear as you explore
    }

    private static Vector2 Drift(WeatherKind k) => k switch
    {
        WeatherKind.Rain => new Vector2(-0.25f, -1.6f),
        WeatherKind.Storm => new Vector2(-0.6f, -2.2f),
        WeatherKind.Snow => new Vector2(-0.03f, -0.18f),
        WeatherKind.AshFall => new Vector2(0.02f, -0.12f),
        WeatherKind.Sandstorm => new Vector2(1.4f, -0.08f),
        _ => Vector2.zero,
    };

    private void SetWeather(WeatherKind k)
    {
        shown = k;
        Sprite sprite = rainSprite;
        Color tint = new Color(0, 0, 0, 0);
        activeCount = 0;
        switch (k)
        {
            case WeatherKind.Rain: activeCount = 110; tint = new Color(0.3f, 0.35f, 0.45f, 0.1f); break;
            case WeatherKind.Storm: activeCount = 150; tint = new Color(0.1f, 0.12f, 0.2f, 0.22f); break;
            case WeatherKind.Snow: activeCount = 110; sprite = snowSprite; tint = new Color(0.9f, 0.95f, 1f, 0.07f); break;
            case WeatherKind.Fog: tint = new Color(0.82f, 0.85f, 0.88f, 0.3f); break;
            case WeatherKind.HeatWave: tint = new Color(1f, 0.7f, 0.3f, 0.08f); break;
            case WeatherKind.Sandstorm: activeCount = 140; sprite = sandSprite; tint = new Color(0.85f, 0.65f, 0.35f, 0.3f); break;
            case WeatherKind.AshFall: activeCount = 80; sprite = snowSprite; tint = new Color(0.3f, 0.28f, 0.28f, 0.18f); break;
            case WeatherKind.Aurora: tint = new Color(0.25f, 0.6f, 0.5f, 0.07f); break;
        }
        overlay.color = tint;
        for (int i = 0; i < particleRenderers.Count; i++)
        {
            var sr = particleRenderers[i];
            sr.sprite = k == WeatherKind.Snow && i % 3 == 0 ? flakeSprite : sprite;
            sr.color = k == WeatherKind.AshFall ? new Color(0.35f, 0.33f, 0.33f, 0.9f) : Color.white;
        }
    }

    /// <summary>The weather's sound loop, under the biome's ambience.</summary>
    private void OnWeatherSound()
    {
        if (settlement == null) return;
        GameAudio.SetWeather(settlement.CurrentWeather switch
        {
            WeatherKind.Rain => "rain",
            WeatherKind.Storm => "storm",
            WeatherKind.Snow => "snow",
            WeatherKind.Sandstorm or WeatherKind.AshFall => "wind",
            _ => null,
        });
    }

    private void OnLightning(BuildingState b)
    {
        flashLeft = 0.35f;
        var controller = FreeModeController.Instance;
        var def = settlement.Database.GetBuilding(b.definitionId);
        if (controller == null || controller.Grid == null || def == null) return;
        Vector3 p = controller.Grid.CellToWorld(b.cell) + new Vector3(def.size.x * 0.5f, def.size.y * 0.6f, 0f);
        bolt.transform.position = new Vector3(p.x, p.y, 0f);
        GameAudio.Thunder(p);
        bolt.transform.localScale = Vector3.one * 2f;
        bolt.enabled = true;
        boltLeft = 0.25f;
        Puff.Spawn(p, 0.7f, new Color(1f, 0.95f, 0.7f, 0.9f), 6, 0.5f);
    }

    // ---------- The ground's seasonal tint ----------

    private static Color GroundTint(Season s, WeatherKind k)
    {
        Color c = s switch
        {
            Season.Spring => new Color(0.96f, 1f, 0.96f),
            Season.Summer => new Color(1f, 1f, 0.94f),
            Season.Autumn => new Color(1f, 0.92f, 0.8f),
            Season.Winter => new Color(0.84f, 0.9f, 1f),
            Season.DrySeason => new Color(1f, 0.95f, 0.86f),
            Season.WetSeason or Season.Rains => new Color(0.94f, 1f, 0.95f),
            _ => Color.white,
        };
        if (k == WeatherKind.Snow) c *= new Color(0.94f, 0.97f, 1f);
        if (k == WeatherKind.AshFall) c *= new Color(0.88f, 0.86f, 0.85f);
        c.a = 1f;
        return c;
    }

    private void ApplyGround()
    {
        var grid = FreeModeController.Instance != null ? FreeModeController.Instance.Grid : null;
        if (grid == null) return;
        foreach (var tm in grid.GetComponentsInChildren<Tilemap>(true))
        {
            if (!baseColors.TryGetValue(tm, out var baseColor))
            {
                baseColor = tm.color;
                baseColors[tm] = baseColor;
            }
            tm.color = baseColor * groundNow;
        }
    }
}
