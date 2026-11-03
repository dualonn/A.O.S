using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Walls and gates in pixel art. Same pieces as before (see FortArt): front runs, a run back to the
/// tile behind, and diagonal runs from this tile's middle to the next one's. Every texture repeats
/// on an 8-pixel grid (a tile is 32), and open ends get no edge line, so neighbouring pieces join
/// into one seamless wall. Edges are drawn by hand rather than by the automatic outline for that reason.
/// </summary>
public static partial class PlaceholderArt
{
    private class PxWallStyle
    {
        public Color32[] ramp;
        public int height;        // face height in pixels
        public int kind;          // 0 palisade, 1 mud brick, 2 stone, 3 ashlar, 4 castle, 5 iron plate
        public bool crenels, towers;
    }

    private const int PxFoot = 10;     // front foot of the wall, up from the tile's bottom
    private const int PxHalf = 6;      // half the wall's thickness
    private const int PxWalk = 4;      // depth of the wall-walk seen on top

    private static PxWallStyle PxWallFor(int age) => age switch
    {
        0 => new PxWallStyle { ramp = P.Wood, height = 25, kind = 0 },
        1 => new PxWallStyle { ramp = P.Mud, height = 22, kind = 1 },
        2 => new PxWallStyle { ramp = P.Stone, height = 29, kind = 2, crenels = true, towers = true },
        3 => new PxWallStyle { ramp = P.Plaster, height = 32, kind = 3, crenels = true, towers = true },
        4 => new PxWallStyle { ramp = P.Stone, height = 36, kind = 4, crenels = true, towers = true },   // castle curtain wall
        5 => new PxWallStyle { ramp = BastionRamp, height = 26, kind = 1, crenels = false, towers = true }, // low, thick brick bastion wall
        _ => new PxWallStyle { ramp = P.Iron, height = 30, kind = 5, crenels = false, towers = true },     // riveted iron-plated wall
    };

    /// <summary>Red brick for Renaissance bastion walls.</summary>
    private static Color32[] BastionRamp => new[] { P.Hex("4a1e18"), P.Hex("7a3424"), P.Hex("a8503a"), P.Hex("cf7556") };

    private static Color32 Edge(Color32[] r) => PixelCanvas.Mix(r[0], P.Ink, 0.5f);

    /// <summary>The colour of one face pixel; (u, v) are along the wall (world-aligned) and up from its foot.</summary>
    private static Color32 FacePixel(PxWallStyle s, int u, int v)
    {
        var r = s.ramp;
        switch (s.kind)
        {
            case 0:   // palisade logs, 4 pixels each
            {
                int i = ((u % 4) + 4) % 4;
                if (v == s.height * 3 / 5) return P.Leather[1];   // lashing
                return i == 0 ? r[3] : i == 3 ? r[1] : r[2];
            }
            case 1:   // mud bricks: 3-pixel courses, 8 long, staggered
            case 2:   // stone blocks: 4-pixel courses
            case 3:   // ashlar: 5-pixel courses
            case 4:   // castle: big 6-pixel courses
            {
                int course = s.kind == 1 ? 3 : s.kind == 2 ? 4 : s.kind == 3 ? 5 : 6;
                int row = v / course, rv = v % course;
                int bu = (((u + (row % 2) * 4) % 8) + 8) % 8;
                if (s.kind >= 3 && v < 2) return r[1];       // plinth
                if (rv == 0 || bu == 0) return s.kind >= 3 ? r[1] : r[0];
                int tone = ((u + (row % 2) * 4) / 8 * 5 + row * 3) % 4;
                if (tone < 0) tone += 4;
                Color32 face = tone == 0 ? r[3] : r[2];
                return rv == course - 1 ? PixelCanvas.Mix(face, P.White, 0.12f) : face;
            }
            case 5:   // riveted iron plates, 8 wide and 6 high, a brass band near the top
            {
                int pu = ((u % 8) + 8) % 8, pv = v % 6;
                if (v >= s.height - 3 && v < s.height - 1) return P.Brass[pv % 2 == 0 ? 1 : 2];
                if (v < 2) return r[0];
                if (pu == 0 || pv == 0) return r[0];
                if ((pu == 2 || pu == 6) && (pv == 1 || pv == 5)) return P.Brass[2];   // rivets
                return pu < 3 ? r[3] : r[2];
            }
        }
        return r[2];
    }

