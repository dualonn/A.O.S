using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Spotted war parties on the map (and the zoomed-out overview): a raider war banner where the
/// party is, a faint line to the town it's after, and how many there are. Unspotted parties
/// aren't drawn: your scouts haven't seen them. Added by the raid loop when the scene starts.
/// </summary>
public class RaidMarchView : MonoBehaviour
{
    private static Sprite banner;
    private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
    private readonly List<LineRenderer> trails = new List<LineRenderer>();
    private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    private readonly List<int> shownCounts = new List<int>();
    private Material lineMaterial;

    private void LateUpdate()
    {
        var s = Settlement.Instance;
        var c = FreeModeController.Instance;
        var cam = Camera.main;
        int n = 0;
        if (s != null && s.State != null && c != null && cam != null && c.Grid != null && !s.RaidsOff)
        {
            bool zoomedOut = c.Grid.CurrentDetail != SettlementGrid.Detail.Tiles;
            float size = zoomedOut ? cam.orthographicSize * 0.09f : 1.7f;
            float width = zoomedOut ? cam.orthographicSize * 0.004f : 0.07f;
            foreach (var p in s.RaidState.parties)
            {
                if (!p.spotted) continue;
                if (marks.Count <= n) Add();
                Vector2 at = p.Position;
                float bob = p.gatherLeft > 0f ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 4f + p.id)) * 0.06f * size;
                var m = marks[n];
                m.enabled = true;
                m.transform.position = new Vector3(at.x, at.y + bob, -1.2f);
                m.transform.localScale = Vector3.one * size;

                var line = trails[n];
                line.enabled = p.gatherLeft <= 0f || zoomedOut;
                line.startWidth = line.endWidth = width;
                line.SetPosition(0, new Vector3(at.x, at.y, -1.1f));
                line.SetPosition(1, new Vector3(p.target.x, p.target.y, -1.1f));

                var label = labels[n];
                label.enabled = !zoomedOut;
                if (shownCounts[n] != p.units.Count + p.id * 1000)
                {
                    shownCounts[n] = p.units.Count + p.id * 1000;
                    label.text = $"<color=#ffb08a>{s.RaidNoun()}</color> ~{p.units.Count}";
                }
                label.transform.position = new Vector3(at.x, at.y + size * 1.05f + bob, -1.2f);
                n++;
            }
        }
        for (int i = n; i < marks.Count; i++)
        {
            marks[i].enabled = false;
            trails[i].enabled = false;
            labels[i].enabled = false;
        }
    }

    private void Add()
    {
        var m = new GameObject("WarPartyBanner").AddComponent<SpriteRenderer>();
        m.transform.SetParent(transform, false);
        m.sprite = Banner;
        m.sortingOrder = 31;
        marks.Add(m);

        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        var line = new GameObject("WarPartyTrail").AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.material = lineMaterial;
        line.startColor = new Color(1f, 0.35f, 0.25f, 0.55f);
        line.endColor = new Color(1f, 0.35f, 0.25f, 0.08f);
        line.sortingOrder = 29;
        trails.Add(line);

        var label = new GameObject("WarPartyLabel").AddComponent<TextMeshPro>();
        label.transform.SetParent(transform, false);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3.2f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(8, 1.2f);
        label.sortingOrder = 32;
        labels.Add(label);
        shownCounts.Add(-1);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    /// <summary>A raider war banner: a spear-tipped pole and a tattered, swallow-tailed crimson flag with crossed bones.</summary>
    public static Sprite Banner
    {
        get
        {
            if (banner != null) return banner;
            var c = new PixelCanvas(24, 32);
            var wood = PixelCanvas.Hex("5a3a22");
            var woodLight = PixelCanvas.Hex("7d5634");
            var iron = PixelCanvas.Hex("b8b8b0");
            var ironDark = PixelCanvas.Hex("6e6e6a");
            var red = PixelCanvas.Hex("8e1f1a");
            var redLight = PixelCanvas.Hex("b23a2a");
            var redDark = PixelCanvas.Hex("5a1210");
            var bone = PixelCanvas.Hex("ddd3ba");
            var trim = PixelCanvas.Hex("8a6a2a");

            // Pole and spear tip.
            c.Rect(4, 0, 5, 26, wood);
            c.Rect(4, 0, 4, 26, woodLight);
            c.Put(5, 27, ironDark); c.Put(4, 27, iron);
            c.Rect(4, 28, 5, 29, iron);
            c.Put(5, 29, ironDark);
            c.Put(4, 30, iron);
            // Crossbar tied with a thong.
            c.Rect(3, 25, 20, 25, wood);
            c.Put(6, 25, woodLight);
            // Flag: hangs from the crossbar, swallow-tailed.
            for (int y = 11; y <= 24; y++)
            {
                for (int x = 6; x <= 20; x++)
                {
                    int notch = Mathf.Abs(y - 15) ;
                    if (x >= 17 && y <= 18 && x - 17 > notch - 1) continue;   // the tail's notch
                    float wave = Mathf.Sin((x - 6) * 0.55f) * 0.5f + 0.5f;
                    var col = y >= 23 ? trim : wave > 0.7f ? redLight : wave < 0.25f ? redDark : red;
                    if (x == 6) col = redDark;
                    c.Put(x, y, col);
                }
            }
            // Tatters at the bottom edge.
            c.Clear(9, 11); c.Clear(12, 11); c.Clear(12, 12); c.Clear(15, 11);
            // Crossed bones.
            for (int i = 0; i <= 6; i++)
            {
                c.Put(9 + i, 14 + i, bone);
                c.Put(15 - i, 14 + i, bone);
            }
            c.Put(8, 14, bone); c.Put(9, 13, bone); c.Put(16, 14, bone); c.Put(15, 13, bone);
            c.Put(8, 20, bone); c.Put(9, 21, bone); c.Put(16, 20, bone); c.Put(15, 21, bone);
            c.Outline(PixelCanvas.Hex("140c0a"), 0.7f);
            var tex = c.ToTexture();
            tex.name = "WarPartyBanner";
            banner = Sprite.Create(tex, new Rect(0, 0, c.W, c.H), new Vector2(4.5f / c.W, 0f), c.H, 0, SpriteMeshType.FullRect);
            banner.name = "WarPartyBanner";
            return banner;
        }
    }
}
