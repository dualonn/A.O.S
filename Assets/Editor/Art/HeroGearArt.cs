using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// How heroes show their gear. Every player unit gets a bare copy of its frames (no helmet,
/// shield or melee weapon) plus, per frame, where the head, chest, hands and weapon are. Each
/// gear look is one small sprite pivoted on its anchor, with a white "trim" sprite the game tints
/// by rarity. So any mix of gear follows every animation frame without baking every combination.
/// (Tools > Age of Stickman > Generate Hero Gear Art.)
/// </summary>
public static partial class PlaceholderArt
{
    private const string WornDir = "Units/HeroGear";

    /// <summary>Redraw every unit (and its hero rig) and the worn gear, without touching other art.</summary>
    [MenuItem("Tools/Age of Stickman/Generate Unit and Hero Art")]
    public static void GenerateUnitAndHeroArt()
    {
        GenerateWornGear();
        GenerateWarriorsAndCrews(AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset"));
        AssetDatabase.SaveAssets();
        Debug.Log("Unit and hero art generated.");
    }

    /// <summary>Bare hero frames, gear anchors and default looks for one unit.</summary>
    private static void BuildHeroRig(WarriorStyle style, SpriteUnitVisual visual)
    {
        string dir = $"Units/{style.name}";
        Directory.CreateDirectory(Path.Combine(Root, dir));
        var anchors = new List<GearAnchor>();
        Sprite Frame(string frame, WarPose pose)
        {
            var p = WarriorCanvas(style);
            DrawWarrior(p, pose, style, true, a => anchors.Add(a));
            return Save(p, $"{dir}/{style.name}_Hero_{frame}.png", 160, WarriorPivot(style));
        }
        visual.heroIdle = Frame("Idle", WarPose.Idle);
        visual.heroWalk = new List<Sprite> { Frame("Walk0", WarPose.StrideA), Frame("Walk1", WarPose.Pass), Frame("Walk2", WarPose.StrideB), Frame("Walk3", WarPose.Pass) };
        visual.heroAttack = new List<Sprite> { Frame("Attack0", WarPose.Windup), Frame("Attack1", WarPose.Raised), Frame("Attack2", WarPose.Strike), Frame("Attack3", WarPose.Recover) };
        visual.heroDeath = new List<Sprite> { Frame("Death0", WarPose.Stagger), Frame("Death1", WarPose.Falling), Frame("Death2", WarPose.Down) };
        visual.anchors = anchors;
        visual.weaponClass = ClassOf(style.weapon);
        visual.defaultHead = style.helm == Helm.BronzeDome ? GearLook.DomeHelm : style.helm == Helm.IronCone ? GearLook.ConeHelm
            : style.helm == Helm.Galea ? GearLook.CrestedHelm : GearLook.None;
        visual.defaultShield = style.shield == ShieldType.Hide ? GearLook.HideShield : style.shield == ShieldType.Pelta ? GearLook.WickerShield
            : style.shield == ShieldType.Scutum ? GearLook.TowerShield : style.shield == ShieldType.None ? GearLook.None : GearLook.RoundShield;
        visual.defaultWeapon = style.weapon == Weapon.Club ? GearLook.Club : style.weapon == Weapon.Spear ? GearLook.Spear
            : style.weapon == Weapon.Axe ? GearLook.Axe : style.weapon == Weapon.Sword ? (style.bladeLength < 0.3f ? GearLook.ShortSword : GearLook.Sword)
            : GearLook.None;
        EditorUtility.SetDirty(visual);
    }

    // ---------- Worn gear pieces ----------

    private static void GenerateWornGear()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        Directory.CreateDirectory(Path.Combine(Root, WornDir));
        int count = System.Enum.GetValues(typeof(GearLook)).Length;
        art.wornGear = new List<Sprite>(new Sprite[count]);
        art.wornGearTrim = new List<Sprite>(new Sprite[count]);
        for (int i = 1; i < count; i++)
        {
            var look = (GearLook)i;
            if (look == GearLook.Bow) continue; // bows stay part of an archer's own frames
            art.wornGear[i] = WornPiece(look, false);
            art.wornGearTrim[i] = WornPiece(look, true);
        }
        EditorUtility.SetDirty(art);
    }

