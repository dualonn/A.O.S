using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Building art: every building is drawn once per upgrade level (upgrades change the look)
/// and, except for huts, as a short looping animation. Drawn in grid cells (1 unit = 1 cell);
/// t runs 0..1 across the loop.
/// </summary>
public static partial class PlaceholderArt
{
    private delegate void BuildingDrawer(Painter p, int level, float t);

    private static readonly Dictionary<string, (BuildingDrawer draw, int frames, float fps)> BuildingArt =
        new Dictionary<string, (BuildingDrawer, int, float)>
        {
            { "Campfire", (DrawCampfire, 4, 7f) },
            { "Hut", (DrawHut, 1, 1f) },
            { "House", (DrawHouse, 4, 4f) },
            { "ForagerCamp", (DrawForagerCamp, 4, 4f) },
            { "LumberCamp", (DrawLumberCamp, 4, 6f) },
            { "Quarry", (DrawQuarry, 4, 6f) },
            { "Barracks", (DrawBarracks, 4, 5f) },
            { "BronzeFoundry", (DrawFoundry, 4, 7f) },
            { "Farm", (DrawFarm, 4, 3f) },
            { "WarriorLodge", (DrawWarriorLodge, 4, 5f) },
            { "Watchtower", (DrawWatchtower, 4, 4f) },
            { "AxeHall", (DrawAxeHall, 4, 5f) },
            { "ChariotYard", (DrawChariotYard, 4, 4f) },
            { "SlingerCamp", (DrawSlingerCamp, 4, 4f) },
            { "SkirmisherCamp", (DrawSkirmisherCamp, 4, 3f) },
            { "ElephantPen", (DrawElephantPen, 4, 3f) },
            // Iron Age
            { "IronMine", (DrawIronMine, 4, 4f) },
            { "StoneHouse", (DrawStoneHouse, 4, 4f) },
            { "Garrison", (DrawGarrison, 4, 6f) },
            { "ArcheryRange", (DrawArcheryRange, 4, 3f) },
            { "StoneTower", (DrawStoneTower, 4, 6f) },
            // Classical Age
            { "GoldMine", (DrawGoldMine, 4, 4f) },
            { "Insula", (DrawInsula, 4, 4f) },
            { "LegionFort", (DrawLegionFort, 4, 4f) },
            { "ArcherAcademy", (DrawArcherAcademy, 4, 3f) },
            { "Stables", (DrawStables, 4, 3f) },
            { "BallistaTower", (DrawBallistaTower, 4, 4f) },
        };

    private static readonly Color Bronze2 = new Color(0.86f, 0.6f, 0.28f);
    private static readonly Color Smoke = new Color(0.55f, 0.55f, 0.55f, 0.5f);
    private static readonly Color Wheat = new Color(0.9f, 0.78f, 0.35f);
    private static readonly Color WheatDark = new Color(0.72f, 0.58f, 0.22f);
    private static readonly Color Field = new Color(0.55f, 0.42f, 0.26f);
    private static readonly Color WaterBlue = new Color(0.3f, 0.55f, 0.85f);

    /// <summary>Draw every level and frame of a building and store them as its looks.</summary>
    [MenuItem("Tools/Age of Stickman/Generate Building Art")]
    public static void GenerateBuildingsOnly()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        foreach (var def in database.buildings)
        {
            if (def != null)
            {
                GenerateBuildingLooks(def);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Building art generated.");
    }

    /// <summary>Painted bounds of a building over every level and frame, in tiles from its footprint corner.</summary>
    public static Rect? MeasureBuilding(BuildingDefinition def)
    {
        if (!BuildingArt.TryGetValue(def.id, out var art))
        {
            return null;
        }
        Rect? all = null;
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            for (int f = 0; f < art.frames; f++)
            {
                var p = new Painter(def.ArtSize.x + 4, def.ArtSize.y + 5, 32f) { Origin = new Vector2(2, 2) };
                art.draw(p, level, art.frames > 1 ? f / (float)art.frames : 0f);
                var b = p.PaintedBounds();
                if (b == null) continue;
                var r = b.Value;
                all = all == null ? r : Rect.MinMaxRect(Mathf.Min(all.Value.xMin, r.xMin), Mathf.Min(all.Value.yMin, r.yMin),
                    Mathf.Max(all.Value.xMax, r.xMax), Mathf.Max(all.Value.yMax, r.yMax));
            }
        }
        return all;
    }

