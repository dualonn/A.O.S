using System;
using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;

public enum MountKind { None, Horse, Chariot, Elephant }

/// <summary>Creatures that aren't built like people.</summary>
public enum Beast { None, Wolf, Spider }

/// <summary>
/// How a fighter looks: team colour, the gear in each slot, a mount, and a few extras. Stored on
/// each unit so heroes can be redrawn in the game with their own gear.
/// </summary>
[Serializable]
public class FigureStyle
{
    [Tooltip("Team colour ramp: Blue (yours) or Red (raiders).")]
    public string team = "Blue";
    public GearLook head = GearLook.Headband;
    [Tooltip("None = a plain tunic in the team colour.")]
    public GearLook body = GearLook.None;
    public GearLook weapon = GearLook.Club;
    public GearLook shield = GearLook.None;
    public GearLook trinket = GearLook.None;
    [Tooltip("Colour ramp for crests and plumes (empty = team colour).")]
    public string crest = "";
    [Tooltip("Colour ramp for the shield face (empty = team colour).")]
    public string shieldColor = "";
    [Tooltip("Colour ramp for a cloak (empty = none).")]
    public string cape = "";
    public MountKind mount;
    [Tooltip("Size: 1 for troops, bigger for bosses.")]
    public float scale = 1f;
    public bool raider, necklace, warpaint;
    [Tooltip("Mount coat colour ramp (Horse, DarkHorse, Elephant).")]
    public string coat = "Horse";
    [Tooltip("Skin colour ramp (empty = people; Bone for skeletons, Troll...).")]
    public string skin = "";
    [Tooltip("Undead: glowing eyes and bare ribs.")]
    public bool undead;
    [Tooltip("A beast instead of a person (wolf, spider): drawn by its own rig.")]
    public Beast beast;

    /// <summary>Rarity of the gear in each slot (Head, Body, Weapon, Shield, Trinket): sets its trim colour. Runtime only.</summary>
    [NonSerialized] public Rarity[] trims;

    public FigureStyle Clone()
    {
        var c = (FigureStyle)MemberwiseClone();
        c.trims = trims != null ? (Rarity[])trims.Clone() : null;
        return c;
    }

    public WeaponClass Class => weapon switch
    {
        GearLook.Bow => WeaponClass.Bow,
        GearLook.Sling or GearLook.Javelin => WeaponClass.Thrown,
        GearLook.Ballista => WeaponClass.None,
        _ => WeaponClass.Melee,
    };
}

/// <summary>
/// Draws fighters in pixel art, frame by frame, back to front so everything sits where it
/// physically is (facing right, the near side is the figure's left):
/// cloak, quiver -> far arm and the weapon in that hand -> far leg -> mount -> near leg ->
/// body and armour -> head and helmet -> near arm with its shield or bow.
/// </summary>
public static class PixelFigure
{
    /// <summary>12 frames: idle, walk x4, attack x4, death x3 (stagger, falling, down).</summary>
    public const int FrameCount = 12;

    private struct Pose
    {
        public float bob, breath;
        public Vector2 fNear, fFar;     // feet: x offset, lift
        public Vector2 hNear, hFar;     // hands, from the shoulders
        public float wa;                // weapon angle (degrees, 0 = forward)
        public bool attack, smear, thrust, release, loaded;
        public int death;               // 0 alive, 1 stagger, 2 falling, 3 down
    }

    // ---------- Canvas size ----------

    /// <summary>Canvas size, and where the feet are (x centre column, ground row).</summary>
    public static void CanvasFor(FigureStyle s, out int w, out int h, out int cx, out int gy)
    {
        float S = s.scale;
        if (s.beast != Beast.None)
        {
            w = Mathf.CeilToInt(48 * S); h = Mathf.CeilToInt(36 * S); cx = Mathf.RoundToInt(23 * S); gy = 4;
            return;
        }
        switch (s.mount)
        {
            case MountKind.Horse: w = 72; h = 60; cx = 34; gy = 4; break;
            case MountKind.Chariot: w = 96; h = 60; cx = 48; gy = 4; break;
            case MountKind.Elephant: w = 96; h = 80; cx = 44; gy = 4; break;
            default: w = 48; h = 44; cx = 23; gy = 4; break;
        }
        w = Mathf.CeilToInt(w * S);
        h = Mathf.CeilToInt(h * S);
        cx = Mathf.RoundToInt(cx * S);
    }

    /// <summary>The sprite pivot (normalised) that puts the feet on the unit's position.</summary>
    public static Vector2 PivotFor(FigureStyle s)
    {
        CanvasFor(s, out int w, out int h, out int cx, out int gy);
        return new Vector2((cx + 0.5f) / w, gy / (float)h);
    }

    // ---------- Poses ----------

    private static Pose PoseFor(FigureStyle s, int frame)
    {
        var p = new Pose { fNear = V(2, 0), fFar = V(-2, 0), wa = 70 };
        var cls = s.Class;
        bool shield = s.shield != GearLook.None;
        // Rest: the weapon hand (far) holds it up at the side; the near hand holds the shield in front.
        p.hFar = V(3, -5);
        p.hNear = shield ? V(4, -4) : V(-1, -6);
        if (s.weapon == GearLook.Spear || s.weapon == GearLook.Javelin) p.wa = 84;
        if (s.weapon == GearLook.Club && s.scale > 1.3f) p.wa = 40;
        if (cls == WeaponClass.Bow) { p.hNear = V(4, -6); p.hFar = V(-1, -6); }
        if (s.weapon == GearLook.Sling) p.hFar = V(2, -6);
        if (s.weapon == GearLook.Ballista) { p.hNear = V(5, -3); p.hFar = V(4, -2); }

        switch (frame)
        {
            case 1: p.fNear = V(3, 0); p.fFar = V(-3, 0); Swing(ref p, shield, 1); break;
            case 2: p.bob = 1; p.fNear = V(1, 0); p.fFar = V(-1, 1); break;
            case 3: p.fNear = V(-3, 0); p.fFar = V(3, 0); Swing(ref p, shield, -1); break;
            case 4: p.bob = 1; p.fNear = V(-1, 1); p.fFar = V(1, 0); break;
            case 5: case 6: case 7: case 8:
                Attack(ref p, s, frame - 5);
                break;
            case 9: p.death = 1; p.hFar = V(1, 3); p.hNear = V(4, 2); p.fNear = V(3, 0); p.fFar = V(-2, 0); break;
            case 10: p.death = 2; p.hFar = V(-2, 4); p.hNear = V(3, 4); break;
            case 11: p.death = 3; p.hFar = V(-3, 2); p.hNear = V(2, 3); break;
        }
        return p;
    }

    private static void Swing(ref Pose p, bool shield, int dir)
    {
        // Free arms swing with the stride; held things stay steady.
        if (!shield && p.hNear.x < 2) p.hNear = V(-1 + 2 * dir, -6);
        p.wa += dir * 3;
    }

