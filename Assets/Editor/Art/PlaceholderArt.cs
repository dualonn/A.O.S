using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Generates the placeholder art (flat stickman style) as PNG sprites under Assets/Art/Generated,
/// then assigns them to buildings, resources and the ArtLibrary.
/// Re-running overwrites the generated files only; hand-made art elsewhere is untouched.
/// </summary>
public static partial class PlaceholderArt
{
    private const string Root = "Assets/Art/Generated";
    private const float BuildingPPU = 128f;

    // Palette
    private static readonly Color Straw = new Color(0.86f, 0.72f, 0.38f);
    private static readonly Color StrawDark = new Color(0.66f, 0.52f, 0.25f);
    private static readonly Color Mud = new Color(0.76f, 0.6f, 0.42f);
    private static readonly Color Wood = new Color(0.6f, 0.4f, 0.22f);
    private static readonly Color WoodLight = new Color(0.82f, 0.66f, 0.44f);
    private static readonly Color WoodDark = new Color(0.4f, 0.26f, 0.14f);
    private static readonly Color Hide = new Color(0.74f, 0.56f, 0.38f);
    private static readonly Color Leaf = new Color(0.33f, 0.58f, 0.27f);
    private static readonly Color LeafLight = new Color(0.45f, 0.7f, 0.35f);
    private static readonly Color LeafDark = new Color(0.22f, 0.42f, 0.2f);
    private static readonly Color Stone = new Color(0.62f, 0.62f, 0.64f);
    private static readonly Color StoneLight = new Color(0.78f, 0.78f, 0.8f);
    private static readonly Color StoneDark = new Color(0.45f, 0.45f, 0.48f);
    private static readonly Color Bronze = new Color(0.8f, 0.52f, 0.22f);
    private static readonly Color BronzeLight = new Color(0.95f, 0.72f, 0.4f);
    private static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
    private static readonly Color FireCore = new Color(1f, 0.88f, 0.35f);
    private static readonly Color Berry = new Color(0.82f, 0.15f, 0.2f);
    private static readonly Color Shadow = new Color(0, 0, 0, 0.2f);
    private static readonly Color Dark = new Color(0.2f, 0.13f, 0.08f);
    private static readonly Color Flag = new Color(0.85f, 0.2f, 0.18f);
    private static readonly Color Blue = new Color(0.25f, 0.45f, 0.85f);

    [MenuItem("Tools/Age of Stickman/Generate Placeholder Art")]
    public static void GenerateAll()
    {
        foreach (var folder in new[] { "Buildings", "World", "UI", "Icons" })
        {
            Directory.CreateDirectory(Path.Combine(Root, folder));
        }

        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (database != null)
        {
            // Every building: one look per upgrade level, each a loop of animation frames.
            foreach (var def in database.buildings)
            {
                if (def != null)
                {
                    GenerateBuildingLooks(def);
                }
            }

            var resourceDrawers = new Dictionary<string, Action<Painter>>
            {
                { "Food", DrawFoodIcon }, { "Wood", DrawWoodIcon }, { "Stone", DrawStoneIcon }, { "Bronze", DrawBronzeIcon },
                { "Iron", DrawIronIcon }, { "Gold", DrawGoldIcon },
                { "Charcoal", DrawCharcoalIcon }, { "Tools", DrawToolsIcon }, { "Coin", DrawCoinIcon },
            };
            foreach (var res in database.resources)
            {
                if (res != null && resourceDrawers.TryGetValue(res.id, out var draw))
                {
                    res.icon = Icon($"Icons/{res.id}.png", draw);
                    EditorUtility.SetDirty(res);
                }
            }
        }

        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<ArtLibrary>();
            AssetDatabase.CreateAsset(art, "Assets/Resources/ArtLibrary.asset");
        }