    /// <summary>A straight stretch of wall seen from the front, from x0 to x1 (pixels, x1 exclusive).</summary>
    private static void PxWallRun(PixelCanvas c, int ox, int oy, PxWallStyle s, int x0, int x1, bool closedL, bool closedR)
    {
        var r = s.ramp;
        int foot = oy + PxFoot, top = foot + s.height;
        // ground shadow band
        for (int x = x0; x < x1; x++) { c.Put(ox + x, foot - 1, new Color32(20, 14, 24, 60)); c.Put(ox + x, foot - 2, new Color32(20, 14, 24, 35)); }
        for (int x = x0; x < x1; x++)
            for (int v = 0; v < s.height; v++)
                c.Put(ox + x, foot + v, FacePixel(s, x, v));
        for (int x = x0; x < x1; x++) c.Put(ox + x, foot, Edge(r));
        if (s.kind == 0)
        {
            // sharpened stake tips, one per log
            for (int x = x0; x < x1; x++)
            {
                int i = ((x % 4) + 4) % 4;
                int tip = i == 1 || i == 2 ? 3 : i == 0 ? 1 : 1;
                if ((closedL && x == x0) || (closedR && x == x1 - 1)) tip = 0;
                for (int t = 0; t < tip; t++) c.Put(ox + x, top + t, t == tip - 1 ? Edge(r) : r[i == 1 ? 3 : 2]);
                if (tip > 0) c.Put(ox + x, top + tip, Edge(r));
                else c.Put(ox + x, top, Edge(r));
            }
        }
        else
        {
            // the wall-walk on top, then crenels on its far edge
            for (int x = x0; x < x1; x++)
            {
                c.Put(ox + x, top, Edge(r));
                for (int d = 1; d <= PxWalk; d++) c.Put(ox + x, top + d, d == PxWalk ? r[2] : r[3]);
                c.Put(ox + x, top + PxWalk + 1, Edge(r));
                if (s.crenels && ((x % 8) + 8) % 8 is >= 2 and <= 5)
                {
                    for (int d = 2; d <= 4; d++) c.Put(ox + x, top + PxWalk + d, d == 4 ? r[3] : r[2]);
                    c.Put(ox + x, top + PxWalk + 5, Edge(r));
                }
                else if (s.crenels && (((x % 8) + 8) % 8 == 1 || ((x % 8) + 8) % 8 == 6))
                {
                    for (int d = 2; d <= 4; d++) c.Put(ox + x, top + PxWalk + d, Edge(r));
                }
            }
        }
        int walkTop = s.kind == 0 ? top + 3 : top + PxWalk + 1;
        if (closedL) for (int y = foot; y <= walkTop; y++) c.Put(ox + x0 - 1, y, Edge(r));
        if (closedR) for (int y = foot; y <= walkTop; y++) c.Put(ox + x1, y, Edge(r));
        // light from the left: a darker face just inside a closed right end
        if (closedR) for (int v = 1; v < s.height; v++) c.Put(ox + x1 - 1, foot + v, PixelCanvas.Mix(c.Get(ox + x1 - 1, foot + v), P.Ink, 0.2f));
    }

    /// <summary>The run going back to the tile behind: only its top shows, exactly one tile long.</summary>
    private static void PxWallBack(PixelCanvas c, int ox, int oy, PxWallStyle s, bool gate = false, bool open = false)
    {
        var r = s.ramp;
        int x0 = 16 - PxHalf, x1 = 16 + PxHalf;
        int bottom = oy + PxFoot + s.height + (s.kind == 0 ? 0 : PxWalk + 1), topY = bottom + 32;
        for (int y = bottom; y < topY; y++)
        {
            int v = y - oy;   // world-aligned so a column of these joins up
            for (int x = x0; x < x1; x++)
            {
                Color32 col;
                if (s.kind == 0) col = (((v % 4) + 4) % 4) switch { 0 => r[1], 1 => r[3], _ => r[2] };
                else col = x == 16 && v % 2 == 0 ? r[2] : r[3];
                c.Put(ox + x, y, col);
            }
            c.Put(ox + x0 - 1, y, Edge(r));
            c.Put(ox + x1, y, Edge(r));
            if (s.crenels && (((v % 8) + 8) % 8) is >= 2 and <= 5) { c.Put(ox + x1 + 1, y, r[2]); c.Put(ox + x1 + 2, y, Edge(r)); }
        }
        if (gate)
        {
            // a gate seen edge-on in the run: planks across, or a gap when open
            int g0 = bottom + 4, g1 = bottom + 20;
            for (int y = g0; y < g1; y++)
                for (int x = x0 - 1; x <= x1; x++)
                    c.Put(ox + x, y, open ? P.Grass[1] : (y % 3 == 0 ? P.Wood[1] : P.Wood[2]));
        }
    }

