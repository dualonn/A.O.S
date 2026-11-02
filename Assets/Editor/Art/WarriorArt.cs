using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Troops, raiders and tower crews: classic stick figures in a team colour, with every age
/// told apart by period gear (weapons, helmets, shields, horses).
/// </summary>
public static partial class PlaceholderArt
{
    // ---------- Styles ----------

    private enum WarPose { Idle, StrideA, Pass, StrideB, Windup, Raised, Strike, Recover, Stagger, Falling, Down }

    private enum Weapon { Club, Spear, Bow, Sword, Axe, Ballista, Sling, Javelin }

    /// <summary>What a mounted warrior rides.</summary>
    private enum Mount { Horse, Chariot, Elephant }

    private enum Helm { None, BronzeDome, IronCone, Galea }

    private enum ShieldType { None, Hide, Round, Scutum, Oval, Pelta }

    /// <summary>A classic stick figure (line limbs, round head) in a team colour; all the detail is gear.</summary>
    private class WarriorStyle
    {
        public string name;
        public Color ink;      // body colour = team colour
        public Color gear;     // headband, wraps, belt, saddle cloth
        public bool feather;   // raider extras
        public bool necklace;
        public Weapon weapon = Weapon.Club;
        public float bladeLength = 0.36f;
        public Helm helm;
        public Color crest = new Color(0, 0, 0, 0);
        public ShieldType shield;
        public Color shieldColor = new Color(0.6f, 0.4f, 0.22f);
        public bool mounted;
        public Mount mount;
        public Color horse = new Color(0.55f, 0.36f, 0.2f);
    }

    private static readonly Color PlayerInk = new Color(0.08f, 0.07f, 0.07f);
    private static readonly Color PlayerGear = new Color(0.22f, 0.45f, 0.9f);
    private static readonly Color RaiderInk = new Color(0.78f, 0.13f, 0.12f);
    private static readonly Color RaiderGear = new Color(0.18f, 0.12f, 0.08f);
    private static readonly Color Iron = new Color(0.7f, 0.72f, 0.76f);
    private static readonly Color IronDark = new Color(0.45f, 0.47f, 0.52f);
    private static readonly Color Gold = new Color(0.95f, 0.78f, 0.25f);

    // Stone Age
    private static readonly WarriorStyle ClubmanStyle = new WarriorStyle { name = "Clubman", ink = PlayerInk, gear = PlayerGear };
    private static readonly WarriorStyle RaiderStyle = new WarriorStyle { name = "Raider", ink = RaiderInk, gear = RaiderGear, feather = true, necklace = true };

    // Bronze Age: spears and tall hide shields
    private static readonly WarriorStyle SpearmanStyle = new WarriorStyle
    {
        name = "Spearman", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Spear, helm = Helm.BronzeDome,
        shield = ShieldType.Hide, crest = PlayerGear
    };
    private static readonly WarriorStyle BronzeRaiderStyle = new WarriorStyle
    {
        name = "BronzeRaider", ink = RaiderInk, gear = RaiderGear, weapon = Weapon.Spear, helm = Helm.BronzeDome,
        shield = ShieldType.Hide, necklace = true, crest = RaiderGear
    };

    private static readonly WarriorStyle AxemanStyle = new WarriorStyle
    {
        name = "Axeman", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Axe, helm = Helm.BronzeDome
    };
    private static readonly WarriorStyle WarChariotStyle = new WarriorStyle
    {
        name = "WarChariot", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Spear, helm = Helm.BronzeDome,
        crest = PlayerGear, mounted = true, mount = Mount.Chariot, horse = new Color(0.62f, 0.42f, 0.24f)
    };

    // Iron Age: iron swords, conical helmets, round wooden shields; the first archers
    private static readonly WarriorStyle SwordsmanStyle = new WarriorStyle
    {
        name = "Swordsman", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Sword, helm = Helm.IronCone,
        shield = ShieldType.Round, shieldColor = new Color(0.35f, 0.5f, 0.85f)
    };
    private static readonly WarriorStyle ArcherStyle = new WarriorStyle { name = "Archer", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Bow };
    private static readonly WarriorStyle SlingerStyle = new WarriorStyle { name = "Slinger", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Sling };
    private static readonly WarriorStyle IronRaiderStyle = new WarriorStyle
    {
        name = "IronRaider", ink = RaiderInk, gear = RaiderGear, weapon = Weapon.Sword, helm = Helm.IronCone,
        shield = ShieldType.Round, shieldColor = new Color(0.5f, 0.32f, 0.18f), necklace = true
    };
    private static readonly WarriorStyle RaiderArcherStyle = new WarriorStyle
    {
        name = "RaiderArcher", ink = RaiderInk, gear = RaiderGear, weapon = Weapon.Bow, feather = true
    };

    // Classical Age: legionaries with gladius and scutum, composite bows, cavalry
    private static readonly WarriorStyle LegionaryStyle = new WarriorStyle
    {
        name = "Legionary", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Sword, bladeLength = 0.28f, helm = Helm.Galea,
        crest = new Color(0.85f, 0.2f, 0.18f), shield = ShieldType.Scutum, shieldColor = new Color(0.3f, 0.45f, 0.85f)
    };
    private static readonly WarriorStyle CompositeArcherStyle = new WarriorStyle
    {
        name = "CompositeArcher", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Bow, helm = Helm.IronCone
    };
    private static readonly WarriorStyle HorsemanStyle = new WarriorStyle
    {
        name = "Horseman", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Spear, helm = Helm.Galea,
        crest = PlayerGear, mounted = true, horse = new Color(0.55f, 0.36f, 0.2f)
    };
    private static readonly WarriorStyle PeltastStyle = new WarriorStyle
    {
        name = "Peltast", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Javelin, shield = ShieldType.Pelta,
        shieldColor = new Color(0.82f, 0.68f, 0.42f)
    };
    private static readonly WarriorStyle WarElephantStyle = new WarriorStyle
    {
        name = "WarElephant", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Spear, helm = Helm.Galea,
        crest = PlayerGear, mounted = true, mount = Mount.Elephant, horse = new Color(0.56f, 0.55f, 0.56f)
    };
    private static readonly WarriorStyle BarbarianStyle = new WarriorStyle
    {
        name = "Barbarian", ink = RaiderInk, gear = RaiderGear, weapon = Weapon.Axe, shield = ShieldType.Oval,
        shieldColor = new Color(0.38f, 0.52f, 0.3f), feather = true, necklace = true
    };
    private static readonly WarriorStyle HorseRaiderStyle = new WarriorStyle
    {
        name = "HorseRaider", ink = RaiderInk, gear = RaiderGear, weapon = Weapon.Sword, mounted = true,
        horse = new Color(0.3f, 0.24f, 0.2f), feather = true
    };

    // Tower crews (cut at the waist; they stand behind a parapet)
    private static readonly WarriorStyle TowerArcherStyle = new WarriorStyle { name = "TowerArcher", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Bow };
    private static readonly WarriorStyle TowerArcherIronStyle = new WarriorStyle
    {
        name = "TowerArcherIron", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Bow, helm = Helm.IronCone
    };
    private static readonly WarriorStyle BallistaCrewStyle = new WarriorStyle
    {
        name = "BallistaCrew", ink = PlayerInk, gear = PlayerGear, weapon = Weapon.Ballista, helm = Helm.Galea, crest = PlayerGear
    };

