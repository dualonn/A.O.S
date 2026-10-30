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

        // Bronze Age units, the Watchtower crew and its arrows
        var spearmanDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Spearman.asset");
        var bronzeRaiderDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/BronzeRaider.asset");
        if (spearmanDef != null)
        {
            spearmanDef.prefab = BuildWarrior(SpearmanStyle, false, spearmanDef);
            spearmanDef.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Spearman/Spearman_Idle.png");
            EditorUtility.SetDirty(spearmanDef);
        }
        if (bronzeRaiderDef != null)
        {
            bronzeRaiderDef.prefab = BuildWarrior(BronzeRaiderStyle, true, bronzeRaiderDef);
            bronzeRaiderDef.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/BronzeRaider/BronzeRaider_Idle.png");
            EditorUtility.SetDirty(bronzeRaiderDef);
        }
        var arrow = ArrowSprite();
        var crew = BuildOperator(TowerArcherStyle);
        var watchtower = database != null ? database.GetBuilding("Watchtower") : null;
        if (watchtower != null)
        {
            watchtower.towerProjectile = arrow;
            watchtower.operatorIdle = crew.idle;
            watchtower.operatorShoot = crew.shoot;
            watchtower.operatorReleaseFrame = 2;
            EditorUtility.SetDirty(watchtower);
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
        public Weapon weapon = Weapon.Club;
        public bool helmet;    // bronze helmet
        public bool shield;    // round bronze shield
        public Color crest = new Color(0, 0, 0, 0);
    }

    private enum Weapon { Club, Spear, Bow }

    private static readonly WarriorStyle SpearmanStyle = new WarriorStyle
    {
        name = "Spearman", ink = new Color(0.08f, 0.07f, 0.07f), gear = new Color(0.22f, 0.45f, 0.9f),
        weapon = Weapon.Spear, helmet = true, shield = true, crest = new Color(0.22f, 0.45f, 0.9f)
    };

    private static readonly WarriorStyle BronzeRaiderStyle = new WarriorStyle
    {
        name = "BronzeRaider", ink = new Color(0.78f, 0.13f, 0.12f), gear = new Color(0.18f, 0.12f, 0.08f),
        weapon = Weapon.Spear, helmet = true, shield = true, necklace = true, crest = new Color(0.15f, 0.1f, 0.08f)
    };

    private static readonly WarriorStyle TowerArcherStyle = new WarriorStyle
    {
        name = "TowerArcher", ink = new Color(0.08f, 0.07f, 0.07f), gear = new Color(0.22f, 0.45f, 0.9f), weapon = Weapon.Bow
    };

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
        // A shield stays braced in front of the chest, so it never covers the face mid-thrust.
        if (s.shield && pose != WarPose.Down && pose != WarPose.Falling)
        {
            handL = new Vector2(Mathf.Clamp(handL.x, 0.54f, 0.6f), Mathf.Min(handL.y, 0.5f));
        }
        // Arms: clubs use both pose hands; spears draw their own weapon arm; bows draw both their own.
        if (s.weapon != Weapon.Bow)
        {
            p.Line(R(shoulder), R(handL), limb, s.ink);
        }
        if (s.weapon == Weapon.Club)
        {
            p.Line(R(shoulder), R(handR), limb, s.ink);
        }
        p.Circle(R(head), 0.085f, s.ink);

        // Gear
        p.Line(R(V(0.425f, 0.785f)), R(V(0.575f, 0.785f)), 0.026f, s.gear);          // headband
        p.Line(R(V(0.455f, 0.385f)), R(V(0.545f, 0.385f)), 0.03f, s.gear);           // belt
        p.Line(R(V(0.5f, 0.385f)), R(V(0.53f, 0.31f)), 0.022f, s.gear);              // belt tail
        Vector2 Wrist(Vector2 hand) => Vector2.Lerp(shoulder, hand, 0.82f);
        if (s.weapon == Weapon.Club)
        {
            p.Circle(R(Wrist(handL)), 0.022f, s.gear);                                // wrist wraps
            p.Circle(R(Wrist(handR)), 0.022f, s.gear);
        }
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

        bool dead = pose == WarPose.Down || pose == WarPose.Falling;
        if (s.helmet)
        {
            // Bronze dome helmet with a crest
            var dome = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                dome.Add(R(head + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.1f));
            }
            dome.Add(R(head + V(-0.1f, -0.02f)));
            p.OutlinedPolygon(dome.ToArray(), Bronze2);
            if (s.crest.a > 0)
            {
                p.OutlinedLine(R(head + V(-0.06f, 0.1f)), R(head + V(0.06f, 0.12f)), 0.04f, s.crest);
            }
        }
        if (s.shield && !dead)
        {
            // Bronze Age shield: a tall wooden frame covered in spotted hide, with a rounded top
            // (as carried by Egyptian and Sumerian spearmen), held in front of the body.
            Vector2 c = handL + V(0.03f, 0.02f);
            var outline = new List<Vector2>();
            outline.Add(R(c + V(-0.085f, -0.19f)));
            outline.Add(R(c + V(0.085f, -0.19f)));
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.PI * i / 6f;
                outline.Add(R(c + V(0.085f * Mathf.Cos(a), 0.12f + 0.07f * Mathf.Sin(a))));
            }
            p.OutlinedPolygon(outline.ToArray(), Hide);
            p.Circle(R(c + V(-0.03f, 0.08f)), 0.022f, WoodDark);
            p.Circle(R(c + V(0.035f, -0.03f)), 0.026f, WoodDark);
            p.Circle(R(c + V(-0.02f, -0.12f)), 0.018f, WoodDark);
            p.Line(R(c + V(-0.085f, -0.19f)), R(c + V(-0.085f, 0.12f)), 0.012f, Wood);
            p.Line(R(c + V(0.085f, -0.19f)), R(c + V(0.085f, 0.12f)), 0.012f, Wood);
        }

        if (dead)
        {
            // Dropped weapon on the ground
            if (s.weapon == Weapon.Spear)
            {
                p.OutlinedLine(T(V(0.0f, 0.09f)), T(V(0.55f, 0.11f)), 0.03f, WoodLight);
                p.OutlinedPolygon(new[] { T(V(0.55f, 0.07f)), T(V(0.68f, 0.11f)), T(V(0.55f, 0.15f)) }, Bronze2);
            }
            else if (s.weapon == Weapon.Club)
            {
                p.OutlinedLine(T(V(0.2f, 0.08f)), T(V(0.42f, 0.1f)), 0.035f, WoodLight);
                p.OutlinedLine(T(V(0.38f, 0.1f)), T(V(0.55f, 0.12f)), 0.075f, Wood);
            }
            return;
        }

        if (s.weapon == Weapon.Club)
        {
            // Club: wrapped grip, then the knobbly head
            Vector2 grip = Vector2.Lerp(handR, clubTip, 0.18f);
            p.OutlinedLine(R(handR), R(Vector2.Lerp(handR, clubTip, 0.55f)), 0.035f, WoodLight);
            p.Line(R(Vector2.Lerp(handR, clubTip, -0.04f)), R(grip), 0.045f, s.gear);
            p.OutlinedLine(R(Vector2.Lerp(handR, clubTip, 0.5f)), R(clubTip), 0.075f, Wood);
            p.Circle(R(Vector2.Lerp(handR, clubTip, 0.78f)), 0.012f, WoodDark);
            p.Circle(R(Vector2.Lerp(handR, clubTip, 0.9f)), 0.012f, WoodDark);
        }
        else if (s.weapon == Weapon.Spear)
        {
            // Spear: held level for the thrust, upright when idle or walking
            Vector2 dir;
            switch (pose)
            {
                case WarPose.Windup: dir = V(1f, 0.05f); handR = V(0.4f, 0.55f); break;
                case WarPose.Raised: dir = V(1f, 0.1f); handR = V(0.45f, 0.6f); break;
                case WarPose.Strike: dir = V(1f, -0.05f); handR = V(0.78f, 0.55f); break;
                case WarPose.Recover: dir = V(1f, -0.15f); handR = V(0.65f, 0.5f); break;
                default: dir = V(0.15f, 1f); break;
            }
            dir.Normalize();
            p.Line(R(shoulder), R(handR), limb, s.ink); // the arm reaching the spear
            Vector2 butt = handR - dir * 0.3f, tip = handR + dir * 0.55f;
            p.OutlinedLine(R(butt), R(tip), 0.03f, WoodLight);
            Vector2 side = new Vector2(-dir.y, dir.x);
            p.OutlinedPolygon(new[] { R(tip - side * 0.05f), R(tip + dir * 0.14f), R(tip + side * 0.05f) }, Bronze2);
            p.Circle(R(handR), 0.025f, s.gear);
        }
        else
        {
            // Bow (tower crews): aim forward, string drawn back on the draw frame
            Vector2 bowHand, stringHand;
            bool drawn = false, released = false;
            switch (pose)
            {
                case WarPose.Windup: bowHand = V(0.68f, 0.6f); stringHand = V(0.6f, 0.62f); break;
                case WarPose.Raised: bowHand = V(0.74f, 0.64f); stringHand = V(0.48f, 0.68f); drawn = true; break;
                case WarPose.Strike: bowHand = V(0.74f, 0.64f); stringHand = V(0.42f, 0.7f); released = true; break;
                case WarPose.Recover: bowHand = V(0.7f, 0.6f); stringHand = V(0.5f, 0.58f); break;
                default: bowHand = V(0.62f, 0.5f); stringHand = V(0.4f, 0.45f); break;
            }
            p.Line(R(shoulder), R(bowHand), limb, s.ink);
            p.Line(R(shoulder), R(stringHand), limb, s.ink);
            Vector2 c = bowHand + V(-0.2f, 0);
            Vector2 top = c + V(0.12f, 0.2f), bottom = c + V(0.12f, -0.2f);
            Vector2 cw = R(c);
            p.Draw(q => Mathf.Max(Mathf.Abs(SdCircle(q, cw, 0.23f)) - 0.016f, cw.x + 0.1f - q.x),
                new Rect(cw.x - 0.3f, cw.y - 0.3f, 0.6f, 0.6f), WoodDark);
            Vector2 nock = drawn ? stringHand : V(top.x, (top.y + bottom.y) / 2);
            p.Line(R(top), R(nock), 0.008f, new Color(0.9f, 0.88f, 0.8f));
            p.Line(R(nock), R(bottom), 0.008f, new Color(0.9f, 0.88f, 0.8f));
            if (!released)
            {
                Vector2 from = drawn ? stringHand : nock;
                p.Line(R(from), R(from + V(0.38f, 0)), 0.014f, WoodLight);
                p.OutlinedPolygon(new[] { R(from + V(0.38f, -0.025f)), R(from + V(0.45f, 0)), R(from + V(0.38f, 0.025f)) }, Bronze2);
            }
            // Quiver on the back
            p.OutlinedBox(0.36f + shift.x, 0.48f, 0.42f + shift.x, 0.66f, 0.01f, Hide);
        }
    }

    /// <summary>Height (sprite units) where tower crew sprites are cut off, just under the hip.</summary>
    private const float OperatorCut = 0.36f;

    /// <summary>Frames for a tower crew member (idle + shoot), for a building to use.</summary>
    private static (Sprite idle, List<Sprite> shoot) BuildOperator(WarriorStyle style)
    {
        string dir = $"Units/{style.name}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        Sprite Frame(string frame, WarPose pose)
        {
            var p = new Painter(1.6f, 1.2f, 160) { Outline = 0.018f };
            DrawWarrior(p, pose, style);
            // Crews stand behind a parapet: keep the waist up. The game draws them in front of the
            // tower with this cut line on the parapet top, so roofs and posts can't hide them.
            p.ClearBelow(OperatorCut);
            return Save(p, $"{dir}/{style.name}_{frame}.png", 160, new Vector2(0.5f, 0.05f));
        }
        var idle = Frame("Idle", WarPose.Idle);
        var shoot = new List<Sprite> { Frame("Shoot0", WarPose.Windup), Frame("Shoot1", WarPose.Raised), Frame("Shoot2", WarPose.Strike), Frame("Shoot3", WarPose.Recover) };
        return (idle, shoot);
    }

    private static Sprite ArrowSprite()
    {
        var p = new Painter(0.6f, 0.14f, 160) { Outline = 0.012f };
        p.Line(V(0.08f, 0.07f), V(0.48f, 0.07f), 0.016f, WoodLight);
        p.OutlinedPolygon(new[] { V(0.48f, 0.035f), V(0.58f, 0.07f), V(0.48f, 0.105f) }, Bronze2);
        p.Line(V(0.04f, 0.04f), V(0.14f, 0.07f), 0.02f, Color.white);
        p.Line(V(0.04f, 0.1f), V(0.14f, 0.07f), 0.02f, Color.white);
        return Save(p, "Units/Arrow.png", 160, new Vector2(0.5f, 0.5f));
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