    /// <summary>
    /// One look as a sprite pivoted on its anchor. Head, body and trinket pieces are drawn in the
    /// warrior's own coordinates (head at 0.5, 0.77...); weapons lie along +x from the grip; shields
    /// are centred on the hand.
    /// </summary>
    private static Sprite WornPiece(GearLook look, bool trim)
    {
        Vector2 size, pivot, figureAnchor;
        switch (look)
        {
            case GearLook.Cap: case GearLook.Hood: case GearLook.DomeHelm: case GearLook.ConeHelm: case GearLook.CrestedHelm: case GearLook.Wreath:
                size = V(0.6f, 0.6f); pivot = V(0.3f, 0.26f); figureAnchor = V(0.5f, 0.77f); break;
            case GearLook.HideVest: case GearLook.Jerkin: case GearLook.Scale: case GearLook.Mail: case GearLook.Plate:
                size = V(0.4f, 0.44f); pivot = V(0.2f, 0.2f); figureAnchor = V(0.5f, 0.52f); break;
            case GearLook.Charm: case GearLook.Amulet:
                size = V(0.3f, 0.3f); pivot = V(0.15f, 0.2f); figureAnchor = V(0.5f, 0.64f); break;
            case GearLook.Horn:
                size = V(0.4f, 0.4f); pivot = V(0.25f, 0.25f); figureAnchor = V(0.5f, 0.385f); break;
            case GearLook.Ring:
                size = V(0.12f, 0.12f); pivot = V(0.06f, 0.06f); figureAnchor = Vector2.zero; break;
            case GearLook.HideShield: case GearLook.WickerShield: case GearLook.RoundShield: case GearLook.TowerShield:
                size = V(0.34f, 0.56f); pivot = V(0.17f, 0.3f); figureAnchor = Vector2.zero; break;
            default: // weapons
                size = V(1.15f, 0.36f); pivot = V(0.38f, 0.18f); figureAnchor = Vector2.zero; break;
        }
        var p = new Painter(size.x, size.y, 160) { Outline = 0.018f, Origin = pivot - figureAnchor };
        DrawWorn(p, look, trim);
        string name = $"Worn_{look}{(trim ? "_Trim" : "")}";
        return Save(p, $"{WornDir}/{name}.png", 160, new Vector2(pivot.x / size.x, pivot.y / size.y));
    }

    private static readonly Color Leather = new Color(0.5f, 0.35f, 0.22f);
    private static readonly Color Fur = new Color(0.58f, 0.56f, 0.53f);
    private static readonly Color Quilt = new Color(0.74f, 0.64f, 0.46f);
    private static readonly Color Ivory = new Color(0.95f, 0.92f, 0.82f);
    private static readonly Color Amber = new Color(0.95f, 0.6f, 0.15f);
    private static readonly Color Laurel = new Color(0.36f, 0.62f, 0.27f);
    private static readonly Color HeroBlue = new Color(0.33f, 0.48f, 0.85f);