    /// <summary>Unit assets (Assets/Data/Units/{id}.asset) and how each is drawn.</summary>
    private static readonly (string id, WarriorStyle style, bool enemy)[] Warriors =
    {
        ("Clubman", ClubmanStyle, false), ("EnemyClubman", RaiderStyle, true),
        ("Spearman", SpearmanStyle, false), ("Axeman", AxemanStyle, false), ("WarChariot", WarChariotStyle, false),
        ("BronzeRaider", BronzeRaiderStyle, true),
        ("Swordsman", SwordsmanStyle, false), ("Archer", ArcherStyle, false), ("Slinger", SlingerStyle, false),
        ("IronRaider", IronRaiderStyle, true), ("RaiderArcher", RaiderArcherStyle, true),
        ("Legionary", LegionaryStyle, false), ("CompositeArcher", CompositeArcherStyle, false), ("Horseman", HorsemanStyle, false),
        ("Peltast", PeltastStyle, false), ("WarElephant", WarElephantStyle, false),
        ("Barbarian", BarbarianStyle, true), ("HorseRaider", HorseRaiderStyle, true),
    };

    /// <summary>Generate every unit's frames and prefab, and every crewed tower's operator and projectile.</summary>
    private static void GenerateWarriorsAndCrews(GameDatabase database)
    {
        var arrow = ArrowSprite();
        var bolt = BoltSprite();
        var stone = SlingStoneSprite();
        var javelin = JavelinSprite();
        foreach (var (id, style, enemy) in Warriors)
        {
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{id}.asset");
            if (def == null)
            {
                continue;
            }
            def.prefab = BuildWarrior(style, enemy, def);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{style.name}/{style.name}_Idle.png");
            def.projectile = style.weapon == Weapon.Bow ? arrow : style.weapon == Weapon.Sling ? stone
                : style.weapon == Weapon.Javelin ? javelin : null;
            EditorUtility.SetDirty(def);
        }

        if (database == null)
        {
            return;
        }
        var crews = new (string building, WarriorStyle style, Sprite projectile)[]
        {
            ("Watchtower", TowerArcherStyle, arrow), ("StoneTower", TowerArcherIronStyle, arrow), ("BallistaTower", BallistaCrewStyle, bolt),
        };
        foreach (var (buildingId, style, projectile) in crews)
        {
            var tower = database.GetBuilding(buildingId);
            if (tower == null)
            {
                continue;
            }
            var crew = BuildOperator(style);
            tower.towerProjectile = projectile;
            tower.operatorIdle = crew.idle;
            tower.operatorShoot = crew.shoot;
            tower.operatorReleaseFrame = 2;
            EditorUtility.SetDirty(tower);
        }
    }

    // ---------- Prefabs ----------

    // Room round each figure's drawing area, so a spear thrust or a raised lance is never cut
    // off at the sprite's edge. The figure is drawn exactly as before, just on a bigger canvas.
    private const float PadSide = 0.45f, PadBottom = 0.1f, PadTop = 0.45f;

    private static Painter WarriorCanvas(WarriorStyle style)
    {
        Vector2 area = WarriorArea(style);
        return new Painter(area.x + 2 * PadSide, area.y + PadBottom + PadTop, 160) { Outline = 0.018f, Origin = V(PadSide, PadBottom) };
    }

    /// <summary>The drawing area a figure is designed in (feet at its bottom centre).</summary>
    private static Vector2 WarriorArea(WarriorStyle style) =>
        !style.mounted ? V(1.6f, 1.2f)
        : style.mount == Mount.Elephant ? V(2.6f, 2.2f)
        : style.mount == Mount.Chariot ? V(2.6f, 1.6f)
        : V(2.0f, 1.6f);

    /// <summary>Where the feet are, in the figure's drawing coordinates.</summary>
    private static Vector2 WarriorFeet(WarriorStyle style) => new Vector2(WarriorArea(style).x * 0.5f, WarriorArea(style).y * 0.05f);

    /// <summary>The sprite pivot (normalised) that puts the feet on the unit's position.</summary>
    private static Vector2 WarriorPivot(WarriorStyle style)
    {
        Vector2 area = WarriorArea(style), feet = WarriorFeet(style);
        return new Vector2((feet.x + PadSide) / (area.x + 2 * PadSide), (feet.y + PadBottom) / (area.y + PadBottom + PadTop));
    }

    /// <summary>Where the figure (designed around x = 0.5) sits on its canvas, so the unit's feet are the pivot.</summary>
    private static float CanvasShift(WarriorStyle s) =>
        !s.mounted ? 0.3f : s.mount == Mount.Elephant ? 0.8f : s.mount == Mount.Chariot ? 0.8f : 0.5f;

