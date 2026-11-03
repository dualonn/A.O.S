using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Hero quests: the lairs quest bosses sleep in (one look for each kind of story), the banner and
/// pennant that mark objectives, the quest icons, the named quest bosses themselves, and the boons
/// quests give the realm. Makes or updates only these (safe to run again); nothing else is redrawn.
/// (Tools > Age of Stickman > Setup Quests.)
/// </summary>
public static partial class PlaceholderArt
{
    private struct QuestBossSpec
    {
        public string id, name;
        public FigureStyle style;
        public int health, attack;
        public float speed, cooldown, range;
        public BossAbility ability;
    }

    private static QuestBossSpec[] QuestBossSpecs => new[]
    {
        new QuestBossSpec { id = "QuestSpiritWolf", name = "Pale Wolf-Spirit", health = 900, attack = 30, speed = 1.3f, cooldown = 0.9f, range = 0.8f, ability = BossAbility.WarCry,
            style = new FigureStyle { team = "Moonstone", beast = Beast.Wolf, weapon = GearLook.None, scale = 2.2f } },
        new QuestBossSpec { id = "QuestBroodMother", name = "Brood Mother", health = 1000, attack = 32, speed = 1f, cooldown = 1.1f, range = 0.9f, ability = BossAbility.Slam,
            style = new FigureStyle { team = "Purple", beast = Beast.Spider, weapon = GearLook.None, scale = 2.1f } },
        new QuestBossSpec { id = "QuestBarrowLord", name = "Barrow Lord", health = 1100, attack = 34, speed = 0.8f, cooldown = 1.1f, range = 0.9f, ability = BossAbility.Slam,
            style = Lich(Fig("Cloth", GearLook.Crown, GearLook.Sword, GearLook.KiteShield, GearLook.Mail, cape: "Purple", crest: "Gold", shieldColor: "Iron", scale: 1.8f, raider: true)) },
        new QuestBossSpec { id = "QuestAshWyrm", name = "Ash Wyrm", health = 1300, attack = 38, speed = 0.9f, cooldown = 1.2f, range = 1f, ability = BossAbility.Slam,
            style = Lich(Fig("Drake", GearLook.GreatHelm, GearLook.Runeblade, body: GearLook.Plate, mount: MountKind.Drake, crest: "Red", cape: "Red", scale: 1.7f, raider: true)) },
        new QuestBossSpec { id = "QuestDrownedCaptain", name = "Drowned Captain", health = 1000, attack = 34, speed = 0.85f, cooldown = 1.1f, range = 0.9f, ability = BossAbility.WarCry,
            style = Skin(Fig("Whale", GearLook.Morion, GearLook.Zweihander, body: GearLook.Cuirass, cape: "Pearl", scale: 1.7f, raider: true), "Reefskin") },
        new QuestBossSpec { id = "QuestIronMagnate", name = "Iron Magnate", health = 1100, attack = 36, speed = 0.8f, cooldown = 1.3f, range = 0.95f, ability = BossAbility.Slam,
            style = Fig("Coal", GearLook.TopHat, GearLook.PistonHammer, body: GearLook.BrassPlate, crest: "Gold", cape: "Red", scale: 1.75f, raider: true) },
        new QuestBossSpec { id = "QuestRogueMachine", name = "Rogue Machine", health = 1200, attack = 36, speed = 0.8f, cooldown = 1.2f, range = 0.95f, ability = BossAbility.Slam,
            style = Fig("Steel", GearLook.RobotHead, GearLook.Claw, body: GearLook.ChromePlate, crest: "Red", scale: 1.8f, raider: true) },
        new QuestBossSpec { id = "QuestRiftHorror", name = "Rift Horror", health = 1250, attack = 38, speed = 0.9f, cooldown = 1.1f, range = 1f, ability = BossAbility.Slam,
            style = Skin(Fig("Rift", GearLook.RiftMask, GearLook.PhaseBlade, body: GearLook.PrismBody, crest: "Void", cape: "Void", scale: 1.9f, raider: true), "Shardskin") },
        new QuestBossSpec { id = "QuestDerelictWarden", name = "Derelict Warden", health = 1250, attack = 38, speed = 0.8f, cooldown = 1.2f, range = 1f, ability = BossAbility.Slam,
            style = Fig("Starmetal", GearLook.VoidHelm, GearLook.StarBlade, body: GearLook.VoidPlate, crest: "Astral", scale: 1.85f, raider: true) },
        new QuestBossSpec { id = "QuestPrecursorWarden", name = "Precursor Warden", health = 1300, attack = 40, speed = 0.8f, cooldown = 1.2f, range = 1f, ability = BossAbility.Slam,
            style = Fig("Prism", GearLook.HaloHead, GearLook.Worldblade, body: GearLook.GenesisPlate, crest: "Gold", cape: "Primordium", scale: 1.95f, raider: true) },
        new QuestBossSpec { id = "QuestBanditKing", name = "Outlaw King", health = 950, attack = 34, speed = 0.95f, cooldown = 1f, range = 0.9f, ability = BossAbility.WarCry,
            style = Fig("Red", GearLook.Hood, GearLook.Axe, GearLook.RoundShield, GearLook.Jerkin, cape: "Leather", shieldColor: "Wood", scale: 1.6f, raider: true, warpaint: true) },
    };