    private static void GenerateBuildingLooks(BuildingDefinition def)
    {
        if (!BuildingArt.TryGetValue(def.id, out var art))
        {
            return;
        }
        string dir = $"Buildings/{def.id}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        def.looks = new List<BuildingLook>();
        int levels = def.upgrades.Count + 1;
        // Canvas: the art area grown to fit everything any frame paints (smoke, flags, swinging
        // tools), so animations never get cut off. The pivot sits on the footprint's corner.
        var artBox = new Rect(Vector2.zero, def.ArtSize);
        var painted = MeasureBuilding(def) ?? artBox;
        var canvas = Rect.MinMaxRect(Mathf.Min(artBox.xMin, painted.xMin) - 0.06f, Mathf.Min(artBox.yMin, painted.yMin) - 0.06f,
            Mathf.Max(artBox.xMax, painted.xMax) + 0.06f, Mathf.Max(artBox.yMax, painted.yMax) + 0.06f);
        var pivot = new Vector2(-canvas.xMin / canvas.width, -canvas.yMin / canvas.height);
        for (int level = 0; level < levels; level++)
        {
            var look = new BuildingLook { fps = art.fps };
            for (int f = 0; f < art.frames; f++)
            {
                var p = new Painter(canvas.width, canvas.height, BuildingPPU) { Origin = -canvas.min };
                art.draw(p, level, art.frames > 1 ? f / (float)art.frames : 0f);
                look.frames.Add(Save(p, $"{dir}/{def.id}_L{level}_F{f}.png", BuildingPPU, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        EditorUtility.SetDirty(def);
    }

    // ---------- Shared bits ----------

    private static float Wave(float t, float phase = 0f) => Mathf.Sin((t + phase) * Mathf.PI * 2f);

    /// <summary>A puff of smoke rising and fading over the loop.</summary>
    private static void SmokePuffs(Painter p, Vector2 from, float t, float height)
    {
        for (int i = 0; i < 2; i++)
        {
            float k = (t + i * 0.5f) % 1f;
            var c = Smoke;
            c.a *= 1f - k;
            p.Circle(from + V(0.08f * Mathf.Sin(k * 6f), k * height), 0.07f + k * 0.08f, c);
        }
    }

    private static void FlameShape(Painter p, Vector2 baseCentre, float width, float height, float t)
    {
        float h = height * (0.85f + 0.15f * Wave(t));
        float lean = 0.05f * Wave(t, 0.25f);
        var outer = new[]
        {
            baseCentre + V(-width, 0), baseCentre + V(width, 0), baseCentre + V(width * 0.6f + lean, h * 0.6f),
            baseCentre + V(width * 0.2f + lean, h * 0.45f), baseCentre + V(lean * 2, h), baseCentre + V(-width * 0.3f + lean, h * 0.55f),
            baseCentre + V(-width * 0.6f, h * 0.7f),
        };
        p.OutlinedPolygon(outer, Fire);
        p.Polygon(new[] { baseCentre + V(-width * 0.5f, 0.02f), baseCentre + V(width * 0.5f, 0.02f), baseCentre + V(lean, h * 0.55f) }, FireCore);
    }

    private static void WavingFlag(Painter p, Vector2 poleBase, float poleHeight, float t, Color color)
    {
        p.OutlinedLine(poleBase, poleBase + V(0, poleHeight), 0.045f, WoodDark);
        Vector2 top = poleBase + V(0.02f, poleHeight - 0.03f);
        float w = Wave(t) * 0.05f;
        p.OutlinedPolygon(new[] { top, top + V(0.2f, 0.03f + w), top + V(0.38f, -0.08f - w), top + V(0, -0.26f) }, color);
    }

    private static void OutlinedBlob(Painter p, Color fill, params (Vector2 c, float r)[] blobs)
    {
        foreach (var (c, r) in blobs) p.Circle(c, r + p.Outline, p.Ink);
        foreach (var (c, r) in blobs) p.Circle(c, r, fill);
    }

    /// <summary>A tool (axe or pickaxe) swinging around a pivot: angle in degrees from horizontal.</summary>
    private static Vector2 ToolTip(Vector2 pivot, float length, float degrees) =>
        pivot + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * length;

    // ---------- Stone Age ----------

    private static void DrawCampfire(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.5f), V(0.85f, 0.25f), Shadow);
        if (level >= 2)
        {
            // Gathering Circle: a ring of seat logs and a carved totem
            p.OutlinedBox(0.1f, 0.85f, 0.5f, 0.98f, 0.06f, Wood);
            p.OutlinedBox(1.5f, 0.85f, 1.9f, 0.98f, 0.06f, Wood);
            p.OutlinedBox(0.18f, 0.12f, 0.24f, 1.55f, 0.02f, WoodDark);
            p.OutlinedCircle(V(0.21f, 1.5f), 0.1f, Wood);
            p.OutlinedCircle(V(0.21f, 1.25f), 0.09f, WoodLight);
            p.Circle(V(0.18f, 1.52f), 0.02f, p.Ink);
            p.Circle(V(0.24f, 1.52f), 0.02f, p.Ink);
        }
        p.OutlinedBox(0.15f, 0.25f, 0.65f, 0.42f, 0.08f, Wood);
        p.OutlinedBox(1.35f, 0.25f, 1.85f, 0.42f, 0.08f, Wood);
        float ring = level >= 1 ? 0.52f : 0.42f;
        int stones = level >= 1 ? 12 : 9;
        for (int i = 0; i < stones; i++)
        {
            float a = i / (float)stones * Mathf.PI * 2;
            p.OutlinedCircle(V(1 + Mathf.Cos(a) * ring, 0.62f + Mathf.Sin(a) * ring * 0.45f), 0.09f, i % 2 == 0 ? Stone : StoneLight);
        }
        p.OutlinedLine(V(0.7f, 0.55f), V(1.3f, 0.75f), 0.08f, WoodDark);
        p.OutlinedLine(V(1.3f, 0.55f), V(0.7f, 0.75f), 0.08f, WoodDark);
        FlameShape(p, V(1f, 0.66f), level >= 1 ? 0.26f : 0.2f, level >= 1 ? 0.62f : 0.48f, t);
        SmokePuffs(p, V(1f, 1.25f), t, 0.55f);
        WavingFlag(p, V(1.72f, 0.55f), 1.3f, t, Blue);
    }

    private static void DrawHut(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.32f), V(0.85f, 0.18f), Shadow);
        p.OutlinedBox(0.45f, 0.3f, 1.55f, 1.05f, 0.03f, Mud);
        if (level >= 2)
        {
            // Wattle Walls: woven hurdles and a little window
            for (float y = 0.42f; y < 1.0f; y += 0.12f)
            {
                for (float x = 0.5f; x < 1.5f; x += 0.16f)
                {
                    p.Line(V(x, y), V(x + 0.08f, y + 0.04f), 0.025f, WoodDark);
                    p.Line(V(x + 0.08f, y + 0.04f), V(x + 0.16f, y), 0.025f, WoodLight);
                }
            }
            p.OutlinedBox(1.25f, 0.62f, 1.45f, 0.82f, 0.02f, Dark);
            p.InkLine(V(1.35f, 0.62f), V(1.35f, 0.82f), 0.6f);
        }
        else
        {
            for (float x = 0.62f; x < 1.5f; x += 0.2f)
            {
                p.InkLine(V(x, 0.33f), V(x, 1.0f), 0.5f);
            }
        }
        p.OutlinedBox(0.84f, 0.3f, 1.16f, 0.78f, 0.12f, Dark);
        if (level >= 1)
        {
            // Thatched Roof: a deeper, layered roof with an overhang
            p.OutlinedPolygon(new[] { V(0.12f, 0.88f), V(1.88f, 0.88f), V(1f, 1.86f) }, StrawDark);
            p.OutlinedPolygon(new[] { V(0.22f, 1.04f), V(1.78f, 1.04f), V(1f, 1.86f) }, Straw);
            p.Line(V(0.36f, 1.2f), V(1.64f, 1.2f), 0.03f, StrawDark);
            p.Line(V(0.52f, 1.38f), V(1.48f, 1.38f), 0.03f, StrawDark);
        }
        else
        {
            p.OutlinedPolygon(new[] { V(0.22f, 0.92f), V(1.78f, 0.92f), V(1f, 1.82f) }, Straw);
            for (int i = 1; i < 6; i++)
            {
                p.Line(V(1f, 1.78f), V(Mathf.Lerp(0.3f, 1.7f, i / 6f), 0.96f), 0.02f, StrawDark);
            }
        }
        p.OutlinedLine(V(0.86f, 1.66f), V(1.14f, 1.98f), 0.05f, WoodDark);
        p.OutlinedLine(V(1.14f, 1.66f), V(0.86f, 1.98f), 0.05f, WoodDark);
    }