    /// <summary>A wall dropping diagonally to the tile in front: from (16, foot) to (16 + 32*dir, foot - 32).</summary>
    private static void PxDiagonal(PixelCanvas c, int ox, int oy, PxWallStyle s, int dir)
    {
        var r = s.ramp;
        for (int i = 0; i <= 32; i++)
        {
            int x = 16 + i * dir;
            int foot = oy + PxFoot - i, top = foot + s.height;
            c.Put(ox + x, foot - 1, new Color32(20, 14, 24, 60));
            for (int v = 0; v < s.height; v++) c.Put(ox + x, foot + v, FacePixel(s, x, v));
            c.Put(ox + x, foot, Edge(r));
            if (s.kind == 0)
            {
                int k = ((x % 4) + 4) % 4;
                int tip = k == 1 || k == 2 ? 3 : 1;
                for (int t = 0; t < tip; t++) c.Put(ox + x, top + t, r[k == 1 ? 3 : 2]);
                c.Put(ox + x, top + tip, Edge(r));
            }
            else
            {
                c.Put(ox + x, top, Edge(r));
                for (int d = 1; d <= PxWalk; d++) c.Put(ox + x, top + d, d == PxWalk ? r[2] : r[3]);
                c.Put(ox + x, top + PxWalk + 1, Edge(r));
                if (s.crenels && ((x % 8) + 8) % 8 is >= 2 and <= 5)
                {
                    for (int d = 2; d <= 4; d++) c.Put(ox + x, top + PxWalk + d, d == 4 ? r[3] : r[2]);
                    c.Put(ox + x, top + PxWalk + 5, Edge(r));
                }
            }
        }
    }

    private static void PxWallPart(PixelCanvas c, int ox, int oy, PxWallStyle s, int part)
    {
        switch (part)
        {
            case 0: PxWallRun(c, ox, oy, s, 16 - PxHalf, 16 + PxHalf, true, true); break;
            case 1: PxWallRun(c, ox, oy, s, 16 - PxHalf, 32, true, false); break;
            case 2: PxWallRun(c, ox, oy, s, 0, 16 + PxHalf, false, true); break;
            case 3: PxWallRun(c, ox, oy, s, 0, 32, false, false); break;
            case 4: PxWallBack(c, ox, oy, s); break;
            case 5: PxDiagonal(c, ox, oy, s, 1); break;
            case 6: PxDiagonal(c, ox, oy, s, -1); break;
            case 7: PxWallRun(c, ox, oy, s, 0, 16, false, false); break;
            case 8: PxWallRun(c, ox, oy, s, 16, 32, false, false); break;
        }
    }