    private struct QuestBoonSpec
    {
        public string id, name, desc;
        public System.Action<Technology, GameDatabase> apply;
    }

    private static QuestBoonSpec[] QuestBoonSpecs => new[]
    {
        new QuestBoonSpec { id = "QuestBoonSpiritKin", name = "Kin of the Pale Wolf", desc = "All your troops have 5% more health.",
            apply = (t, db) => t.unitModifiers.Add(new UnitStatModifier { healthMultiplier = 1.05f }) },
        new QuestBoonSpec { id = "QuestBoonBarrowPeace", name = "The Barrow's Peace", desc = "Your buildings have 8% more health.",
            apply = (t, db) => t.buildingHealthMultiplier = 1.08f },
        new QuestBoonSpec { id = "QuestBoonWyrmGold", name = "The Wyrm's Hoard", desc = "Gold comes 10% faster.",
            apply = (t, db) => QuestProd(t, db, "Gold", 1.1f) },
        new QuestBoonSpec { id = "QuestBoonCartographers", name = "Varro's Charts", desc = "Research goes 5% faster.",
            apply = (t, db) => t.researchSpeedMultiplier = 1.05f },
        new QuestBoonSpec { id = "QuestBoonTradewind", name = "Honest Ledgers", desc = "Every person pays a little more Coin in taxes.",
            apply = (t, db) => t.coinPerPersonPerSecond = 0.002f },
        new QuestBoonSpec { id = "QuestBoonWarbanner", name = "Halcyon's Lullaby", desc = "All your troops hit 5% harder.",
            apply = (t, db) => t.unitModifiers.Add(new UnitStatModifier { attackMultiplier = 1.05f }) },
        new QuestBoonSpec { id = "QuestBoonRiftWard", name = "The Rift Ward", desc = "Buildings 6% tougher, troops 3% tougher.",
            apply = (t, db) => { t.buildingHealthMultiplier = 1.06f; t.unitModifiers.Add(new UnitStatModifier { healthMultiplier = 1.03f }); } },
        new QuestBoonSpec { id = "QuestBoonStarCharts", name = "The Keeper's Map", desc = "Ships reach 2 light-years further and fly a little faster.",
            apply = (t, db) => { t.navigationRange = 2f; t.driveSpeed = 0.1f; } },
        new QuestBoonSpec { id = "QuestBoonPrecursorCodex", name = "The Precursor Codex", desc = "Research goes 6% faster.",
            apply = (t, db) => t.researchSpeedMultiplier = 1.06f },
        new QuestBoonSpec { id = "QuestBoonHuntersMoon", name = "Hunter's Moon", desc = "Food comes 8% faster.",
            apply = (t, db) => QuestProd(t, db, "Food", 1.08f) },
        new QuestBoonSpec { id = "QuestBoonTimberSong", name = "The Timber Song", desc = "Wood comes 8% faster.",
            apply = (t, db) => QuestProd(t, db, "Wood", 1.08f) },
        new QuestBoonSpec { id = "QuestBoonStoneOath", name = "The Stone Oath", desc = "Stone comes 8% faster.",
            apply = (t, db) => QuestProd(t, db, "Stone", 1.08f) },
        new QuestBoonSpec { id = "QuestBoonFleetfoot", name = "Fleet of Foot", desc = "All your troops move 4% faster.",
            apply = (t, db) => t.unitModifiers.Add(new UnitStatModifier { speedMultiplier = 1.04f }) },
        new QuestBoonSpec { id = "QuestBoonDeepForge", name = "The Deep Forge", desc = "Iron comes 8% faster.",
            apply = (t, db) => QuestProd(t, db, "Iron", 1.08f) },
    };

