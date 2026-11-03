using System;
using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;

public enum MountKind { None, Horse, Chariot, Elephant, Cannon, Crawler, Tank, ArmouredCar, Walker, Airship, Biplane, Drill, HoverTank, HoverCar, Jetpack, Drone, Jet, Griffin, Skybike, Starship, HoverThrone, Drake }

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
    [Tooltip("A medic: satchel of bandages on the hip and a white armband.")]
    public bool medic;
    [Tooltip("A war horse in a cloth caparison (team colour) with a steel face plate.")]
    public bool barding;

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
        GearLook.Crossbow => WeaponClass.Crossbow,
        GearLook.RuneStaff => WeaponClass.Staff,
        GearLook.Arquebus or GearLook.Pistol or GearLook.Rifle or GearLook.Musket
            or GearLook.BoltRifle or GearLook.MachineGun or GearLook.Tommygun => WeaponClass.Musket,
        GearLook.Flask or GearLook.Grenade => WeaponClass.Thrown,
        GearLook.AetherRod or GearLook.LeydenRod or GearLook.ManaStaff or GearLook.BeamStaff => WeaponClass.Staff,
        GearLook.RayGun or GearLook.RayRifle or GearLook.AssaultRifle or GearLook.PlasmaRifle or GearLook.ManaRifle => WeaponClass.Musket,
        GearLook.Cannon => WeaponClass.Cannon,
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
            case MountKind.Cannon: w = 76; h = 48; cx = 20; gy = 4; break;   // the gunner stands behind the gun
            case MountKind.Crawler: w = 88; h = 54; cx = 26; gy = 4; break;  // the commander stands in the hatch
            case MountKind.Tank: w = 96; h = 54; cx = 32; gy = 4; break;     // in the turret hatch
            case MountKind.ArmouredCar: w = 80; h = 50; cx = 32; gy = 4; break;
            case MountKind.Walker: w = 64; h = 76; cx = 30; gy = 4; break;   // the pilot in a cockpit on two iron legs
            case MountKind.Airship: w = 88; h = 112; cx = 40; gy = 4; break; // high in the air: gondola under a gasbag
            case MountKind.Biplane: w = 80; h = 84; cx = 38; gy = 4; break;
            case MountKind.Drill: w = 84; h = 50; cx = 30; gy = 4; break;    // a tunnelling machine, the driver in its hatch
            case MountKind.HoverTank: w = 96; h = 60; cx = 32; gy = 4; break;
            case MountKind.HoverCar: w = 80; h = 56; cx = 32; gy = 4; break;
            case MountKind.Jetpack: w = 56; h = 76; cx = 26; gy = 4; break;   // flying on a rocket pack
            case MountKind.Drone: w = 40; h = 56; cx = 20; gy = 4; break;     // a pilotless quad-rotor
            case MountKind.Jet: w = 84; h = 80; cx = 40; gy = 4; break;       // a jet fighter, the pilot under its canopy
            case MountKind.Griffin: w = 80; h = 100; cx = 38; gy = 4; break;  // a rider on a flying griffin (or a raider's wyvern)
            case MountKind.Skybike: w = 72; h = 80; cx = 30; gy = 4; break;   // a mana jetbike, the rider low in its seat
            case MountKind.Starship: w = 104; h = 100; cx = 46; gy = 4; break; // a star-steel gunship, the pilot under the hatch
            case MountKind.HoverThrone: w = 92; h = 76; cx = 42; gy = 4; break; // a throne on a hovering disc, a nova cannon at its side
            case MountKind.Drake: w = 100; h = 84; cx = 42; gy = 4; break;     // a rider on an armoured war drake that breathes fire
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
        if (s.weapon == GearLook.Spear || s.weapon == GearLook.Javelin || s.weapon == GearLook.Pike || s.weapon == GearLook.RuneStaff
            || s.weapon == GearLook.AetherRod || s.weapon == GearLook.LeydenRod || s.weapon == GearLook.ManaStaff || s.weapon == GearLook.BeamStaff) p.wa = 84;
        if (s.weapon == GearLook.Lance) p.wa = 62;
        if (cls == WeaponClass.Crossbow || LongGun(s)) { p.hNear = V(5, -4); p.hFar = V(2, -5); }
        if (s.weapon == GearLook.Pistol || s.weapon == GearLook.RayGun) p.wa = -60;
        if (s.weapon == GearLook.Halberd) p.wa = 84;
        if (cls == WeaponClass.Cannon) { p.wa = 80; p.hFar = V(3, -4); }
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

    /// <summary>Two-handed shoulder guns (arquebus, rifle).</summary>
    private static bool LongGun(FigureStyle s) => s.weapon == GearLook.Arquebus || s.weapon == GearLook.Rifle || s.weapon == GearLook.Musket
        || s.weapon == GearLook.BoltRifle || s.weapon == GearLook.MachineGun || s.weapon == GearLook.Tommygun || s.weapon == GearLook.RayRifle
        || s.weapon == GearLook.AssaultRifle || s.weapon == GearLook.PlasmaRifle || s.weapon == GearLook.ManaRifle;

    /// <summary>Vehicles the figure stands in (a hatch or an open top) rather than rides.</summary>
    private static bool Hatch(MountKind m) => m == MountKind.Crawler || m == MountKind.Tank || m == MountKind.ArmouredCar
        || m == MountKind.Airship || m == MountKind.Biplane || m == MountKind.Drill || m == MountKind.HoverTank || m == MountKind.HoverCar
        || m == MountKind.Skybike || m == MountKind.Starship || m == MountKind.HoverThrone;

    /// <summary>Machines with nobody visible aboard: only the machine is drawn.</summary>
    private static bool Pilotless(MountKind m) => m == MountKind.Drone || m == MountKind.Jet;

    /// <summary>How high a flying craft is drawn above its shadow (pixels, before scaling).</summary>
    private static int Altitude(FigureStyle s) => s.mount == MountKind.Airship ? 50 : s.mount == MountKind.Biplane ? 40
        : s.mount == MountKind.Jetpack ? 28 : s.mount == MountKind.HoverTank || s.mount == MountKind.HoverCar ? 6
        : s.mount == MountKind.Drone ? 30 : s.mount == MountKind.Jet ? 42 : s.mount == MountKind.Griffin ? 30
        : s.mount == MountKind.Skybike ? 26 : s.mount == MountKind.Starship ? 44 : s.mount == MountKind.HoverThrone ? 8 : 0;

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
            case WeaponClass.Crossbow:
                // shouldered and aimed, loose, then lowered to span it again
                p.hNear = i == 3 ? V(5, -4) : V(6, -1);
                p.hFar = i == 3 ? V(2, -5) : V(2, -1);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Musket when s.weapon == GearLook.Pistol || s.weapon == GearLook.RayGun:
                // one-handed: raise the pistol level, fire, lower it
                p.hFar = i == 3 ? V(3, -5) : V(6, 1);
                p.wa = i == 3 ? -60 : 0;
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Musket:
                // the arquebus to the shoulder, aim, fire, then lower it to reload
                p.hNear = i == 3 ? V(5, -4) : V(6, 0);
                p.hFar = i == 3 ? V(2, -5) : V(1, 0);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Cannon:
                // put the burning match to the touch-hole; the gun goes off and rolls back
                p.hFar = i <= 1 ? V(7, -1) : i == 2 ? V(3, -2) : V(3, -4);
                p.wa = i <= 1 ? 25 : 80;
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.Staff:
                // raise the staff, gather the rune's light, then thrust it forward to loose it
                p.hFar = i == 0 ? V(0, 5) : i == 1 ? V(3, 5) : i == 2 ? V(6, 1) : V(4, -3);
                p.wa = i == 0 ? 100 : i == 1 ? 80 : i == 2 ? 20 : 70;
                if (!shield) p.hNear = i == 2 ? V(6, 0) : V(4, -3);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
            case WeaponClass.None:     // ballista crew: crank and loose
                p.hFar = i == 1 ? V(3, -1) : V(4, -2);
                p.loaded = i <= 1;
                p.release = i == 2;
                return;
        }
        if (s.weapon == GearLook.Spear || s.weapon == GearLook.Pike || s.weapon == GearLook.Lance || s.weapon == GearLook.Halberd)
        {
            p.hFar = i == 0 ? V(-1, -2) : i == 1 ? V(0, -1) : i == 2 ? V(6, -1) : V(4, -2);
            p.wa = i == 0 ? 4 : i == 1 ? 6 : i == 2 ? 0 : -6;
            p.thrust = i == 2;
            return;
        }
        // Swung weapons: up behind the head, over, and down in front.
        p.hFar = i == 0 ? V(-2, 3) : i == 1 ? V(0, 5) : i == 2 ? V(6, 0) : V(5, -4);
        p.wa = i == 0 ? 150 : i == 1 ? 110 : i == 2 ? -10 : -55;
        p.smear = i == 2 && s.weapon != GearLook.None;   // no swoosh without a weapon (medics bandaging)
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
        if (Pilotless(s.mount))
        {
            MachineRig.Draw(c, s, pose.attack, pose.release, frame, cx, gy + Mathf.RoundToInt(Altitude(s) * s.scale), gy);
            return c;
        }
        new Rig(c, s, pose, cx, gy + Mathf.RoundToInt(Altitude(s) * s.scale), gy).Draw();
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
        private readonly int cx, gy, groundY;
        private readonly Color32[] team, crest, shieldRamp, skinRamp;
        private Vector2 hip, sh, head, shNear, shFar, hNear, hFar, footNear, footFar;
        private float headR;
        private bool riding, thrown;

        public Rig(PixelCanvas canvas, FigureStyle style, Pose pose, int cx, int gy, int groundY)
        {
            c = canvas;
            s = style;
            p = pose;
            S = s.scale;
            limb = S < 1.4f ? 2 : 3;
            this.cx = cx;
            this.gy = gy;
            this.groundY = groundY;
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
            bool mounted = s.mount != MountKind.None && s.mount != MountKind.Cannon && s.mount != MountKind.Jetpack && !Hatch(s.mount);
            riding = mounted && p.death < 2;
            thrown = mounted && p.death >= 2;
            float lean = p.death == 1 ? -15f : p.death == 2 ? -55f : p.death == 3 ? -88f : 0f;
            // In a vehicle's hatch the crewman slumps down inside it instead of toppling out.
            bool inHatch = Hatch(s.mount);
            if (inHatch) lean = p.death == 1 ? -10f : p.death == 2 ? -20f : p.death == 3 ? -30f : 0f;

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
            if (mounted && p.death >= 2) DrawMount(fallen: true);

            // Lean the rider/figure for death frames (round the feet, or the saddle).
            c.Pivot = riding ? hip : V(hip.x, gy);
            c.Angle = lean;
            c.Shift = inHatch ? V(0, -p.death * X(2.5f)) : p.death == 3 ? V(X(-4), X(2)) : Vector2.zero;

            if (s.mount == MountKind.Airship && p.death < 3) AirshipBag();
            if (s.mount == MountKind.Jetpack) Jetpack();
            DrawCape();
            if (s.weapon == GearLook.Bow) Quiver();
            if (s.trinket == GearLook.Horn) Horn();

            // Far arm and whatever that hand holds: behind the body.
            FarArm();

            if (riding)
            {
                c.Angle = 0; c.Shift = Vector2.zero;
                if (s.mount != MountKind.Chariot && s.mount != MountKind.Walker) FarLegRiding();
                DrawMount(fallen: false);
                c.Pivot = hip; c.Angle = lean;
                if (s.mount == MountKind.Chariot) { StandingLegs(); ChariotFront(); }
                else if (s.mount != MountKind.Walker) NearLegRiding();
            }
            else if (!inHatch)
            {
                StandingLegs();   // (a crewman's legs are inside the vehicle)
            }

            Torso();
            Head();
            NearArm();

            c.Angle = 0; c.Shift = Vector2.zero;
            if (s.mount == MountKind.Cannon) FieldGun();
            if (s.mount == MountKind.Crawler) SteamCrawler();
            if (s.mount == MountKind.Tank) TankHull();
            if (s.mount == MountKind.ArmouredCar) ArmouredCar();
            if (s.mount == MountKind.Walker && riding) WalkerCockpit();
            if (s.mount == MountKind.Airship) AirshipGondola();
            if (s.mount == MountKind.Biplane) Biplane();
            if (s.mount == MountKind.Drill) DrillMachine();
            if (s.mount == MountKind.HoverTank) HoverHull(true);
            if (s.mount == MountKind.HoverCar) HoverHull(false);
            if (s.mount == MountKind.Skybike) StarHull(false);
            if (s.mount == MountKind.Starship) StarHull(true);
            if (s.mount == MountKind.HoverThrone) Throne();
            c.Outline(P.Ink, 0.6f);
            if (p.release && (s.Class == WeaponClass.Musket || s.Class == WeaponClass.Cannon)) MuzzleFlash();
            if (p.smear) Smear();
            if (p.thrust) Thrust();
            float shadowX = s.mount == MountKind.None ? cx + 1 : s.mount == MountKind.Cannon ? cx + X(12) : s.mount == MountKind.Crawler ? cx + X(8)
                : s.mount == MountKind.Tank ? cx + X(4) : s.mount == MountKind.ArmouredCar ? cx + X(2) : cx + X(s.mount == MountKind.Chariot ? 4 : 0);
            float shadowW = s.mount == MountKind.None ? X(7) : s.mount == MountKind.Elephant ? X(18) : s.mount == MountKind.Cannon ? X(16)
                : s.mount == MountKind.Crawler || s.mount == MountKind.Tank ? X(20) : s.mount == MountKind.ArmouredCar ? X(16)
                : s.mount == MountKind.Walker ? X(10) : X(s.mount == MountKind.Chariot ? 24 : 14);
            if (p.death == 3 && s.mount == MountKind.None) shadowX += X(8);
            if (Altitude(s) > 0) { shadowX = cx + X(2); shadowW = X(s.mount == MountKind.Airship ? 16 : s.mount == MountKind.Starship ? 18 : s.mount == MountKind.Jetpack ? 6 : 13); }   // the craft's shadow on the ground
            c.Shadow(shadowX, groundY, shadowW, X(1.8f));
        }

        // ---------- Body ----------

        private void StandingLegs()
        {
            bool metal = s.body == GearLook.ChromePlate || s.body == GearLook.BrassPlate && s.head == GearLook.BoilerHelm || s.body == GearLook.SynthBody
                || s.body == GearLook.GolemBody || s.body == GearLook.RunePlate || s.body == GearLook.StarPlate || s.body == GearLook.VoidPlate;   // robots, automatons, androids, golems, power armour
            Color32[] pants = metal ? (s.body == GearLook.ChromePlate ? P.Chrome : s.body == GearLook.SynthBody ? P.Synth : s.body == GearLook.GolemBody ? P.Rock
                : s.body == GearLook.RunePlate ? P.Moonstone : s.body == GearLook.StarPlate ? P.Astral : s.body == GearLook.VoidPlate ? P.Void : P.Brass)
                : s.body == GearLook.TacVest ? P.Tactical : s.raider ? P.Leather : P.Cloth;
            Color32[] boots = s.body == GearLook.GolemBody ? P.Rock : metal ? P.Iron : P.Leather;
            Leg(footFar, pants[1], boots[1], -1);
            Leg(footNear, pants[2], boots[2], 1);
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
                GearLook.Mail or GearLook.Plate or GearLook.Surcoat or GearLook.Cuirass => P.Steel,
                GearLook.BrassPlate => P.Brass,
                GearLook.Fatigues => P.Olive,
                GearLook.Jumpsuit => team,
                GearLook.TacVest => P.Tactical,
                GearLook.SynthBody => P.Synth,
                GearLook.ChromePlate => P.Chrome,
                GearLook.TrenchCoat => P.Khaki,
                GearLook.Robe or GearLook.Spellweave => crest,
                GearLook.GolemBody => P.Rock,
                GearLook.RunePlate => P.Moonstone,
                GearLook.StarPlate => P.Astral,
                GearLook.VoidPlate => P.Void,
                GearLook.Bare => skinRamp,
                _ => team,
            };
            if (s.body == GearLook.Robe)
            {
                // a long robe to the ankles, over the legs
                float top = hip.y + 1, bottom = gy + 1;
                c.FillLocal(new Rect(hip.x - X(4.2f), bottom, X(8.4f), top - bottom), l =>
                {
                    float t = (top - l.y) / Mathf.Max(1, top - bottom);
                    float half = X(2.8f) + X(1.4f) * t;
                    if (Mathf.Abs(l.x - hip.x) > half) return null;
                    if (l.y < bottom + 1) return Trim(GearSlot.Body, crest[0]);
                    return l.x < hip.x - half * 0.4f ? crest[1] : crest[2];
                });
            }
            if (s.body == GearLook.Greatcoat || s.body == GearLook.TrenchCoat)
            {
                // coat-tails to the knee, split at the back
                float top = hip.y + 1, bottom = hip.y - X(5.5f);
                c.FillLocal(new Rect(hip.x - X(3.6f), bottom, X(7.2f), top - bottom), l =>
                {
                    if (Mathf.Abs(l.x - (hip.x - X(1.2f))) < 0.6f && l.y < hip.y - X(2)) return null;   // the split
                    if (l.y < bottom + 1) return Trim(GearSlot.Body, team[0]);
                    var coat = s.body == GearLook.TrenchCoat ? P.Khaki : team;
                    return l.x < hip.x - X(1.2f) ? coat[1] : coat[2];
                });
            }
            // Tunic skirt in the team colour shows below any armour (not under a robe).
            if (s.body != GearLook.Robe && s.body != GearLook.Greatcoat && s.body != GearLook.TrenchCoat)
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
                case GearLook.Surcoat:
                {
                    // mail, with a tabard in the team colour over it and a cross on the chest
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        ((int)l.x + (int)l.y) % 2 == 0 && Inside(l, a, b, r) ? (Color32?)P.Steel[1] : null);
                    c.FillLocal(new Rect(a.x - r + 1, a.y - X(1.5f), r * 2 - 2, b.y - a.y - X(0.5f)), l =>
                        Inside(l, a, b, r) || l.y < a.y ? (Color32?)(l.x < a.x - X(0.6f) ? team[1] : team[2]) : null);
                    Vector2 chest = Vector2.Lerp(a, b, 0.6f);
                    c.Line(chest + V(0, X(-1.6f)), chest + V(0, X(1.6f)), Trim(GearSlot.Body, P.Linen[3]));
                    c.Line(chest + V(X(-1.2f), X(0.4f)), chest + V(X(1.2f), X(0.4f)), Trim(GearSlot.Body, P.Linen[3]));
                    break;
                }
                case GearLook.Doublet:
                    // a slashed doublet: puffed stripes of the team colour and the crest colour
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        Inside(l, a, b, r) && ((int)(l.x + 0.5f) % 3 == 0) ? (Color32?)crest[2] : null);
                    break;
                case GearLook.Cuirass:
                    // a polished breastplate, a bright ridge down the middle, and tassets over the hips
                    c.Line(V(a.x + X(0.6f), a.y + 1), V(b.x + X(0.6f), b.y - 1), P.Steel[3]);
                    c.FillLocal(new Rect(a.x - r, a.y - X(1.6f), r * 2, X(1.6f)), l => ((int)l.x % 3 == 0) ? (Color32?)P.Steel[1] : P.Steel[2]);
                    break;
                case GearLook.Greatcoat:
                {
                    // white crossbelts and a row of brass buttons
                    c.Line(V(b.x - r + 1, b.y - 1), V(a.x + r - 1, a.y + 1), P.Linen[3]);
                    c.Line(V(b.x + r - 1, b.y - 1), V(a.x - r + 1, a.y + 1), P.Linen[2]);
                    for (float y = a.y + X(1.2f); y < b.y - 1; y += X(1.5f)) c.Put(c.T(V(a.x + X(1.2f), y)), Trim(GearSlot.Body, P.Brass[2]));
                    c.Line(V(b.x - r + 0.5f, b.y - 0.5f), V(b.x + r - 0.5f, b.y - 0.5f), crest[2]);   // the collar
                    break;
                }
                case GearLook.Fatigues:
                {
                    // olive tunic with webbing: a belt, two ammunition pouches and a strap over the shoulder
                    c.Line(V(b.x - r + 1, b.y - 1), V(a.x + r - 1, a.y + 1), P.Khaki[1]);
                    c.Put(c.T(V(a.x - X(1.4f), a.y + X(0.8f))), P.Khaki[1]);
                    c.Put(c.T(V(a.x + X(1.4f), a.y + X(0.8f))), P.Khaki[1]);
                    c.Line(V(b.x - r + 0.5f, b.y - 0.5f), V(b.x + r - 0.5f, b.y - 0.5f), team[2]);   // the collar tabs in the team colour
                    break;
                }
                case GearLook.Jumpsuit:
                {
                    // a one-piece space suit: a silver yoke, a zip and a lightning-bolt badge
                    c.Line(V(b.x - r + 0.5f, b.y - 0.5f), V(b.x + r - 0.5f, b.y - 0.5f), P.Chrome[2]);
                    c.Line(V(b.x - r + 0.5f, b.y - 1.5f), V(b.x + r - 0.5f, b.y - 1.5f), P.Chrome[1]);
                    c.Line(V(a.x + X(0.4f), a.y + 1), V(b.x + X(0.4f), b.y - 2), team[3]);
                    Vector2 badge = Vector2.Lerp(a, b, 0.6f) + V(X(1.2f), 0);
                    c.Put(c.T(badge), Trim(GearSlot.Body, P.Gold[3])); c.Put(c.T(badge + V(-0.5f, -1)), P.Gold[2]);
                    break;
                }
                case GearLook.TacVest:
                {
                    // a plate carrier: pouches across the front, a team-colour flash on the shoulder
                    for (int i = -1; i <= 1; i++) c.FillLocal(new Rect(a.x + i * X(1.6f) - X(0.6f), a.y + X(1), X(1.2f), X(1.6f)), _ => P.Tactical[3]);
                    c.Line(V(b.x - r + 0.5f, b.y - 1), V(b.x + r - 0.5f, b.y - 1), P.Tactical[2]);
                    c.Put(c.T(shNear + V(0, -0.5f)), team[2]); c.Put(c.T(shNear + V(0.5f, -1.5f)), team[2]);
                    break;
                }
                case GearLook.Spellweave:
                {
                    // a long robe stitched with glowing runes, a gold sash at the waist
                    c.Line(V(a.x - X(0.5f), a.y), V(b.x - X(0.5f), b.y - 1), crest[1]);
                    for (float y = a.y + X(1.5f); y < b.y - 1; y += X(2.2f)) c.Put(c.T(V(a.x + X(0.9f), y)), Trim(GearSlot.Body, P.Arcane[3]));
                    c.Line(V(a.x - r + 0.5f, a.y - 1), V(a.x + r - 0.5f, a.y - 1), P.Gold[2]);
                    break;
                }
                case GearLook.GolemBody:
                {
                    // courses of stacked stone with a glowing crack down the chest
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        Inside(l, a, b, r) && (int)(l.y - a.y) % 4 == 3 ? (Color32?)P.Rock[0] : null);
                    c.Line(V(a.x - X(0.8f), a.y + X(1)), V(a.x + X(0.6f), b.y - X(2)), P.Arcane[2]);
                    c.Ellipse(shNear + V(0, 0.5f), X(2.2f), X(1.8f), P.Rock);   // a boulder shoulder
                    c.Put(c.T(Vector2.Lerp(a, b, 0.6f)), Trim(GearSlot.Body, P.Arcane[3]));
                    // a riveted iron band round the waist and a mana core set into the chest: built, not just carved
                    c.Line(V(a.x - r + 0.5f, a.y + X(1)), V(a.x + r - 0.5f, a.y + X(1)), P.Iron[2], S > 1.3f ? 2 : 1);
                    c.Ellipse(Vector2.Lerp(a, b, 0.7f) + V(X(0.8f), 0), X(0.9f), X(0.9f), new[] { P.Mana[1], P.Mana[2], P.Mana[3], P.White });
                    break;
                }
                case GearLook.RunePlate:
                {
                    // moonstone power armour: a mana conduit down the front, a glowing crystal core in the chest
                    c.Line(V(a.x - r + 0.5f, a.y + X(1.5f)), V(a.x + r - 0.5f, a.y + X(1.5f)), P.Steel[1]);
                    c.Line(V(a.x + X(0.6f), a.y + 1), V(b.x + X(0.6f), b.y - 1), P.Mana[2]);
                    c.Ellipse(Vector2.Lerp(a, b, 0.62f) + V(X(0.6f), 0), X(1.1f), X(1.1f), new[] { P.Mana[1], P.Mana[2], P.Mana[3], P.Mana[3] });
                    c.Put(c.T(Vector2.Lerp(a, b, 0.62f) + V(X(0.6f), 0)), Trim(GearSlot.Body, P.White));
                    c.Ellipse(shNear + V(0, 0.5f), X(1.9f), X(1.5f), P.Moonstone);   // a pauldron
                    c.Put(c.T(shNear + V(X(0.4f), X(0.6f))), P.Arcane[3]);
                    break;
                }
                case GearLook.StarPlate:
                {
                    // star-steel power armour: gold at the belt and collar, a star burning in the chest
                    c.Line(V(a.x - r + 0.5f, a.y + X(1.2f)), V(a.x + r - 0.5f, a.y + X(1.2f)), Trim(GearSlot.Body, P.Gold[2]));
                    c.Line(V(b.x - r + 0.5f, b.y - 1), V(b.x + r - 0.5f, b.y - 1), P.Gold[2]);
                    var star = Vector2.Lerp(a, b, 0.6f) + V(X(0.6f), 0);
                    foreach (var o in new[] { V(1, 0), V(-1, 0), V(0, 1), V(0, -1) }) c.Put(c.T(star + o), P.Mana[3]);
                    c.Put(c.T(star), P.White);
                    c.Ellipse(shNear + V(0, 0.5f), X(1.9f), X(1.5f), P.Astral);   // a pauldron
                    c.Put(c.T(shNear + V(0, X(1.2f))), P.Gold[3]);
                    break;
                }
                case GearLook.VoidPlate:
                {
                    // black-violet armour, a seam of magenta light across it, a spike on the shoulder
                    c.Line(V(a.x - X(1), a.y + X(1)), V(b.x + X(1), b.y - X(1)), P.Void[3]);
                    c.Ellipse(shNear + V(0, 0.5f), X(1.8f), X(1.4f), P.Void);
                    c.Line(shNear + V(0, X(1.5f)), shNear + V(X(-0.6f), X(3.2f)), P.Void[2]);
                    break;
                }
                case GearLook.SynthBody:
                {
                    // a smooth synthetic torso with glowing circuit seams
                    c.Line(V(a.x, a.y + 1), V(b.x, b.y - 1), P.Neon[2]);
                    c.Line(V(a.x - X(1.5f), a.y + X(2)), V(a.x - X(0.2f), a.y + X(3.5f)), P.Neon[1]);
                    c.Put(c.T(Vector2.Lerp(a, b, 0.65f) + V(X(1.2f), 0)), Trim(GearSlot.Body, P.Neon[3]));
                    break;
                }
                case GearLook.ChromePlate:
                {
                    // a riveted chrome chest with a grille of lights
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        Inside(l, a, b, r) && (int)(l.y - a.y) % 3 == 2 ? (Color32?)P.Chrome[1] : null);
                    Vector2 g = Vector2.Lerp(a, b, 0.6f);
                    for (int i = -1; i <= 1; i++) c.Put(c.T(g + V(i * X(1.1f), 0)), i == 0 ? P.Ray[2] : P.Red[2]);
                    c.Ellipse(shNear + V(0, 0.5f), X(1.9f), X(1.5f), P.Chrome);
                    break;
                }
                case GearLook.TrenchCoat:
                {
                    // a double row of buttons, a belt with a buckle, and the collar turned up
                    for (float y = a.y + X(1.2f); y < b.y - 1; y += X(1.6f)) { c.Put(c.T(V(a.x - X(0.6f), y)), P.Khaki[0]); c.Put(c.T(V(a.x + X(1.2f), y)), P.Khaki[0]); }
                    c.Line(V(b.x - r + 0.5f, b.y - 0.5f), V(b.x + r - 0.5f, b.y - 0.5f), P.Khaki[3]);
                    break;
                }
                case GearLook.BrassPlate:
                {
                    // riveted plates and a pressure gauge on the chest
                    c.FillLocal(new Rect(a.x - r, a.y, r * 2, b.y - a.y), l =>
                        Inside(l, a, b, r) && (int)(l.y - a.y) % 3 == 2 ? (Color32?)P.Brass[1] : null);
                    Vector2 g = Vector2.Lerp(a, b, 0.62f) + V(X(0.6f), 0);
                    c.Ellipse(g, X(1.3f), X(1.3f), new[] { P.Coal[1], P.Linen[2], P.Linen[3], P.White });
                    c.Put(c.T(g + V(0.5f, 0.5f)), P.Red[2]);
                    c.Ellipse(shNear + V(0, 0.5f), X(1.9f), X(1.5f), P.Brass);   // pauldron
                    c.Put(c.T(shNear + V(0, 1)), Trim(GearSlot.Body, P.Coal[1]));
                    break;
                }
                case GearLook.Robe:
                    c.Line(V(a.x - X(0.5f), a.y), V(b.x - X(0.5f), b.y - 1), crest[1]);   // the robe's front fold
                    c.Line(V(a.x - r + 0.5f, a.y - 1), V(a.x + r - 0.5f, a.y - 1), P.Linen[2]);   // rope belt
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
            if (s.body != GearLook.None && s.body != GearLook.Bare && s.body != GearLook.Robe)
                c.Line(V(a.x - r + 0.5f, a.y), V(a.x + r - 0.5f, a.y), hem);
            if (s.body != GearLook.Robe)
            {
                c.Line(V(a.x - r + 0.5f, a.y - 1), V(a.x + r - 0.5f, a.y - 1), s.raider ? team[2] : P.Leather[1]);
                c.Put(c.T(V(a.x + r - 1.5f, a.y - 0.5f)), s.raider ? team[3] : P.Gold[2]);
            }

            if (s.necklace)
                for (int i = -1; i <= 1; i++)
                    c.Put(c.T(V(sh.x + i * X(1.2f) + 0.5f, sh.y - 1.5f - (i == 0 ? 1 : 0))), P.Bone[3]);
            if (s.trinket == GearLook.Charm)
            {
                c.Line(V(sh.x - 1, sh.y - 1), V(sh.x + 1, sh.y - 2.5f), P.Leather[1]);
                c.Put(c.T(V(sh.x + 1.5f, sh.y - 3.5f)), Trim(GearSlot.Trinket, P.Bone[3]));
                c.Put(c.T(V(sh.x + 1.5f, sh.y - 4.5f)), P.Bone[2]);
            }
            if (s.medic)
            {
                // a strap across the chest to a satchel on the hip, marked with a red cross
                c.Line(V(sh.x - X(1.5f), sh.y - 1), V(hip.x + X(2), hip.y + X(1)), P.Leather[1]);
                Vector2 bag = hip + V(X(2.2f), X(-0.5f));
                c.FillLocal(new Rect(bag.x - X(1.6f), bag.y - X(1.2f), X(3.2f), X(2.6f)), _ => P.Linen[3]);
                c.Put(c.T(bag + V(0, 0.3f)), P.Red[2]);
                c.Put(c.T(bag + V(-1, 0.3f)), P.Red[2]);
                c.Put(c.T(bag + V(1, 0.3f)), P.Red[2]);
                c.Put(c.T(bag + V(0, 1.3f)), P.Red[2]);
                c.Put(c.T(bag + V(0, -0.7f)), P.Red[2]);
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
                case GearLook.GreatHelm:
                {
                    // a flat-topped barrel helm with an eye slit and breaths, the face fully hidden
                    var box = new Rect(hd.x - r - 0.4f, hd.y - X(2.8f), r * 2 + 0.8f, X(6.8f));
                    c.FillLocal(box, l => l.x < box.xMin + X(1.6f) ? P.Steel[1] : l.x > box.xMax - X(1.6f) ? P.Steel[3] : P.Steel[2]);
                    c.Line(V(box.xMin, box.yMax - 0.5f), V(box.xMax - 0.5f, box.yMax - 0.5f), P.Steel[1]);
                    c.Line(V(hd.x - X(0.2f), hd.y + X(0.6f)), V(box.xMax - 0.5f, hd.y + X(0.6f)), P.Ink);   // eye slit
                    rim = Trim(GearSlot.Head, P.Gold[2]);
                    c.Line(V(hd.x + X(1.6f), box.yMin), V(hd.x + X(1.6f), hd.y + X(0.2f)), rim);           // brass cross
                    c.Line(V(hd.x + X(0.4f), hd.y - X(0.4f)), V(box.xMax - 0.5f, hd.y - X(0.4f)), rim);
                    if (!string.IsNullOrEmpty(s.crest))
                        c.Ellipse(V(hd.x, box.yMax + X(1.4f)), X(2.2f), X(1.6f), crest);                  // a crest on top
                    break;
                }
                case GearLook.Morion:
                {
                    Hair(false);
                    c.Ellipse(hd + V(0, 0.8f), r + 0.2f, X(3.0f), P.Steel, l => l.y > hd.y + X(1.2f));
                    // the tall comb along the top
                    for (int i = -2; i <= 2; i++)
                        c.Line(V(hd.x + i * X(0.9f), hd.y + X(3.6f)), V(hd.x + i * X(0.9f), hd.y + X(5.2f) - Mathf.Abs(i) * X(0.5f)), i < 0 ? P.Steel[3] : P.Steel[2]);
                    // the brim, sweeping up to points front and back
                    rim = Trim(GearSlot.Head, P.Steel[1]);
                    c.Line(V(hd.x - r - X(1.5f), hd.y + X(2.2f)), V(hd.x - r, hd.y + X(1.3f)), rim);
                    c.Line(V(hd.x - r, hd.y + X(1.3f)), V(hd.x + r, hd.y + X(1.3f)), rim);
                    c.Line(V(hd.x + r, hd.y + X(1.3f)), V(hd.x + r + X(1.5f), hd.y + X(2.2f)), rim);
                    break;
                }
                case GearLook.FeatheredCap:
                {
                    Hair(false);
                    // a soft slashed cap worn at an angle, with a big feather
                    c.Ellipse(hd + V(X(-0.4f), X(2.0f)), r + X(1.2f), X(1.6f), crest, l => l.y > hd.y + X(1.2f));
                    c.Line(V(hd.x - r - X(1.4f), hd.y + X(1.4f)), V(hd.x + r + X(0.8f), hd.y + X(1.6f)), Trim(GearSlot.Head, crest[1]));
                    for (int i = 0; i <= 7; i++)
                    {
                        float t = i / 7f;
                        Vector2 q = hd + V(X(-1.5f) - X(5) * t, X(3.0f) + X(2.6f) * Mathf.Sin(t * Mathf.PI * 0.8f));
                        c.Line(q, q + V(0, X(1.2f)), t < 0.5f ? P.Linen[3] : P.Linen[2]);
                    }
                    break;
                }
                case GearLook.PlagueMask:
                {
                    // a wide black hat over a leather mask with a long beak and glass eyes
                    c.Ellipse(hd + V(0, 0.3f), r + 0.6f, X(3.6f) + 0.4f, P.Leather);
                    c.Polygon(new[] { hd + V(X(1.6f), X(0.6f)), hd + V(X(7.5f), X(-2.4f)), hd + V(X(1.8f), X(-1.8f)) },
                        l => l.y > hd.y - X(1.0f) ? P.Bone[2] : P.Bone[1]);
                    c.Ellipse(hd + V(X(1.6f), X(0.4f)), X(0.9f), X(0.9f), new[] { P.Coal[2], P.Blue[2], P.Linen[3], P.White });
                    c.Line(V(hd.x - r - X(2.5f), hd.y + X(2.2f)), V(hd.x + r + X(2.5f), hd.y + X(2.2f)), P.Coal[1], S > 1.3f ? 3 : 2);
                    c.FillLocal(new Rect(hd.x - r + X(0.4f), hd.y + X(2.6f), r * 2 - X(0.8f), X(3.0f)), l => l.x < hd.x ? P.Coal[1] : P.Coal[2]);
                    c.Line(V(hd.x - r + X(0.4f), hd.y + X(2.8f)), V(hd.x + r - X(0.4f), hd.y + X(2.8f)), Trim(GearSlot.Head, P.Leather[2]));
                    break;
                }
                case GearLook.Shako:
                case GearLook.Bearskin:
                case GearLook.TopHat:
                {
                    Hair(false);
                    bool bear = s.head == GearLook.Bearskin, top = s.head == GearLook.TopHat;
                    float hw = r + (bear ? X(0.8f) : 0.2f), y0 = hd.y + X(1.4f), y1 = hd.y + X(bear ? 8.0f : top ? 7.4f : 6.4f);
                    var body = bear ? P.Coal : top ? P.Coal : team;
                    c.FillLocal(new Rect(hd.x - hw, y0, hw * 2, y1 - y0), l =>
                    {
                        if (bear)
                        {
                            float dx = (l.x - hd.x) / hw, dy = (l.y - y0) / (y1 - y0);
                            if (dy > 0.55f && dx * dx + (dy - 0.55f) * (dy - 0.55f) * 4.5f > 1f) return null;
                            return ((int)l.x * 3 + (int)l.y) % 4 == 0 ? P.Coal[2] : l.x < hd.x ? P.Coal[1] : P.Coal[0];
                        }
                        return l.x < hd.x - hw + X(1.2f) ? body[2] : l.x > hd.x + hw - X(1.2f) ? body[0] : body[1];
                    });
                    rim = Trim(GearSlot.Head, top ? P.Coal[2] : P.Leather[0]);
                    if (top)
                    {
                        c.Line(V(hd.x - r - X(1.4f), y0), V(hd.x + r + X(1.4f), y0), rim, S > 1.3f ? 2 : 1);   // brim
                        c.Line(V(hd.x - hw, y0 + X(1.2f)), V(hd.x + hw, y0 + X(1.2f)), crest[2], S > 1.3f ? 2 : 1);   // hatband
                    }
                    else if (!bear)
                    {
                        c.Line(V(hd.x, y0), V(hd.x + r + X(2.2f), y0 - X(0.6f)), rim, S > 1.3f ? 2 : 1);      // the peak
                        c.Line(V(hd.x - hw, y1 - 1), V(hd.x + hw, y1 - 1), P.Coal[1]);                        // the crown's top
                        c.Ellipse(V(hd.x + X(0.6f), y0 + X(2.4f)), X(1.0f), X(1.2f), P.Brass);                 // brass plate
                        c.Line(V(hd.x - X(0.6f), y1), V(hd.x - X(0.6f), y1 + X(2.4f)), crest[3], 2);           // plume
                    }
                    else
                    {
                        c.Line(V(hd.x + X(0.6f), y0 + X(1)), V(hd.x + X(1.4f), y1 - X(1)), Trim(GearSlot.Head, crest[2]));   // cord
                    }
                    break;
                }
                case GearLook.TacHelmet:
                {
                    Hair(false);
                    // a matte combat helmet with a glowing visor strip and a little camera on the side
                    c.Ellipse(hd + V(0, X(0.8f)), r + 0.5f, X(3.4f), P.Tactical, l => l.y > hd.y - X(0.2f));
                    c.Line(V(hd.x - X(0.2f), hd.y + X(0.2f)), V(hd.x + r + 0.4f, hd.y + X(0.2f)), Trim(GearSlot.Head, P.Neon[2]), S > 1.3f ? 2 : 1);
                    c.Put(c.T(hd + V(X(-r * 0.6f), X(2))), P.Red[3]);
                    break;
                }
                case GearLook.RuneVisor:
                {
                    Hair(false);
                    // a closed moonstone helm, a glowing mana visor across the eyes and a gold rune on the brow
                    c.Ellipse(hd + V(0, X(0.5f)), r + 0.6f, X(3.7f) + 0.3f, P.Moonstone);
                    c.Line(V(hd.x - X(0.3f), hd.y + X(0.6f)), V(hd.x + r + 0.5f, hd.y + X(0.6f)), Trim(GearSlot.Head, P.Mana[3]), S > 1.3f ? 2 : 1);
                    c.Put(c.T(hd + V(X(0.4f), X(2.6f))), P.Gold[3]);
                    c.Line(V(hd.x - X(0.6f), hd.y + X(3.6f)), V(hd.x - X(1.4f), hd.y + X(5.6f)), P.Steel[2]);   // an antenna
                    c.Put(c.T(hd + V(-X(1.4f), X(5.8f))), P.Arcane[3]);
                    break;
                }
                case GearLook.StarHelm:
                {
                    Hair(false);
                    // a dark star-steel helm, a gold fin along the crown and a T-shaped visor of white light
                    c.Ellipse(hd + V(0, X(0.5f)), r + 0.6f, X(3.7f) + 0.3f, P.Astral);
                    c.Line(V(hd.x - r * 0.9f, hd.y + X(3.6f)), V(hd.x + r * 0.4f, hd.y + X(4.5f)), Trim(GearSlot.Head, P.Gold[2]), S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - X(0.2f), hd.y + X(0.6f)), V(hd.x + r + 0.5f, hd.y + X(0.6f)), P.White);
                    c.Line(V(hd.x + X(1.6f), hd.y - X(1.2f)), V(hd.x + X(1.6f), hd.y + X(0.6f)), P.Mana[3]);
                    break;
                }
                case GearLook.VoidHelm:
                {
                    Hair(false);
                    // a black horned helm, two magenta eyes burning in its slit
                    c.Ellipse(hd + V(0, X(0.5f)), r + 0.6f, X(3.7f) + 0.3f, P.Void);
                    c.Line(V(hd.x - X(0.6f), hd.y + X(2.8f)), V(hd.x - X(2.8f), hd.y + X(6)), P.Void[2], 2);
                    c.Line(V(hd.x + X(1), hd.y + X(3)), V(hd.x + X(2.4f), hd.y + X(6.2f)), P.Void[2], 2);
                    c.Put(c.T(hd + V(X(1.2f), X(0.6f))), P.Void[3]);
                    c.Put(c.T(hd + V(X(2.6f), X(0.6f))), P.Void[3]);
                    break;
                }
                case GearLook.WizardHat:
                {
                    Hair(false);
                    // a tall pointed hat with a wide brim, its tip bent over, a gold star on the front
                    c.Polygon(new[] { V(hd.x - r, hd.y + X(2.2f)), V(hd.x + r, hd.y + X(2.2f)), V(hd.x - X(1.2f), hd.y + X(9.5f)) }, _ => crest[2]);
                    c.Line(V(hd.x - r * 0.3f, hd.y + X(2.4f)), V(hd.x - X(1.2f), hd.y + X(9.5f)), crest[1]);
                    c.Line(V(hd.x - X(1.2f), hd.y + X(9.5f)), V(hd.x - X(3.6f), hd.y + X(8.2f)), crest[2], 2);
                    c.Line(V(hd.x - r - X(2.2f), hd.y + X(2)), V(hd.x + r + X(2.2f), hd.y + X(2)), Trim(GearSlot.Head, crest[1]), S > 1.3f ? 2 : 1);   // brim
                    c.Put(c.T(hd + V(X(0.2f), X(4.6f))), P.Gold[3]);
                    break;
                }
                case GearLook.GolemHead:
                {
                    // a carved block of stone with a glowing rune for eyes and another on the brow
                    c.Ellipse(hd + V(0, 0.3f), r + 0.6f, X(3.6f) + 0.4f, P.Rock);
                    c.Line(V(hd.x + X(0.2f), hd.y + X(0.8f)), V(hd.x + r, hd.y + X(0.8f)), Trim(GearSlot.Head, P.Arcane[3]), S > 1.3f ? 2 : 1);
                    c.Put(c.T(hd + V(-X(0.8f), X(2.6f))), P.Arcane[2]);
                    c.Line(V(hd.x - r, hd.y - X(1)), V(hd.x - r * 0.3f, hd.y + X(1.5f)), P.Rock[0]);   // a crack
                    break;
                }
                case GearLook.SynthHead:
                {
                    // a smooth, featureless synthetic head with a cyan visor band
                    c.Ellipse(hd + V(0, 0.3f), r + 0.4f, X(3.6f) + 0.3f, P.Synth);
                    c.Line(V(hd.x - X(0.6f), hd.y + X(0.6f)), V(hd.x + r + 0.3f, hd.y + X(0.6f)), Trim(GearSlot.Head, P.Neon[2]), S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - r * 0.4f, hd.y + X(3.2f)), V(hd.x - r * 0.4f, hd.y - X(2)), P.Synth[1]);   // a seam
                    break;
                }
                case GearLook.BubbleHelmet:
                {
                    Hair(false);
                    // a glass fishbowl helmet with a chrome collar and a little antenna
                    var glass = new[] { new Color32(150, 210, 240, 90), new Color32(180, 230, 250, 110), new Color32(210, 240, 255, 130), new Color32(255, 255, 255, 190) };
                    c.Ellipse(hd + V(0, X(0.4f)), r + X(1.6f), X(3.6f) + X(1.6f), glass);
                    c.Put(c.T(hd + V(X(-1.6f), X(3.2f))), P.White);   // the glint
                    c.Line(V(hd.x - r - X(1), hd.y - X(3.2f)), V(hd.x + r + X(1), hd.y - X(3.2f)), Trim(GearSlot.Head, P.Chrome[2]), S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - X(1.5f), hd.y + X(5)), V(hd.x - X(2.2f), hd.y + X(7)), P.Chrome[1]);
                    c.Put(c.T(V(hd.x - X(2.2f), hd.y + X(7.4f))), P.Red[3]);
                    break;
                }
                case GearLook.RobotHead:
                {
                    // a boxy chrome head: two round glowing eyes, a grille mouth and ear bolts
                    var box = new Rect(hd.x - r, hd.y - X(3), r * 2 + X(0.4f), X(6.6f));
                    c.FillLocal(box, l => l.x < box.xMin + X(1.4f) ? P.Chrome[3] : l.x > box.xMax - X(1.4f) ? P.Chrome[1] : P.Chrome[2]);
                    c.Line(V(box.xMin, box.yMax - 0.5f), V(box.xMax - 0.5f, box.yMax - 0.5f), Trim(GearSlot.Head, P.Chrome[1]));
                    c.Ellipse(hd + V(X(1.6f), X(0.8f)), X(0.9f), X(0.9f), P.Ray);
                    c.Line(V(hd.x + X(0.2f), hd.y - X(1.6f)), V(box.xMax - 1, hd.y - X(1.6f)), P.Coal[1]);
                    c.Ellipse(hd + V(X(-r * 0.3f - 1), X(0.4f)), X(0.8f), X(0.8f), P.Iron);
                    c.Line(V(hd.x, box.yMax), V(hd.x, box.yMax + X(2)), P.Chrome[1]);
                    c.Put(c.T(V(hd.x, box.yMax + X(2.4f))), P.Red[3]);
                    break;
                }
                case GearLook.SteelHelmet:
                {
                    Hair(false);
                    // a wide flat steel helmet with a chin strap
                    c.Ellipse(hd + V(0, X(1.0f)), r + 0.3f, X(3.0f), P.Olive, l => l.y > hd.y + X(1.0f));
                    rim = Trim(GearSlot.Head, P.Olive[0]);
                    c.Line(V(hd.x - r - X(1.0f), hd.y + X(1.0f)), V(hd.x + r + X(1.0f), hd.y + X(1.0f)), rim, S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x + X(1.2f), hd.y + X(1.0f)), V(hd.x + X(1.6f), hd.y - X(2.4f)), P.Leather[1]);
                    break;
                }
                case GearLook.Fedora:
                {
                    Hair(false);
                    // a pinched felt hat with a dark band, its brim snapped down at the front
                    c.Ellipse(hd + V(0, X(2.4f)), r - 0.2f, X(2.0f), P.Cloth, l => l.y > hd.y + X(1.6f));
                    c.Line(V(hd.x - r - X(1.6f), hd.y + X(1.6f)), V(hd.x + r + X(1.8f), hd.y + X(1.3f)), Trim(GearSlot.Head, P.Cloth[1]), S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - r + 0.4f, hd.y + X(2.1f)), V(hd.x + r - 0.4f, hd.y + X(2.1f)), crest[1]);
                    c.Put(c.T(hd + V(0, X(4.2f))), P.Cloth[1]);   // the pinch
                    break;
                }
                case GearLook.Tricorne:
                {
                    Hair(false);
                    // a black felt hat cocked up on three sides, edged in lace
                    c.Ellipse(hd + V(0, X(1.6f)), r + 0.2f, X(2.2f), P.Coal, l => l.y > hd.y + X(1.4f));
                    rim = Trim(GearSlot.Head, crest[3]);
                    c.Line(V(hd.x - r - X(2.2f), hd.y + X(3.0f)), V(hd.x - r + X(0.4f), hd.y + X(1.4f)), P.Coal[1], S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - r + X(0.4f), hd.y + X(1.4f)), V(hd.x + r + X(1.8f), hd.y + X(1.4f)), P.Coal[2], S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x + r + X(1.8f), hd.y + X(1.4f)), V(hd.x + r + X(2.4f), hd.y + X(2.8f)), P.Coal[1], S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x - r - X(2.2f), hd.y + X(3.4f)), V(hd.x + r + X(2.4f), hd.y + X(3.2f)), rim);   // lace edging
                    c.Put(c.T(hd + V(X(-1.2f), X(2.4f))), crest[2]);                                          // cockade
                    break;
                }
                case GearLook.Busby:
                {
                    Hair(false);
                    // a tall fur cap with a coloured bag hanging down one side and a plume
                    float y0 = hd.y + X(1.4f), y1 = hd.y + X(6.2f);
                    c.FillLocal(new Rect(hd.x - r, y0, r * 2, y1 - y0), l =>
                        ((int)l.x * 3 + (int)l.y) % 4 == 0 ? P.Fur[2] : l.x < hd.x ? P.Fur[1] : P.Fur[0]);
                    c.Line(V(hd.x - r, y1 - X(0.6f)), V(hd.x - r - X(1.6f), y0 + X(0.8f)), crest[2], S > 1.3f ? 3 : 2);   // the bag
                    c.Line(V(hd.x + X(0.6f), y1), V(hd.x + X(0.6f), y1 + X(2.6f)), Trim(GearSlot.Head, P.White), 2);   // plume
                    c.Put(c.T(V(hd.x + X(0.6f), y1 + X(2.8f))), crest[3]);
                    break;
                }
                case GearLook.Goggles:
                {
                    Hair(false);
                    // a leather flying cap with brass goggles pushed up on the brow
                    c.Ellipse(hd + V(-0.2f, 0.4f), r + 0.6f, X(3.6f) + 0.4f, P.Leather, l => l.y > hd.y + X(0.6f) || l.x < hd.x - X(1.8f));
                    Vector2 g = hd + V(X(1.2f), X(1.5f));
                    c.Line(V(hd.x - r, g.y), g, P.Leather[0], S > 1.3f ? 2 : 1);
                    c.Ellipse(g, X(1.3f), X(1.1f), new[] { P.Brass[1], P.Brass[2], P.Aether[2], P.Aether[3] });
                    c.Ellipse(g + V(X(-2.2f), 0), X(1.1f), X(1.0f), new[] { P.Brass[0], P.Brass[1], P.Aether[1], P.Aether[2] });
                    c.Put(c.T(g + V(X(1.4f), 0)), Trim(GearSlot.Head, P.Brass[3]));
                    break;
                }
                case GearLook.BoilerHelm:
                {
                    // a riveted brass dome over the whole head, a glowing slit, a little stack venting steam
                    c.Ellipse(hd + V(0, 0.4f), r + 0.9f, X(3.6f) + 0.9f, P.Brass);
                    c.Line(V(hd.x - X(0.6f), hd.y + X(0.4f)), V(hd.x + r + 0.4f, hd.y + X(0.4f)), P.Coal[0], S > 1.3f ? 2 : 1);
                    c.Line(V(hd.x + X(0.6f), hd.y + X(0.4f)), V(hd.x + r - 0.2f, hd.y + X(0.4f)), Trim(GearSlot.Head, P.Aether[3]));
                    for (int i = -2; i <= 2; i++) c.Put(c.T(hd + V(i * X(1.3f), X(-1.6f))), P.Brass[0]);   // rivets
                    c.Line(V(hd.x - X(1.4f), hd.y + X(3.6f)), V(hd.x - X(1.4f), hd.y + X(5.6f)), P.Iron[2], 2);
                    c.Put(c.T(V(hd.x - X(1.4f), hd.y + X(6.6f))), new Color32(230, 236, 240, 180));
                    break;
                }
                case GearLook.KettleHat:
                    Hair(false);
                    c.Ellipse(hd + V(0, 0.6f), r + 0.4f, X(3.6f) + 0.3f, P.Steel, l => l.y > hd.y + X(1.4f));
                    rim = Trim(GearSlot.Head, P.Steel[1]);
                    c.Line(V(hd.x - r - X(2.2f), hd.y + X(1.2f)), V(hd.x + r + X(2.2f), hd.y + X(1.2f)), rim, S > 1.3f ? 2 : 1);   // wide brim
                    c.Line(V(hd.x - r - X(1.6f), hd.y + X(1.6f)), V(hd.x + r + X(1.6f), hd.y + X(1.6f)), P.Steel[2]);
                    break;
                case GearLook.Cowl:
                {
                    // a deep hood in the robe's colour, the face in its shadow
                    c.Ellipse(hd + V(-0.5f, 0.5f), r + 1.2f, X(3.6f) + 1.0f, crest, l => l.y > hd.y + X(0.2f) || l.x < hd.x - X(0.6f));
                    c.Line(V(hd.x - r - 1.2f, hd.y - 1), V(hd.x - r - X(1.2f), hd.y - X(3.2f)), crest[1], 2);   // drape behind
                    c.Line(V(hd.x - X(0.4f), hd.y + X(0.4f)), V(hd.x + r, hd.y + X(0.4f)), Trim(GearSlot.Head, crest[1]));
                    break;
                }
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
            if (s.Class == WeaponClass.Crossbow || LongGun(s))
            {
                Arm(shFar, hFar, true);
                return;
            }
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
            if (s.Class == WeaponClass.Crossbow)
            {
                Crossbow();
                Arm(shNear, hNear, false);
                return;
            }
            if (LongGun(s))
            {
                Arquebus();
                Arm(shNear, hNear, false);
                return;
            }
            if (s.weapon == GearLook.Cannon && s.mount != MountKind.Cannon && s.mount != MountKind.Starship && s.mount != MountKind.HoverThrone) BastionGun();
            if (s.Class == WeaponClass.Bow)
            {
                Arm(shNear, hNear, false);
                Bow(hNear);
                return;
            }
            if (s.shield != GearLook.None && p.death < 2)
            {
                Arm(shNear, hNear, false);
                bool tall = s.shield == GearLook.TowerShield || s.shield == GearLook.HideShield || s.shield == GearLook.OvalShield || s.shield == GearLook.KiteShield;
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
            if (s.body == GearLook.Robe)
            {
                // wide robe sleeves to the wrist
                c.Line(from, Vector2.Lerp(from, hand, 0.85f), far ? crest[1] : crest[2], limb + 1);
                c.FillLocal(new Rect(hand.x - 0.5f, hand.y - 0.5f, limb, limb), _ => far ? skinRamp[1] : skinRamp[2]);
                return;
            }
            Color32 skinCol = far ? skin[1] : skin[2];
            c.Line(from, hand, skinCol, limb);
            if (s.medic && !far)
            {
                Vector2 band = Vector2.Lerp(from, hand, 0.55f);
                c.Line(band + V(-0.5f, 0), band + V(0.5f, 0), P.White, limb);   // white armband
            }
            if (sleeve)
            {
                var sl = s.body == GearLook.Mail || s.body == GearLook.Plate || s.body == GearLook.Surcoat ? P.Steel : s.body == GearLook.Scale ? P.Bronze
                    : s.body == GearLook.Jerkin ? P.Linen : s.body == GearLook.Doublet ? crest : s.body == GearLook.BrassPlate ? P.Brass
                    : s.body == GearLook.Fatigues ? P.Olive : s.body == GearLook.TrenchCoat ? P.Khaki : s.body == GearLook.ChromePlate ? P.Chrome
                    : s.body == GearLook.TacVest ? P.Tactical : s.body == GearLook.SynthBody ? P.Synth
                    : s.body == GearLook.GolemBody ? P.Rock : s.body == GearLook.Spellweave ? crest : s.body == GearLook.RunePlate ? P.Moonstone
                    : s.body == GearLook.StarPlate ? P.Astral : s.body == GearLook.VoidPlate ? P.Void : team;
                // coat sleeves reach the wrist, with a cuff in the collar colour; fatigues get a team armband
                bool longSleeve = s.body == GearLook.Greatcoat || s.body == GearLook.TrenchCoat || s.body == GearLook.Fatigues
                    || s.body == GearLook.Jumpsuit || s.body == GearLook.ChromePlate || s.body == GearLook.TacVest || s.body == GearLook.SynthBody
                    || s.body == GearLook.GolemBody || s.body == GearLook.Spellweave || s.body == GearLook.RunePlate
                    || s.body == GearLook.StarPlate || s.body == GearLook.VoidPlate;
                c.Line(from, Vector2.Lerp(from, hand, longSleeve ? 0.85f : 0.4f), far ? sl[1] : sl[2], limb);
                if (s.body == GearLook.Greatcoat) c.Put(c.T(Vector2.Lerp(from, hand, 0.82f)), crest[2]);
                if (s.body == GearLook.Fatigues && !far) c.Put(c.T(Vector2.Lerp(from, hand, 0.3f)), team[2]);
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
                case GearLook.Pike:
                {
                    float len = X(20), butt = X(6);
                    c.Line(at(-butt), at(len), P.Wood[2], ww);
                    c.Polygon(new[] { at(len, -X(1.1f)), at(len + X(4), 0), at(len, X(1.1f)) }, _ => P.Steel[3]);
                    c.Line(at(len - X(2)), at(len), Trim(GearSlot.Weapon, P.Steel[1]), ww);   // langets
                    break;
                }
                case GearLook.Lance:
                {
                    float len = X(riding ? 22 : 15), butt = X(4);
                    c.Line(at(-butt), at(len), P.Wood[3], ww + 1);
                    c.Line(at(len * 0.55f), at(len), P.Wood[2], ww);   // tapering
                    c.Polygon(new[] { at(len, -X(0.9f)), at(len + X(2.5f), 0), at(len, X(0.9f)) }, _ => P.Steel[3]);
                    c.Polygon(new[] { at(X(1), -X(2)), at(X(3), 0), at(X(1), X(2)) }, _ => Trim(GearSlot.Weapon, P.Steel[2]));   // vamplate
                    // a pennant in the team colour just behind the tip
                    Vector2 pen = at(len - X(2.5f));
                    c.Polygon(new[] { pen, pen + V(X(-4), X(-1.2f)), pen + V(X(-4), X(1.6f)) }, l => team[2]);
                    break;
                }
                case GearLook.Mace:
                {
                    float len = X(8);
                    c.Line(at(-2), at(len), P.Wood[1], ww);
                    c.Ellipse(at(len), X(1.6f), X(1.6f), P.Steel);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        c.Put(c.T(at(len, X(2.2f) * i)), Trim(GearSlot.Weapon, P.Steel[3]));
                        c.Put(c.T(at(len + X(2.2f) * i * 0.6f)), P.Steel[3]);
                    }
                    break;
                }
                case GearLook.RuneStaff:
                {
                    float len = X(13);
                    c.Line(at(-X(5)), at(len), P.Wood[1], ww);
                    c.Line(at(len - X(2), -X(1)), at(len + X(1), -X(1)), P.Wood[2]);   // the crook holding the stone
                    var stone = P.Rune;
                    bool lit = p.loaded || !p.attack;
                    c.Ellipse(at(len + X(1.5f)), X(1.6f), X(1.9f), lit ? stone : new[] { P.Coal[1], P.Coal[2], stone[1], stone[2] });
                    if (p.loaded && p.attack)
                        foreach (var (t, side) in new[] { (2.5f, 2.5f), (3.5f, -2f), (0.5f, -3f) })
                            c.Put(c.T(at(len + X(t), X(side))), stone[3]);   // gathering sparks
                    break;
                }
                case GearLook.Halberd:
                {
                    float len = X(15), butt = X(5);
                    c.Line(at(-butt), at(len), P.Wood[2], ww);
                    c.Polygon(new[] { at(len, -X(0.8f)), at(len + X(3), 0), at(len, X(0.8f)) }, _ => P.Steel[3]);   // spike
                    c.Polygon(new[] { at(len - X(3.5f), X(0.5f)), at(len - X(0.5f), X(0.5f)), at(len, X(3.4f)), at(len - X(4), X(3))},
                        _ => Trim(GearSlot.Weapon, P.Steel[2]));                                                     // axe blade
                    c.Line(at(len - X(2), -X(0.5f)), at(len - X(3), -X(2.2f)), P.Steel[2]);                        // back hook
                    break;
                }
                case GearLook.Zweihander:
                {
                    float len = X(13);
                    c.Line(at(-3), at(0), P.Leather[2], ww);
                    c.Line(at(1, -X(2.4f)), at(1, X(2.4f)), Trim(GearSlot.Weapon, P.Steel[1]), ww);   // wide crossguard
                    c.Line(at(2), at(len), P.Steel[3], ww + 1);
                    c.Line(at(3, -0.7f), at(len - 1, -0.7f), P.Steel[2]);
                    c.Put(c.T(at(len + 1)), P.Steel[2]);
                    break;
                }
                case GearLook.Pistol:
                {
                    c.Line(at(-X(1)), at(X(4)), P.Iron[1], ww + 1);         // barrel
                    c.Line(at(-X(1.5f), -X(0.5f)), at(-X(2.5f), -X(2)), P.Wood[2], ww + 1);   // butt
                    c.Put(c.T(at(X(1), X(1))), Trim(GearSlot.Weapon, P.Gold[2]));               // wheel-lock
                    if (p.release) muzzle = at(X(5));
                    break;
                }
                case GearLook.Grenade:
                    if (s.mount == MountKind.Airship) break;   // bombs drop from the gondola, not the hand
                    if (!p.loaded && p.attack) break;     // thrown
                    c.Ellipse(hand + V(0, X(-0.2f)), X(1.7f), X(1.7f), P.Iron);
                    c.Line(hand + V(X(0.6f), X(1.4f)), hand + V(X(1.4f), X(2.6f)), P.Linen[2]);          // fuse
                    c.Put(c.T(hand + V(X(1.6f), X(3))), p.loaded ? P.Fire[3] : P.Fire[2]);
                    break;
                case GearLook.AetherRod:
                {
                    // a brass rod wound with copper coil, an aether globe crackling at the tip
                    float len = X(12);
                    c.Line(at(-X(4)), at(len), P.Brass[1], ww);
                    for (float t = len * 0.45f; t < len - 1; t += X(1.2f)) c.Put(c.T(at(t, X(0.8f))), P.Bronze[3]);
                    var orb = P.Aether;
                    c.Ellipse(at(len + X(1.6f)), X(1.7f), X(1.7f), p.loaded || !p.attack ? orb : new[] { P.Coal[1], orb[0], orb[1], orb[2] });
                    if (p.loaded && p.attack)
                        foreach (var (t, side) in new[] { (3f, 2.2f), (2f, -2.6f), (4f, -0.6f) })
                            c.Put(c.T(at(len + X(t), X(side))), orb[3]);
                    break;
                }
                case GearLook.ManaStaff:
                {
                    // a pale wooden staff, a mana crystal floating in its forked head
                    float len = X(13);
                    c.Line(at(-X(5)), at(len), P.Linen[1], ww);
                    c.Line(at(len - X(1.5f), -X(1.2f)), at(len + X(0.5f), -X(1.6f)), P.Linen[1]);
                    c.Line(at(len - X(1.5f), X(1.2f)), at(len + X(0.5f), X(1.6f)), P.Linen[1]);
                    var crystal = P.Mana;
                    bool lit = p.loaded || !p.attack;
                    c.Polygon(new[] { at(len + X(0.5f)), at(len + X(2.5f), -X(1.3f)), at(len + X(4.5f)), at(len + X(2.5f), X(1.3f)) },
                        _ => lit ? crystal[2] : crystal[0]);
                    c.Put(c.T(at(len + X(2), X(0.4f))), lit ? crystal[3] : crystal[1]);
                    if (p.loaded && p.attack)
                        foreach (var (t, side) in new[] { (5.5f, 2f), (4f, -2.6f), (1.5f, 2.6f) })
                            c.Put(c.T(at(len + X(t), X(side))), crystal[3]);
                    break;
                }
                case GearLook.StarBlade:
                {
                    // a gold hilt holding a blade of violet starlight, gold sparks drifting in it
                    float len = X(11);
                    bool voidBlade = s.raider;
                    var glow = voidBlade ? P.Void : P.Arcane;
                    c.Line(at(-2), at(0), voidBlade ? P.Void[1] : P.Astral[1], ww);
                    c.Line(at(1, -X(1.6f)), at(1, X(1.6f)), Trim(GearSlot.Weapon, voidBlade ? P.Void[2] : P.Gold[2]), ww);
                    c.Line(at(2), at(len), glow[3], ww + 1);
                    c.Line(at(2, -0.6f), at(len - 1, -0.6f), P.White);
                    for (float t = X(3); t < len - 1; t += X(2.5f)) c.Put(c.T(at(t, 0.5f)), voidBlade ? P.Void[2] : P.Gold[3]);
                    c.Put(c.T(at(len + 1)), glow[2]);
                    break;
                }
                case GearLook.BeamStaff:
                {
                    // a star-steel staff with a gold ring at its head, a captured star burning in the ring
                    float len = X(13);
                    c.Line(at(-X(5)), at(len), s.raider ? P.Void[1] : P.Steel[2], ww);
                    for (float t = 0; t < len - X(2); t += X(3)) c.Put(c.T(at(t)), s.raider ? P.Void[3] : P.Mana[2]);
                    Vector2 ring = at(len + X(2));
                    c.Ellipse(ring, X(2.2f), X(2.2f), s.raider ? P.Void : P.Gold);
                    bool lit = p.loaded || !p.attack;
                    c.Ellipse(ring, X(1.1f), X(1.1f), lit ? new[] { P.Gold[3], P.White, P.White, P.White } : P.Astral);
                    if (p.loaded && p.attack)
                        foreach (var (t, side) in new[] { (5f, 2.2f), (4f, -2.6f), (1.5f, 2.6f) })
                            c.Put(c.T(at(len + X(t), X(side))), P.Gold[3]);
                    break;
                }
                case GearLook.Runeblade:
                {
                    // a long steel sword, runes glowing violet along the fuller
                    float len = X(11);
                    c.Line(at(-2), at(0), P.Leather[2], ww);
                    c.Line(at(1, -X(1.6f)), at(1, X(1.6f)), Trim(GearSlot.Weapon, P.Gold[2]), ww);
                    c.Line(at(2), at(len), P.Steel[3], ww + 1);
                    for (float t = X(3); t < len - 1; t += X(2)) c.Put(c.T(at(t, -0.5f)), P.Arcane[3]);
                    c.Put(c.T(at(len + 1)), P.Steel[2]);
                    break;
                }
                case GearLook.EnergyBlade:
                {
                    // a glowing energy sword: a black hilt and a blade of cyan light
                    float len = X(10);
                    c.Line(at(-2), at(0), P.Tactical[2], ww);
                    c.Line(at(1, -X(1)), at(1, X(1)), Trim(GearSlot.Weapon, P.Tactical[3]));
                    c.Line(at(2), at(len), P.Neon[3], ww + 1);
                    c.Line(at(2, -0.8f), at(len - 1, -0.8f), P.Neon[2]);
                    c.Put(c.T(at(len + 1)), P.Neon[2]);
                    break;
                }
                case GearLook.RayGun:
                {
                    // a finned retro ray pistol: a chrome barrel ringed with coils, a bulb at the tip
                    c.Line(at(-X(1)), at(X(4)), P.Chrome[2], ww + 1);
                    c.Line(at(-X(1.5f), -X(0.5f)), at(-X(2.5f), -X(2)), P.Red[2], ww + 1);   // grip
                    c.Put(c.T(at(X(1), X(1))), P.Red[3]); c.Put(c.T(at(X(2), X(1))), P.Red[3]);   // fins
                    c.Ellipse(at(X(4.6f)), X(0.9f), X(0.9f), p.loaded || !p.attack ? P.Ray : P.Chrome);
                    if (p.release) muzzle = at(X(5.5f));
                    break;
                }
                case GearLook.Claw:
                {
                    // a robot's pincer: two curved chrome jaws
                    float len = X(6);
                    c.Line(at(-1), at(len * 0.5f), P.Chrome[1], ww + 1);
                    bool open = !p.attack || p.loaded;
                    c.Line(at(len * 0.5f), at(len, open ? X(2) : X(0.6f)), P.Chrome[3], ww);
                    c.Line(at(len * 0.5f), at(len, open ? -X(2) : -X(0.6f)), Trim(GearSlot.Weapon, P.Chrome[2]), ww);
                    break;
                }
                case GearLook.Pick:
                {
                    // a miner's pick: a long haft and a curved iron head, one end pointed
                    float len = X(9);
                    c.Line(at(-2), at(len), P.Wood[2], ww);
                    Vector2 h0 = at(len, -X(3)), h1 = at(len + X(1), X(3.4f));
                    c.Line(h0, at(len + X(0.6f)), Trim(GearSlot.Weapon, P.Iron[2]), ww + 1);
                    c.Line(at(len + X(0.6f)), h1, P.Iron[3], ww + 1);
                    break;
                }
                case GearLook.Shovel:
                {
                    // a short entrenching shovel, the blade sharpened for close work
                    float len = X(8);
                    c.Line(at(-2), at(len), P.Wood[2], ww);
                    c.Polygon(new[] { at(len, -X(1.6f)), at(len + X(3.4f), -X(1.2f)), at(len + X(3.8f), 0), at(len + X(3.4f), X(1.2f)), at(len, X(1.6f)) },
                        _ => Trim(GearSlot.Weapon, P.Iron[2]));
                    c.Line(at(len + X(0.5f), -X(1.2f)), at(len + X(3), -X(0.8f)), P.Iron[3]);
                    break;
                }
                case GearLook.Sabre:
                {
                    // a curved light-cavalry sabre with a brass stirrup hilt
                    float len = X(10);
                    c.Line(at(-2), at(0), P.Leather[1], ww);
                    c.Line(at(-2, X(1.2f)), at(1, X(1.2f)), Trim(GearSlot.Weapon, P.Brass[2]));   // knuckle-bow
                    c.Line(at(1, -X(1.2f)), at(1, X(1.2f)), P.Brass[2]);
                    Vector2 prev = at(1.5f);
                    for (int i = 1; i <= 8; i++)
                    {
                        float t = i / 8f;
                        Vector2 q = at(1.5f + (len - 1.5f) * t, -X(1.8f) * t * t);   // the curve sweeps back along the edge
                        c.Line(prev, q, i == 8 ? P.Steel[2] : P.Steel[3], ww);
                        prev = q;
                    }
                    break;
                }
                case GearLook.LeydenRod:
                {
                    // a brass rod topped with a glass Leyden jar, blue-white sparks dancing on its knob
                    float len = X(11);
                    c.Line(at(-X(4)), at(len - X(2)), P.Brass[1], ww);
                    var glass = new[] { P.Water[1], P.Water[2], P.Water[3], P.White };
                    c.Ellipse(at(len), X(1.6f), X(2.2f), glass);
                    c.Line(at(len - X(1.2f), X(1.5f)), at(len - X(1.2f), -X(1.5f)), P.Brass[2]);   // foil band
                    c.Ellipse(at(len + X(2.6f)), X(0.9f), X(0.9f), P.Brass);
                    if (p.loaded || !p.attack)
                        foreach (var (t, side) in new[] { (3.6f, 1.4f), (3.4f, -1.6f), (4.4f, 0.2f) })
                            c.Put(c.T(at(len + X(t), X(side))), p.loaded ? P.White : P.Water[4]);
                    break;
                }
                case GearLook.Wrench:
                {
                    float len = X(8);
                    c.Line(at(-2), at(len), P.Iron[2], ww + 1);
                    c.Line(at(len, -X(1.6f)), at(len + X(2), -X(1.6f)), P.Iron[3], ww);   // the open jaw
                    c.Line(at(len, X(1.6f)), at(len + X(2), X(1.6f)), P.Iron[3], ww);
                    c.Line(at(len, -X(1.6f)), at(len, X(1.6f)), Trim(GearSlot.Weapon, P.Iron[2]), ww);
                    break;
                }
                case GearLook.PistonHammer:
                {
                    // an iron haft and a brass hammer block driven by a piston
                    float len = X(11);
                    c.Line(at(-3), at(len), P.Iron[1], ww + 1);
                    c.Polygon(new[] { at(len - X(1.5f), -X(3)), at(len + X(2.5f), -X(3)), at(len + X(2.5f), X(3)), at(len - X(1.5f), X(3)) },
                        l => Vector2.Dot(l - at(len), n) > X(1.5f) ? Trim(GearSlot.Weapon, P.Brass[3]) : P.Brass[2]);
                    c.Line(at(len * 0.55f, X(1.2f)), at(len - X(1.5f), X(1.2f)), P.Steel[3]);   // the piston rod
                    if (p.smear) c.Put(c.T(at(len + X(3.5f))), new Color32(230, 236, 240, 200));
                    break;
                }
                case GearLook.Flask:
                    if (!p.loaded && p.attack) break;     // thrown
                    c.Line(hand, hand + V(0, X(1.6f)), P.Linen[3]);                                   // the neck and its rag
                    c.Ellipse(hand + V(0, X(-0.4f)), X(1.6f), X(1.6f), new[] { P.Green[0], P.Green[2], P.Green[3], P.White });
                    c.Put(c.T(hand + V(0, X(2))), P.Fire[2]);                                         // the lit rag
                    break;
                case GearLook.Cannon when s.mount == MountKind.Crawler || s.mount == MountKind.Tank || s.mount == MountKind.Starship || s.mount == MountKind.HoverThrone:
                    break;   // the commander works the gun from inside
                case GearLook.Cannon:
                {
                    // the linstock: a staff holding a smouldering match
                    float len = X(9);
                    c.Line(at(-X(2)), at(len), P.Wood[2], ww);
                    c.Line(at(len), at(len + X(1.5f), X(1)), P.Iron[2]);
                    c.Put(c.T(at(len + X(1.8f), X(1.2f))), p.loaded ? P.Fire[3] : P.Fire[1]);
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

        private Vector2 muzzle = new Vector2(-1, -1);

        private void Arquebus()
        {
            // A long matchlock: butt at the shoulder, barrel out past the near hand.
            bool aiming = p.attack && p.hNear.y > -2;
            Vector2 butt = aiming ? hFar + V(X(-1.5f), 0) : hFar + V(X(-1), X(-1));
            Vector2 d = aiming ? V(1, 0) : V(0.8f, 0.6f);
            bool rifle = s.weapon == GearLook.Rifle || s.weapon == GearLook.BoltRifle, musket = s.weapon == GearLook.Musket;
            if (s.weapon == GearLook.AssaultRifle || s.weapon == GearLook.PlasmaRifle)
            {
                // a matte black assault rifle (or a plasma rifle with a glowing coil), a curved magazine below
                bool plasma = s.weapon == GearLook.PlasmaRifle;
                Vector2 rear = butt + d * X(2.5f), front = butt + d * X(13);
                c.Line(butt, rear, P.Tactical[1], limb + 1);
                c.Line(rear, front, P.Tactical[2], limb);
                c.Line(rear + d * X(2) + V(0, -X(0.5f)), rear + d * X(3) + V(0, -X(2.5f)), P.Tactical[1], 2);   // magazine
                c.Put(c.T(rear + d * X(4) + V(0, X(1.2f))), Trim(GearSlot.Weapon, P.Tactical[3]));               // sight
                if (plasma) for (int i = 0; i < 3; i++) c.Put(c.T(rear + d * X(6 + i * 2)), P.Neon[2]);
                muzzle = front + d * X(1);
                return;
            }
            if (s.weapon == GearLook.ManaRifle)
            {
                // a staff made into a rifle: a pale wooden stock, a moonstone barrel and a mana crystal for a chamber
                Vector2 rear = butt + d * X(3), front = butt + d * X(14);
                c.Line(butt, rear, P.Linen[1], limb + 1);
                c.Line(rear, front, P.Moonstone[2], limb);
                c.Line(rear + d * X(1) + V(0, X(1)), rear + d * X(4) + V(0, X(1)), P.Gold[2]);
                bool lit = p.loaded || !p.attack;
                c.Ellipse(rear + d * X(2.5f) + V(0, X(0.4f)), X(1.4f), X(1.2f), lit ? P.Mana : new[] { P.Coal[1], P.Mana[0], P.Mana[1], P.Mana[1] });
                for (int i = 0; i < 3; i++) c.Put(c.T(rear + d * X(6 + i * 2)), P.Arcane[(i + (lit ? 1 : 0)) % 2 == 0 ? 3 : 2]);
                c.Ellipse(front + d * X(0.6f), X(0.9f), X(0.9f), lit ? P.Mana : P.Moonstone);
                muzzle = front + d * X(1.2f);
                return;
            }
            if (s.weapon == GearLook.RayRifle)
            {
                // a long chrome ray rifle: a ribbed barrel, red fins, a glowing emitter
                Vector2 rear = butt + d * X(2), front = butt + d * X(13);
                c.Line(butt, rear, P.Red[2], limb + 1);
                c.Line(rear, front, P.Chrome[2], limb);
                for (int i = 0; i < 4; i++) c.Put(c.T(rear + d * X(3 + i * 2) + V(0, X(1))), P.Chrome[1]);
                c.Put(c.T(rear + d * X(2) + V(0, -X(1.4f))), P.Red[3]);
                c.Ellipse(front + d * X(0.8f), X(1.1f), X(1.1f), p.loaded || !p.attack ? P.Ray : P.Chrome);
                muzzle = front + d * X(1.5f);
                return;
            }
            if (s.weapon == GearLook.MachineGun || s.weapon == GearLook.Tommygun)
            {
                bool mg = s.weapon == GearLook.MachineGun;
                Vector2 rear = butt + d * X(mg ? 2 : 1.5f), front = butt + d * X(mg ? 15 : 10);
                c.Line(butt, rear, P.Wood[1], limb + 1);
                c.Line(rear, front, P.Iron[1], limb + (mg ? 1 : 0));
                if (mg)
                {
                    c.Line(rear + d * X(3), front - d * X(1), P.Iron[2], limb + 2);                 // the cooling jacket
                    for (int i = 0; i < 4; i++) c.Put(c.T(rear + d * X(4 + i * 2) + V(0, X(1.2f))), P.Iron[0]);
                    c.Line(rear + V(0, -X(1)), rear + V(X(-1.5f), -X(4)), P.Brass[2]);              // the ammunition belt
                }
                else
                    c.Ellipse(rear + d * X(3) + V(0, -X(1.8f)), X(1.6f), X(1.6f), P.Iron);           // drum magazine
                c.Put(c.T(rear + d * X(1) + V(0, X(1))), Trim(GearSlot.Weapon, P.Iron[3]));
                muzzle = front + d;
                return;
            }
            Vector2 lock_ = butt + d * X(4), end = butt + d * X(rifle || musket ? 16 : 15);
            c.Line(butt, lock_, rifle ? P.Wood[1] : P.Wood[2], limb + 1);
            c.Line(lock_, end, P.Iron[1], limb);
            if (musket)
            {
                c.Line(lock_ + d * X(1), end - d * X(1), P.Wood[2]);                 // full-length stock
                c.Put(c.T(lock_ + V(0, X(1))), Trim(GearSlot.Weapon, P.Iron[3]));   // the flint cock
                c.Line(end, end + d * X(4), P.Steel[3]);                             // a long socket bayonet
            }
            else if (rifle)
            {
                c.Line(lock_ + d * X(1), end - d * X(3), P.Wood[1]);                 // the fore-stock
                c.Put(c.T(lock_ + V(0, X(1))), Trim(GearSlot.Weapon, P.Brass[2]));   // the hammer
                c.Line(end, end + d * X(3), P.Steel[3]);                             // bayonet
            }
            else
            {
                c.Put(c.T(lock_ + V(0, X(1))), Trim(GearSlot.Weapon, P.Gold[2]));   // the serpentine
                c.Put(c.T(lock_ + V(X(-0.5f), X(1.5f))), P.Fire[2]);              // its lit match
            }
            muzzle = end + d;
        }

        private void MuzzleFlash()
        {
            Vector2 m = muzzle;
            if (m.x < 0) m = hFar + V(X(5), 0);
            c.Ellipse(m + V(X(1.2f), 0), X(1.8f), X(1.3f), new[] { P.Fire[1], P.Fire[2], P.Glow[3], P.White });
            c.Put(c.T(m + V(X(3), X(1))), P.Glow[3]);
            c.Put(c.T(m + V(X(3), X(-1))), P.Glow[2]);
        }

        /// <summary>A field gun on a wooden carriage, in front of its gunner; it rolls back when fired.</summary>
        private void FieldGun()
        {
            float recoil = p.release ? -X(2.5f) : p.attack && !p.loaded ? -X(1) : 0;
            Vector2 axle = V(cx + X(12) + recoil, gy + X(4));
            // trail and carriage cheeks
            c.Line(axle + V(X(-9), X(-3.5f)), axle + V(X(2), X(1)), P.Wood[1], 3);
            c.Line(axle + V(X(-2), X(1.5f)), axle + V(X(4), X(3)), P.Wood[2], 3);
            // the barrel: bronze, thicker at the breech
            Vector2 breech = axle + V(X(-3), X(3.5f)), mouth = axle + V(X(13), X(5));
            c.Capsule(breech, mouth, X(1.9f), P.Bronze, 0.2f);
            c.Ellipse(breech + V(X(-1.4f), 0), X(1.2f), X(1.2f), P.Bronze);                     // cascabel
            c.Line(mouth + V(X(-0.5f), X(-2)), mouth + V(X(-0.5f), X(2)), P.Bronze[1], 2);      // muzzle swell
            c.Put(c.T(breech + V(X(1.5f), X(1.8f))), P.Coal[1]);                                  // touch-hole
            muzzle = mouth + V(X(1), 0);
            // the wheel
            float r = X(4.5f);
            c.FillLocal(new Rect(axle.x - r - 1, axle.y - r - 1, r * 2 + 2, r * 2 + 2), l =>
            {
                float dd = (l - axle).magnitude;
                if (dd > r) return null;
                if (dd > r - 1.5f) return P.Wood[1];
                float ang = Mathf.Atan2(l.y - axle.y, l.x - axle.x) * Mathf.Rad2Deg + (p.release ? 22 : 0);
                bool spoke = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 45f) * 45f)) < 9f;
                return spoke || dd < 1.5f ? (Color32?)P.Wood[2] : null;
            });
            if (p.attack && p.loaded) c.Put(c.T(breech + V(X(1.5f), X(2.6f))), P.Fire[3]);    // the match at the touch-hole
        }

        /// <summary>
        /// A steam crawler: an armoured iron hull on clanking treads, a gun out front and a smoking
        /// stack behind, its commander standing in the hatch.
        /// </summary>
        private void SteamCrawler()
        {
            float recoil = p.release ? -X(1.5f) : 0;
            float x0 = cx - X(11), x1 = cx + X(22), yb = gy + X(1), yt = gy + X(12);
            int phase = p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? 2 : p.bob > 0 ? 3 : 0;
            // the stack, venting steam
            Vector2 stack = V(x0 + X(4), yt);
            c.Line(stack, stack + V(0, X(7)), P.Iron[1], S > 1.3f ? 4 : 3);
            c.Line(stack + V(X(-1), X(7)), stack + V(X(1), X(7)), P.Brass[2], 2);
            c.Ellipse(stack + V(X(-1.5f) - phase, X(10)), X(2.2f), X(1.6f), P.Stone, null, 0.4f);
            // the gun
            Vector2 breech = V(x1 - X(4) + recoil, yt - X(3)), mouth = breech + V(X(13), X(1));
            c.Capsule(breech, mouth, X(1.7f), P.Iron, 0.2f);
            c.Line(mouth + V(X(-0.4f), X(-1.8f)), mouth + V(X(-0.4f), X(1.8f)), P.Brass[2], 2);
            muzzle = mouth + V(X(1), 0);
            // the hull: sloped front, riveted plates, a band in the team colour
            c.FillLocal(new Rect(x0, yb + X(3), x1 - x0, yt - yb - X(3)), l =>
            {
                if (l.x > x1 - X(4) && l.y > yt - (x1 - l.x) * 0.9f - X(0.5f) && l.x - (x1 - X(4)) > (yt - l.y) * 0.7f) return null;   // sloped glacis
                if (l.y > yt - 1.2f) return P.Iron[3];
                if (Mathf.Abs(l.y - (yb + X(7))) < X(0.8f)) return team[2];
                if (((int)(l.x - x0) % 6 == 0 && (int)l.y % 3 == 0)) return P.Brass[2];   // rivets
                return l.x < x0 + X(2) ? P.Iron[3] : ((int)(l.x - x0) / 6 % 2 == 0 ? P.Iron[2] : P.Iron[1]);
            });
            c.Put(c.T(V(x1 - X(9), yb + X(9))), P.Aether[3]);   // a vision port aglow
            // treads, the links walking along with the wheels
            var tread = new Rect(x0 - X(1), yb - X(1), x1 - x0 + X(2), X(5));
            c.FillLocal(tread, l =>
            {
                float dx = Mathf.Min(l.x - tread.xMin, tread.xMax - l.x), dy = Mathf.Abs(l.y - tread.center.y) / (tread.height / 2);
                if (dx < X(2.5f) && (X(2.5f) - dx) * (X(2.5f) - dx) / (X(2.5f) * X(2.5f)) + dy * dy > 1.1f) return null;
                bool edge = l.y < tread.yMin + 1 || l.y > tread.yMax - 1;
                if (edge) return ((int)l.x + phase) % 3 == 0 ? P.Iron[3] : P.Coal[1];
                return P.Coal[2];
            });
            for (int i = 0; i < 5; i++)
            {
                Vector2 wheel = V(Mathf.Lerp(tread.xMin + X(2.6f), tread.xMax - X(2.6f), i / 4f), tread.center.y);
                c.Ellipse(wheel, X(1.7f), X(1.7f), P.Iron);
                c.Put(c.T(wheel + V(phase % 2 == 0 ? 0.5f : -0.5f, 0.5f)), P.Brass[2]);
            }
            if (p.attack && p.loaded) c.Put(c.T(breech + V(X(1), X(2))), P.Fire[3]);
        }

        /// <summary>A tank: a riveted olive hull on tracks, a turret with a long gun, the commander in its hatch.</summary>
        private void TankHull()
        {
            float recoil = p.release ? -X(1.5f) : 0;
            float x0 = cx - X(16), x1 = cx + X(22), yb = gy + X(1), yt = gy + X(9);
            int phase = p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? 2 : p.bob > 0 ? 3 : 0;
            var hull = P.Olive;
            // tracks
            var tread = new Rect(x0 - X(1), yb - X(1), x1 - x0 + X(2), X(5));
            c.FillLocal(tread, l =>
            {
                float dx = Mathf.Min(l.x - tread.xMin, tread.xMax - l.x), dy = Mathf.Abs(l.y - tread.center.y) / (tread.height / 2);
                if (dx < X(2.5f) && (X(2.5f) - dx) * (X(2.5f) - dx) / (X(2.5f) * X(2.5f)) + dy * dy > 1.1f) return null;
                if (l.y < tread.yMin + 1 || l.y > tread.yMax - 1) return ((int)l.x + phase) % 3 == 0 ? P.Iron[3] : P.Coal[1];
                return P.Coal[2];
            });
            for (int i = 0; i < 6; i++)
            {
                Vector2 wheel = V(Mathf.Lerp(tread.xMin + X(2.6f), tread.xMax - X(2.6f), i / 5f), tread.center.y);
                c.Ellipse(wheel, X(1.6f), X(1.6f), P.Iron);
            }
            // the hull, with a sloped front
            c.FillLocal(new Rect(x0, yb + X(3), x1 - x0, yt - yb - X(3)), l =>
            {
                if (l.x > x1 - X(5) && l.x - (x1 - X(5)) > (yt - l.y) * 0.8f) return null;
                if (l.y > yt - 1.2f) return hull[3];
                return ((int)(l.x - x0) % 7 == 0 && (int)l.y % 3 == 0) ? hull[0] : l.x < x0 + X(2) ? hull[3] : hull[2];
            });
            c.Put(c.T(V(x0 + X(4), yb + X(6))), team[2]);   // a team-colour roundel
            c.Put(c.T(V(x0 + X(5), yb + X(6))), team[2]);
            // the turret around the commander's waist, and its gun
            Vector2 breech = V(cx + X(6) + recoil, yt + X(3)), mouth = breech + V(X(18), 0);
            c.Line(breech, mouth, hull[1], S > 1.3f ? 3 : 2);
            c.Line(mouth + V(X(-1), 0), mouth, hull[0], S > 1.3f ? 4 : 3);   // the muzzle brake
            muzzle = mouth + V(X(1), 0);
            c.FillLocal(new Rect(cx - X(7), yt, X(14), X(5.5f)), l =>
            {
                float dx = Mathf.Abs(l.x - cx) / X(7), dy = (l.y - yt) / X(5.5f);
                if (dy > 0.6f && dx > 1.1f - dy * 0.6f) return null;
                return l.y > yt + X(5) ? hull[3] : l.x < cx - X(4) ? hull[3] : hull[2];
            });
            if (p.attack && p.loaded) c.Put(c.T(breech), P.Fire[3]);
        }

        /// <summary>An armoured car: a boxy riveted body on big wheels, the gunner standing in the open top with his machine gun.</summary>
        private void ArmouredCar()
        {
            float x0 = cx - X(13), x1 = cx + X(15), yb = gy + X(4), yt = gy + X(11);
            int phase = p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? 2 : 0;
            var hull = P.Olive;
            c.FillLocal(new Rect(x0, yb, x1 - x0, yt - yb), l =>
            {
                if (l.x > x1 - X(5) && l.y > yb + X(4) && l.x - (x1 - X(5)) > (l.y - (yb + X(4))) * 1.2f) return null;   // sloped bonnet
                if (l.y > yt - 1.2f) return hull[3];
                return ((int)(l.x - x0) % 6 == 0 && (int)l.y % 3 == 0) ? hull[0] : l.x < x0 + X(2) ? hull[3] : hull[2];
            });
            c.Rect((int)(x1 - X(6)), (int)(yb + X(4)), (int)(x1 - X(4)), (int)(yb + X(5)), P.Coal[1]);   // the driver's slit
            c.Line(V(x0 + X(2), yb + X(3)), V(x1 - X(6), yb + X(3)), team[2]);
            foreach (float wx in new[] { x0 + X(5), x1 - X(6) })
            {
                Vector2 w = V(wx, gy + X(3));
                c.Ellipse(w, X(3.2f), X(3.2f), P.Coal);
                c.Ellipse(w, X(1.2f), X(1.2f), P.Iron);
                c.Put(c.T(w + V(phase == 1 ? X(1) : phase == 2 ? X(-1) : 0, X(0.8f))), P.Iron[3]);
            }
        }

        /// <summary>The walker's open cockpit in front of the pilot's waist, with its gauges.</summary>
        private void WalkerCockpit()
        {
            var save = (c.Angle, c.Shift);
            c.Angle = 0; c.Shift = Vector2.zero;
            var hull = P.Olive;
            var box = new Rect(hip.x - X(7), hip.y - X(5), X(15), X(9));
            c.FillLocal(box, l =>
            {
                if (l.x > box.xMax - X(3) && l.y > box.yMax - X(3) && (l.x - (box.xMax - X(3))) > (box.yMax - l.y)) return null;
                if (l.y > box.yMax - 1.2f) return Trim(GearSlot.Body, hull[3]);
                return ((int)l.x % 5 == 0 && (int)l.y % 3 == 0) ? hull[0] : l.x < box.xMin + X(2) ? hull[3] : hull[2];
            });
            c.Put(c.T(V(box.xMin + X(3), box.yMin + X(5))), P.Glow[3]);   // a lamp
            c.Line(V(box.xMin + X(2), box.yMin + X(2)), V(box.xMax - X(4), box.yMin + X(2)), team[2]);
            (c.Angle, c.Shift) = save;
        }

        /// <summary>The walker's two iron legs (back-jointed, like a bird's) and the hull behind the pilot.</summary>
        private void WalkerLegs(int drop)
        {
            var hull = P.Olive;
            Vector2 seat = V(cx + 0.5f, gy + X(22) - (drop == 0 ? 0 : drop == 1 ? X(6) : X(14)));
            int g = Gait;
            for (int side = -1; side <= 1; side += 2)
            {
                bool far = side < 0;
                float swing = drop > 0 ? 0 : g * X(3) * side;
                Vector2 hipJ = seat + V(X(far ? -2 : 2), X(-3));
                Vector2 knee = hipJ + V(X(-4) + swing * 0.3f, X(-8));
                Vector2 foot = drop == 2 ? hipJ + V(X(side * 6), X(-12)) : V(seat.x + X(far ? -3 : 3) + swing, gy + 1);
                if (drop == 2) knee = Vector2.Lerp(hipJ, foot, 0.5f) + V(0, X(2));
                var col = far ? hull[1] : hull[2];
                c.Line(hipJ, knee, col, S > 1.3f ? 4 : 3);
                c.Line(knee, foot, col, S > 1.3f ? 3 : 2);
                c.Ellipse(knee, X(1.5f), X(1.5f), P.Iron);
                c.Line(foot + V(X(-2), 0), foot + V(X(3), 0), P.Iron[1], 2);   // a splayed iron foot
            }
            // the engine pack behind the pilot, with an exhaust stack
            c.Ellipse(seat + V(X(-5), X(3)), X(4), X(5), hull);
            c.Line(seat + V(X(-7), X(6)), seat + V(X(-7), X(11)), P.Iron[1], 2);
            if (drop == 0) c.Ellipse(seat + V(X(-7.5f) - Gait, X(13)), X(1.6f), X(1.2f), P.Stone, null, 0.4f);
        }

        /// <summary>The airship's gasbag, drawn behind its crew: a long ribbed envelope, fins at the tail.</summary>
        private void AirshipBag()
        {
            var save = (c.Angle, c.Shift);
            c.Angle = 0; c.Shift = Vector2.zero;
            float sink = p.death == 2 ? X(-6) : p.death == 1 ? X(-2) : 0;
            Vector2 bag = V(cx + X(2), gy + X(31) + sink + p.bob * 0.5f);
            var cloth = s.raider ? P.Leather : P.Linen;
            c.Ellipse(bag, X(26), X(10), cloth);
            for (int i = -3; i <= 3; i++) c.Line(bag + V(i * X(6.5f), X(-9)), bag + V(i * X(6.5f), X(9)), cloth[1]);   // the ribs
            c.Line(bag + V(X(-24), 0), bag + V(X(24), 0), team[2], 2);                                                   // a band in the team colour
            c.Polygon(new[] { bag + V(X(-22), X(2)), bag + V(X(-32), X(9)), bag + V(X(-30), X(1)) }, _ => cloth[1]);   // tail fins
            c.Polygon(new[] { bag + V(X(-22), X(-2)), bag + V(X(-32), X(-9)), bag + V(X(-30), X(-1)) }, _ => cloth[1]);
            // rigging down to the gondola
            c.Line(bag + V(X(-10), X(-9)), V(cx - X(9), gy + X(9)), P.Linen[1]);
            c.Line(bag + V(X(12), X(-9)), V(cx + X(10), gy + X(9)), P.Linen[1]);
            (c.Angle, c.Shift) = save;
        }

        /// <summary>The airship's wooden gondola round the crewman's waist, a propeller turning behind it.</summary>
        private void AirshipGondola()
        {
            var box = new Rect(cx - X(10), gy + X(1), X(21), X(9));
            c.FillLocal(box, l =>
            {
                if (l.y < box.yMin + X(2) && (l.x < box.xMin + X(2) || l.x > box.xMax - X(2))) return null;   // rounded hull
                if (l.y > box.yMax - 1.2f) return Trim(GearSlot.Body, P.Brass[2]);
                return ((int)l.x % 4 == 0) ? P.Wood[1] : P.Wood[2];
            });
            Vector2 prop = V(box.xMin - X(2), box.center.y);
            bool spin = (p.fNear.x > 2 || p.fNear.x < -2 || p.bob > 0);
            c.Line(prop + V(0, spin ? X(-4) : X(-1)), prop + V(0, spin ? X(4) : X(1)), P.Wood[3], 2);
            c.Line(prop + V(X(1), 0), prop + V(X(3), 0), P.Iron[1], 2);
            muzzle = V(box.center.x, box.yMin - X(1));   // bombs drop from the bottom
        }

        /// <summary>A biplane round its pilot: fuselage, two wings, a spinning propeller.</summary>
        private void Biplane()
        {
            var body = s.raider ? P.Leather : team;
            float y = gy + X(4);
            // lower wing behind, fuselage, upper wing over the pilot's head
            c.Line(V(cx - X(6), y - X(1)), V(cx + X(10), y - X(1)), P.Linen[1], S > 1.3f ? 4 : 3);
            c.Capsule(V(cx - X(18), y + X(2)), V(cx + X(14), y + X(1)), X(3), body, 0.1f);
            c.Polygon(new[] { V(cx - X(18), y + X(2)), V(cx - X(23), y + X(7)), V(cx - X(21), y + X(1)) }, _ => body[1]);   // tail fin
            c.Line(V(cx - X(22), y + X(2)), V(cx - X(14), y + X(2)), body[1], 2);                                             // tailplane
            c.Line(V(cx - X(4), y + X(14)), V(cx + X(12), y + X(14)), P.Linen[2], S > 1.3f ? 4 : 3);
            c.Line(V(cx - X(2), y), V(cx - X(2), y + X(14)), P.Wood[1]); c.Line(V(cx + X(9), y), V(cx + X(9), y + X(14)), P.Wood[1]);   // struts
            c.Put(c.T(V(cx + X(3), y + X(14))), team[2]);
            // the engine cowling and propeller
            c.Ellipse(V(cx + X(15), y + X(1)), X(2.5f), X(2.8f), P.Iron);
            bool spin = !(p.death > 0);
            c.Line(V(cx + X(18), y + (spin ? X(-5) : X(-2))), V(cx + X(18), y + (spin ? X(6) : X(2))), P.Wood[3], 2);
            if (p.death >= 2) c.Ellipse(V(cx + X(14), y + X(5)), X(2.5f), X(2), P.Stone, null, 0.4f);   // trailing smoke as it goes down
            muzzle = V(cx + X(17), y + X(4));
        }

        /// <summary>A tunnelling machine: an iron body on tracks with a great spiral drill for a nose.</summary>
        private void DrillMachine()
        {
            float x0 = cx - X(12), x1 = cx + X(12), yb = gy + X(1), yt = gy + X(10);
            int phase = p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? 2 : p.bob > 0 ? 3 : (p.attack ? 1 : 0);
            var hull = s.raider ? P.Rock : P.Iron;
            c.FillLocal(new Rect(x0, yb + X(2), x1 - x0, yt - yb - X(2)), l =>
                l.y > yt - 1.2f ? hull[3] : ((int)(l.x - x0) % 6 == 0 && (int)l.y % 3 == 0) ? P.Brass[2] : l.x < x0 + X(2) ? hull[3] : hull[2]);
            c.Line(V(x0 + X(2), yb + X(6)), V(x1 - X(2), yb + X(6)), team[2]);
            // the drill: a cone with a turning spiral
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f, r = X(5) * (1 - t);
                float x = x1 + X(14) * t;
                c.Line(V(x, yb + X(5) - r), V(x, yb + X(5) + r), (i + phase) % 3 == 0 ? P.Steel[3] : P.Steel[1]);
            }
            for (int i = 0; i < 4; i++) c.Ellipse(V(Mathf.Lerp(x0 + X(2), x1 - X(2), i / 3f), yb + X(1)), X(1.8f), X(1.8f), P.Coal);
            c.Line(V(x0 + X(3), yt), V(x0 + X(3), yt + X(5)), P.Iron[1], 2);   // exhaust
        }

        /// <summary>A hover craft: a rounded chrome hull with fins, floating over a glowing pad (a tank adds a ray cannon).</summary>
        private void HoverHull(bool tank)
        {
            float x0 = cx - X(tank ? 16 : 12), x1 = cx + X(tank ? 20 : 14), yb = gy + X(1), yt = gy + X(tank ? 9 : 8);
            var hull = s.raider ? P.Iron : P.Chrome;
            // the glow underneath, flickering
            bool flick = p.fNear.x > 2 || p.bob > 0;
            c.Ellipse(V((x0 + x1) / 2, groundY + X(1.5f)), (x1 - x0) / 2.2f, X(1.5f), new[] { P.Ray[0], P.Ray[1], flick ? P.Ray[2] : P.Ray[1], P.Ray[3] });
            c.FillLocal(new Rect(x0, yb, x1 - x0, yt - yb), l =>
            {
                float dx = Mathf.Min(l.x - x0, x1 - l.x);
                if (dx < X(3) && l.y < yb + (X(3) - dx)) return null;   // rounded underside
                if (l.y > yt - 1.2f) return hull[3];
                return l.y < yb + X(2) ? hull[1] : l.x < x0 + X(3) ? hull[3] : hull[2];
            });
            c.Line(V(x0 + X(2), yb + X(4)), V(x1 - X(3), yb + X(4)), team[2], 2);   // a racing stripe in the team colour
            c.Polygon(new[] { V(x0, yt), V(x0 - X(4), yt + X(5)), V(x0 + X(4), yt) }, _ => P.Red[2]);   // a tail fin
            if (tank)
            {
                Vector2 breech = V(cx + X(4), yt + X(2)), mouth = breech + V(X(18), 0);
                c.Line(breech, mouth, hull[1], S > 1.3f ? 3 : 2);
                c.Ellipse(mouth, X(1.6f), X(1.6f), P.Ray);
                muzzle = mouth + V(X(1), 0);
                c.FillLocal(new Rect(cx - X(6), yt, X(12), X(5)), l =>
                {
                    float dx2 = Mathf.Abs(l.x - cx) / X(6), dy = (l.y - yt) / X(5);
                    return dx2 * dx2 + dy * dy > 1f ? (Color32?)null : l.x < cx - X(3) ? hull[3] : hull[2];
                });
            }
        }

        /// <summary>
        /// A star-steel craft: a mana jetbike (small) or a gunship (big) with swept wings, a crystal behind the
        /// hatch and a cannon in its nose. Raiders' craft are void-black with magenta drives.
        /// </summary>
        private void StarHull(bool big)
        {
            float x0 = cx - X(big ? 20 : 11), x1 = cx + X(big ? 26 : 15), yb = gy + X(big ? -1 : 1), yt = gy + X(big ? 8 : 7);
            var hull = s.raider ? P.Void : P.Astral;
            var drive = s.raider ? new[] { P.Void[1], P.Void[2], P.Void[3], P.White } : new[] { P.Mana[1], P.Mana[2], P.Mana[3], P.White };
            bool flick = p.fNear.x > 2 || p.bob > 0 || p.attack;
            float h = yt - yb, noseFrom = x1 - X(big ? 10 : 7);
            if (big)   // swept wings under the hull
                c.Polygon(new[] { V(cx - X(4), yb + X(2)), V(cx + X(10), yb + X(2)), V(cx - X(14), yb - X(6)) }, _ => hull[1]);
            c.FillLocal(new Rect(x0, yb, x1 - x0, h), l =>
            {
                float n = Mathf.Clamp01((l.x - noseFrom) / (x1 - noseFrom));   // the nose tapers to a point
                if (l.y > yt - n * h * 0.75f || l.y < yb + n * h * 0.2f) return null;
                if (l.x < x0 + X(1.5f) && l.y < yb + X(1)) return null;
                if (l.y > yt - n * h * 0.75f - 1.2f) return hull[3];
                return l.y < yb + X(2) ? hull[1] : hull[2];
            });
            c.Line(V(x0 + X(2), yb + X(3)), V(noseFrom + X(2), yb + X(3)), s.raider ? P.Void[3] : P.Gold[2]);
            c.Line(V(x0 + X(3), yb + X(5)), V(x0 + X(8), yb + X(5)), team[2]);
            // the drive at the stern, its flame flickering
            c.FillLocal(new Rect(x0 - X(2), yb + X(1.5f), X(2.5f), h - X(3)), _ => hull[1]);
            c.Ellipse(V(x0 - X(3.5f), (yb + yt) / 2), X(flick ? 3.2f : 2.2f), X(1.5f), drive);
            c.Polygon(new[] { V(x0 + X(1), yt), V(x0 - X(2), yt + X(big ? 7 : 5)), V(x0 + X(6), yt) }, _ => hull[2]);   // a tail fin
            c.Put(c.T(V(x0 - X(2), yt + X(big ? 7 : 5))), s.raider ? P.Void[3] : P.Gold[3]);
            if (big)
            {
                // a mana crystal behind the hatch, and a cannon in the nose
                Vector2 cr = V(cx - X(9), yt + X(2.5f));
                c.Polygon(new[] { cr + V(0, X(3)), cr + V(X(1.6f), 0), cr + V(0, X(-1.5f)), cr + V(X(-1.6f), 0) }, _ => drive[2]);
                Vector2 breech = V(noseFrom - X(2), yb + X(1)), mouth = breech + V(X(15), 0);
                c.Line(breech, mouth, hull[0], S > 1.3f ? 3 : 2);
                c.Ellipse(mouth, X(1.2f), X(1.2f), drive);
                muzzle = mouth + V(X(1), 0);
            }
            else
            {
                c.Ellipse(V(cx + X(2), yb - X(0.5f)), X(5), X(0.9f), drive);   // the lift glow under the bike
            }
        }

        /// <summary>
        /// A throne on a hovering disc of star-steel (void-black for raiders): mana crystals circle it, a nova
        /// cannon is mounted at its side with a little sun at the mouth, a glow underneath.
        /// </summary>
        private void Throne()
        {
            var hull = s.raider ? P.Void : P.Astral;
            var glow = s.raider ? new[] { P.Void[1], P.Void[2], P.Void[3], P.White } : new[] { P.Mana[1], P.Mana[2], P.Mana[3], P.White };
            bool flick = p.fNear.x > 2 || p.bob > 0 || p.attack;
            float dy = gy + X(3);
            // the lift glow on the ground, and the disc
            c.Ellipse(V(cx + 0.5f, groundY + X(1)), X(flick ? 13 : 12), X(1.4f), glow);
            c.Ellipse(V(cx + 0.5f, dy), X(16), X(3.6f), hull);
            c.Line(V(cx - X(15), dy), V(cx + X(16), dy), s.raider ? P.Void[3] : P.Gold[2]);
            for (int i = -2; i <= 2; i++) c.Put(c.T(V(cx + i * X(6), dy - X(2))), glow[2]);
            // the throne's front: a gold-trimmed panel that hides the pilot's legs
            c.FillLocal(new Rect(cx - X(7), dy + X(1), X(14), X(6)), l => l.y > dy + X(6) ? (Color32?)(s.raider ? P.Void[3] : P.Gold[2]) : l.x < cx - X(4) ? hull[3] : hull[2]);
            c.Put(c.T(V(cx, dy + X(4))), glow[3]);
            // the nova cannon on its arm, a little sun at the mouth
            Vector2 breech = V(cx + X(6), dy + X(7)), mouth = breech + V(X(12) - (p.release ? X(1.5f) : 0), X(1));
            c.Line(breech, mouth, hull[1], S > 1.3f ? 3 : 2);
            c.Line(breech + V(0, -X(1)), breech + V(X(4), -X(1)), s.raider ? P.Void[3] : P.Gold[2]);
            c.Ellipse(mouth + V(X(1), 0), X(1.8f), X(1.8f), s.raider ? glow : new[] { P.Gold[1], P.Gold[2], P.Gold[3], P.White });
            muzzle = mouth + V(X(2), 0);
            // three crystals orbiting the throne
            for (int i = 0; i < 3; i++)
            {
                float t = (p.fNear.x + p.bob * 2 + (p.attack ? 3 : 0)) * 0.6f + i * 2.1f;   // turns with the pose
                Vector2 o = V(cx + Mathf.Cos(t) * X(18), dy + X(9) + Mathf.Sin(t) * X(3));
                c.Polygon(new[] { o + V(0, X(2)), o + V(X(1.2f), 0), o + V(0, -X(2)), o + V(-X(1.2f), 0) }, _ => glow[2]);
                c.Put(c.T(o + V(0, X(0.6f))), glow[3]);
            }
        }

        /// <summary>A rocket pack on the flyer's back: two chrome tubes with flames out of the bottom.</summary>
        private void Jetpack()
        {
            var save = (c.Angle, c.Shift);
            c.Angle = 0; c.Shift = Vector2.zero;
            for (int i = 0; i < 2; i++)
            {
                Vector2 top = sh + V(X(-2.6f - i * 1.4f), X(-1)), bottom = top + V(0, X(-7));
                c.Capsule(top, bottom, X(1.1f), P.Chrome, 0.2f);
                if (p.death < 2)
                {
                    int flick = (p.bob > 0 ? 1 : 0) + (p.fNear.x > 2 ? 1 : 0) + i;
                    c.Line(bottom + V(0, -X(1)), bottom + V(0, -X(3 + flick % 2)), P.Fire[2], 2);
                    c.Put(c.T(bottom + V(0, -X(4 + flick % 2))), P.Glow[3]);
                }
            }
            c.Put(c.T(sh + V(X(-3), X(0.5f))), P.Red[3]);
            (c.Angle, c.Shift) = save;
        }

        /// <summary>A short iron gun on the bastion's parapet, in front of its gunner.</summary>
        private void BastionGun()
        {
            float recoil = p.release ? -X(2) : 0;
            Vector2 breech = sh + V(X(2) + recoil, X(-4)), mouth = breech + V(X(11), X(0.5f));
            c.Capsule(breech, mouth, X(1.7f), P.Iron, 0.2f);
            c.Ellipse(breech + V(X(-1.2f), 0), X(1.1f), X(1.1f), P.Iron);
            c.Line(mouth + V(X(-0.4f), X(-1.8f)), mouth + V(X(-0.4f), X(1.8f)), P.Iron[1], 2);
            muzzle = mouth + V(X(1), 0);
        }

        private void Crossbow()
        {
            // A crossbow held level at the shoulder: stock along the forearm, the bow across the front.
            Vector2 back = hFar + V(X(-1.5f), 0), front = hNear + V(X(4), p.attack && p.hNear.y > -2 ? 0 : X(-1));
            if (!p.attack || p.hNear.y < -2) front = hNear + V(X(4), X(2));   // lowered: pointing up a little
            Vector2 d = (front - back).normalized, n = V(-d.y, d.x);
            c.Line(back, front, P.Wood[2], limb + 1);
            c.Line(back, back + d * X(2), P.Wood[1], limb + 1);
            Vector2 bowAt = front - d * X(1);
            float flex = p.loaded ? X(-1.2f) : X(0.6f);
            Vector2 tipA = bowAt + n * X(4) + d * flex, tipB = bowAt - n * X(4) + d * flex;
            c.Line(bowAt, tipA, Trim(GearSlot.Weapon, P.Steel[1]), 2);
            c.Line(bowAt, tipB, Trim(GearSlot.Weapon, P.Steel[1]), 2);
            Vector2 nut = p.loaded ? Vector2.Lerp(back, front, 0.45f) : bowAt;
            c.Line(tipA, nut, P.Linen[3]);
            c.Line(tipB, nut, P.Linen[3]);
            if (p.loaded) { c.Line(nut, front + d * X(0.8f), P.Wood[3]); c.Put(c.T(front + d * X(1.3f)), P.Steel[3]); }
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
                case GearLook.KiteShield:
                {
                    // a heater-shaped kite shield: flat top, coming to a point below, with a bend in the crest colour
                    float hw = X(3.2f), top = at.y + X(4.5f), bottom = at.y - X(6);
                    c.FillLocal(new Rect(at.x - hw - 1, bottom - 1, hw * 2 + 2, top - bottom + 2), l =>
                    {
                        if (l.y > top) return null;
                        float t = Mathf.Clamp01((at.y - l.y) / (at.y - bottom));
                        float half = l.y > at.y ? hw : hw * Mathf.Sqrt(1 - t * t);
                        if (Mathf.Abs(l.x - at.x) > half) return null;
                        if (Mathf.Abs(l.x - at.x) > half - 1 || l.y > top - 1) return rim;
                        bool bend = Mathf.Abs((l.x - at.x) + (l.y - at.y) * 0.7f) < X(1.1f);
                        return bend ? crest[2] : l.x < at.x ? ramp[1] : ramp[2];
                    });
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
            MountKind.Walker => V(cx + 0.5f, gy + X(22)),
            MountKind.Drake => V(cx + 0.5f - X(3), gy + X(21)),
            _ => V(cx + 0.5f - X(1), gy + X(15)),
        };

        private float ThrownOffset() => s.mount switch
        {
            MountKind.Elephant => X(22),
            MountKind.Drake => X(22),
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
                case MountKind.Walker: WalkerLegs(drop); break;
                case MountKind.Horse: Horse(V(cx + 0.5f, gy), coat, drop, 0); break;
                case MountKind.Chariot:
                    Horse(V(cx + 0.5f + X(16), gy), coat, drop, 0);
                    ChariotBack(drop);
                    break;
                case MountKind.Elephant: Elephant(V(cx + 0.5f, gy), coat, drop); break;
                case MountKind.Griffin: Griffin(V(cx + 0.5f, gy), P.Named(s.coat == "Wyvern" ? "Wyvern" : "Griffin"), drop); break;
                case MountKind.Drake: Drake(V(cx + 0.5f, gy), drop); break;
            }
            (c.Angle, c.Shift, c.Pivot) = save;
        }

        private int Gait => p.death > 0 ? 0 : p.fNear.x > 2 ? 1 : p.fNear.x < -2 ? -1 : 0;

        /// <summary>A griffin: a lion's body, an eagle's head and wings beating with the stride. A raider's wyvern is scaled, with a lizard's head.</summary>
        private void Griffin(Vector2 o, Color32[] coat, int drop)
        {
            float lift = drop == 0 ? 0 : drop == 1 ? -X(4) : -X(8);
            Vector2 body = o + V(0, X(11) + lift);
            bool wyvern = s.coat == "Wyvern";
            var feathers = wyvern ? coat : P.Linen;
            // wings beat with the stride, held high at rest
            float flap = drop > 0 ? -0.5f : p.fNear.x > 2 ? 1.1f : p.fNear.x < -2 ? -0.2f : 0.7f;
            void Wing(float dx, bool far)
            {
                if (drop == 2) return;
                Vector2 root = body + V(X(dx), X(4)), tip = root + V(X(-15), X(12 * flap + 6)), back = root + V(X(-13), X(-1));
                Color32[] ramp = wyvern ? coat : (far ? P.Griffin : feathers);
                c.Polygon(new[] { root, tip, back }, _ => far ? ramp[1] : ramp[2]);
                c.Line(root, tip, ramp[far ? 0 : 1], 2);
                for (int i = 1; i < 5; i++) c.Line(Vector2.Lerp(root, tip, i / 5f), Vector2.Lerp(back, tip, i / 5f + 0.1f), ramp[far ? 0 : 1]);
            }
            Wing(-2, true);
            // tail: a lion's tuft, or a wyvern's long spade
            Vector2 tailEnd = body + V(X(wyvern ? -19 : -16), X(wyvern ? 1 : 5));
            c.Line(body + V(X(-9), X(1)), tailEnd, coat[1], 2);
            if (wyvern) c.Polygon(new[] { tailEnd + V(0, X(1.6f)), tailEnd + V(X(-2.8f), 0), tailEnd + V(0, X(-1.6f)) }, _ => coat[2]);
            else c.Ellipse(tailEnd, X(1.6f), X(1.6f), new[] { coat[0], coat[0], coat[1], coat[1] });
            // tucked legs: talons in front, paws behind
            if (drop < 2)
            {
                c.Line(body + V(X(6), X(-4)), body + V(X(9), X(-7)), wyvern ? coat[1] : P.Gold[2], 2);
                c.Line(body + V(X(-7), X(-4)), body + V(X(-11), X(-6)), coat[1], 2);
            }
            c.Ellipse(body + V(X(-7), X(0.3f)), X(5.5f), X(5), coat);
            c.Ellipse(body, X(10), X(5.2f), coat);
            c.Ellipse(body + V(X(7), X(1)), X(4.6f), X(5.2f), wyvern ? coat : feathers);   // a feathered breast
            // neck and head
            Vector2 neck0 = body + V(X(8), X(3)), head = drop == 2 ? body + V(X(14), X(-1)) : body + V(X(12), X(8.5f));
            c.Capsule(neck0, head, X(2.8f), wyvern ? coat : feathers);
            c.Ellipse(head, X(3.4f), X(3), wyvern ? coat : feathers);
            if (wyvern)
            {
                c.Polygon(new[] { head + V(X(1.5f), X(1.6f)), head + V(X(7), X(0)), head + V(X(1.5f), X(-1.8f)) }, _ => coat[2]);   // long jaw
                c.Line(head + V(X(-1), X(2.4f)), head + V(X(-3.5f), X(4.5f)), coat[1], 2);   // horn
                c.Put(head + V(X(1), X(1)), P.Red[3]);
            }
            else
            {
                c.Polygon(new[] { head + V(X(2.4f), X(1.6f)), head + V(X(6.4f), X(0.2f)), head + V(X(5), X(-2.2f)), head + V(X(2.4f), X(-1.2f)) }, _ => P.Gold[2]);   // hooked beak
                c.Line(head + V(X(-1.5f), X(2.4f)), head + V(X(-3.5f), X(4.2f)), feathers[1], 2);   // ear tuft
                c.Put(head + V(X(1.2f), X(1)), P.Ink);
            }
            Wing(1, false);
            // the saddle cloth in the team colour
            if (drop < 2) c.FillLocal(new Rect(body.x - X(5), body.y + X(2), X(8), X(3.5f)), l => l.x < body.x - X(3) ? team[2] : team[1]);
        }

        /// <summary>
        /// A heavy war drake: four clawed legs, a long spiked tail, a horned head that breathes fire when it
        /// attacks, and moonstone barding with a mana line along it.
        /// </summary>
        private void Drake(Vector2 o, int drop)
        {
            var coat = P.Drake;
            float lift = drop == 0 ? 0 : drop == 1 ? -X(5) : -X(10);
            Vector2 body = o + V(0, X(13) + lift);
            int g = Gait;
            void Leg(float dx, bool far, int phase)
            {
                if (drop == 2) return;
                float swing = g * X(2.5f) * phase;
                Vector2 top = body + V(X(dx), X(-2)), knee = top + V(X(-1) + swing * 0.5f, X(-6)), foot = V(top.x + swing, gy + 1);
                var col = far ? coat[1] : coat[2];
                c.Line(top, knee, col, S > 1.3f ? 6 : 5);
                c.Line(knee, foot, col, S > 1.3f ? 5 : 4);
                c.Line(foot + V(X(-1), 0), foot + V(X(3), 0), P.Bone[2], 2);   // claws
            }
            Leg(-8, true, 1); Leg(7, true, -1);
            // the tail, sweeping back and down, spikes along it
            Vector2 t0 = body + V(X(-10), X(1)), t1 = body + V(X(-18), X(-3)), t2 = body + V(X(-26), X(-6) + (drop == 2 ? X(4) : 0));
            c.Capsule(t0, t1, X(2.6f), coat); c.Capsule(t1, t2, X(1.6f), coat);
            c.Polygon(new[] { t2 + V(0, X(1.8f)), t2 + V(X(-3.5f), 0), t2 + V(0, X(-1.8f)) }, _ => coat[1]);
            // the body
            c.Ellipse(body + V(X(-6), 0), X(7), X(6.5f), coat);
            c.Ellipse(body, X(12), X(7), coat);
            c.Ellipse(body + V(X(7), X(1)), X(6), X(6.5f), coat);
            c.Line(body + V(X(-9), X(-4)), body + V(X(9), X(-3.5f)), coat[3]);   // a pale belly
            for (int i = 0; i < 5; i++) c.Put(c.T(body + V(X(-14 + i * 3), X(4.8f - Mathf.Abs(i - 1) * 0.6f))), coat[0]);   // tail-root spikes
            // neck and horned head
            bool breathe = p.attack && (p.release || !p.loaded);
            Vector2 neck0 = body + V(X(9), X(3)), head = drop == 2 ? body + V(X(17), X(-2)) : body + V(X(15), X(10));
            c.Capsule(neck0, head, X(3.8f), coat);
            c.Ellipse(head, X(4.4f), X(3.6f), coat);
            float gape = breathe && drop == 0 ? X(1.6f) : 0;
            c.Polygon(new[] { head + V(X(1.5f), X(1.4f)), head + V(X(7), X(0.6f)), head + V(X(6), X(-0.4f)), head + V(X(1.5f), X(-0.4f)) }, _ => coat[2]);   // snout
            c.Polygon(new[] { head + V(X(1.5f), X(-0.6f)), head + V(X(6), X(-1.2f) - gape), head + V(X(1.5f), X(-2.2f)) }, _ => coat[1]);   // jaw
            c.Line(head + V(X(-1), X(2.2f)), head + V(X(-4.5f), X(5)), P.Bone[2], 2);   // horns
            c.Line(head + V(X(0.5f), X(2.6f)), head + V(X(-2.5f), X(5.6f)), P.Bone[3], 2);
            c.Put(c.T(head + V(X(1.6f), X(1.2f))), P.Glow[3]);   // eye
            if (breathe && drop == 0)
            {
                Vector2 m = head + V(X(7), X(-0.4f));
                c.Ellipse(m + V(X(2.5f), 0), X(3), X(1.8f), P.Fire);
                c.Ellipse(m + V(X(1.2f), 0), X(1.4f), X(1), new[] { P.Fire[2], P.Fire[3], P.White, P.White });
                muzzle = m;
            }
            if (drop < 2)
            {
                // folded leathery wings along the flank: too heavy to fly
                Vector2 w0 = body + V(X(4), X(5)), w1 = body + V(X(-12), X(9)), w2 = body + V(X(-10), X(2));
                c.Polygon(new[] { w0, w1, w2 }, _ => coat[1]);
                c.Line(w0, w1, coat[0], 2);
                c.Line(w0, Vector2.Lerp(w1, w2, 0.5f), coat[0]);
                // moonstone barding over the back with a mana line, and the saddle cloth in the team colour
                c.FillLocal(new Rect(body.x - X(9), body.y + X(1), X(18), X(5.5f)), l =>
                {
                    float dx = (l.x - body.x) / X(9.5f), dy = (l.y - body.y) / X(6.2f);
                    if (dx * dx + dy * dy > 1f) return null;
                    return l.y > body.y + X(4.6f) ? Trim(GearSlot.Body, P.Gold[2]) : l.x < body.x - X(6) ? P.Moonstone[3] : P.Moonstone[2];
                });
                c.Line(body + V(X(-8), X(2.5f)), body + V(X(8), X(2.5f)), P.Mana[2]);
                c.FillLocal(new Rect(body.x - X(5), body.y + X(4), X(7), X(3)), l => l.x < body.x - X(3) ? team[2] : team[1]);
                Leg(-6, false, -1); Leg(9, false, 1);
            }
        }

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
            if (s.barding && drop < 2)
            {
                // a caparison in the team colour down to the knees, and a steel chanfron on the face
                c.FillLocal(new Rect(body.x - X(12), body.y - X(6), X(22), X(10.5f)), l =>
                {
                    float dx = (l.x - body.x) / X(10), dy = (l.y - (body.y + X(0.5f))) / X(5);
                    if (dx * dx + dy * dy > 1.25f || l.y > body.y + X(4.4f)) return null;
                    if (l.y < body.y - X(5)) return Trim(GearSlot.Body, P.Gold[2]);   // fringe
                    var cloth = string.IsNullOrEmpty(s.cape) ? team : P.Named(s.cape);   // a cloaked rider's horse matches the cloak
                    return ((int)(l.x - body.x) / 4) % 2 == 0 ? cloth[2] : cloth[1];
                });
                Vector2 face = body + V(X(13.5f), X(8));
                c.Line(face + V(X(-2), X(1)), face + V(X(1.5f), X(-1)), P.Steel[2], 2);
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

    /// <summary>Pilotless machines (drones, jets): drawn whole, with their shadow on the ground below.</summary>
    private static class MachineRig
    {
        public static void Draw(PixelCanvas c, FigureStyle s, bool attack, bool release, int frame, int cx, int alt, int groundY)
        {
            float S = s.scale;
            float X(float v) => v * S;
            var team = P.Named(s.team);
            var body = s.raider ? P.Tactical : P.Synth;
            int death = frame >= 9 ? frame - 8 : 0;
            float drop = death * X(6);
            Vector2 o = new Vector2(cx + 0.5f, alt - drop);
            if (s.mount == MountKind.Drone)
            {
                // a quad-rotor: a body, four arms with spinning rotors, a camera and a gun underneath
                c.Ellipse(o + new Vector2(0, X(2)), X(5), X(3), body);
                c.Line(o + new Vector2(X(-2), X(2)), o + new Vector2(X(2), X(2)), team[2], 2);
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 arm = o + new Vector2(i * X(9), X(4));
                    c.Line(o + new Vector2(i * X(3), X(3)), arm, body[1], 2);
                    bool blur = death == 0 && (frame + (i > 0 ? 1 : 0)) % 2 == 0;
                    c.Line(arm + new Vector2(blur ? X(-4) : X(-2), X(1)), arm + new Vector2(blur ? X(4) : X(2), X(1)), blur ? new Color32(220, 230, 240, 150) : P.Tactical[2], 1);
                }
                c.Ellipse(o + new Vector2(X(3), X(0.5f)), X(1.4f), X(1.4f), P.Neon);   // the camera eye
                c.Line(o + new Vector2(X(1), X(-1)), o + new Vector2(X(6), X(-1)), P.Tactical[2], 2);
                if (release) { c.Put(o + new Vector2(X(7), X(-1)), P.Glow[3]); c.Put(o + new Vector2(X(8), X(-1)), P.White); }
                if (death >= 2) c.Ellipse(o + new Vector2(0, X(6)), X(2), X(1.6f), P.Stone, null, 0.4f);
                c.Outline(P.Ink, 0.6f);
                c.Shadow(cx + X(1), groundY, X(6), X(1.4f));
                return;
            }
            // a jet: a swept delta wing, a canopy with the pilot's helmet, an engine glow at the tail
            var hull = s.raider ? P.Tactical : P.Chrome;
            c.Capsule(o + new Vector2(X(-18), X(3)), o + new Vector2(X(16), X(3)), X(3), hull, 0.1f);
            c.Polygon(new[] { o + new Vector2(X(-8), X(2)), o + new Vector2(X(6), X(2)), o + new Vector2(X(-12), X(-6)) }, _ => hull[1]);   // wing
            c.Polygon(new[] { o + new Vector2(X(-14), X(4)), o + new Vector2(X(-20), X(11)), o + new Vector2(X(-17), X(4)) }, _ => team[2]);   // tail fin
            c.Ellipse(o + new Vector2(X(6), X(5.5f)), X(4), X(2), new[] { P.Neon[0], P.Neon[1], P.Neon[2], P.Neon[3] });   // canopy
            c.Ellipse(o + new Vector2(X(5), X(5.5f)), X(1.2f), X(1.2f), P.Tactical);
            c.Line(o + new Vector2(X(-12), X(1)), o + new Vector2(X(2), X(1)), team[2], 2);
            bool burn = death == 0;
            if (burn) { c.Line(o + new Vector2(X(-22), X(3)), o + new Vector2(X(-19), X(3)), frame % 2 == 0 ? P.Fire[3] : P.Fire[2], 2); }
            if (release) { c.Put(o + new Vector2(X(19), X(2)), P.Glow[3]); c.Put(o + new Vector2(X(20), X(2)), P.White); }
            if (death >= 2) c.Ellipse(o + new Vector2(X(-14), X(7)), X(3), X(2), P.Stone, null, 0.4f);
            c.Outline(P.Ink, 0.6f);
            c.Shadow(cx + X(1), groundY, X(14), X(1.6f));
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
