using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Stone and ore deposits: shaded boulders in the same warm rock colours as the hills,
/// in several layouts (lone boulder, cluster, slab, rubble) so deposit fields look natural.
/// Ores are the same boulders with copper, iron or gold showing through.
/// </summary>
public static partial class PlaceholderArt
{
    private enum Ore { None, Copper, Iron, Gold, Rune, Saltpetre, Quicksilver, Coal, Aether, Oil, Uranium, Silicon, Mana }

    private const int StoneVariants = 4;
    private const int OreVariants = 3;

    [MenuItem("Tools/Age of Stickman/Generate Deposit Art")]
    public static void GenerateDepositsOnly()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (art == null)
        {
            Debug.LogError("No ArtLibrary yet: run Generate Placeholder Art first.");
            return;
        }
        GeneratePixelProps(art, database);   // pixel art now (PixelWorld.cs)
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Deposit art generated.");
    }

    private static void GenerateDepositArt(ArtLibrary art, GameDatabase database)
    {
        Directory.CreateDirectory(Path.Combine(Root, "World/Deposits"));
        art.rocks = DepositSet("Stone", StoneVariants, Ore.None);
        var copper = DepositSet("Copper", OreVariants, Ore.Copper);
        var iron = DepositSet("Iron", OreVariants, Ore.Iron);
        var gold = DepositSet("Gold", OreVariants, Ore.Gold);
        if (database == null)
        {
            return;
        }
        foreach (var deposit in database.deposits)
        {
            if (deposit == null) continue;
            switch (deposit.id)
            {
                case "Stone": deposit.sprites = new List<Sprite>(art.rocks); break;
                case "CopperOre": deposit.sprites = copper; break;
                case "IronOre": deposit.sprites = iron; break;
                case "GoldOre": deposit.sprites = gold; break;
                default: continue;
            }
            EditorUtility.SetDirty(deposit);
        }
    }

    private static List<Sprite> DepositSet(string name, int count, Ore ore)
    {
        var sprites = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            int variant = i;
            sprites.Add(Prop($"World/Deposits/{name}{i}.png", 1f, 0.75f, p => DrawDeposit(p, variant, ore)));
        }
        return sprites;
    }

    // ---------- Boulders ----------

    /// <summary>
    /// A chunky rock seen from the game's 3/4 top-down angle: a lit top face, a front face and a
    /// shaded right side, sitting on the ground at 'foot'.
    /// </summary>
    private struct Boulder
    {
        public Vector2[] points;              // silhouette
        public Vector2[] top, side;           // faces
        public Vector2 center, size;          // centre and half-size, for placing ore
    }

    private static Boulder MakeBoulder(Vector2 foot, float width, float height, int seed)
    {
        var rng = new System.Random(seed);
        float J() => ((float)rng.NextDouble() - 0.5f) * 0.12f;
        float w = width * 0.5f, h = height;
        var leftMid = foot + V(-w * (1.02f + J()), h * (0.5f + J()));
        var topLeft = foot + V(-w * (0.5f + J()), h * (0.94f + J() * 0.5f));
        var topRight = foot + V(w * (0.55f + J()), h);
        var rightMid = foot + V(w * (1.0f + J()), h * (0.58f + J()));
        var frontTop = foot + V(w * (0.12f + J()), h * (0.5f + J()));   // where top, front and side meet
        var bottomLeft = foot + V(-w * 0.9f, 0);
        var bottomRight = foot + V(w * 0.92f, 0);
        var bottomMid = foot + V(w * (0.15f + J()), 0);
        return new Boulder
        {
            points = new[] { bottomLeft, leftMid, topLeft, topRight, rightMid, bottomRight },
            top = new[] { leftMid, topLeft, topRight, rightMid, frontTop },
            side = new[] { frontTop, rightMid, bottomRight, bottomMid },
            center = foot + V(0, h * 0.45f),
            size = V(w, h * 0.5f),
        };
    }

    private static void DrawBoulder(Painter p, Boulder b, Color baseColor)
    {
        var full = new Rect(0, 0, 1, 0.75f);
        Color lit = Color.Lerp(baseColor, Color.white, 0.38f);
        Color shade = Color.Lerp(baseColor, Color.black, 0.25f);
        Color ink = new Color(0.22f, 0.18f, 0.15f);
        var pts = b.points;
        System.Func<Vector2, float> body = q => SdPolygon(q, pts);
        var top = b.top;
        var side = b.side;

        p.Draw(q => body(q) - 0.02f, full, ink);
        p.Draw(body, full, baseColor);                                          // front face
        p.Draw(q => Mathf.Max(SdPolygon(q, side), body(q)), full, shade);      // right side
        p.Draw(q => Mathf.Max(SdPolygon(q, top), body(q)), full, lit);         // top face
        // Edges between the faces.
        p.Draw(q => Mathf.Max(Mathf.Min(SdSegment(q, top[0], top[4]), SdSegment(q, top[4], top[3])) - 0.006f, body(q)), full, ink);
        p.Draw(q => Mathf.Max(SdSegment(q, side[0], side[3]) - 0.006f, body(q)), full, ink);
    }

    /// <summary>Boulder layouts for each variant, back to front.</summary>
    private static List<Boulder> DepositLayout(int variant)
    {
        var list = new List<Boulder>();
        switch (variant)
        {
            case 0: // one big rock with a couple of stones
                list.Add(MakeBoulder(V(0.5f, 0.12f), 0.5f, 0.42f, 11));
                list.Add(MakeBoulder(V(0.85f, 0.06f), 0.15f, 0.12f, 12));
                list.Add(MakeBoulder(V(0.16f, 0.05f), 0.13f, 0.1f, 13));
                break;
            case 1: // a cluster of three
                list.Add(MakeBoulder(V(0.33f, 0.24f), 0.3f, 0.27f, 21));
                list.Add(MakeBoulder(V(0.67f, 0.22f), 0.28f, 0.25f, 22));
                list.Add(MakeBoulder(V(0.5f, 0.05f), 0.32f, 0.28f, 23));
                break;
            case 2: // a low slab with a rock behind it
                list.Add(MakeBoulder(V(0.68f, 0.22f), 0.26f, 0.28f, 32));
                list.Add(MakeBoulder(V(0.42f, 0.06f), 0.6f, 0.22f, 31));
                list.Add(MakeBoulder(V(0.88f, 0.04f), 0.11f, 0.09f, 33));
                break;
            default: // rubble
                list.Add(MakeBoulder(V(0.3f, 0.22f), 0.2f, 0.17f, 41));
                list.Add(MakeBoulder(V(0.64f, 0.24f), 0.22f, 0.19f, 42));
                list.Add(MakeBoulder(V(0.47f, 0.06f), 0.22f, 0.18f, 43));
                list.Add(MakeBoulder(V(0.83f, 0.05f), 0.14f, 0.11f, 44));
                list.Add(MakeBoulder(V(0.15f, 0.04f), 0.12f, 0.09f, 45));
                break;
        }
        return list;
    }

    private static void DrawDeposit(Painter p, int variant, Ore ore)
    {
        var full = new Rect(0, 0, 1, 0.75f);
        Color rock = ore == Ore.Iron ? new Color(0.55f, 0.47f, 0.43f)
            : ore == Ore.Gold ? new Color(0.68f, 0.63f, 0.56f)
            : ore == Ore.Copper ? new Color(0.64f, 0.57f, 0.49f)
            : new Color(0.66f, 0.63f, 0.58f);
        var boulders = DepositLayout(variant);

        // Soft ground shadow under the whole group.
        p.Ellipse(V(0.5f, 0.07f), V(0.44f, 0.07f), new Color(0, 0, 0, 0.16f));
        foreach (var b in boulders)
        {
            DrawBoulder(p, b, rock);
        }
        if (ore == Ore.None)
        {
            return;
        }

        // Ore shows only on the boulders' faces.
        System.Func<Vector2, float> onRock = q =>
        {
            float d = float.MaxValue;
            foreach (var b in boulders) d = Mathf.Min(d, SdPolygon(q, b.points));
            return d + 0.025f;
        };
        var rng = new System.Random(variant * 31 + (int)ore * 7);
        Color vein, nugget, accent;
        switch (ore)
        {
            case Ore.Copper: vein = new Color(0.78f, 0.45f, 0.22f); nugget = new Color(0.9f, 0.55f, 0.28f); accent = new Color(0.35f, 0.72f, 0.6f); break;
            case Ore.Iron: vein = new Color(0.62f, 0.26f, 0.16f); nugget = new Color(0.7f, 0.3f, 0.18f); accent = new Color(0.86f, 0.55f, 0.4f); break;
            default: vein = new Color(0.85f, 0.68f, 0.2f); nugget = new Color(0.98f, 0.82f, 0.28f); accent = Color.white; break;
        }
        foreach (var b in boulders)
        {
            if (b.size.x < 0.08f) continue;
            // A thin crooked vein across the face.
            var a = b.center + new Vector2(-b.size.x * 0.7f, b.size.y * (-0.2f + (float)rng.NextDouble() * 0.5f));
            var m = b.center + new Vector2(b.size.x * ((float)rng.NextDouble() * 0.4f - 0.2f), b.size.y * ((float)rng.NextDouble() * 0.6f - 0.4f));
            var e = b.center + new Vector2(b.size.x * 0.7f, b.size.y * (-0.1f + (float)rng.NextDouble() * 0.6f));
            p.Draw(q => Mathf.Max(Mathf.Min(SdSegment(q, a, m), SdSegment(q, m, e)) - 0.008f, onRock(q)), full, vein);
            // Angular chunks of ore scattered over the rock, different sizes, never in a row.
            int count = b.size.x > 0.18f ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                var c = b.center + new Vector2(b.size.x * ((float)rng.NextDouble() * 1.4f - 0.7f), b.size.y * ((float)rng.NextDouble() * 1.4f - 0.6f));
                float r = 0.018f + (float)rng.NextDouble() * (i == 0 ? 0.03f : 0.016f);
                float spin = (float)rng.NextDouble() * Mathf.PI;
                var chunk = new Vector2[5];
                for (int k = 0; k < chunk.Length; k++)
                {
                    float ang = spin + k * Mathf.PI * 2 / chunk.Length;
                    float rr = r * (0.7f + (float)rng.NextDouble() * 0.5f);
                    chunk[k] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f) * rr;
                }
                var facet = new[] { chunk[0], chunk[1], c };
                p.Draw(q => Mathf.Max(SdPolygon(q, chunk) - 0.008f, onRock(q)), full, new Color(0.22f, 0.18f, 0.15f));
                p.Draw(q => Mathf.Max(SdPolygon(q, chunk), onRock(q)), full, nugget);
                p.Draw(q => Mathf.Max(SdPolygon(q, facet), onRock(q)), full, Color.Lerp(nugget, accent, 0.5f));
            }
            if (ore == Ore.Gold)
            {
                // A little sparkle.
                var c = b.center + new Vector2(b.size.x * 0.3f, b.size.y * 0.5f);
                p.Draw(q => Mathf.Min(SdSegment(q, c - V(0.03f, 0), c + V(0.03f, 0)), SdSegment(q, c - V(0, 0.03f), c + V(0, 0.03f))) - 0.005f, full, new Color(1, 1, 0.9f, 0.9f));
            }
        }
    }
}
