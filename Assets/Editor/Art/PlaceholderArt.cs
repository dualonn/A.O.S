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
public static class PlaceholderArt
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

        var buildingDrawers = new Dictionary<string, Action<Painter>>
        {
            { "Hut", DrawHut },
            { "ForagerCamp", DrawForagerCamp },
            { "LumberCamp", DrawLumberCamp },
            { "Quarry", DrawQuarry },
            { "Barracks", DrawBarracks },
            { "BronzeFoundry", DrawFoundry },
            { "Campfire", DrawCampfire },
        };

        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (database != null)
        {
            foreach (var def in database.buildings)
            {
                if (def == null || !buildingDrawers.TryGetValue(def.id, out var draw))
                {
                    continue;
                }
                var painter = new Painter(def.size.x, def.size.y, BuildingPPU);
                draw(painter);
                def.icon = Save(painter, $"Buildings/{def.id}.png", BuildingPPU, Vector2.zero);
                EditorUtility.SetDirty(def);
            }

            var resourceDrawers = new Dictionary<string, Action<Painter>>
            {
                { "Food", DrawFoodIcon }, { "Wood", DrawWoodIcon }, { "Stone", DrawStoneIcon }, { "Bronze", DrawBronzeIcon },
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
        var berryBush = Prop("World/BerryBush.png", 1f, 0.8f, DrawBerryBush);
        var copperOre = Prop("World/CopperOre.png", 1f, 0.7f, DrawCopperOre);
        art.trees = new List<Sprite>
        {
            Prop("World/TreeRound.png", 1.4f, 2f, DrawRoundTree),
            Prop("World/TreePine.png", 1.2f, 2.2f, DrawPineTree),
        };
        art.rocks = new List<Sprite>
        {
            Prop("World/RockA.png", 1f, 0.7f, p => DrawRock(p, 1)),
            Prop("World/RockB.png", 0.8f, 0.6f, p => DrawRock(p, 2)),
        };
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

        // Troops and raiders in the same style and scale as everything else
        var clubmanDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Clubman.asset");
        var raiderDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/EnemyClubman.asset");
        if (clubmanDef != null)
        {
            clubmanDef.prefab = BuildWarrior(ClubmanStyle, false, clubmanDef);
            clubmanDef.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Clubman/Clubman_Idle.png");
            EditorUtility.SetDirty(clubmanDef);
        }
        if (raiderDef != null)
        {
            raiderDef.prefab = BuildWarrior(RaiderStyle, true, raiderDef);
            raiderDef.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Raider/Raider_Idle.png");
            EditorUtility.SetDirty(raiderDef);
        }

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
                    case "Stone": deposit.sprites = new List<Sprite>(art.rocks); break;
                    case "CopperOre": deposit.sprites = new List<Sprite> { copperOre }; break;
                }
                EditorUtility.SetDirty(deposit);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Placeholder art generated in " + Root);
    }

    // ---------- Saving ----------

    private static Sprite Save(Painter painter, string relativePath, float ppu, Vector2 pivot,
        bool tile = false, Vector4 border = default)
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
        importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

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

    // ---------- Buildings (drawn in grid cells; 1 unit = 1 cell) ----------

    private static void DrawHut(Painter p)
    {
        p.Ellipse(V(1, 0.32f), V(0.85f, 0.18f), Shadow);
        p.OutlinedBox(0.45f, 0.3f, 1.55f, 1.05f, 0.03f, Mud);
        // Wall planks
        for (float x = 0.62f; x < 1.5f; x += 0.2f)
        {
            p.InkLine(V(x, 0.33f), V(x, 1.0f), 0.5f);
        }
        p.OutlinedBox(0.84f, 0.3f, 1.16f, 0.78f, 0.12f, Dark);
        var roof = new[] { V(0.22f, 0.92f), V(1.78f, 0.92f), V(1f, 1.82f) };
        p.OutlinedPolygon(roof, Straw);
        for (int i = 1; i < 6; i++)
        {
            float t = i / 6f;
            p.Line(V(1f, 1.78f), V(Mathf.Lerp(0.3f, 1.7f, t), 0.96f), 0.02f, StrawDark);
        }
        p.OutlinedLine(V(0.86f, 1.66f), V(1.14f, 1.98f), 0.05f, WoodDark);
        p.OutlinedLine(V(1.14f, 1.66f), V(0.86f, 1.98f), 0.05f, WoodDark);
    }

    private static void DrawForagerCamp(Painter p)
    {
        p.Ellipse(V(1, 0.32f), V(0.9f, 0.18f), Shadow);
        // Hide tent
        p.OutlinedLine(V(0.38f, 0.3f), V(0.8f, 1.42f), 0.04f, WoodDark);
        p.OutlinedLine(V(1.12f, 0.3f), V(0.62f, 1.42f), 0.04f, WoodDark);
        p.OutlinedPolygon(new[] { V(0.18f, 0.3f), V(1.22f, 0.3f), V(0.7f, 1.25f) }, Hide);
        p.OutlinedPolygon(new[] { V(0.54f, 0.3f), V(0.86f, 0.3f), V(0.7f, 0.78f) }, Dark);
        // Berry bush
        var bush = new[] { (V(1.5f, 0.66f), 0.36f), (V(1.24f, 0.52f), 0.24f), (V(1.76f, 0.5f), 0.24f) };
        foreach (var (c, r) in bush) p.Circle(c, r + p.Outline, p.Ink);
        foreach (var (c, r) in bush) p.Circle(c, r, Leaf);
        p.Circle(V(1.42f, 0.78f), 0.12f, LeafLight);
        foreach (var c in new[] { V(1.38f, 0.58f), V(1.58f, 0.72f), V(1.66f, 0.48f), V(1.2f, 0.46f), V(1.52f, 0.86f) })
        {
            p.OutlinedCircle(c, 0.045f, Berry);
        }
        // Basket
        p.OutlinedBox(0.95f, 0.28f, 1.2f, 0.45f, 0.04f, Straw);
        p.OutlinedCircle(V(1.03f, 0.5f), 0.04f, Berry);
        p.OutlinedCircle(V(1.12f, 0.5f), 0.04f, Berry);
    }

    private static void DrawLumberCamp(Painter p)
    {
        p.Ellipse(V(1, 0.32f), V(0.9f, 0.18f), Shadow);
        // Log pile 3-2-1
        float[] rows = { 0.42f, 0.66f, 0.9f };
        for (int row = 0; row < 3; row++)
        {
            float y = rows[row];
            float x0 = 0.2f + row * 0.13f;
            float length = 1.0f - row * 0.1f;
            p.OutlinedBox(x0, y - 0.11f, x0 + length, y + 0.11f, 0.1f, Wood);
            p.Line(V(x0 + 0.15f, y), V(x0 + length - 0.2f, y), 0.012f, WoodDark);
            float ex = x0 + length;
            p.OutlinedEllipse(V(ex, rows[row]), V(0.09f, 0.11f), WoodLight);
            p.Ellipse(V(ex, rows[row]), V(0.04f, 0.05f), Wood);
        }
        // Stump and axe
        p.OutlinedBox(1.45f, 0.28f, 1.8f, 0.55f, 0.03f, Wood);
        p.OutlinedEllipse(V(1.625f, 0.55f), V(0.175f, 0.06f), WoodLight);
        p.OutlinedLine(V(1.56f, 0.58f), V(1.82f, 1.12f), 0.045f, WoodLight);
        p.OutlinedPolygon(new[] { V(1.72f, 0.98f), V(1.92f, 1.0f), V(1.95f, 1.18f), V(1.78f, 1.1f) }, StoneLight);
    }

    private static void DrawQuarry(Painter p)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.35f, 0.24f), Shadow);
        var face = new[] { V(0.18f, 0.3f), V(0.35f, 1.55f), V(0.95f, 2.35f), V(1.8f, 2.55f), V(2.55f, 2.05f), V(2.82f, 0.3f) };
        p.OutlinedPolygon(face, Stone);
        p.OutlinedPolygon(new[] { V(0.8f, 0.3f), V(0.95f, 1.5f), V(1.6f, 1.85f), V(2.2f, 1.4f), V(2.3f, 0.3f) }, StoneDark);
        p.Polygon(new[] { V(0.95f, 2.2f), V(1.8f, 2.42f), V(2.3f, 2.1f), V(1.6f, 2.05f) }, StoneLight);
        p.InkLine(V(0.5f, 1.2f), V(0.75f, 1.0f));
        p.InkLine(V(2.45f, 1.6f), V(2.6f, 1.2f));
        p.InkLine(V(1.3f, 1.7f), V(1.45f, 1.35f));
        // Cut blocks
        p.OutlinedBox(0.95f, 0.3f, 1.4f, 0.62f, 0.02f, StoneLight);
        p.OutlinedBox(1.45f, 0.3f, 1.9f, 0.62f, 0.02f, StoneLight);
        p.OutlinedBox(1.2f, 0.67f, 1.65f, 0.99f, 0.02f, StoneLight);
        // Pickaxe
        p.OutlinedLine(V(2.15f, 0.32f), V(2.45f, 1.05f), 0.05f, WoodLight);
        p.OutlinedPolygon(new[] { V(2.15f, 1.02f), V(2.42f, 1.14f), V(2.72f, 0.98f), V(2.44f, 1.03f) }, StoneDark);
    }

    private static void DrawBarracks(Painter p)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.4f, 0.25f), Shadow);
        // Longhouse
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
        // Crossed clubs emblem
        p.OutlinedLine(V(1.3f, 1.2f), V(1.7f, 1.65f), 0.06f, WoodLight);
        p.OutlinedLine(V(1.7f, 1.2f), V(1.3f, 1.65f), 0.06f, WoodLight);
        p.OutlinedCircle(V(1.7f, 1.65f), 0.07f, Wood);
        p.OutlinedCircle(V(1.3f, 1.65f), 0.07f, Wood);
        // Flag
        p.OutlinedLine(V(2.55f, 1.9f), V(2.55f, 2.9f), 0.04f, WoodDark);
        p.OutlinedPolygon(new[] { V(2.57f, 2.86f), V(2.95f, 2.72f), V(2.57f, 2.58f) }, Flag);
        // Palisade with a gate gap
        var rng = new System.Random(7);
        for (float x = 0.22f; x < 2.85f; x += 0.21f)
        {
            if (x > 1.25f && x < 1.75f) continue;
            float top = 1.08f + (float)rng.NextDouble() * 0.12f;
            p.OutlinedPolygon(new[] { V(x - 0.09f, 0.3f), V(x + 0.09f, 0.3f), V(x + 0.09f, top), V(x, top + 0.14f), V(x - 0.09f, top) }, Wood);
        }
        p.OutlinedLine(V(0.15f, 0.8f), V(1.25f, 0.8f), 0.05f, WoodDark);
        p.OutlinedLine(V(1.75f, 0.8f), V(2.88f, 0.8f), 0.05f, WoodDark);
    }

    private static void DrawFoundry(Painter p)
    {
        p.Ellipse(V(1.5f, 0.38f), V(1.35f, 0.24f), Shadow);
        // Chimney + smoke
        p.OutlinedBox(1.75f, 1.4f, 2.1f, 2.55f, 0.02f, StoneDark);
        p.Circle(V(1.95f, 2.72f), 0.13f, new Color(0.5f, 0.5f, 0.5f, 0.55f));
        p.Circle(V(2.1f, 2.86f), 0.1f, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        // Furnace dome
        p.OutlinedBox(0.35f, 0.3f, 2.05f, 1.85f, 0.55f, Stone);
        var rng = new System.Random(3);
        for (int i = 0; i < 14; i++)
        {
            float x = 0.55f + (float)rng.NextDouble() * 1.3f;
            float y = 0.5f + (float)rng.NextDouble() * 1.15f;
            p.Box(V(x, y), V(0.22f, 0.12f), 0.03f, StoneLight);
            p.InkLine(V(x - 0.11f, y - 0.06f), V(x + 0.11f, y - 0.06f), 0.5f);
        }
        // Fire mouth
        p.OutlinedBox(0.85f, 0.3f, 1.55f, 0.95f, 0.3f, Dark);
        p.Circle(V(1.2f, 0.5f), 0.22f, Fire);
        p.Circle(V(1.2f, 0.47f), 0.12f, FireCore);
        // Bronze ingots
        foreach (var (x, y) in new[] { (2.2f, 0.32f), (2.55f, 0.32f), (2.38f, 0.48f) })
        {
            p.OutlinedPolygon(new[] { V(x - 0.15f, y), V(x + 0.15f, y), V(x + 0.1f, y + 0.14f), V(x - 0.1f, y + 0.14f) }, Bronze);
            p.Line(V(x - 0.07f, y + 0.1f), V(x + 0.07f, y + 0.1f), 0.02f, BronzeLight);
        }
    }

    private static void DrawCampfire(Painter p)
    {
        p.Ellipse(V(1, 0.5f), V(0.85f, 0.25f), Shadow);
        // Sitting logs
        p.OutlinedBox(0.15f, 0.25f, 0.65f, 0.42f, 0.08f, Wood);
        p.OutlinedBox(1.35f, 0.25f, 1.85f, 0.42f, 0.08f, Wood);
        // Stone ring
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2;
            p.OutlinedCircle(V(1 + Mathf.Cos(a) * 0.42f, 0.62f + Mathf.Sin(a) * 0.2f), 0.09f, i % 2 == 0 ? Stone : StoneLight);
        }
        // Crossed logs and fire
        p.OutlinedLine(V(0.7f, 0.55f), V(1.3f, 0.75f), 0.08f, WoodDark);
        p.OutlinedLine(V(1.3f, 0.55f), V(0.7f, 0.75f), 0.08f, WoodDark);
        p.OutlinedPolygon(new[] { V(0.78f, 0.68f), V(1.22f, 0.68f), V(1.1f, 1.05f), V(1.0f, 0.9f), V(0.92f, 1.15f), V(0.86f, 0.95f) }, Fire);
        p.Polygon(new[] { V(0.9f, 0.7f), V(1.1f, 0.7f), V(1.02f, 0.92f), V(0.96f, 0.85f) }, FireCore);
        // Banner pole marks the town center
        p.OutlinedLine(V(1.72f, 0.55f), V(1.72f, 1.85f), 0.045f, WoodDark);
        p.OutlinedPolygon(new[] { V(1.74f, 1.82f), V(1.98f, 1.7f), V(1.74f, 1.56f) }, Blue);
        p.Circle(V(1.0f, 1.35f), 0.12f, new Color(0.5f, 0.5f, 0.5f, 0.45f));
        p.Circle(V(1.12f, 1.58f), 0.09f, new Color(0.5f, 0.5f, 0.5f, 0.3f));
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

    private static void DrawCopperOre(Painter p)
    {
        Color copper = new Color(0.85f, 0.5f, 0.25f);
        Color verdigris = new Color(0.35f, 0.7f, 0.6f);
        p.Ellipse(V(0.5f, 0.1f), V(0.45f, 0.08f), Shadow);
        p.OutlinedPolygon(new[] { V(0.08f, 0.08f), V(0.16f, 0.45f), V(0.45f, 0.62f), V(0.8f, 0.5f), V(0.92f, 0.08f) }, StoneDark);
        foreach (var (c, col) in new[] { (V(0.3f, 0.3f), copper), (V(0.55f, 0.42f), verdigris), (V(0.7f, 0.25f), copper), (V(0.42f, 0.18f), verdigris), (V(0.62f, 0.3f), copper) })
        {
            p.OutlinedCircle(c, 0.05f, col);
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

    private static void DrawRock(Painter p, int variant)
    {
        if (variant == 1)
        {
            p.Ellipse(V(0.5f, 0.1f), V(0.45f, 0.08f), Shadow);
            p.OutlinedPolygon(new[] { V(0.08f, 0.08f), V(0.18f, 0.42f), V(0.45f, 0.62f), V(0.78f, 0.52f), V(0.92f, 0.08f) }, Stone);
            p.Polygon(new[] { V(0.25f, 0.42f), V(0.45f, 0.58f), V(0.6f, 0.52f), V(0.4f, 0.4f) }, StoneLight);
        }
        else
        {
            p.Ellipse(V(0.4f, 0.1f), V(0.36f, 0.07f), Shadow);
            p.OutlinedPolygon(new[] { V(0.08f, 0.08f), V(0.2f, 0.4f), V(0.55f, 0.5f), V(0.72f, 0.08f) }, StoneDark);
            p.Polygon(new[] { V(0.22f, 0.36f), V(0.5f, 0.45f), V(0.45f, 0.35f) }, Stone);
        }
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

    // ---------- Warriors (troops and raiders) ----------

    private enum WarPose { Idle, StrideA, Pass, StrideB, Windup, Raised, Strike, Recover, Stagger, Falling, Down }

    /// <summary>
    /// A classic stick figure (line limbs, round head) in a team colour; all the detail is gear.
    /// </summary>
    private class WarriorStyle
    {
        public string name;
        public Color ink;      // body colour = team colour
        public Color gear;     // headband, wraps, belt
        public bool feather;   // raider extras
        public bool necklace;
    }

    private static readonly WarriorStyle ClubmanStyle = new WarriorStyle
    {
        name = "Clubman", ink = new Color(0.08f, 0.07f, 0.07f), gear = new Color(0.22f, 0.45f, 0.9f)
    };

    private static readonly WarriorStyle RaiderStyle = new WarriorStyle
    {
        name = "Raider", ink = new Color(0.78f, 0.13f, 0.12f), gear = new Color(0.18f, 0.12f, 0.08f), feather = true, necklace = true
    };

    /// <summary>Generate a warrior's frames and build a prefab for it. Returns the prefab.</summary>
    private static GameObject BuildWarrior(WarriorStyle style, bool isEnemy, UnitDefinition definition)
    {
        string dir = $"Units/{style.name}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        Sprite Frame(string frame, WarPose pose)
        {
            // Wide canvas so swings and falling bodies aren't clipped; feet stay at the centre.
            var p = new Painter(1.6f, 1.2f, 160) { Outline = 0.018f };
            DrawWarrior(p, pose, style);
            return Save(p, $"{dir}/{style.name}_{frame}.png", 160, new Vector2(0.5f, 0.05f));
        }

        var idle = Frame("Idle", WarPose.Idle);
        var walk = new List<Sprite> { Frame("Walk0", WarPose.StrideA), Frame("Walk1", WarPose.Pass), Frame("Walk2", WarPose.StrideB), Frame("Walk3", WarPose.Pass) };
        var attack = new List<Sprite> { Frame("Attack0", WarPose.Windup), Frame("Attack1", WarPose.Raised), Frame("Attack2", WarPose.Strike), Frame("Attack3", WarPose.Recover) };
        var death = new List<Sprite> { Frame("Death0", WarPose.Stagger), Frame("Death1", WarPose.Falling), Frame("Death2", WarPose.Down) };

        // Prefab: sprite + animation + combat + a small collider for targeting and hovering.
        var go = new GameObject($"{style.name}Unit");
        go.tag = isEnemy ? "enemy" : "troop";
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle;
        var visual = go.AddComponent<SpriteUnitVisual>();
        visual.idle = idle;
        visual.walk = walk;
        visual.attack = attack;
        visual.death = death;
        visual.impactFrame = 2;
        visual.barHeight = 0.95f;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;
        col.offset = new Vector2(0, 0.4f);
        go.AddComponent<MouseTouching>();
        Unit unit = isEnemy ? go.AddComponent<Enemy>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = 0.38f;

        string prefabPath = $"Assets/Prefabs/Units/{style.name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    private static void DrawWarrior(Painter p, WarPose pose, WarriorStyle s)
    {
        const float limb = 0.042f;
        // Death frames lean the whole figure back around the feet.
        float lean = pose == WarPose.Stagger ? -18f : pose == WarPose.Falling ? -55f : pose == WarPose.Down ? -88f : 0f;
        Vector2 pivot = V(0.5f, 0.06f);
        float rad = lean * Mathf.Deg2Rad;
        // The figure is designed around x = 0.5; shift it to the middle of the 1.6-wide canvas.
        Vector2 shift = V(0.3f, 0f);
        Vector2 T(Vector2 v) => v + shift;
        Vector2 R(Vector2 v)
        {
            Vector2 d = v - pivot;
            return shift + pivot + new Vector2(d.x * Mathf.Cos(rad) - d.y * Mathf.Sin(rad), d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad));
        }
        if (pose == WarPose.Down)
        {
            pivot = V(0.5f, 0.12f); // lie a little above the ground line
        }

        Vector2 hip = V(0.5f, 0.38f), shoulder = V(0.5f, 0.63f), head = V(0.5f, 0.77f);
        Vector2 footL, footR, handL, handR, clubTip;
        switch (pose)
        {
            case WarPose.StrideA:
                footL = V(0.37f, 0.06f); footR = V(0.63f, 0.06f); handL = V(0.38f, 0.46f); handR = V(0.64f, 0.5f); clubTip = V(0.78f, 0.78f); break;
            case WarPose.StrideB:
                footL = V(0.63f, 0.06f); footR = V(0.37f, 0.06f); handL = V(0.62f, 0.46f); handR = V(0.58f, 0.48f); clubTip = V(0.72f, 0.8f); break;
            case WarPose.Pass:
                footL = V(0.47f, 0.06f); footR = V(0.54f, 0.11f); handL = V(0.45f, 0.44f); handR = V(0.62f, 0.48f); clubTip = V(0.76f, 0.79f); break;
            case WarPose.Windup:
                footL = V(0.38f, 0.06f); footR = V(0.62f, 0.06f); handL = V(0.44f, 0.6f); handR = V(0.4f, 0.62f); clubTip = V(0.16f, 0.72f); break;
            case WarPose.Raised:
                footL = V(0.38f, 0.06f); footR = V(0.62f, 0.06f); handL = V(0.5f, 0.86f); handR = V(0.53f, 0.88f); clubTip = V(0.32f, 1.0f); break;
            case WarPose.Strike:
                footL = V(0.36f, 0.06f); footR = V(0.66f, 0.06f); handL = V(0.7f, 0.58f); handR = V(0.72f, 0.56f); clubTip = V(0.95f, 0.3f); break;
            case WarPose.Recover:
                footL = V(0.38f, 0.06f); footR = V(0.64f, 0.06f); handL = V(0.62f, 0.46f); handR = V(0.66f, 0.45f); clubTip = V(0.86f, 0.2f); break;
            default: // Idle and death frames
                footL = V(0.42f, 0.06f); footR = V(0.58f, 0.06f); handL = V(0.38f, 0.44f); handR = V(0.63f, 0.47f); clubTip = V(0.75f, 0.78f); break;
        }

        p.Ellipse(T(V(pose == WarPose.Down ? 0.8f : 0.5f, 0.06f)), V(pose == WarPose.Down ? 0.36f : 0.16f, 0.035f), Shadow);

        // Feather behind the head (drawn first so the head overlaps it)
        if (s.feather)
        {
            p.OutlinedLine(R(V(0.45f, 0.82f)), R(V(0.36f, 0.98f)), 0.03f, new Color(0.95f, 0.92f, 0.85f));
            p.Line(R(V(0.37f, 0.96f)), R(V(0.355f, 0.99f)), 0.03f, new Color(0.15f, 0.12f, 0.1f));
        }
        // Headband tails flowing behind
        p.Line(R(V(0.43f, 0.79f)), R(V(0.33f, 0.74f)), 0.022f, s.gear);
        p.Line(R(V(0.43f, 0.79f)), R(V(0.35f, 0.7f)), 0.022f, s.gear);

        // The stickman: legs, spine, arms, round head
        p.Line(R(hip), R(footL), limb, s.ink);
        p.Line(R(hip), R(footR), limb, s.ink);
        p.Line(R(hip), R(V(0.5f, 0.69f)), limb, s.ink);
        p.Line(R(shoulder), R(handL), limb, s.ink);
        p.Line(R(shoulder), R(handR), limb, s.ink);
        p.Circle(R(head), 0.085f, s.ink);

        // Gear
        p.Line(R(V(0.425f, 0.785f)), R(V(0.575f, 0.785f)), 0.026f, s.gear);          // headband
        p.Line(R(V(0.455f, 0.385f)), R(V(0.545f, 0.385f)), 0.03f, s.gear);           // belt
        p.Line(R(V(0.5f, 0.385f)), R(V(0.53f, 0.31f)), 0.022f, s.gear);              // belt tail
        Vector2 Wrist(Vector2 hand) => Vector2.Lerp(shoulder, hand, 0.82f);
        p.Circle(R(Wrist(handL)), 0.022f, s.gear);                                    // wrist wraps
        p.Circle(R(Wrist(handR)), 0.022f, s.gear);
        if (s.necklace)
        {
            // Bone teeth hanging on the chest
            p.Line(R(V(0.455f, 0.64f)), R(V(0.545f, 0.64f)), 0.01f, s.gear);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 top = V(0.5f + i * 0.03f, 0.635f);
                p.OutlinedLine(R(top), R(top + V(0, -0.035f)), 0.014f, new Color(0.96f, 0.94f, 0.86f));
            }
        }

        // Club: wrapped grip, then the knobbly head
        if (pose != WarPose.Down && pose != WarPose.Falling)
        {
            Vector2 grip = Vector2.Lerp(handR, clubTip, 0.18f);
            p.OutlinedLine(R(handR), R(Vector2.Lerp(handR, clubTip, 0.55f)), 0.035f, WoodLight);
            p.Line(R(Vector2.Lerp(handR, clubTip, -0.04f)), R(grip), 0.045f, s.gear);
            p.OutlinedLine(R(Vector2.Lerp(handR, clubTip, 0.5f)), R(clubTip), 0.075f, Wood);
            p.Circle(R(Vector2.Lerp(handR, clubTip, 0.78f)), 0.012f, WoodDark);        // knots
            p.Circle(R(Vector2.Lerp(handR, clubTip, 0.9f)), 0.012f, WoodDark);
        }
        else
        {
            // Dropped club on the ground
            p.OutlinedLine(T(V(0.2f, 0.08f)), T(V(0.42f, 0.1f)), 0.035f, WoodLight);
            p.OutlinedLine(T(V(0.38f, 0.1f)), T(V(0.55f, 0.12f)), 0.075f, Wood);
        }
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