        art.grassTile = Tile("World/Grass.png", 1, 1, p => DrawGrass(p, new Color(0.42f, 0.6f, 0.31f), 11));
        art.settlementTile = Tile("World/SettlementGround.png", 1, 1, p => DrawGrass(p, new Color(0.55f, 0.62f, 0.36f), 23));
        art.pathTile = Tile("World/Path.png", 1, 1.6f, DrawPath);
        art.waterTile = Tile("World/Water.png", 1, 1, DrawWater);
        art.hillTile = Tile("World/Hill.png", 1, 1, DrawHill);
        GenerateTerrain(art);
        var berryBush = Prop("World/BerryBush.png", 1f, 0.8f, DrawBerryBush);
        art.trees = new List<Sprite>
        {
            Prop("World/TreeRound.png", 1.4f, 2f, DrawRoundTree),
            Prop("World/TreePine.png", 1.2f, 2.2f, DrawPineTree),
        };
        GenerateDepositArt(art, database);   // stone and ores (DepositArt.cs)
        art.bushes = new List<Sprite> { Prop("World/Bush.png", 1f, 0.7f, DrawBush) };

        var scaffoldPainter = new Painter(1, 1, BuildingPPU);
        DrawScaffold(scaffoldPainter);
        art.scaffold = Save(scaffoldPainter, "World/Scaffold.png", BuildingPPU, Vector2.zero);
        art.rallyFlag = Prop("World/RallyFlag.png", 0.7f, 1.3f, DrawRallyFlag);

        Directory.CreateDirectory(Path.Combine(Root, "Units"));
        art.villagerIdle = Villager("Units/Villager_Idle.png", VillagerPose.Idle);
        art.villagerWalk = new List<Sprite>
        {
            Villager("Units/Villager_Walk0.png", VillagerPose.StrideA),
            Villager("Units/Villager_Walk1.png", VillagerPose.Pass),
            Villager("Units/Villager_Walk2.png", VillagerPose.StrideB),
            Villager("Units/Villager_Walk3.png", VillagerPose.Pass),
        };
        art.villagerWork = new List<Sprite>
        {
            Villager("Units/Villager_Work0.png", VillagerPose.ToolUp),
            Villager("Units/Villager_Work1.png", VillagerPose.ToolDown),
        };

        art.panel = PanelSprite();
        art.peopleIcon = Icon("Icons/People.png", p => DrawStickIcon(p, Painter.V(0, 0), new Color(0.1f, 0.1f, 0.1f), false));
        art.enemyIcon = Icon("Icons/Enemy.png", p => DrawStickIcon(p, Painter.V(0, 0), new Color(0.85f, 0.15f, 0.15f), true));
        art.researchIcon = Icon("Icons/Research.png", DrawScrollIcon);
        EditorUtility.SetDirty(art);

        // Troops, raiders and tower crews for every age
        GenerateWarriorsAndCrews(database);

