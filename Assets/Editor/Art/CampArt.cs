using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// The raider camp seen on the map: a trampled clearing with two dark hide tents, a fire, a
/// stack of loot and a ragged black-and-red banner. 3x3 tiles, animated (fire and banner).
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Color RaiderHide = new Color(0.42f, 0.32f, 0.25f);
    private static readonly Color RaiderHideDark = new Color(0.3f, 0.22f, 0.17f);
    private static readonly Color RaiderRed = new Color(0.62f, 0.12f, 0.1f);
    private static readonly Color Dirt = new Color(0.52f, 0.43f, 0.3f, 0.85f);

    private const int CampFrames = 4;

    [MenuItem("Tools/Age of Stickman/Generate Camp Art")]
    public static void GenerateCampArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        Directory.CreateDirectory(Path.Combine(Root, "World/Camp"));
        art.campFrames = new List<Sprite>();
        var canvas = Rect.MinMaxRect(-0.3f, -0.3f, 3.3f, 3.6f);
        var pivot = new Vector2(-canvas.xMin / canvas.width, -canvas.yMin / canvas.height);
        for (int f = 0; f < CampFrames; f++)
        {
            var p = new Painter(canvas.width, canvas.height, BuildingPPU) { Origin = -canvas.min };
            DrawCamp(p, f / (float)CampFrames);
            art.campFrames.Add(Save(p, $"World/Camp/Camp_F{f}.png", BuildingPPU, pivot));
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Camp art generated.");
    }

    private static void DrawCamp(Painter p, float t)
    {
        // Trampled clearing
        p.Ellipse(V(1.5f, 1.15f), V(1.55f, 0.95f), Dirt);
        p.Ellipse(V(1.5f, 1.1f), V(1.2f, 0.7f), new Color(0.47f, 0.38f, 0.26f, 0.7f));

        // Banner on a tall pole at the back right
        p.OutlinedLine(V(2.55f, 1.6f), V(2.55f, 3.05f), 0.05f, WoodDark);
        float w = Wave(t) * 0.06f;
        Vector2 top = V(2.57f, 3.0f);
        p.OutlinedPolygon(new[] { top, top + V(0.42f, -0.02f + w), top + V(0.3f, -0.2f), top + V(0.44f, -0.42f - w), top + V(0, -0.46f) }, RaiderRed);
        p.Polygon(new[] { top + V(0.06f, -0.12f), top + V(0.22f, -0.13f), top + V(0.18f, -0.3f), top + V(0.06f, -0.3f) }, new Color(0.12f, 0.1f, 0.1f));
        p.OutlinedCircle(V(2.55f, 3.08f), 0.06f, new Color(0.9f, 0.88f, 0.8f)); // skull cap on the pole

        // Back tent (left)
        Tent(p, V(0.75f, 1.55f), 0.7f, 0.85f);
        // Front tent (right)
        Tent(p, V(2.2f, 0.95f), 0.62f, 0.75f);

        // Loot pile: a sack, a chest and some spears leaning together
        p.OutlinedLine(V(0.3f, 0.45f), V(0.55f, 1.15f), 0.035f, WoodLight);
        p.OutlinedLine(V(0.75f, 0.45f), V(0.55f, 1.15f), 0.035f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.5f, 1.1f), V(0.6f, 1.1f), V(0.55f, 1.25f) }, StoneDark);
        p.OutlinedBox(0.15f, 0.25f, 0.55f, 0.5f, 0.03f, Wood);
        p.Line(V(0.15f, 0.42f), V(0.55f, 0.42f), 0.03f, Bronze);
        p.OutlinedEllipse(V(0.82f, 0.36f), V(0.17f, 0.14f), Hide);

        // Fire in the middle: a stone ring and a flickering flame
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            p.OutlinedCircle(V(1.5f, 0.95f) + new Vector2(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.15f), 0.07f, Stone);
        }
        p.OutlinedLine(V(1.32f, 0.92f), V(1.68f, 1.0f), 0.05f, WoodDark);
        FlameShape(p, V(1.5f, 0.95f), 0.14f, 0.42f, t);
        SmokePuffs(p, V(1.5f, 1.45f), t, 0.5f);
    }

    private static void Tent(Painter p, Vector2 baseCentre, float halfWidth, float height)
    {
        var b = baseCentre;
        p.Ellipse(b + V(0, 0.02f), V(halfWidth * 1.05f, 0.12f), Shadow);
        p.OutlinedPolygon(new[] { b + V(-halfWidth, 0), b + V(halfWidth, 0), b + V(0.06f, height) }, RaiderHide);
        p.Polygon(new[] { b + V(0.03f, 0), b + V(halfWidth, 0), b + V(0.06f, height) }, RaiderHideDark);
        // Doorway flap
        p.OutlinedPolygon(new[] { b + V(-0.16f, 0), b + V(0.16f, 0), b + V(0.02f, height * 0.5f) }, new Color(0.1f, 0.08f, 0.07f));
        // Poles poking out of the top
        p.OutlinedLine(b + V(0.0f, height - 0.08f), b + V(-0.1f, height + 0.15f), 0.025f, WoodDark);
        p.OutlinedLine(b + V(0.1f, height - 0.08f), b + V(0.2f, height + 0.15f), 0.025f, WoodDark);
        p.InkLine(b + V(-halfWidth * 0.5f, height * 0.5f), b + V(halfWidth * 0.45f, height * 0.5f), 0.4f);
    }
}
