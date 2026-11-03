using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The star map: the galaxy round your worlds. Drag to pan, scroll to zoom. Click a star for its
/// system (its worlds on their orbits), click a world for what's known about it: its kind,
/// traits, moons, how far it is and how long a ship takes, and whether you've settled it.
/// Stars beyond your ships' reach are dim; systems you haven't scanned show only their star.
/// </summary>
public class StarMapPanel : MonoBehaviour, IDragHandler, IScrollHandler
{
    private Settlement settlement;
    private RectTransform panel, viewport, content, info;
    private TextMeshProUGUI title, infoText;
    private readonly List<GameObject> marks = new List<GameObject>();
    private Vector2 centre;          // light-years at the middle of the view
    private float zoom = 9f;         // pixels per light-year
    private StarSystem openSystem;
    private Planet picked;
    private bool showAnomaly;
    private float redrawAt;
    private static Sprite dot;

    /// <summary>What a world button on the info card does (set by the systems that use the map, e.g. colony ships).</summary>
    public static readonly List<(string label, System.Func<Planet, bool> shown, System.Action<Planet> act)> Actions =
        new List<(string, System.Func<Planet, bool>, System.Action<Planet>)>();

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static StarMapPanel Create(Transform root, Settlement settlement)
    {
        var go = new GameObject("StarMap", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var map = go.AddComponent<StarMapPanel>();
        map.settlement = settlement;
        map.Build((RectTransform)go.transform);
        map.panel.gameObject.SetActive(false);
        return map;
    }

    private void Build(RectTransform rt)
    {
        panel = rt;
        UIBuilder.Anchor(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        panel.offsetMin = new Vector2(240, 80); panel.offsetMax = new Vector2(-240, -70);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.015f, 0.015f, 0.04f, 1f);
        title = UIBuilder.Text("Title", panel, "", 24);
        UIBuilder.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(-120, 34));
        title.alignment = TextAlignmentOptions.Center;
        var close = UIBuilder.Button("Close", panel, Toggle, out var closeLabel, 34, 18);
        UIBuilder.Anchor((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -6), new Vector2(40, 34));
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;
        var back = UIBuilder.Button("Back", panel, () => { openSystem = null; picked = null; Redraw(); }, out var backLabel, 34, 16);
        UIBuilder.Anchor((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -6), new Vector2(110, 34));
        backLabel.text = "< Galaxy"; backLabel.alignment = TextAlignmentOptions.Center;

        viewport = UIBuilder.Rect("View", panel);
        UIBuilder.Stretch(viewport);
        viewport.offsetMin = new Vector2(8, 8); viewport.offsetMax = new Vector2(-330, -46);
        viewport.gameObject.AddComponent<RectMask2D>();
        var catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0, 0, 0, 0.01f);   // catches drags and scrolls
        content = UIBuilder.Rect("Content", viewport);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);

        info = UIBuilder.VerticalPanel("Info", panel, 12, 8);
        UIBuilder.Anchor(info, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, -20), new Vector2(316, -64));
        infoText = UIBuilder.Text("Text", info, "", 17);
        infoText.textWrappingMode = TextWrappingModes.Normal;
        infoText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
    }

    public void Toggle()
    {
        bool open = !panel.gameObject.activeSelf;
        panel.gameObject.SetActive(open);
        if (open)
        {
            // Over the side panels, but under the tooltip.
            panel.SetAsLastSibling();
            var tip = panel.parent.Find("Tooltip");
            if (tip != null) tip.SetAsLastSibling();
            Galaxy.Init(settlement.State.mapSeed);
            var here = settlement.CurrentPlanet;
            if (here != null) centre = here.system.position;
            Redraw();
        }
    }

    /// <summary>Open the map on a system (and a body in it, if given).</summary>
    public void Show(StarSystem system, Planet body = null)
    {
        if (!IsOpen) Toggle();
        openSystem = system;
        picked = body;
        Redraw();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { if (openSystem != null) { openSystem = null; picked = null; Redraw(); } else Toggle(); }
        if (Time.unscaledTime >= redrawAt) Redraw();   // ships move, colonies grow
    }

    public void OnDrag(PointerEventData e)
    {
        if (openSystem != null) return;
        centre -= e.delta / Mathf.Max(0.5f, zoom) / panel.lossyScale.x;
        Redraw();
    }

    public void OnScroll(PointerEventData e)
    {
        if (openSystem != null) return;
        zoom = Mathf.Clamp(zoom * (e.scrollDelta.y > 0 ? 1.2f : 1f / 1.2f), 1.2f, 40f);
        Redraw();
    }

    // ---------- Drawing ----------

    private void Redraw()
    {
        redrawAt = Time.unscaledTime + 1f;
        foreach (var m in marks) Destroy(m);
        marks.Clear();
        if (openSystem != null) DrawSystem(openSystem); else DrawGalaxy();
        ShowInfo();
    }

    private void DrawGalaxy()
    {
        title.text = "<b>The Stars</b>  <size=70%><color=#aaa>drag to look round, scroll to zoom, click a star</color></size>";
        Vector2 size = viewport.rect.size;
        float halfW = size.x * 0.5f / zoom, halfH = size.y * 0.5f / zoom;
        var min = new Vector2Int(Mathf.FloorToInt((centre.x - halfW) / Galaxy.SectorSize) - 1, Mathf.FloorToInt((centre.y - halfH) / Galaxy.SectorSize) - 1);
        var max = new Vector2Int(Mathf.FloorToInt((centre.x + halfW) / Galaxy.SectorSize) + 1, Mathf.FloorToInt((centre.y + halfH) / Galaxy.SectorSize) + 1);
        float reach = settlement.ShipReach;
        // The reach of your ships: a faint ring round each settled system.
        foreach (var home in settlement.SettledSystems())
        {
            float r = (reach + 0.5f) * zoom * 2f;
            if (r > 2f) Mark(home.position, r, new Color(0.3f, 0.6f, 1f, 0.08f), null);
        }
        for (int sy = min.y; sy <= max.y; sy++)
            for (int sx = min.x; sx <= max.x; sx++)
                foreach (var s in Galaxy.SystemsIn(new Vector2Int(sx, sy)))
                {
                    bool inReach = settlement.InReach(s);
                    bool known = settlement.IsScanned(s);
                    bool settled = settlement.HasColonyIn(s);
                    var c = StarColour(s.star);
                    if (!inReach) c = Color.Lerp(c, Color.black, 0.6f);
                    var sys = s;
                    if (settlement.PendingAnomaly(s) != AnomalyKind.None) Mark(s.position, 22, new Color(1f, 0.8f, 0.3f, 0.35f), null);
                    Mark(s.position, settled ? 18 : s.star == StarType.BlueGiant ? 14 : 11, c, () => { openSystem = sys; picked = null; Redraw(); });
                    if (zoom > 5f && (known || settled || inReach))
                        Label(s.position + new Vector2(0, -1.2f), settled ? $"<b>{s.name}</b>" : s.name, settled ? new Color(0.6f, 1f, 0.7f) : new Color(0.75f, 0.75f, 0.8f));
                }
    }

    private void DrawSystem(StarSystem s)
    {
        bool known = settlement.IsScanned(s);
        title.text = $"<b>{s.name}</b>  <size=70%><color=#aaa>{StarName(s.star)}{(known ? "" : " · not yet scanned")}</color></size>";
        Vector2 size = viewport.rect.size;
        float maxR = Mathf.Min(size.x, size.y) * 0.46f;
        AddLocal(Vector2.zero, 34, StarColour(s.star), null);
        var anomaly = settlement.PendingAnomaly(s);
        if (anomaly != AnomalyKind.None)
        {
            Vector2 at = new Vector2(-maxR * 0.75f, maxR * 0.75f);
            AddLocal(at, 26, new Color(1f, 0.8f, 0.3f, 0.5f), () => { picked = null; showAnomaly = true; ShowInfo(); });
            AddLocal(at, 12, new Color(1f, 0.95f, 0.7f), () => { picked = null; showAnomaly = true; ShowInfo(); });
            var label = UIBuilder.Text("Anomaly", content, Galaxy.Describe(anomaly).title, 14, TextAlignmentOptions.Center);
            label.color = new Color(1f, 0.85f, 0.5f);
            label.rectTransform.anchoredPosition = at + new Vector2(0, -24);
            label.rectTransform.sizeDelta = new Vector2(160, 20);
            marks.Add(label.gameObject);
        }
        foreach (var p in s.bodies)
        {
            float r = 40 + p.orbit * (maxR - 40);
            AddRing(r);
            float angle = p.index * 2.39996f + (float)(Galaxy.Hash(s.key.x, s.key.y, p.index, 3) % 628) / 100f;
            Vector2 at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            var planet = p;
            bool settled = settlement.WorldIndexOf(p) >= 0;
            Color c = known || settled ? BodyColour(p) : new Color(0.35f, 0.35f, 0.4f);
            float sz = p.kind == BodyKind.GasGiant ? 22 : p.kind == BodyKind.AsteroidBelt ? 8 : p.kind == BodyKind.Moon ? 9 : 14;
            AddLocal(at, sz, c, () => { picked = planet; showAnomaly = false; ShowInfo(); });
            if (settled) AddLocal(at + new Vector2(sz * 0.6f, sz * 0.6f), 7, new Color(0.5f, 1f, 0.6f), null);
            var label = UIBuilder.Text("Name", content, known || settled ? p.name : "?", 14, TextAlignmentOptions.Center);
            label.rectTransform.anchoredPosition = at + new Vector2(0, -sz - 8);
            label.rectTransform.sizeDelta = new Vector2(140, 20);
            marks.Add(label.gameObject);
        }
        content.anchoredPosition = Vector2.zero;
    }

    private void ShowInfo()
    {
        foreach (Transform child in info) if (child != infoText.transform) Destroy(child.gameObject);
        if (picked == null && showAnomaly && openSystem != null && settlement.PendingAnomaly(openSystem) is AnomalyKind pending && pending != AnomalyKind.None)
        {
            var a = Galaxy.Describe(pending);
            infoText.text = $"<b><size=120%>{a.title}</size></b>\n<color=#fc8>at {openSystem.name}</color>\n\n{a.text}\n\n<color=#aaa>Rewards go to the world you're on.</color>";
            var sys = openSystem;
            foreach (var (label, choice) in new[] { (a.choiceA, 0), (a.choiceB, 1) })
            {
                int c = choice;
                var b = UIBuilder.Button("Choice", info, () => { settlement.ResolveAnomaly(sys, c); showAnomaly = false; Redraw(); }, out var bl, 44, 16);
                bl.text = label; bl.alignment = TextAlignmentOptions.Center;
            }
            return;
        }
        if (picked == null)
        {
            if (openSystem == null)
            {
                var codex = settlement.State.codex;
                string found = codex.Count == 0 ? "" : "\n\n<b>Discoveries</b> <color=#aaa>(" + codex.Count + ")</color>\n<size=85%>"
                    + string.Join("\n", codex.GetRange(Mathf.Max(0, codex.Count - 5), Mathf.Min(5, codex.Count))) + "</size>";
                infoText.text = $"<b>Your worlds</b>\n{settlement.WorldsSummary()}\n\n<color=#aaa>Ships reach {settlement.ShipReach:0.#} light-years from your settled systems, at speed x{settlement.ShipSpeed:0.##}. Research Navigation and Star Drives to go further and faster. Gold rings mark anomalies your probes have found.</color>{found}";
            }
            else infoText.text = "<color=#aaa>Click a world for what's known about it.</color>";
            return;
        }
        var p = picked;
        bool known = settlement.IsScanned(p.system);
        var profile = p.CanLand && known ? WorldProfile.Get(p.profile) : null;
        string kind = p.kind switch { BodyKind.GasGiant => "Gas giant", BodyKind.AsteroidBelt => "Asteroid belt", BodyKind.Moon => "Moon", _ => "World" };
        var sb = new System.Text.StringBuilder();
        sb.Append($"<b><size=120%>{(known ? p.name : "Unknown world")}</size></b>\n<color=#9cf>{kind}{(profile != null ? " · " + profile.displayName : "")}</color>\n\n");
        if (!known) sb.Append("<color=#aaa>Scan this system to learn more.</color>\n");
        else
        {
            if (profile != null) sb.Append(profile.description).Append("\n\n");
            foreach (var t in p.traits)
            {
                string effect = Settlement.TraitEffect(t);
                sb.Append($"<b>{t}</b>{(effect.Length > 0 ? $" <size=85%><color=#aaa>({effect})</color></size>" : "")}\n");
            }
            if (p.moons > 0) sb.Append($"<b>Moons:</b> {p.moons}\n");
        }
        if (settlement.HasOrbitalMine(p)) sb.Append("\n<color=#8f8>Your orbital mine works here.</color>\n");
        else if (p.kind == BodyKind.AsteroidBelt || p.kind == BodyKind.GasGiant)
            sb.Append($"\n<color=#aaa>Can be mined from orbit ({(p.kind == BodyKind.AsteroidBelt ? "starmetal, iron, stone" : "power cells, mana")}) from a Starport world in this system, with Astro-mining.</color>\n");
        int world = settlement.WorldIndexOf(p);
        if (world >= 0) sb.Append($"\n<color=#8f8>You have {(settlement.IsWorldFounded(settlement.State.worlds[world]) ? "a colony" : "a landing")} here.</color>\n");
        var from = settlement.CurrentPlanet;
        if (from != null && from != p)
            sb.Append($"\n<color=#aaa>From {from.name}: {(from.system == p.system ? "in this system" : Galaxy.Distance(from.system, p.system).ToString("0.#") + " light-years")}, " +
                      $"about {FormatTime(Galaxy.TravelSeconds(from, p, settlement.ShipSpeed))} by ship{(settlement.InReach(p.system) ? "" : " <color=#f88>(out of reach)</color>")}.</color>\n");
        infoText.text = sb.ToString();
        if (world >= 0 && world != settlement.State.currentWorld)
        {
            var go = UIBuilder.Button("Visit", info, () => { Toggle(); settlement.SwitchWorld(world); }, out var l, 40, 18);
            l.text = "Go to this world"; l.alignment = TextAlignmentOptions.Center;
            if (settlement.IsWorldFounded(settlement.State.worlds[world])) CargoRoutes(world);
        }
        foreach (var (label, shown, act) in Actions)
        {
            if (!shown(p)) continue;
            var a = act;
            var b = UIBuilder.Button(label, info, () => { a(p); Redraw(); }, out var l2, 40, 18);
            l2.text = label; l2.alignment = TextAlignmentOptions.Center;
        }
    }

    /// <summary>The cargo routes from the world you're on to this one: what each carries, how much, and a way to add more.</summary>
    private void CargoRoutes(int to)
    {
        int from = settlement.State.currentWorld;
        float trip = settlement.RoundTripSeconds(new CargoRoute { fromWorld = from, toWorld = to });
        var header = UIBuilder.Text("Cargo", info, $"<b>Cargo from {settlement.CurrentWorld.name}</b>  <size=80%><color=#aaa>(a freighter every {FormatTime(trip)}; needs a Starport at both ends and 10 Power Cells a trip)</color></size>", 15);
        header.textWrappingMode = TextWrappingModes.Normal;
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        foreach (var route in settlement.RoutesBetween(from, to))
        {
            var r = route;
            var row = UIBuilder.Row("Route", info, 4);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            void Btn(string text, UnityEngine.Events.UnityAction act, float width)
            {
                var b = UIBuilder.Button(text, row, () => { act(); ShowInfo(); }, out var lb, 30, 15);
                b.GetComponent<LayoutElement>().preferredWidth = width;
                lb.text = text; lb.alignment = TextAlignmentOptions.Center;
            }
            var res = settlement.Database.GetResource(r.resourceId);
            Btn("<", () => settlement.CycleRouteResource(r, -1), 24);
            var name = UIBuilder.Text("Res", row, $"{(res != null ? res.displayName : r.resourceId)}", 15, TextAlignmentOptions.Center);
            name.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
            Btn(">", () => settlement.CycleRouteResource(r, 1), 24);
            Btn("-", () => r.amount = Mathf.Max(Settlement.RouteStep, r.amount - Settlement.RouteStep), 24);
            var amt = UIBuilder.Text("Amount", row, r.amount.ToString(), 15, TextAlignmentOptions.Center);
            amt.gameObject.AddComponent<LayoutElement>().preferredWidth = 46;
            Btn("+", () => r.amount = Mathf.Min(Settlement.RouteMax, r.amount + Settlement.RouteStep), 24);
            Btn(r.paused ? "Go" : "||", () => r.paused = !r.paused, 30);
            Btn("x", () => settlement.RemoveRoute(r), 24);
        }
        var add = UIBuilder.Button("AddRoute", info, () => { settlement.AddRoute(from, to); ShowInfo(); }, out var al, 34, 16);
        al.text = "+ Add a cargo route"; al.alignment = TextAlignmentOptions.Center;
    }

    public static string FormatTime(float seconds) =>
        seconds >= 3600 ? $"{seconds / 3600f:0.#} h" : seconds >= 60 ? $"{Mathf.CeilToInt(seconds / 60f)} min" : $"{Mathf.CeilToInt(seconds)} s";

    // ---------- Pieces ----------

    private void Mark(Vector2 ly, float size, Color c, UnityEngine.Events.UnityAction click)
    {
        AddLocal((ly - centre) * zoom, size, c, click);
    }

    private void AddLocal(Vector2 at, float size, Color c, UnityEngine.Events.UnityAction click)
    {
        var img = UIBuilder.Icon("Dot", content, Dot, size);
        img.color = c;
        img.rectTransform.anchoredPosition = at;
        img.raycastTarget = click != null;
        if (click != null) img.gameObject.AddComponent<Button>().onClick.AddListener(click);
        marks.Add(img.gameObject);
    }

    private void AddRing(float r)
    {
        var img = UIBuilder.Icon("Orbit", content, Ring, r * 2f);
        img.color = new Color(0.4f, 0.5f, 0.7f, 0.25f);
        img.raycastTarget = false;
        marks.Add(img.gameObject);
    }

    private void Label(Vector2 ly, string text, Color c)
    {
        var t = UIBuilder.Text("Name", content, text, 13, TextAlignmentOptions.Center);
        t.color = c;
        t.rectTransform.anchoredPosition = (ly - centre) * zoom;
        t.rectTransform.sizeDelta = new Vector2(150, 18);
        marks.Add(t.gameObject);
    }

    private static Sprite Dot => dot ??= Circle(32, false);
    private static Sprite ring;
    private static Sprite Ring => ring ??= Circle(128, true);

    private static Sprite Circle(int size, bool hollow)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f - 1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = hollow ? Mathf.Clamp01(1.5f - Mathf.Abs(d - r + 1)) : Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public static Color StarColour(StarType t) => t switch
    {
        StarType.RedDwarf => new Color(1f, 0.55f, 0.4f),
        StarType.BlueGiant => new Color(0.6f, 0.75f, 1f),
        StarType.Binary => new Color(1f, 0.85f, 0.6f),
        StarType.Neutron => new Color(0.85f, 0.95f, 1f),
        StarType.VoidTouched => new Color(0.85f, 0.4f, 1f),
        _ => new Color(1f, 0.95f, 0.65f),
    };

    private static string StarName(StarType t) => t switch
    {
        StarType.RedDwarf => "red dwarf", StarType.BlueGiant => "blue giant", StarType.Binary => "binary star",
        StarType.Neutron => "neutron star", StarType.VoidTouched => "void-touched star", _ => "yellow star",
    };

    private static Color BodyColour(Planet p)
    {
        if (p.kind == BodyKind.GasGiant) return new Color(0.85f, 0.7f, 0.5f);
        if (p.kind == BodyKind.AsteroidBelt) return new Color(0.6f, 0.58f, 0.55f);
        return p.profile switch
        {
            "Desert" => new Color(0.9f, 0.6f, 0.35f), "Ice" => new Color(0.85f, 0.95f, 1f), "Swamp" => new Color(0.45f, 0.6f, 0.35f),
            "Volcanic" => new Color(0.85f, 0.35f, 0.25f), "Crystal" => new Color(0.75f, 0.55f, 1f), "Ocean" => new Color(0.3f, 0.55f, 0.95f),
            "Barren" => new Color(0.6f, 0.6f, 0.62f), "Jungle" => new Color(0.2f, 0.6f, 0.25f), "Fungal" => new Color(0.8f, 0.45f, 0.8f),
            _ => new Color(0.4f, 0.8f, 0.45f),
        };
    }
}