        // Deposit sprites on the map
        if (database != null)
        {
            foreach (var deposit in database.deposits)
            {
                if (deposit == null) continue;
                switch (deposit.id)
                {
                    case "Forest": deposit.sprites = new List<Sprite>(art.trees); break;
                    case "Berries": deposit.sprites = new List<Sprite> { berryBush }; break;
                }
                EditorUtility.SetDirty(deposit);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Placeholder art generated in " + Root);
    }

    // ---------- Saving ----------

    private static Sprite Save(Painter painter, string relativePath, float ppu, Vector2 pivot,
        bool tile = false, Vector4 border = default, bool clamp = false)
    {
        string path = $"{Root}/{relativePath}";
        File.WriteAllBytes(path, painter.ToTexture().EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = tile && !clamp ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = tile || border != Vector4.zero ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        settings.spriteBorder = border;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite Tile(string path, float w, float h, Action<Painter> draw)
    {
        var p = new Painter(w, h, BuildingPPU);
        draw(p);
        return Save(p, path, BuildingPPU, Vector2.zero, tile: true);
    }

    private static Sprite Prop(string path, float w, float h, Action<Painter> draw)
    {
        var p = new Painter(w, h, BuildingPPU);
        draw(p);
        return Save(p, path, BuildingPPU, new Vector2(0.5f, 0.06f));
    }

    private static Sprite Icon(string path, Action<Painter> draw)
    {
        var p = new Painter(1, 1, 96) { Outline = 0.05f };
        draw(p);
        return Save(p, path, 96, new Vector2(0.5f, 0.5f));
    }

    private static Sprite PanelSprite()
    {
        var p = new Painter(64, 64, 1);
        p.Box(V(32, 32), V(64, 64), 14, Color.white);
        return Save(p, "UI/Panel.png", 100, new Vector2(0.5f, 0.5f), border: new Vector4(16, 16, 16, 16));
    }

    // ---------- World ----------

    private static void DrawWater(Painter p)
    {
        Color water = new Color(0.27f, 0.52f, 0.8f);
        Color light = new Color(0.45f, 0.68f, 0.92f);
        p.Box(V(0.5f, 0.5f), V(1, 1), 0, water);
        var rng = new System.Random(17);
        for (int i = 0; i < 3; i++)
        {
            float x = 0.15f + (float)rng.NextDouble() * 0.5f;
            float y = 0.2f + (float)rng.NextDouble() * 0.6f;
            p.Line(V(x, y), V(x + 0.08f, y + 0.03f), 0.018f, light);
            p.Line(V(x + 0.08f, y + 0.03f), V(x + 0.16f, y), 0.018f, light);
        }
    }

    private static void DrawHill(Painter p)
    {
        Color rock = new Color(0.56f, 0.51f, 0.43f);
        Color dark = new Color(0.45f, 0.4f, 0.33f);
        Color light = new Color(0.66f, 0.61f, 0.52f);
        p.Box(V(0.5f, 0.5f), V(1, 1), 0, rock);
        var rng = new System.Random(29);
        for (int i = 0; i < 5; i++)
        {
            float x = 0.12f + (float)rng.NextDouble() * 0.76f;
            float y = 0.12f + (float)rng.NextDouble() * 0.76f;
            p.Ellipse(V(x, y), V(0.05f, 0.03f), i % 2 == 0 ? dark : light);
        }
        p.Line(V(0.2f, 0.7f), V(0.32f, 0.62f), 0.015f, dark);
        p.Line(V(0.6f, 0.3f), V(0.72f, 0.36f), 0.015f, dark);
    }

    private static void DrawBerryBush(Painter p)
    {
        p.Ellipse(V(0.5f, 0.1f), V(0.42f, 0.07f), Shadow);
        var blobs = new[] { (V(0.5f, 0.42f), 0.3f), (V(0.25f, 0.3f), 0.2f), (V(0.75f, 0.3f), 0.2f) };
        foreach (var (c, r) in blobs) p.Circle(c, r + p.Outline, p.Ink);
        foreach (var (c, r) in blobs) p.Circle(c, r, Leaf);
        p.Circle(V(0.42f, 0.52f), 0.1f, LeafLight);
        foreach (var c in new[] { V(0.35f, 0.38f), V(0.55f, 0.55f), V(0.66f, 0.32f), V(0.25f, 0.26f), V(0.5f, 0.3f), V(0.78f, 0.4f) })
        {
            p.OutlinedCircle(c, 0.045f, Berry);
        }
    }

    private static void DrawGrass(Painter p, Color baseColor, int seed)
    {
        p.Box(V(0.5f, 0.5f), V(1, 1), 0, baseColor);
        var rng = new System.Random(seed);
        Color dark = baseColor * 0.85f; dark.a = 1;
        Color light = Color.Lerp(baseColor, Color.white, 0.15f);
        for (int i = 0; i < 14; i++)
        {
            float x = 0.1f + (float)rng.NextDouble() * 0.8f;
            float y = 0.1f + (float)rng.NextDouble() * 0.8f;
            Color c = i % 2 == 0 ? dark : light;
            p.Line(V(x, y), V(x - 0.03f, y + 0.07f), 0.012f, c);
            p.Line(V(x, y), V(x + 0.03f, y + 0.08f), 0.012f, c);
        }
    }

    private static void DrawPath(Painter p)
    {
        Color dirt = new Color(0.66f, 0.53f, 0.35f);
        Color edge = new Color(0.5f, 0.4f, 0.26f);
        const float twoPi = Mathf.PI * 2;
        Func<Vector2, float> band = q => Mathf.Abs(q.y - 0.8f) - (0.62f + 0.035f * Mathf.Sin(q.x * twoPi * 2));
        p.Draw(q => band(q) - 0.03f, new Rect(0, 0, 1, 1.6f), edge);
        p.Draw(band, new Rect(0, 0, 1, 1.6f), dirt);
        var rng = new System.Random(5);
        for (int i = 0; i < 6; i++)
        {
            float x = 0.1f + (float)rng.NextDouble() * 0.8f;
            float y = 0.4f + (float)rng.NextDouble() * 0.8f;
            p.Ellipse(V(x, y), V(0.035f, 0.025f), edge);
        }
    }

    private static void DrawRoundTree(Painter p)
    {
        p.Ellipse(V(0.7f, 0.14f), V(0.5f, 0.1f), Shadow);
        p.OutlinedBox(0.6f, 0.1f, 0.8f, 0.9f, 0.03f, Wood);
        var blobs = new[] { (V(0.7f, 1.35f), 0.52f), (V(0.4f, 1.05f), 0.33f), (V(1.0f, 1.05f), 0.33f) };
        foreach (var (c, r) in blobs) p.Circle(c, r + p.Outline, p.Ink);
        foreach (var (c, r) in blobs) p.Circle(c, r, Leaf);
        p.Circle(V(0.58f, 1.5f), 0.18f, LeafLight);
    }

    private static void DrawPineTree(Painter p)
    {
        p.Ellipse(V(0.6f, 0.14f), V(0.45f, 0.1f), Shadow);
        p.OutlinedBox(0.52f, 0.1f, 0.68f, 0.6f, 0.02f, Wood);
        p.OutlinedPolygon(new[] { V(0.1f, 0.5f), V(1.1f, 0.5f), V(0.6f, 1.35f) }, LeafDark);
        p.OutlinedPolygon(new[] { V(0.2f, 1.0f), V(1.0f, 1.0f), V(0.6f, 1.75f) }, Leaf);
        p.OutlinedPolygon(new[] { V(0.32f, 1.45f), V(0.88f, 1.45f), V(0.6f, 2.12f) }, LeafLight);
    }

    private static void DrawBush(Painter p)
    {
        p.Ellipse(V(0.5f, 0.1f), V(0.42f, 0.07f), Shadow);
        var blobs = new[] { (V(0.5f, 0.38f), 0.28f), (V(0.26f, 0.26f), 0.2f), (V(0.74f, 0.26f), 0.2f) };
        foreach (var (c, r) in blobs) p.Circle(c, r + p.Outline, p.Ink);
        foreach (var (c, r) in blobs) p.Circle(c, r, LeafDark);
        p.Circle(V(0.44f, 0.46f), 0.1f, Leaf);
    }

    private static void DrawScaffold(Painter p)
    {
        p.Outline = 0.025f;
        p.OutlinedLine(V(0.1f, 0.05f), V(0.1f, 0.95f), 0.04f, WoodLight);
        p.OutlinedLine(V(0.9f, 0.05f), V(0.9f, 0.95f), 0.04f, WoodLight);
        p.OutlinedLine(V(0.05f, 0.5f), V(0.95f, 0.5f), 0.04f, WoodLight);
        p.OutlinedLine(V(0.05f, 0.9f), V(0.95f, 0.9f), 0.04f, WoodLight);
        p.OutlinedLine(V(0.12f, 0.1f), V(0.88f, 0.48f), 0.03f, Wood);
        p.OutlinedLine(V(0.88f, 0.52f), V(0.12f, 0.88f), 0.03f, Wood);
    }

    private static void DrawRallyFlag(Painter p)
    {
        p.Ellipse(V(0.35f, 0.08f), V(0.18f, 0.05f), Shadow);
        p.OutlinedLine(V(0.2f, 0.08f), V(0.2f, 1.25f), 0.04f, WoodDark);
        p.OutlinedPolygon(new[] { V(0.22f, 1.22f), V(0.62f, 1.08f), V(0.22f, 0.92f) }, Blue);
    }

    // ---------- Villager (ambient townsfolk) ----------

    private enum VillagerPose { Idle, StrideA, Pass, StrideB, ToolUp, ToolDown }

    private static Sprite Villager(string path, VillagerPose pose)
    {
        var p = new Painter(0.6f, 0.9f, 160) { Outline = 0.018f };
        DrawVillager(p, pose);
        return Save(p, path, 160, new Vector2(0.5f, 0.05f));
    }

    private static void DrawVillager(Painter p, VillagerPose pose)
    {
        Color ink = p.Ink;
        const float limb = 0.04f;
        Vector2 hip = V(0.3f, 0.36f);
        Vector2 shoulder = V(0.3f, 0.6f);

        Vector2 footL, footR, handL, handR;
        switch (pose)
        {
            case VillagerPose.StrideA:
                footL = V(0.17f, 0.05f); footR = V(0.43f, 0.05f); handL = V(0.42f, 0.42f); handR = V(0.18f, 0.44f);
                break;
            case VillagerPose.StrideB:
                footL = V(0.43f, 0.05f); footR = V(0.17f, 0.05f); handL = V(0.18f, 0.44f); handR = V(0.42f, 0.42f);
                break;
            case VillagerPose.Pass:
                footL = V(0.27f, 0.05f); footR = V(0.34f, 0.1f); handL = V(0.26f, 0.4f); handR = V(0.35f, 0.41f);
                break;
            case VillagerPose.ToolUp:
                footL = V(0.2f, 0.05f); footR = V(0.4f, 0.05f); handL = V(0.43f, 0.68f); handR = V(0.45f, 0.66f);
                break;
            case VillagerPose.ToolDown:
                footL = V(0.2f, 0.05f); footR = V(0.4f, 0.05f); handL = V(0.43f, 0.46f); handR = V(0.45f, 0.44f);
                break;
            default:
                footL = V(0.24f, 0.05f); footR = V(0.36f, 0.05f); handL = V(0.19f, 0.4f); handR = V(0.41f, 0.4f);
                break;
        }

        p.Ellipse(V(0.3f, 0.05f), V(0.14f, 0.03f), Shadow);
        // Classic stickman townsperson: a hide scarf and a little satchel set them apart from soldiers.
        p.Line(hip, footL, limb, ink);
        p.Line(hip, footR, limb, ink);
        p.Line(hip, V(0.3f, 0.66f), limb, ink);
        p.Line(shoulder, handL, limb, ink);
        p.Line(shoulder, handR, limb, ink);
        p.Circle(V(0.3f, 0.74f), 0.085f, ink);
        p.Line(V(0.25f, 0.63f), V(0.35f, 0.63f), 0.035f, Hide);          // scarf
        p.Line(V(0.27f, 0.63f), V(0.23f, 0.55f), 0.028f, Hide);          // scarf tail
        p.Line(V(0.34f, 0.6f), V(0.24f, 0.42f), 0.012f, WoodDark);      // satchel strap
        p.OutlinedBox(0.2f, 0.36f, 0.27f, 0.43f, 0.015f, Hide);          // satchel

        if (pose == VillagerPose.ToolUp || pose == VillagerPose.ToolDown)
        {
            // A stone tool: handle + head
            Vector2 hand = handR;
            Vector2 tip = pose == VillagerPose.ToolUp ? hand + V(0.04f, 0.12f) : hand + V(0.05f, -0.13f);
            p.OutlinedLine(hand, tip, 0.025f, WoodLight);
            p.OutlinedCircle(tip, 0.035f, StoneDark);
        }
    }

    // ---------- Icons (1x1 unit) ----------

    private static void DrawFoodIcon(Painter p)
    {
        p.OutlinedLine(V(0.58f, 0.42f), V(0.8f, 0.2f), 0.09f, Color.white);
        p.OutlinedCircle(V(0.82f, 0.26f), 0.06f, Color.white);
        p.OutlinedCircle(V(0.74f, 0.18f), 0.06f, Color.white);
        p.Circle(V(0.42f, 0.58f), 0.3f + p.Outline, p.Ink);
        p.Circle(V(0.42f, 0.58f), 0.3f, new Color(0.7f, 0.3f, 0.2f));
        p.Circle(V(0.34f, 0.67f), 0.1f, new Color(0.85f, 0.45f, 0.3f));
    }

    private static void DrawWoodIcon(Painter p)
    {
        p.OutlinedBox(0.1f, 0.3f, 0.78f, 0.66f, 0.12f, Wood);
        p.Line(V(0.2f, 0.48f), V(0.65f, 0.48f), 0.02f, WoodDark);
        p.OutlinedEllipse(V(0.8f, 0.48f), V(0.12f, 0.18f), WoodLight);
        p.Ellipse(V(0.8f, 0.48f), V(0.05f, 0.08f), Wood);
    }

    private static void DrawStoneIcon(Painter p)
    {
        p.OutlinedPolygon(new[] { V(0.12f, 0.2f), V(0.22f, 0.62f), V(0.52f, 0.82f), V(0.85f, 0.6f), V(0.88f, 0.2f) }, Stone);
        p.Polygon(new[] { V(0.28f, 0.58f), V(0.52f, 0.76f), V(0.7f, 0.62f), V(0.45f, 0.52f) }, StoneLight);
    }

    private static void DrawBronzeIcon(Painter p)
    {
        p.OutlinedPolygon(new[] { V(0.1f, 0.25f), V(0.9f, 0.25f), V(0.75f, 0.68f), V(0.25f, 0.68f) }, Bronze);
        p.Line(V(0.32f, 0.58f), V(0.68f, 0.58f), 0.05f, BronzeLight);
    }

    private static void DrawStickIcon(Painter p, Vector2 offset, Color color, bool club)
    {
        float w = 0.07f;
        p.Circle(V(0.5f, 0.78f), 0.13f, color);
        p.Line(V(0.5f, 0.66f), V(0.5f, 0.36f), w, color);
        p.Line(V(0.5f, 0.36f), V(0.32f, 0.08f), w, color);
        p.Line(V(0.5f, 0.36f), V(0.68f, 0.08f), w, color);
        p.Line(V(0.5f, 0.58f), V(0.28f, 0.42f), w, color);
        p.Line(V(0.5f, 0.58f), V(0.72f, 0.46f), w, color);
        if (club)
        {
            p.OutlinedLine(V(0.72f, 0.46f), V(0.86f, 0.8f), 0.08f, Wood);
        }
    }

    private static void DrawScrollIcon(Painter p)
    {
        p.OutlinedBox(0.2f, 0.25f, 0.8f, 0.75f, 0.02f, new Color(0.95f, 0.88f, 0.7f));
        p.OutlinedCircle(V(0.2f, 0.5f), 0.09f, new Color(0.85f, 0.75f, 0.55f));
        p.OutlinedCircle(V(0.8f, 0.5f), 0.09f, new Color(0.85f, 0.75f, 0.55f));
        for (float y = 0.38f; y < 0.7f; y += 0.1f)
        {
            p.Line(V(0.35f, y), V(0.65f, y), 0.025f, new Color(0.5f, 0.4f, 0.3f));
        }
    }
}