    /// <summary>Generate a warrior's frames and build a prefab for it. Returns the prefab.</summary>
    private static GameObject BuildWarrior(WarriorStyle style, bool isEnemy, UnitDefinition definition)
    {
        string dir = $"Units/{style.name}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        Sprite Frame(string frame, WarPose pose)
        {
            // Wide canvas so swings and falling bodies aren't clipped; feet stay at the centre.
            var p = WarriorCanvas(style);
            DrawWarrior(p, pose, style);
            return Save(p, $"{dir}/{style.name}_{frame}.png", 160, WarriorPivot(style));
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
        if (!isEnemy)
        {
            BuildHeroRig(style, visual);
        }
        bool elephant = style.mounted && style.mount == Mount.Elephant;
        visual.barHeight = elephant ? 1.95f : style.mounted ? 1.3f : 0.95f;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = elephant ? 0.6f : style.mounted ? 0.4f : 0.25f;
        col.offset = new Vector2(elephant ? 0.2f : 0f, elephant ? 0.8f : style.mounted ? 0.55f : 0.4f);
        go.AddComponent<MouseTouching>();
        Unit unit = isEnemy ? go.AddComponent<Enemy>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = elephant ? 0.95f : style.mounted ? 0.6f : 0.38f;

        string prefabPath = $"Assets/Prefabs/Units/{style.name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
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
            // tower with this cut line on the parapet top, so nothing on the tower can hide them.
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

    private static Sprite SlingStoneSprite()
    {
        var p = new Painter(0.16f, 0.16f, 160) { Outline = 0.012f };
        p.OutlinedCircle(V(0.08f, 0.08f), 0.045f, StoneLight);
        return Save(p, "Units/SlingStone.png", 160, new Vector2(0.5f, 0.5f));
    }

    private static Sprite JavelinSprite()
    {
        var p = new Painter(0.7f, 0.14f, 160) { Outline = 0.012f };
        p.OutlinedLine(V(0.04f, 0.07f), V(0.56f, 0.07f), 0.022f, WoodLight);
        p.Line(V(0.24f, 0.07f), V(0.3f, 0.07f), 0.03f, PlayerGear); // throwing loop
        p.OutlinedPolygon(new[] { V(0.55f, 0.035f), V(0.68f, 0.07f), V(0.55f, 0.105f) }, Iron);
        return Save(p, "Units/Javelin.png", 160, new Vector2(0.5f, 0.5f));
    }

    private static Sprite BoltSprite()
    {
        var p = new Painter(0.9f, 0.2f, 160) { Outline = 0.014f };
        p.OutlinedLine(V(0.1f, 0.1f), V(0.72f, 0.1f), 0.028f, Wood);
        p.OutlinedPolygon(new[] { V(0.7f, 0.05f), V(0.87f, 0.1f), V(0.7f, 0.15f) }, Iron);
        p.OutlinedPolygon(new[] { V(0.06f, 0.1f), V(0.2f, 0.1f), V(0.12f, 0.18f) }, StoneLight);
        p.OutlinedPolygon(new[] { V(0.06f, 0.1f), V(0.2f, 0.1f), V(0.12f, 0.02f) }, StoneLight);
        return Save(p, "Units/Bolt.png", 160, new Vector2(0.5f, 0.5f));
    }

    // ---------- Drawing ----------

    /// <summary>
    /// Draw one frame of a warrior. 'bare' leaves off the helmet, shield and melee weapon (a hero
    /// wears their own, drawn on top); 'anchors' receives where that gear goes on this frame.
    /// </summary>
    private static void DrawWarrior(Painter p, WarPose pose, WarriorStyle s, bool bare = false, System.Action<GearAnchor> anchors = null)
    {
        const float limb = 0.042f;
        // Death frames lean the whole figure (and horse) back around the feet.
        float lean = pose == WarPose.Stagger ? -18f : pose == WarPose.Falling ? -55f : pose == WarPose.Down ? -88f : 0f;
        Vector2 pivot = pose == WarPose.Down ? V(0.5f, 0.12f) : V(0.5f, 0.06f);
        float rad = lean * Mathf.Deg2Rad;
        // A falling rider is thrown clear: the horse collapses on its own and the rider falls in front of it.
        bool thrown = s.mounted && (pose == WarPose.Falling || pose == WarPose.Down);
        bool riding = s.mounted && !thrown;
        // The figure is designed around x = 0.5; shift it to the middle of the canvas.
        // Lying bodies stretch to the right, so they start a little further left.
        bool chariot = s.mounted && s.mount == Mount.Chariot, elephant = s.mounted && s.mount == Mount.Elephant;
        float baseShift = CanvasShift(s);
        // Thrown riders land in front of a horse or elephant, but behind a wrecked chariot's team.
        float thrownShift = chariot ? baseShift - 0.55f : elephant ? baseShift + 0.45f : baseShift + 0.12f;
        Vector2 shift = s.mounted ? V(thrown ? thrownShift : baseShift, 0f) : V(pose == WarPose.Down || pose == WarPose.Falling ? 0.18f : 0.3f, 0f);
        // Riders sit on the horse's back, stand in the chariot, or sit high on the elephant.
        Vector2 rise = !riding ? Vector2.zero : chariot ? V(0f, 0.2f) : elephant ? V(0f, 0.62f) : V(0f, 0.34f);
        Vector2 T(Vector2 v) => v + shift;
        Vector2 Rot(Vector2 v)
        {
            Vector2 d = v - pivot;
            return shift + pivot + new Vector2(d.x * Mathf.Cos(rad) - d.y * Mathf.Sin(rad), d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad));
        }
        Vector2 R(Vector2 v) => Rot(v + rise);

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
        if (riding && chariot)
        {
            // The charioteer stands braced on the car's floor.
            footL = V(0.42f, 0.06f); footR = V(0.58f, 0.06f);
        }
        else if (riding)
        {
            // Legs hang down the horse's (or elephant's) sides.
            footL = (elephant ? V(0.6f, 0.8f) : V(0.6f, 0.47f)) - rise;
            footR = (elephant ? V(0.52f, 0.82f) : V(0.52f, 0.49f)) - rise;
        }
        bool attacking = pose == WarPose.Windup || pose == WarPose.Raised || pose == WarPose.Strike || pose == WarPose.Recover;
        if (attacking && (s.weapon == Weapon.Sling || s.weapon == Weapon.Javelin))
        {
            handL = V(0.68f, 0.64f); // the free arm points at the target
        }

        bool down = pose == WarPose.Down;
        float shadowX = riding && chariot ? 0.75f : riding && elephant ? 0.6f : down ? 0.8f : 0.5f;
        float shadowW = riding && chariot ? 0.65f : riding && elephant ? 0.55f : down ? 0.36f : riding ? 0.42f : 0.16f;
        p.Ellipse(T(V(shadowX, 0.06f)), V(shadowW, 0.035f), Shadow);

        if (riding && chariot)
        {
            DrawChariotTeam(p, pose, s, Rot);
        }
        else if (riding && elephant)
        {
            p.Line(R(hip), R(footR), limb, s.ink); // far leg, behind the elephant
            DrawElephant(p, pose, s, Rot);
        }
        else if (riding)
        {
            p.Line(R(hip), R(footR), limb, s.ink); // far leg, behind the horse
            DrawHorse(p, pose, s, Rot);
        }
        else if (thrown && chariot)
        {
            DrawChariotWreck(p, pose, s, baseShift);
        }
        else if (thrown && elephant)
        {
            DrawFallenElephant(p, pose, s, baseShift - 0.5f);
        }
        else if (thrown)
        {
            DrawFallenHorse(p, pose, s);
        }

        // Feather behind the head (drawn first so the head overlaps it)
        var helm = bare ? Helm.None : s.helm;
        bool bareWeapon = bare && ClassOf(s.weapon) == WeaponClass.Melee;
        if (s.feather && helm == Helm.None && !bare)
        {
            p.OutlinedLine(R(V(0.45f, 0.82f)), R(V(0.36f, 0.98f)), 0.03f, new Color(0.95f, 0.92f, 0.85f));
            p.Line(R(V(0.37f, 0.96f)), R(V(0.355f, 0.99f)), 0.03f, new Color(0.15f, 0.12f, 0.1f));
        }
        // Headband tails flowing behind
        if (helm == Helm.None || helm == Helm.BronzeDome)
        {
            p.Line(R(V(0.43f, 0.79f)), R(V(0.33f, 0.74f)), 0.022f, s.gear);
            p.Line(R(V(0.43f, 0.79f)), R(V(0.35f, 0.7f)), 0.022f, s.gear);
        }

        // The stickman: legs, spine, arms, round head
        p.Line(R(hip), R(footL), limb, s.ink);
        if (!riding)
        {
            p.Line(R(hip), R(footR), limb, s.ink);
        }
        p.Line(R(hip), R(V(0.5f, 0.69f)), limb, s.ink);
        if (riding && chariot)
        {
            DrawChariotCar(p, pose, s, Rot); // the car's front hides the charioteer's legs
        }
        // A shield stays braced in front of the chest, so it never covers the face mid-swing.
        bool holdsShield = bare ? HeroTakesShield(s) : s.shield != ShieldType.None;
        if (holdsShield && pose != WarPose.Down && pose != WarPose.Falling)
        {
            handL = new Vector2(Mathf.Clamp(handL.x, 0.54f, 0.6f), Mathf.Min(handL.y, 0.5f));
        }
        bool swings = s.weapon == Weapon.Club || s.weapon == Weapon.Sword || s.weapon == Weapon.Axe;
        // Arms: swung weapons use both pose hands; spears, bows and the ballista draw their own.
        if (s.weapon != Weapon.Bow && s.weapon != Weapon.Ballista)
        {
            p.Line(R(shoulder), R(handL), limb, s.ink);
        }
        if (swings)
        {
            p.Line(R(shoulder), R(handR), limb, s.ink);
        }
        p.Circle(R(head), 0.085f, s.ink);

        // Gear
        if (helm == Helm.None)
        {
            p.Line(R(V(0.425f, 0.785f)), R(V(0.575f, 0.785f)), 0.026f, s.gear); // headband
        }
        p.Line(R(V(0.455f, 0.385f)), R(V(0.545f, 0.385f)), 0.03f, s.gear);       // belt
        p.Line(R(V(0.5f, 0.385f)), R(V(0.53f, 0.31f)), 0.022f, s.gear);          // belt tail
        Vector2 Wrist(Vector2 hand) => Vector2.Lerp(shoulder, hand, 0.82f);
        if (s.weapon == Weapon.Club)
        {
            p.Circle(R(Wrist(handL)), 0.022f, s.gear);                            // wrist wraps
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
        if (anchors != null)
        {
            // Where a hero's gear goes on this frame, in sprite units from the feet.
            Vector2 feet = WarriorFeet(s);
            Vector2 L(Vector2 v) => R(v) - feet;
            Vector2 grip = handR, dir = clubTip - handR;
            if (s.weapon == Weapon.Spear) SpearHold(pose, ref grip, out dir);
            var a = new GearAnchor
            {
                head = L(head), neck = L(V(0.5f, 0.64f)), torso = L(V(0.5f, 0.52f)), hip = L(V(0.5f, 0.385f)),
                shield = L(handL + V(0.03f, 0.02f)), handL = L(handL), lean = lean, shieldShown = !dead,
                grip = dead ? T(V(0.3f, 0.09f)) - feet : L(grip),
                weaponAngle = dead ? 0f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + lean,
            };
            anchors(a);
        }
        if (!bare)
        {
            DrawHelm(p, s, head, R);
        }
        if (!dead && !bare)
        {
            DrawShield(p, s, handL + V(0.03f, 0.02f), R);
        }

        if (dead)
        {
            if (!bareWeapon)
            {
                DrawDroppedWeapon(p, s, T);
            }
            return;
        }
        if (bareWeapon && s.weapon != Weapon.Spear)
        {
            return; // the hero's own weapon is drawn on top
        }

        switch (s.weapon)
        {
            case Weapon.Club:
            {
                // Club: wrapped grip, then the knobbly head
                Vector2 grip = Vector2.Lerp(handR, clubTip, 0.18f);
                p.OutlinedLine(R(handR), R(Vector2.Lerp(handR, clubTip, 0.55f)), 0.035f, WoodLight);
                p.Line(R(Vector2.Lerp(handR, clubTip, -0.04f)), R(grip), 0.045f, s.gear);
                p.OutlinedLine(R(Vector2.Lerp(handR, clubTip, 0.5f)), R(clubTip), 0.075f, Wood);
                p.Circle(R(Vector2.Lerp(handR, clubTip, 0.78f)), 0.012f, WoodDark);
                p.Circle(R(Vector2.Lerp(handR, clubTip, 0.9f)), 0.012f, WoodDark);
                break;
            }
            case Weapon.Sword:
            {
                // Iron blade with a crossguard, swung along the same arc as a club
                Vector2 dir = (clubTip - handR).normalized, side = new Vector2(-dir.y, dir.x);
                Vector2 end = handR + dir * s.bladeLength;
                p.OutlinedLine(R(handR - dir * 0.06f), R(handR), 0.03f, WoodDark);
                p.OutlinedPolygon(new[] { R(handR + side * 0.022f), R(end + side * 0.016f), R(end + dir * 0.05f), R(end - side * 0.016f), R(handR - side * 0.022f) }, Iron);
                p.OutlinedLine(R(handR + dir * 0.01f + side * 0.055f), R(handR + dir * 0.01f - side * 0.055f), 0.022f, s.helm == Helm.Galea ? Gold : IronDark);
                break;
            }
            case Weapon.Axe:
            {
                // Long-hafted axe with a bearded iron head
                Vector2 dir = (clubTip - handR).normalized, side = new Vector2(-dir.y, dir.x);
                Vector2 end = handR + dir * 0.42f;
                p.OutlinedLine(R(handR - dir * 0.06f), R(end + dir * 0.03f), 0.03f, WoodLight);
                p.OutlinedPolygon(new[] { R(end + dir * 0.02f), R(end + side * 0.11f + dir * 0.05f), R(end + side * 0.12f - dir * 0.11f), R(end - dir * 0.08f) },
                    s.helm == Helm.BronzeDome ? Bronze2 : Iron);
                break;
            }
            case Weapon.Sling:
            {
                // Sling: whirled overhead, loosed forward; a bag of stones on the hip
                Vector2 hand, pouch;
                bool loaded = true;
                switch (pose)
                {
                    case WarPose.Windup: hand = V(0.38f, 0.74f); pouch = V(0.2f, 0.6f); break;
                    case WarPose.Raised: hand = V(0.52f, 0.9f); pouch = V(0.66f, 1.04f); break;
                    case WarPose.Strike: hand = V(0.76f, 0.68f); pouch = V(0.9f, 0.76f); loaded = false; break;
                    case WarPose.Recover: hand = V(0.68f, 0.5f); pouch = V(0.7f, 0.32f); loaded = false; break;
                    default: hand = handR; pouch = handR + V(0.04f, -0.2f); break;
                }
                p.OutlinedCircle(R(V(0.43f, 0.33f)), 0.045f, Hide);
                p.Line(R(shoulder), R(hand), limb, s.ink);
                Color cord = new Color(0.92f, 0.86f, 0.7f);
                p.Line(R(hand), R(pouch), 0.012f, cord);
                if (pose == WarPose.Strike)
                {
                    p.Line(R(hand), R(hand + V(0.12f, -0.1f)), 0.012f, cord); // the loosed cord
                }
                p.OutlinedCircle(R(pouch), 0.028f, Hide);
                if (loaded)
                {
                    p.Circle(R(pouch + V(0, 0.01f)), 0.016f, StoneLight);
                }
                p.Circle(R(hand), 0.022f, s.gear);
                break;
            }
            case Weapon.Javelin:
            {
                // Javelin: drawn back over the shoulder, then thrown
                Vector2 hand, dir;
                bool held = true;
                switch (pose)
                {
                    case WarPose.Windup: hand = V(0.34f, 0.7f); dir = V(1f, 0.12f); break;
                    case WarPose.Raised: hand = V(0.28f, 0.74f); dir = V(1f, 0.2f); break;
                    case WarPose.Strike: hand = V(0.76f, 0.72f); dir = V(1f, 0f); held = false; break;
                    case WarPose.Recover: hand = V(0.66f, 0.52f); dir = V(1f, 0f); held = false; break;
                    default: hand = handR; dir = V(0.15f, 1f); break;
                }
                dir.Normalize();
                p.Line(R(shoulder), R(hand), limb, s.ink);
                if (held)
                {
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    Vector2 butt = hand - dir * 0.22f, tip = hand + dir * 0.34f;
                    p.OutlinedLine(R(butt), R(tip), 0.024f, WoodLight);
                    p.OutlinedPolygon(new[] { R(tip - side * 0.035f), R(tip + dir * 0.1f), R(tip + side * 0.035f) }, Iron);
                }
                p.Circle(R(hand), 0.022f, s.gear);
                break;
            }
            case Weapon.Spear:
            {
                // Spear: held level for the thrust, upright when idle or walking
                SpearHold(pose, ref handR, out var dir);
                p.Line(R(shoulder), R(handR), limb, s.ink); // the arm reaching the spear
                if (bareWeapon)
                {
                    break;
                }
                float length = s.mounted ? 0.75f : 0.55f;   // cavalry carry longer lances
                Vector2 butt = handR - dir * 0.3f, tip = handR + dir * length;
                p.OutlinedLine(R(butt), R(tip), 0.03f, WoodLight);
                Vector2 side = new Vector2(-dir.y, dir.x);
                p.OutlinedPolygon(new[] { R(tip - side * 0.05f), R(tip + dir * 0.14f), R(tip + side * 0.05f) }, s.helm == Helm.BronzeDome ? Bronze2 : Iron);
                p.Circle(R(handR), 0.025f, s.gear);
                break;
            }
            case Weapon.Bow:
            {
                // Bow: aim forward, string drawn back on the draw frame
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
                // Quiver on the back
                var quiver = new[] { R(V(0.36f, 0.48f)), R(V(0.42f, 0.48f)), R(V(0.44f, 0.66f)), R(V(0.38f, 0.66f)) };
                p.OutlinedPolygon(quiver, Hide);
                p.Line(R(shoulder), R(bowHand), limb, s.ink);
                p.Line(R(shoulder), R(stringHand), limb, s.ink);
                Vector2 c = bowHand + V(-0.2f, 0);
                Vector2 top = c + V(0.12f, 0.2f), bottom = c + V(0.12f, -0.2f);
                Vector2 cw = R(c);
                Color bowColor = s.helm == Helm.IronCone && s.name.StartsWith("Composite") ? Dark : WoodDark;
                p.Draw(q => Mathf.Max(Mathf.Abs(SdCircle(q, cw, 0.23f)) - 0.016f, cw.x + 0.1f - q.x),
                    new Rect(cw.x - 0.3f, cw.y - 0.3f, 0.6f, 0.6f), bowColor);
                if (s.name.StartsWith("Composite"))
                {
                    // Recurved tips
                    p.Line(R(top), R(top + V(0.06f, 0.04f)), 0.016f, bowColor);
                    p.Line(R(bottom), R(bottom + V(0.06f, -0.04f)), 0.016f, bowColor);
                }
                Vector2 nock = drawn ? stringHand : V(top.x, (top.y + bottom.y) / 2);
                p.Line(R(top), R(nock), 0.008f, new Color(0.9f, 0.88f, 0.8f));
                p.Line(R(nock), R(bottom), 0.008f, new Color(0.9f, 0.88f, 0.8f));
                if (!released)
                {
                    Vector2 from = drawn ? stringHand : nock;
                    p.Line(R(from), R(from + V(0.38f, 0)), 0.014f, WoodLight);
                    p.OutlinedPolygon(new[] { R(from + V(0.38f, -0.025f)), R(from + V(0.45f, 0)), R(from + V(0.38f, 0.025f)) }, Iron);
                }
                break;
            }
            case Weapon.Ballista:
                DrawBallista(p, pose, s, shoulder, limb, R);
                break;
        }
    }

    /// <summary>Spears are held level for the thrust and upright when idle or walking.</summary>
    private static void SpearHold(WarPose pose, ref Vector2 hand, out Vector2 dir)
    {
        switch (pose)
        {
            case WarPose.Windup: dir = V(1f, 0.05f); hand = V(0.4f, 0.55f); break;
            case WarPose.Raised: dir = V(1f, 0.1f); hand = V(0.45f, 0.6f); break;
            case WarPose.Strike: dir = V(1f, -0.05f); hand = V(0.78f, 0.55f); break;
            case WarPose.Recover: dir = V(1f, -0.15f); hand = V(0.65f, 0.5f); break;
            default: dir = V(0.15f, 1f); break;
        }
        dir.Normalize();
    }

    private static WeaponClass ClassOf(Weapon w) =>
        w == Weapon.Bow ? WeaponClass.Bow : w == Weapon.Sling || w == Weapon.Javelin ? WeaponClass.Thrown
        : w == Weapon.Ballista ? WeaponClass.None : WeaponClass.Melee;

    /// <summary>Archers need both hands for the bow; everyone else can carry a shield.</summary>
    private static bool HeroTakesShield(WarriorStyle s) => ClassOf(s.weapon) == WeaponClass.Melee || ClassOf(s.weapon) == WeaponClass.Thrown;

    private static void DrawHelm(Painter p, WarriorStyle s, Vector2 head, System.Func<Vector2, Vector2> R)
    {
        switch (s.helm)
        {
            case Helm.BronzeDome:
            case Helm.Galea:
            {
                // Dome; the Roman galea adds a neck guard and a front-to-back crest
                var dome = new List<Vector2>();
                for (int i = 0; i <= 8; i++)
                {
                    float a = Mathf.PI * i / 8f;
                    dome.Add(R(head + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.1f));
                }
                dome.Add(R(head + V(-0.1f, -0.02f)));
                bool galea = s.helm == Helm.Galea;
                if (galea)
                {
                    p.OutlinedLine(R(head + V(-0.09f, -0.01f)), R(head + V(-0.14f, -0.05f)), 0.03f, Iron);
                }
                p.OutlinedPolygon(dome.ToArray(), galea ? Iron : Bronze2);
                if (galea)
                {
                    p.OutlinedLine(R(head + V(0.06f, 0.0f)), R(head + V(0.07f, -0.07f)), 0.022f, Iron); // cheek guard
                }
                if (s.crest.a > 0)
                {
                    if (galea)
                    {
                        p.OutlinedLine(R(head + V(-0.08f, 0.11f)), R(head + V(0.07f, 0.12f)), 0.05f, s.crest);
                    }
                    else
                    {
                        p.OutlinedLine(R(head + V(-0.06f, 0.1f)), R(head + V(0.06f, 0.12f)), 0.04f, s.crest);
                    }
                }
                break;
            }
            case Helm.IronCone:
            {
                // Conical iron helmet with a nose guard
                p.OutlinedPolygon(new[] { R(head + V(-0.105f, 0.0f)), R(head + V(0.105f, 0.0f)), R(head + V(0.01f, 0.17f)) }, Iron);
                p.OutlinedLine(R(head + V(-0.1f, 0.01f)), R(head + V(0.1f, 0.01f)), 0.02f, IronDark);
                p.OutlinedLine(R(head + V(0.065f, 0.0f)), R(head + V(0.065f, -0.07f)), 0.018f, Iron);
                if (s.ink == RaiderInk && s.feather)
                {
                    p.OutlinedLine(R(head + V(0.0f, 0.15f)), R(head + V(-0.08f, 0.24f)), 0.025f, new Color(0.95f, 0.92f, 0.85f));
                }
                break;
            }
        }
    }

    private static void DrawShield(Painter p, WarriorStyle s, Vector2 c, System.Func<Vector2, Vector2> R)
    {
        switch (s.shield)
        {
            case ShieldType.Hide:
            {
                // Bronze Age: a tall wooden frame covered in spotted hide, with a rounded top
                // (as carried by Egyptian and Sumerian spearmen).
                var outline = new List<Vector2> { R(c + V(-0.085f, -0.19f)), R(c + V(0.085f, -0.19f)) };
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
                break;
            }
            case ShieldType.Round:
            {
                // Iron Age: painted wooden planks round an iron boss
                p.OutlinedCircle(R(c), 0.13f, s.shieldColor);
                p.Line(R(c + V(-0.045f, -0.12f)), R(c + V(-0.045f, 0.12f)), 0.008f, WoodDark);
                p.Line(R(c + V(0.045f, -0.12f)), R(c + V(0.045f, 0.12f)), 0.008f, WoodDark);
                p.OutlinedCircle(R(c), 0.038f, Iron);
                break;
            }
            case ShieldType.Scutum:
            {
                // Classical: the curved rectangular legionary shield with a boss and painted wings
                var corners = new[] { R(c + V(-0.1f, -0.22f)), R(c + V(0.1f, -0.22f)), R(c + V(0.1f, 0.2f)), R(c + V(-0.1f, 0.2f)) };
                p.OutlinedPolygon(corners, s.shieldColor);
                p.Line(R(c + V(-0.08f, 0.06f)), R(c + V(0.08f, 0.1f)), 0.014f, Gold);
                p.Line(R(c + V(-0.08f, -0.08f)), R(c + V(0.08f, -0.12f)), 0.014f, Gold);
                p.OutlinedCircle(R(c), 0.04f, Gold);
                break;
            }
            case ShieldType.Oval:
            {
                // Celtic long oval shield with a central spine and boss
                var oval = new List<Vector2>();
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.PI * 2f * i / 16f;
                    oval.Add(R(c + V(Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.2f)));
                }
                p.OutlinedPolygon(oval.ToArray(), s.shieldColor);
                p.Line(R(c + V(0, -0.18f)), R(c + V(0, 0.18f)), 0.014f, WoodDark);
                p.OutlinedCircle(R(c), 0.035f, Iron);
                break;
            }
            case ShieldType.Pelta:
            {
                // Thracian pelta: a light wicker crescent
                var crescent = new List<Vector2>();
                for (int i = 0; i <= 14; i++)
                {
                    float a = (35f - 250f * i / 14f) * Mathf.Deg2Rad;
                    crescent.Add(R(c + V(Mathf.Cos(a), Mathf.Sin(a)) * 0.12f));
                }
                crescent.Add(R(c + V(-0.05f, 0.04f)));
                crescent.Add(R(c + V(0f, 0.02f)));
                crescent.Add(R(c + V(0.05f, 0.04f)));
                p.OutlinedPolygon(crescent.ToArray(), s.shieldColor);
                p.Line(R(c + V(-0.07f, -0.04f)), R(c + V(0.07f, -0.04f)), 0.01f, WoodDark);
                p.Line(R(c + V(-0.06f, -0.08f)), R(c + V(0.06f, -0.08f)), 0.01f, WoodDark);
                break;
            }
        }
    }

    private static void DrawDroppedWeapon(Painter p, WarriorStyle s, System.Func<Vector2, Vector2> T)
    {
        switch (s.weapon)
        {
            case Weapon.Spear:
                p.OutlinedLine(T(V(0.0f, 0.09f)), T(V(0.55f, 0.11f)), 0.03f, WoodLight);
                p.OutlinedPolygon(new[] { T(V(0.55f, 0.07f)), T(V(0.68f, 0.11f)), T(V(0.55f, 0.15f)) }, s.helm == Helm.BronzeDome ? Bronze2 : Iron);
                break;
            case Weapon.Club:
                p.OutlinedLine(T(V(0.2f, 0.08f)), T(V(0.42f, 0.1f)), 0.035f, WoodLight);
                p.OutlinedLine(T(V(0.38f, 0.1f)), T(V(0.55f, 0.12f)), 0.075f, Wood);
                break;
            case Weapon.Sword:
                p.OutlinedLine(T(V(0.22f, 0.09f)), T(V(0.3f, 0.09f)), 0.03f, WoodDark);
                p.OutlinedPolygon(new[] { T(V(0.3f, 0.07f)), T(V(0.3f + s.bladeLength, 0.08f)), T(V(0.35f + s.bladeLength, 0.09f)), T(V(0.3f + s.bladeLength, 0.1f)), T(V(0.3f, 0.11f)) }, Iron);
                break;
            case Weapon.Axe:
                p.OutlinedLine(T(V(0.1f, 0.09f)), T(V(0.55f, 0.1f)), 0.03f, WoodLight);
                p.OutlinedPolygon(new[] { T(V(0.5f, 0.1f)), T(V(0.6f, 0.2f)), T(V(0.64f, 0.08f)), T(V(0.56f, 0.06f)) }, s.helm == Helm.BronzeDome ? Bronze2 : Iron);
                break;
            case Weapon.Sling:
                p.Line(T(V(0.3f, 0.08f)), T(V(0.56f, 0.09f)), 0.012f, new Color(0.92f, 0.86f, 0.7f));
                p.OutlinedCircle(T(V(0.57f, 0.09f)), 0.025f, Hide);
                break;
            case Weapon.Javelin:
                p.OutlinedLine(T(V(0.12f, 0.09f)), T(V(0.6f, 0.1f)), 0.024f, WoodLight);
                p.OutlinedPolygon(new[] { T(V(0.6f, 0.07f)), T(V(0.7f, 0.1f)), T(V(0.6f, 0.13f)) }, Iron);
                break;
            case Weapon.Bow:
                p.OutlinedLine(T(V(0.15f, 0.09f)), T(V(0.62f, 0.09f)), 0.02f, WoodDark);
                break;
        }
    }

    /// <summary>A horse facing right, under a rider. Legs swing with the walk; it topples with the rider.</summary>
    private static void DrawHorse(Painter p, WarPose pose, WarriorStyle s, System.Func<Vector2, Vector2> Rot)
    {
        Color coat = s.horse, dark = Color.Lerp(s.horse, Color.black, 0.45f);
        float swing = pose == WarPose.StrideA ? 0.08f : pose == WarPose.StrideB ? -0.08f : 0f;
        float rear = pose == WarPose.Windup ? 0.04f : 0f; // a little lift of the forehand on the windup
        // Far legs first, darker
        p.Line(Rot(V(0.7f, 0.42f + rear)), Rot(V(0.7f - swing, 0.06f + rear)), 0.04f, dark);
        p.Line(Rot(V(0.32f, 0.42f)), Rot(V(0.32f + swing, 0.06f)), 0.04f, dark);
        // Tail
        p.OutlinedLine(Rot(V(0.21f, 0.56f)), Rot(V(0.12f, 0.34f)), 0.045f, dark);
        // Body, neck and head
        var body = new List<Vector2>();
        for (int i = 0; i < 18; i++)
        {
            float a = Mathf.PI * 2f * i / 18f;
            body.Add(Rot(V(0.48f + Mathf.Cos(a) * 0.29f, 0.5f + rear * 0.5f + Mathf.Sin(a) * 0.12f)));
        }
        p.OutlinedPolygon(body.ToArray(), coat);
        p.OutlinedPolygon(new[] { Rot(V(0.66f, 0.48f + rear)), Rot(V(0.78f, 0.56f + rear)), Rot(V(0.9f, 0.8f + rear)), Rot(V(0.8f, 0.84f + rear)), Rot(V(0.66f, 0.62f + rear)) }, coat);
        p.OutlinedPolygon(new[] { Rot(V(0.8f, 0.84f + rear)), Rot(V(0.9f, 0.82f + rear)), Rot(V(1.02f, 0.7f + rear)), Rot(V(0.97f, 0.65f + rear)), Rot(V(0.86f, 0.72f + rear)) }, coat);
        p.OutlinedPolygon(new[] { Rot(V(0.82f, 0.85f + rear)), Rot(V(0.85f, 0.93f + rear)), Rot(V(0.87f, 0.84f + rear)) }, coat); // ear
        p.Circle(Rot(V(0.9f, 0.77f + rear)), 0.014f, p.Ink);
        p.Line(Rot(V(0.8f, 0.85f + rear)), Rot(V(0.67f, 0.6f + rear)), 0.035f, dark); // mane
        // Saddle cloth in the team's gear colour
        p.OutlinedPolygon(new[] { Rot(V(0.4f, 0.6f)), Rot(V(0.6f, 0.6f)), Rot(V(0.58f, 0.46f)), Rot(V(0.42f, 0.46f)) }, s.gear);
        // Near legs
        p.Line(Rot(V(0.66f, 0.42f + rear)), Rot(V(0.66f + swing, 0.06f + rear)), 0.045f, coat);
        p.Line(Rot(V(0.28f, 0.42f)), Rot(V(0.28f - swing, 0.06f)), 0.045f, coat);
        p.Circle(Rot(V(0.66f + swing, 0.07f + rear)), 0.025f, dark); // hooves
        p.Circle(Rot(V(0.28f - swing, 0.07f)), 0.025f, dark);
    }

    /// <summary>A horse down on its side (canvas units), legs out; the rider has been thrown in front.</summary>
    private static void DrawFallenHorse(Painter p, WarPose pose, WarriorStyle s, float dx = 0f)
    {
        Vector2 F(float x, float y) => V(x + dx, y);
        Color coat = s.horse, dark = Color.Lerp(s.horse, Color.black, 0.45f);
        float y = pose == WarPose.Down ? 0.2f : 0.3f;
        float tilt = pose == WarPose.Down ? 0f : 0.08f;
        p.Ellipse(F(0.6f, 0.07f), F(0.48f, 0.05f), Shadow);
        // Legs sticking out from the belly
        p.Line(F(0.42f, y + 0.08f), F(0.22f, y + 0.36f), 0.04f, dark);
        p.Line(F(0.72f, y + 0.08f), F(0.62f, y + 0.4f), 0.04f, dark);
        p.OutlinedLine(F(0.2f, y + 0.02f), F(0.05f, y - 0.1f), 0.045f, dark); // tail
        var body = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < 18; i++)
        {
            float a = Mathf.PI * 2f * i / 18f;
            body.Add(F(0.55f + Mathf.Cos(a) * 0.32f, y + Mathf.Sin(a) * 0.12f + Mathf.Cos(a) * tilt));
        }
        p.OutlinedPolygon(body.ToArray(), coat);
        p.OutlinedPolygon(new[] { F(0.47f, y + 0.12f), F(0.65f, y + 0.12f), F(0.63f, y - 0.02f), F(0.49f, y - 0.02f) }, s.gear);
        // Neck and head resting on the ground
        p.OutlinedPolygon(new[] { F(0.8f, y + 0.08f), F(1.0f, 0.16f), F(1.12f, 0.1f), F(1.1f, 0.04f), F(0.95f, 0.06f), F(0.8f, y - 0.06f) }, coat);
        p.Line(F(0.82f, y + 0.08f), F(1.0f, 0.17f), 0.03f, dark);
        p.Line(F(0.48f, y - 0.06f), F(0.36f, y - 0.16f > 0.06f ? y - 0.16f : 0.06f), 0.045f, coat);
        p.Line(F(0.66f, y - 0.06f), F(0.8f, 0.06f), 0.045f, coat);
    }

