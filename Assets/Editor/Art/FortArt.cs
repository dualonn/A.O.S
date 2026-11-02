using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Walls and gates for every age: Palisade, Mud-brick, Stone and Fortified. Walls are built from
/// parts per tile (a front piece plus runs back to the north, north-east and north-west), so
/// they join straight and diagonally; each gate has 4 pieces (east-west or north-south, closed or open). Pieces are 1 tile wide, drawn in the game's
/// 3/4 view with the pivot on the tile's bottom-left corner.
/// </summary>
public static partial class PlaceholderArt
{
    private struct WallStyle
    {
        public Color face, top, edge;
        public float height;
        public int texture;      // 0 logs, 1 mud bricks, 2 stone blocks, 3 dressed ashlar
        public bool crenels;
        public bool towers;      // gatehouses get towers either side
    }

    private const float WallFoot = 0.3f;       // front foot of the wall, up from the tile's bottom
    private const float WallDepth = 0.24f;     // how deep the top face looks
    private const float WallHalf = 0.19f;      // half the wall's thickness, side to side
    private const float WallCanvasHeight = 2.7f;

    private static readonly Color AshlarFace = new Color(0.86f, 0.83f, 0.76f);
    private static readonly Color AshlarTop = new Color(0.95f, 0.93f, 0.88f);

    private static WallStyle StyleFor(int age) => age switch
    {
        0 => new WallStyle { face = Wood, top = WoodLight, edge = WoodDark, height = 0.78f, texture = 0 },
        1 => new WallStyle { face = MudBrick, top = new Color(0.86f, 0.7f, 0.5f), edge = MudBrickDark, height = 0.7f, texture = 1 },
        2 => new WallStyle { face = Stone, top = StoneLight, edge = StoneDark, height = 0.9f, texture = 2, crenels = true, towers = true },
        _ => new WallStyle { face = AshlarFace, top = AshlarTop, edge = MarbleShade, height = 1.0f, texture = 3, crenels = true, towers = true },
    };

    // ---------- Drawing ----------

    // Textures repeat on a grid that divides a tile evenly, so neighbouring pieces line up
    // into one continuous wall with no seams.
    private const float StakeStep = 0.125f;
    private const float BlockStep = 0.25f;
    private const float CrenelStep = 0.25f;

    /// <summary>While set, texture lines are trimmed to this shape so they never poke past the wall.</summary>
    private static Vector2[] textureClip;

