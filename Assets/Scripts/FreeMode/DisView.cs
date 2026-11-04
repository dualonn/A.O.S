using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Natural disasters on the world you're looking at: floodwater over the flooded tiles, flames,
/// smoke and embers through a burning forest, lava and the smoking vent, cracks and dust in a
/// quake, sickly motes over sick houses, meteors streaking down (or bursting on a shield dome),
/// and fire crews' water arcing onto the fires. Pixel sprites made once in code, a few fixed pools
/// of renderers, nothing allocated per frame. Also the alert under the top bar while one is on,
/// with "Show me" to glide the camera there. Sets itself up in any scene with a free mode map.
/// </summary>
public class DisView : MonoBehaviour
{
    private const int GroundOrder = -18, SootOrder = -5, FlameOrder = -3, DropOrder = -2, SmokeOrder = 89, SkyOrder = 92;
    private const int GroundCap = 640, FlameCap = 200, PuffCap = 90, DropCap = 40, MeteorCap = 10;

    private Settlement settlement;
    private FreeModeController controller;
    private Camera cam;

    private Sprite[] water, lava, flame;
    private Sprite mud, burnt, rock, crater, crack, blob, drop, meteor, trail;

    private SpriteRenderer[] ground, flames;
    private int groundUsed, flamesUsed;

    private struct Mote { public SpriteRenderer sr; public Vector3 vel; public float born, life, size0, size1; public Color color; }
    private Mote[] puffs;
    private int nextPuff;

    private struct Drop { public SpriteRenderer sr; public Vector2 from, to; public float born, life; }
    private Drop[] drops;
    private int nextDrop, splashSeen;

    private struct Rock { public SpriteRenderer head, tail; public Vector2 at; public float impact; public bool live, blocked; }
    private Rock[] rocks;
    private readonly HashSet<float> rocksSeen = new HashSet<float>();

    private float refreshAt, smokeAt, shakeAt, moteAt, crackleAt;
    private readonly List<Vector2> burning = new List<Vector2>();
    private readonly List<Vector2> sick = new List<Vector2>();