    /// <summary>A chariot's horse and pole, ahead of the car (drawn before the charioteer).</summary>
    private static void DrawChariotTeam(Painter p, WarPose pose, WarriorStyle s, System.Func<Vector2, Vector2> Rot)
    {
        p.OutlinedLine(Rot(V(0.62f, 0.3f)), Rot(V(1.0f, 0.46f)), 0.03f, WoodDark);
        DrawHorse(p, pose, s, v => Rot(v + V(0.45f, 0f)));
    }

    /// <summary>The chariot car and its near wheel, in front of the charioteer's legs. The wheel turns as it rolls.</summary>
    private static void DrawChariotCar(Painter p, WarPose pose, WarriorStyle s, System.Func<Vector2, Vector2> Rot)
    {
        p.OutlinedPolygon(new[] { Rot(V(0.28f, 0.24f)), Rot(V(0.7f, 0.24f)), Rot(V(0.7f, 0.52f)), Rot(V(0.64f, 0.58f)), Rot(V(0.34f, 0.58f)), Rot(V(0.28f, 0.46f)) }, Wood);
        p.Line(Rot(V(0.3f, 0.44f)), Rot(V(0.69f, 0.44f)), 0.03f, s.gear);
        p.OutlinedLine(Rot(V(0.34f, 0.58f)), Rot(V(0.64f, 0.58f)), 0.025f, Bronze2);
        float spin = pose == WarPose.StrideA ? 0f : pose == WarPose.Pass ? 20f : pose == WarPose.StrideB ? 40f : 10f;
        Vector2 hub = Rot(V(0.44f, 0.2f));
        var bounds = new Rect(hub.x - 0.2f, hub.y - 0.2f, 0.4f, 0.4f);
        p.Draw(q => Mathf.Abs(SdCircle(q, hub, 0.135f)) - 0.03f, bounds, p.Ink);
        p.Draw(q => Mathf.Abs(SdCircle(q, hub, 0.135f)) - 0.018f, bounds, WoodDark);
        for (int k = 0; k < 6; k++)
        {
            float a = (spin + k * 60f) * Mathf.Deg2Rad;
            p.Line(hub, hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.13f, 0.014f, WoodDark);
        }
        p.OutlinedCircle(hub, 0.03f, Bronze2);
    }