    private static void Ink(Painter p, Vector2 a, Vector2 b, float scale = 1f)
    {
        float h = p.Outline * scale * 0.5f;
        var shape = textureClip;
        if (shape == null)
        {
            p.Line(a, b, p.Outline * scale, p.Ink);
            return;
        }
        var bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - h, Mathf.Min(a.y, b.y) - h, Mathf.Max(a.x, b.x) + h, Mathf.Max(a.y, b.y) + h);
        p.Draw(q => Mathf.Max(SdSegment(q, a, b) - h, SdPolygon(q, shape)), bounds, p.Ink);
    }

    private static Vector2[] RectShape(float x0, float y0, float x1, float y1) =>
        new[] { V(x0, y0), V(x1, y0), V(x1, y1), V(x0, y1) };

    private static IEnumerable<float> GridSteps(float from, float to, float step, float offset = 0f)
    {
        for (float x = Mathf.Ceil((from - offset) / step) * step + offset; x < to - 0.001f; x += step)
        {
            if (x > from + 0.001f) yield return x;
        }
    }

    /// <summary>
    /// A stretch of wall seen from the front, x0 to x1. Open ends have no edge line, so runs on
    /// neighbouring tiles join up seamlessly; closed ends are the wall's ends.
    /// </summary>
    private static void WallRun(Painter p, WallStyle s, float x0, float x1, float foot, float height,
        bool closedLeft = true, bool closedRight = true)
    {
        float top = foot + height;
        p.BoxMinMax(x0, foot, x1, top, 0, s.face);
        textureClip = RectShape(x0, foot, x1, top);
        WallTexture(p, s, x0, x1, foot, top);
        textureClip = null;
        p.InkLine(V(x0, foot), V(x1, foot));
        if (closedLeft) p.InkLine(V(x0, foot), V(x0, top));
        if (closedRight) p.InkLine(V(x1, foot), V(x1, top));
        if (s.texture == 0)
        {
            // Palisade: sharpened stakes instead of a flat top.
            p.InkLine(V(x0, top), V(x1, top), 0.6f);
            foreach (float x in GridSteps(x0 - StakeStep * 0.5f, x1 + StakeStep * 0.5f, StakeStep, StakeStep * 0.5f))
            {
                if (x - 0.05f < x0 - 0.001f && closedLeft) continue;
                if (x + 0.05f > x1 + 0.001f && closedRight) continue;
                p.OutlinedPolygon(new[] { V(x - 0.06f, top - 0.02f), V(x + 0.06f, top - 0.02f), V(x, top + 0.12f) }, s.top);
            }
            return;
        }
        float walk = top + WallDepth * 0.55f;
        p.BoxMinMax(x0, top, x1, walk, 0, s.top);
        p.InkLine(V(x0, top), V(x1, top), 0.6f);
        p.InkLine(V(x0, walk), V(x1, walk));
        if (closedLeft) p.InkLine(V(x0, top), V(x0, walk));
        if (closedRight) p.InkLine(V(x1, top), V(x1, walk));
        if (s.crenels)
        {
            foreach (float x in GridSteps(x0, x1 - 0.09f, CrenelStep, 0.075f))
            {
                p.OutlinedBox(x, walk, x + 0.1f, walk + 0.11f, 0.01f, s.top);
            }
        }
    }

    private static void WallTexture(Painter p, WallStyle s, float x0, float x1, float y0, float y1)
    {
        switch (s.texture)
        {
            case 0: // logs
                foreach (float x in GridSteps(x0, x1, StakeStep)) Ink(p, V(x, y0 + 0.02f), V(x, y1 - 0.02f), 0.5f);
                p.Line(V(x0, y0 + (y1 - y0) * 0.6f), V(x1, y0 + (y1 - y0) * 0.6f), 0.025f, s.edge);
                break;
            case 1: // mud bricks
            case 2: // stone blocks
            case 3: // dressed ashlar
                float course = s.texture == 1 ? 0.14f : s.texture == 2 ? 0.17f : 0.2f;
                int row = 0;
                for (float y = y0 + course; y < y1 + course - 0.02f; y += course, row++)
                {
                    float rowTop = Mathf.Min(y, y1);
                    if (y < y1 - 0.02f) Ink(p, V(x0, y), V(x1, y), 0.35f);
                    foreach (float x in GridSteps(x0, x1, BlockStep, row % 2 == 0 ? 0f : BlockStep * 0.5f))
                    {
                        Ink(p, V(x, rowTop - course + 0.02f), V(x, rowTop - 0.02f), 0.3f);
                    }
                }
                if (s.texture == 3) p.BoxMinMax(x0, y0, x1, y0 + 0.08f, 0, s.edge); // plinth
                break;
        }
    }

    /// <summary>
    /// The run going back (north) to the next tile: only its top is seen, rising into the tile
    /// behind. Exactly one tile long, so a run of them is one unbroken strip.
    /// </summary>
    private static void WallBack(Painter p, WallStyle s, float gapFrom = -1f, float gapTo = -1f, bool gapOpen = false)
    {
        float x0 = 0.5f - WallHalf, x1 = 0.5f + WallHalf;
        float bottom = WallFoot + s.height + (s.texture == 0 ? 0f : WallDepth * 0.55f), topY = bottom + 1f;
        if (s.texture == 0)
        {
            // Palisade seen end-on: a line of stake tips.
            p.BoxMinMax(x0 + 0.05f, bottom, x1 - 0.05f, topY, 0, s.face);
            p.InkLine(V(x0 + 0.05f, bottom), V(x0 + 0.05f, topY));
            p.InkLine(V(x1 - 0.05f, bottom), V(x1 - 0.05f, topY));
            foreach (float y in GridSteps(bottom - 0.07f, topY + 0.07f, StakeStep, bottom % StakeStep))
            {
                p.OutlinedCircle(V(0.5f, y), 0.06f, s.top);
            }
        }
        else
        {
            p.BoxMinMax(x0, bottom, x1, topY, 0, s.top);
            p.InkLine(V(x0, bottom), V(x0, topY));
            p.InkLine(V(x1, bottom), V(x1, topY));
            p.InkLine(V(0.5f, bottom), V(0.5f, topY), 0.3f);
            if (s.crenels)
            {
                foreach (float y in GridSteps(bottom, topY - 0.09f, CrenelStep, bottom % CrenelStep + 0.06f))
                {
                    p.OutlinedBox(x1 - 0.05f, y, x1 + 0.02f, y + 0.1f, 0.01f, s.top);
                }
            }
        }
        if (gapFrom >= 0)
        {
            // A gate seen edge-on: planks across the gap, or a clear gap when open.
            p.OutlinedBox(x0 - 0.02f, gapFrom, x1 + 0.02f, gapTo, 0.01f, gapOpen ? new Color(0.36f, 0.5f, 0.28f) : Wood);
            if (!gapOpen)
            {
                for (float y = gapFrom + 0.08f; y < gapTo; y += 0.08f) p.InkLine(V(x0, y), V(x1, y), 0.35f);
            }
        }
    }

    /// <summary>
    /// Wall parts, combined per tile by how it joins its neighbours. Front pieces: 0 post (ends
    /// and corners), 1 east end, 2 west end, 3 east-west run, 7 west half and 8 east half (a
    /// straight run that carries on into a diagonal at the tile's middle). Runs: 4 north (rising
    /// back to the tile behind), 5 south-east and 6 south-west (dropping to the tile in front).
    /// Every run meets the next at the same front line, so straight and slanted walls join flush.
    /// </summary>
    public const int WallPartCount = 9;

    private static void DrawWallPart(Painter p, WallStyle s, int part)
    {
        switch (part)
        {
            case 0:
                p.Ellipse(V(0.5f, WallFoot + 0.02f), V(0.26f, 0.07f), Shadow);
                WallRun(p, s, 0.5f - WallHalf, 0.5f + WallHalf, WallFoot, s.height);
                break;
            case 1:
                p.Ellipse(V(0.66f, WallFoot + 0.02f), V(0.36f, 0.07f), Shadow);
                WallRun(p, s, 0.5f - WallHalf, 1f, WallFoot, s.height, true, false);
                break;
            case 2:
                p.Ellipse(V(0.34f, WallFoot + 0.02f), V(0.36f, 0.07f), Shadow);
                WallRun(p, s, 0f, 0.5f + WallHalf, WallFoot, s.height, false, true);
                break;
            case 3:
                p.Ellipse(V(0.5f, WallFoot + 0.02f), V(0.5f, 0.07f), Shadow);
                WallRun(p, s, 0f, 1f, WallFoot, s.height, false, false);
                break;
            case 4:
                WallBack(p, s);
                break;
            case 5:
                DiagonalRun(p, s, 1f);
                break;
            case 6:
                DiagonalRun(p, s, -1f);
                break;
            case 7:
                p.Ellipse(V(0.25f, WallFoot + 0.02f), V(0.28f, 0.07f), Shadow);
                WallRun(p, s, 0f, 0.5f, WallFoot, s.height, false, false);
                break;
            case 8:
                p.Ellipse(V(0.75f, WallFoot + 0.02f), V(0.28f, 0.07f), Shadow);
                WallRun(p, s, 0.5f, 1f, WallFoot, s.height, false, false);
                break;
        }
    }

    /// <summary>
    /// A wall dropping diagonally to the tile in front (dir 1 = south-east, -1 = south-west).
    /// Its front line runs from this tile's middle to that tile's middle, the same line straight
    /// runs use, so they meet flush; ends are open so a slanted line reads as one wall.
    /// </summary>
    private static void DiagonalRun(Painter p, WallStyle s, float dir)
    {
        Vector2 f0 = V(0.5f, WallFoot), f1 = V(0.5f + dir, WallFoot - 1f);
        Vector2 up = V(0, s.height);
        // Ground shadow: a band along the foot, so a slanted line's shadow is unbroken too.
        p.Polygon(new[] { f0 + V(0, 0.07f), f1 + V(0, 0.07f), f1 + V(0, -0.07f), f0 + V(0, -0.07f) }, Shadow);
        var face = new[] { f0, f1, f1 + up, f0 + up };
        p.Polygon(face, s.face);
        textureClip = face;
        const int n = 8;
        if (s.texture == 0)
        {
            for (int i = 1; i < n; i++)
            {
                Vector2 q = Vector2.Lerp(f0, f1, i / (float)n);
                Ink(p, q, q + up, 0.5f);
            }
            p.Line(f0 + up * 0.6f, f1 + up * 0.6f, 0.025f, s.edge);
        }
        else
        {
            float course = s.texture == 1 ? 0.14f : s.texture == 2 ? 0.17f : 0.2f;
            int row = 0;
            for (float y = course; y < s.height + course - 0.02f; y += course, row++)
            {
                float rowTop = Mathf.Min(y, s.height);
                if (y < s.height - 0.02f) Ink(p, f0 + V(0, y), f1 + V(0, y), 0.35f);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 q = Vector2.Lerp(f0, f1, (i + (row % 2 == 0 ? 0f : 0.5f)) / 6f);
                    Ink(p, q + V(0, rowTop - course), q + V(0, rowTop), 0.3f);
                }
            }
            if (s.texture == 3)
            {
                p.Polygon(new[] { f0, f1, f1 + V(0, 0.08f), f0 + V(0, 0.08f) }, s.edge); // plinth
            }
        }
        textureClip = null;
        p.InkLine(f0, f1);
        if (s.texture == 0)
        {
            p.InkLine(f0 + up, f1 + up, 0.6f);
            for (int i = 0; i < n; i++)
            {
                Vector2 q = Vector2.Lerp(f0, f1, (i + 0.5f) / n) + up;
                p.OutlinedPolygon(new[] { q + V(-0.06f, -0.02f), q + V(0.06f, -0.02f), q + V(0, 0.12f) }, s.top);
            }
            return;
        }
        Vector2 lift = V(0, WallDepth * 0.55f);
        p.Polygon(new[] { f0 + up, f1 + up, f1 + up + lift, f0 + up + lift }, s.top);
        p.InkLine(f0 + up, f1 + up, 0.6f);
        p.InkLine(f0 + up + lift, f1 + up + lift);
        if (s.crenels)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 q = Vector2.Lerp(f0, f1, (i + 0.5f) / 4f) + up + lift;
                p.OutlinedBox(q.x - 0.05f, q.y, q.x + 0.05f, q.y + 0.11f, 0.01f, s.top);
            }
        }
    }

    private static void DrawGatePiece(Painter p, WallStyle s, bool eastWest, bool open)
    {
        p.Ellipse(V(0.5f, WallFoot + 0.02f), V(0.45f, 0.08f), Shadow);
        float postW = s.towers ? 0.24f : 0.16f;
        float postH = s.height + (s.towers ? 0.45f : 0.22f);
        if (!eastWest)
        {
            // North-south: the wall runs back, the gate is in that run.
            WallBack(p, s, 1f + WallFoot - 0.05f, 1f + WallFoot + s.height * 0.7f, open);
            WallRun(p, s, 0.5f - WallHalf, 0.5f + WallHalf, WallFoot, s.height);
            p.OutlinedBox(0.5f - WallHalf - 0.04f, WallFoot, 0.5f + WallHalf + 0.04f, WallFoot + postH * 0.8f, 0.01f, s.face);
            WallTexture(p, s, 0.5f - WallHalf - 0.04f, 0.5f + WallHalf + 0.04f, WallFoot, WallFoot + postH * 0.8f);
            return;
        }
        // East-west: posts (or towers) either side, the doors between.
        WallRun(p, s, 0f, postW, WallFoot, s.height);
        WallRun(p, s, 1f - postW, 1f, WallFoot, s.height);
        float d0 = postW, d1 = 1f - postW, top = WallFoot + s.height * 0.92f;
        // Lintel / walkway over the gate
        p.OutlinedBox(d0 - 0.02f, top, d1 + 0.02f, top + 0.12f, 0.01f, s.texture == 0 ? Wood : s.top);
        if (open)
        {
            p.OutlinedBox(d0, WallFoot, d1, top, 0.01f, Dark);
            p.Box(V(0.5f, WallFoot + 0.06f), V(d1 - d0 - 0.04f, 0.1f), 0, new Color(0.45f, 0.38f, 0.28f));
            // Doors folded back against the posts
            p.OutlinedBox(d0, WallFoot, d0 + 0.07f, top - 0.02f, 0.01f, Wood);
            p.OutlinedBox(d1 - 0.07f, WallFoot, d1, top - 0.02f, 0.01f, Wood);
        }
        else
        {
            p.OutlinedBox(d0, WallFoot, d1, top, 0.01f, Wood);
            for (float x = d0 + 0.09f; x < d1 - 0.02f; x += 0.09f) p.InkLine(V(x, WallFoot + 0.03f), V(x, top - 0.03f), 0.4f);
            p.Line(V(d0 + 0.02f, WallFoot + 0.15f), V(d1 - 0.02f, WallFoot + 0.15f), 0.03f, s.texture >= 2 ? Iron : WoodDark);
            p.Line(V(d0 + 0.02f, top - 0.13f), V(d1 - 0.02f, top - 0.13f), 0.03f, s.texture >= 2 ? Iron : WoodDark);
        }
        // Posts or towers, in front of the doors
        foreach (float x in new[] { 0f, 1f - postW })
        {
            p.OutlinedBox(x, WallFoot, x + postW, WallFoot + postH, 0.01f, s.face);
            WallTexture(p, s, x, x + postW, WallFoot, WallFoot + postH);
            if (s.texture == 0)
            {
                p.OutlinedPolygon(new[] { V(x, WallFoot + postH), V(x + postW, WallFoot + postH), V(x + postW * 0.5f, WallFoot + postH + 0.16f) }, s.top);
            }
            else
            {
                p.OutlinedBox(x - 0.02f, WallFoot + postH, x + postW + 0.02f, WallFoot + postH + 0.1f, 0.01f, s.top);
                if (s.crenels)
                {
                    p.OutlinedBox(x, WallFoot + postH + 0.1f, x + 0.08f, WallFoot + postH + 0.2f, 0.01f, s.top);
                    p.OutlinedBox(x + postW - 0.08f, WallFoot + postH + 0.1f, x + postW, WallFoot + postH + 0.2f, 0.01f, s.top);
                }
            }
        }
    }

    private static List<Sprite> WallPieces(string id, WallStyle style)
    {
        string dir = $"Buildings/{id}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        foreach (var old in Directory.GetFiles(Path.Combine(Root, dir), "*.png"))
        {
            AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
        }
        var list = new List<Sprite>();
        // Canvas reaches a tile either side for the diagonal runs; pivot stays on the tile's corner.
        for (int part = 0; part < WallPartCount; part++)
        {
            var p = new Painter(3f, WallCanvasHeight + 1f, BuildingPPU) { Origin = V(1f, 1f) };
            DrawWallPart(p, style, part);
            list.Add(Save(p, $"{dir}/{id}_part{part}.png", BuildingPPU, new Vector2(1f / 3f, 1f / (WallCanvasHeight + 1f))));
        }
        return list;
    }

    private static List<Sprite> GatePieces(string id, WallStyle style)
    {
        string dir = $"Buildings/{id}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        var list = new List<Sprite>();
        int i = 0;
        foreach (bool eastWest in new[] { true, false })
        {
            foreach (bool open in new[] { false, true })
            {
                var p = new Painter(1f, WallCanvasHeight, BuildingPPU);
                DrawGatePiece(p, style, eastWest, open);
                list.Add(Save(p, $"{dir}/{id}_{i++}.png", BuildingPPU, Vector2.zero));
            }
        }
        return list;
    }

    // ---------- Data ----------

    [MenuItem("Tools/Age of Stickman/Setup Walls")]
    public static void SetupWalls()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        ResourceType wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron");
        var masonry = db.technologies.Find(t => t != null && t.id == "Masonry");
        var fortification = db.technologies.Find(t => t != null && t.id == "Fortification");
        var engineering = db.technologies.Find(t => t != null && t.id == "Engineering");

        var woodworking = Tech(db, "Woodworking", "Woodworking", db.ages[0],
            "Split and sharpen logs: +5% building health. Unlocks the Palisade and its gate.",
            30f, Cost(wood, 60, Res(db, "Food"), 30), "StoneTools");
        woodworking.buildingHealthMultiplier = 1.05f;
        EditorUtility.SetDirty(woodworking);

        // id, name, age, kind, health, build seconds, defence, cost, tech, art style
        var tiers = new[]
        {
            (wall: "Palisade", wallName: "Palisade", gate: "PalisadeGate", gateName: "Palisade Gate", tech: woodworking,
                wallCost: Cost(wood, 6), gateCost: Cost(wood, 20), hp: 150, def: 0.04f),
            (wall: "MudBrickWall", wallName: "Mud-brick Wall", gate: "MudBrickGate", gateName: "Mud-brick Gate", tech: masonry,
                wallCost: Cost(wood, 2, stone, 6), gateCost: Cost(wood, 10, stone, 16), hp: 320, def: 0.06f),
            (wall: "StoneWall", wallName: "Stone Wall", gate: "StoneGatehouse", gateName: "Stone Gatehouse", tech: fortification,
                wallCost: Cost(stone, 12), gateCost: Cost(stone, 30, wood, 10), hp: 650, def: 0.1f),
            (wall: "FortifiedWall", wallName: "Fortified Wall", gate: "FortifiedGatehouse", gateName: "Fortified Gatehouse", tech: engineering,
                wallCost: Cost(stone, 18), gateCost: Cost(stone, 40, iron, 6), hp: 1100, def: 0.15f),
        };

        BuildingDefinition prevWall = null, prevGate = null;
        for (int age = 0; age < tiers.Length; age++)
        {
            var t = tiers[age];
            var style = StyleFor(age);
            string after = age == 0 ? "Barracks" : prevGate.id;

            var wall = Building(db, t.wall, t.wallName, db.ages[age], after, Vector2Int.one, Vector2Int.one,
                "Drag out a line to build it. Blocks everyone; raiders have to smash through it.",
                t.wallCost, 4f + age, t.hp, 0, t.tech);
            wall.wallKind = WallTile.Wall;
            wall.canBuildOnHills = true;
            wall.defensePower = t.def;
            wall.production = new List<ResourceRate>();
            wall.upgrades = new List<BuildingUpgrade>();
            wall.pieces = WallPieces(t.wall, age);
            wall.icon = wall.pieces[3];
            wall.looks = new List<BuildingLook>();
            EditorUtility.SetDirty(wall);

            var gate = Building(db, t.gate, t.gateName, db.ages[age], t.wall, Vector2Int.one, Vector2Int.one,
                "Put it in a wall: your people and troops walk through, raiders have to break it down.",
                t.gateCost, 8f + age * 2, Mathf.RoundToInt(t.hp * 1.4f), 0, t.tech);
            gate.wallKind = WallTile.Gate;
            gate.canBuildOnHills = true;
            gate.defensePower = t.def * 1.5f;
            gate.production = new List<ResourceRate>();
            gate.upgrades = new List<BuildingUpgrade>();
            gate.pieces = GatePieces(t.gate, age);
            gate.icon = gate.pieces[0];
            gate.looks = new List<BuildingLook>();
            EditorUtility.SetDirty(gate);

            // Each tier ranks up from the one before: select a whole wall and rank it up at once.
            if (prevWall != null)
            {
                RankUp(prevWall, wall, t.wallCost, t.tech);
                RankUp(prevGate, gate, t.gateCost, t.tech);
            }
            prevWall = wall;
            prevGate = gate;
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("Walls and gates set up for every age.");
    }

    private static void RankUp(BuildingDefinition from, BuildingDefinition to, List<ResourceAmount> newCost, Technology tech)
    {
        from.rankUpTo = to;
        from.rankUpTime = 6f;
        // Upgrading in place costs most of a new piece.
        var cost = newCost.ConvertAll(c => new ResourceAmount { resource = c.resource, amount = Mathf.Max(1, Mathf.RoundToInt(c.amount * 0.8f)) });
        from.rankUpRequirements = new Requirements
        {
            cost = cost,
            technologies = tech != null ? new List<Technology> { tech } : new List<Technology>(),
        };
        EditorUtility.SetDirty(from);
    }
}
