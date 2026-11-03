using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Natural wonders stand out on the map. Zoomed out (overview and strategy map) each has its own
/// icon on a soft golden glow, bigger than other sites', with its name and blessing on hover; one
/// not yet seen pulses. Up close, a small icon bobs over a wonder nobody has visited yet.
/// </summary>
public class XWonderMarkers : MonoBehaviour
{
    private FreeModeController controller;

    private class Marker
    {
        public SiteState site;
        public SpriteRenderer icon, glow;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private TextMeshPro label;
    private float syncAt;
    private static Sprite glowSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => glowSprite = null;

    private const int Order = 31;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        var labelObject = new GameObject("WonderLabel");
        labelObject.transform.SetParent(transform, false);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 230);
        label.rectTransform.sizeDelta = new Vector2(16, 3);
        label.sortingOrder = Order + 2;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text = "";
    }

    /// <summary>A soft round glow, made once (a few rings of pixels, so it suits the pixel art).</summary>
    private static Sprite Glow()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 24;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                // Stepped, not smooth: three bands.
                byte a = d < 0.45f ? (byte)150 : d < 0.7f ? (byte)95 : d < 0.95f ? (byte)40 : (byte)0;
                px[y * n + x] = new Color32(255, 235, 150, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return glowSprite;
    }

    private Sprite IconFor(SiteState site, ArtLibrary art)
    {
        if (art == null) return null;
        int k = (int)SiteKind.NaturalWonder;
        if (art.siteIcons != null && art.siteIcons.Count > k && art.siteIcons[k] != null) return art.siteIcons[k];
        // Before Setup Extras has drawn the icon: the wonder's own picture.
        if (art.naturalWonders != null && site.variant >= 0 && site.variant < art.naturalWonders.Count) return art.naturalWonders[site.variant];
        return null;
    }

    private void Sync()
    {
        var s = controller.Settlement;
        var art = ArtLibrary.Load();
        int i = 0;
        foreach (var site in s.State.sites)
        {
            if (site.kind != SiteKind.NaturalWonder) continue;
            if (markers.Count <= i)
            {
                var m = new Marker
                {
                    glow = new GameObject("WonderGlow").AddComponent<SpriteRenderer>(),
                    icon = new GameObject("WonderIcon").AddComponent<SpriteRenderer>(),
                };
                m.glow.transform.SetParent(transform, false);
                m.icon.transform.SetParent(transform, false);
                m.glow.sprite = Glow();
                m.glow.sortingOrder = Order - 1;
                m.icon.sortingOrder = Order;
                markers.Add(m);
            }
            markers[i].site = site;
            markers[i].icon.sprite = IconFor(site, art);
            i++;
        }
        for (int j = markers.Count - 1; j >= i; j--)
        {
            if (markers[j].icon != null) Destroy(markers[j].icon.gameObject);
            if (markers[j].glow != null) Destroy(markers[j].glow.gameObject);
            markers.RemoveAt(j);
        }
    }

    /// <summary>Scale a sprite to 'size' world units and centre it on 'at' (whatever its pivot).</summary>
    private static void Place(SpriteRenderer sr, Vector3 at, float size)
    {
        if (sr.sprite == null) return;
        var b = sr.sprite.bounds;
        float scale = size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
        sr.transform.localScale = Vector3.one * scale;
        Vector3 off = b.center * scale;
        sr.transform.position = new Vector3(at.x - off.x, at.y - off.y, at.z);
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Settlement == null || controller.Settlement.State == null) return;
        var cam = Camera.main;
        var grid = controller.Grid;
        if (cam == null || grid == null) return;
        if (Time.unscaledTime >= syncAt)
        {
            syncAt = Time.unscaledTime + 1f;
            Sync();
        }
        bool zoomedOut = grid.CurrentDetail != SettlementGrid.Detail.Tiles;
        float screenSize = cam.orthographicSize * 0.12f;   // as the other map markers
        Vector3 mouse = SettlementGrid.MouseWorld();
        Marker hovered = null;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
        foreach (var m in markers)
        {
            var site = m.site;
            bool seen = site.done;
            Vector3 centre = grid.CellToWorld(Vector2Int.zero) + (Vector3)site.Centre;
            if (zoomedOut)
            {
                float size = screenSize * 1.1f;
                m.icon.enabled = m.icon.sprite != null;
                m.glow.enabled = true;
                Place(m.icon, new Vector3(centre.x, centre.y, -1.2f), size);
                float glowSize = size * (seen ? 1.7f : 1.6f + 0.35f * pulse);
                m.glow.transform.position = new Vector3(centre.x, centre.y, -1.1f);
                m.glow.transform.localScale = Vector3.one * glowSize;
                m.glow.color = seen ? new Color(1f, 0.95f, 0.75f, 0.8f) : new Color(0.75f, 0.95f, 1f, 0.55f + 0.4f * pulse);
                m.icon.color = seen ? Color.white : new Color(0.85f, 0.95f, 1f);
                if (Vector2.Distance(mouse, centre) < size * 0.8f) hovered = m;
            }
            else
            {
                // Up close the wonder itself shows; one nobody has seen gets a small bobbing marker.
                m.glow.enabled = false;
                m.icon.enabled = !seen && m.icon.sprite != null;
                if (m.icon.enabled)
                {
                    float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.15f;
                    Place(m.icon, new Vector3(centre.x, centre.y + site.Size * 0.5f + 1.6f + bob, -1.2f), 0.9f);
                    m.icon.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * pulse);
                }
            }
        }
        // Hover a marker on the zoomed-out map: its name, and what it gives.
        if (hovered != null)
        {
            var info = Settlement.NaturalWonderOf(hovered.site);
            label.text = hovered.site.done
                ? $"<color=#8fe><b>{info.name}</b></color>\n<size=75%>Natural wonder. {info.effect}</size>"
                : $"<color=#8fe><b>{info.name}</b></color>\n<size=75%>A natural wonder, not yet seen. Send any unit there: {info.effect}</size>";
            float scale = cam.orthographicSize / 12f;
            label.transform.localScale = Vector3.one * scale;
            Vector3 c = grid.CellToWorld(Vector2Int.zero) + (Vector3)hovered.site.Centre;
            label.transform.position = new Vector3(c.x, c.y + screenSize * 1.3f, -1.3f);
        }
        else if (label.text.Length > 0) label.text = "";
    }
}