    // the alert
    private RectTransform alert;
    private TextMeshProUGUI alertLabel;
    private Transform eventCard;
    private float uiSearchAt;
    private int alertId = -1;

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
        if (FindAnyObjectByType<FreeModeController>() == null || FindAnyObjectByType<DisView>() != null) return;
        new GameObject("Disasters").AddComponent<DisView>();
    }

    private void Start()
    {
        settlement = Settlement.Instance;
        controller = FreeModeController.Instance != null ? FreeModeController.Instance : FindAnyObjectByType<FreeModeController>();
        cam = Camera.main;
        if (settlement == null || cam == null) { enabled = false; return; }
        MakeSprites();
        ground = new SpriteRenderer[GroundCap];
        for (int i = 0; i < GroundCap; i++) ground[i] = Make("Ground", null, GroundOrder);
        flames = new SpriteRenderer[FlameCap];
        for (int i = 0; i < FlameCap; i++) flames[i] = Make("Flame", flame[0], FlameOrder);
        puffs = new Mote[PuffCap];
        for (int i = 0; i < PuffCap; i++) puffs[i].sr = Make("Smoke", blob, SmokeOrder);
        drops = new Drop[DropCap];
        for (int i = 0; i < DropCap; i++) drops[i].sr = Make("Water", drop, DropOrder);
        rocks = new Rock[MeteorCap];
        for (int i = 0; i < MeteorCap; i++) { rocks[i].tail = Make("Trail", trail, SkyOrder); rocks[i].head = Make("Meteor", meteor, SkyOrder + 1); }
        splashSeen = settlement.DisSplashCount;
        settlement.DisasterNews += OnNews;
    }

    private void OnDestroy()
    {
        if (settlement != null) settlement.DisasterNews -= OnNews;
    }

    private SpriteRenderer Make(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    private void OnNews(DisasterState d)
    {
        alertId = -1;   // rebuild the alert's words
        refreshAt = 0f;
        if (d.struck && !d.over && d.kind == "Flood") GameAudio.Play("river_splash", d.centre);
    }

    // ---------- Each frame ----------

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        float now = Time.time;
        float play = settlement.State.playTime;
        var list = settlement.DisastersHere;
        if (Time.unscaledTime >= refreshAt)
        {
            refreshAt = Time.unscaledTime + 0.2f;
            Refresh(list, play);
            UpdateAlert();
        }
        Emit(list, play, now);
        Splashes(now);
        Meteors(list, play);
        AnimatePuffs(now);
        AnimateDrops(now);
    }

    private Rect ViewRect()
    {
        float h = cam.orthographicSize, w = h * cam.aspect;
        var p = cam.transform.position;
        return new Rect(p.x - w - 2f, p.y - h - 2f, w * 2f + 4f, h * 2f + 4f);
    }

    private static uint Hash(int x, int y) => (uint)(x * 73856093 ^ y * 19349663);

    /// <summary>Put the pooled ground and flame sprites on the tiles in view.</summary>
    private void Refresh(List<DisasterState> list, float play)
    {
        var view = ViewRect();
        int frame = Mathf.FloorToInt(Time.time * 5f);
        groundUsed = 0;
        flamesUsed = 0;
        burning.Clear();
        sick.Clear();
        foreach (var d in list)
        {
            // scars first, so live tiles draw over them
            for (int i = 0; i < d.marks.Count; i++)
            {
                var t = d.marks[i];
                if (!view.Contains(new Vector2(t.x, t.y))) continue;
                float left = d.markTimes[i] - play;
                Sprite s = d.kind switch
                {
                    "Flood" => mud, "Wildfire" => burnt, "Volcano" => rock,
                    "Quake" => crack, "Meteors" => crater, _ => null,
                };
                if (s == null) continue;
                if (d.kind == "Volcano" && t.x == Mathf.FloorToInt(d.centre.x) && t.y == Mathf.FloorToInt(d.centre.y)) s = crater;
                // charred forest goes over the trees, darkening them; the rest lies on the ground
                Ground(t, s, Mathf.Clamp01(left / 12f), s == burnt ? SootOrder : GroundOrder);
            }
            if (d.over) continue;
            switch (d.kind)
            {
                case "Flood":
                    for (int i = 0; i < d.tiles.Count; i++)
                    {
                        var t = d.tiles[i];
                        if (!view.Contains(new Vector2(t.x, t.y))) continue;
                        uint h = Hash(t.x, t.y);
                        float fade = d.struck ? Mathf.Clamp01(d.left / 6f) : 1f;
                        Ground(t, water[((int)(h & 1)) * 4 + ((frame + (int)(h >> 3 & 3)) & 3)], fade);
                    }
                    break;
                case "Volcano":
                    for (int i = 0; i < d.tiles.Count; i++)
                    {
                        var t = d.tiles[i];
                        if (!view.Contains(new Vector2(t.x, t.y))) continue;
                        Ground(t, lava[(frame + (int)(Hash(t.x, t.y) & 3)) & 3], 1f);
                        if ((Hash(t.x, t.y) & 7) == 0) Flame(new Vector2(t.x + 0.5f, t.y + 0.3f), frame, 0.7f);
                    }
                    break;
                case "Wildfire":
                    for (int i = 0; i < d.tiles.Count; i++)
                    {
                        var t = d.tiles[i];
                        var p = new Vector2(t.x + 0.5f, t.y + 0.45f);
                        burning.Add(p);
                        if (!view.Contains(p)) continue;
                        Ground(t, burnt, 1f, SootOrder);
                        Flame(p, frame, 1f);
                        if ((Hash(t.x, t.y) & 1) == 0) Flame(p + new Vector2(0.28f, 0.18f), frame + 2, 0.75f);
                    }
                    foreach (int id in d.hit)
                    {
                        var b = Find(id, out var def);
                        if (b == null) continue;
                        for (int f = 0; f < Mathf.Max(2, def.size.x); f++)
                        {
                            var p = new Vector2(b.cell.x + 0.5f + f * (def.size.x - 1f) / Mathf.Max(1, Mathf.Max(2, def.size.x) - 1), b.cell.y + def.size.y * 0.75f);
                            burning.Add(p);
                            if (view.Contains(p)) Flame(p, frame + f, 1.15f);
                        }
                    }
                    break;
                case "Plague":
                    foreach (int id in d.hit)
                    {
                        var b = Find(id, out var def);
                        if (b != null) sick.Add(new Vector2(b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.8f));
                    }
                    break;
            }
        }
        for (int i = groundUsed; i < GroundCap; i++) if (ground[i].enabled) ground[i].enabled = false;
        for (int i = flamesUsed; i < FlameCap; i++) if (flames[i].enabled) flames[i].enabled = false;
    }

    private BuildingState Find(int id, out BuildingDefinition def)
    {
        def = null;
        foreach (var b in settlement.State.buildings)
        {
            if (b.instanceId != id) continue;
            def = settlement.Database.GetBuilding(b.definitionId);
            return def != null ? b : null;
        }
        return null;
    }

    private void Ground(Vector2Int t, Sprite s, float alpha, int order = GroundOrder)
    {
        if (groundUsed >= GroundCap || s == null) return;
        var sr = ground[groundUsed++];
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.transform.position = new Vector3(t.x + 0.5f, t.y + 0.5f, 0f);
        sr.color = new Color(1f, 1f, 1f, alpha);
        sr.enabled = true;
    }

    private void Flame(Vector2 p, int frame, float scale)
    {
        if (flamesUsed >= FlameCap) return;
        var sr = flames[flamesUsed++];
        sr.sprite = flame[(frame + (int)(Hash(Mathf.FloorToInt(p.x * 4f), Mathf.FloorToInt(p.y * 4f)) & 3)) & 3];
        sr.transform.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y) - 0.05f);
        sr.transform.localScale = Vector3.one * scale;
        sr.enabled = true;
    }

    // ---------- Smoke, dust, motes, shaking ----------

    private void Emit(List<DisasterState> list, float play, float now)
    {
        foreach (var d in list)
        {
            if (d.over) continue;
            switch (d.kind)
            {
                case "Wildfire":
                    if (burning.Count > 0 && now >= smokeAt)
                    {
                        smokeAt = now + Mathf.Lerp(0.18f, 0.05f, Mathf.Clamp01(burning.Count / 60f));
                        var p = burning[Random.Range(0, burning.Count)];
                        SpawnPuff(p + new Vector2(0f, 0.5f), new Vector3(Random.Range(-0.1f, 0.25f), Random.Range(0.6f, 0.9f)), 2.4f, 0.35f, 1.3f,
                            new Color(0.32f, 0.3f, 0.3f, 0.55f));
                        if (Random.value < 0.4f)
                            SpawnPuff(p + new Vector2(0f, 0.3f), new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1f, 1.6f)), 0.9f, 0.08f, 0.05f,
                                new Color(1f, 0.6f, 0.2f, 1f));   // an ember
                    }
                    if (burning.Count > 0 && now >= crackleAt)
                    {
                        crackleAt = now + 2.5f;
                        GameAudio.Play("fire_crackle", burning[Random.Range(0, burning.Count)], 0.6f);
                    }
                    break;
                case "Volcano":
                    if (now >= smokeAt)
                    {
                        smokeAt = now + (d.struck ? 0.07f : 0.3f);
                        var c = d.centre + new Vector2(0f, 0.6f);
                        SpawnPuff(c, new Vector3(Random.Range(-0.2f, 0.35f), Random.Range(0.8f, 1.4f)), d.struck ? 3.5f : 2.5f, 0.5f, d.struck ? 2.2f : 1.3f,
                            d.struck ? new Color(0.2f, 0.18f, 0.18f, 0.7f) : new Color(0.55f, 0.53f, 0.52f, 0.5f));
                        if (d.struck && Random.value < 0.5f)
                            SpawnPuff(c, new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 2.6f)), 1.2f, 0.12f, 0.08f, new Color(1f, 0.5f, 0.15f, 1f));
                    }
                    if (now >= shakeAt)
                    {
                        shakeAt = now + (d.struck ? 0.4f : 1.1f);
                        Shake(d.centre, d.struck ? 14f : 10f);
                    }
                    break;
                case "Quake":
                    if (d.struck && now >= shakeAt)
                    {
                        shakeAt = now + 0.12f;
                        Shake(d.centre, d.radius);
                        var p = d.centre + Random.insideUnitCircle * d.radius * 0.7f;
                        SpawnPuff(p, new Vector3(Random.Range(-0.3f, 0.3f), 0.3f), 1.2f, 0.5f, 1.1f, new Color(0.62f, 0.53f, 0.42f, 0.55f));
                    }
                    else if (!d.struck && d.warnedEarly && now >= shakeAt)
                    {
                        shakeAt = now + 3f;
                        Shake(d.centre, d.radius * 0.5f);   // faint foreshocks
                    }
                    break;
                case "Plague":
                    if (sick.Count > 0 && now >= moteAt)
                    {
                        moteAt = now + 0.22f;
                        var p = sick[Random.Range(0, sick.Count)] + new Vector2(Random.Range(-0.6f, 0.6f), 0f);
                        SpawnPuff(p, new Vector3(Random.Range(-0.1f, 0.1f), 0.35f), 2f, 0.12f, 0.2f, new Color(0.62f, 0.78f, 0.32f, 0.85f));
                    }
                    break;
                case "Flood":
                    if (d.struck && d.tiles.Count > 0 && now >= moteAt)
                    {
                        moteAt = now + 0.3f;
                        var t = d.tiles[Random.Range(0, d.tiles.Count)];
                        SpawnPuff(new Vector2(t.x + Random.value, t.y + Random.value), Vector3.zero, 0.7f, 0.05f, 0.35f, new Color(0.8f, 0.92f, 1f, 0.7f));   // ripples
                    }
                    break;
            }
        }
    }

    private void Shake(Vector2 centre, float radius)
    {
        if (controller == null) return;
        float r2 = radius * radius;
        foreach (var v in controller.Buildings)
        {
            if (v == null || v.State == null || v.Definition == null) continue;
            var c = new Vector2(v.State.cell.x + v.Definition.size.x * 0.5f, v.State.cell.y + v.Definition.size.y * 0.5f);
            if ((c - centre).sqrMagnitude <= r2) v.Shake();
        }
    }

    private void SpawnPuff(Vector2 at, Vector3 vel, float life, float size0, float size1, Color color)
    {
        ref var p = ref puffs[nextPuff];
        nextPuff = (nextPuff + 1) % PuffCap;
        p.sr.transform.position = new Vector3(at.x, at.y, -0.6f);
        p.vel = vel;
        p.born = Time.time;
        p.life = life;
        p.size0 = size0;
        p.size1 = size1;
        p.color = color;
        p.sr.color = color;
        p.sr.transform.localScale = Vector3.one * size0 * 4f;
        p.sr.enabled = true;
    }

    private void AnimatePuffs(float now)
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < PuffCap; i++)
        {
            ref var p = ref puffs[i];
            if (!p.sr.enabled) continue;
            float t = (now - p.born) / p.life;
            if (t >= 1f) { p.sr.enabled = false; continue; }
            var tr = p.sr.transform;
            tr.position += p.vel * dt;
            tr.localScale = Vector3.one * Mathf.Lerp(p.size0, p.size1, t) * 4f;   // the blob is a quarter tile across
            var c = p.color;
            c.a *= t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
            p.sr.color = c;
        }
    }

    // ---------- Fire crews' water ----------

    private void Splashes(float now)
    {
        int count = settlement.DisSplashCount;
        if (count - splashSeen > settlement.DisSplashes.Length) splashSeen = count - settlement.DisSplashes.Length;
        for (; splashSeen < count; splashSeen++)
        {
            var s = settlement.DisSplashes[splashSeen % settlement.DisSplashes.Length];
            for (int k = 0; k < 3; k++)
            {
                ref var d = ref drops[nextDrop];
                nextDrop = (nextDrop + 1) % DropCap;
                d.from = s.from + new Vector2(0f, 0.8f);
                d.to = s.to + Random.insideUnitCircle * 0.3f;
                d.born = now + k * 0.08f;
                d.life = 0.6f + Random.value * 0.15f;
                d.sr.enabled = true;
                d.sr.transform.position = new Vector3(d.from.x, d.from.y, -0.5f);
            }
            GameAudio.Play("splash", s.to, 0.35f);
        }
    }

    private void AnimateDrops(float now)
    {
        for (int i = 0; i < DropCap; i++)
        {
            ref var d = ref drops[i];
            if (!d.sr.enabled) continue;
            float t = (now - d.born) / d.life;
            if (t >= 1f)
            {
                d.sr.enabled = false;
                SpawnPuff(d.to, new Vector3(0f, 0.4f), 0.6f, 0.15f, 0.4f, new Color(0.9f, 0.92f, 0.95f, 0.6f));   // hiss of steam
                continue;
            }
            t = Mathf.Max(0f, t);
            var p = Vector2.Lerp(d.from, d.to, t);
            float arc = Mathf.Min(4f, Vector2.Distance(d.from, d.to) * 0.35f + 0.8f);
            p.y += Mathf.Sin(t * Mathf.PI) * arc;
            d.sr.transform.position = new Vector3(p.x, p.y, -0.5f);
        }
    }

    // ---------- Meteors ----------

    private static readonly Vector2 FallFrom = new Vector2(5f, 9f);

    private void Meteors(List<DisasterState> list, float play)
    {
        foreach (var d in list)
        {
            if (d.kind != "Meteors" || d.over) continue;
            for (int i = 0; i < d.tiles.Count; i++)
            {
                float impact = d.times[i];
                if (!rocksSeen.Add(impact)) continue;
                for (int r = 0; r < MeteorCap; r++)
                {
                    if (rocks[r].live) continue;
                    var t = d.tiles[i];
                    rocks[r].live = true;
                    rocks[r].at = new Vector2(t.x + 0.5f, t.y + 0.5f);
                    rocks[r].impact = impact;
                    rocks[r].blocked = settlement.DisBlocked(rocks[r].at);
                    rocks[r].head.enabled = rocks[r].tail.enabled = true;
                    break;
                }
            }
        }
        if (rocksSeen.Count > 200) rocksSeen.Clear();
        for (int r = 0; r < MeteorCap; r++)
        {
            ref var m = ref rocks[r];
            if (!m.live) continue;
            float k = 1f - (m.impact - play) / 1.1f;   // 0 at launch .. 1 at impact
            float stop = m.blocked ? 0.62f : 1f;          // a shield dome catches it in the air
            if (k >= stop)
            {
                m.live = false;
                m.head.enabled = m.tail.enabled = false;
                var at = m.at + FallFrom * (1f - stop);
                if (m.blocked)
                {
                    RingPulse.Spawn(at, 2.2f, new Color(0.45f, 0.85f, 1f, 0.9f));
                    SpawnPuff(at, new Vector3(0f, 0.2f), 0.6f, 0.4f, 1.2f, new Color(0.6f, 0.9f, 1f, 0.7f));
                    GameAudio.Play("force_field", at, 0.6f);
                }
                else
                {
                    Puff.Blast(at, 1.3f, true);
                    SpawnPuff(at, new Vector3(0f, 0.5f), 2.5f, 0.6f, 1.8f, new Color(0.3f, 0.27f, 0.25f, 0.6f));
                }
                continue;
            }
            k = Mathf.Max(0f, k);
            var p = m.at + FallFrom * (1f - k);
            m.head.transform.position = new Vector3(p.x, p.y, -0.7f);
            m.tail.transform.position = new Vector3(p.x, p.y, -0.69f);
        }
    }

    // ---------- The alert ----------

    private void UpdateAlert()
    {
        var d = settlement.DisasterAlert;
        if (alert == null)
        {
            if (d == null || Time.unscaledTime < uiSearchAt) return;
            uiSearchAt = Time.unscaledTime + 1f;
            BuildAlert();
            if (alert == null) return;
        }
        alert.gameObject.SetActive(d != null);
        if (d == null) return;
        alertLabel.text = settlement.DisStatus(d);
        alertId = d.id;
        // stay clear of an open event card
        float y = -104f;
        if (eventCard != null && eventCard.gameObject.activeInHierarchy)
            y = -106f - ((RectTransform)eventCard).rect.height - 6f;
        alert.anchoredPosition = new Vector2(0f, y);
    }

    private void BuildAlert()
    {
        var events = FindAnyObjectByType<EventPanel>();
        if (events == null || events.transform.parent == null) return;
        eventCard = events.transform.Find("EventCard");
        var root = events.transform.parent;
        var panel = UIBuilder.Panel("DisasterAlert", root, new Color(0.22f, 0.07f, 0.05f, 0.9f));
        alert = panel.rectTransform;
        UIBuilder.Anchor(alert, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(420, 36));
        var row = UIBuilder.Row("Row", alert, 8);
        UIBuilder.Stretch(row, 3);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 3, 0, 0);
        alertLabel = UIBuilder.Text("Label", row, "", 16);
        alertLabel.alignment = TextAlignmentOptions.MidlineLeft;
        alertLabel.textWrappingMode = TextWrappingModes.NoWrap;
        alertLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var show = UIBuilder.Button("ShowMe", row, ShowMe, out var showLabel, 28, 15);
        showLabel.text = "Show me";
        showLabel.alignment = TextAlignmentOptions.Center;
        show.GetComponent<LayoutElement>().preferredWidth = 92;
        alert.gameObject.SetActive(false);
    }

    private void ShowMe()
    {
        var d = settlement.DisasterAlert;
        if (d == null || controller == null) return;
        Vector2 at = d.centre;
        if (d.kind == "Wildfire" && d.tiles.Count > 0) at = new Vector2(d.tiles[0].x + 0.5f, d.tiles[0].y + 0.5f);
        controller.PanTo(new Vector3(at.x, at.y, 0f));
        RingPulse.Spawn(at, 3f, new Color(1f, 0.55f, 0.3f, 0.9f));
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

    private static int H(int x, int y, int salt) => (int)((uint)(x * 374761393 + y * 668265263 + salt * 982451653) >> 7 & 1023);

    private void MakeSprites()
    {
        var mid = new Vector2(0.5f, 0.5f);
        var clear = new Color32(0, 0, 0, 0);
        // Floodwater: muddy blue, ripples sliding across, two patterns so a lake of it doesn't tile.
        water = new Sprite[8];
        for (int v = 0; v < 2; v++)
            for (int f = 0; f < 4; f++)
            {
                int vv = v, ff = f;
                water[v * 4 + f] = MakeSprite(32, 32, (x, y) =>
                {
                    int h = H(x, y, 3 + vv);
                    int band = (y + ff * 2 + (x / 8 + vv * 3) % 3) % 8;
                    if (band == 0 && ((x + y * 3 + vv * 5) % 11) < 6) return new Color32(176, 214, 236, 200);
                    if (h < 30) return new Color32(92, 118, 140, 175);   // silt
                    return h < 300 ? new Color32(58, 104, 160, 170) : new Color32(66, 116, 172, 165);
                }, mid);
            }
        mud = MakeSprite(32, 32, (x, y) =>
        {
            int h = H(x, y, 11);
            if (h < 40) return new Color32(120, 160, 190, 140);   // puddles left behind
            return h < 500 ? new Color32(98, 76, 52, 120) : new Color32(112, 88, 60, 110);
        }, mid);
        // Burnt forest: charred ground with embers and stumps, and soot rising over where the trees stand.
        burnt = MakeSprite(32, 48, (x, y) =>
        {
            int h = H(x, y, 13);
            if (y >= 32)
            {
                // soot over the tree tops, thinning upwards
                int a = 150 - (y - 32) * 9;
                return a > 0 && h < 900 ? new Color32(30, 26, 24, (byte)a) : clear;
            }
            if (h < 8) return new Color32(255, 128, 48, 230);                    // embers
            if (h < 60) return new Color32(122, 116, 110, 180);                   // ash
            if ((x == 9 || x == 10) && y >= 12 && y <= 17) return new Color32(26, 20, 18, 230);   // a charred stump
            if ((x == 22 || x == 23) && y >= 18 && y <= 22) return new Color32(26, 20, 18, 230);
            return h < 600 ? new Color32(38, 32, 30, 170) : new Color32(52, 44, 40, 160);
        }, new Vector2(0.5f, 16f / 48f));
        lava = new Sprite[4];
        for (int f = 0; f < 4; f++)
        {
            int ff = f;
            lava[f] = MakeSprite(32, 32, (x, y) =>
            {
                // crusted plates over glowing rock: a coarse cell grid with bright seams that pulse
                int cx = (x + (y / 8 % 2) * 4) / 8, cy = y / 6;
                int seamX = (x + (y / 8 % 2) * 4) % 8, seamY = y % 6;
                bool seam = seamX == 0 || seamY == 0;
                int h = H(cx, cy, 17);
                if (seam) return ((x + y + ff) % 4 == 0) ? new Color32(255, 236, 120, 245) : new Color32(255, 150, 40, 240);
                if (h < 220) return new Color32(232, 92, 30, 238);
                int glow = (h + ff * 97) % 1024;
                return glow < 120 ? new Color32(200, 70, 28, 236) : new Color32(82, 34, 28, 238);
            }, mid);
        }
        rock = MakeSprite(32, 32, (x, y) =>
        {
            int cx = (x + (y / 8 % 2) * 4) % 8, cy = y % 6;
            if (cx == 0 || cy == 0) return H(x, y, 19) < 200 ? new Color32(150, 50, 30, 200) : new Color32(34, 30, 34, 220);
            return H(x, y, 21) < 400 ? new Color32(58, 52, 58, 215) : new Color32(70, 64, 70, 210);
        }, mid);
        crater = MakeSprite(32, 32, (x, y) =>
        {
            float dx = x - 15.5f, dy = (y - 15.5f) * 1.4f, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 13f) return clear;
            if (d > 11f) return new Color32(110, 92, 74, 190);   // thrown-up rim
            if (d < 4f && H(x, y, 23) < 120) return new Color32(255, 140, 50, 220);
            return d < 7f ? new Color32(30, 24, 24, 220) : new Color32(56, 46, 42, 205);
        }, mid);
        crack = MakeSprite(32, 32, (x, y) =>
        {
            // a jagged split running across the tile
            int line = 16 + Mathf.RoundToInt(Mathf.Sin(x * 0.45f) * 3f + Mathf.Sin(x * 1.3f) * 1.5f);
            if (Mathf.Abs(y - line) == 0) return new Color32(22, 18, 18, 230);
            if (Mathf.Abs(y - line) == 1 && H(x, y, 29) < 500) return new Color32(70, 58, 48, 170);
            if (x > 18 && x < 24 && Mathf.Abs(y - (line - (x - 18) * 2)) == 0) return new Color32(22, 18, 18, 210);   // a branch
            return clear;
        }, mid);
        flame = new Sprite[4];
        var fire = PixelPalette.Fire;
        for (int f = 0; f < 4; f++)
        {
            int ff = f;
            flame[f] = MakeSprite(14, 18, (x, y) =>
            {
                // three tongues of flame that lick up and sway
                float best = 0f;
                for (int t = 0; t < 3; t++)
                {
                    float cx = 3.5f + t * 3.5f + Mathf.Sin((ff + t * 1.7f) * 1.6f) * 0.8f;
                    float top = (t == 1 ? 16f : 11f) - ((ff + t) % 4 == 0 ? 2f : 0f);
                    float k = y / top;
                    if (k > 1f) continue;
                    float half = 2.6f * (1f - k * k) + 0.4f;
                    float d = Mathf.Abs(x + 0.5f - cx - k * k * Mathf.Sin(ff + t) * 1.2f);
                    if (d <= half) best = Mathf.Max(best, 1f - Mathf.Max(k, d / half * 0.6f));
                }
                if (best <= 0f) return clear;
                return best > 0.75f ? fire[3] : best > 0.5f ? fire[2] : best > 0.25f ? fire[1] : fire[0];
            }, new Vector2(0.5f, 0.05f));
        }
        blob = MakeSprite(8, 8, (x, y) =>
        {
            float dx = x + 0.5f - 4f, dy = y + 0.5f - 4f;
            return dx * dx + dy * dy <= 16f ? new Color32(255, 255, 255, 255) : clear;
        }, mid);
        drop = MakeSprite(3, 3, (x, y) => x == 1 || y == 1 ? new Color32(150, 210, 255, 255) : new Color32(90, 160, 230, 200), mid);
        meteor = MakeSprite(8, 8, (x, y) =>
        {
            float dx = x + 0.5f - 4f, dy = y + 0.5f - 4f, d = dx * dx + dy * dy;
            if (d > 12f) return clear;
            return d < 4f ? new Color32(255, 246, 200, 255) : d < 8f ? new Color32(255, 170, 60, 255) : new Color32(150, 70, 40, 255);
        }, mid);
        trail = MakeSprite(24, 40, (x, y) =>
        {
            // a smoky, fiery streak along the line it falls from (up and to the right)
            float along = y / 40f;
            float cx = 2f + along * 20f;
            float d = Mathf.Abs(x - cx);
            float w = 1f + along * 2.5f;
            if (d > w) return clear;
            byte a = (byte)(230 * (1f - along));
            return along < 0.3f ? new Color32(255, 200, 90, a) : along < 0.6f ? new Color32(230, 110, 50, a) : new Color32(120, 110, 110, a);
        }, new Vector2(0.1f, 0.05f));
    }
}