    private static void Attack(ref Pose p, FigureStyle s, int i)
    {
        p.attack = true;
        p.fNear = V(3 + (i == 2 ? 1 : 0), 0);
        p.fFar = V(-3, 0);
        p.bob = i == 1 ? 1 : 0;
        bool shield = s.shield != GearLook.None;
        if (shield) p.hNear = V(5, -3);
        switch (s.Class)
        {
            case WeaponClass.Bow:
                p.hNear = i == 0 ? V(6, -1) : i == 3 ? V(5, -3) : V(6, 0);
                p.hFar = i == 0 ? V(5, -1) : i == 1 ? V(-1, 0) : i == 2 ? V(-2, 1) : V(1, -4);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Thrown when s.weapon == GearLook.Sling:
                p.hFar = i == 0 ? V(-1, 6) : i == 1 ? V(2, 8) : i == 2 ? V(6, 3) : V(5, -3);
                if (!shield) p.hNear = V(5, 0);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Thrown:   // javelin
                p.hFar = i == 0 ? V(-3, 3) : i == 1 ? V(-4, 4) : i == 2 ? V(6, 2) : V(4, -3);
                p.wa = i == 0 ? 10 : 15;
                if (!shield) p.hNear = V(5, 1);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.None:     // ballista crew: crank and loose
                p.hFar = i == 1 ? V(3, -1) : V(4, -2);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
        }
        if (s.weapon == GearLook.Spear)
        {
            p.hFar = i == 0 ? V(-1, -2) : i == 1 ? V(0, -1) : i == 2 ? V(6, -1) : V(4, -2);
            p.wa = i == 0 ? 4 : i == 1 ? 6 : i == 2 ? 0 : -6;
            p.thrust = i == 2;
            return;
        }
        // Swung weapons: up behind the head, over, and down in front.
        p.hFar = i == 0 ? V(-2, 3) : i == 1 ? V(0, 5) : i == 2 ? V(6, 0) : V(5, -4);
        p.wa = i == 0 ? 150 : i == 1 ? 110 : i == 2 ? -10 : -55;
        p.smear = i == 2;
    }

    // ---------- Drawing ----------

