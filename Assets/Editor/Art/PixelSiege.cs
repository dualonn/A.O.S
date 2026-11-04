using UnityEngine;
using P = PixelPalette;
using Kind = WarCatalog.SiegeKind;

/// <summary>
/// Siege engines in pixel art, side view facing right, 32 pixels a tile like everything else:
/// rams, onagers, trebuchets, bombards, steam siege tanks, howitzers, missile carriers, rune
/// colossi, siege mechs, orbital beacons and reality breakers. Twelve frames each, in the same
/// order as the troops' (idle, 4 walk, 4 attack with the strike on the third, 3 death).
/// Raiders' engines carry red, yours blue.
/// </summary>
public static class PixelSiege
{
    public const int FrameCount = 12;

    public static void CanvasFor(Kind kind, out int w, out int h, out int cx, out int gy)
    {
        gy = 4;
        switch (kind)
        {
            case Kind.Ram: w = 62; h = 40; break;
            case Kind.Onager: w = 58; h = 48; break;
            case Kind.Trebuchet: w = 66; h = 76; break;
            case Kind.Bombard: w = 64; h = 38; break;
            case Kind.SteamTank: w = 66; h = 50; break;
            case Kind.Howitzer: w = 66; h = 46; break;
            case Kind.Launcher: w = 70; h = 50; break;
            case Kind.Colossus: w = 60; h = 70; break;
            case Kind.Mech: w = 64; h = 70; break;
            case Kind.Beacon: w = 50; h = 68; break;
            default: w = 58; h = 68; break;
        }
        cx = w / 2;
    }

    public static Vector2 PivotFor(Kind kind)
    {
        CanvasFor(kind, out int w, out int h, out int cx, out int gy);
        return new Vector2((cx + 0.5f) / w, gy / (float)h);
    }

    /// <summary>One frame: 0 idle, 1-4 moving, 5-8 attacking (the blow lands on 7), 9-11 wrecked.</summary>
    public static PixelCanvas Draw(Kind kind, bool raider, int frame)
    {
        if (frame >= 9) return Wreck(Draw(kind, raider, 0), kind, frame - 9);
        CanvasFor(kind, out int w, out int h, out int cx, out int gy);
        var c = new PixelCanvas(w, h);
        int walk = frame >= 1 && frame <= 4 ? frame - 1 : -1;
        int atk = frame >= 5 && frame <= 8 ? frame - 5 : -1;
        var team = raider ? P.Red : P.Blue;
        switch (kind)
        {
            case Kind.Ram: Ram(c, team, raider, walk, atk); break;
            case Kind.Onager: Onager(c, team, raider, walk, atk); break;
            case Kind.Trebuchet: Trebuchet(c, team, raider, walk, atk); break;
            case Kind.Bombard: Bombard(c, team, raider, walk, atk); break;
            case Kind.SteamTank: SteamTank(c, team, raider, walk, atk); break;
            case Kind.Howitzer: Howitzer(c, team, raider, walk, atk); break;
            case Kind.Launcher: Launcher(c, team, raider, walk, atk); break;
            case Kind.Colossus: Colossus(c, team, raider, walk, atk); break;
            case Kind.Mech: Mech(c, team, raider, walk, atk); break;
            case Kind.Beacon: Beacon(c, team, raider, walk, atk); break;
            default: Breaker(c, team, raider, walk, atk); break;
        }
        c.Angle = 0f;
        c.Shift = Vector2.zero;
        c.Outline(P.Ink, 0.6f);
        c.Shadow(cx, gy - 0.5f, w * 0.42f, 2.2f);
        return c;
    }