    /// <summary>A wrecked chariot (canvas units): the horse down, the car tipped, a wheel off.</summary>
    private static void DrawChariotWreck(Painter p, WarPose pose, WarriorStyle s, float baseShift)
    {
        DrawFallenHorse(p, pose, s, baseShift + 0.15f);
        p.OutlinedEllipse(V(baseShift - 0.05f, 0.1f), V(0.15f, 0.05f), WoodDark);
        p.OutlinedPolygon(new[] { V(baseShift + 0.25f, 0.07f), V(baseShift + 0.65f, 0.09f), V(baseShift + 0.7f, 0.33f), V(baseShift + 0.32f, 0.38f) }, Wood);
        p.Line(V(baseShift + 0.28f, 0.2f), V(baseShift + 0.67f, 0.2f), 0.03f, s.gear);
    }

    /// <summary>A war elephant facing right, under its rider. Legs swing with the walk; the trunk rears on the attack.</summary>
    private static void DrawElephant(Painter p, WarPose pose, WarriorStyle s, System.Func<Vector2, Vector2> Rot)
    {
        Color coat = s.horse, dark = Color.Lerp(s.horse, Color.black, 0.35f), ivory = new Color(0.97f, 0.94f, 0.85f);
        float swing = pose == WarPose.StrideA ? 0.07f : pose == WarPose.StrideB ? -0.07f : 0f;
        float o = p.Outline;
        // Far legs and tail
        p.OutlinedLine(Rot(V(0.3f, 0.5f)), Rot(V(0.3f + swing, 0.1f)), 0.1f, dark);
        p.OutlinedLine(Rot(V(0.72f, 0.5f)), Rot(V(0.72f - swing, 0.1f)), 0.1f, dark);
        p.Line(Rot(V(0.1f, 0.72f)), Rot(V(0.05f, 0.46f)), 0.025f, dark);
        // Body
        var body = new List<Vector2>();
        for (int i = 0; i < 24; i++)
        {
            float a = Mathf.PI * 2f * i / 24f;
            body.Add(Rot(V(0.5f + Mathf.Cos(a) * 0.42f, 0.68f + Mathf.Sin(a) * 0.28f)));
        }
        p.OutlinedPolygon(body.ToArray(), coat);
        // Saddle cloth in the team colour with gold trim
        p.OutlinedPolygon(new[] { Rot(V(0.3f, 0.95f)), Rot(V(0.68f, 0.95f)), Rot(V(0.72f, 0.62f)), Rot(V(0.26f, 0.62f)) }, s.gear);
        p.Line(Rot(V(0.27f, 0.65f)), Rot(V(0.71f, 0.65f)), 0.025f, Gold);
        // Trunk (raised to trumpet on the attack), head over its root
        bool rearing = pose == WarPose.Raised || pose == WarPose.Strike;
        var trunk = rearing
            ? new[] { V(1.0f, 0.78f), V(1.14f, 0.88f), V(1.22f, 1.0f), V(1.18f, 1.1f) }
            : new[] { V(1.0f, 0.74f), V(1.1f, 0.52f), V(1.08f, 0.3f), V(1.15f, 0.16f) };
        for (int i = 0; i < 3; i++)
        {
            p.Line(Rot(trunk[i]), Rot(trunk[i + 1]), 0.08f - i * 0.013f + o * 2f, p.Ink);
        }
        var headPts = new List<Vector2>();
        for (int i = 0; i < 18; i++)
        {
            float a = Mathf.PI * 2f * i / 18f;
            headPts.Add(Rot(V(0.9f + Mathf.Cos(a) * 0.18f, 0.8f + Mathf.Sin(a) * 0.17f)));
        }
        p.OutlinedPolygon(headPts.ToArray(), coat);
        for (int i = 0; i < 3; i++)
        {
            p.Line(Rot(trunk[i]), Rot(trunk[i + 1]), 0.08f - i * 0.013f, coat);
        }
        p.OutlinedPolygon(new[] { Rot(V(0.98f, 0.66f)), Rot(V(1.16f, 0.56f)), Rot(V(1.22f, 0.62f)), Rot(V(1.0f, 0.71f)) }, ivory);
        var ear = new List<Vector2>();
        for (int i = 0; i < 14; i++)
        {
            float a = Mathf.PI * 2f * i / 14f;
            ear.Add(Rot(V(0.83f + Mathf.Cos(a) * 0.09f, 0.77f + Mathf.Sin(a) * 0.15f)));
        }
        p.OutlinedPolygon(ear.ToArray(), Color.Lerp(coat, dark, 0.35f));
        p.Circle(Rot(V(0.97f, 0.86f)), 0.016f, p.Ink);
        // Near legs with toenails
        p.OutlinedLine(Rot(V(0.36f, 0.5f)), Rot(V(0.36f - swing, 0.1f)), 0.11f, coat);
        p.OutlinedLine(Rot(V(0.78f, 0.5f)), Rot(V(0.78f + swing, 0.1f)), 0.11f, coat);
        foreach (float x in new[] { 0.36f - swing, 0.78f + swing })
        {
            p.Circle(Rot(V(x + 0.03f, 0.08f)), 0.018f, ivory);
        }
    }

