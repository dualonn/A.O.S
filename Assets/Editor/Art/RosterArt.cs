using System.Collections.Generic;
using UnityEngine;
using static Painter;

/// <summary>
/// Training buildings for the wider age rosters: Axe Hall and Chariot Yard (Bronze),
/// Slinger Camp (Iron), Skirmisher Camp and Elephant Pen (Classical).
/// Same conventions as BuildingArt: grid-cell units, t runs 0..1 across the loop, levels add visible changes.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Color Wicker = new Color(0.82f, 0.68f, 0.42f);
    private static readonly Color ElephantGrey = new Color(0.56f, 0.55f, 0.56f);

    /// <summary>A side-view spoked wheel; spin in degrees turns the spokes.</summary>
    private static void SpokedWheel(Painter p, Vector2 hub, float r, float spin)
    {
        var bounds = new Rect(hub.x - r - 0.08f, hub.y - r - 0.08f, (r + 0.08f) * 2f, (r + 0.08f) * 2f);
        p.Draw(q => Mathf.Abs(SdCircle(q, hub, r)) - 0.045f, bounds, p.Ink);
        p.Draw(q => Mathf.Abs(SdCircle(q, hub, r)) - 0.028f, bounds, WoodDark);
        for (int k = 0; k < 6; k++)
        {
            float a = (spin + k * 60f) * Mathf.Deg2Rad;
            p.Line(hub, hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0.025f, WoodDark);
        }
        p.OutlinedCircle(hub, r * 0.22f, Bronze2);
    }

    /// <summary>A wicker crescent (pelta) shield, as hung on a wall.</summary>
    private static void PeltaShield(Painter p, Vector2 c, float r)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= 14; i++)
        {
            float a = (35f - 250f * i / 14f) * Mathf.Deg2Rad;
            pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        pts.Add(c + V(-r * 0.4f, r * 0.3f));
        pts.Add(c + V(0f, r * 0.15f));
        pts.Add(c + V(r * 0.4f, r * 0.3f));
        p.OutlinedPolygon(pts, Wicker);
        p.InkLine(c + V(-r * 0.6f, -r * 0.35f), c + V(r * 0.6f, -r * 0.35f), 0.4f);
    }

    // ---------- Bronze Age ----------

    private static void DrawAxeHall(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.36f), V(1.4f, 0.24f), Shadow);
        if (level >= 3)
        {
            // Second Hearth: a fire pit with smoke on the left
            p.OutlinedEllipse(V(0.3f, 0.3f), V(0.24f, 0.08f), StoneDark);
            FlameShape(p, V(0.3f, 0.32f), 0.1f, 0.3f, t);
            SmokePuffs(p, V(0.3f, 0.7f), t, 0.9f);
        }
        // Flat-roofed mud-brick hall with timber roof beams poking out
        p.OutlinedBox(0.55f, 0.3f, 2.45f, 1.7f, 0.03f, MudBrick);
        for (float y = 0.5f; y < 1.6f; y += 0.22f)
        {
            p.InkLine(V(0.58f, y), V(2.42f, y), 0.35f);
        }
        p.OutlinedBox(0.45f, 1.66f, 2.55f, 1.84f, 0.02f, MudBrickDark);
        for (float x = 0.65f; x < 2.5f; x += 0.3f)
        {
            p.OutlinedCircle(V(x, 1.75f), 0.045f, WoodLight);
        }
        p.OutlinedBox(1.25f, 0.3f, 1.75f, 1.1f, 0.22f, Dark);
        // Crossed axes over the door
        p.OutlinedLine(V(1.2f, 1.2f), V(1.8f, 1.6f), 0.035f, WoodLight);
        p.OutlinedLine(V(1.8f, 1.2f), V(1.2f, 1.6f), 0.035f, WoodLight);
        p.OutlinedPolygon(new[] { V(1.72f, 1.6f), V(1.9f, 1.68f), V(1.86f, 1.48f) }, Bronze2);
        p.OutlinedPolygon(new[] { V(1.28f, 1.6f), V(1.1f, 1.68f), V(1.14f, 1.48f) }, Bronze2);
        WavingFlag(p, V(2.35f, 1.84f), 0.8f, t, Blue);
        // Chopping block: an axe splits a log over the loop
        p.OutlinedBox(2.45f, 0.2f, 2.85f, 0.45f, 0.04f, Wood);
        int f = Mathf.Clamp((int)(t * 4), 0, 3);
        float[] angles = { 80f, 120f, 15f, 45f };
        Vector2 pivot = V(2.75f, 0.85f);
        Vector2 head = ToolTip(pivot, 0.36f, 180f + angles[f] * -1f);
        p.OutlinedLine(pivot, head, 0.03f, WoodLight);
        p.OutlinedPolygon(new[] { head + V(-0.06f, 0.05f), head + V(0.06f, 0.05f), head + V(0.03f, -0.07f), head + V(-0.03f, -0.07f) }, Bronze2);
        if (f == 2)
        {
            p.OutlinedBox(2.5f, 0.45f, 2.6f, 0.5f, 0.01f, WoodLight);
            p.OutlinedBox(2.72f, 0.48f, 2.8f, 0.53f, 0.01f, WoodLight);
        }
        if (level >= 1)
        {
            // Crescent Blades: a rack of bronze axes
            p.OutlinedLine(V(0.2f, 0.95f), V(0.62f, 0.95f), 0.03f, WoodDark);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.26f + i * 0.14f;
                p.OutlinedLine(V(x, 0.3f), V(x, 1.0f), 0.022f, WoodLight);
                p.OutlinedPolygon(new[] { V(x, 0.92f), V(x + 0.09f, 1.0f), V(x + 0.1f, 0.82f), V(x, 0.84f) }, Bronze2);
            }
        }
        if (level >= 2)
        {
            // Leather Caps on pegs by the door
            foreach (float x in new[] { 0.85f, 2.1f })
            {
                p.OutlinedPolygon(new[] { V(x - 0.1f, 0.85f), V(x + 0.1f, 0.85f), V(x + 0.07f, 0.97f), V(x, 1.0f), V(x - 0.07f, 0.97f) }, Hide);
            }
        }
    }

    private static void DrawChariotYard(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.34f), V(1.45f, 0.24f), Shadow);
        // Open-fronted timber shed
        p.OutlinedBox(0.25f, 0.3f, 2.0f, 1.55f, 0.02f, WoodDark);
        p.OutlinedPolygon(new[] { V(0.1f, 1.5f), V(2.15f, 1.5f), V(1.9f, 2.05f), V(0.35f, 2.05f) }, Straw);
        p.OutlinedLine(V(0.3f, 0.3f), V(0.3f, 1.5f), 0.06f, Wood);
        p.OutlinedLine(V(1.95f, 0.3f), V(1.95f, 1.5f), 0.06f, Wood);
        // A parked chariot inside, pole resting on the ground
        p.OutlinedLine(V(1.3f, 0.5f), V(1.9f, 0.3f), 0.035f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.6f, 0.5f), V(1.35f, 0.5f), V(1.35f, 0.9f), V(1.25f, 1.0f), V(0.7f, 1.0f), V(0.6f, 0.8f) }, Wood);
        p.Line(V(0.62f, 0.78f), V(1.33f, 0.78f), 0.05f, Blue);
        p.OutlinedLine(V(0.7f, 1.0f), V(1.25f, 1.0f), 0.04f, Bronze2);
        // A wheelwright's wheel turning on its stand
        SpokedWheel(p, V(0.95f, 0.52f), 0.24f, 0f);
        p.OutlinedLine(V(2.45f, 0.2f), V(2.45f, 0.62f), 0.05f, WoodDark);
        SpokedWheel(p, V(2.45f, 0.75f), 0.28f, t * 60f);
        WavingFlag(p, V(2.05f, 1.55f), 0.9f, t, Blue);
        if (level >= 1)
        {
            // Spoked Wheels: spares leaning on the shed
            SpokedWheel(p, V(2.25f, 1.2f), 0.2f, 15f);
        }
        if (level >= 2)
        {
            // Wicker Sides: woven screens stacked by the door
            for (int i = 0; i < 2; i++)
            {
                float x = 0.0f + i * 0.12f;
                p.OutlinedBox(x + 0.05f, 0.2f, x + 0.3f, 0.75f, 0.03f, Wicker);
                for (float y = 0.3f; y < 0.72f; y += 0.1f)
                {
                    p.InkLine(V(x + 0.07f, y), V(x + 0.28f, y + 0.05f), 0.35f);
                }
            }
        }
        if (level >= 3)
        {
            // Second Team: a horse looking out of a stall at the back
            Vector2 h = V(1.65f, 1.15f + Wave(t) * 0.03f);
            p.OutlinedBox(1.45f, 0.95f, 1.9f, 1.45f, 0.02f, Dark);
            p.OutlinedPolygon(new[] { h + V(-0.12f, -0.15f), h + V(0.08f, -0.15f), h + V(0.1f, 0.12f), h + V(-0.08f, 0.14f) }, HorseBrown);
            p.OutlinedPolygon(new[] { h + V(0.05f, 0.1f), h + V(0.28f, 0.0f), h + V(0.26f, -0.1f), h + V(0.05f, -0.05f) }, HorseBrown);
            p.Circle(h + V(0.13f, 0.04f), 0.016f, p.Ink);
        }
    }

    // ---------- Iron Age ----------

    private static void DrawSlingerCamp(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.3f), V(1.45f, 0.2f), Shadow);
        p.OutlinedBox(0.1f, 0.15f, 2.9f, 1.4f, 0.1f, new Color(0.64f, 0.64f, 0.4f));
        // Low stone hut with thatch
        StoneBlocks(p, 0.2f, 1.25f, 1.15f, 1.9f, Stone, 0.16f);
        p.OutlinedPolygon(new[] { V(0.05f, 1.85f), V(1.3f, 1.85f), V(0.68f, 2.4f) }, Straw);
        p.OutlinedBox(0.5f, 1.25f, 0.82f, 1.65f, 0.1f, Dark);
        // Clay pots on posts as targets: a stone flies and breaks one each loop
        var posts = new[] { V(2.2f, 1.1f), V(2.6f, 0.75f), V(2.2f, 0.4f) };
        int f = Mathf.Clamp((int)(t * 4), 0, 3);
        for (int i = 0; i < posts.Length; i++)
        {
            Vector2 c = posts[i];
            p.OutlinedLine(c + V(0, -0.3f), c, 0.04f, WoodDark);
            bool broken = i == 1 && f == 3;
            if (broken)
            {
                p.OutlinedPolygon(new[] { c + V(-0.1f, 0f), c + V(0.1f, 0f), c + V(0.06f, 0.08f), c + V(0f, 0.04f), c + V(-0.05f, 0.09f) }, Terracotta);
                p.OutlinedCircle(c + V(0.18f, -0.25f), 0.03f, Terracotta);
                p.OutlinedCircle(c + V(-0.16f, -0.28f), 0.025f, Terracotta);
            }
            else
            {
                p.OutlinedEllipse(c + V(0, 0.1f), V(0.11f, 0.12f), Terracotta);
                p.OutlinedBox(c.x - 0.05f, c.y + 0.2f, c.x + 0.05f, c.y + 0.27f, 0.01f, TerracottaDark);
            }
        }
        if (f < 3)
        {
            // The stone in flight toward the middle pot
            Vector2 from = V(1.4f, 0.9f), to = posts[1] + V(0, 0.1f);
            float k = (f + 1) / 3f;
            p.OutlinedCircle(Vector2.Lerp(from, to, k) + V(0, Mathf.Sin(k * Mathf.PI) * 0.25f), 0.035f, StoneLight);
        }
        // Throwing line
        p.Line(V(1.35f, 0.25f), V(1.35f, 1.15f), 0.02f, new Color(0.95f, 0.95f, 0.85f));
        WavingFlag(p, V(1.2f, 1.85f), 0.7f, t, Flag);
        if (level >= 1)
        {
            // Lead Bullets: a little crucible over embers
            p.OutlinedEllipse(V(0.45f, 0.35f), V(0.18f, 0.06f), StoneDark);
            p.Circle(V(0.4f, 0.37f), 0.04f, Wave(t) > 0 ? FireCore : Fire);
            p.OutlinedBox(0.36f, 0.4f, 0.56f, 0.55f, 0.04f, IronDark);
        }
        if (level >= 2)
        {
            // Longer Cords hanging on a rack
            p.OutlinedLine(V(0.75f, 0.25f), V(0.75f, 0.95f), 0.03f, WoodDark);
            p.OutlinedLine(V(1.15f, 0.25f), V(1.15f, 0.95f), 0.03f, WoodDark);
            p.OutlinedLine(V(0.72f, 0.92f), V(1.18f, 0.92f), 0.025f, WoodLight);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.83f + i * 0.12f;
                p.Line(V(x, 0.92f), V(x + Wave(t, i * 0.2f) * 0.02f, 0.45f), 0.012f, new Color(0.92f, 0.86f, 0.7f));
                p.OutlinedCircle(V(x + Wave(t, i * 0.2f) * 0.02f, 0.44f), 0.025f, Hide);
            }
        }
        if (level >= 3)
        {
            // Pebble Pile by the line
            OutlinedBlob(p, StoneLight, (V(1.6f, 0.3f), 0.08f), (V(1.72f, 0.28f), 0.07f), (V(1.66f, 0.38f), 0.07f), (V(1.8f, 0.34f), 0.05f));
        }
    }

    // ---------- Classical Age ----------

    private static void DrawSkirmisherCamp(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.3f), V(1.45f, 0.2f), Shadow);
        p.OutlinedBox(0.1f, 0.15f, 2.9f, 1.3f, 0.1f, new Color(0.66f, 0.66f, 0.42f));
        // Small columned hall with a terracotta roof
        p.OutlinedBox(0.15f, 1.2f, 1.55f, 1.32f, 0.01f, MarbleShade);
        p.OutlinedBox(0.25f, 1.32f, 1.45f, 1.95f, 0.02f, Ochre);
        foreach (float x in new[] { 0.3f, 0.65f, 1.05f, 1.4f })
        {
            p.OutlinedBox(x - 0.05f, 1.32f, x + 0.05f, 1.95f, 0.01f, Marble);
        }
        p.OutlinedPolygon(new[] { V(0.1f, 1.95f), V(1.6f, 1.95f), V(0.85f, 2.4f) }, Terracotta);
        // A straw dummy that collects a javelin per beat
        Vector2 d = V(2.3f, 0.75f);
        p.OutlinedLine(d + V(0, -0.5f), d + V(0, 0.4f), 0.05f, WoodDark);
        p.OutlinedLine(d + V(-0.3f, 0.22f), d + V(0.3f, 0.22f), 0.05f, WoodDark);
        p.OutlinedEllipse(d + V(0, 0.05f), V(0.18f, 0.28f), HayColor);
        p.OutlinedCircle(d + V(0, 0.48f), 0.12f, HayColor);
        int hits = Mathf.Clamp((int)(t * 4), 0, 3) + 1;
        var spots = new[] { V(2.25f, 0.95f), V(2.35f, 0.7f), V(2.28f, 0.55f), V(2.33f, 1.18f) };
        for (int i = 0; i < hits; i++)
        {
            Vector2 s = spots[i];
            p.OutlinedLine(s, s + V(-0.45f, 0.08f), 0.025f, WoodLight);
        }
        p.Line(V(1.25f, 0.25f), V(1.25f, 1.1f), 0.02f, new Color(0.95f, 0.95f, 0.85f));
        WavingFlag(p, V(1.6f, 1.3f), 1.0f, t, Blue);
        if (level >= 1)
        {
            // Throwing Loops: javelins in a rack, loops tied on
            p.OutlinedLine(V(0.25f, 0.6f), V(0.95f, 0.6f), 0.03f, WoodDark);
            for (int i = 0; i < 5; i++)
            {
                float x = 0.3f + i * 0.14f;
                p.OutlinedLine(V(x, 0.25f), V(x + 0.05f, 1.05f), 0.02f, WoodLight);
                p.Line(V(x + 0.02f, 0.62f), V(x + 0.03f, 0.7f), 0.03f, Blue);
            }
        }
        if (level >= 2)
        {
            // Iron Heads: a crate of javelin points
            p.OutlinedBox(1.4f, 0.2f, 1.8f, 0.45f, 0.02f, Wood);
            for (int i = 0; i < 3; i++)
            {
                float x = 1.48f + i * 0.11f;
                p.OutlinedPolygon(new[] { V(x, 0.45f), V(x + 0.04f, 0.58f), V(x + 0.08f, 0.45f) }, Iron);
            }
        }
        if (level >= 3)
        {
            // Pelta Shields hung along the hall
            PeltaShield(p, V(0.48f, 1.62f), 0.13f);
            PeltaShield(p, V(0.85f, 1.62f), 0.13f);
            PeltaShield(p, V(1.22f, 1.62f), 0.13f);
        }
    }

    private static void DrawElephantPen(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.36f), V(1.45f, 0.25f), Shadow);
        // Elephant head and trunk over the back wall, trunk swaying
        Vector2 h = V(1.5f, 1.85f + Wave(t) * 0.02f);
        p.OutlinedEllipse(h + V(-0.25f, -0.02f), V(0.15f, 0.24f), Color.Lerp(ElephantGrey, Color.black, 0.2f));
        p.OutlinedEllipse(h + V(0.25f, -0.02f), V(0.15f, 0.24f), Color.Lerp(ElephantGrey, Color.black, 0.2f));
        p.OutlinedCircle(h, 0.24f, ElephantGrey);
        float sway = Wave(t) * 0.1f;
        p.OutlinedLine(h + V(0, -0.1f), h + V(sway, -0.45f), 0.09f, ElephantGrey);
        p.OutlinedLine(h + V(sway, -0.45f), h + V(sway * 1.6f + 0.06f, -0.7f), 0.07f, ElephantGrey);
        p.Line(h + V(0, -0.1f), h + V(sway, -0.45f), 0.09f, ElephantGrey);
        p.Circle(h + V(-0.09f, 0.06f), 0.025f, p.Ink);
        p.Circle(h + V(0.09f, 0.06f), 0.025f, p.Ink);
        p.OutlinedLine(h + V(-0.12f, -0.12f), h + V(-0.2f, -0.3f), 0.035f, new Color(0.97f, 0.94f, 0.85f));
        p.OutlinedLine(h + V(0.12f, -0.12f), h + V(0.2f, -0.3f), 0.035f, new Color(0.97f, 0.94f, 0.85f));
        // Stone enclosure wall with a tall arched gate
        StoneBlocks(p, 0.15f, 0.3f, 2.85f, 1.35f, StoneLight, 0.18f);
        p.OutlinedBox(0.05f, 1.3f, 2.95f, 1.42f, 0.01f, Stone);
        p.OutlinedBox(1.15f, 0.3f, 1.85f, 1.15f, 0.3f, Dark);
        p.OutlinedLine(V(1.15f, 1.0f), V(1.85f, 1.0f), 0.03f, WoodDark);
        WavingFlag(p, V(2.75f, 1.42f), 0.9f, t, Blue);
        // Hay and a water trough
        p.OutlinedBox(0.25f, 0.15f, 0.75f, 0.45f, 0.04f, HayColor);
        p.OutlinedBox(2.2f, 0.18f, 2.8f, 0.38f, 0.02f, WoodDark);
        p.Line(V(2.24f, 0.36f), V(2.76f, 0.36f), 0.03f, WaterBlue);
        if (level >= 1)
        {
            // Leather Barding draped over the wall
            p.OutlinedPolygon(new[] { V(0.3f, 1.42f), V(0.95f, 1.42f), V(0.9f, 0.85f), V(0.35f, 0.85f) }, Hide);
            p.Line(V(0.36f, 0.95f), V(0.89f, 0.95f), 0.025f, Gold);
        }
        if (level >= 2)
        {
            // Tusk Blades in a rack
            p.OutlinedLine(V(2.05f, 0.75f), V(2.75f, 0.75f), 0.03f, WoodDark);
            for (int i = 0; i < 3; i++)
            {
                float x = 2.12f + i * 0.22f;
                p.OutlinedPolygon(new[] { V(x, 0.75f), V(x + 0.12f, 0.75f), V(x + 0.18f, 1.15f) }, Iron);
            }
        }
        if (level >= 3)
        {
            // Second Stall: a second gate in the wall
            p.OutlinedBox(0.95f, 0.3f, 1.12f, 0.75f, 0.08f, Dark);
            p.OutlinedBox(1.88f, 0.3f, 2.05f, 0.75f, 0.08f, Dark);
            p.OutlinedLine(V(0.9f, 1.15f), V(2.1f, 1.15f), 0.04f, Gold);
        }
    }
}