    // ---------- Shared parts ----------

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    private static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color32[] r)
    {
        c.Rect(x0, y0, x1, y1, r[2]);
        c.Rect(x0, y1, x1, y1, r[3]);
        c.Rect(x0, y0, x0, y1, r[3]);
        c.Rect(x0, y0, x1, y0, r[1]);
        c.Rect(x1, y0, x1, y1, r[1]);
    }

    private static void Planks(PixelCanvas c, int x0, int y0, int x1, int y1, Color32[] wood)
    {
        Box(c, x0, y0, x1, y1, wood);
        for (int x = x0 + 5; x < x1; x += 6) c.Rect(x, y0 + 1, x, y1 - 1, wood[1]);
    }

    private static void Wheel(PixelCanvas c, float x, float y, float r, int walk, Color32[] wood, Color32[] rim)
    {
        c.Ellipse(V(x, y), r, r, rim);
        c.Ellipse(V(x, y), r - 1.3f, r - 1.3f, wood);
        float turn = (walk < 0 ? 0 : walk) * 22.5f;
        for (int k = 0; k < 4; k++)
        {
            float a = (k * 45f + turn) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 1.2f);
            c.LineRaw(V(x, y) - d, V(x, y) + d, wood[0]);
        }
        c.Put(x, y, rim[3]);
    }

    private static void Pennant(PixelCanvas c, int x, int y, int h, Color32[] team, int phase)
    {
        for (int i = 0; i < h; i++) c.Put(x, y + i, P.Wood[i % 3 == 0 ? 1 : 2]);
        int wave = phase % 2;
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 4 - i / 2; j++)
                c.Put(x + 1 + i, y + h - 1 - j - (i > 2 ? wave : 0), j == 0 ? team[3] : team[2]);
    }

    private static void Flash(PixelCanvas c, float x, float y, float size, Color32[] ramp = null)
    {
        ramp ??= P.Fire;
        c.Ellipse(V(x, y), size, size * 0.8f, new[] { ramp[1], ramp[2], ramp[3], P.White });
        c.Put(x + size + 1, y, ramp[3]);
        c.Put(x + size + 2, y, ramp[2]);
    }

    private static void Puffs(PixelCanvas c, float x, float y, int phase, int n = 3)
    {
        var smoke = new[] { P.Stone[1], P.Stone[2], P.Stone[3], P.Stone[4] };
        for (int i = 0; i < n; i++)
        {
            float r = 1.6f + i * 0.7f;
            c.Ellipse(V(x - i * 1.5f + (phase % 2), y + i * 3.2f + phase * 0.6f), r, r * 0.85f, smoke);
        }
    }

    private static void Treads(PixelCanvas c, int x0, int x1, int y0, int y1, int walk, Color32[] metal)
    {
        var hull = new[] { V(x0 + 3, y0), V(x1 - 3, y0), V(x1, y0 + 3), V(x1, y1 - 2), V(x1 - 2, y1), V(x0 + 2, y1), V(x0, y1 - 2), V(x0, y0 + 3) };
        c.Polygon(hull, P.Coal[1]);
        int shift = walk < 0 ? 0 : walk;
        for (int x = x0 + 1 + shift % 3; x < x1; x += 3) { c.Put(x, y1, P.Coal[3]); c.Put(x + 1, y0, P.Coal[3]); }
        int n = Mathf.Max(2, (x1 - x0) / 8);
        for (int i = 0; i < n; i++)
        {
            float wx = x0 + 4 + i * ((x1 - x0 - 8) / (float)(n - 1));
            c.Ellipse(V(wx, (y0 + y1) * 0.5f), 2.4f, 2.4f, metal);
            c.Put(wx, (y0 + y1) * 0.5f, P.Coal[0]);
        }
    }

    private static void Rivets(PixelCanvas c, int x0, int x1, int y, Color32 col)
    {
        for (int x = x0; x <= x1; x += 4) c.Put(x, y, col);
    }

    // ---------- Bronze Age: covered ram ----------

    private static void Ram(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var wood = raider ? P.Horse : P.Wood;
        int bob = walk >= 0 && walk % 2 == 1 ? 1 : 0;
        foreach (float x in new[] { 14f, 30f, 46f }) Wheel(c, x, 7, 4.5f, walk, wood, P.Wood);
        Planks(c, 6, 9, 54, 12, wood);
        // the ram log, slung inside, battering out at the front
        int push = atk switch { 0 => -3, 1 => -5, 2 => 5, 3 => 2, _ => 0 };
        c.Capsule(V(6 + push, 17), V(54 + push, 17), 2.3f, wood);
        c.Ellipse(V(57 + push, 17), 3.4f, 3.1f, raider ? P.Iron : P.Bronze);
        c.Put(59 + push, 18, (raider ? P.Iron : P.Bronze)[3]);
        // a roof of hides on an A-frame
        var roof = new[] { V(4, 13 + bob), V(56, 13 + bob), V(46, 31 + bob), V(14, 31 + bob) };
        c.Polygon(roof, l =>
        {
            int row = Mathf.FloorToInt(l.y);
            if (row == 21 + bob || row == 22 + bob) return team[(Mathf.FloorToInt(l.x) / 3) % 2 == 0 ? 2 : 1];
            if ((Mathf.FloorToInt(l.x) + row * 3) % 11 == 0) return P.Leather[1];
            return l.y > 27 + bob ? P.Leather[3] : row % 4 == 0 ? P.Leather[1] : P.Leather[2];
        });
        c.LineRaw(V(14, 31 + bob), V(46, 31 + bob), P.Wood[3]);
        c.LineRaw(V(20, 29 + bob), V(20, 19), P.Iron[1]);
        c.LineRaw(V(40, 29 + bob), V(40, 19), P.Iron[1]);
        if (raider) { c.Ellipse(V(30, 26 + bob), 2.2f, 2f, P.Bone); c.Put(29, 26 + bob, P.Ink); c.Put(31, 26 + bob, P.Ink); }
        else Pennant(c, 44, 31 + bob, 7, team, walk + 1);
        if (atk == 2) { c.Put(62, 15, P.Bone[3]); c.Put(61, 20, P.Bone[2]); c.Put(62, 18, P.Bone[3]); }
    }

    // ---------- Classical: onager ----------

    private static void Onager(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var wood = raider ? P.Horse : P.Wood;
        Wheel(c, 14, 6, 4f, walk, wood, P.Iron);
        Wheel(c, 42, 6, 4f, walk, wood, P.Iron);
        Planks(c, 6, 8, 50, 12, wood);
        // the crossbar the arm strikes
        c.Line(V(36, 12), V(38, 30), wood[1], 2);
        c.Line(V(44, 12), V(40, 30), wood[1], 2);
        c.Rect(34, 29, 44, 31, wood[2]);
        c.Rect(36, 31, 42, 32, P.Linen[2]);
        c.Rect(8, 12, 30, 15, team[1]);
        c.Rect(8, 15, 30, 15, team[2]);
        // the arm, wound back on its twisted ropes
        float angle = atk switch { 0 => 172f, 1 => 128f, 2 => 84f, 3 => 96f, _ => 168f };
        var pivot = V(22, 15);
        c.Pivot = pivot;
        c.Angle = angle;
        c.Line(pivot, pivot + V(23, 0), wood[2], 3);
        c.Line(pivot + V(2, 1), pivot + V(22, 1), wood[3], 1);
        c.Ellipse(pivot + V(24, 0), 2.6f, 2.2f, P.Leather);
        if (atk < 2) c.Ellipse(pivot + V(24, 1.5f), 2.2f, 2.2f, P.Stone);
        c.Angle = 0f;
        c.Ellipse(pivot, 3.4f, 3.4f, P.Linen);
        c.Put(pivot.x, pivot.y, P.Leather[0]);
        Pennant(c, 9, 15, 10, team, (walk + 4) % 4);
    }

    // ---------- Medieval: trebuchet ----------

    private static void Trebuchet(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var wood = raider ? P.Horse : P.Wood;
        foreach (float x in new[] { 12f, 33f, 54f }) Wheel(c, x, 7, 4.2f, walk, wood, P.Iron);
        Planks(c, 4, 9, 62, 13, wood);
        // A-frame
        c.Line(V(14, 13), V(31, 54), wood[1], 3);
        c.Line(V(52, 13), V(35, 54), wood[1], 3);
        c.Line(V(22, 32), V(44, 32), wood[2], 2);
        var pivot = V(33, 54);
        // the long arm and its sling, the counterweight at the short end
        float angle = atk switch { 0 => 214f, 1 => 140f, 2 => 62f, 3 => 78f, _ => 212f };
        float rad = angle * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        var tip = pivot + dir * 34f;
        var tail = pivot - dir * 11f;
        c.Line(pivot, tip, wood[2], 3);
        c.Line(pivot, tail, wood[2], 3);
        c.Rect(Mathf.RoundToInt(tail.x) - 5, Mathf.RoundToInt(tail.y) - 9, Mathf.RoundToInt(tail.x) + 5, Mathf.RoundToInt(tail.y), P.Iron[1]);
        c.Rect(Mathf.RoundToInt(tail.x) - 4, Mathf.RoundToInt(tail.y) - 8, Mathf.RoundToInt(tail.x) + 4, Mathf.RoundToInt(tail.y) - 1, wood[1]);
        c.Rect(Mathf.RoundToInt(tail.x) - 3, Mathf.RoundToInt(tail.y) - 4, Mathf.RoundToInt(tail.x) + 3, Mathf.RoundToInt(tail.y) - 2, P.Stone[2]);
        if (atk < 2)
        {
            var bag = tip + (atk == 1 ? dir * 4f : V(0, -5));
            c.LineRaw(tip, bag, P.Linen[1]);
            c.Ellipse(bag, 2.6f, 2.6f, P.Stone);
        }
        else c.LineRaw(tip, tip + dir * 5f, P.Linen[1]);
        c.Ellipse(pivot, 2.2f, 2.2f, P.Iron);
        Pennant(c, 33, 56, 10, team, walk + 1);
    }

    // ---------- Renaissance: great bombard ----------

    private static void Bombard(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var wood = raider ? P.Horse : P.Wood;
        int recoil = atk == 2 ? -3 : atk == 3 ? -2 : 0;
        Wheel(c, 15, 6, 4.2f, walk, wood, P.Iron);
        Wheel(c, 39, 6, 4.2f, walk, wood, P.Iron);
        Planks(c, 8, 8, 46, 12, wood);
        c.Polygon(new[] { V(10, 12), V(22, 12), V(18, 20), V(12, 18) }, wood[1]);   // the wedge that sets the elevation
        c.Rect(8, 12, 18, 13, team[1]);
        var metal = raider ? P.Iron : P.Bronze;
        var a = V(12 + recoil, 17);
        var b = V(50 + recoil, 21);
        c.Capsule(a, b, 5.2f, metal);
        c.Ellipse(a - V(1, 0), 4f, 4.6f, metal);   // the powder chamber
        for (int i = 0; i < 4; i++)
        {
            var p = Vector2.Lerp(a, b, 0.22f + i * 0.2f);
            c.LineRaw(p + V(0.6f, -5f), p + V(-0.6f, 5f), metal[0]);
            c.LineRaw(p + V(1.6f, -5f), p + V(0.4f, 5f), metal[3]);
        }
        c.Ellipse(b + V(2, 0), 2.6f, 6.2f, metal);
        c.Ellipse(b + V(3, 0), 1.4f, 4f, P.Coal);
        b += V(2, 0);
        if (atk == 2) Flash(c, b.x + 5, b.y, 3.6f);
        if (atk == 3) Puffs(c, b.x + 3, b.y + 2, 1);
        if (!raider) Pennant(c, 8, 14, 12, team, walk + 1);
    }

    // ---------- Industrial: steam siege tank ----------

    private static void SteamTank(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var hullRamp = raider ? P.Iron : P.Brass;
        Treads(c, 4, 60, 3, 13, walk, P.Iron);
        int bob = walk >= 0 && walk % 2 == 1 ? 1 : 0;
        var hull = new[] { V(6, 13 + bob), V(56, 13 + bob), V(62, 20 + bob), V(56, 29 + bob), V(10, 29 + bob), V(6, 24 + bob) };
        c.Polygon(hull, l => l.y > 27 + bob ? hullRamp[3] : l.x > 55 ? hullRamp[1] : ((int)l.x % 8 == 0 ? hullRamp[1] : hullRamp[2]));
        Rivets(c, 9, 55, 15 + bob, hullRamp[3]);
        Rivets(c, 11, 55, 27 + bob, hullRamp[1]);
        c.Rect(8, 20 + bob, 54, 21 + bob, team[2]);
        c.Rect(8, 22 + bob, 54, 22 + bob, team[1]);
        // smokestack and steam
        Box(c, 14, 29 + bob, 18, 39 + bob, P.Iron);
        c.Rect(13, 39 + bob, 19, 40 + bob, P.Iron[3]);
        Puffs(c, 16, 43 + bob, walk < 0 ? (atk < 0 ? 0 : atk) : walk, 2);
        // turret and gun
        int recoil = atk == 2 ? -3 : atk == 3 ? -1 : 0;
        Box(c, 30, 29 + bob, 46, 37 + bob, P.Iron);
        c.Rect(32, 37 + bob, 44, 38 + bob, P.Iron[2]);
        c.Line(V(46 + recoil, 33 + bob), V(63 + recoil, 34 + bob), P.Steel[1], 3);
        c.LineRaw(V(46 + recoil, 34 + bob), V(62 + recoil, 35 + bob), P.Steel[3]);
        if (atk == 2) Flash(c, 64, 34 + bob, 2.6f);
        if (atk == 3) Puffs(c, 62, 36 + bob, 1, 2);
        c.Ellipse(V(38, 33 + bob), 1.6f, 1.6f, team);
    }

    // ---------- Modern: siege howitzer ----------

    private static void Howitzer(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var drab = raider ? P.Khaki : P.Olive;
        // trail legs to the back
        c.Line(V(26, 9), V(3, 3), drab[1], 3);
        c.Line(V(26, 11), V(6, 5), drab[2], 1);
        c.Rect(1, 2, 5, 4, drab[1]);
        int recoil = atk == 2 ? -4 : atk == 3 ? -2 : 0;
        float rad = 22f * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        var breech = V(26, 20) + dir * recoil;
        var muzzle = breech + dir * 36f;
        c.Capsule(V(24, 17), V(24, 17) + dir * 16f, 3f, drab);   // recoil cylinder
        c.Line(breech, muzzle, P.Tactical[2], 3);
        c.LineRaw(breech + V(0, 1), muzzle + V(0, 1), drab[3]);
        c.Rect(Mathf.RoundToInt(muzzle.x) - 1, Mathf.RoundToInt(muzzle.y) - 2, Mathf.RoundToInt(muzzle.x) + 1, Mathf.RoundToInt(muzzle.y) + 2, P.Tactical[1]);
        // the shield and wheel
        c.Polygon(new[] { V(28, 10), V(36, 10), V(37, 30), V(28, 27) }, l => l.x < 30 ? drab[3] : drab[2]);
        c.Rect(30, 18, 34, 21, team[2]);
        Wheel(c, 26, 9, 7f, walk, P.Tactical, P.Coal);
        c.Ellipse(V(26, 9), 2.2f, 2.2f, drab);
        if (atk == 2) Flash(c, muzzle.x + 3, muzzle.y + 1, 3.4f);
        if (atk == 3) Puffs(c, muzzle.x + 1, muzzle.y + 2, 1, 2);
    }

    // ---------- Information Age: missile carrier ----------

    private static void Launcher(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var drab = raider ? P.Tactical : P.Khaki;
        foreach (float x in new[] { 13f, 27f, 56f }) Wheel(c, x, 6, 4.6f, walk, P.Coal, P.Coal);
        Box(c, 4, 9, 64, 13, drab);
        // cab
        Box(c, 50, 13, 65, 28, drab);
        c.Rect(56, 20, 63, 26, P.Chrome[1]);
        c.Rect(57, 24, 62, 25, P.Chrome[3]);
        c.Rect(50, 15, 64, 16, team[2]);
        // the launch rack, raised
        float lift = walk >= 0 ? 12f : 30f;
        c.Pivot = V(14, 15);
        c.Angle = lift;
        c.Polygon(new[] { V(8, 14), V(46, 14), V(46, 24), V(8, 24) }, l => l.y > 22 ? drab[3] : l.y < 16 ? drab[1] : drab[2]);
        for (int i = 0; i < 4; i++)
        {
            bool gone = atk >= 2 && i == 0 || atk == 3 && i == 1;
            var col = gone ? P.Coal : P.Iron;
            c.Ellipse(V(46, 16 + i * 2.4f), 1.2f, 1.2f, col);
            if (!gone) c.Put(47, 16 + i * 2.4f, P.Fire[2]);
        }
        var exhaust = c.T(V(8, 19));
        var nose = c.T(V(48, 17));
        c.Angle = 0f;
        Box(c, 10, 13, 18, 16, P.Iron);
        if (atk == 2) { Flash(c, nose.x + 3, nose.y + 2, 2.8f); Puffs(c, exhaust.x - 2, exhaust.y - 2, 0, 3); }
        if (atk == 3) Puffs(c, exhaust.x - 3, exhaust.y, 1, 2);
    }

    // ---------- Arcane: rune colossus ----------

    private static void Colossus(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var stone = raider ? P.Rock : P.Stone;
        var glow = raider ? P.Fire : P.Rune;
        int step = walk < 0 ? 0 : walk;
        int frontLift = step == 1 ? 3 : 0, backLift = step == 3 ? 3 : 0;
        int frontX = step == 1 ? 3 : step == 3 ? -2 : 0, backX = -frontX;
        // legs
        Box(c, 18 + backX, 4 + backLift, 27 + backX, 24, stone);
        Box(c, 31 + frontX, 4 + frontLift, 40 + frontX, 24, stone);
        c.Rect(17 + backX, 4 + backLift, 28 + backX, 6 + backLift, stone[1]);
        c.Rect(30 + frontX, 4 + frontLift, 41 + frontX, 6 + frontLift, stone[1]);
        // body
        c.Polygon(new[] { V(12, 24), V(46, 24), V(50, 50), V(8, 50) }, l => (int)l.y % 6 == 0 ? stone[1] : l.x < 20 ? stone[3] : stone[2]);
        c.LineRaw(V(16, 44), V(26, 34), glow[2]);
        c.LineRaw(V(26, 34), V(36, 44), glow[2]);
        c.Ellipse(V(29, 39), 3.6f, 3.6f, raider ? P.Fire : P.Arcane);
        c.Put(29, 40, P.White);
        // a team-coloured sash hung with a rune
        c.Line(V(10, 48), V(44, 26), team[2], 2);
        c.Put(27, 37, team[3]);
        // head
        Box(c, 23, 50, 37, 60, stone);
        c.Rect(31, 55, 36, 56, glow[3]);
        // back arm and the striking arm
        c.Capsule(V(12, 46), V(6, 26), 3.4f, stone);
        var shoulder = V(44, 46);
        var fist = atk switch { 0 => V(46, 64), 1 => V(52, 62), 2 => V(55, 10), 3 => V(52, 20), _ => V(50, 24 + (step % 2)) };
        c.Capsule(shoulder, fist, 3.8f, stone);
        c.Ellipse(fist, 5f, 4.6f, stone);
        c.Put(fist.x + 1, fist.y + 1, glow[3]);
        if (atk == 2) for (int i = 0; i < 5; i++) c.Put(48 + i * 2, 5 + (i % 2), P.Dirt[3]);
    }

    // ---------- Stellar: siege mech ----------

    private static void Mech(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var metal = raider ? P.Coal : P.Starmetal;
        var energy = raider ? P.Fire : P.Solar;
        int step = walk < 0 ? 0 : walk;
        Vector2 Leg(float hipX, int phase, Color32[] ramp)
        {
            float lift = phase == 1 ? 4f : 0f, swing = phase == 1 ? 4f : phase == 3 ? -3f : 0f;
            var hip = V(hipX, 30);
            var knee = V(hipX + 7 + swing * 0.5f, 18 + lift * 0.5f);
            var foot = V(hipX + swing, 5 + lift);
            c.Capsule(hip, knee, 3f, ramp);
            c.Capsule(knee, foot, 2.4f, ramp);
            c.Rect(Mathf.RoundToInt(foot.x) - 4, Mathf.RoundToInt(foot.y) - 1, Mathf.RoundToInt(foot.x) + 5, Mathf.RoundToInt(foot.y) + 1, ramp[1]);
            return knee;
        }
        var dark = new[] { metal[0], metal[1], metal[2], metal[3] };
        Leg(22, (step + 2) % 4, dark);
        Leg(32, step, metal);
        // body and canopy
        c.Polygon(new[] { V(12, 30), V(46, 30), V(50, 40), V(44, 50), V(16, 50), V(10, 42) }, l => l.y > 47 ? metal[3] : l.x < 18 ? metal[1] : metal[2]);
        c.Polygon(new[] { V(40, 38), V(49, 41), V(44, 48), V(38, 47) }, l => l.y > 45 ? energy[3] : energy[2]);
        c.Rect(14, 38, 38, 39, team[2]);
        c.Rect(14, 40, 38, 40, team[1]);
        c.LineRaw(V(18, 50), V(18, 58), metal[2]);
        c.Put(18, 59, energy[3]);
        // shoulder cannon
        int recoil = atk == 2 ? -3 : atk == 3 ? -1 : 0;
        Box(c, 22 + recoil, 50, 38 + recoil, 56, metal);
        c.Line(V(38 + recoil, 53), V(60 + recoil, 55), metal[1], 3);
        for (int x = 42; x < 58; x += 4) c.Put(x + recoil, 55, energy[2]);
        if (atk == 1) c.Ellipse(V(60, 55), 1.6f, 1.6f, energy);
        if (atk == 2) Flash(c, 62, 55, 3.2f, energy);
    }

    // ---------- Galactic: orbital beacon ----------

    private static void Beacon(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var metal = raider ? P.Coal : P.Starmetal;
        var light = raider ? P.Fire : P.Rift;
        int step = walk < 0 ? 0 : walk;
        int s1 = step == 1 ? 2 : step == 3 ? -2 : 0;
        var hub = V(25, 24 + (step % 2));
        c.Line(hub, V(6 + s1, 4), metal[1], 2);
        c.Line(hub, V(44 - s1, 4), metal[2], 2);
        c.Line(hub, V(27, 3), metal[0], 2);
        c.Ellipse(hub, 5f, 3f, metal);
        c.Ellipse(hub, 6.5f, 1.5f, team, l => l.y < hub.y);
        c.Line(hub, hub + V(0, 26), metal[2], 3);
        c.LineRaw(hub + V(-1, 2), hub + V(-1, 25), metal[3]);
        var dish = hub + V(0, 28);
        c.Ellipse(dish, 9f, 3.4f, raider ? P.Iron : P.Riftstone);
        c.Ellipse(dish + V(0, 1), 3f, 2f, light);
        float pulse = atk >= 0 ? 1f : (step % 2) * 0.5f;
        c.Put(dish.x, dish.y + 3, light[3]);
        if (atk == 1 || atk == 2)
        {
            for (int y = (int)dish.y + 3; y < c.H; y++) { c.Put(dish.x, y, light[3]); c.Put(dish.x - 1, y, light[2]); if (atk == 2) c.Put(dish.x + 1, y, light[2]); }
            c.Ellipse(dish + V(0, 4), 4f + pulse, 1.6f, light);
        }
        // a red targeting dot shone forward
        c.LineRaw(dish + V(8, -1), dish + V(14, -6), new Color32(255, 70, 60, 170));
    }

    // ---------- Universal: reality breaker ----------

    private static void Breaker(PixelCanvas c, Color32[] team, bool raider, int walk, int atk)
    {
        var body = raider ? P.Entropy : P.Pearl;
        var core = raider ? P.Entropy : P.Prism;
        int phase = walk >= 0 ? walk : atk >= 0 ? atk : 0;
        float hover = 14f + (phase % 2);
        var mid = V(29, 24 + hover);
        // the rings, turning
        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f + phase * 0.025f) * Mathf.PI * 2f;
            var p = mid + new Vector2(Mathf.Cos(a) * 20f, Mathf.Sin(a) * 5f);
            if (Mathf.Sin(a) < 0) c.Put(p.x, p.y, body[1]);
        }
        var shell = new[] { V(29, mid.y - 18), V(39, mid.y), V(29, mid.y + 20), V(19, mid.y) };
        c.Polygon(shell, l =>
        {
            if (l.x < 29 && l.y > mid.y) return body[3];
            if (l.x >= 29 && l.y > mid.y) return body[2];
            if (l.x < 29) return body[2];
            return body[1];
        });
        c.LineRaw(shell[0], shell[2], body[0]);
        c.Ellipse(mid, 3.6f, 3.6f, raider ? new[] { core[0], core[1], core[3], P.White } : new[] { core[0], core[1], core[2], P.White });
        c.Ellipse(mid + V(0, -1), 1.6f, 1.6f, team);
        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f + phase * 0.025f) * Mathf.PI * 2f;
            var p = mid + new Vector2(Mathf.Cos(a) * 20f, Mathf.Sin(a) * 5f);
            if (Mathf.Sin(a) >= 0) { c.Put(p.x, p.y, body[3]); c.Put(p.x + 1, p.y, core[i % 4]); }
        }
        if (atk == 1) c.Ellipse(mid + V(8, 0), 2f, 2f, core);
        if (atk == 2) { c.LineRaw(mid, V(c.W - 1, mid.y), core[3], 2); Flash(c, c.W - 5, mid.y, 3f, new[] { core[0], core[1], core[2], P.White }); }
    }

    // ---------- Wrecks ----------

    /// <summary>Death frames from the idle one: it lurches, keels over burning, and settles as a smoking wreck.</summary>
    private static PixelCanvas Wreck(PixelCanvas src, Kind kind, int stage)
    {
        CanvasFor(kind, out int w, out int h, out int cx, out int gy);
        var d = new PixelCanvas(w, h);
        float ang = stage == 0 ? -9f : stage == 1 ? -20f : 0f;
        float squash = stage == 2 ? 0.45f : 1f;
        float rad = ang * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        var pivot = V(cx + w * 0.3f, gy);
        float dark = stage == 0 ? 0.12f : stage == 1 ? 0.3f : 0.45f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 q = V(x + 0.5f, y + 0.5f) - pivot;
                Vector2 s = stage < 2 ? pivot + V(q.x * cos + q.y * sin, -q.x * sin + q.y * cos) : V(x + 0.5f, gy + (y + 0.5f - gy) / squash);
                var col = src.Get(Mathf.FloorToInt(s.x), Mathf.FloorToInt(s.y));
                if (col.a < 200) continue;   // (leave the old shadow behind)
                d.Put(x, y, PixelCanvas.Mix(col, P.Coal[0], dark));
            }
        if (stage >= 1)
        {
            var fire = P.Fire;
            for (int i = 0; i < 4; i++) d.Ellipse(V(cx - 10 + i * 7, gy + 6 + (i % 2) * 3), 1.6f, 2.4f, new[] { fire[0], fire[1], fire[2], fire[3] });
        }
        Puffs(d, cx + (stage - 1) * 4, gy + 10 + stage * 3, stage, stage + 2);
        if (stage == 2)
            for (int i = 0; i < 9; i++) d.Put(cx - 18 + i * 5, gy + (i % 3 == 0 ? 1 : 0), i % 2 == 0 ? P.Wood[1] : P.Iron[2]);
        d.Shadow(cx, gy - 0.5f, w * 0.42f, 2.2f);
        return d;
    }
}