    public static PixelCanvas Draw(FigureStyle s, int frame)
    {
        CanvasFor(s, out int w, out int h, out int cx, out int gy);
        var c = new PixelCanvas(w, h);
        if (s.beast != Beast.None)
        {
            BeastRig.Draw(c, s, frame, cx, gy);
            return c;
        }
        var pose = PoseFor(s, frame);
        new Rig(c, s, pose, cx, gy).Draw();
        return c;
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    /// <summary>One frame being drawn.</summary>
    private class Rig
    {
        private readonly PixelCanvas c;
        private readonly FigureStyle s;
        private readonly Pose p;
        private readonly float S;
        private readonly int limb;
        private readonly int cx, gy;
        private readonly Color32[] team, crest, shieldRamp, skinRamp;
        private Vector2 hip, sh, head, shNear, shFar, hNear, hFar, footNear, footFar;
        private float headR;
        private bool riding, thrown;

        public Rig(PixelCanvas canvas, FigureStyle style, Pose pose, int cx, int gy)
        {
            c = canvas;
            s = style;
            p = pose;
            S = s.scale;
            limb = S < 1.4f ? 2 : 3;
            this.cx = cx;
            this.gy = gy;
            team = P.Named(s.team);
            crest = string.IsNullOrEmpty(s.crest) ? team : P.Named(s.crest);
            shieldRamp = string.IsNullOrEmpty(s.shieldColor) ? team : P.Named(s.shieldColor);
            skinRamp = string.IsNullOrEmpty(s.skin) ? P.Skin : P.Named(s.skin);
        }

        private float X(float v) => v * S;

        private Color32 Trim(GearSlot slot, Color32 normal)
        {
            if (s.trims == null || s.trims.Length <= (int)slot || s.trims[(int)slot] <= Rarity.Common) return normal;
            return P.RarityTrim(s.trims[(int)slot]);
        }

        public void Draw()
        {
            riding = s.mount != MountKind.None && p.death < 2;
            thrown = s.mount != MountKind.None && p.death >= 2;
            float lean = p.death == 1 ? -15f : p.death == 2 ? -55f : p.death == 3 ? -88f : 0f;

            // Joints (local, before leaning)
            float baseX = cx + 0.5f;
            if (riding)
            {
                hip = MountSaddle() + V(0, p.bob * 0.5f);
            }
            else
            {
                if (thrown) baseX += ThrownOffset();
                hip = V(baseX, gy + X(7) + 1 + p.bob);
            }
            sh = V(hip.x, hip.y + X(7) - p.breath);
            head = V(hip.x + X(0.3f), sh.y + X(4.1f));
            headR = X(3.4f);
            shNear = sh + V(X(1), -1);
            shFar = sh + V(X(-0.5f), 0);
            hNear = sh + p.hNear * S;
            hFar = sh + p.hFar * S;
            footNear = V(hip.x + p.fNear.x * S, gy + p.fNear.y);
            footFar = V(hip.x + p.fFar.x * S, gy + p.fFar.y);

            // Things on the ground first (a dropped weapon and shield, a fallen mount).
            if (p.death >= 2) DropGear();
            if (s.mount != MountKind.None && p.death >= 2) DrawMount(fallen: true);

            // Lean the rider/figure for death frames (round the feet, or the saddle).
            c.Pivot = riding ? hip : V(hip.x, gy);
            c.Angle = lean;
            c.Shift = p.death == 3 ? V(X(-4), X(2)) : Vector2.zero;

            DrawCape();
            if (s.weapon == GearLook.Bow) Quiver();
            if (s.trinket == GearLook.Horn) Horn();

            // Far arm and whatever that hand holds: behind the body.
            FarArm();

            if (riding)
            {
                c.Angle = 0; c.Shift = Vector2.zero;
                if (s.mount != MountKind.Chariot) FarLegRiding();
                DrawMount(fallen: false);
                c.Pivot = hip; c.Angle = lean;
                if (s.mount == MountKind.Chariot) { StandingLegs(); ChariotFront(); }
                else NearLegRiding();
            }
            else
            {
                StandingLegs();
            }

            Torso();
            Head();
            NearArm();

            c.Angle = 0; c.Shift = Vector2.zero;
            c.Outline(P.Ink, 0.6f);
            if (p.smear) Smear();
            if (p.thrust) Thrust();
            float shadowX = s.mount == MountKind.None ? cx + 1 : cx + X(s.mount == MountKind.Chariot ? 4 : 0);
            float shadowW = s.mount == MountKind.None ? X(7) : s.mount == MountKind.Elephant ? X(18) : X(s.mount == MountKind.Chariot ? 24 : 14);
            if (p.death == 3 && s.mount == MountKind.None) shadowX += X(8);
            c.Shadow(shadowX, gy, shadowW, X(1.8f));
        }

        // ---------- Body ----------

        private void StandingLegs()
        {
            Color32[] pants = s.raider ? P.Leather : P.Cloth;
            Leg(footFar, pants[1], P.Leather[1], -1);
            Leg(footNear, pants[2], P.Leather[2], 1);
        }

        private void Leg(Vector2 foot, Color32 col, Color32 boot, int side)
        {
            Vector2 hipJoint = hip + V(side * 0.6f * S, 0);
            c.Line(hipJoint, foot + V(0, 2), col, limb);
            // boot, toe pointing forward
            c.FillLocal(new Rect(foot.x - 0.5f, foot.y, limb + 1.5f, 2), _ => boot);
        }

        private void Torso()
        {
            float r = X(2.7f);
            Vector2 a = hip + V(0, 1), b = sh + V(0, -0.5f);
            var cloth = s.body switch
            {
                GearLook.Jerkin => P.Linen,
                GearLook.Scale => P.Bronze,
                GearLook.Mail or GearLook.Plate => P.Steel,
                GearLook.Bare => skinRamp,
                _ => team,
            };
            // Tunic skirt in the team colour shows below any armour.
            c.FillLocal(new Rect(hip.x - X(3.5f), hip.y - X(1.5f), X(7.5f), X(2.5f)), l =>
                l.y < hip.y + 1 ? (Color32?)(l.x < hip.x ? team[2] : team[1]) : null);
            c.Capsule(a, b, r, cloth, s.body == GearLook.Bare ? -0.45f : 0f);   // a bare chest a shade darker, so the near arm shows

            switch (s.body)
            {
                case GearLook.HideVest:
                    // Hide over the tunic, open at the front
                    c.FillLocal(new Rect(a.x - r, a.y, r * 1.6f, b.y - a.y), l =>
                        (l.x + l.y) % 5 < 1 ? P.Fur[1] : P.Fur[2]);
                    c.Line(V(a.x - r, a.y - 1), V(a.x + r * 0.6f, a.y - 1), Trim(GearSlot.Body, P.Fur[1]));
                    break;
                case GearLook.Jerkin:
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        ((int)l.x + (int)l.y) % 3 == 0 && Inside(l, a, b, r) ? (Color32?)P.Linen[1] : null);
                    break;
                case GearLook.Scale:
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        ((int)l.y % 2 == 0) && ((int)l.x + (int)l.y / 2) % 2 == 0 && Inside(l, a, b, r) ? (Color32?)P.Bronze[0] : null);
                    break;
                case GearLook.Mail:
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        ((int)l.x + (int)l.y) % 2 == 0 && Inside(l, a, b, r) ? (Color32?)P.Steel[1] : null);
                    break;
                case GearLook.Plate:
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        (int)(l.y - a.y) % 2 == 1 && Inside(l, a, b, r) ? (Color32?)P.Steel[1] : null);
                    c.Ellipse(shNear + V(0, 0.5f), X(1.8f), X(1.4f), P.Steel);   // pauldron
                    break;
                case GearLook.Bare:
                    if (s.undead)
                    {
                        // ribs and spine
                        for (float y = a.y + X(1.5f); y < b.y - 1; y += X(1.6f)) c.Line(V(a.x - r + 1, y), V(a.x + r - 1, y), P.Coal[1]);
                        c.Line(V(a.x - 0.5f, a.y), V(a.x - 0.5f, b.y), skinRamp[3]);
                    }
                    else c.Line(V(b.x - r + 1, b.y), V(a.x + r - 1, a.y + 1), P.Leather[1]);   // leather straps across a bare chest
                    break;
            }
            // hem, belt and buckle
            Color32 hem = Trim(GearSlot.Body, s.body == GearLook.None ? team[1] : cloth[1]);
            if (s.body != GearLook.None && s.body != GearLook.Bare)
                c.Line(V(a.x - r + 0.5f, a.y), V(a.x + r - 0.5f, a.y), hem);
            c.Line(V(a.x - r + 0.5f, a.y - 1), V(a.x + r - 0.5f, a.y - 1), s.raider ? team[2] : P.Leather[1]);
            c.Put(c.T(V(a.x + r - 1.5f, a.y - 0.5f)), s.raider ? team[3] : P.Gold[2]);

            if (s.necklace)
                for (int i = -1; i <= 1; i++)
                    c.Put(c.T(V(sh.x + i * X(1.2f) + 0.5f, sh.y - 1.5f - (i == 0 ? 1 : 0))), P.Bone[3]);
            if (s.trinket == GearLook.Charm)
            {
                c.Line(V(sh.x - 1, sh.y - 1), V(sh.x + 1, sh.y - 2.5f), P.Leather[1]);
                c.Put(c.T(V(sh.x + 1.5f, sh.y - 3.5f)), Trim(GearSlot.Trinket, P.Bone[3]));
                c.Put(c.T(V(sh.x + 1.5f, sh.y - 4.5f)), P.Bone[2]);
            }
            if (s.trinket == GearLook.Amulet)
            {
                c.Line(V(sh.x - 1, sh.y - 1), V(sh.x + 1, sh.y - 2.5f), P.Gold[2]);
                c.Put(c.T(V(sh.x + 1.5f, sh.y - 3.5f)), Trim(GearSlot.Trinket, P.Glow[1]));
            }
        }

        private static bool Inside(Vector2 l, Vector2 a, Vector2 b, float r)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(l - a, ab) / ab.sqrMagnitude);
            return (l - (a + ab * t)).sqrMagnitude < (r - 0.6f) * (r - 0.6f);
        }

        private void Head()
        {
            var skin = skinRamp;
            c.Ellipse(head, headR, X(3.6f), skin);
            // face (looking right): eye, brow, nose, ear
            Vector2 eye = head + V(X(1.7f), X(0.1f));
            c.Put(c.T(eye), s.undead ? P.Red[3] : P.Ink);
            if (S > 1.3f) c.Put(c.T(eye + V(0, 1)), s.undead ? P.Red[2] : P.Ink);
            if (s.undead) c.Put(c.T(eye + V(-1.6f, 0)), P.Coal[1]);   // the other socket
            c.Put(c.T(eye + V(-0.2f, X(1.3f))), s.raider ? P.Hair[1] : skin[1]);   // brow
            c.Put(c.T(head + V(headR + 0.3f, X(-0.6f))), skin[2]);                  // nose
            c.Put(c.T(head + V(X(-1.2f), X(-0.4f))), skin[1]);                     // ear
            if (s.warpaint)
            {
                c.Put(c.T(eye + V(-1, -1.2f)), P.Red[2]);
                c.Put(c.T(eye + V(0, -1.2f)), P.Red[2]);
                c.Put(c.T(eye + V(-1, -2.2f)), P.Red[1]);
            }
            Hat();
            if (s.trinket == GearLook.Wreath)
            {
                for (int i = -3; i <= 3; i++)
                {
                    Vector2 q = head + V(i * X(1.0f), X(1.4f) + (Mathf.Abs(i) == 3 ? -1 : 0));
                    c.Put(c.T(q), i % 2 == 0 ? P.Leaf[3] : P.Leaf[2]);
                }
                c.Put(c.T(head + V(X(-3.6f), X(0.6f))), Trim(GearSlot.Trinket, P.Gold[2]));
            }
        }

        private void Hair(bool wild)
        {
            if (s.undead) return;   // skulls are bald
            c.Ellipse(head + V(-0.3f, 0.4f), headR + 0.5f, X(3.6f) + 0.4f, P.Hair,
                l => l.y > head.y + X(1.1f) || l.x < head.x - X(1.3f));
            if (wild)
                for (int i = -2; i <= 2; i++)
                    c.Put(c.T(head + V(i * X(1.4f) - 0.5f, X(3.6f) + 1.2f)), P.Hair[i % 2 == 0 ? 1 : 2]);
        }

        private void Hat()
        {
            Vector2 hd = head;
            float r = headR;
            Color32 rim;
            switch (s.head)
            {
                case GearLook.None:
                    Hair(s.raider);
                    break;
                case GearLook.Bare:
                    break;   // bald (skulls, trolls)
                case GearLook.Headband:
                case GearLook.Feather:
                {
                    Hair(s.raider);
                    float y = hd.y + X(1.0f);
                    c.Line(V(hd.x - r, y), V(hd.x + r, y), team[2], S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - r - 0.5f, y), V(hd.x - r - X(3), y - X(2)), team[1]);
                    c.Line(V(hd.x - r - 0.5f, y + 0.5f), V(hd.x - r - X(3), y - X(0.6f)), team[2]);
                    if (s.head == GearLook.Feather)
                    {
                        c.Line(V(hd.x - X(1.5f), y + 1), V(hd.x - X(3), y + X(5)), P.Bone[3]);
                        c.Put(c.T(V(hd.x - X(3), y + X(5))), P.Ink);
                    }
                    break;
                }
                case GearLook.Cap:
                    Hair(false);
                    c.Ellipse(hd + V(0, 0.3f), r + 0.6f, X(3.6f) + 0.4f, P.Leather, l => l.y > hd.y + X(1.0f));
                    c.Line(V(hd.x - r - 0.5f, hd.y + X(1.0f)), V(hd.x + r + 0.8f, hd.y + X(1.0f)), Trim(GearSlot.Head, P.Leather[1]));
                    break;
                case GearLook.Hood:
                    c.Ellipse(hd + V(-0.4f, 0.4f), r + 1.0f, X(3.6f) + 0.8f, P.Wolf, l => l.y > hd.y + X(0.6f) || l.x < hd.x - X(1.0f));
                    foreach (float ex in new[] { -1.8f, 1.2f })
                    {
                        Vector2 e = hd + V(X(ex), X(4.2f));
                        c.Line(e, e + V(0, X(1.4f)), P.Wolf[2], 2);
                    }
                    c.Line(V(hd.x - X(1), hd.y + X(0.7f)), V(hd.x + r, hd.y + X(0.7f)), Trim(GearSlot.Head, P.Bone[3]));
                    c.Line(V(hd.x - r - 0.5f, hd.y - 1), V(hd.x - r - X(1.5f), hd.y - X(4)), P.Wolf[1], 2); // pelt down the back
                    break;
                case GearLook.DomeHelm:
                    Hair(false);
                    c.Ellipse(hd + V(0, 0.3f), r + 0.7f, X(3.6f) + 0.6f, P.Bronze, l => l.y > hd.y + X(0.8f));
                    rim = Trim(GearSlot.Head, P.Bronze[1]);
                    c.Line(V(hd.x - r - 0.7f, hd.y + X(0.8f)), V(hd.x + r + 0.7f, hd.y + X(0.8f)), rim);
                    c.Line(V(hd.x - X(1), hd.y + X(4.4f)), V(hd.x + X(1), hd.y + X(4.6f)), crest[2], 2);   // small crest
                    break;
                case GearLook.ConeHelm:
                    Hair(false);
                    c.Polygon(new[] { V(hd.x - r - 0.8f, hd.y + X(0.8f)), V(hd.x + r + 0.8f, hd.y + X(0.8f)), V(hd.x + X(0.3f), hd.y + X(6.0f)) },
                        l => l.x < hd.x - X(0.6f) ? P.Steel[3] : l.x < hd.x + X(1.5f) ? P.Steel[2] : P.Steel[1]);
                    rim = Trim(GearSlot.Head, P.Steel[1]);
                    c.Line(V(hd.x - r - 0.8f, hd.y + X(0.8f)), V(hd.x + r + 0.8f, hd.y + X(0.8f)), rim);
                    c.Line(V(hd.x + r - 0.3f, hd.y + X(0.8f)), V(hd.x + r - 0.3f, hd.y - X(0.8f)), P.Steel[2]);   // nasal
                    break;
                case GearLook.CrestedHelm:
                case GearLook.PlumedHelm:
                    Hair(false);
                    c.Ellipse(hd + V(0, 0.3f), r + 0.7f, X(3.6f) + 0.6f, P.Steel, l => l.y > hd.y + X(0.6f) || l.x < hd.x - X(1.8f));
                    c.Line(V(hd.x + X(0.8f), hd.y + X(0.6f)), V(hd.x + X(0.8f), hd.y - X(1.6f)), P.Steel[2], S > 1.3f ? 2 : 1);   // cheek guard
                    rim = Trim(GearSlot.Head, P.Steel[1]);
                    c.Line(V(hd.x - r - 0.7f, hd.y + X(0.6f)), V(hd.x + r + 0.5f, hd.y + X(0.6f)), rim);
                    if (s.head == GearLook.CrestedHelm)
                    {
                        // transverse crest across the top
                        for (int i = -3; i <= 3; i++)
                            c.Line(V(hd.x + i * X(0.8f), hd.y + X(4.0f)), V(hd.x + i * X(0.8f), hd.y + X(5.4f) - Mathf.Abs(i) * 0.3f), i < 0 ? crest[3] : crest[2]);
                    }
                    else
                    {
                        // a towering plume sweeping back
                        for (int i = 0; i <= 8; i++)
                        {
                            float t = i / 8f;
                            Vector2 q = hd + V(X(1.2f) - X(7) * t, X(4.2f) + X(3.4f) * Mathf.Sin(t * Mathf.PI * 0.9f));
                            c.Line(q, q + V(0, X(1.6f)), t < 0.4f ? crest[3] : crest[2], 2);
                        }
                    }
                    break;
                case GearLook.Crown:
                    Hair(s.raider);
                    for (int i = -2; i <= 2; i++)
                        c.Line(V(hd.x + i * X(1.3f), hd.y + X(2.4f)), V(hd.x + i * X(1.3f), hd.y + X(i % 2 == 0 ? 5.2f : 4.0f)), i < 0 ? P.Gold[3] : P.Gold[2], 2);
                    c.Line(V(hd.x - r, hd.y + X(2.4f)), V(hd.x + r, hd.y + X(2.4f)), P.Gold[1], 2);
                    c.Put(c.T(hd + V(0.5f, X(3.2f))), P.Red[2]);
                    break;
                case GearLook.BearPelt:
                {
                    var fur = P.Fur;
                    c.Ellipse(hd + V(-0.6f, X(0.9f)), r + 1.6f, X(3.6f) + 1.2f, fur, l => l.y > hd.y + X(1.4f) || l.x < hd.x - X(1.7f));
                    foreach (float ex in new[] { -2.6f, 1.4f })
                        c.Ellipse(hd + V(X(ex), X(4.8f)), X(1.4f), X(1.2f), fur);
                    // snout over the brow, with teeth and a glinting eye
                    Vector2 sn = hd + V(X(3.2f), X(2.6f));
                    c.Ellipse(sn, X(2.6f), X(1.4f), fur);
                    c.Put(c.T(sn + V(X(2.4f), X(0.3f))), P.Ink);
                    for (int i = 0; i < 3; i++) c.Put(c.T(sn + V(X(-1.2f + i * 1.2f), X(-1.4f))), P.Bone[3]);
                    c.Put(c.T(hd + V(X(1.3f), X(3.6f))), P.Gold[3]);
                    // pelt down the back
                    c.FillLocal(new Rect(hip.x - X(4.5f), hip.y + X(1), X(3), head.y - hip.y - X(1)), l => ((int)l.x + (int)l.y) % 6 == 0 ? (Color32?)null : fur[l.x < hip.x - X(3.2f) ? 1 : 2]);
                    break;
                }
            }
        }

        // ---------- Arms and what they hold ----------

        private void FarArm()
        {
            // The far arm is behind the body (darker); its weapon is drawn first, the hand on top.
            if (s.Class == WeaponClass.Bow)
            {
                Arm(shFar, hFar, true);
                if (p.loaded && p.attack && p.hFar.x < 2)
                {
                    // the nocked arrow, from the drawing hand to the bow
                    c.Line(hFar, hNear + V(X(2), 0), P.Wood[3]);
                    c.Put(c.T(hNear + V(X(2.5f), 0)), P.Steel[3]);
                }
                return;
            }
            Weapon(hFar, p.wa);
            Arm(shFar, hFar, true);
        }

        private void NearArm()
        {
            if (s.Class == WeaponClass.Bow)
            {
                Arm(shNear, hNear, false);
                Bow(hNear);
                return;
            }
            if (s.shield != GearLook.None && p.death < 2)
            {
                Arm(shNear, hNear, false);
                bool tall = s.shield == GearLook.TowerShield || s.shield == GearLook.HideShield || s.shield == GearLook.OvalShield;
                Shield(hNear + V(X(0.5f), tall ? X(-1.5f) : X(0.5f)));   // tall shields sit lower, clear of the face
            }
            else
            {
                Arm(shNear, hNear, false);
            }
            if (s.weapon == GearLook.Ballista) BallistaFrame();
            if (s.trinket == GearLook.Ring) c.Put(c.T(hNear + V(0.5f, 0.5f)), Trim(GearSlot.Trinket, P.Gold[3]));
        }

        private void Arm(Vector2 from, Vector2 hand, bool far)
        {
            var skin = skinRamp;
            bool sleeve = s.body != GearLook.Bare;
            Color32 skinCol = far ? skin[1] : skin[2];
            c.Line(from, hand, skinCol, limb);
            if (sleeve)
            {
                var sl = s.body == GearLook.Mail || s.body == GearLook.Plate ? P.Steel : s.body == GearLook.Scale ? P.Bronze : s.body == GearLook.Jerkin ? P.Linen : team;
                c.Line(from, Vector2.Lerp(from, hand, 0.4f), far ? sl[1] : sl[2], limb);
            }
            c.FillLocal(new Rect(hand.x - 0.5f, hand.y - 0.5f, limb, limb), _ => far ? skin[1] : skin[2]);
        }

        private void Weapon(Vector2 hand, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 d = V(Mathf.Cos(rad), Mathf.Sin(rad)), n = V(-d.y, d.x);
            Vector2 at(float t, float side = 0) => hand + d * t + n * side;
            int ww = S > 1.3f ? 2 : 1;
            switch (s.weapon)
            {
                case GearLook.Sword:
                case GearLook.ShortSword:
                {
                    bool gladius = s.weapon == GearLook.ShortSword;
                    float len = X(gladius ? 7 : 9);
                    c.Line(at(-2), at(0), P.Leather[2], ww);
                    c.Put(c.T(at(-2.6f)), gladius ? P.Gold[2] : P.Steel[1]);
                    Color32 guard = Trim(GearSlot.Weapon, gladius ? P.Gold[2] : P.Steel[1]);
                    c.Line(at(1, -X(1.5f)), at(1, X(1.5f)), guard);
                    c.Line(at(2), at(len), P.Steel[3], ww);
                    c.Line(at(2, -0.6f), at(len - 1, -0.6f), P.Steel[2]);
                    c.Put(c.T(at(len + 0.8f)), P.Steel[2]);
                    break;
                }
                case GearLook.Axe:
                {
                    float len = X(10);
                    c.Line(at(-2), at(len), P.Wood[2], ww);
                    Color32 head = s.raider ? P.Steel[2] : P.Bronze[2];
                    // bearded blade on the striking side
                    c.Polygon(new[] { at(len - X(4), X(0.5f)), at(len + X(0.5f), X(0.5f)), at(len + X(1), X(3.5f)), at(len - X(4.5f), X(3)) },
                        l => Vector2.Dot(l - at(len), n) > X(2.6f) ? Trim(GearSlot.Weapon, s.raider ? P.Steel[3] : P.Bronze[3]) : head);
                    break;
                }
                case GearLook.Club:
                {
                    float len = X(10);
                    c.Line(at(-2), at(len * 0.6f), P.Wood[2], ww);
                    for (float t = len * 0.45f; t <= len; t += 0.5f)
                    {
                        float r = X(0.8f) + X(1.3f) * Mathf.Sin(Mathf.PI * (t - len * 0.45f) / (len * 0.55f + 0.01f));
                        c.Ellipse(at(t), r, r, P.Wood, null, -0.3f);
                    }
                    c.Line(at(-1), at(1.5f), Trim(GearSlot.Weapon, team[2]), ww);   // grip wrap
                    if (S > 1.3f)
                        foreach (var (t, side) in new[] { (0.62f, 2.0f), (0.82f, -2.2f), (0.9f, 2.1f) })
                            c.Put(c.T(at(len * t, X(side))), P.Bone[3]);
                    break;
                }
                case GearLook.Spear:
                {
                    float len = X(riding ? 18 : 14), butt = X(5);
                    c.Line(at(-butt), at(len), P.Wood[2], ww);
                    Color32 tip = s.team == "Blue" && s.head == GearLook.DomeHelm ? P.Bronze[3] : P.Steel[3];
                    c.Polygon(new[] { at(len, -X(1.4f)), at(len + X(3.5f), 0), at(len, X(1.4f)) }, _ => tip);
                    c.Line(at(len - X(1)), at(len), Trim(GearSlot.Weapon, team[2]), ww);
                    break;
                }
                case GearLook.Javelin:
                    if (!p.loaded && p.attack) break;     // thrown
                    if (p.attack)
                    {
                        c.Line(at(-X(5)), at(X(8)), P.Wood[3]);
                        c.Polygon(new[] { at(X(8), -1), at(X(10.5f), 0), at(X(8), 1) }, _ => P.Steel[3]);
                    }
                    else
                    {
                        c.Line(at(-X(4)), at(X(10)), P.Wood[3]);
                        c.Polygon(new[] { at(X(10), -1), at(X(12.5f), 0), at(X(10), 1) }, _ => P.Steel[3]);
                    }
                    break;
                case GearLook.Sling:
                {
                    Color32 cord = P.Linen[2];
                    Vector2 pouch = p.attack && p.hFar.y > 4 ? hand + V(X(-3), X(3)) : p.release ? hand + V(X(4), X(-2)) : hand + V(X(1), X(-4));
                    c.Line(hand, pouch, cord);
                    c.Put(c.T(pouch), P.Leather[2]);
                    if (p.loaded || !p.attack) c.Put(c.T(pouch + V(0, 1)), P.Stone[3]);
                    break;
                }
            }
        }

        private void Bow(Vector2 hand)
        {
            // A bow held out in the near hand (in front), its string to the drawing hand.
            bool composite = s.head == GearLook.ConeHelm;
            var wood = composite ? P.Leather : P.Wood;
            bool aiming = p.attack;
            float hgt = X(6.5f);
            Vector2 top = hand + V(aiming ? -X(1) : X(0.5f), hgt), bot = hand + V(aiming ? -X(1) : X(0.5f), -hgt);
            for (int i = -6; i <= 6; i++)
            {
                float t = i / 6f;
                Vector2 q = hand + V(X(1.6f) * (1 - t * t) * (aiming ? 1 : 0.6f), t * hgt);
                c.Put(c.T(q), Math.Abs(i) > 4 ? wood[1] : Trim(GearSlot.Weapon, wood[2]));
            }
            if (composite)
            {
                c.Put(c.T(top + V(-1, 0)), wood[1]);
                c.Put(c.T(bot + V(-1, 0)), wood[1]);
            }
            Vector2 nock = p.loaded && p.attack && p.hFar.x < 2 ? hFar : V(top.x, hand.y);
            c.Line(top, nock, P.Linen[3]);
            c.Line(nock, bot, P.Linen[3]);
            c.FillLocal(new Rect(hand.x - 0.5f, hand.y - 0.5f, limb, limb), _ => skinRamp[2]);
        }

        private void Quiver()
        {
            Vector2 a = sh + V(X(-2.5f), X(-5)), b = sh + V(X(-1.8f), X(0.5f));
            c.Line(a, b, P.Leather[2], limb + 1);
            for (int i = 0; i < 3; i++) c.Put(c.T(b + V(i - 1, 1.5f)), i == 1 ? P.Red[2] : P.Bone[3]);
        }

        private void Horn()
        {
            Vector2 a = hip + V(X(-3.5f), X(1)), b = hip + V(X(-1.5f), X(-2.5f));
            c.Line(a, b, P.Bone[2], 2);
            c.Put(c.T(a), P.Bone[3]);
            c.Put(c.T(Vector2.Lerp(a, b, 0.5f)), Trim(GearSlot.Trinket, P.Gold[2]));
        }

        private void Shield(Vector2 at)
        {
            var ramp = shieldRamp;
            Color32 rim = Trim(GearSlot.Shield, P.Gold[2]);
            switch (s.shield)
            {
                case GearLook.RoundShield:
                {
                    float r = X(3.8f);
                    c.Ellipse(at, r, r * 1.05f, ramp);
                    c.FillLocal(new Rect(at.x - r - 1, at.y - r - 1, r * 2 + 2, r * 2 + 2), l =>
                    {
                        float d = (l - at).magnitude / r;
                        return d <= 1.05f && d > 0.75f ? (Color32?)(l.x + l.y < at.x + at.y ? rim : P.Gold[1]) : null;
                    });
                    c.Put(c.T(at), P.Steel[3]);
                    break;
                }
                case GearLook.OvalShield:
                {
                    c.Ellipse(at, X(3.2f), X(5.2f), ramp);
                    c.Line(at + V(0, X(-4.5f)), at + V(0, X(4.5f)), Trim(GearSlot.Shield, P.Wood[1]));
                    c.Ellipse(at, X(1.1f), X(1.1f), P.Iron);
                    break;
                }
                case GearLook.TowerShield:
                {
                    var box = new Rect(at.x - X(3), at.y - X(5.5f), X(6), X(11));
                    c.FillLocal(box, l => l.x < box.xMin + 1 ? ramp[3] : l.x > box.xMax - 1.5f ? ramp[1] : ramp[2]);
                    c.Line(V(box.xMin, box.yMax - 0.5f), V(box.xMax - 0.5f, box.yMax - 0.5f), rim);
                    c.Line(V(box.xMin, box.yMin), V(box.xMax - 0.5f, box.yMin), rim);
                    c.Line(at + V(X(-2), X(2.5f)), at + V(X(2), X(3.5f)), P.Gold[2]);       // painted wings
                    c.Line(at + V(X(-2), X(-2.5f)), at + V(X(2), X(-3.5f)), P.Gold[2]);
                    c.Ellipse(at, X(1.2f), X(1.2f), P.Gold);
                    break;
                }
                case GearLook.WickerShield:
                {
                    // a light crescent (pelta) of woven wicker
                    float r = X(4);
                    c.FillLocal(new Rect(at.x - r - 1, at.y - r - 1, r * 2 + 2, r * 2 + 2), l =>
                    {
                        Vector2 q = l - at;
                        bool inMain = q.sqrMagnitude <= r * r;
                        bool notch = (q - V(0, r * 0.95f)).sqrMagnitude <= r * r * 0.25f;
                        if (!inMain || notch) return null;
                        return ((int)l.x + (int)l.y) % 3 == 0 ? P.Straw[1] : q.x < 0 ? P.Straw[3] : P.Straw[2];
                    });
                    c.Line(at + V(-r + 1, X(-1)), at + V(r - 1, X(-1)), Trim(GearSlot.Shield, P.Wood[1]));
                    break;
                }
                default: // hide shield: tall, round-topped, spotted
                {
                    var box = new Rect(at.x - X(2.8f), at.y - X(5), X(5.6f), X(9));
                    c.FillLocal(new Rect(box.xMin, box.yMin, box.width, box.height + X(1.5f)), l =>
                    {
                        if (l.y > box.yMax)
                        {
                            float dx = (l.x - box.center.x) / (box.width / 2), dy = (l.y - box.yMax) / X(1.5f);
                            if (dx * dx + dy * dy > 1) return null;
                        }
                        bool spot = ((int)(l.x * 0.7f) * 7 + (int)(l.y * 0.6f) * 3) % 5 == 0;
                        return spot ? P.Fur[1] : l.x < box.xMin + 1 ? P.Leather[3] : P.Leather[2];
                    });
                    c.Line(V(box.xMin, box.yMin), V(box.xMin, box.yMax), Trim(GearSlot.Shield, P.Wood[1]));
                    c.Line(V(box.xMax - 0.5f, box.yMin), V(box.xMax - 0.5f, box.yMax), Trim(GearSlot.Shield, P.Wood[1]));
                    break;
                }
            }
        }

        private void BallistaFrame()
        {
            // The ballista in front of its crewman: stock, bow arms and (when loaded) a bolt.
            Vector2 stock = sh + V(X(3), X(-4));
            c.Line(stock + V(X(-3), 0), stock + V(X(9), 0), P.Wood[2], 2);
            float flex = p.loaded ? -X(2) : X(1);
            c.Line(stock + V(X(7), 0), stock + V(X(5) + flex, X(5)), P.Wood[1], 2);
            c.Line(stock + V(X(7), 0), stock + V(X(5) + flex, X(-5)), P.Wood[1], 2);
            c.Line(stock + V(X(5) + flex, X(5)), stock + V(p.loaded ? X(-1) : X(5), 0), P.Linen[3]);
            c.Line(stock + V(X(5) + flex, X(-5)), stock + V(p.loaded ? X(-1) : X(5), 0), P.Linen[3]);
            if (p.loaded)
            {
                c.Line(stock + V(X(-1), 1), stock + V(X(10), 1), P.Wood[3]);
                c.Put(c.T(stock + V(X(10.5f), 1)), P.Steel[3]);
            }
        }

        private void DrawCape()
        {
            if (string.IsNullOrEmpty(s.cape)) return;
            var ramp = P.Named(s.cape);
            float sway = p.fNear.x > 2 ? X(1) : p.fNear.x < -2 ? -X(1) : p.attack ? X(1.5f) : 0;
            var pts = new[] { sh + V(X(-0.5f), 0), sh + V(X(1.5f), -1), hip + V(X(-1) + sway, X(-4)), hip + V(X(-5) + sway, X(-4.5f)), hip + V(X(-6) + sway, X(-2)) };
            c.Polygon(pts, l => l.x < hip.x - X(3.5f) ? ramp[1] : ramp[2]);
        }

        private void DropGear()
        {
            // On the ground in front of the fallen body, lying flat.
            c.Angle = 0; c.Shift = Vector2.zero;
            Vector2 g = V(cx + X(thrown ? 4 : 10), gy + 1);
            if (s.Class == WeaponClass.Melee && s.weapon != GearLook.None)
            {
                var keep = hFar;
                Weapon(g, 0);
            }
            if (s.shield != GearLook.None)
            {
                Shield(V(cx - X(5), gy + X(3)));
            }
        }

        // ---------- Mounts ----------

        private float MountH => s.mount == MountKind.Elephant ? X(26) : s.mount == MountKind.Chariot ? X(9) : X(15);

        private Vector2 MountSaddle() => s.mount switch
        {
            MountKind.Chariot => V(cx + 0.5f - X(10), gy + X(9) + X(7) + 1),
            MountKind.Elephant => V(cx + 0.5f - X(2), gy + X(27)),
            _ => V(cx + 0.5f - X(1), gy + X(15)),
        };

        private float ThrownOffset() => s.mount switch
        {
            MountKind.Elephant => X(22),
            MountKind.Chariot => X(-18),
            _ => X(14),
        };

        private void FarLegRiding()
        {
            Color32[] pants = s.raider ? P.Leather : P.Cloth;
            c.Line(hip + V(X(-0.5f), 0), hip + V(X(1.5f), X(-5)), pants[1], limb);
        }

        private void NearLegRiding()
        {
            Color32[] pants = s.raider ? P.Leather : P.Cloth;
            Vector2 knee = hip + V(X(2.5f), X(-2)), foot = hip + V(X(1.5f), X(-6.5f));
            c.Line(hip, knee, pants[2], limb);
            c.Line(knee, foot, pants[2], limb);
            c.FillLocal(new Rect(foot.x - 0.5f, foot.y - 1, limb + 1, 2), _ => P.Leather[2]);
        }

        private void DrawMount(bool fallen)
        {
            var save = (c.Angle, c.Shift, c.Pivot);
            c.Angle = 0; c.Shift = Vector2.zero;
            var coat = P.Named(s.coat == "" ? "Horse" : s.coat);
            if (s.coat == "DarkHorse") coat = P.DarkHorse;
            else if (s.coat == "Elephant") coat = P.Elephant;
            else if (s.coat == "Horse") coat = P.Horse;
            int drop = !fallen ? 0 : p.death == 2 ? 1 : 2;
            switch (s.mount)
            {
                case MountKind.Horse: Horse(V(cx + 0.5f, gy), coat, drop, 0); break;
                case MountKind.Chariot:
                    Horse(V(cx + 0.5f + X(16), gy), coat, drop, 0);
                    ChariotBack(drop);
                    break;
                case MountKind.Elephant: Elephant(V(cx + 0.5f, gy), coat, drop); break;
            }
            (c.Angle, c.Shift, c.Pivot) = save;
        }

        private int Gait => p.death > 0 ? 0 : p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? -1 : 0;

        private void Horse(Vector2 o, Color32[] coat, int drop, int unused)
        {
            float lift = drop == 0 ? 0 : drop == 1 ? -X(4) : -X(8);
            Vector2 body = o + V(0, X(11) + lift);
            int g = Gait;
            var mane = s.coat == "Horse" ? P.Hair : P.DarkHorse;
            // far legs (darker), then body, then near legs
            if (drop < 2)
            {
                Legs(body, coat[0], g, far: true, drop);
            }
            c.Line(body + V(X(-9), X(3)), body + V(X(-12), X(-4) + (drop == 2 ? X(3) : 0)), mane[1], 2);   // tail
            c.Ellipse(body + V(X(-6), 0), X(5), X(4.5f), coat);
            c.Ellipse(body, X(9), X(4.6f), coat);
            c.Ellipse(body + V(X(6.5f), X(0.8f)), X(4), X(4.6f), coat);
            if (drop < 2)
            {
                // neck and head, nose down a little
                Vector2 neck0 = body + V(X(8), X(3)), neck1 = body + V(X(11), X(10)), nose = body + V(X(15.5f), X(7));
                c.Capsule(neck0, neck1, X(2.6f), coat);
                c.Capsule(neck1, nose, X(2.1f), coat, 0.2f);
                c.Line(neck0 + V(X(-1.5f), X(1.5f)), neck1 + V(X(-1.5f), X(1)), mane[1], 2);   // mane
                c.Put(neck1 + V(X(0.5f), X(2.6f)), coat[1]);                                     // ear
                c.Put(neck1 + V(X(1.8f), X(0.6f)), P.Ink);                                        // eye
                c.Put(nose + V(X(0.8f), X(-0.6f)), coat[0]);
                Legs(body, coat[1], g, far: false, drop);
            }
            else
            {
                c.Capsule(body + V(X(8), X(2)), body + V(X(15), X(-1)), X(2.4f), coat);   // head on the ground
                for (int i = 0; i < 2; i++)
                    c.Line(body + V(X(-2 + i * 6), X(-3)), body + V(X(1 + i * 6), X(-6)), coat[1], 2);
            }
            // saddle cloth in the team colour
            if (s.mount == MountKind.Horse)
                c.FillLocal(new Rect(body.x - X(5), body.y + X(1), X(8), X(4.5f)), l => l.y > body.y + X(5) ? (Color32?)null : l.x < body.x - X(3) ? team[2] : team[1]);
        }

        private void Legs(Vector2 body, Color32 col, int g, bool far, int drop)
        {
            float len = drop == 0 ? X(10) : X(5);
            float[] xs = far ? new[] { X(-6.5f), X(5.5f) } : new[] { X(-5), X(7) };
            for (int i = 0; i < 2; i++)
            {
                float swing = g * X(2.5f) * (i == 0 ? 1 : -1) * (far ? -1 : 1);
                Vector2 top = body + V(xs[i], X(-2)), foot = body + V(xs[i] + swing, -len - X(1));
                if (drop == 1) foot = body + V(xs[i] + X(3), -len);
                c.Line(top, foot, col, 2);
                c.Put(foot + V(0, -0.5f), P.Ink);
                c.Put(foot + V(1, -0.5f), P.Ink);
            }
        }

        private void ChariotBack(int drop)
        {
            // the wheel and the car's floor, behind the charioteer
            Vector2 wheel = V(cx + 0.5f - X(10), gy + X(5.5f));
            float r = X(5.5f);
            c.FillLocal(new Rect(wheel.x - r - 1, wheel.y - r - 1, r * 2 + 2, r * 2 + 2), l =>
            {
                float d = (l - wheel).magnitude;
                if (d > r) return null;
                if (d > r - 1.5f) return P.Wood[1];
                float a = Mathf.Atan2(l.y - wheel.y, l.x - wheel.x) * Mathf.Rad2Deg;
                bool spoke = Mathf.Abs(Mathf.DeltaAngle(a, Mathf.Round(a / 45f) * 45f)) < 8f;
                return spoke || d < 1.5f ? (Color32?)P.Wood[2] : null;
            });
            c.Line(V(cx - X(4), gy + X(8)), V(cx + X(9), gy + X(13)), P.Wood[1], 2);   // yoke pole
        }

        private void ChariotFront()
        {
            var save = (c.Angle, c.Shift);
            c.Angle = 0; c.Shift = Vector2.zero;
            // the car's front panel hides the charioteer's legs
            var box = new Rect(cx - X(15), gy + X(8), X(11), X(8));
            c.FillLocal(box, l => l.y > box.yMax - 1.5f ? Trim(GearSlot.Body, P.Bronze[3]) : l.x < box.xMin + 1.5f ? P.Wood[3] : ((int)l.x % 4 == 0 ? P.Wood[1] : P.Wood[2]));
            c.Line(V(box.xMin + 2, box.yMin + X(3)), V(box.xMax - 2, box.yMin + X(3)), team[2]);
            (c.Angle, c.Shift) = save;
        }

        private void Elephant(Vector2 o, Color32[] coat, int drop)
        {
            float lift = drop == 0 ? 0 : drop == 1 ? -X(6) : -X(12);
            Vector2 body = o + V(0, X(19) + lift);
            int g = Gait;
            if (drop < 2)
                for (int i = 0; i < 2; i++)
                {
                    float sw = g * X(2) * (i == 0 ? -1 : 1);
                    c.Capsule(body + V(X(-9 + i * 15), X(-4)), o + V(X(-9 + i * 15) + sw, X(2)), X(3.2f), new[] { coat[0], coat[0], coat[1], coat[1] });
                }
            c.Line(body + V(X(-15), X(4)), body + V(X(-17), X(-6)), coat[0], 2);   // tail
            c.Ellipse(body, X(15), X(10.5f), coat);
            Vector2 headC = body + V(X(13), X(3));
            c.Ellipse(headC, X(7), X(7.5f), coat);
            // trunk curling down, tusk, ear, eye
            Vector2 t0 = headC + V(X(5), X(-3)), t1 = headC + V(X(8), X(-12)), t2 = headC + V(X(10), X(-17) - lift * 0.5f);
            c.Capsule(t0, t1, X(2.2f), coat);
            c.Capsule(t1, t2, X(1.6f), coat);
            c.Line(headC + V(X(4), X(-4)), headC + V(X(9), X(-6)), P.Bone[3], 2);
            c.Ellipse(headC + V(X(-3), X(0)), X(4), X(5.5f), new[] { coat[0], coat[1], coat[1], coat[2] });
            c.Put(headC + V(X(3), X(2)), P.Ink);
            if (drop < 2)
                for (int i = 0; i < 2; i++)
                {
                    float sw = g * X(2) * (i == 0 ? 1 : -1);
                    c.Capsule(body + V(X(-7 + i * 15), X(-5)), o + V(X(-7 + i * 15) + sw, X(2)), X(3.4f), coat);
                    c.Line(o + V(X(-9 + i * 15) + sw, X(1)), o + V(X(-5 + i * 15) + sw, X(1)), coat[3]);   // toenails
                }
            // a team-coloured saddle cloth with gold fringe
            var cloth = new Rect(body.x - X(9), body.y + X(2), X(14), X(9));
            c.FillLocal(cloth, l => l.y < cloth.yMin + 1.2f ? (Color32?)(((int)l.x % 2 == 0) ? P.Gold[2] : P.Gold[1]) : l.x < cloth.xMin + 2 ? team[2] : team[1]);
        }

        // ---------- Effects (after the outline) ----------

        private void Smear()
        {
            float r = X(11);
            Vector2 o = sh + V(X(1), 0);
            for (int a = -20; a <= 110; a += 4)
            {
                float t = a * Mathf.Deg2Rad;
                Vector2 q = o + V(Mathf.Cos(t), Mathf.Sin(t)) * r, q2 = o + V(Mathf.Cos(t), Mathf.Sin(t)) * (r - 1);
                if (c.Get((int)q.x, (int)q.y).a < 200) c.Put(q, new Color32(255, 255, 255, 190));
                if (c.Get((int)q2.x, (int)q2.y).a < 200) c.Put(q2, new Color32(210, 235, 255, 110));
            }
        }

        private void Thrust()
        {
            Vector2 tip = hFar + V(X(14), 0);
            for (int i = 0; i < 3; i++)
            {
                float y = tip.y + (i - 1) * X(2.5f);
                c.LineRaw(V(tip.x + X(1), y), V(tip.x + X(5), y), new Color32(255, 255, 255, (byte)(i == 1 ? 200 : 120)));
            }
        }
    }

    /// <summary>Wolves and giant spiders: four- and eight-legged, facing right, 12 frames like everyone else.</summary>
    private static class BeastRig
    {
        public static void Draw(PixelCanvas c, FigureStyle s, int frame, int cx, int gy)
        {
            float S = s.scale;
            int gait = frame == 1 ? 1 : frame == 3 ? -1 : 0;
            float lunge = frame == 6 ? 3 : frame == 7 ? 5 : frame == 5 ? -1 : frame == 8 ? 1 : 0;
            int death = frame >= 9 ? frame - 8 : 0;
            bool bite = frame == 6 || frame == 7;
            c.Pivot = new Vector2(cx, gy);
            c.Angle = death == 1 ? -10 : 0;
            if (s.beast == Beast.Wolf) Wolf(c, cx + lunge * S, gy, S, gait, bite, death, frame);
            else Spider(c, cx + lunge * S, gy, S, gait, bite, death, frame);
            c.Angle = 0;
            c.Outline(P.Ink, 0.6f);
            c.Shadow(cx + 1, gy, 13 * S, 2f * S);
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static void Pt(PixelCanvas c, Vector2 v, Color32 col) => c.Put(c.T(v), col);

        private static void Wolf(PixelCanvas c, float x, int gy, float S, int gait, bool bite, int death, int frame)
        {
            var fur = P.Wolf;
            var dark = new[] { fur[0], fur[0], fur[1], fur[1] };
            bool lying = death >= 2;
            float body = lying ? gy + 4 * S : gy + 10 * S;
            float bob = frame == 2 || frame == 4 ? 1 : 0;
            body += bob;
            // far legs, tail, body, head, near legs
            if (!lying)
            {
                Leg(c, x - 6 * S, body - 2 * S, gy, -gait * 2 * S, dark, S);
                Leg(c, x + 5 * S, body - 2 * S, gy, gait * 2 * S, dark, S);
            }
            c.Capsule(V(x - 9 * S, body + 1 * S), V(x - 15 * S, body - (lying ? 1 : 3) * S), 1.8f * S, fur);       // bushy tail
            c.Capsule(V(x - 7 * S, body), V(x + 5 * S, body + 0.5f * S), 4.2f * S, fur);
            c.Ellipse(V(x + 4 * S, body + 1 * S), 4.3f * S, 4.6f * S, fur);                                      // deep chest
            c.Line(V(x - 6 * S, body - 3 * S), V(x + 3 * S, body - 3.5f * S), fur[3]);                           // pale belly
            float hy = lying ? body + 2 * S : body + (bite ? 3 : 5) * S;
            Vector2 head = V(x + 9 * S, hy);
            c.Ellipse(head, 3.4f * S, 3f * S, fur);
            c.Polygon(new[] { head + V(-2 * S, 2 * S), head + V(-1 * S, 5.5f * S), head + V(0.5f * S, 2 * S) }, l => fur[2]);   // ear
            Vector2 snout = head + V(4.6f * S, -0.8f * S);
            c.Capsule(head + V(1.5f * S, -0.5f * S), snout, 1.6f * S, fur);
            Pt(c, snout + V(1.2f * S, 0.4f * S), P.Ink);                                                            // nose
            if (bite)
            {
                c.Polygon(new[] { head + V(2 * S, -1.5f * S), snout + V(1.5f * S, -2.5f * S), snout + V(1 * S, -1 * S) }, l => P.Red[1]);
                Pt(c, snout + V(0.5f * S, -1.2f * S), P.Bone[3]);
                Pt(c, snout + V(-1 * S, -1.6f * S), P.Bone[3]);
            }
            Pt(c, head + V(1.2f * S, 0.8f * S), death > 0 ? P.Coal[2] : P.Gold[3]);                               // eye
            if (!lying)
            {
                Leg(c, x - 4.5f * S, body - 2 * S, gy, gait * 2 * S, fur, S);
                Leg(c, x + 6.5f * S, body - 2 * S, gy, -gait * 2 * S, fur, S);
            }
            else
            {
                for (int i = 0; i < 2; i++) c.Line(V(x - 4 * S + i * 9 * S, body - 2 * S), V(x - 1 * S + i * 9 * S, body - 4 * S), fur[1], 2);
            }
        }

        private static void Leg(PixelCanvas c, float x, float top, int gy, float swing, Color32[] ramp, float S)
        {
            Vector2 knee = V(x + swing * 0.5f, (top + gy) * 0.5f + 1), foot = V(x + swing, gy + 1);
            c.Line(V(x, top), knee, ramp[2], S > 1.3f ? 3 : 2);
            c.Line(knee, foot, ramp[1], S > 1.3f ? 3 : 2);
            Pt(c, foot + V(1, -1), ramp[0]);
        }

        private static void Spider(PixelCanvas c, float x, int gy, float S, int gait, bool bite, int death, int frame)
        {
            var carapace = new[] { P.Hex("1a1420"), P.Hex("2e2236"), P.Hex("4a3858"), P.Hex("6e5a80") };
            bool curled = death >= 2;
            float body = curled ? gy + 4 * S : gy + 8 * S + (frame == 2 || frame == 4 ? 1 : 0);
            float rear = bite ? 3 * S : 0;   // rears up to strike
            void Legs(bool far)
            {
                for (int i = 0; i < 4; i++)
                {
                    float bx = x - 3 * S + i * 2.2f * S;
                    float side = (i - 1.5f) * 3.4f * S;
                    float step = ((i + (far ? 1 : 0)) % 2 == 0 ? gait : -gait) * 1.6f * S;
                    Vector2 hip = V(bx, body), knee, foot;
                    if (curled) { knee = hip + V(side * 0.4f, 3 * S); foot = knee + V(-side * 0.2f, 2 * S); }
                    else
                    {
                        knee = V(bx + side + step * 0.5f, body + 5 * S + (i == 3 ? rear : 0));
                        foot = V(bx + side * 1.6f + step, i == 3 && bite ? body + 4 * S : gy + 1);
                    }
                    var col = far ? carapace[1] : carapace[2];
                    c.Line(hip, knee, col, 2);
                    c.Line(knee, foot, far ? carapace[0] : carapace[1], S > 1.3f ? 2 : 1);
                }
            }
            Legs(true);
            Vector2 abdomen = V(x - 6 * S, body + 1.5f * S), head = V(x + 3 * S, body + rear * 0.4f);
            c.Ellipse(abdomen, 6.5f * S, 5.2f * S, carapace);
            c.Polygon(new[] { abdomen + V(-1 * S, 3 * S), abdomen + V(2 * S, 1 * S), abdomen + V(-1 * S, -1 * S), abdomen + V(-3 * S, 1 * S) }, l => P.Red[2]);   // hourglass
            c.Ellipse(head, 4 * S, 3.4f * S, carapace);
            for (int i = 0; i < 3; i++) Pt(c, head + V((1.5f + i * 0.9f) * S, (1 + (i % 2)) * S), death > 0 ? P.Coal[2] : P.Red[3]);   // eyes
            c.Line(head + V(3 * S, -2 * S), head + V(3.5f * S, -(bite ? 4.5f : 3.5f) * S), P.Bone[2]);                                // fangs
            c.Line(head + V(2 * S, -2 * S), head + V(2.2f * S, -(bite ? 4.5f : 3.5f) * S), P.Bone[2]);
            Legs(false);
        }
    }
}
