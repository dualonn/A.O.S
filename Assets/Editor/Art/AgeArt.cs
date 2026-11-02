using UnityEngine;
using static Painter;

/// <summary>
/// Iron Age and Classical Age buildings, ore deposits and resource icons.
/// Same conventions as BuildingArt: drawn in grid cells, t runs 0..1 across the loop,
/// upgrades (level) add visible changes. Crewed towers are roofless; the game adds the crew.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Color IronOreRock = new Color(0.5f, 0.42f, 0.4f);
    private static readonly Color Rust = new Color(0.72f, 0.38f, 0.22f);
    private static readonly Color Terracotta = new Color(0.78f, 0.4f, 0.28f);
    private static readonly Color TerracottaDark = new Color(0.6f, 0.28f, 0.2f);
    private static readonly Color Ochre = new Color(0.9f, 0.76f, 0.52f);
    private static readonly Color Marble = new Color(0.94f, 0.93f, 0.9f);
    private static readonly Color MarbleShade = new Color(0.8f, 0.8f, 0.78f);
    private static readonly Color HayColor = new Color(0.92f, 0.8f, 0.42f);
    private static readonly Color HorseBrown = new Color(0.55f, 0.36f, 0.2f);

    // ---------- Shared bits ----------

    /// <summary>A wall of dressed stone blocks in staggered courses.</summary>
    private static void StoneBlocks(Painter p, float x0, float y0, float x1, float y1, Color fill, float course = 0.18f)
    {
        p.OutlinedBox(x0, y0, x1, y1, 0.02f, fill);
        int row = 0;
        for (float y = y0 + course; y < y1 - 0.02f; y += course, row++)
        {
            p.InkLine(V(x0 + 0.02f, y), V(x1 - 0.02f, y), 0.35f);
        }
        row = 0;
        for (float y = y0; y < y1 - 0.02f; y += course, row++)
        {
            float offset = row % 2 == 0 ? 0f : 0.14f;
            for (float x = x0 + 0.14f + offset; x < x1 - 0.06f; x += 0.28f)
            {
                p.InkLine(V(x, y + 0.01f), V(x, Mathf.Min(y + course, y1) - 0.01f), 0.3f);
            }
        }
    }

    /// <summary>Crenellated top (merlons) along a wall.</summary>
    private static void Merlons(Painter p, float x0, float x1, float y, float height, Color fill)
    {
        for (float x = x0; x < x1 - 0.05f; x += 0.3f)
        {
            p.OutlinedBox(x, y, Mathf.Min(x + 0.17f, x1), y + height, 0.01f, fill);
        }
    }

    /// <summary>An archery target on a stand: straw boss with painted rings.</summary>
    private static void Target(Painter p, Vector2 c, float r)
    {
        p.OutlinedLine(c + V(-r * 0.6f, -r * 1.6f), c + V(-r * 0.2f, 0), 0.03f, WoodDark);
        p.OutlinedLine(c + V(r * 0.6f, -r * 1.6f), c + V(r * 0.2f, 0), 0.03f, WoodDark);
        p.OutlinedCircle(c, r, Straw);
        p.Circle(c, r * 0.72f, new Color(0.95f, 0.95f, 0.9f));
        p.Circle(c, r * 0.48f, Flag);
        p.Circle(c, r * 0.22f, new Color(0.95f, 0.85f, 0.3f));
    }

    private static void StuckArrow(Painter p, Vector2 at)
    {
        p.Line(at, at + V(-0.16f, 0.03f), 0.014f, WoodLight);
        p.Line(at + V(-0.16f, 0.03f), at + V(-0.2f, 0.06f), 0.018f, Color.white);
    }

    /// <summary>Mine entrance in a rock face, with a cart rolling out on rails and an ore heap.</summary>
    private static void MineBase(Painter p, float t, Color rock, Color ore, bool glint)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.4f, 0.24f), Shadow);
        p.OutlinedPolygon(new[] { V(0.1f, 0.3f), V(0.3f, 1.6f), V(0.9f, 2.3f), V(1.7f, 2.45f), V(2.5f, 1.95f), V(2.85f, 0.3f) }, rock);
        p.Polygon(new[] { V(0.9f, 2.15f), V(1.7f, 2.32f), V(2.25f, 2.0f), V(1.55f, 1.98f) }, Color.Lerp(rock, Color.white, 0.25f));
        foreach (var c in new[] { V(0.5f, 1.3f), V(2.4f, 1.5f), V(0.75f, 0.9f), V(2.2f, 1.0f), V(1.9f, 1.85f) })
        {
            p.OutlinedCircle(c, 0.06f, ore);
        }
        // Timber-framed adit
        p.OutlinedBox(0.95f, 0.3f, 1.75f, 1.25f, 0.35f, Dark);
        p.OutlinedLine(V(0.95f, 0.3f), V(0.95f, 1.2f), 0.07f, Wood);
        p.OutlinedLine(V(1.75f, 0.3f), V(1.75f, 1.2f), 0.07f, Wood);
        p.OutlinedLine(V(0.88f, 1.2f), V(1.82f, 1.2f), 0.08f, Wood);
        // Rails and a cart rolling out and back
        p.Line(V(1.05f, 0.34f), V(2.85f, 0.2f), 0.02f, IronDark);
        p.Line(V(1.05f, 0.44f), V(2.85f, 0.34f), 0.02f, IronDark);
        float cx = 1.4f + (Wave(t) * 0.5f + 0.5f) * 0.9f;
        p.OutlinedPolygon(new[] { V(cx - 0.25f, 0.45f), V(cx + 0.25f, 0.42f), V(cx + 0.3f, 0.75f), V(cx - 0.3f, 0.78f) }, Wood);
        p.OutlinedCircle(V(cx - 0.15f, 0.42f), 0.07f, IronDark);
        p.OutlinedCircle(V(cx + 0.15f, 0.4f), 0.07f, IronDark);
        OutlinedBlob(p, ore, (V(cx - 0.12f, 0.8f), 0.08f), (V(cx + 0.06f, 0.82f), 0.09f), (V(cx + 0.18f, 0.78f), 0.06f));
        if (glint)
        {
            float g = Mathf.Repeat(t * 2f, 1f);
            p.Circle(V(cx - 0.05f + g * 0.2f, 0.86f), 0.02f, Color.white);
        }
        // Ore heap at the end of the line
        OutlinedBlob(p, ore, (V(2.55f, 0.42f), 0.16f), (V(2.75f, 0.38f), 0.12f), (V(2.65f, 0.55f), 0.11f));
    }

    /// <summary>A headframe over a shaft with a turning wheel.</summary>
    private static void Headframe(Painter p, float t, Vector2 at)
    {
        p.OutlinedLine(at + V(-0.35f, 0), at + V(0, 1.0f), 0.05f, Wood);
        p.OutlinedLine(at + V(0.35f, 0), at + V(0, 1.0f), 0.05f, Wood);
        p.OutlinedCircle(at + V(0, 1.0f), 0.2f, WoodLight);
        float a = t * Mathf.PI;
        for (int i = 0; i < 3; i++)
        {
            float s = a + i * Mathf.PI / 3f;
            p.Line(at + V(0, 1.0f) + new Vector2(Mathf.Cos(s), Mathf.Sin(s)) * 0.18f, at + V(0, 1.0f) - new Vector2(Mathf.Cos(s), Mathf.Sin(s)) * 0.18f, 0.02f, WoodDark);
        }
        p.Line(at + V(0.18f, 1.0f), at + V(0.18f, 0.1f), 0.012f, Dark);
    }

    // ---------- Iron Age ----------

    private static void DrawIronMine(Painter p, int level, float t)
    {
        MineBase(p, t, IronOreRock, Rust, false);
        if (level >= 1)
        {
            // Timber Shafts: a headframe winding ore up from a second shaft
            Headframe(p, t, V(0.45f, 1.55f));
        }
        if (level >= 2)
        {
            // Ore Carts: a second, loaded cart waiting on a siding
            p.OutlinedPolygon(new[] { V(0.15f, 0.3f), V(0.6f, 0.3f), V(0.65f, 0.6f), V(0.1f, 0.6f) }, Wood);
            OutlinedBlob(p, Rust, (V(0.28f, 0.65f), 0.09f), (V(0.45f, 0.67f), 0.1f));
            p.OutlinedCircle(V(0.25f, 0.28f), 0.06f, IronDark);
            p.OutlinedCircle(V(0.5f, 0.28f), 0.06f, IronDark);
        }
    }

    /// <summary>Stone House (ranked up from a Mud-Brick House): stone walls, smoking chimney.</summary>
    private static void DrawStoneHouse(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.3f), V(0.9f, 0.17f), Shadow);
        StoneBlocks(p, 0.22f, 0.28f, 1.78f, 1.12f, Stone, 0.17f);
        p.OutlinedBox(0.8f, 0.28f, 1.15f, 0.82f, 0.15f, Wood);
        p.InkLine(V(0.975f, 0.3f), V(0.975f, 0.78f), 0.5f);
        p.OutlinedBox(0.38f, 0.62f, 0.6f, 0.86f, 0.02f, Dark);
        p.OutlinedBox(1.4f, 0.62f, 1.62f, 0.86f, 0.02f, Dark);
        // Chimney with smoke
        p.OutlinedBox(1.38f, 1.3f, 1.56f, 1.8f, 0.01f, StoneDark);
        SmokePuffs(p, V(1.47f, 1.9f), t, 0.12f);
        // Roof: thatch, then Tiled Roof (terracotta tiles)
        bool tiled = level >= 1;
        p.OutlinedPolygon(new[] { V(0.08f, 1.08f), V(1.92f, 1.08f), V(1.0f, 1.82f) }, tiled ? Terracotta : StrawDark);
        if (tiled)
        {
            for (float y = 1.2f; y < 1.7f; y += 0.12f)
            {
                float half = (1.82f - y) / 0.74f * 0.92f;
                p.Line(V(1f - half + 0.04f, y), V(1f + half - 0.04f, y), 0.02f, TerracottaDark);
            }
        }
        else
        {
            for (int i = 1; i < 6; i++)
            {
                p.Line(V(1f, 1.78f), V(Mathf.Lerp(0.2f, 1.8f, i / 6f), 1.12f), 0.02f, Straw);
            }
        }
        if (level >= 2)
        {
            // Courtyard: a low wall and a little olive tree swaying beside the house
            p.OutlinedBox(1.62f, 0.22f, 1.98f, 0.42f, 0.01f, StoneLight);
            float sway = Wave(t) * 0.03f;
            p.OutlinedLine(V(1.82f, 0.42f), V(1.82f, 0.75f), 0.035f, WoodDark);
            OutlinedBlob(p, Leaf, (V(1.82f + sway, 0.85f), 0.13f), (V(1.72f + sway, 0.78f), 0.09f), (V(1.92f + sway, 0.78f), 0.09f));
        }
    }

    private static void DrawGarrison(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.4f, 0.25f), Shadow);
        if (level >= 3)
        {
            // Drill Yard: a training wing on the left
            StoneBlocks(p, 0.02f, 0.3f, 0.55f, 1.25f, StoneLight, 0.16f);
            p.OutlinedPolygon(new[] { V(0.0f, 1.2f), V(0.6f, 1.2f), V(0.6f, 1.55f) }, StrawDark);
        }
        // Stone lower walls, timber hall above, long thatch roof
        StoneBlocks(p, 0.45f, 0.3f, 2.55f, 0.95f, Stone, 0.16f);
        p.OutlinedBox(0.45f, 0.95f, 2.55f, 1.65f, 0.02f, Wood);
        for (float x = 0.65f; x < 2.5f; x += 0.24f)
        {
            p.InkLine(V(x, 0.98f), V(x, 1.6f), 0.5f);
        }
        p.OutlinedPolygon(new[] { V(0.25f, 1.6f), V(2.75f, 1.6f), V(2.35f, 2.4f), V(0.65f, 2.4f) }, StrawDark);
        p.OutlinedBox(1.25f, 0.3f, 1.75f, 1.05f, 0.22f, Dark);
        // Round wooden shields with iron bosses on the wall
        foreach (float x in new[] { 0.8f, 2.2f })
        {
            p.OutlinedCircle(V(x, 1.28f), 0.17f, new Color(0.35f, 0.5f, 0.85f));
            p.OutlinedCircle(V(x, 1.28f), 0.05f, Iron);
        }
        WavingFlag(p, V(2.7f, 1.65f), 1.2f, t, Flag);
        if (level >= 1)
        {
            // Iron Helmets: conical helmets on a rack of posts
            for (int i = 0; i < 3; i++)
            {
                float x = 2.35f + i * 0.2f;
                p.OutlinedLine(V(x, 0.2f), V(x, 0.62f), 0.025f, WoodDark);
                p.OutlinedPolygon(new[] { V(x - 0.08f, 0.6f), V(x + 0.08f, 0.6f), V(x, 0.78f) }, Iron);
            }
        }
        if (level >= 2)
        {
            // Forged Blades: an anvil and a smith's hammer striking sparks
            p.OutlinedPolygon(new[] { V(0.15f, 0.25f), V(0.45f, 0.25f), V(0.4f, 0.4f), V(0.55f, 0.48f), V(0.1f, 0.48f), V(0.2f, 0.4f) }, IronDark);
            int f = Mathf.Clamp((int)(t * 4), 0, 3);
            float[] angles = { 70f, 40f, 5f, 35f };
            Vector2 pivot = V(0.6f, 0.75f);
            Vector2 head = ToolTip(pivot, 0.32f, 180f + angles[f] * -1f + 0f);
            p.OutlinedLine(pivot, head, 0.03f, WoodLight);
            p.OutlinedBox(head.x - 0.06f, head.y - 0.04f, head.x + 0.06f, head.y + 0.04f, 0.01f, Iron);
            if (f == 2)
            {
                p.Circle(V(0.3f, 0.58f), 0.025f, FireCore);
                p.Circle(V(0.22f, 0.62f), 0.018f, Fire);
                p.Circle(V(0.38f, 0.64f), 0.018f, Fire);
            }
        }
    }

    private static void DrawArcheryRange(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.3f), V(1.45f, 0.2f), Shadow);
        // Trampled practice ground
        p.OutlinedBox(0.1f, 0.15f, 2.9f, 1.4f, 0.1f, new Color(0.62f, 0.66f, 0.38f));
        // Shooters' shelter on the left
        p.OutlinedBox(0.2f, 1.3f, 1.2f, 2.0f, 0.02f, Wood);
        p.OutlinedBox(0.45f, 1.3f, 0.8f, 1.75f, 0.1f, Dark);
        p.OutlinedPolygon(new[] { V(0.05f, 1.95f), V(1.35f, 1.95f), V(0.7f, 2.45f) }, Straw);
        // Targets down the range; arrows land one by one over the loop
        int hits = Mathf.Clamp((int)(t * 4), 0, 3) + 1;
        var targets = new[] { V(2.45f, 1.85f), V(2.45f, 0.95f) };
        foreach (var c in targets)
        {
            Target(p, c, 0.22f);
        }
        var spots = new[] { V(2.4f, 1.9f), V(2.5f, 0.98f), V(2.47f, 1.78f), V(2.42f, 0.9f) };
        for (int i = 0; i < hits; i++)
        {
            StuckArrow(p, spots[i]);
        }
        // Shooting line
        p.Line(V(1.4f, 0.25f), V(1.4f, 1.35f), 0.02f, new Color(0.95f, 0.95f, 0.85f));
        WavingFlag(p, V(1.25f, 1.95f), 0.8f, t, Flag);
        if (level >= 1)
        {
            // Straw Targets: a far target set further back
            Target(p, V(1.85f, 2.35f), 0.16f);
        }
        if (level >= 2)
        {
            // Fletchers: a table of shafts and feathers
            p.OutlinedBox(0.3f, 0.35f, 1.1f, 0.55f, 0.02f, WoodLight);
            p.OutlinedLine(V(0.4f, 0.15f), V(0.4f, 0.35f), 0.03f, WoodDark);
            p.OutlinedLine(V(1.0f, 0.15f), V(1.0f, 0.35f), 0.03f, WoodDark);
            for (int i = 0; i < 4; i++)
            {
                p.Line(V(0.45f + i * 0.13f, 0.6f), V(0.55f + i * 0.13f, 0.62f), 0.012f, WoodLight);
                p.Circle(V(0.45f + i * 0.13f, 0.6f), 0.02f, Color.white);
            }
        }
        if (level >= 3)
        {
            // Longer Lines: a second shelter for more archers
            p.OutlinedBox(1.35f, 1.6f, 1.95f, 2.0f, 0.02f, Wood);
            p.OutlinedPolygon(new[] { V(1.28f, 1.95f), V(2.02f, 1.95f), V(1.65f, 2.25f) }, Straw);
        }
    }

    private static void DrawStoneTower(Painter p, int level, float t)
    {
        // Roofless stone tower: the archer (added by the game) stands behind the battlements.
        p.Ellipse(V(1, 0.25f), V(0.7f, 0.15f), Shadow);
        const float deck = 1.15f;
        if (level >= 3)
        {
            // Thick Walls: buttresses at the foot
            p.OutlinedPolygon(new[] { V(0.15f, 0.2f), V(0.45f, 0.2f), V(0.45f, 0.8f) }, StoneDark);
            p.OutlinedPolygon(new[] { V(1.85f, 0.2f), V(1.55f, 0.2f), V(1.55f, 0.8f) }, StoneDark);
        }
        StoneBlocks(p, 0.42f, 0.2f, 1.58f, deck, Stone, 0.16f);
        p.OutlinedBox(0.88f, 0.2f, 1.12f, 0.6f, 0.12f, Dark);
        // Torch flickering by the door
        p.OutlinedLine(V(1.25f, 0.55f), V(1.3f, 0.75f), 0.025f, WoodDark);
        FlameShape(p, V(1.3f, 0.75f), 0.05f, 0.14f, t);
        // Overhanging platform and parapet; merlons behind the crew's cut line
        p.OutlinedBox(0.32f, deck, 1.68f, deck + 0.12f, 0.02f, StoneDark);
        float merlon = level >= 1 ? 0.22f : 0.14f; // Higher Battlements
        Merlons(p, 0.32f, 1.68f, deck + 0.3f, merlon, StoneLight);
        p.OutlinedBox(0.32f, deck + 0.12f, 1.68f, deck + 0.3f, 0.02f, StoneLight);
        if (level >= 1)
        {
            WavingFlag(p, V(1.62f, deck + 0.12f), 0.62f, t, Blue);
        }
        if (level >= 2)
        {
            // Iron Arrowheads: bundles of iron-tipped arrows on the parapet
            foreach (float x in new[] { 0.45f, 1.42f })
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 a = V(x + i * 0.03f, deck + 0.1f), b = V(x - 0.05f + i * 0.05f, deck + 0.44f);
                    p.OutlinedLine(a, b, 0.018f, WoodLight);
                    Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
                    float glint = Mathf.Sin((t + i * 0.3f) * Mathf.PI * 2) * 0.5f + 0.5f;
                    p.OutlinedPolygon(new[] { b - side * 0.035f, b + dir * 0.08f, b + side * 0.035f }, Color.Lerp(Iron, Color.white, glint * 0.5f));
                }
            }
        }
    }

    // ---------- Classical Age ----------

    private static void DrawGoldMine(Painter p, int level, float t)
    {
        MineBase(p, t, new Color(0.62f, 0.56f, 0.5f), Gold, true);
        if (level >= 1)
        {
            // Water Sluices: a wooden trough washing ore, water running
            p.OutlinedPolygon(new[] { V(0.1f, 1.45f), V(0.9f, 0.95f), V(0.95f, 1.05f), V(0.15f, 1.58f) }, Wood);
            for (int i = 0; i < 3; i++)
            {
                float k = Mathf.Repeat(t + i / 3f, 1f);
                Vector2 w = Vector2.Lerp(V(0.18f, 1.5f), V(0.88f, 1.02f), k);
                p.Line(w, w + V(0.08f, -0.05f), 0.02f, new Color(0.6f, 0.8f, 1f));
            }
        }
        if (level >= 2)
        {
            // Deep Galleries: a headframe over a deep shaft
            Headframe(p, t, V(2.3f, 1.7f));
        }
    }

    /// <summary>Insula (ranked up from a Stone House): plastered Roman apartment block.</summary>
    private static void DrawInsula(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.28f), V(0.92f, 0.16f), Shadow);
        float top = level >= 1 ? 1.62f : 1.2f; // Upper Storeys
        p.OutlinedBox(0.18f, 0.25f, 1.82f, top, 0.02f, Ochre);
        // Shop arch on the ground floor, windows above
        p.OutlinedBox(0.3f, 0.25f, 0.75f, 0.72f, 0.2f, Dark);
        p.OutlinedBox(1.05f, 0.25f, 1.35f, 0.7f, 0.02f, Wood);
        for (float y = 0.85f; y < top - 0.25f; y += 0.42f)
        {
            for (float x = 0.35f; x < 1.75f; x += 0.42f)
            {
                p.OutlinedBox(x, y, x + 0.18f, y + 0.22f, 0.02f, Dark);
                p.OutlinedBox(x - 0.03f, y - 0.04f, x + 0.21f, y, 0.0f, WoodLight);
            }
        }
        // Washing line flapping between windows
        float flap = Wave(t) * 0.03f;
        p.Line(V(0.28f, top - 0.12f), V(1.72f, top - 0.12f), 0.01f, Dark);
        p.OutlinedPolygon(new[] { V(0.5f, top - 0.12f), V(0.7f, top - 0.12f), V(0.72f + flap, top - 0.32f), V(0.52f + flap, top - 0.3f) }, Plaster);
        p.OutlinedPolygon(new[] { V(1.1f, top - 0.12f), V(1.25f, top - 0.12f), V(1.27f + flap, top - 0.3f), V(1.12f + flap, top - 0.28f) }, new Color(0.55f, 0.7f, 0.9f));
        // Low terracotta roof
        p.OutlinedPolygon(new[] { V(0.08f, top), V(1.92f, top), V(1.6f, top + 0.3f), V(0.4f, top + 0.3f) }, Terracotta);
        for (float x = 0.3f; x < 1.75f; x += 0.15f)
        {
            p.Line(V(x, top + 0.03f), V(x + 0.05f, top + 0.27f), 0.015f, TerracottaDark);
        }
        if (level >= 2)
        {
            // Fountain: a stone basin with a splashing spout
            p.OutlinedEllipse(V(1.62f, 0.3f), V(0.3f, 0.1f), Marble);
            p.Ellipse(V(1.62f, 0.32f), V(0.24f, 0.06f), WaterBlue);
            p.OutlinedLine(V(1.62f, 0.32f), V(1.62f, 0.55f), 0.04f, MarbleShade);
            float h = 0.12f + Wave(t) * 0.03f;
            p.Line(V(1.62f, 0.56f), V(1.52f, 0.56f + h * 0.5f), 0.02f, new Color(0.6f, 0.8f, 1f));
            p.Line(V(1.62f, 0.56f), V(1.72f, 0.56f + h * 0.5f), 0.02f, new Color(0.6f, 0.8f, 1f));
        }
    }

    private static void DrawLegionFort(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.35f), V(1.45f, 0.24f), Shadow);
        if (level >= 3)
        {
            // Extra Contubernium: leather tents inside the walls, behind
            foreach (float x in new[] { 0.55f, 2.45f })
            {
                p.OutlinedPolygon(new[] { V(x - 0.35f, 1.65f), V(x + 0.35f, 1.65f), V(x, 2.15f) }, Hide);
                p.InkLine(V(x, 1.65f), V(x, 2.12f), 0.6f);
            }
        }
        // Walls with crenellations and corner towers
        StoneBlocks(p, 0.15f, 0.3f, 2.85f, 1.4f, Stone, 0.18f);
        Merlons(p, 0.15f, 2.85f, 1.4f, 0.16f, Stone);
        StoneBlocks(p, 0.05f, 0.3f, 0.55f, 1.75f, StoneLight, 0.18f);
        StoneBlocks(p, 2.45f, 0.3f, 2.95f, 1.75f, StoneLight, 0.18f);
        Merlons(p, 0.05f, 0.55f, 1.75f, 0.16f, StoneLight);
        Merlons(p, 2.45f, 2.95f, 1.75f, 0.16f, StoneLight);
        // Gatehouse arch
        p.OutlinedBox(1.15f, 0.3f, 1.85f, 1.15f, 0.34f, Dark);
        // Standard with a golden eagle, cloth waving
        p.OutlinedLine(V(1.5f, 1.4f), V(1.5f, 2.6f), 0.04f, WoodDark);
        float w = Wave(t) * 0.04f;
        p.OutlinedPolygon(new[] { V(1.32f, 2.3f), V(1.68f, 2.3f), V(1.68f + w, 1.95f), V(1.32f + w, 1.95f) }, Flag);
        p.OutlinedLine(V(1.32f, 2.32f), V(1.68f, 2.32f), 0.03f, Gold);
        p.OutlinedPolygon(new[] { V(1.38f, 2.6f), V(1.5f, 2.72f), V(1.62f, 2.6f), V(1.5f, 2.55f) }, Gold);
        if (level >= 1)
        {
            // Testudo Drills: a stack of scuta leaning by the gate
            for (int i = 0; i < 3; i++)
            {
                float x = 0.7f + i * 0.12f;
                p.OutlinedPolygon(new[] { V(x, 0.2f), V(x + 0.18f, 0.2f), V(x + 0.24f, 0.72f), V(x + 0.06f, 0.72f) }, new Color(0.3f, 0.45f, 0.85f));
                p.Circle(V(x + 0.12f, 0.46f), 0.03f, Gold);
            }
        }
        if (level >= 2)
        {
            // Gladius Training: a post scarred by sword cuts, and a rack of swords
            p.OutlinedLine(V(2.1f, 0.15f), V(2.1f, 0.8f), 0.06f, Wood);
            for (int i = 0; i < 3; i++)
            {
                float x = 2.3f + i * 0.12f;
                p.OutlinedLine(V(x, 0.2f), V(x, 0.62f), 0.03f, Iron);
                p.OutlinedLine(V(x - 0.05f, 0.25f), V(x + 0.05f, 0.25f), 0.02f, Gold);
            }
        }
    }

    private static void DrawArcherAcademy(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.3f), V(1.45f, 0.2f), Shadow);
        // Colonnaded hall at the back
        p.OutlinedBox(0.15f, 1.35f, 2.85f, 1.5f, 0.0f, MarbleShade);
        for (float x = 0.3f; x < 2.8f; x += 0.38f)
        {
            p.OutlinedBox(x, 1.5f, x + 0.12f, 2.2f, 0.02f, Marble);
        }
        p.OutlinedBox(0.1f, 2.2f, 2.9f, 2.35f, 0.0f, Marble);
        p.OutlinedPolygon(new[] { V(0.05f, 2.35f), V(2.95f, 2.35f), V(1.5f, 2.85f) }, Marble);
        p.OutlinedPolygon(new[] { V(0.6f, 2.42f), V(2.4f, 2.42f), V(1.5f, 2.72f) }, MarbleShade);
        // Range in front
        p.OutlinedBox(0.1f, 0.15f, 2.9f, 1.3f, 0.08f, new Color(0.7f, 0.66f, 0.45f));
        Target(p, V(2.5f, 0.85f), 0.2f);
        int hits = Mathf.Clamp((int)(t * 4), 0, 3) + 1;
        var spots = new[] { V(2.46f, 0.9f), V(2.55f, 0.82f), V(2.5f, 0.92f), V(2.44f, 0.8f) };
        for (int i = 0; i < hits; i++) StuckArrow(p, spots[i]);
        p.Line(V(1.2f, 0.22f), V(1.2f, 1.25f), 0.02f, new Color(0.95f, 0.95f, 0.85f));
        if (level >= 1)
        {
            // Composite Bows: a rack of recurved bows
            for (int i = 0; i < 3; i++)
            {
                float x = 0.35f + i * 0.2f;
                p.Draw(q => Mathf.Max(Mathf.Abs(SdCircle(q, V(x - 0.15f, 0.65f), 0.22f)) - 0.014f, x - 0.08f - q.x), new Rect(x - 0.4f, 0.4f, 0.5f, 0.5f), Dark);
            }
        }
        if (level >= 2)
        {
            // Iron Volleys: crates of iron-tipped arrows
            p.OutlinedBox(0.9f, 0.2f, 1.1f, 0.42f, 0.01f, Wood);
            for (int i = 0; i < 4; i++)
            {
                p.Line(V(0.93f + i * 0.05f, 0.42f), V(0.93f + i * 0.05f, 0.62f), 0.012f, WoodLight);
                p.Circle(V(0.93f + i * 0.05f, 0.63f), 0.018f, Iron);
            }
        }
        if (level >= 3)
        {
            // Second Range: another target lane
            Target(p, V(2.0f, 0.55f), 0.16f);
        }
    }

    private static void DrawStables(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.35f), V(1.45f, 0.24f), Shadow);
        if (level >= 3)
        {
            // Paddock: a fenced field off to the side
            p.OutlinedLine(V(0.05f, 0.45f), V(0.05f, 1.3f), 0.03f, WoodDark);
            p.OutlinedLine(V(0.05f, 0.75f), V(0.6f, 0.75f), 0.025f, WoodLight);
            p.OutlinedLine(V(0.05f, 1.05f), V(0.6f, 1.05f), 0.025f, WoodLight);
        }
        // Long timber stable with stall doors
        p.OutlinedBox(0.4f, 0.3f, 2.6f, 1.55f, 0.02f, Wood);
        p.OutlinedPolygon(new[] { V(0.25f, 1.5f), V(2.75f, 1.5f), V(2.4f, 2.2f), V(0.6f, 2.2f) }, Terracotta);
        for (float x = 0.55f; x < 2.5f; x += 0.7f)
        {
            p.OutlinedBox(x, 0.3f, x + 0.5f, 1.05f, 0.03f, WoodDark);
            p.Line(V(x, 0.68f), V(x + 0.5f, 0.68f), 0.02f, Wood);
        }
        // A horse looking out over the middle stall door, nodding
        float nod = Wave(t) * 0.04f;
        Vector2 h = V(1.5f, 0.82f + nod);
        p.OutlinedPolygon(new[] { h + V(-0.12f, -0.1f), h + V(0.08f, -0.1f), h + V(0.1f, 0.18f), h + V(-0.08f, 0.2f) }, HorseBrown);
        p.OutlinedPolygon(new[] { h + V(0.05f, 0.15f), h + V(0.32f, 0.05f), h + V(0.3f, -0.05f), h + V(0.05f, 0.0f) }, HorseBrown);
        p.OutlinedPolygon(new[] { h + V(-0.02f, 0.18f), h + V(0.0f, 0.28f), h + V(0.04f, 0.18f) }, HorseBrown);
        p.Circle(h + V(0.14f, 0.1f), 0.018f, p.Ink);
        p.Line(h + V(-0.08f, 0.2f), h + V(-0.1f, -0.05f), 0.03f, Dark);
        // Hay bales
        p.OutlinedBox(2.25f, 0.15f, 2.75f, 0.42f, 0.04f, HayColor);
        p.OutlinedBox(2.35f, 0.42f, 2.7f, 0.65f, 0.04f, HayColor);
        WavingFlag(p, V(2.65f, 1.55f), 1.0f, t, Flag);
        if (level >= 1)
        {
            // Saddle Blankets drying on the rail
            p.OutlinedLine(V(0.25f, 0.55f), V(0.25f, 0.15f), 0.03f, WoodDark);
            p.OutlinedPolygon(new[] { V(0.1f, 0.5f), V(0.4f, 0.5f), V(0.4f, 0.25f), V(0.1f, 0.25f) }, Blue);
        }
        if (level >= 2)
        {
            // Lances leaning on the wall
            for (int i = 0; i < 3; i++)
            {
                float x = 2.0f + i * 0.08f;
                p.OutlinedLine(V(x, 0.2f), V(x + 0.15f, 1.6f), 0.025f, WoodLight);
                p.OutlinedPolygon(new[] { V(x + 0.12f, 1.58f), V(x + 0.2f, 1.6f), V(x + 0.17f, 1.74f) }, Iron);
            }
        }
    }

    private static void DrawBallistaTower(Painter p, int level, float t)
    {
        // Roofless: the ballista and its crew (added by the game) sit on the open platform.
        p.Ellipse(V(1, 0.25f), V(0.75f, 0.15f), Shadow);
        const float deck = 1.15f;
        bool marble = level >= 3; // Marble Facing
        StoneBlocks(p, 0.38f, 0.2f, 1.62f, deck, marble ? Marble : Stone, 0.2f);
        if (marble)
        {
            p.OutlinedBox(0.32f, 0.2f, 0.44f, deck, 0.0f, MarbleShade);
            p.OutlinedBox(1.56f, 0.2f, 1.68f, deck, 0.0f, MarbleShade);
        }
        p.OutlinedBox(0.85f, 0.2f, 1.15f, 0.62f, 0.15f, Dark);
        p.OutlinedBox(0.25f, deck, 1.75f, deck + 0.12f, 0.02f, StoneDark);
        p.OutlinedBox(0.25f, deck + 0.12f, 1.75f, deck + 0.3f, 0.02f, marble ? Marble : StoneLight);
        // Shields hung along the parapet
        foreach (float x in new[] { 0.45f, 1.55f })
        {
            p.OutlinedPolygon(new[] { V(x - 0.08f, deck + 0.02f), V(x + 0.08f, deck + 0.02f), V(x + 0.08f, deck + 0.28f), V(x - 0.08f, deck + 0.28f) }, new Color(0.3f, 0.45f, 0.85f));
            p.Circle(V(x, deck + 0.15f), 0.025f, Gold);
        }
        WavingFlag(p, V(1.7f, deck + 0.12f), 0.62f, t, Flag);
        if (level >= 1)
        {
            // Torsion Springs: coils of sinew rope
            p.OutlinedCircle(V(0.2f, 0.35f), 0.12f, Hide);
            p.OutlinedCircle(V(0.2f, 0.35f), 0.05f, WoodDark);
        }
        if (level >= 2)
        {
            // Iron Bolts: a rack of bolts by the door
            for (int i = 0; i < 3; i++)
            {
                float x = 1.72f + i * 0.08f;
                p.OutlinedLine(V(x, 0.2f), V(x, 0.7f), 0.022f, Wood);
                p.OutlinedPolygon(new[] { V(x - 0.03f, 0.68f), V(x, 0.8f), V(x + 0.03f, 0.68f) }, Iron);
            }
        }
    }

    // ---------- Deposits and icons ----------

    private static void DrawIronIcon(Painter p)
    {
        p.OutlinedPolygon(new[] { V(0.1f, 0.18f), V(0.62f, 0.18f), V(0.54f, 0.42f), V(0.18f, 0.42f) }, IronDark);
        p.OutlinedPolygon(new[] { V(0.38f, 0.42f), V(0.9f, 0.42f), V(0.82f, 0.68f), V(0.46f, 0.68f) }, Iron);
        p.Line(V(0.52f, 0.6f), V(0.76f, 0.6f), 0.04f, Color.white);
    }

    private static void DrawGoldIcon(Painter p)
    {
        foreach (float y in new[] { 0.25f, 0.38f, 0.51f })
        {
            p.OutlinedEllipse(V(0.42f, y), V(0.3f, 0.1f), Gold);
        }
        p.OutlinedEllipse(V(0.68f, 0.3f), V(0.22f, 0.22f), Gold);
        p.Circle(V(0.68f, 0.3f), 0.12f, new Color(1f, 0.88f, 0.45f));
    }
}
