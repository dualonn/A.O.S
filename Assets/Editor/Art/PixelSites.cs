using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// The wild: site art (ruins, dungeons, lairs, shrines, caches, lore stones), their map icons, the
/// creatures that live there, and the shrine blessings. (Tools > Age of Stickman > Setup Wild Sites.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Wild Sites")]
    public static void SetupWildSites()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        GenerateSiteArt(art);
        SetupCreatures(db);
        SetupBlessings(db);
        EditorUtility.SetDirty(db);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Wild sites set up.");
    }

    // ---------- Site art ----------

    private static void GenerateSiteArt(ArtLibrary art)
    {
        ResetDir("World/Sites");
        art.siteRuins = SitePx("Ruins", 3, k => PxRuins(k, false));
        art.siteRuinsLooted = SitePx("RuinsLooted", 3, k => PxRuins(k, true));
        art.siteDungeon = SitePx("Dungeon", 3, k => PxDungeon(k, false));
        art.siteDungeonSealed = SitePx("DungeonSealed", 3, k => PxDungeon(k, true));
        art.siteLairs = new List<Sprite> { SitePx("WolfDen", 3, PxWolfDen), SitePx("SpiderNest", 3, PxSpiderNest), SitePx("TrollCave", 3, PxTrollCave) };
        art.siteShrine = SitePx("Shrine", 2, k => PxShrine(k, false));
        art.siteShrineUsed = SitePx("ShrineUsed", 2, k => PxShrine(k, true));
        art.siteCache = SitePx("Cache", 1, PxCache);
        art.siteCaravan = SitePx("Caravan", 1, PxCaravan);
        art.siteLoreStone = SitePx("LoreStone", 1, k => PxLoreStone(k, false));
        art.siteLoreStoneRead = SitePx("LoreStoneRead", 1, k => PxLoreStone(k, true));
        art.siteIcons = new List<Sprite>
        {
            IconPixel("World/Sites/Icon_Ruins.png", IcRuins), IconPixel("World/Sites/Icon_Dungeon.png", IcDungeon),
            IconPixel("World/Sites/Icon_Lair.png", IcLair), IconPixel("World/Sites/Icon_Shrine.png", IcShrine),
            IconPixel("World/Sites/Icon_Cache.png", IcCache), IconPixel("World/Sites/Icon_Lore.png", IcLore),
        };
    }

    private static Sprite SitePx(string name, int tiles, System.Action<K> draw)
    {
        int w = tiles * TilePx + MarginSide * 2, h = tiles * TilePx + MarginBottom + MarginTop;
        var c = new PixelCanvas(w, h);
        var k = new K(c, MarginSide, MarginBottom, 0, 0, 1, name.GetHashCode());
        draw(k);
        return SavePixel(c, $"World/Sites/{name}.png", PixelsPerTile, new Vector2(MarginSide / (float)w, MarginBottom / (float)h));
    }

    private static void Column(K k, int x, int y, int h, bool broken)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            k.Px(x, yy, P.Stone[3]); k.Px(x + 1, yy, P.Stone[2]); k.Px(x + 2, yy, P.Stone[2]); k.Px(x + 3, yy, P.Stone[1]);
            if ((yy - y) % 7 == 6) k.HLine(x, x + 3, yy, P.Stone[1]);
        }
        k.HLine(x - 1, x + 4, y - 1, P.Stone[1]);
        if (broken) { k.Px(x + 3, y + h, P.Stone[2]); k.Px(x + 2, y + h, P.Stone[2]); k.Px(x + 1, y + h + 1, P.Stone[3]); }
        else { k.HLine(x - 1, x + 4, y + h, P.Stone[4]); k.HLine(x - 1, x + 4, y + h + 1, P.Stone[2]); }
    }

    private static void Vines(K k, int x, int y, int len)
    {
        for (int i = 0; i < len; i++) k.Px(x + (i % 3 == 0 ? 1 : 0), y - i, i % 2 == 0 ? P.Leaf[3] : P.Leaf[2]);
    }

    private static void Chest(K k, int x, int y, bool open)
    {
        k.Rect(x, y, x + 11, y + 6, P.Wood[2]);
        k.HLine(x, x + 11, y + 6, P.Wood[3]);
        k.VLine(x + 2, y, y + 6, P.Gold[1]); k.VLine(x + 9, y, y + 6, P.Gold[1]);
        if (open)
        {
            k.Rect(x + 1, y + 5, x + 10, y + 6, P.Coal[1]);
            k.Rect(x, y + 7, x + 11, y + 11, P.Wood[1]);   // lid thrown back
            k.HLine(x, x + 11, y + 11, P.Wood[2]);
        }
        else
        {
            k.Rect(x, y + 7, x + 11, y + 9, P.Wood[3]);
            k.HLine(x, x + 11, y + 9, P.Wood[3]);
            k.Rect(x + 5, y + 4, x + 6, y + 7, P.Gold[2]);   // lock
            k.Px(x + 5, y + 8, P.Gold[3]);
        }
    }

    private static void PxRuins(K k, bool looted)
    {
        k.Ground(4, 4, 92, 56, P.Dirt);
        // a broken back wall with an arch
        k.Wall(10, 40, 70, 64, K.Mat.Stone);
        for (int x = 10; x <= 70; x++)
        {
            int top = 64 - Mathf.Abs(Mathf.RoundToInt(Mathf.Sin(x * 0.35f) * 4 + Mathf.Sin(x * 0.9f) * 2));
            for (int y = top + 1; y <= 64; y++) k.Erase(x, y);
        }
        k.Rect(32, 40, 46, 56, P.Coal[1]);
        Vines(k, 14, 60, 12); Vines(k, 60, 58, 10);
        Column(k, 76, 20, 34, false);
        Column(k, 6, 16, 22, true);
        // a fallen column on the ground
        k.Rect(56, 8, 82, 11, P.Stone[2]); k.HLine(56, 82, 11, P.Stone[3]); k.Ellipse(82, 9.5f, 2, 2.4f, P.Stone);
        k.Rocks(18, 8, 3);
        Chest(k, 40, 16, looted);
        if (looted) { k.Px(38, 15, P.Gold[3]); k.Px(54, 16, P.Gold[2]); }
        k.Finish();
    }

    private static void PxDungeon(K k, bool sealedUp)
    {
        k.Ground(10, 2, 86, 20, P.Dirt);
        k.Mound(48, 40, 46, 32);
        // a stone archway into the hill
        for (int y = 16; y <= 50; y++)
            for (int x = 30; x <= 66; x++)
            {
                float dx = (x - 48) / 18f, dy = (y - 30) / 20f;
                bool inArch = y < 30 || dx * dx + dy * dy <= 1;
                float ix = (x - 48) / 12f, iy = (y - 30) / 14f;
                bool inside = (y < 30 && Mathf.Abs(x - 48) <= 12) || (ix * ix + iy * iy <= 1);
                if (!inArch) continue;
                if (inside) k.Px(x, y, sealedUp ? (((x / 4 + y / 3) % 2 == 0) ? P.Stone[2] : P.Stone[1]) : P.Coal[0]);
                else k.Px(x, y, ((x + y / 4 * 3) % 6 == 0 || y % 4 == 0) ? P.Stone[1] : x < 40 ? P.Stone[3] : P.Stone[2]);
            }
        if (!sealedUp)
        {
            k.Px(45, 22, P.Red[3]); k.Px(47, 22, P.Red[3]);   // eyes in the dark
            k.Px(52, 26, P.Red[2]); k.Px(54, 26, P.Red[2]);
            k.Torch(27, 16); k.Torch(68, 16);
        }
        else
        {
            k.Line(36, 20, 60, 40, P.Iron[2]); k.Line(36, 40, 60, 20, P.Iron[2]);   // chains across the door
        }
        // a skull over the keystone
        k.Ellipse(48, 53, 3.4f, 3, P.Bone);
        k.Px(47, 53, P.Coal[1]); k.Px(49, 53, P.Coal[1]);
        k.Finish();
    }

    /// <summary>A dark opening in a hillside: flat black inside, the rock's shadowed lip round the top.</summary>
    private static void CaveMouth(K k, float cx, int bottom, float rx, float h)
    {
        for (int y = bottom; y <= bottom + h; y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= cx + rx; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y - bottom) / h;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                k.Px(x, y, d > 0.8f && dy > 0.3f ? P.Rock[0] : dy < 0.15f ? P.Coal[1] : P.Coal[0]);
            }
    }

    private static void Bones(K k, int x, int y)
    {
        k.Line(x, y, x + 5, y + 1, P.Bone[3]);
        k.Px(x - 1, y, P.Bone[2]); k.Px(x + 6, y + 1, P.Bone[2]);
        k.Ellipse(x + 9, y + 1.5f, 2, 1.8f, P.Bone);
        k.Px(x + 8, y + 1, P.Coal[1]);
    }

    private static void PxWolfDen(K k)
    {
        k.Ground(6, 2, 90, 34, P.Dirt);
        k.Mound(52, 30, 36, 26);
        CaveMouth(k, 50, 6, 13, 13);
        k.Mound(22, 18, 14, 12);
        Bones(k, 66, 6); Bones(k, 14, 4);
        k.Finish();
    }

    private static void PxSpiderNest(K k)
    {
        k.Ground(6, 2, 90, 50, P.Dirt);
        // two dead trees with webs strung between them
        foreach (int x in new[] { 18, 76 })
        {
            k.Rect(x, 4, x + 3, 58, P.Coal[2]);
            k.Line(x + 1, 44, x + (x < 40 ? 10 : -10), 60, P.Coal[2], 2);
            k.Line(x + 1, 30, x + (x < 40 ? -6 : 6), 40, P.Coal[2], 2);
        }
        // a round web: spokes from a hub, threads spiralling out
        var silk = new Color32(236, 236, 240, 190);
        Vector2 hub = new Vector2(48, 36);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2;
            k.Line(hub.x, hub.y, hub.x + Mathf.Cos(a) * 27, hub.y + Mathf.Sin(a) * 22, silk);
        }
        for (int ring = 1; ring <= 4; ring++)
            for (int i = 0; i < 10; i++)
            {
                float a0 = i / 10f * Mathf.PI * 2, a1 = (i + 1) / 10f * Mathf.PI * 2, r = ring * 6.5f;
                k.Line(hub.x + Mathf.Cos(a0) * r, hub.y + Mathf.Sin(a0) * r * 0.8f, hub.x + Mathf.Cos(a1) * r, hub.y + Mathf.Sin(a1) * r * 0.8f, silk);
            }
        for (int i = 0; i < 4; i++) k.Ellipse(30 + i * 11, 6 + (i % 2) * 3, 3, 3.6f, P.Bone);   // egg sacs
        Bones(k, 56, 4);
        k.Finish();
    }

    private static void PxTrollCave(K k)
    {
        k.Ground(4, 2, 92, 30, P.Dirt);
        k.Mound(48, 40, 46, 34);
        CaveMouth(k, 44, 8, 17, 18);
        // a cookpot over a fire, and a big club leaning on the rock
        k.Fire(68, 6, 8, 5);
        k.Ellipse(72, 13, 5, 4, P.Iron);
        k.HLine(67, 77, 16, P.Iron[1]);
        k.Line(10, 6, 20, 30, P.Wood[2], 3);
        k.Ellipse(20, 30, 4, 4, P.Wood);
        Bones(k, 26, 4); Bones(k, 54, 2);
        k.Finish();
    }

    private static void PxShrine(K k, bool used)
    {
        k.Ground(4, 2, 60, 22, P.Stone);
        k.Rect(10, 4, 54, 8, P.Stone[2]); k.HLine(10, 54, 8, P.Stone[4]);
        k.Rect(16, 9, 48, 12, P.Stone[3]); k.HLine(16, 48, 12, P.Stone[4]);
        // twin standing stones with a glowing crystal between them
        foreach (int x in new[] { 16, 42 })
        {
            k.Rect(x, 13, x + 6, 44, P.Stone[2]); k.VLine(x, 13, 44, P.Stone[3]); k.VLine(x + 6, 13, 44, P.Stone[1]);
            k.HLine(x, x + 6, 45, P.Stone[3]);
            for (int y = 18; y < 40; y += 6) k.HLine(x + 2, x + 4, y, used ? P.Stone[1] : P.Water[3]);   // runes
        }
        k.Rect(14, 46, 50, 49, P.Stone[2]); k.HLine(14, 50, 49, P.Stone[4]);   // lintel
        var glow = used ? new[] { P.Stone[1], P.Stone[2], P.Stone[2], P.Stone[3] } : new[] { P.Water[1], P.Water[2], P.Water[3], P.Water[4] };
        k.C.Polygon(new[] { new Vector2(32, 14), new Vector2(37, 26), new Vector2(32, 40), new Vector2(27, 26) },
            l => l.x < 31 ? glow[3] : l.x > 33 ? glow[1] : glow[2]);
        k.Px(30, 30, P.White);
        // candles
        foreach (int x in new[] { 12, 50 }) { k.VLine(x, 9, 12, P.Linen[3]); if (!used) k.Px(x, 13, P.Glow[3]); }
        k.Finish();
        if (!used)
            for (int i = 0; i < 6; i++) k.Px(26 + (i * 7) % 13, 40 + (i * 5) % 9, new Color32(170, 230, 255, 160));   // motes of light
    }

    private static void PxCache(K k)
    {
        k.Ellipse(16, 6, 13, 5, P.Dirt, l => l.y >= 1);
        Chest(k, 10, 4, false);
        k.Line(26, 4, 30, 18, P.Wood[3]); k.Rect(28, 2, 31, 5, P.Steel[2]);   // a shovel left in the dirt
        k.Finish();
    }

    private static void PxCaravan(K k)
    {
        // a broken cart tipped on its side, crates spilled
        k.Rect(2, 6, 26, 14, P.Wood[2]); k.HLine(2, 26, 14, P.Wood[3]); k.HLine(2, 26, 10, P.Wood[1]);
        k.Ellipse(8, 5, 4.5f, 4.5f, P.Wood); k.Px(8, 5, P.Wood[0]);
        k.Line(26, 8, 36, 4, P.Wood[1], 2);
        k.Crate(28, 2); k.Barrel(-2, 2);
        // a torn canopy
        k.C.Polygon(new[] { new Vector2(4, 14), new Vector2(24, 14), new Vector2(20, 22), new Vector2(8, 21) }, l => (int)l.x % 4 == 0 ? P.Linen[1] : P.Linen[2]);
        k.Finish();
    }

    private static void PxLoreStone(K k, bool read)
    {
        k.Ellipse(16, 4, 8, 2.5f, P.Grass, l => true);
        k.C.Polygon(new[] { new Vector2(10, 3), new Vector2(22, 3), new Vector2(21, 26), new Vector2(17, 30), new Vector2(11, 27) },
            l => l.x < 13 ? P.Stone[3] : l.x > 19 ? P.Stone[1] : P.Stone[2]);
        var rune = read ? P.Water[4] : P.Coal[1];
        // angular runes
        k.Line(14, 24, 14, 19, rune); k.Line(14, 24, 17, 22, rune); k.Line(14, 21, 17, 19, rune);
        k.Line(16, 16, 13, 13, rune); k.Line(16, 16, 19, 13, rune); k.Line(16, 16, 16, 11, rune);
        k.Line(13, 8, 16, 6, rune); k.Line(16, 6, 19, 8, rune); k.Px(16, 8, rune);
        Vines(k, 11, 12, 6);
        k.Finish();
    }

    // ---------- Map icons ----------

    private static void IcRuins(PixelCanvas c)
    {
        c.Rect(4, 2, 7, 16, P.Stone[2]); c.Rect(13, 2, 16, 11, P.Stone[2]); c.Rect(3, 16, 8, 17, P.Stone[3]);
        c.Rect(2, 1, 18, 2, P.Stone[1]);
    }

    private static void IcDungeon(PixelCanvas c)
    {
        c.Ellipse(new Vector2(10, 11), 7, 7, P.Bone);
        c.Rect(6, 3, 14, 6, P.Bone[2]);
        c.Rect(6, 9, 8, 12, P.Coal[1]); c.Rect(12, 9, 14, 12, P.Coal[1]);
        c.Put(10, 7, P.Coal[1]);
    }

    private static void IcLair(PixelCanvas c)
    {
        c.Ellipse(new Vector2(10, 6), 5, 4, P.Fur);
        foreach (var (x, y) in new[] { (4, 12), (8, 15), (12, 15), (16, 12) }) c.Ellipse(new Vector2(x, y), 2, 2.2f, P.Fur);
    }

    private static void IcShrine(PixelCanvas c)
    {
        c.Polygon(new[] { new Vector2(10, 2), new Vector2(15, 10), new Vector2(10, 18), new Vector2(5, 10) }, l => l.x < 9 ? P.Water[4] : l.x > 11 ? P.Water[2] : P.Water[3]);
    }

    private static void IcCache(PixelCanvas c)
    {
        c.Rect(3, 4, 16, 11, P.Wood[2]); c.Rect(3, 12, 16, 15, P.Wood[3]);
        c.Rect(6, 4, 6, 15, P.Gold[2]); c.Rect(13, 4, 13, 15, P.Gold[2]); c.Rect(9, 9, 10, 12, P.Gold[3]);
    }

    private static void IcLore(PixelCanvas c)
    {
        c.Polygon(new[] { new Vector2(5, 2), new Vector2(15, 2), new Vector2(14, 16), new Vector2(10, 19), new Vector2(6, 16) }, l => l.x < 8 ? P.Stone[3] : P.Stone[2]);
        c.LineRaw(new Vector2(8, 12), new Vector2(12, 12), P.Water[4]); c.LineRaw(new Vector2(10, 14), new Vector2(10, 7), P.Water[4]);
    }

    // ---------- Creatures ----------

    private struct CreatureSpec
    {
        public string id, name;
        public FigureStyle style;
        public int health, attack;
        public float speed, cooldown, range, detection;
        public bool ranged, boss;
    }

    private static CreatureSpec[] CreatureSpecs => new[]
    {
        new CreatureSpec { id = "Skeleton", name = "Skeleton", health = 120, attack = 14, speed = 0.8f, cooldown = 1.1f, range = 0.6f, detection = 3f,
            style = Fig("Cloth", GearLook.Bare, GearLook.Sword, body: GearLook.Bare, raider: true) },
        new CreatureSpec { id = "SkeletonArcher", name = "Skeleton Archer", health = 90, attack = 10, speed = 0.8f, cooldown = 1.6f, range = 4f, detection = 5f, ranged = true,
            style = Fig("Cloth", GearLook.Bare, GearLook.Bow, body: GearLook.Bare, raider: true) },
        new CreatureSpec { id = "Wolf", name = "Grey Wolf", health = 110, attack = 12, speed = 1.4f, cooldown = 0.9f, range = 0.6f, detection = 4f,
            style = new FigureStyle { team = "Wolf", beast = Beast.Wolf, weapon = GearLook.None } },
        new CreatureSpec { id = "GiantSpider", name = "Giant Spider", health = 140, attack = 16, speed = 1.1f, cooldown = 1.2f, range = 0.7f, detection = 4f,
            style = new FigureStyle { team = "Coal", beast = Beast.Spider, weapon = GearLook.None } },
        new CreatureSpec { id = "Troll", name = "Troll", health = 900, attack = 34, speed = 0.7f, cooldown = 1.6f, range = 0.9f, detection = 4f,
            style = Fig("Leather", GearLook.Bare, GearLook.Club, body: GearLook.Bare, scale: 1.6f, raider: true) },
        new CreatureSpec { id = "BarrowKing", name = "The Barrow King", health = 1800, attack = 36, speed = 0.8f, cooldown = 1.1f, range = 0.8f, detection = 5f, boss = true,
            style = Fig("Purple", GearLook.Crown, GearLook.Sword, GearLook.RoundShield, GearLook.Mail, cape: "Purple", shieldColor: "Iron", scale: 1.7f, raider: true) },
    };

    private static void SetupCreatures(GameDatabase db)
    {
        var arrow = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Arrow.png");
        var stoneAge = db.ages.Count > 0 ? db.ages[0] : null;
        var defs = new Dictionary<string, UnitDefinition>();
        foreach (var spec in CreatureSpecs)
        {
            var style = spec.style;
            if (spec.id.StartsWith("Skeleton") || spec.id == "BarrowKing") { style.skin = "Bone"; style.undead = true; }
            if (spec.id == "Troll") style.skin = "Troll";
            string path = $"Assets/Data/Units/{spec.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.displayName = spec.name;
            def.age = stoneAge;
            def.troopClass = stoneAge != null && stoneAge.enemyUnits.Count > 0 ? stoneAge.enemyUnits[0].troopClass : null;
            def.maxHealth = spec.health;
            def.attackPower = spec.attack;
            def.moveSpeed = spec.speed;
            def.attackCooldown = spec.cooldown;
            def.attackRange = spec.range;
            def.detectionRange = spec.detection;
            def.hitDelay = 0.3f;
            def.projectile = spec.ranged ? arrow : null;
            def.isBoss = spec.boss;
            def.prefab = BuildPixelUnit(spec.id, style, true, def, spec.boss);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{spec.id}/{spec.id}_Idle.png");
            defs[spec.id] = def;
            EditorUtility.SetDirty(def);
        }
        var king = defs["BarrowKing"];
        king.bossAbility = BossAbility.Summon;
        king.abilityCooldown = 12f;
        king.summons = defs["Skeleton"];
        king.summonCount = 3;
        db.skeleton = defs["Skeleton"];
        db.skeletonArcher = defs["SkeletonArcher"];
        db.wolf = defs["Wolf"];
        db.giantSpider = defs["GiantSpider"];
        db.troll = defs["Troll"];
        db.barrowKing = king;
    }

    // ---------- Blessings ----------

    private static void SetupBlessings(GameDatabase db)
    {
        Directory.CreateDirectory("Assets/Data/Blessings");
        Technology Blessing(string id, string name, string text)
        {
            string path = $"Assets/Data/Blessings/{id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Technology>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Technology>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.id = id;
            t.displayName = name;
            t.description = text;
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            return t;
        }
        var oak = Blessing("BlessingOak", "Blessing of the Oak", "+10% wood from every lumber camp.");
        oak.productionModifiers.Add(new ProductionModifier { resource = db.GetResource("Wood"), multiplier = 1.1f });
        var harvest = Blessing("BlessingHarvest", "Blessing of the Harvest", "+10% food from every farm and forager.");
        harvest.productionModifiers.Add(new ProductionModifier { resource = db.GetResource("Food"), multiplier = 1.1f });
        var ironSkin = Blessing("BlessingIronSkin", "Blessing of Iron Skin", "All troops +8% health.");
        ironSkin.unitModifiers.Add(new UnitStatModifier { healthMultiplier = 1.08f });
        var hunt = Blessing("BlessingHunt", "Blessing of the Hunt", "All troops +6% attack.");
        hunt.unitModifiers.Add(new UnitStatModifier { attackMultiplier = 1.06f });
        var swift = Blessing("BlessingSwift", "Blessing of Swift Feet", "All troops move 8% faster.");
        swift.unitModifiers.Add(new UnitStatModifier { speedMultiplier = 1.08f });
        var stone = Blessing("BlessingStone", "Blessing of Stone", "All buildings +8% health.");
        stone.buildingHealthMultiplier = 1.08f;
        db.blessings = new List<Technology> { oak, harvest, ironSkin, hunt, swift, stone };
        foreach (var b in db.blessings) EditorUtility.SetDirty(b);
    }
}
