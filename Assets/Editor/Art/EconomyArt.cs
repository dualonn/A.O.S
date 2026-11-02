using UnityEngine;
using static Painter;

/// <summary>
/// Production-chain buildings and resources: the Charcoal Kiln (Bronze Age), the Smithy (Iron Age)
/// and the Mint (Classical Age), plus the Charcoal, Tools and Coin icons.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Color Clay = new Color(0.72f, 0.5f, 0.36f);
    private static readonly Color ClayDark = new Color(0.55f, 0.36f, 0.25f);
    private static readonly Color CharcoalBlack = new Color(0.16f, 0.15f, 0.15f);
    private static readonly Color CharcoalGrey = new Color(0.32f, 0.31f, 0.31f);
    private static readonly Color Ember = new Color(1f, 0.42f, 0.12f);
    private static readonly Color SteelGrey = new Color(0.52f, 0.54f, 0.58f);
    private static readonly Color SteelLight = new Color(0.75f, 0.77f, 0.8f);
    private static readonly Color CoinGold = new Color(0.98f, 0.8f, 0.3f);
    private static readonly Color CoinShine = new Color(1f, 0.95f, 0.7f);

    // ---------- Bronze Age: Charcoal Kiln (2x2) ----------

    /// <summary>A domed clay kiln smouldering wood into charcoal, with a woodpile and a heap of charcoal.</summary>
    private static void DrawCharcoalKiln(Painter p, int level, float t)
    {
        p.Ellipse(V(1f, 0.32f), V(0.92f, 0.2f), Shadow);
        bool brick = level >= 2;

        // Woodpile waiting to go in (left)
        for (int i = 0; i < 3; i++)
        {
            float y = 0.3f + i * 0.13f, x0 = 0.05f + i * 0.05f;
            p.OutlinedBox(x0, y - 0.06f, x0 + 0.42f - i * 0.06f, y + 0.06f, 0.05f, Wood);
            p.OutlinedEllipse(V(x0 + 0.42f - i * 0.06f, y), V(0.05f, 0.06f), WoodLight);
        }

        // The kiln: a turf/clay dome (later a brick beehive kiln)
        var dome = new[]
        {
            V(0.55f, 0.3f), V(1.65f, 0.3f), V(1.7f, 0.75f), V(1.55f, 1.12f), V(1.3f, 1.32f),
            V(0.9f, 1.32f), V(0.65f, 1.12f), V(0.5f, 0.75f),
        };
        p.OutlinedPolygon(dome, brick ? MudBrick : level >= 1 ? new Color(0.45f, 0.5f, 0.3f) : Clay);
        if (brick)
        {
            for (float y = 0.45f; y < 1.25f; y += 0.15f)
            {
                // Follow the dome's curve so courses stay inside it.
                float inset = y > 0.75f ? (y - 0.75f) * 0.75f : 0f;
                p.InkLine(V(0.6f + inset, y), V(1.64f - inset, y), 0.4f);
            }
        }
        else if (level >= 1)
        {
            // Turf clamp: grass tufts on the cover
            foreach (var (x, y) in new[] { (0.75f, 0.9f), (1.1f, 1.18f), (1.42f, 0.95f), (1.0f, 0.62f) })
            {
                p.Line(V(x, y), V(x - 0.04f, y + 0.08f), 0.02f, LeafLight);
                p.Line(V(x, y), V(x + 0.04f, y + 0.08f), 0.02f, LeafLight);
            }
        }
        else
        {
            p.Line(V(0.72f, 0.6f), V(0.9f, 0.85f), 0.018f, ClayDark);
            p.Line(V(1.35f, 0.5f), V(1.5f, 0.7f), 0.018f, ClayDark);
        }

        // Glowing mouth at the front
        p.OutlinedBox(0.9f, 0.3f, 1.3f, 0.58f, 0.18f, Dark);
        float glow = 0.13f + 0.03f * Wave(t);
        p.Circle(V(1.1f, 0.4f), glow, Ember);
        p.Circle(V(1.1f, 0.38f), glow * 0.5f, FireCore);

        // Vent smoke
        p.OutlinedBox(1.03f, 1.28f, 1.17f, 1.4f, 0.02f, brick ? MudBrickDark : ClayDark);
        SmokePuffs(p, V(1.1f, 1.45f), t, 0.45f);

        // Charcoal heap (right)
        OutlinedBlob(p, CharcoalBlack, (V(1.78f, 0.36f), 0.13f), (V(1.92f, 0.34f), 0.11f), (V(1.85f, 0.46f), 0.1f));
        p.Circle(V(1.82f, 0.44f), 0.03f, CharcoalGrey);
        p.Circle(V(1.93f, 0.38f), 0.025f, CharcoalGrey);
    }

    // ---------- Iron Age: Smithy (3x2, art 3x3) ----------

    /// <summary>An open-fronted forge: hearth, anvil with a hammer striking, finished tools on a rack.</summary>
    private static void DrawSmithy(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.35f), V(1.4f, 0.22f), Shadow);

        // Back wall and chimney
        p.OutlinedBox(2.15f, 1.3f, 2.5f, 2.7f, 0.02f, StoneDark);
        SmokePuffs(p, V(2.32f, 2.8f), t, 0.4f);
        p.OutlinedBox(0.25f, 0.3f, 2.75f, 1.7f, 0.02f, Stone);
        var rng = new System.Random(7);
        for (int i = 0; i < 12; i++)
        {
            float x = 0.45f + (float)rng.NextDouble() * 2.1f, y = 0.9f + (float)rng.NextDouble() * 0.7f;
            p.Box(V(x, y), V(0.24f, 0.12f), 0.03f, StoneLight);
        }

        // Timber lean-to roof over the work area
        p.OutlinedPolygon(new[] { V(0.05f, 1.55f), V(2.95f, 1.55f), V(2.7f, 2.05f), V(0.3f, 2.05f) }, level >= 1 ? TerracottaDark : Wood);
        for (float x = 0.4f; x < 2.7f; x += 0.3f)
        {
            p.InkLine(V(x, 1.58f), V(x + 0.05f, 2.02f), 0.4f);
        }
        p.OutlinedLine(V(0.3f, 0.3f), V(0.3f, 1.55f), 0.06f, WoodDark);
        p.OutlinedLine(V(2.7f, 0.3f), V(2.7f, 1.55f), 0.06f, WoodDark);

        // Hearth glowing in the back
        p.OutlinedBox(1.65f, 0.3f, 2.45f, 0.85f, 0.08f, StoneDark);
        p.OutlinedBox(1.78f, 0.62f, 2.32f, 0.8f, 0.05f, Dark);
        float glow = 0.75f + 0.25f * Wave(t);
        p.Ellipse(V(2.05f, 0.71f), V(0.22f * glow, 0.06f), Ember);
        p.Ellipse(V(2.05f, 0.71f), V(0.12f * glow, 0.035f), FireCore);
        if (level >= 1)
        {
            // Bellows beside the hearth
            float squash = 0.06f + 0.05f * (Wave(t) * 0.5f + 0.5f);
            p.OutlinedPolygon(new[] { V(2.45f, 0.45f), V(2.75f, 0.45f - squash * 0.3f), V(2.75f, 0.55f + squash), V(2.45f, 0.6f) }, Hide);
        }

        // Anvil on a stump, hammer rising and striking (sparks on the hit frame)
        p.OutlinedBox(0.95f, 0.3f, 1.2f, 0.55f, 0.03f, Wood);
        p.OutlinedPolygon(new[] { V(0.8f, 0.55f), V(1.35f, 0.55f), V(1.42f, 0.68f), V(0.75f, 0.68f) }, SteelGrey);
        p.Line(V(0.8f, 0.66f), V(1.38f, 0.66f), 0.02f, SteelLight);
        int frame = Mathf.Clamp((int)(t * 4), 0, 3);
        float[] angles = { 70f, 35f, 0f, 30f };
        Vector2 pivot = V(1.55f, 0.95f);
        Vector2 head = ToolTip(pivot, 0.42f, 180f - angles[frame]);
        p.OutlinedLine(pivot, head, 0.04f, WoodLight);
        p.OutlinedBox(head.x - 0.08f, head.y - 0.05f, head.x + 0.08f, head.y + 0.05f, 0.01f, SteelGrey);
        if (frame == 2)
        {
            p.Circle(V(1.0f, 0.78f), 0.025f, FireCore);
            p.Circle(V(0.92f, 0.86f), 0.02f, Ember);
            p.Circle(V(1.08f, 0.9f), 0.018f, FireCore);
        }

        // Tool rack: axe, pick and blade hanging on the wall
        p.OutlinedLine(V(0.45f, 1.35f), V(0.45f, 0.95f), 0.03f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.45f, 1.3f), V(0.6f, 1.36f), V(0.6f, 1.2f), V(0.45f, 1.22f) }, SteelGrey);
        p.OutlinedLine(V(0.75f, 1.38f), V(0.75f, 0.98f), 0.03f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.62f, 1.36f), V(0.75f, 1.42f), V(0.88f, 1.36f), V(0.75f, 1.38f) }, SteelGrey);
        if (level >= 2)
        {
            // Second Forge: a quench trough and a finished sword
            p.OutlinedBox(0.05f, 0.25f, 0.6f, 0.42f, 0.04f, Wood);
            p.Box(V(0.32f, 0.38f), V(0.48f, 0.05f), 0.02f, WaterBlue);
            p.OutlinedLine(V(1.0f, 1.0f), V(1.0f, 1.42f), 0.035f, SteelLight);
            p.OutlinedLine(V(0.92f, 1.04f), V(1.08f, 1.04f), 0.03f, Bronze);
        }
    }

    // ---------- Classical Age: Mint (2x2, art 2x3) ----------

    /// <summary>A small marble mint: columns, a pediment, stacks of coin and a die being struck.</summary>
    private static void DrawMint(Painter p, int level, float t)
    {
        p.Ellipse(V(1f, 0.3f), V(0.95f, 0.18f), Shadow);
        // Steps and floor
        p.OutlinedBox(0.1f, 0.22f, 1.9f, 0.36f, 0.01f, MarbleShade);
        p.OutlinedBox(0.18f, 0.36f, 1.82f, 0.46f, 0.01f, Marble);
        // Back wall
        p.OutlinedBox(0.3f, 0.46f, 1.7f, 1.5f, 0.01f, MarbleShade);
        // Columns
        foreach (float x in new[] { 0.32f, 0.78f, 1.22f, 1.68f })
        {
            p.OutlinedBox(x - 0.08f, 0.46f, x + 0.08f, 1.5f, 0.01f, Marble);
            p.InkLine(V(x, 0.5f), V(x, 1.45f), 0.35f);
        }
        // Entablature and pediment
        p.OutlinedBox(0.12f, 1.5f, 1.88f, 1.66f, 0.01f, Marble);
        p.OutlinedPolygon(new[] { V(0.08f, 1.66f), V(1.92f, 1.66f), V(1f, 2.12f) }, level >= 1 ? Terracotta : Marble);
        p.OutlinedCircle(V(1f, 1.83f), 0.1f, CoinGold);

        // Coin stacks between the columns, glinting
        foreach (var (x, n) in new[] { (0.55f, level >= 1 ? 4 : 3), (1.45f, level >= 2 ? 5 : 3) })
        {
            for (int i = 0; i < n; i++)
            {
                p.OutlinedEllipse(V(x, 0.52f + i * 0.06f), V(0.11f, 0.035f), CoinGold);
            }
        }
        float shine = Mathf.Repeat(t, 1f);
        if (shine < 0.5f)
        {
            Vector2 c = V(0.5f + shine * 0.25f, 0.6f + shine * 0.2f);
            p.Line(c - V(0.04f, 0), c + V(0.04f, 0), 0.012f, CoinShine);
            p.Line(c - V(0, 0.04f), c + V(0, 0.04f), 0.012f, CoinShine);
        }

        // Die and hammer striking coins (centre)
        p.OutlinedBox(0.92f, 0.46f, 1.08f, 0.62f, 0.01f, SteelGrey);
        int frame = Mathf.Clamp((int)(t * 4), 0, 3);
        float lift = new[] { 0.3f, 0.18f, 0.02f, 0.12f }[frame];
        p.OutlinedBox(0.95f, 0.64f + lift, 1.05f, 0.74f + lift, 0.01f, SteelGrey);
        p.OutlinedLine(V(1f, 0.74f + lift), V(1f, 0.98f + lift), 0.03f, WoodLight);
        if (frame == 2)
        {
            p.Circle(V(0.86f, 0.7f), 0.02f, CoinShine);
            p.Circle(V(1.14f, 0.72f), 0.02f, CoinShine);
        }
    }

    // ---------- Icons ----------

    private static void DrawCharcoalIcon(Painter p)
    {
        OutlinedBlob(p, CharcoalBlack, (V(0.35f, 0.35f), 0.2f), (V(0.65f, 0.32f), 0.18f), (V(0.5f, 0.6f), 0.2f));
        p.Circle(V(0.45f, 0.62f), 0.05f, CharcoalGrey);
        p.Circle(V(0.68f, 0.38f), 0.04f, CharcoalGrey);
        p.Circle(V(0.33f, 0.3f), 0.04f, Ember);
    }

    private static void DrawToolsIcon(Painter p)
    {
        // Hammer and pick, crossed
        p.OutlinedLine(V(0.2f, 0.15f), V(0.75f, 0.75f), 0.07f, WoodLight);
        p.OutlinedLine(V(0.8f, 0.15f), V(0.25f, 0.75f), 0.07f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.62f, 0.86f), V(0.88f, 0.6f), V(0.8f, 0.55f), V(0.56f, 0.8f) }, SteelGrey);
        p.OutlinedPolygon(new[] { V(0.08f, 0.68f), V(0.25f, 0.9f), V(0.42f, 0.75f), V(0.25f, 0.7f) }, SteelLight);
    }

    private static void DrawCoinIcon(Painter p)
    {
        p.OutlinedEllipse(V(0.38f, 0.3f), V(0.26f, 0.09f), CoinGold);
        p.OutlinedEllipse(V(0.38f, 0.4f), V(0.26f, 0.09f), CoinGold);
        p.OutlinedCircle(V(0.62f, 0.55f), 0.27f, CoinGold);
        p.Circle(V(0.62f, 0.55f), 0.18f, new Color(0.9f, 0.68f, 0.2f));
        p.Circle(V(0.62f, 0.55f), 0.13f, CoinGold);
        p.Circle(V(0.55f, 0.63f), 0.04f, CoinShine);
    }
}