    /// <summary>A war elephant collapsed on its side (canvas units); the rider has been thrown in front.</summary>
    private static void DrawFallenElephant(Painter p, WarPose pose, WarriorStyle s, float dx)
    {
        Vector2 F(float x, float y) => V(x + dx, y);
        Color coat = s.horse, dark = Color.Lerp(s.horse, Color.black, 0.35f), ivory = new Color(0.97f, 0.94f, 0.85f);
        float y = pose == WarPose.Down ? 0.3f : 0.38f;
        p.Ellipse(F(0.85f, 0.07f), V(0.65f, 0.05f), Shadow);
        p.OutlinedLine(F(0.45f, y + 0.1f), F(0.3f, y + 0.42f), 0.1f, dark);
        p.OutlinedLine(F(0.95f, y + 0.1f), F(0.88f, y + 0.46f), 0.1f, dark);
        var body = new List<Vector2>();
        for (int i = 0; i < 24; i++)
        {
            float a = Mathf.PI * 2f * i / 24f;
            body.Add(F(0.75f + Mathf.Cos(a) * 0.45f, y + Mathf.Sin(a) * 0.26f));
        }
        p.OutlinedPolygon(body.ToArray(), coat);
        p.OutlinedPolygon(new[] { F(0.55f, y + 0.18f), F(0.92f, y + 0.18f), F(0.9f, y - 0.05f), F(0.57f, y - 0.05f) }, s.gear);
        p.OutlinedLine(F(1.36f, 0.18f), F(1.55f, 0.1f), 0.06f, coat);
        p.OutlinedLine(F(1.55f, 0.1f), F(1.72f, 0.09f), 0.05f, coat);
        p.OutlinedCircle(F(1.28f, 0.22f), 0.17f, coat);
        p.OutlinedLine(F(1.36f, 0.14f), F(1.52f, 0.24f), 0.035f, ivory);
        p.OutlinedEllipse(F(1.2f, 0.24f), V(0.08f, 0.13f), Color.Lerp(coat, dark, 0.35f));
        p.InkLine(F(1.31f, 0.28f), F(1.37f, 0.28f));
        p.OutlinedLine(F(0.5f, y - 0.1f), F(0.42f, 0.07f), 0.1f, coat);
        p.OutlinedLine(F(1.0f, y - 0.1f), F(1.1f, 0.07f), 0.1f, coat);
    }