    private static void QuestProd(Technology t, GameDatabase db, string id, float m)
    {
        var r = db.resources.Find(x => x != null && x.id == id);
        if (r != null) t.productionModifiers.Add(new ProductionModifier { resource = r, multiplier = m });
    }

    private static readonly (string id, System.Action<PixelCanvas> draw)[] QuestLairs =
    {
        ("spirit", QlSpirit), ("wyrm", QlWyrm), ("barrow", QlBarrow), ("beast", QlBeast), ("expedition", QlExpedition),
        ("conspiracy", QlConspiracy), ("machine", QlMachine), ("rift", QlRift), ("derelict", QlDerelict),
        ("precursor", QlPrecursor), ("bandit", QlBandit),
    };

    [MenuItem("Tools/Age of Stickman/Setup Quests")]
    public static void SetupQuests()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Setup Quests: stop playing first."); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        const string path = "Assets/Resources/QuestArt.asset";
        var art = AssetDatabase.LoadAssetAtPath<QuestArt>(path);
        if (art == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            art = ScriptableObject.CreateInstance<QuestArt>();
            AssetDatabase.CreateAsset(art, path);
        }

        // Lairs, 4 tiles wide, standing on their bottom middle.
        art.lairIds = new List<string>();
        art.lairs = new List<Sprite>();
        foreach (var (id, draw) in QuestLairs)
        {
            var c = new PixelCanvas(128, 104);
            draw(c);
            c.Outline(P.Ink, 0.7f);
            art.lairIds.Add(id);
            art.lairs.Add(SavePixel(c, $"Quests/Lair_{id}.png", PixelsPerTile, new Vector2(0.5f, 0.12f)));
        }
        var banner = new PixelCanvas(28, 64);
        QlBanner(banner);
        banner.Outline(P.Ink, 0.7f);
        art.banner = SavePixel(banner, "Quests/Banner.png", PixelsPerTile, new Vector2(0.5f, 0.05f));
        var pennant = new PixelCanvas(24, 30);
        QlPennant(pennant);
        pennant.Outline(P.Ink, 0.7f);
        art.pennant = SavePixel(pennant, "Quests/Pennant.png", PixelsPerTile, new Vector2(0.5f, 0.05f));
        art.icon = IconPixel("Quests/Icon_Quest.png", QiQuest);
        art.taleIcon = IconPixel("Quests/Icon_Tale.png", QiTale);