    private static void DrawForagerCamp(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.32f), V(0.9f, 0.18f), Shadow);
        if (level >= 2)
        {
            // Drying Racks: a pole rack with strips swaying in the breeze
            p.OutlinedLine(V(0.12f, 0.3f), V(0.12f, 1.4f), 0.04f, WoodDark);
            p.OutlinedLine(V(0.62f, 0.3f), V(0.62f, 1.4f), 0.04f, WoodDark);
            p.OutlinedLine(V(0.08f, 1.36f), V(0.66f, 1.36f), 0.04f, Wood);
            for (int i = 0; i < 4; i++)
            {
                float x = 0.2f + i * 0.13f, sway = Wave(t, i * 0.2f) * 0.03f;
                p.OutlinedLine(V(x, 1.34f), V(x + sway, 1.08f), 0.045f, i % 2 == 0 ? new Color(0.7f, 0.3f, 0.25f) : new Color(0.75f, 0.6f, 0.4f));
            }
        }
        p.OutlinedLine(V(0.38f, 0.3f), V(0.8f, 1.42f), 0.04f, WoodDark);
        p.OutlinedLine(V(1.12f, 0.3f), V(0.62f, 1.42f), 0.04f, WoodDark);
        p.OutlinedPolygon(new[] { V(0.18f, 0.3f), V(1.22f, 0.3f), V(0.7f, 1.25f) }, Hide);
        p.OutlinedPolygon(new[] { V(0.54f, 0.3f), V(0.86f, 0.3f), V(0.7f, 0.78f) }, Dark);
        float sway2 = Wave(t) * 0.025f;
        OutlinedBlob(p, Leaf, (V(1.5f + sway2, 0.66f), 0.36f), (V(1.24f, 0.52f), 0.24f), (V(1.76f + sway2, 0.5f), 0.24f));
        p.Circle(V(1.42f + sway2, 0.78f), 0.12f, LeafLight);
        foreach (var c in new[] { V(1.38f, 0.58f), V(1.58f, 0.72f), V(1.66f, 0.48f), V(1.2f, 0.46f), V(1.52f, 0.86f) })
        {
            p.OutlinedCircle(c + V(sway2, 0), 0.045f, Berry);
        }
        int baskets = level >= 1 ? 3 : 1;
        for (int i = 0; i < baskets; i++)
        {
            float x = 0.95f - i * 0.3f;
            if (level >= 2 && x < 0.7f) x = 1.85f - (i - 1) * 0.0f; // keep clear of the rack
            p.OutlinedBox(x, 0.28f, x + 0.25f, 0.45f, 0.04f, Straw);
            p.OutlinedCircle(V(x + 0.08f, 0.5f), 0.04f, Berry);
            p.OutlinedCircle(V(x + 0.17f, 0.5f), 0.04f, Berry);
        }
    }

    private static void DrawLumberCamp(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.32f), V(0.9f, 0.18f), Shadow);
        if (level >= 2)
        {
            // Log Sledges: a sledge loaded with logs out front
            p.OutlinedLine(V(0.05f, 0.14f), V(0.75f, 0.14f), 0.04f, WoodDark);
            p.OutlinedLine(V(0.75f, 0.14f), V(0.85f, 0.24f), 0.04f, WoodDark);
            p.OutlinedBox(0.1f, 0.18f, 0.7f, 0.3f, 0.05f, Wood);
            p.OutlinedEllipse(V(0.7f, 0.24f), V(0.05f, 0.06f), WoodLight);
        }
        float[] rows = { 0.42f, 0.66f, 0.9f };
        for (int row = 0; row < 3; row++)
        {
            float y = rows[row], x0 = 0.2f + row * 0.13f, length = 1.0f - row * 0.1f;
            p.OutlinedBox(x0, y - 0.11f, x0 + length, y + 0.11f, 0.1f, Wood);
            p.Line(V(x0 + 0.15f, y), V(x0 + length - 0.2f, y), 0.012f, WoodDark);
            p.OutlinedEllipse(V(x0 + length, y), V(0.09f, 0.11f), WoodLight);
            p.Ellipse(V(x0 + length, y), V(0.04f, 0.05f), Wood);
        }
        if (level >= 1)
        {
            // Flint Axes: a spare axe leaning on the pile
            p.OutlinedLine(V(0.3f, 1.0f), V(0.45f, 1.45f), 0.035f, WoodLight);
            p.OutlinedPolygon(new[] { V(0.4f, 1.36f), V(0.56f, 1.4f), V(0.55f, 1.55f), V(0.43f, 1.48f) }, StoneDark);
        }
        // Stump with an axe chopping (up, down, hit, back)
        p.OutlinedBox(1.45f, 0.28f, 1.8f, 0.55f, 0.03f, Wood);
        p.OutlinedEllipse(V(1.625f, 0.55f), V(0.175f, 0.06f), WoodLight);
        float[] angles = { 75f, 40f, -5f, 30f };
        float angle = angles[Mathf.Clamp((int)(t * 4), 0, 3)];
        Vector2 pivot = V(1.92f, 0.62f);
        Vector2 head = ToolTip(pivot, 0.5f, 180f - angle);
        p.OutlinedLine(pivot, head, 0.045f, WoodLight);
        p.OutlinedCircle(head, 0.08f, level >= 1 ? StoneDark : StoneLight);
        if ((int)(t * 4) == 2)
        {
            p.OutlinedCircle(V(1.4f, 0.75f), 0.035f, WoodLight);
            p.OutlinedCircle(V(1.33f, 0.66f), 0.025f, WoodLight);
        }
    }

    private static void DrawQuarry(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.35f, 0.24f), Shadow);
        var face = new[] { V(0.18f, 0.3f), V(0.35f, 1.55f), V(0.95f, 2.35f), V(1.8f, 2.55f), V(2.55f, 2.05f), V(2.82f, 0.3f) };
        p.OutlinedPolygon(face, Stone);
        p.OutlinedPolygon(new[] { V(0.8f, 0.3f), V(0.95f, 1.5f), V(1.6f, 1.85f), V(2.2f, 1.4f), V(2.3f, 0.3f) }, StoneDark);
        p.Polygon(new[] { V(0.95f, 2.2f), V(1.8f, 2.42f), V(2.3f, 2.1f), V(1.6f, 2.05f) }, StoneLight);
        p.InkLine(V(0.5f, 1.2f), V(0.75f, 1.0f));
        p.InkLine(V(2.45f, 1.6f), V(2.6f, 1.2f));
        if (level >= 1)
        {
            // Bone Wedges: wedges hammered into the seams
            foreach (var w in new[] { V(1.25f, 1.55f), V(1.7f, 1.3f), V(0.6f, 1.6f) })
            {
                p.OutlinedPolygon(new[] { w, w + V(0.1f, 0.02f), w + V(0.03f, -0.16f) }, new Color(0.95f, 0.93f, 0.85f));
            }
        }
        if (level >= 2)
        {
            // Rope Hoists: an A-frame crane lifting a block that bobs
            p.OutlinedLine(V(2.35f, 0.3f), V(2.62f, 2.6f), 0.05f, Wood);
            p.OutlinedLine(V(2.9f, 0.3f), V(2.62f, 2.6f), 0.05f, Wood);
            p.OutlinedLine(V(2.62f, 2.55f), V(1.95f, 2.65f), 0.045f, Wood);
            float y = 1.95f + Wave(t) * 0.12f;
            p.Line(V(1.98f, 2.62f), V(1.98f, y + 0.2f), 0.015f, WoodDark);
            p.OutlinedBox(1.85f, y, 2.1f, y + 0.2f, 0.02f, StoneLight);
        }
        p.OutlinedBox(0.95f, 0.3f, 1.4f, 0.62f, 0.02f, StoneLight);
        p.OutlinedBox(1.45f, 0.3f, 1.9f, 0.62f, 0.02f, StoneLight);
        p.OutlinedBox(1.2f, 0.67f, 1.65f, 0.99f, 0.02f, StoneLight);
        // Pickaxe striking the rock face
        float[] angles = { 80f, 55f, 15f, 45f };
        int f = Mathf.Clamp((int)(t * 4), 0, 3);
        Vector2 pivot = V(2.2f, 0.45f);
        Vector2 head = ToolTip(pivot, 0.62f, 180f - angles[f]);
        p.OutlinedLine(pivot, head, 0.05f, WoodLight);
        Vector2 dir = (head - pivot).normalized, side = new Vector2(-dir.y, dir.x);
        p.OutlinedPolygon(new[] { head - side * 0.18f, head + dir * 0.06f, head + side * 0.18f, head + dir * 0.02f }, StoneDark);
        if (f == 2)
        {
            p.Circle(head + V(-0.12f, 0.05f), 0.08f, new Color(0.8f, 0.78f, 0.72f, 0.7f));
            p.Circle(head + V(-0.22f, 0.14f), 0.05f, new Color(0.8f, 0.78f, 0.72f, 0.5f));
        }
    }

    private static void DrawBarracks(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.4f, 0.25f), Shadow);
        if (level >= 2)
        {
            // Bigger Bunks: a lean-to wing on the left
            p.OutlinedBox(0.05f, 1.0f, 0.55f, 1.6f, 0.02f, Mud);
            p.OutlinedPolygon(new[] { V(0.0f, 1.55f), V(0.62f, 1.55f), V(0.62f, 1.95f) }, StrawDark);
        }
        p.OutlinedBox(0.5f, 1.0f, 2.5f, 1.85f, 0.03f, Mud);
        for (float x = 0.7f; x < 2.45f; x += 0.22f)
        {
            p.InkLine(V(x, 1.03f), V(x, 1.8f), 0.5f);
        }
        p.OutlinedPolygon(new[] { V(0.3f, 1.78f), V(2.7f, 1.78f), V(2.3f, 2.5f), V(0.7f, 2.5f) }, StrawDark);
        for (float x = 0.6f; x < 2.5f; x += 0.18f)
        {
            p.Line(V(x, 1.82f), V(x + 0.12f, 2.46f), 0.018f, Straw);
        }
        p.OutlinedLine(V(1.3f, 1.2f), V(1.7f, 1.65f), 0.06f, WoodLight);
        p.OutlinedLine(V(1.7f, 1.2f), V(1.3f, 1.65f), 0.06f, WoodLight);
        p.OutlinedCircle(V(1.7f, 1.65f), 0.07f, Wood);
        p.OutlinedCircle(V(1.3f, 1.65f), 0.07f, Wood);
        WavingFlag(p, V(2.55f, 1.9f), 0.98f, t, Flag);
        var rng = new System.Random(7);
        for (float x = 0.22f; x < 2.85f; x += 0.21f)
        {
            float top = 1.08f + (float)rng.NextDouble() * 0.12f;
            if (x > 1.25f && x < 1.75f) continue;
            p.OutlinedPolygon(new[] { V(x - 0.09f, 0.3f), V(x + 0.09f, 0.3f), V(x + 0.09f, top), V(x, top + 0.14f), V(x - 0.09f, top) }, Wood);
        }
        p.OutlinedLine(V(0.15f, 0.8f), V(1.25f, 0.8f), 0.05f, WoodDark);
        p.OutlinedLine(V(1.75f, 0.8f), V(2.88f, 0.8f), 0.05f, WoodDark);
        if (level >= 1)
        {
            // Running Drills: a straw training dummy by the gate (wobbles when hit)
            float wob = Wave(t) * 0.04f;
            p.OutlinedLine(V(2.2f, 0.12f), V(2.2f + wob, 0.75f), 0.04f, WoodDark);
            p.OutlinedEllipse(V(2.2f + wob, 0.6f), V(0.1f, 0.16f), Straw);
            p.OutlinedCircle(V(2.2f + wob, 0.82f), 0.07f, Straw);
        }
        if (level >= 3)
        {
            // Hardened Clubs: a weapon rack beside the gate
            p.OutlinedLine(V(0.55f, 0.12f), V(1.05f, 0.12f), 0.04f, WoodDark);
            p.OutlinedLine(V(0.55f, 0.55f), V(1.05f, 0.55f), 0.04f, WoodDark);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.65f + i * 0.15f;
                p.OutlinedLine(V(x, 0.12f), V(x + 0.03f, 0.7f), 0.035f, WoodLight);
                p.OutlinedLine(V(x + 0.02f, 0.5f), V(x + 0.04f, 0.72f), 0.07f, Wood);
            }
        }
    }

    // ---------- Bronze Age ----------

    private static void DrawFoundry(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.35f, 0.24f), Shadow);
        p.OutlinedBox(1.75f, 1.4f, 2.1f, 2.45f, 0.02f, StoneDark);
        SmokePuffs(p, V(1.93f, 2.55f), t, 0.35f);
        p.OutlinedBox(0.35f, 0.3f, 2.05f, 1.85f, 0.55f, Stone);
        var rng = new System.Random(3);
        for (int i = 0; i < 14; i++)
        {
            float x = 0.55f + (float)rng.NextDouble() * 1.3f, y = 0.5f + (float)rng.NextDouble() * 1.15f;
            p.Box(V(x, y), V(0.22f, 0.12f), 0.03f, StoneLight);
            p.InkLine(V(x - 0.11f, y - 0.06f), V(x + 0.11f, y - 0.06f), 0.5f);
        }
        p.OutlinedBox(0.85f, 0.3f, 1.55f, 0.95f, 0.3f, Dark);
        float glow = 0.2f + 0.04f * Wave(t);
        p.Circle(V(1.2f, 0.5f), glow, Fire);
        p.Circle(V(1.2f, 0.47f), glow * 0.55f, FireCore);
        foreach (var (x, y) in new[] { (2.2f, 0.32f), (2.55f, 0.32f), (2.38f, 0.48f) })
        {
            p.OutlinedPolygon(new[] { V(x - 0.15f, y), V(x + 0.15f, y), V(x + 0.1f, y + 0.14f), V(x - 0.1f, y + 0.14f) }, Bronze);
            p.Line(V(x - 0.07f, y + 0.1f), V(x + 0.07f, y + 0.1f), 0.02f, BronzeLight);
        }
        if (level >= 1)
        {
            // Clay Crucibles
            foreach (float x in new[] { 0.2f, 0.42f })
            {
                p.OutlinedPolygon(new[] { V(x - 0.09f, 0.3f), V(x + 0.09f, 0.3f), V(x + 0.11f, 0.48f), V(x - 0.11f, 0.48f) }, new Color(0.7f, 0.42f, 0.3f));
                p.Ellipse(V(x, 0.48f), V(0.09f, 0.025f), Fire);
            }
        }
        if (level >= 2)
        {
            // Bellows pumping air into the fire
            float squash = 0.1f + 0.06f * (Wave(t) * 0.5f + 0.5f);
            p.OutlinedPolygon(new[] { V(1.6f, 0.4f), V(2.0f, 0.4f - squash * 0.3f), V(2.0f, 0.4f + squash + 0.12f), V(1.6f, 0.56f) }, Hide);
            p.OutlinedLine(V(1.45f, 0.48f), V(1.62f, 0.48f), 0.03f, WoodDark);
        }
    }

    private static readonly Color MudBrick = new Color(0.78f, 0.58f, 0.38f);
    private static readonly Color MudBrickDark = new Color(0.62f, 0.44f, 0.28f);
    private static readonly Color Plaster = new Color(0.93f, 0.89f, 0.8f);

    /// <summary>Mud-brick house (ranked up from a Hut): flat roof, cooking smoke from the roof vent.</summary>
    private static void DrawHouse(Painter p, int level, float t)
    {
        p.Ellipse(V(1, 0.3f), V(0.9f, 0.17f), Shadow);
        bool plastered = level >= 1;
        float top = 1.08f;
        // Walls
        p.OutlinedBox(0.25f, 0.28f, 1.75f, top, 0.02f, plastered ? Plaster : MudBrick);
        if (!plastered)
        {
            // Brick courses
            for (float y = 0.4f; y < top; y += 0.13f)
            {
                p.InkLine(V(0.28f, y), V(1.72f, y), 0.35f);
                float offset = Mathf.Repeat(y * 7.7f, 1f) > 0.5f ? 0.12f : 0f;
                for (float x = 0.36f + offset; x < 1.72f; x += 0.24f)
                {
                    p.InkLine(V(x, y), V(x, y + 0.13f), 0.3f);
                }
            }
        }
        else
        {
            // Whitewashed Walls: a painted band along the top
            p.Box(V(1f, top - 0.11f), V(1.46f, 0.06f), 0f, MudBrickDark);
        }
        // Door with a lintel and a small high window
        p.OutlinedBox(0.82f, 0.28f, 1.14f, 0.76f, 0.02f, Dark);
        p.OutlinedBox(0.76f, 0.76f, 1.2f, 0.83f, 0.01f, Wood);
        p.OutlinedBox(1.38f, 0.66f, 1.56f, 0.8f, 0.01f, Dark);
        // Flat roof on poles: beam ends poke out under the parapet
        p.OutlinedBox(0.18f, top, 1.82f, top + 0.12f, 0.02f, plastered ? MudBrick : MudBrickDark);
        for (float x = 0.32f; x < 1.8f; x += 0.28f)
        {
            p.OutlinedCircle(V(x, top - 0.03f), 0.035f, WoodLight);
        }
        if (level >= 2)
        {
            // Upper Room: a second storey on the roof with a reed awning that flaps
            p.OutlinedBox(0.32f, top + 0.12f, 1.12f, top + 0.6f, 0.02f, plastered ? Plaster : MudBrick);
            p.OutlinedBox(0.6f, top + 0.12f, 0.84f, top + 0.44f, 0.02f, Dark);
            p.OutlinedBox(0.26f, top + 0.6f, 1.18f, top + 0.68f, 0.02f, MudBrickDark);
            float flap = Wave(t) * 0.025f;
            p.OutlinedLine(V(1.2f, top + 0.12f), V(1.2f, top + 0.5f), 0.025f, WoodDark);
            p.OutlinedLine(V(1.7f, top + 0.12f), V(1.7f, top + 0.5f), 0.025f, WoodDark);
            p.OutlinedPolygon(new[] { V(1.14f, top + 0.5f), V(1.76f, top + 0.5f), V(1.78f, top + 0.42f + flap), V(1.12f, top + 0.42f - flap) }, Straw);
        }
        // Cooking smoke from a roof vent
        Vector2 vent = level >= 2 ? V(1.45f, top + 0.12f) : V(1.4f, top + 0.12f);
        p.OutlinedBox(vent.x - 0.06f, vent.y, vent.x + 0.06f, vent.y + 0.08f, 0.01f, StoneDark);
        SmokePuffs(p, vent + V(0, 0.12f), t, level >= 2 ? 0.25f : 0.5f);
    }

    private static void DrawFarm(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.3f), V(1.45f, 0.2f), Shadow);
        p.OutlinedBox(0.1f, 0.15f, 2.9f, 2.0f, 0.04f, Field);
        if (level >= 1)
        {
            // Irrigation Ditches: a water channel through the field
            p.BoxMinMax(0.1f, 1.02f, 2.9f, 1.12f, 0, WaterBlue);
            p.Line(V(0.3f, 1.07f), V(0.5f, 1.07f), 0.015f, new Color(0.6f, 0.8f, 1f));
            p.Line(V(1.6f, 1.07f), V(1.8f, 1.07f), 0.015f, new Color(0.6f, 0.8f, 1f));
        }
        // Rows of wheat swaying together
        for (float y = 0.3f; y < 1.95f; y += 0.28f)
        {
            if (level >= 1 && y > 0.95f && y < 1.2f) continue;
            for (float x = 0.25f; x < 2.8f; x += 0.15f)
            {
                float sway = Wave(t, x * 0.3f) * 0.04f;
                p.Line(V(x, y), V(x + sway, y + 0.2f), 0.02f, WheatDark);
                p.Ellipse(V(x + sway, y + 0.22f), V(0.025f, 0.05f), Wheat);
            }
        }
        // Little shed
        p.OutlinedBox(2.2f, 2.0f, 2.85f, 2.5f, 0.02f, Mud);
        p.OutlinedPolygon(new[] { V(2.1f, 2.45f), V(2.95f, 2.45f), V(2.52f, 2.85f) }, Straw);
        if (level >= 2)
        {
            // Scarecrow & Plough
            p.OutlinedLine(V(0.6f, 1.95f), V(0.6f, 2.75f), 0.04f, WoodDark);
            p.OutlinedLine(V(0.35f, 2.5f), V(0.85f, 2.5f), 0.04f, WoodDark);
            p.OutlinedCircle(V(0.6f, 2.72f), 0.09f, Straw);
            p.OutlinedPolygon(new[] { V(0.48f, 2.8f), V(0.72f, 2.8f), V(0.6f, 2.92f) }, StrawDark);
            p.OutlinedLine(V(1.3f, 2.1f), V(1.8f, 2.3f), 0.04f, WoodLight);
            p.OutlinedPolygon(new[] { V(1.25f, 2.05f), V(1.4f, 2.08f), V(1.32f, 2.2f) }, Bronze);
        }
    }

    private static void DrawWarriorLodge(Painter p, int level, float t)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.4f, 0.25f), Shadow);
        if (level >= 3)
        {
            // Great Hall: a second, taller roof behind
            p.OutlinedPolygon(new[] { V(0.6f, 1.9f), V(2.4f, 1.9f), V(1.5f, 2.75f) }, StrawDark);
        }
        p.OutlinedBox(0.3f, 0.3f, 2.7f, 1.6f, 0.03f, Mud);
        for (float x = 0.5f; x < 2.65f; x += 0.25f)
        {
            p.InkLine(V(x, 0.33f), V(x, 1.55f), 0.5f);
        }
        p.OutlinedPolygon(new[] { V(0.1f, 1.5f), V(2.9f, 1.5f), V(2.5f, 2.3f), V(0.5f, 2.3f) }, Straw);
        p.OutlinedBox(1.25f, 0.3f, 1.75f, 1.0f, 0.2f, Dark);
        // Tall hide shields on the wall (Bronze Age style)
        foreach (float x in new[] { 0.7f, 2.3f })
        {
            p.OutlinedPolygon(new[] { V(x - 0.13f, 0.72f), V(x + 0.13f, 0.72f), V(x + 0.13f, 1.22f), V(x, 1.34f), V(x - 0.13f, 1.22f) }, Hide);
            p.Circle(V(x - 0.04f, 1.12f), 0.03f, WoodDark);
            p.Circle(V(x + 0.05f, 0.92f), 0.035f, WoodDark);
        }
        WavingFlag(p, V(2.75f, 1.6f), 1.25f, t, Flag);
        if (level >= 1)
        {
            // Shield Wall Drills: hide shields stacked against the wall
            p.OutlinedPolygon(new[] { V(0.1f, 0.25f), V(0.32f, 0.25f), V(0.36f, 0.75f), V(0.14f, 0.75f) }, Hide);
            p.OutlinedPolygon(new[] { V(0.22f, 0.25f), V(0.44f, 0.25f), V(0.46f, 0.72f), V(0.24f, 0.72f) }, Hide);
            p.Circle(V(0.35f, 0.5f), 0.03f, WoodDark);
        }
        if (level >= 2)
        {
            // Bronze Spearheads: a spear rack
            for (int i = 0; i < 3; i++)
            {
                float x = 2.0f + i * 0.13f;
                p.OutlinedLine(V(x, 0.2f), V(x + 0.05f, 1.05f), 0.03f, WoodLight);
                p.OutlinedPolygon(new[] { V(x + 0.02f, 1.02f), V(x + 0.09f, 1.03f), V(x + 0.06f, 1.18f) }, Bronze2);
            }
        }
    }

    private static void DrawWatchtower(Painter p, int level, float t)
    {
        // No roof: the live archer (added by the game) stands on the open platform, behind the parapet.
        p.Ellipse(V(1, 0.25f), V(0.6f, 0.15f), Shadow);
        // Taller Tower (level 1+) raises the deck.
        float deck = level >= 1 ? 1.35f : 1.15f;
        // Legs and bracing
        p.OutlinedLine(V(0.45f, 0.15f), V(0.6f, deck), 0.07f, Wood);
        p.OutlinedLine(V(1.55f, 0.15f), V(1.4f, deck), 0.07f, Wood);
        p.OutlinedLine(V(0.5f, 0.4f), V(1.47f, deck - 0.1f), 0.04f, WoodDark);
        p.OutlinedLine(V(1.5f, 0.4f), V(0.53f, deck - 0.1f), 0.04f, WoodDark);
        if (level >= 1)
        {
            // Taller Tower: a second cross-brace and a ladder up the side
            p.OutlinedLine(V(0.47f, 0.28f), V(1.53f, 0.28f), 0.035f, WoodDark);
            p.OutlinedLine(V(1.62f, 0.15f), V(1.55f, deck), 0.025f, WoodLight);
            p.OutlinedLine(V(1.8f, 0.15f), V(1.73f, deck), 0.025f, WoodLight);
            for (float y = 0.3f; y < deck; y += 0.17f)
            {
                float k = (y - 0.15f) / (deck - 0.15f) * 0.07f;
                p.InkLine(V(1.62f - k, y), V(1.8f - k, y), 0.6f);
            }
            // and a pennant on the ladder-side corner, clear of the archer
            WavingFlag(p, V(1.58f, deck + 0.12f), 0.5f, t, Blue);
        }
        // Platform with a parapet; Bronze Plating clads the parapet in bronze sheets
        bool plated = level >= 3;
        p.OutlinedBox(0.4f, deck, 1.6f, deck + 0.12f, 0.02f, WoodLight);
        p.OutlinedBox(0.4f, deck + 0.12f, 1.6f, deck + 0.3f, 0.02f, plated ? Bronze : Wood);
        if (plated)
        {
            for (float x = 0.55f; x < 1.6f; x += 0.3f)
            {
                p.InkLine(V(x, deck + 0.14f), V(x, deck + 0.28f), 0.5f);
                p.Circle(V(x - 0.09f, deck + 0.21f), 0.018f, BronzeLight);
            }
            // A glint sweeping along the plates
            float gx = Mathf.Lerp(0.5f, 1.5f, Mathf.Repeat(t, 1f));
            p.Line(V(gx, deck + 0.15f), V(gx + 0.06f, deck + 0.27f), 0.025f, new Color(1f, 0.95f, 0.75f, 0.8f));
        }
        else
        {
            for (float x = 0.5f; x < 1.6f; x += 0.2f)
            {
                p.InkLine(V(x, deck + 0.14f), V(x, deck + 0.28f), 0.5f);
            }
        }
        if (level >= 2)
        {
            // Bronze-Tipped Arrows: bundles of arrows leaning on the parapet, tips glinting
            foreach (float x in new[] { 0.6f, 1.36f })
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 a = V(x + i * 0.03f, deck + 0.1f), b = V(x - 0.05f + i * 0.05f, deck + 0.44f);
                    p.OutlinedLine(a, b, 0.018f, WoodLight);
                    float glint = Mathf.Sin((t + i * 0.3f) * Mathf.PI * 2) * 0.5f + 0.5f;
                    Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
                    p.OutlinedPolygon(new[] { b - side * 0.035f, b + dir * 0.08f, b + side * 0.035f }, Color.Lerp(Bronze, BronzeLight, glint));
                }
            }
        }
    }
}