    /// <summary>A crewman working a ballista mounted on the parapet: wind, aim, loose.</summary>
    private static void DrawBallista(Painter p, WarPose pose, WarriorStyle s, Vector2 shoulder, float limb, System.Func<Vector2, Vector2> R)
    {
        // How far the string is drawn back, and whether a bolt is loaded.
        float drawX; bool loaded;
        switch (pose)
        {
            case WarPose.Windup: drawX = 0.78f; loaded = false; break;
            case WarPose.Strike: drawX = 1.0f; loaded = false; break;
            case WarPose.Recover: drawX = 0.92f; loaded = false; break;
            default: drawX = 0.64f; loaded = true; break; // idle and aimed
        }
        bool released = pose == WarPose.Strike || pose == WarPose.Recover;
        // Stand and stock
        p.OutlinedLine(R(V(0.86f, 0.3f)), R(V(0.86f, 0.52f)), 0.05f, WoodDark);
        p.OutlinedLine(R(V(0.56f, 0.52f)), R(V(1.18f, 0.56f)), 0.055f, Wood);
        // Torsion frame and arms
        p.OutlinedBox(0.98f + 0.3f, 0.44f, 1.08f + 0.3f, 0.68f, 0.01f, WoodDark);
        Vector2 frame = V(1.03f, 0.56f);
        Vector2 armTop = released ? V(1.0f, 0.86f) : V(0.86f, 0.8f), armBottom = released ? V(1.0f, 0.26f) : V(0.86f, 0.32f);
        p.OutlinedLine(R(frame + V(0, 0.1f)), R(armTop), 0.028f, Wood);
        p.OutlinedLine(R(frame - V(0, 0.1f)), R(armBottom), 0.028f, Wood);
        Vector2 nock = V(drawX, 0.56f);
        p.Line(R(armTop), R(nock), 0.01f, new Color(0.9f, 0.88f, 0.8f));
        p.Line(R(nock), R(armBottom), 0.01f, new Color(0.9f, 0.88f, 0.8f));
        if (loaded)
        {
            p.OutlinedLine(R(nock), R(nock + V(0.46f, 0)), 0.022f, Wood);
            p.OutlinedPolygon(new[] { R(nock + V(0.44f, -0.03f)), R(nock + V(0.54f, 0)), R(nock + V(0.44f, 0.03f)) }, Iron);
        }
        // The crewman's hands: on the winch while winding, on the trigger otherwise
        Vector2 handA = pose == WarPose.Windup ? V(0.6f, 0.6f) : V(0.62f, 0.56f);
        Vector2 handB = pose == WarPose.Windup ? V(0.6f, 0.48f) : V(0.7f, 0.52f);
        p.Line(R(shoulder), R(handA), limb, s.ink);
        p.Line(R(shoulder), R(handB), limb, s.ink);
        p.OutlinedCircle(R(V(0.6f, 0.54f)), 0.035f, WoodLight); // winch
    }
}