    private static void PxGatePiece(PixelCanvas c, PxWallStyle s, bool eastWest, bool open)
    {
        var r = s.ramp;
        int postW = s.towers ? 8 : 5;
        int postH = s.height + (s.towers ? 14 : 7);
        int foot = PxFoot;
        if (!eastWest)
        {
            PxWallBack(c, 0, 0, s, gate: true, open: open);
            PxWallRun(c, 0, 0, s, 16 - PxHalf - 1, 16 + PxHalf + 1, true, true);
            return;
        }
        PxWallRun(c, 0, 0, s, 0, postW, false, true);
        PxWallRun(c, 0, 0, s, 32 - postW, 32, true, false);
        int d0 = postW, d1 = 32 - postW, top = foot + s.height - 3;
        // doors (or the open way through, doors folded back)
        for (int y = foot; y < top; y++)
            for (int x = d0; x < d1; x++)
            {
                Color32 col;
                if (open) col = (x < d0 + 2 || x >= d1 - 2) ? P.Wood[2] : y < foot + 3 ? P.Dirt[2] : P.Coal[1];
                else col = (x - d0) % 4 == 0 ? P.Wood[1] : (y == foot + 4 || y == top - 4) ? (s.kind >= 2 ? P.Iron[2] : P.Wood[0]) : P.Wood[2];
                c.Put(x, y, col);
            }
        // lintel / walkway over the gate
        for (int x = d0 - 1; x <= d1; x++) { c.Put(x, top, Edge(r)); c.Put(x, top + 1, r[3]); c.Put(x, top + 2, r[2]); c.Put(x, top + 3, Edge(r)); }
        // posts or towers in front
        foreach (int x0 in new[] { 0, 32 - postW })
        {
            for (int x = x0; x < x0 + postW; x++)
                for (int v = 0; v < postH; v++)
                    c.Put(x, foot + v, FacePixel(s, x, v));
            for (int y = foot; y < foot + postH; y++) { c.Put(x0, y, Edge(r)); c.Put(x0 + postW - 1, y, PixelCanvas.Mix(c.Get(x0 + postW - 1, y), P.Ink, 0.35f)); }
            if (s.kind == 0)
            {
                for (int x = x0; x < x0 + postW; x++)
                {
                    int tip = postW / 2 - Mathf.Abs(x - x0 - postW / 2) + 1;
                    for (int t = 0; t < tip; t++) c.Put(x, foot + postH + t, r[2]);
                    c.Put(x, foot + postH + tip, Edge(r));
                }
            }
            else
            {
                for (int x = x0 - 1; x <= x0 + postW; x++) { c.Put(x, foot + postH, Edge(r)); c.Put(x, foot + postH + 1, r[3]); c.Put(x, foot + postH + 2, Edge(r)); }
                if (s.crenels)
                {
                    c.Rect(x0, foot + postH + 3, x0 + 2, foot + postH + 5, r[2]);
                    c.Rect(x0 + postW - 3, foot + postH + 3, x0 + postW - 1, foot + postH + 5, r[2]);
                    c.Put(x0 + 1, foot + postH + 6, Edge(r)); c.Put(x0 + postW - 2, foot + postH + 6, Edge(r));
                }
                if (s.towers) for (int y = foot + postH - 9; y < foot + postH - 4; y++) c.Put(x0 + postW / 2, y, P.Coal[1]);   // arrow slit
            }
        }
        if (s.towers && !open)
        {
            // a team banner hung over the gate
            for (int y = top - 9; y < top; y++) for (int x = 13; x < 19; x++) c.Put(x, y, x == 13 ? P.Blue[3] : x == 18 ? P.Blue[1] : P.Blue[2]);
            c.Put(15, top - 5, P.Gold[2]); c.Put(16, top - 5, P.Gold[2]);
        }
    }

    // ---------- Assembly (used by SetupWalls) ----------

    private const int WallCanvasW = 96, WallCanvasH = 118;

    private static List<Sprite> WallPieces(string id, int age)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var s = PxWallFor(age);
        var list = new List<Sprite>();
        for (int part = 0; part < WallPartCount; part++)
        {
            // a tile either side for the diagonal runs; pivot on the middle tile's bottom-left corner
            var c = new PixelCanvas(WallCanvasW, WallCanvasH);
            PxWallPart(c, 32, 32, s, part);
            list.Add(SavePixel(c, $"{dir}/{id}_part{part}.png", PixelsPerTile, new Vector2(32f / WallCanvasW, 32f / WallCanvasH)));
        }
        return list;
    }

    private static List<Sprite> GatePieces(string id, int age)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var s = PxWallFor(age);
        var list = new List<Sprite>();
        int i = 0;
        foreach (bool eastWest in new[] { true, false })
            foreach (bool open in new[] { false, true })
            {
                var c = new PixelCanvas(32, 96);
                PxGatePiece(c, s, eastWest, open);
                list.Add(SavePixel(c, $"{dir}/{id}_{i++}.png", PixelsPerTile, Vector2.zero));
            }
        return list;
    }

    [MenuItem("Tools/Age of Stickman/Generate Wall Art")]
    public static void GenerateWallArt() => GenerateWallArt(-1);

    /// <summary>Draw wall and gate pieces; 'onlyTier' limits it to one age's pair (-1 = all).</summary>
    private static void GenerateWallArt(int onlyTier)
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var tiers = new[] { ("Palisade", "PalisadeGate"), ("MudBrickWall", "MudBrickGate"), ("StoneWall", "StoneGatehouse"), ("FortifiedWall", "FortifiedGatehouse"), ("CastleWall", "CastleGatehouse"), ("BastionWall", "BastionGate"), ("IroncladWall", "IroncladGate") };
        for (int age = 0; age < tiers.Length; age++)
        {
            if (onlyTier >= 0 && age != onlyTier) continue;
            var wall = db.GetBuilding(tiers[age].Item1);
            var gate = db.GetBuilding(tiers[age].Item2);
            if (wall != null) { wall.pieces = WallPieces(wall.id, age); wall.icon = wall.pieces[3]; EditorUtility.SetDirty(wall); }
            if (gate != null) { gate.pieces = GatePieces(gate.id, age); gate.icon = gate.pieces[0]; EditorUtility.SetDirty(gate); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Wall art generated.");
    }
}