    /// <summary>
    /// Draw a look. The main pass draws the whole piece; the trim pass draws only its accents
    /// (rim, crest, guard, gem) in white, for the game to tint by rarity.
    /// </summary>
    private static void DrawWorn(Painter p, GearLook look, bool trim)
    {
        bool main = !trim;
        Color A(Color c) => trim ? Color.white : c;
        System.Func<Vector2, Vector2> same = v => v;
        Vector2 h = V(0.5f, 0.77f);
        switch (look)
        {
            // ----- Head -----
            case GearLook.Cap:
                if (main) p.OutlinedPolygon(Dome(h + V(0, -0.005f), 0.095f, 0.09f), Leather);
                p.OutlinedLine(h + V(-0.1f, -0.005f), h + V(0.1f, -0.005f), 0.026f, A(Color.Lerp(Leather, Color.black, 0.3f)));
                break;
            case GearLook.Hood:
                if (main)
                {
                    // Wolf pelt over the head and down the back, ears up.
                    p.OutlinedPolygon(new[] { h + V(-0.02f, 0.06f), h + V(-0.12f, -0.02f), h + V(-0.17f, -0.17f), h + V(-0.09f, -0.13f), h + V(-0.05f, -0.03f) }, Fur);
                    p.OutlinedPolygon(new[] { h + V(-0.06f, 0.07f), h + V(-0.03f, 0.17f), h + V(0.01f, 0.08f) }, Fur);
                    p.OutlinedPolygon(new[] { h + V(0.01f, 0.08f), h + V(0.05f, 0.16f), h + V(0.07f, 0.06f) }, Fur);
                    p.OutlinedPolygon(Dome(h, 0.105f, 0.1f), Fur);
                }
                // Fangs along the brow
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.02f + i * 0.04f;
                    p.OutlinedPolygon(new[] { h + V(x - 0.014f, 0.01f), h + V(x + 0.014f, 0.01f), h + V(x, -0.025f) }, A(Ivory));
                }
                break;
            case GearLook.DomeHelm:
            case GearLook.ConeHelm:
            case GearLook.CrestedHelm:
            {
                var style = new WarriorStyle
                {
                    helm = look == GearLook.DomeHelm ? Helm.BronzeDome : look == GearLook.ConeHelm ? Helm.IronCone : Helm.Galea,
                    crest = look == GearLook.CrestedHelm ? new Color(0.85f, 0.2f, 0.18f) : look == GearLook.DomeHelm ? PlayerGear : new Color(0, 0, 0, 0),
                    ink = PlayerInk,
                };
                if (main) DrawHelm(p, style, h, same);
                // Trim: the crest, or the brow band of a crestless helm.
                if (look == GearLook.CrestedHelm) p.OutlinedLine(h + V(-0.08f, 0.11f), h + V(0.07f, 0.12f), 0.05f, A(style.crest));
                else if (look == GearLook.DomeHelm) p.OutlinedLine(h + V(-0.06f, 0.1f), h + V(0.06f, 0.12f), 0.04f, A(style.crest));
                else p.OutlinedLine(h + V(-0.1f, 0.01f), h + V(0.1f, 0.01f), 0.022f, A(IronDark));
                break;
            }
            case GearLook.Wreath:
                // Laurel leaves round the brow, tied at the back with a ribbon.
                if (main)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float x = -0.1f + i * 0.033f;
                        p.OutlinedEllipse(h + V(x, 0.03f + Mathf.Sin(i * 1.7f) * 0.008f), V(0.022f, 0.012f), Laurel);
                    }
                }
                p.Line(h + V(-0.1f, 0.03f), h + V(-0.16f, -0.04f), 0.016f, A(Gold));
                p.Line(h + V(-0.1f, 0.03f), h + V(-0.14f, -0.07f), 0.016f, A(Gold));
                break;

            // ----- Body (worn over the chest, from the belt to the shoulders) -----
            case GearLook.HideVest:
            case GearLook.Jerkin:
            case GearLook.Scale:
            case GearLook.Mail:
            case GearLook.Plate:
            {
                var vest = new[] { V(0.44f, 0.395f), V(0.56f, 0.395f), V(0.575f, 0.6f), V(0.545f, 0.665f), V(0.455f, 0.665f), V(0.425f, 0.6f) };
                Color fill = look == GearLook.HideVest ? Hide : look == GearLook.Jerkin ? Quilt : look == GearLook.Scale ? Bronze2 : Iron;
                if (main)
                {
                    p.OutlinedPolygon(vest, fill);
                    Color dark = Color.Lerp(fill, Color.black, 0.35f);
                    switch (look)
                    {
                        case GearLook.HideVest:
                            p.Circle(V(0.475f, 0.56f), 0.014f, WoodDark);
                            p.Circle(V(0.525f, 0.47f), 0.016f, WoodDark);
                            break;
                        case GearLook.Jerkin:
                            for (float y = 0.44f; y < 0.64f; y += 0.05f) p.Line(V(0.435f, y), V(0.565f, y), 0.007f, dark);
                            p.Line(V(0.5f, 0.4f), V(0.5f, 0.66f), 0.007f, dark);
                            break;
                        case GearLook.Scale:
                            for (float y = 0.43f; y < 0.64f; y += 0.04f)
                                for (float x = 0.45f; x < 0.56f; x += 0.03f)
                                    p.Circle(V(x + (Mathf.RoundToInt(y * 25) % 2) * 0.015f, y), 0.008f, dark);
                            break;
                        case GearLook.Mail:
                            for (float y = 0.42f; y < 0.65f; y += 0.03f)
                                for (float x = 0.445f; x < 0.56f; x += 0.025f)
                                    p.Circle(V(x, y), 0.005f, dark);
                            break;
                        case GearLook.Plate:
                            for (float y = 0.45f; y < 0.62f; y += 0.045f) p.Line(V(0.43f, y), V(0.57f, y), 0.009f, dark);
                            p.OutlinedEllipse(V(0.44f, 0.64f), V(0.035f, 0.025f), fill); // shoulder plates
                            p.OutlinedEllipse(V(0.56f, 0.64f), V(0.035f, 0.025f), fill);
                            break;
                    }
                }
                // Trim: the hem and collar (brass edging, a leather cord on hide).
                Color edge = look == GearLook.HideVest ? Leather : look == GearLook.Jerkin ? Leather : Gold;
                p.Line(V(0.44f, 0.4f), V(0.56f, 0.4f), 0.016f, A(edge));
                p.Line(V(0.46f, 0.66f), V(0.54f, 0.66f), 0.014f, A(edge));
                break;
            }

            // ----- Weapons (lying along +x from the grip) -----
            case GearLook.Club:
                if (main)
                {
                    p.OutlinedLine(V(0, 0), V(0.25f, 0.01f), 0.035f, WoodLight);
                    p.OutlinedLine(V(0.2f, 0.01f), V(0.42f, 0.02f), 0.075f, Wood);
                    p.Circle(V(0.33f, 0.03f), 0.012f, WoodDark);
                    p.Circle(V(0.38f, 0.0f), 0.012f, WoodDark);
                }
                p.Line(V(-0.02f, 0), V(0.06f, 0), 0.045f, A(PlayerGear)); // wrapped grip
                break;
            case GearLook.Sword:
            case GearLook.ShortSword:
            {
                bool gladius = look == GearLook.ShortSword;
                float len = gladius ? 0.28f : 0.36f, w = gladius ? 0.026f : 0.022f;
                if (main)
                {
                    p.OutlinedLine(V(-0.06f, 0), V(0, 0), 0.03f, WoodDark);
                    p.OutlinedPolygon(new[] { V(0, w), V(len, w * 0.75f), V(len + 0.05f, 0), V(len, -w * 0.75f), V(0, -w) }, Iron);
                    p.Line(V(0.03f, 0), V(len - 0.02f, 0), 0.006f, IronDark); // fuller
                }
                p.OutlinedLine(V(0.01f, 0.055f), V(0.01f, -0.055f), 0.022f, A(gladius ? Gold : IronDark));
                p.OutlinedCircle(V(-0.07f, 0), 0.018f, A(gladius ? Gold : IronDark));
                break;
            }
            case GearLook.Axe:
            {
                float end = 0.42f;
                if (main)
                {
                    p.OutlinedLine(V(-0.06f, 0), V(end + 0.03f, 0), 0.03f, WoodLight);
                    p.OutlinedPolygon(new[] { V(end + 0.02f, 0), V(end + 0.05f, 0.11f), V(end - 0.11f, 0.12f), V(end - 0.08f, 0) }, Bronze2);
                }
                p.Line(V(end + 0.045f, 0.105f), V(end - 0.1f, 0.115f), 0.018f, A(Color.Lerp(Bronze2, Color.white, 0.4f))); // the edge
                break;
            }
            case GearLook.Spear:
                if (main)
                {
                    p.OutlinedLine(V(-0.3f, 0), V(0.55f, 0), 0.03f, WoodLight);
                    p.OutlinedPolygon(new[] { V(0.55f, -0.05f), V(0.69f, 0), V(0.55f, 0.05f) }, Iron);
                }
                p.Line(V(0.5f, 0), V(0.56f, 0), 0.042f, A(PlayerGear)); // binding below the head
                break;

            // ----- Shields (centred on the hand) -----
            case GearLook.HideShield:
                if (main) DrawShield(p, new WarriorStyle { shield = ShieldType.Hide }, Vector2.zero, same);
                p.Line(V(-0.085f, -0.19f), V(-0.085f, 0.12f), 0.016f, A(Wood));
                p.Line(V(0.085f, -0.19f), V(0.085f, 0.12f), 0.016f, A(Wood));
                break;
            case GearLook.WickerShield:
                if (main) DrawShield(p, new WarriorStyle { shield = ShieldType.Pelta, shieldColor = new Color(0.82f, 0.68f, 0.42f) }, Vector2.zero, same);
                p.Line(V(-0.07f, -0.04f), V(0.07f, -0.04f), 0.014f, A(WoodDark));
                p.Line(V(-0.06f, -0.08f), V(0.06f, -0.08f), 0.014f, A(WoodDark));
                break;
            case GearLook.RoundShield:
                if (main) DrawShield(p, new WarriorStyle { shield = ShieldType.Round, shieldColor = HeroBlue }, Vector2.zero, same);
                p.Draw(q => Mathf.Abs(SdCircle(q, Vector2.zero, 0.115f)) - 0.012f, new Rect(-0.14f, -0.14f, 0.28f, 0.28f), A(IronDark));
                p.OutlinedCircle(Vector2.zero, 0.038f, A(Iron));
                break;
            case GearLook.TowerShield:
                if (main) DrawShield(p, new WarriorStyle { shield = ShieldType.Scutum, shieldColor = HeroBlue }, Vector2.zero, same);
                p.Line(V(-0.08f, 0.06f), V(0.08f, 0.1f), 0.014f, A(Gold));
                p.Line(V(-0.08f, -0.08f), V(0.08f, -0.12f), 0.014f, A(Gold));
                p.OutlinedCircle(Vector2.zero, 0.04f, A(Gold));
                break;

            // ----- Trinkets -----
            case GearLook.Charm:
                if (main)
                {
                    p.Line(V(0.46f, 0.645f), V(0.5f, 0.6f), 0.01f, Leather);
                    p.Line(V(0.54f, 0.645f), V(0.5f, 0.6f), 0.01f, Leather);
                }
                p.OutlinedPolygon(new[] { V(0.488f, 0.605f), V(0.512f, 0.605f), V(0.5f, 0.55f) }, A(Ivory)); // a big fang
                break;
            case GearLook.Amulet:
                p.Line(V(0.46f, 0.645f), V(0.5f, 0.6f), 0.01f, A(Gold));
                p.Line(V(0.54f, 0.645f), V(0.5f, 0.6f), 0.01f, A(Gold));
                if (main) p.OutlinedCircle(V(0.5f, 0.585f), 0.024f, Amber);
                break;
            case GearLook.Horn:
                // A curved war horn slung at the hip.
                if (main)
                {
                    var horn = new List<Vector2>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f, a = Mathf.Lerp(200f, 300f, t) * Mathf.Deg2Rad, r = 0.11f;
                        horn.Add(V(0.47f, 0.42f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 0.025f * (1 - t)));
                    }
                    for (int i = 8; i >= 0; i--)
                    {
                        float t = i / 8f, a = Mathf.Lerp(200f, 300f, t) * Mathf.Deg2Rad, r = 0.11f;
                        horn.Add(V(0.47f, 0.42f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 0.025f * (1 - t) - 0.004f));
                    }
                    p.OutlinedPolygon(horn, Ivory);
                }
                p.Line(V(0.35f, 0.39f), V(0.385f, 0.36f), 0.02f, A(Gold));
                p.Line(V(0.43f, 0.32f), V(0.45f, 0.29f), 0.016f, A(Gold));
                break;
            case GearLook.Ring:
                if (main) p.Draw(q => Mathf.Abs(SdCircle(q, Vector2.zero, 0.022f)) - 0.008f, new Rect(-0.04f, -0.04f, 0.08f, 0.08f), Gold);
                p.OutlinedCircle(V(0, 0.024f), 0.013f, A(new Color(0.75f, 0.85f, 1f)));
                break;
        }
    }

    /// <summary>A half-dome over a point (a cap or hood top) as a polygon.</summary>
    private static Vector2[] Dome(Vector2 c, float rx, float ry)
    {
        var pts = new Vector2[10];
        for (int i = 0; i <= 8; i++)
        {
            float a = Mathf.PI * i / 8f;
            pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        pts[9] = c + V(-rx, -0.01f);
        return pts;
    }
}