        // The named bosses (their art lives in Units/<id> like every unit's).
        var stoneAge = db.ages.Count > 0 ? db.ages[0] : null;
        art.bosses = new List<UnitDefinition>();
        foreach (var spec in QuestBossSpecs)
        {
            string defPath = $"Assets/Data/Units/{spec.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
            }
            def.displayName = spec.name;
            def.age = stoneAge;
            def.troopClass = stoneAge != null && stoneAge.enemyUnits.Count > 0 && stoneAge.enemyUnits[0] != null ? stoneAge.enemyUnits[0].troopClass : null;
            def.maxHealth = spec.health;
            def.attackPower = spec.attack;
            def.moveSpeed = spec.speed;
            def.attackCooldown = spec.cooldown;
            def.attackRange = spec.range;
            def.detectionRange = 5f;
            def.hitDelay = 0.3f;
            def.projectile = null;
            def.isBoss = true;
            def.isNative = false;
            def.bossAbility = spec.ability;
            def.abilityCooldown = spec.ability == BossAbility.WarCry ? 11f : 7f;
            def.abilityRadius = 2.4f;
            def.abilityPower = 1.3f;
            def.summons = null;
            def.summonCount = 0;
            SetFlight(def, spec.style.weapon);
            def.prefab = BuildPixelUnit(spec.id, spec.style, true, def, boss: true);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{spec.id}/{spec.id}_Idle.png");
            EditorUtility.SetDirty(def);
            art.bosses.Add(def);   // (not in any age's chiefs: they only wake for quests)
        }

        // Boons: technologies outside the research tree, kept with the wonders' effects so research lookups find them.
        Directory.CreateDirectory("Assets/Data/Quests");
        if (db.wonderEffects == null) db.wonderEffects = new List<Technology>();
        art.boons = new List<Technology>();
        foreach (var spec in QuestBoonSpecs)
        {
            string tPath = $"Assets/Data/Quests/{spec.id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Technology>(tPath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Technology>();
                AssetDatabase.CreateAsset(t, tPath);
            }
            t.id = spec.id;
            t.displayName = spec.name;
            t.description = spec.desc;
            t.age = null;
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.researchSpeedMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.navigationRange = 0f;
            t.driveSpeed = 0f;
            t.maxLevel = 1;
            spec.apply(t, db);
            EditorUtility.SetDirty(t);
            if (!db.wonderEffects.Contains(t)) db.wonderEffects.Add(t);
            art.boons.Add(t);
        }
        EditorUtility.SetDirty(db);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log($"Quests set up: {art.lairs.Count} lairs, {art.bosses.Count} bosses, {art.boons.Count} boons.");
    }

    // ---------- Lairs (128 x 104; the footprint's middle is x 64, the ground line y ~12) ----------

    private static Vector2 Qv(float x, float y) => new Vector2(x, y);

    private static void QlGround(PixelCanvas c, Color32[] ramp, float rx = 52, float ry = 11)
    {
        c.Ellipse(Qv(64, 13), rx, ry, ramp);
        var rng = new System.Random(ramp[1].r * 31 + ramp[2].g);
        for (int i = 0; i < 40; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * 0.85f;
            c.Put(64 + Mathf.Cos(a) * rx * r, 13 + Mathf.Sin(a) * ry * r, ramp[rng.Next(4)]);
        }
    }

    private static void QlStone(PixelCanvas c, float x, float y, float w, float h, Color32[] ramp, Color32 rune)
    {
        c.Polygon(new[] { Qv(x - w, y), Qv(x + w, y), Qv(x + w * 0.8f, y + h), Qv(x - w * 0.6f, y + h + 2) }, l => PixelCanvas.Shade(ramp, (l.x - x) / w * -0.6f + 0.2f, 0.3f));
        c.Line(Qv(x - w + 1, y + 1), Qv(x - w * 0.6f + 1, y + h + 1), ramp[3]);
        c.Put(x, y + h * 0.5f, rune); c.Put(x, y + h * 0.5f + 1, rune); c.Put(x - 1, y + h * 0.6f + 2, rune);
    }

    private static void QlBones(PixelCanvas c, float x, float y)
    {
        c.Line(Qv(x - 4, y), Qv(x + 4, y + 1), P.Bone[2], 2);
        c.Ellipse(Qv(x + 6, y + 2), 2.5f, 2f, P.Bone);
        c.Put(x + 6, y + 2, P.Ink); c.Put(x + 7, y + 2, P.Ink);
    }

    private static void QlSpirit(PixelCanvas c)
    {
        QlGround(c, P.Grass);
        c.Ellipse(Qv(64, 15), 22, 6, P.Moonstone);   // the pale glow on the ground
        float[] xs = { 30, 46, 82, 98, 64 };
        float[] ys = { 14, 22, 22, 14, 26 };
        for (int i = 0; i < xs.Length; i++) QlStone(c, xs[i], ys[i], 5, 20 + (i % 2) * 6, P.Stone, P.Moonstone[3]);
        // an antlered skull on a pole
        c.Line(Qv(64, 12), Qv(64, 50), P.Wood[1], 2);
        c.Ellipse(Qv(64, 52), 4, 3.5f, P.Bone);
        c.Line(Qv(61, 54), Qv(54, 62), P.Bone[2], 2); c.Line(Qv(57, 58), Qv(54, 56), P.Bone[2]);
        c.Line(Qv(67, 54), Qv(74, 62), P.Bone[2], 2); c.Line(Qv(71, 58), Qv(74, 56), P.Bone[2]);
        c.Put(63, 52, P.Moonstone[3]); c.Put(65, 52, P.Moonstone[3]);
        for (int i = 0; i < 6; i++) c.Put(50 + i * 6, 34 + (i % 3) * 7, P.Moonstone[3]);   // drifting motes
    }

    private static void QlWyrm(PixelCanvas c)
    {
        QlGround(c, P.Coal);
        // the crag, with the cave mouth
        c.Polygon(new[] { Qv(14, 12), Qv(30, 52), Qv(48, 72), Qv(70, 84), Qv(92, 66), Qv(114, 12) }, l => PixelCanvas.Shade(P.Rock, (64 - l.x) / 50f * 0.6f + (l.y - 40) / 60f, 0.35f));
        c.Ellipse(Qv(66, 22), 20, 15, P.Coal, l => l.y >= 12);
        c.Ellipse(Qv(66, 18), 13, 8, new[] { P.Red[0], P.Red[1], P.Red[2], P.Gold[2] }, l => l.y >= 12);   // embers inside
        for (int i = 0; i < 9; i++) c.Put(56 + i * 2.3f, 14 + (i % 3), P.Gold[2 + i % 2]);   // the hoard's glint
        QlBones(c, 26, 10); QlBones(c, 96, 12);
        // scorch streaks and smoke
        c.Line(Qv(40, 30), Qv(52, 60), P.Coal[0], 2); c.Line(Qv(84, 28), Qv(90, 52), P.Coal[0], 2);
        c.Ellipse(Qv(74, 92), 7, 5, P.Cloth); c.Ellipse(Qv(84, 98), 5, 4, P.Cloth);
    }

    private static void QlBarrow(PixelCanvas c)
    {
        QlGround(c, P.Grass);
        c.Ellipse(Qv(64, 14), 50, 30, P.Grass, l => l.y >= 12);
        c.Ellipse(Qv(64, 14), 44, 24, P.Leaf, l => l.y >= 30);
        // the stone door and lintel
        c.Rect(54, 12, 74, 34, P.Stone[1]);
        c.Rect(57, 12, 71, 30, P.Coal[0]);
        c.Rect(51, 34, 77, 38, P.Stone[2]);
        c.Line(Qv(51, 38), Qv(77, 38), P.Stone[3]);
        QlStone(c, 46, 12, 3, 18, P.Stone, P.Purple[3]);
        QlStone(c, 82, 12, 3, 18, P.Stone, P.Purple[3]);
        c.Put(62, 22, P.Purple[3]); c.Put(66, 22, P.Purple[3]);   // eyes in the dark
        // crows on the mound
        c.Ellipse(Qv(38, 40), 2.5f, 2, P.Coal); c.Ellipse(Qv(92, 36), 2.5f, 2, P.Coal);
    }

    private static void QlBeast(PixelCanvas c)
    {
        QlGround(c, P.Dirt);
        c.Polygon(new[] { Qv(16, 12), Qv(26, 46), Qv(50, 62), Qv(80, 60), Qv(104, 40), Qv(112, 12) }, l => PixelCanvas.Shade(P.Rock, (64 - l.x) / 50f * 0.6f + (l.y - 30) / 50f, 0.35f));
        c.Ellipse(Qv(64, 18), 20, 16, P.Coal, l => l.y >= 12);
        // webs across the mouth, claw marks, bones
        c.Line(Qv(46, 30), Qv(82, 18), P.Linen[2]); c.Line(Qv(48, 18), Qv(80, 30), P.Linen[2]); c.Line(Qv(64, 14), Qv(64, 33), P.Linen[1]);
        for (int i = 0; i < 3; i++) c.Line(Qv(90 + i * 3, 30), Qv(94 + i * 3, 42), P.Rock[0]);
        QlBones(c, 30, 9); QlBones(c, 84, 8); QlBones(c, 58, 6);
        c.Put(58, 22, P.Red[3]); c.Put(61, 22, P.Red[3]); c.Put(68, 24, P.Red[3]); c.Put(71, 24, P.Red[3]);
    }

    private static void QlExpedition(PixelCanvas c)
    {
        QlGround(c, P.Sand);
        // a slashed tent
        c.Polygon(new[] { Qv(26, 12), Qv(48, 46), Qv(70, 12) }, l => PixelCanvas.Shade(P.Linen, (48 - l.x) / 22f * 0.7f, 0.3f));
        c.Polygon(new[] { Qv(42, 12), Qv(48, 26), Qv(54, 12) }, P.Coal[0]);
        c.Line(Qv(56, 30), Qv(62, 20), P.Linen[0]);
        // broken crates and a cold fire
        c.Rect(78, 12, 92, 24, P.Wood[2]); c.Line(Qv(78, 18), Qv(92, 18), P.Wood[0]); c.Line(Qv(80, 24), Qv(90, 13), P.Wood[1]);
        c.Rect(94, 12, 104, 19, P.Wood[1]);
        c.Ellipse(Qv(68, 10), 5, 2, P.Coal);
        // the tattered flag
        c.Line(Qv(108, 12), Qv(108, 64), P.Wood[1], 2);
        c.Polygon(new[] { Qv(110, 64), Qv(126, 60), Qv(118, 56), Qv(124, 52), Qv(110, 50) }, P.Blue[2]);
        c.Line(Qv(110, 57), Qv(120, 57), P.Gold[2]);
    }

    private static void QlConspiracy(PixelCanvas c)
    {
        QlGround(c, P.Stone);
        // a brick warehouse front
        c.Rect(28, 12, 100, 62, P.Red[1]);
        for (int y = 14; y < 62; y += 4)
            for (int x = 28 + (y / 4 % 2) * 3; x < 100; x += 7) c.Put(x, y, P.Red[0]);
        c.Polygon(new[] { Qv(24, 62), Qv(64, 80), Qv(104, 62) }, l => PixelCanvas.Shade(P.Coal, (64 - l.x) / 40f * 0.5f, 0.3f));
        c.Rect(50, 12, 78, 40, P.Wood[0]);
        c.Line(Qv(64, 12), Qv(64, 40), P.Wood[1]);
        c.Rect(36, 46, 46, 54, P.Gold[2]); c.Rect(82, 46, 92, 54, P.Coal[0]);   // one window lit
        // the circle-and-hand sign, crates, a lamp
        c.Ellipse(Qv(64, 52), 5, 5, P.Brass); c.Ellipse(Qv(64, 52), 2.5f, 2.5f, P.Coal);
        c.Rect(102, 12, 114, 22, P.Wood[2]); c.Rect(104, 22, 112, 30, P.Wood[1]);
        c.Line(Qv(18, 12), Qv(18, 48), P.Iron[1], 2); c.Ellipse(Qv(18, 50), 3, 3, P.Glow);
    }

    private static void QlMachine(PixelCanvas c)
    {
        QlGround(c, P.Rock);
        // a lattice mast with a dish
        for (int y = 12; y < 86; y += 8)
        {
            float w = 16 - (y - 12) * 0.16f;
            c.Line(Qv(64 - w, y), Qv(64 + w, y + 8), P.Steel[1]);
            c.Line(Qv(64 + w, y), Qv(64 - w, y + 8), P.Steel[1]);
        }
        c.Line(Qv(48, 12), Qv(62, 90), P.Steel[2], 2); c.Line(Qv(80, 12), Qv(66, 90), P.Steel[2], 2);
        c.Ellipse(Qv(76, 82), 10, 6, P.Chrome);
        c.Line(Qv(76, 82), Qv(84, 90), P.Steel[3]);
        c.Ellipse(Qv(64, 94), 3, 3, new[] { P.Red[1], P.Red[2], P.Red[3], P.White });   // the warning light
        // a hut with a broken door, cables, sparks
        c.Rect(84, 12, 110, 30, P.Steel[1]); c.Rect(90, 12, 98, 24, P.Coal[0]);
        c.Line(Qv(64, 12), Qv(30, 14), P.Coal[1], 2); c.Line(Qv(30, 14), Qv(20, 20), P.Coal[1], 2);
        c.Put(20, 22, P.Ray[3]); c.Put(22, 24, P.Ray[2]); c.Put(18, 23, P.Ray[3]); c.Put(56, 62, P.Ray[3]); c.Put(72, 40, P.Ray[3]);
    }

    private static void QlRift(PixelCanvas c)
    {
        QlGround(c, P.Coal);
        // the tear itself: jagged, violet, deep
        var tear = new[] { Qv(64, 14), Qv(56, 30), Qv(60, 42), Qv(52, 58), Qv(60, 74), Qv(64, 92), Qv(70, 72), Qv(76, 58), Qv(68, 44), Qv(74, 30) };
        c.Polygon(tear, l => PixelCanvas.Shade(P.Rift, -Mathf.Abs(l.x - 64) / 10f + 0.6f, 0.2f));
        c.Polygon(new[] { Qv(64, 24), Qv(61, 40), Qv(64, 70), Qv(67, 42) }, P.Void[0]);
        // crystal shards round it
        foreach (var (x, h) in new[] { (34f, 22f), (44f, 14f), (88f, 18f), (98f, 26f), (22f, 12f), (108f, 12f) })
            c.Polygon(new[] { Qv(x - 4, 12), Qv(x, 12 + h), Qv(x + 4, 12) }, l => PixelCanvas.Shade(P.Riftstone, (x - l.x) / 4f * 0.6f, 0.3f));
        for (int i = 0; i < 8; i++) c.Put(40 + i * 7, 60 + (i % 3) * 9, P.Rift[3]);
    }

    private static void QlDerelict(PixelCanvas c)
    {
        QlGround(c, P.Dirt);
        // a hull half in the ground, nose up
        c.Polygon(new[] { Qv(14, 12), Qv(30, 46), Qv(92, 70), Qv(112, 58), Qv(106, 12) }, l => PixelCanvas.Shade(P.Starmetal, (l.y - 30) / 40f + (64 - l.x) / 80f, 0.3f));
        c.Line(Qv(30, 36), Qv(98, 58), P.Steel[0]);
        c.Line(Qv(40, 24), Qv(104, 40), P.Steel[0]);
        for (int i = 0; i < 5; i++) c.Ellipse(Qv(46 + i * 12, 34 + i * 4), 2, 2, P.Glow);   // portholes, some lit
        c.Polygon(new[] { Qv(70, 62), Qv(78, 88), Qv(86, 66) }, P.Steel[1]);   // the broken fin
        c.Polygon(new[] { Qv(50, 12), Qv(58, 24), Qv(74, 24), Qv(80, 12) }, P.Coal[0]);   // open hatch
        c.Ellipse(Qv(112, 82), 6, 5, P.Cloth); c.Ellipse(Qv(118, 90), 4, 3, P.Cloth);   // smoke
    }

    private static void QlPrecursor(PixelCanvas c)
    {
        QlGround(c, P.Sand);
        c.Rect(40, 12, 88, 16, P.Marble[1]); c.Rect(44, 16, 84, 19, P.Marble[2]);   // the plinth
        c.Polygon(new[] { Qv(54, 19), Qv(58, 86), Qv(64, 96), Qv(70, 86), Qv(74, 19) }, l => PixelCanvas.Shade(P.Prism, (64 - l.x) / 10f * 0.6f + 0.1f, 0.3f));
        for (int y = 26; y < 82; y += 9) { c.Put(63, y, P.Gold[3]); c.Put(64, y + 2, P.Gold[3]); c.Put(65, y + 4, P.Gold[2]); }   // glyphs
        // the floating ring and the stones that answer
        c.Ellipse(Qv(64, 70), 20, 4, P.Gold, l => Mathf.Pow((l.x - 64) / 20f, 2) + Mathf.Pow((l.y - 70) / 4f, 2) > 0.45f);
        QlStone(c, 26, 12, 4, 14, P.Marble, P.Gold[3]);
        QlStone(c, 102, 12, 4, 14, P.Marble, P.Gold[3]);
    }

    private static void QlBandit(PixelCanvas c)
    {
        QlGround(c, P.Dirt);
        // a ring of sharpened stakes, a tent and the skull flag
        for (int x = 16; x <= 112; x += 6)
        {
            if (x > 54 && x < 74) continue;   // the gap in the palisade
            int h = 26 + (x * 7 % 5);
            c.Polygon(new[] { Qv(x - 2.5f, 10), Qv(x - 2.5f, 10 + h), Qv(x, 14 + h), Qv(x + 2.5f, 10 + h), Qv(x + 2.5f, 10) }, l => PixelCanvas.Shade(P.Wood, (x - l.x) / 2.5f * 0.6f, 0.3f));
        }
        c.Polygon(new[] { Qv(46, 36), Qv(64, 62), Qv(82, 36) }, l => PixelCanvas.Shade(P.Leather, (64 - l.x) / 18f * 0.6f, 0.3f));
        c.Line(Qv(64, 36), Qv(64, 92), P.Wood[1], 2);
        c.Polygon(new[] { Qv(66, 92), Qv(86, 88), Qv(66, 78) }, P.Red[1]);
        c.Ellipse(Qv(73, 86), 2.5f, 2, P.Bone);
        c.Ellipse(Qv(64, 12), 5, 2.5f, new[] { P.Red[1], P.Red[2], P.Gold[2], P.Gold[3] });   // the campfire
    }

    // ---------- Markers and icons ----------

    private static void QlBanner(PixelCanvas c)
    {
        c.Shadow(14, 3, 8, 2.5f);
        c.Line(Qv(8, 2), Qv(8, 62), P.Wood[1], 2);
        c.Line(Qv(8, 2), Qv(8, 62), P.Wood[2]);
        c.Ellipse(Qv(8.5f, 62), 2, 2, P.Gold);
        c.Polygon(new[] { Qv(10, 60), Qv(27, 56), Qv(22, 48), Qv(27, 40), Qv(10, 38) }, l => PixelCanvas.Shade(P.Gold, (l.y - 50) / 12f, 0.3f));
        c.Line(Qv(10, 58), Qv(24, 55), P.Red[2]); c.Line(Qv(10, 40), Qv(24, 41), P.Red[2]);
        c.Ellipse(Qv(16, 49), 2.5f, 2.5f, P.Red);   // the seal
        c.Rect(4, 2, 12, 4, P.Stone[1]);   // a cairn at its foot
        c.Rect(5, 4, 11, 6, P.Stone[2]);
    }

    private static void QlPennant(PixelCanvas c)
    {
        c.Line(Qv(6, 0), Qv(6, 29), P.Wood[2], 2);
        c.Polygon(new[] { Qv(8, 28), Qv(23, 23), Qv(8, 16) }, l => PixelCanvas.Shade(P.Gold, (l.y - 22) / 8f, 0.3f));
        c.Line(Qv(8, 22), Qv(18, 22), P.Red[2]);
    }

    private static void QiQuest(PixelCanvas c)
    {
        // a rolled scroll with a red seal and a gold mark
        c.Rect(4, 5, 15, 15, P.Linen[2]);
        c.Line(Qv(4, 15), Qv(15, 15), P.Linen[3]);
        c.Ellipse(Qv(4, 10), 1.6f, 5.5f, P.Linen);
        c.Ellipse(Qv(15.5f, 10), 1.6f, 5.5f, P.Linen);
        c.Line(Qv(7, 12), Qv(12, 12), P.Linen[0]); c.Line(Qv(7, 9), Qv(11, 9), P.Linen[0]);
        c.Ellipse(Qv(11, 5), 2.2f, 2.2f, P.Red);
        c.Rect(9, 2, 9, 4, P.Red[1]); c.Rect(13, 2, 13, 4, P.Red[1]);
    }

    private static void QiTale(PixelCanvas c)
    {
        // a closed book with a gold clasp
        c.Rect(4, 3, 15, 16, P.Red[1]);
        c.Rect(5, 4, 14, 15, P.Red[2]);
        c.Rect(15, 4, 16, 15, P.Linen[2]);
        c.Line(Qv(5, 15), Qv(14, 15), P.Red[3]);
        c.Rect(13, 8, 16, 10, P.Gold[2]);
        c.Ellipse(Qv(9.5f, 10), 2, 2, P.Gold);
    }
}
