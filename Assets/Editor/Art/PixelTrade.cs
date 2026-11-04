using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Trade in pixel art (Setup Caravans): the trading posts of each age, road and rail pieces that
/// join up seamlessly (16 per kind, by which sides join), and the caravans side on: ox wagon,
/// horse caravan, wagon train, steam train, maglev freighter and hover freighter.
/// </summary>
public static partial class PlaceholderArt
{
    // =====================================================================
    // Trading posts (3x3 footprint, art 3x4)
    // =====================================================================

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> TradeBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "TradePost", (PxTradePost, 4, 3f) }, { "TradeCaravanserai", (PxTradeCaravanserai, 4, 3f) },
            { "TradeCoachingInn", (PxTradeCoachingInn, 4, 4f) }, { "TradeRailStation", (PxTradeRailStation, 4, 5f) },
            { "TradeMaglevTerminal", (PxTradeMaglevTerminal, 4, 6f) }, { "TradeHoverPort", (PxTradeHoverPort, 4, 6f) },
        };

    /// <summary>Draw one trading post's looks (like GenerateBuildingLooks, for this file's art only).</summary>
    private static void GenerateTradeLooks(BuildingDefinition def)
    {
        if (!TradeBuildingArt.TryGetValue(def.id, out var art)) return;
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = def.ArtSize.x * TilePx, artH = def.ArtSize.y * TilePx;
        int w = artW + MarginSide * 2, h = artH + MarginBottom + MarginTop;
        var pivot = new Vector2(MarginSide / (float)w, MarginBottom / (float)h);
        def.looks = new List<BuildingLook>();
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            var look = new BuildingLook { fps = art.fps };
            for (int f = 0; f < art.frames; f++)
            {
                var c = new PixelCanvas(w, h);
                var k = new K(c, MarginSide, MarginBottom, level, f, art.frames, def.id.GetHashCode());
                art.draw(k);
                look.frames.Add(SavePixel(c, $"{dir}/{def.id}_L{level}_F{f}.png", PixelsPerTile, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        UnityEditor.EditorUtility.SetDirty(def);
    }

    /// <summary>Sacks of goods heaped in the yard.</summary>
    private static void TradeSacks(K k, int x, int y, int n, Color32[] ramp = null)
    {
        ramp ??= P.Linen;
        for (int i = 0; i < n; i++)
        {
            int sx = x + (i % 3) * 6 + (i / 3) * 3, sy = y + (i / 3) * 4;
            k.Ellipse(sx + 2.5f, sy + 2.5f, 3f, 2.6f, ramp);
            k.Px(sx + 2, sy + 5, ramp[1]); k.Px(sx + 3, sy + 5, ramp[1]);
        }
    }

    /// <summary>A striped market awning on two poles.</summary>
    private static void TradeAwning(K k, int x0, int x1, int y, Color32[] stripe)
    {
        k.VLine(x0, y - 14, y, P.Wood[1]); k.VLine(x1, y - 14, y, P.Wood[1]);
        for (int yy = y; yy <= y + 5; yy++)
            for (int x = x0 - 1; x <= x1 + 1; x++)
                k.Px(x, yy, ((x - x0) / 4) % 2 == 0 ? stripe[2] : P.Linen[3]);
        for (int x = x0 - 1; x <= x1 + 1; x += 2) k.Px(x, y - 1, ((x - x0) / 4) % 2 == 0 ? stripe[1] : P.Linen[2]);
    }

    /// <summary>A hanging sign on a bracket (inns and posts), swinging a little.</summary>
    private static void TradeSign(K k, int x, int y, Color32[] board)
    {
        k.HLine(x, x + 9, y, P.Iron[1]);
        int sway = k.Frame % 4 == 1 ? 1 : k.Frame % 4 == 3 ? -1 : 0;
        k.Rect(x + 2 + sway, y - 9, x + 9 + sway, y - 2, board[2]);
        k.HLine(x + 2 + sway, x + 9 + sway, y - 2, board[3]);
        k.Ellipse(x + 5.5f + sway, y - 5.5f, 1.6f, 1.6f, P.Gold);
    }

    private static void PxTradePost(K k)
    {
        k.Ground(2, 2, 93, 80, P.Dirt);
        // A mud-brick store with a thatched roof, goods piled under an awning.
        Hall(k, 36, 90, 40, 20, K.Mat.Mud, K.RoofType.Thatch, 16, 12, windows: 1);
        TradeAwning(k, 8, 32, 56, P.Red);
        TradeSacks(k, 10, 40, 4 + k.Level * 2);
        k.Pot(26, 40, P.Tile); k.Pot(30, 42, P.Tile);
        // An ox cart waiting in front, the ox dozing.
        k.Cart(50, 12, P.Straw);
        k.Ellipse(74, 20, 8, 5, P.Fur); k.Ellipse(83, 20 + (k.Frame % 2), 3, 3, P.Fur);
        k.Line(83, 24, 81, 27, P.Bone[2]); k.Line(85, 24, 87, 27, P.Bone[2]);
        for (int i = 0; i < 4; i++) k.VLine(68 + i * 4, 11, 16, P.Fur[1]);
        if (k.Level >= 1) { k.Crate(12, 18); k.Crate(20, 18); }
        if (k.Level >= 2) k.Banner(4, 20, 26, k.Team);
        k.Finish();
    }

    private static void PxTradeCaravanserai(K k)
    {
        k.Ground(2, 2, 93, 90, P.Sand);
        // A walled courtyard of stone with a tall arched gate, and a tiled hall behind.
        Hall(k, 10, 86, 54, 18, K.Mat.Stone, K.RoofType.Tile, 14, 12, windows: 2, arched: true, door: false);
        k.Wall(6, 14, 90, 38, K.Mat.Plaster);
        for (int x = 6; x <= 90; x += 6) k.Rect(x, 39, x + 3, 42, P.Plaster[2]);   // crenels
        k.Wall(38, 14, 58, 50, K.Mat.Stone);
        k.Door(42, 15, 12, 22, arched: true, open: true);
        k.Ellipse(48, 46, 4, 3, P.Tile);
        // Camels and pack horses at the gate, bales by the wall.
        TradeSacks(k, 8, 4, 3 + k.Level, P.Straw);
        k.Ellipse(74, 10, 7, 4, P.Sand); k.Ellipse(73, 15, 3.5f, 3, P.Sand); k.Ellipse(81, 13 + (k.Frame % 2), 2.5f, 2.5f, P.Sand);
        for (int i = 0; i < 4; i++) k.VLine(69 + i * 3, 3, 7, P.Sand[1]);
        k.Rect(70, 14, 76, 17, P.Red[2]);
        TradeAwning(k, 62, 88, 30, P.Blue);
        if (k.Level >= 1) k.Banner(6, 42, 22, k.Team);
        if (k.Level >= 2) k.Banner(88, 42, 22, k.Team);
        k.Finish();
    }

    private static void PxTradeCoachingInn(K k)
    {
        k.Ground(2, 2, 93, 90, P.Dirt);
        // A timber-framed inn, two storeys of plaster with a hanging sign, the coach house beside it.
        int top = Hall(k, 8, 60, 24, 34, K.Mat.Plaster, K.RoofType.Tile, 18, 14, windows: 4);
        k.HLine(8, 60, 41, P.Wood[1]);
        for (int x = 14; x <= 54; x += 12) k.Window(x, 46, 5, 6, true);
        k.Chimney(48, top + 8, 5, 12);
        k.Smoke(50, top + 22);
        TradeSign(k, 61, 50, k.Team);
        Hall(k, 64, 92, 26, 22, K.Mat.Planks, K.RoofType.Shingle, 12, 8, windows: 0, door: false);
        k.Rect(68, 27, 88, 42, P.Coal[1]);
        // A coach out front: a box body in your colours, big wheels and luggage on the roof.
        k.Rect(30, 8, 50, 20, k.Team[1]); k.Rect(32, 10, 48, 18, k.Team[2]);
        k.Rect(35, 13, 40, 17, P.Glow[2]);
        k.Rect(32, 21, 46, 23, P.Leather[2]);
        k.Ellipse(33, 6, 4, 4, P.Wood); k.Ellipse(47, 6, 4, 4, P.Wood);
        k.Px(33, 6, P.Ink); k.Px(47, 6, P.Ink);
        k.Hay(76, 4); k.Trough(56, 4, 10);
        if (k.Level >= 1) { k.Barrel(6, 6); k.Barrel(12, 6); }
        if (k.Level >= 2) k.FlowerBox(14, 44, 10);
        k.Finish();
    }

    private static void PxTradeRailStation(K k)
    {
        k.Ground(2, 2, 93, 90, P.Rock);
        // Track along the front, a platform, and a brick station with a clock and an iron canopy.
        for (int x = 0; x <= 95; x++) { k.Px(x, 4, P.Rock[1]); k.Px(x, 12, P.Rock[1]); }
        k.Rails(0, 95, 6);
        k.Rect(2, 14, 93, 20, P.Stone[2]); k.HLine(2, 93, 20, P.Stone[3]);
        int top = Hall(k, 14, 82, 24, 28, K.Mat.Stone, K.RoofType.Slate, 14, 10, windows: 4, arched: true);
        for (int y = 24; y <= top; y += 3) k.HLine(14, 82, y, P.Tile[1]);   // brick courses
        k.Ellipse(48, top + 8, 5, 5, P.Marble); k.Line(48, top + 8, 48, top + 11, P.Ink); k.Line(48, top + 8, 50, top + 8, P.Ink);
        // The canopy: iron columns and a glazed roof over the platform.
        for (int x = 6; x <= 90; x += 14) k.VLine(x, 20, 32, P.Iron[2]);
        for (int x = 4; x <= 92; x++) { k.Px(x, 33, P.Iron[1]); k.Px(x, 34, (x % 4 == 0) ? P.Iron[2] : P.Steel[3]); }
        k.Chimney(70, top + 4, 5, 10, P.Tile);
        k.Smoke(72, top + 16, 0.8f + 0.1f * (k.Frame % 2));
        k.Crate(8, 21); k.Barrel(84, 21);
        if (k.Level >= 1) { k.Crate(16, 21); k.Crate(22, 21); }
        if (k.Level >= 2) k.Banner(90, 36, 22, k.Team);
        k.Finish();
    }

    private static void PxTradeMaglevTerminal(K k)
    {
        k.Ground(2, 2, 93, 90, P.Stone);
        // A streamlined terminal of chrome and glass, its fins lit in neon, the guideway in front.
        k.Rect(0, 5, 95, 12, P.Stone[3]); k.HLine(0, 95, 12, P.Stone[4]);
        for (int x = 0; x <= 95; x++) k.Px(x, 8, (x + k.Frame * 3) % 8 < 4 ? P.Neon[2] : P.Neon[1]);
        k.Rect(4, 14, 91, 18, P.Chrome[1]);
        for (int y = 20; y <= 52; y++)
        {
            float t = (y - 20) / 32f;
            int inset = Mathf.RoundToInt(t * t * 14);
            for (int x = 10 + inset; x <= 86 - inset; x++)
                k.Px(x, y, y % 6 == 0 ? P.Chrome[1] : x < 30 ? P.Chrome[3] : x > 70 ? P.Chrome[1] : P.Chrome[2]);
        }
        for (int x = 18; x <= 78; x += 8) k.Rect(x, 26, x + 5, 36, P.Neon[0]);
        for (int x = 18; x <= 78; x += 8) k.Rect(x + 1, 33, x + 2, 35, P.Neon[3]);
        k.Rect(42, 19, 54, 32, P.Neon[1]); k.Rect(44, 19, 52, 30, P.Neon[0]);   // doors
        k.VLine(48, 52, 70, P.Chrome[2]); k.Ellipse(48, 72, 3, 3, P.Ray);       // an aerial with a glowing ball
        for (int i = 0; i < 3; i++) k.Line(24 + i * 24, 52, 24 + i * 24, 60 + (i == 1 ? 6 : 0), P.Chrome[1], 2);
        if (k.Level >= 1) { k.Crate(6, 20); k.Crate(86, 20); }
        if (k.Level >= 2) k.Banner(92, 20, 24, k.Team);
        k.Finish();
    }

    private static void PxTradeHoverPort(K k)
    {
        k.Ground(2, 2, 93, 90, P.Stone);
        // A landing pad of glowing rings, a dome hangar behind, cargo pods stacked to the side.
        k.Ellipse(36, 26, 30, 14, P.Starmetal);
        for (int r = 0; r < 3; r++)
        {
            float rx = 26 - r * 8, ry = 12 - r * 3.5f;
            for (int a = 0; a < 64; a++)
            {
                float ang = a / 64f * Mathf.PI * 2f;
                bool lit = (a + k.Frame * 4 + r * 8) % 16 < 6;
                k.Px(Mathf.RoundToInt(36 + Mathf.Cos(ang) * rx), Mathf.RoundToInt(26 + Mathf.Sin(ang) * ry), lit ? P.Neon[3] : P.Neon[1]);
            }
        }
        k.Ellipse(74, 60, 20, 18, P.Astral, l => l.y >= 52);
        k.Rect(54, 44, 94, 52, P.Starmetal[1]);
        k.Rect(66, 44, 82, 54, P.Neon[0]); k.Rect(68, 44, 80, 52, P.Neon[1]);
        for (int i = 0; i < 2 + k.Level; i++)
        {
            var ramp = i % 3 == 0 ? P.Red : i % 3 == 1 ? P.Gold : P.Green;
            k.Rect(72 + (i % 2) * 10, 8 + (i / 2) * 8, 80 + (i % 2) * 10, 14 + (i / 2) * 8, ramp[2]);
            k.HLine(72 + (i % 2) * 10, 80 + (i % 2) * 10, 14 + (i / 2) * 8, ramp[3]);
        }
        k.VLine(8, 30, 70, P.Starmetal[2]); k.Ellipse(8, 72, 2.5f, 2.5f, k.Frame % 2 == 0 ? P.Red : P.Glow);
        if (k.Level >= 2) k.Banner(92, 56, 22, k.Team);
        k.Finish();
    }

    // =====================================================================
    // Road and rail pieces: 16 per kind, index = which sides join (N=1, E=2, S=4, W=8)
    // =====================================================================

    /// <summary>Road tiers: 0 dirt track, 1 cobbles, 2 macadam, 3 asphalt. Rail: 10 rail track, 11 maglev guideway.</summary>
    private static List<Sprite> TradeRoadPieces(string id, int kind)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var list = new List<Sprite>();
        for (int mask = 0; mask < 16; mask++)
            list.Add(SavePixel(TradeRoadPiece(kind, mask), $"{dir}/{id}_piece{mask}.png", PixelsPerTile, Vector2.zero));
        return list;
    }

    /// <summary>
    /// Where a pixel lies on a road piece: across (from the centre line) and along (tile pixels, so
    /// patterns meet at the tile edge). Corners are quarter arcs round the corner between the two
    /// sides; other shapes are arms from the middle out to each joined side.
    /// </summary>
    private static bool TradeRoadAt(int mask, float x, float y, float half, out float across, out float along, out bool junction)
    {
        bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
        int count = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);
        junction = false;
        across = 99f; along = 0f;
        if (count == 2 && !(n && s) && !(e && w))
        {
            // A bend: a quarter ring round the corner between its two sides.
            float cx = e ? 32f : 0f, cy = n ? 32f : 0f;
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            across = r - 16f;
            float ang = Mathf.Atan2(Mathf.Abs(dy), Mathf.Abs(dx));   // 0 along the x edge, pi/2 along the y edge
            along = ang / (Mathf.PI * 0.5f) * 32f;
            return Mathf.Abs(across) <= half;
        }
        bool inside = false;
        float best = 99f, bestAlong = 0f;
        void Arm(bool horizontal, float from, float to)
        {
            float a = horizontal ? y - 16f : x - 16f, l = horizontal ? x : y;
            if (l < from || l > to || Mathf.Abs(a) > half) return;
            inside = true;
            if (Mathf.Abs(a) < Mathf.Abs(best)) { best = a; bestAlong = l; }
        }
        if (count == 0) Arm(true, 6f, 26f);   // a lone piece lies east-west
        else
        {
            if (e) Arm(true, 16f, 32f);
            if (w) Arm(true, 0f, 16f);
            if (n) Arm(false, 16f, 32f);
            if (s) Arm(false, 0f, 16f);
            if (count >= 3 && Mathf.Abs(x - 16f) <= half && Mathf.Abs(y - 16f) <= half)
            {
                junction = true;
                if (!inside) { inside = true; best = 0f; bestAlong = x; }
            }
            // A dead end is rounded off in the middle of the tile.
            if (count == 1 && !inside && Vector2.Distance(new Vector2(x, y), new Vector2(16f, 16f)) <= half)
            {
                inside = true;
                bool horizontal = e || w;
                best = horizontal ? y - 16f : x - 16f;
                bestAlong = horizontal ? x : y;
            }
        }
        across = best;
        along = bestAlong;
        return inside;
    }

    private static PixelCanvas TradeRoadPiece(int kind, int mask)
    {
        var c = new PixelCanvas(32, 32);
        bool rail = kind >= 10;
        float half = kind == 11 ? 8.5f : rail ? 10f : 11f;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (!TradeRoadAt(mask, px, py, half, out float across, out float along, out bool junction)) continue;
                // Dirt tracks wander a little at the edge (repeating every half tile along, so neighbours meet).
                float h = kind == 0 && !junction ? half - 1.3f + Mathf.Sin(along * 0.3927f) * 0.9f : half;
                if (Mathf.Abs(across) > h) continue;
                float edge = h - Mathf.Abs(across);
                int hash = (x * 73 + y * 151 + kind * 37) % 29;
                Color32 col;
                switch (kind)
                {
                    case 0:   // dirt track: packed earth, two wheel ruts, grass creeping in
                        col = edge < 1.2f ? P.Dirt[1] : Mathf.Abs(Mathf.Abs(across) - 4.5f) < 0.8f ? P.Dirt[1] : hash == 0 ? P.Dirt[3] : P.Dirt[2];
                        if (edge < 2f && hash % 5 == 0) col = P.Grass[2];
                        break;
                    case 1:   // cobbles: rows of rounded setts with dark joints, kerb stones
                    {
                        if (edge < 1.6f) { col = edge < 0.8f ? P.Stone[1] : P.Stone[3]; break; }
                        float u = junction ? px : along, v = junction ? py : across + 16f;
                        int row = Mathf.FloorToInt(v / 3.2f);
                        float off = row % 2 == 0 ? 0f : 2f;
                        bool joint = Mathf.Repeat(v, 3.2f) < 0.8f || Mathf.Repeat(u + off, 4f) < 0.9f;
                        col = joint ? P.Stone[1] : (Mathf.Repeat(v, 3.2f) > 2.4f ? P.Stone[3] : P.Stone[2]);
                        if (!joint && hash == 3) col = P.Rock[2];
                        break;
                    }
                    case 2:   // macadam: crushed stone, lighter edges, a grey-brown speckle
                        col = edge < 1.2f ? P.Rock[1] : hash < 3 ? P.Rock[3] : hash < 8 ? P.Rock[1] : P.Rock[2];
                        break;
                    case 3:   // asphalt: dark, kerbs, a dashed white line down the middle
                        if (edge < 1.6f) { col = edge < 0.8f ? P.Stone[1] : P.Stone[3]; break; }
                        col = hash < 2 ? P.Coal[3] : P.Coal[2];
                        if (!junction && Mathf.Abs(across) < 0.75f && Mathf.Repeat(along, 8f) < 4f) col = P.Linen[3];
                        break;
                    case 10:  // rail: ballast, sleepers across, two iron rails
                    {
                        col = hash < 4 ? P.Rock[3] : hash < 12 ? P.Rock[1] : P.Rock[2];
                        if (edge < 1f) col = P.Rock[0];
                        bool sleeper = !junction && Mathf.Repeat(along, 4f) < 2f && Mathf.Abs(across) < 8.5f;
                        if (junction) sleeper = Mathf.Abs(across) < 8.5f && (Mathf.Repeat(px, 4f) < 2f);
                        if (sleeper) col = Mathf.Repeat(along, 4f) < 1f ? P.Wood[2] : P.Wood[1];
                        float ra = Mathf.Abs(Mathf.Abs(across) - 5f);
                        if (ra < 0.6f) col = P.Steel[2];
                        else if (ra < 1.1f) col = P.Iron[0];
                        break;
                    }
                    default:  // maglev guideway: a pale concrete beam with a glowing strip down it
                    {
                        col = edge < 1f ? P.Stone[1] : edge < 2f ? P.Stone[4] : P.Stone[3];
                        float ra = Mathf.Abs(across);
                        if (ra < 1.2f) col = P.Neon[2];
                        else if (ra < 2.1f) col = P.Neon[1];
                        else if (!junction && Mathf.Repeat(along, 16f) < 1f) col = P.Stone[2];   // beam joints
                        break;
                    }
                }
                c.Put(x, y, col);
            }
        return c;
    }

    // =====================================================================
    // Caravans, side on, facing right (frame 0 idle, 1..4 on the move)
    // =====================================================================

    /// <summary>Draw one vehicle's frames: (canvas size, pivot x, smoke point) and the drawer.</summary>
    private static TradeLibrary.Vehicle TradeVehicleArt(int tier)
    {
        string name = TradeCatalog.VehicleNames[tier].Replace(" ", "");
        string dir = $"Units/Trade{name}";
        ResetDir(dir);
        int w, h, cx;
        switch (tier)
        {
            case 0: w = 84; h = 48; cx = 40; break;
            case 1: w = 96; h = 56; cx = 46; break;
            case 2: w = 112; h = 60; cx = 54; break;
            case 3: w = 180; h = 60; cx = 90; break;
            case 4: w = 180; h = 48; cx = 90; break;
            default: w = 120; h = 76; cx = 60; break;
        }
        const int gy = 4;
        var v = new TradeLibrary.Vehicle { name = TradeCatalog.VehicleNames[tier], fps = tier >= 3 ? 8f : 6f };
        var pivot = new Vector2(cx / (float)w, gy / (float)h);
        Vector2 smoke = Vector2.zero;
        for (int f = 0; f < 5; f++)
        {
            var c = new PixelCanvas(w, h);
            bool moving = f > 0;
            int frame = moving ? f - 1 : 0;
            Vector2 s;
            switch (tier)
            {
                case 0: s = PxOxWagon(c, gy, frame, moving); break;
                case 1: s = PxHorseCaravan(c, gy, frame, moving); break;
                case 2: s = PxWagonTrain(c, gy, frame, moving); break;
                case 3: s = PxSteamTrain(c, gy, frame, moving); break;
                case 4: s = PxMaglev(c, gy, frame, moving); break;
                default: s = PxHoverFreighter(c, gy, frame, moving); break;
            }
            c.Outline(P.Ink, 0.55f);
            if (tier != 5) c.Shadow(cx, gy, w * 0.42f, 2.5f, 70);
            if (s != Vector2.zero) smoke = new Vector2((s.x - cx) / PixelsPerTile, (s.y - gy) / PixelsPerTile);
            v.frames.Add(SavePixel(c, $"{dir}/Trade{name}_{(moving ? "Move" + frame : "Idle")}.png", PixelsPerTile, pivot));
        }
        v.smoke = tier == 3 || tier == 5 ? smoke : Vector2.zero;
        return v;
    }

    private static void TPx(PixelCanvas c, int x, int y, Color32 col) => c.Put(x, y, col);

    private static void TradeWheel(PixelCanvas c, float cx, float cy, float r, Color32[] ramp, int frame, int spokes = 4)
    {
        c.Ellipse(new Vector2(cx, cy), r, r, ramp);
        c.Ellipse(new Vector2(cx, cy), r - 1.4f, r - 1.4f, new[] { ramp[1], ramp[1], ramp[2], ramp[2] });
        float a0 = frame * Mathf.PI / (spokes * 2f);
        for (int i = 0; i < spokes; i++)
        {
            float a = a0 + i * Mathf.PI / spokes;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 1f);
            c.LineRaw(new Vector2(cx, cy) - d, new Vector2(cx, cy) + d, ramp[0]);
        }
        c.Put(cx, cy, P.Iron[2]);
    }

    /// <summary>Four legs at a walk (or a trot): 'phase' 0..3.</summary>
    private static void TradeLegs(PixelCanvas c, float x0, float x1, float top, float ground, Color32[] ramp, int frame, bool moving, float r = 1.3f)
    {
        float[] xs = { x0, x0 + 3, x1 - 3, x1 };
        for (int i = 0; i < 4; i++)
        {
            float swing = moving ? Mathf.Sin((frame + (i % 2 == 0 ? 0 : 2) + (i >= 2 ? 1 : 0)) * Mathf.PI / 2f) * 2.2f : 0f;
            var col = i == 1 || i == 3 ? new[] { ramp[0], ramp[0], ramp[1], ramp[1] } : ramp;
            c.Capsule(new Vector2(xs[i], top), new Vector2(xs[i] + swing, ground + 1), r, col);
            c.Put(xs[i] + swing, ground, P.Coal[1]);
        }
    }

    private static void TradeOx(PixelCanvas c, float x, float gy, int frame, bool moving)
    {
        var hide = new[] { P.Hex("4a2e20"), P.Hex("7a4e32"), P.Hex("a8744a"), P.Hex("d2a274") };
        TradeLegs(c, x - 8, x + 8, gy + 9, gy, hide, frame, moving, 1.5f);
        c.Ellipse(new Vector2(x, gy + 12), 11, 6.5f, hide);
        c.Ellipse(new Vector2(x + 2, gy + 16), 5, 2.5f, hide);   // the hump at the shoulders
        float bob = moving && frame % 2 == 1 ? -1 : 0;
        c.Ellipse(new Vector2(x + 12, gy + 12 + bob), 4, 3.4f, hide);
        c.Ellipse(new Vector2(x + 15, gy + 10 + bob), 2.2f, 2f, new[] { P.Bone[0], P.Bone[1], P.Bone[1], P.Bone[2] });
        c.LineRaw(new Vector2(x + 10, gy + 15 + bob), new Vector2(x + 8, gy + 19 + bob), P.Bone[2]);
        c.LineRaw(new Vector2(x + 13, gy + 15 + bob), new Vector2(x + 15, gy + 19 + bob), P.Bone[3]);
        c.Put(x + 13, gy + 13 + bob, P.Ink);
        c.LineRaw(new Vector2(x - 11, gy + 13), new Vector2(x - 13, gy + 7 + (moving ? frame % 2 : 0)), hide[1]);   // tail
    }

    private static void TradeHorse(PixelCanvas c, float x, float gy, int frame, bool moving, Color32[] coat)
    {
        TradeLegs(c, x - 8, x + 8, gy + 12, gy, coat, frame, moving, 1.3f);
        c.Ellipse(new Vector2(x, gy + 15), 10.5f, 5.5f, coat);
        float bob = moving && frame % 2 == 1 ? 1 : 0;
        c.Capsule(new Vector2(x + 7, gy + 17), new Vector2(x + 12, gy + 24 + bob), 3f, coat);
        c.Capsule(new Vector2(x + 12, gy + 24 + bob), new Vector2(x + 17, gy + 21 + bob), 2.3f, coat);
        c.LineRaw(new Vector2(x + 6, gy + 20), new Vector2(x + 11, gy + 27 + bob), P.Hair[1], 2);   // mane
        c.Put(x + 14, gy + 24 + bob, P.Ink);
        c.LineRaw(new Vector2(x - 10, gy + 17), new Vector2(x - 13, gy + 9 + (moving ? frame % 2 : 0)), P.Hair[1], 2);   // tail
    }

    /// <summary>A driver sitting on the box, reins in hand. Returns where the hands are.</summary>
    private static Vector2 TradeDriver(PixelCanvas c, float x, float y, Color32[] coat, Color32[] hat, int frame, bool moving)
    {
        float bob = moving && frame % 2 == 1 ? 1 : 0;
        c.Capsule(new Vector2(x, y), new Vector2(x, y + 6 + bob), 2.6f, coat);
        c.Ellipse(new Vector2(x + 0.5f, y + 10 + bob), 2.8f, 2.8f, P.Skin);
        c.Rect((int)x - 3, (int)(y + 12 + bob), (int)x + 4, (int)(y + 12 + bob), hat[1]);
        c.Rect((int)x - 1, (int)(y + 13 + bob), (int)x + 2, (int)(y + 14 + bob), hat[2]);
        var hand = new Vector2(x + 4, y + 4 + bob);
        c.LineRaw(new Vector2(x + 1, y + 5 + bob), hand, coat[1]);
        return hand;
    }

    private static Vector2 PxOxWagon(PixelCanvas c, int gy, int frame, bool moving)
    {
        // The wagon: a plank box heaped with sacks under a hide, two solid wheels.
        c.Rect(6, gy + 9, 44, gy + 17, P.Wood[2]);
        for (int x = 6; x <= 44; x += 6) c.Rect(x, gy + 9, x, gy + 17, P.Wood[1]);
        c.Rect(6, gy + 17, 44, gy + 18, P.Wood[3]);
        c.Ellipse(new Vector2(25, gy + 21), 17, 6, P.Linen, l => l.y >= gy + 18);
        c.Ellipse(new Vector2(16, gy + 22), 5, 4, P.Straw, l => l.y >= gy + 18);
        c.LineRaw(new Vector2(10, gy + 24), new Vector2(40, gy + 21), P.Leather[1]);   // a rope over the load
        TradeWheel(c, 14, gy + 6, 6, P.Wood, moving ? frame : 0, 3);
        TradeWheel(c, 36, gy + 6, 6, P.Wood, moving ? frame + 1 : 0, 3);
        c.LineRaw(new Vector2(44, gy + 11), new Vector2(60, gy + 14), P.Wood[1], 2);   // the pole
        var hands = TradeDriver(c, 42, gy + 19, P.Cloth, P.Straw, frame, moving);
        TradeOx(c, 64, gy, frame, moving);
        c.LineRaw(new Vector2(57, gy + 19), new Vector2(57, gy + 13), P.Wood[1], 2);   // the yoke
        c.LineRaw(hands, new Vector2(56, gy + 18), P.Leather[1]);
        return Vector2.zero;
    }

    private static Vector2 PxHorseCaravan(PixelCanvas c, int gy, int frame, bool moving)
    {
        // A covered wagon: canvas over hoops, spoked wheels, crates at the back.
        c.Rect(6, gy + 10, 52, gy + 17, P.Wood[2]);
        c.Rect(6, gy + 17, 52, gy + 18, P.Wood[3]);
        for (int x = 6; x <= 52; x++)
        {
            float t = (x - 6) / 46f;
            int top = gy + 30 - Mathf.RoundToInt(Mathf.Abs(t - 0.5f) * 6f);
            for (int y = gy + 19; y <= top; y++)
            {
                var col = y == top ? P.Linen[3] : (x - 6) % 12 == 0 ? P.Linen[1] : x < 20 ? P.Linen[3] : P.Linen[2];
                c.Put(x, y, col);
            }
        }
        c.Ellipse(new Vector2(53, gy + 24), 2.5f, 5, P.Coal);   // the open front
        c.Rect(1, gy + 12, 5, gy + 17, P.Wood[1]);
        TradeWheel(c, 15, gy + 7, 7, P.Wood, moving ? frame : 0);
        TradeWheel(c, 43, gy + 7, 7, P.Wood, moving ? frame + 2 : 0);
        c.LineRaw(new Vector2(52, gy + 12), new Vector2(68, gy + 15), P.Wood[1], 2);
        var hands = TradeDriver(c, 51, gy + 19, P.Leather, P.Leather, frame, moving);
        TradeHorse(c, 76, gy, frame, moving, P.Horse);
        c.LineRaw(hands, new Vector2(84, gy + 22), P.Leather[0]);
        return Vector2.zero;
    }

    private static Vector2 PxWagonTrain(PixelCanvas c, int gy, int frame, bool moving)
    {
        // A heavy freight wagon in your colours, roped bales up top, drawn by a pair of horses.
        c.Rect(4, gy + 10, 58, gy + 22, P.Blue[1]);
        c.Rect(6, gy + 12, 56, gy + 20, P.Blue[2]);
        for (int x = 8; x <= 54; x += 8) c.Rect(x, gy + 12, x, gy + 20, P.Blue[1]);
        c.Rect(4, gy + 22, 58, gy + 23, P.Gold[2]);
        for (int i = 0; i < 4; i++) c.Ellipse(new Vector2(12 + i * 12, gy + 26), 5.5f, 3.5f, i % 2 == 0 ? P.Straw : P.Linen);
        c.LineRaw(new Vector2(6, gy + 27), new Vector2(56, gy + 27), P.Leather[1]);
        TradeWheel(c, 16, gy + 7, 7.5f, P.Wood, moving ? frame : 0);
        TradeWheel(c, 46, gy + 7, 7.5f, P.Wood, moving ? frame + 2 : 0);
        c.LineRaw(new Vector2(58, gy + 12), new Vector2(76, gy + 15), P.Wood[1], 2);
        var hands = TradeDriver(c, 58, gy + 24, P.Red, P.Coal, frame, moving);
        TradeHorse(c, 80, gy, (frame + 1) % 4, moving, P.DarkHorse);   // the far horse, a step behind
        TradeHorse(c, 86, gy, frame, moving, P.Horse);
        c.LineRaw(hands, new Vector2(94, gy + 22), P.Leather[0]);
        return Vector2.zero;
    }

    private static Vector2 PxSteamTrain(PixelCanvas c, int gy, int frame, bool moving)
    {
        int wy = gy + 5;
        // Two goods wagons and the tender (left), the locomotive (right).
        void Wagon(int x0, int x1, Color32[] ramp, bool coal)
        {
            c.Rect(x0, gy + 8, x1, gy + 22, ramp[1]);
            c.Rect(x0 + 2, gy + 10, x1 - 2, gy + 20, ramp[2]);
            for (int x = x0 + 4; x < x1 - 2; x += 6) c.Rect(x, gy + 10, x, gy + 20, ramp[1]);
            c.Rect(x0, gy + 22, x1, gy + 23, ramp[3]);
            if (coal) for (int x = x0 + 2; x <= x1 - 2; x++) c.Put(x, gy + 24 + ((x * 7) % 3 == 0 ? 1 : 0), P.Coal[2]);
            TradeWheel(c, x0 + 6, wy, 4.5f, P.Iron, moving ? frame : 0, 3);
            TradeWheel(c, x1 - 6, wy, 4.5f, P.Iron, moving ? frame + 1 : 0, 3);
            c.Rect(x1 + 1, gy + 10, x1 + 3, gy + 11, P.Iron[1]);   // coupling
        }
        Wagon(2, 40, P.Wood, false);
        Wagon(44, 82, P.Red, false);
        Wagon(86, 110, P.Coal, true);
        // The locomotive: boiler, smokebox, dome, chimney, cab, big red driving wheels with rods.
        c.Capsule(new Vector2(124, gy + 18), new Vector2(166, gy + 18), 7f, P.Iron);
        c.Rect(160, gy + 11, 168, gy + 25, P.Coal[2]);
        for (int x = 128; x <= 160; x += 10) c.Rect(x, gy + 11, x, gy + 25, P.Brass[2]);   // boiler bands
        c.Ellipse(new Vector2(146, gy + 26), 3.5f, 2.5f, P.Brass);
        c.Rect(161, gy + 25, 165, gy + 33, P.Coal[2]); c.Rect(160, gy + 33, 166, gy + 35, P.Coal[3]);
        c.Rect(112, gy + 10, 126, gy + 34, P.Red[1]); c.Rect(114, gy + 12, 124, gy + 32, P.Red[2]);
        c.Rect(116, gy + 24, 122, gy + 30, P.Glow[2]);   // the cab window, firelit
        c.Rect(110, gy + 34, 128, gy + 36, P.Coal[2]);
        for (int i = 0; i < 6; i++) c.LineRaw(new Vector2(168 + i, gy + 2), new Vector2(168, gy + 10), P.Iron[1]);   // cowcatcher
        float a = moving ? frame * Mathf.PI / 2f : 0f;
        for (int i = 0; i < 3; i++) TradeWheel(c, 132 + i * 12, wy + 2, 6.5f, P.Red, moving ? frame + i : 0);
        var crank = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f;
        c.LineRaw(new Vector2(132, wy + 2) + crank, new Vector2(156, wy + 2) + crank, P.Steel[3], 2);   // the coupling rod
        c.LineRaw(new Vector2(156, wy + 2) + crank, new Vector2(164, wy + 8), P.Steel[2]);             // the piston rod
        c.Rect(160, wy + 6, 168, wy + 10, P.Iron[2]);
        TradeWheel(c, 164, wy, 3.5f, P.Iron, moving ? frame : 0, 2);
        return new Vector2(163, gy + 36);
    }

    private static Vector2 PxMaglev(PixelCanvas c, int gy, int frame, bool moving)
    {
        int lift = gy + 6 + (moving && frame % 2 == 1 ? 1 : 0);
        // A streamlined chrome train riding a cushion of light: three cars, a rounded nose, portholes.
        void Car(int x0, int x1, bool nose)
        {
            for (int x = x0; x <= x1; x++)
            {
                float round = nose ? Mathf.Max(0f, (x - (x1 - 14)) / 14f) : 0f;
                int top = lift + 18 - Mathf.RoundToInt(round * round * 12f);
                int bottom = lift + Mathf.RoundToInt(round * 3f);
                for (int y = bottom; y <= top; y++)
                {
                    var col = y == top ? P.Chrome[3] : y > top - 4 ? P.Chrome[2] : y < bottom + 3 ? P.Chrome[0] : P.Chrome[1];
                    if (y == lift + 8 || y == lift + 9) col = P.Red[2];   // the speed stripe
                    c.Put(x, y, col);
                }
            }
            for (int x = x0 + 6; x < x1 - (nose ? 18 : 6); x += 9) c.Ellipse(new Vector2(x, lift + 13), 2.4f, 2.4f, P.Neon);
            if (nose) c.Ellipse(new Vector2(x1 - 9, lift + 12), 4f, 2.6f, P.Neon);
        }
        Car(4, 56, false);
        Car(60, 112, false);
        Car(116, 176, true);
        for (int x = 4; x <= 176; x++)
        {
            bool lit = (x + frame * 5) % 10 < 6;
            c.Put(x, lift - 2, new Color32(P.Neon[2].r, P.Neon[2].g, P.Neon[2].b, (byte)(lit ? 200 : 110)));
            c.Put(x, lift - 3, new Color32(P.Neon[3].r, P.Neon[3].g, P.Neon[3].b, (byte)(lit ? 90 : 40)));
        }
        c.Line(new Vector2(140, lift + 18), new Vector2(136, lift + 24), P.Chrome[1]);   // a little fin on top
        return Vector2.zero;
    }

    private static Vector2 PxHoverFreighter(PixelCanvas c, int gy, int frame, bool moving)
    {
        int hover = gy + 18 + (frame % 2 == 1 ? 1 : 0);
        // The shadow far below, then thrusters, the hull, cargo pods and the cockpit.
        for (int x = 20; x <= 100; x++)
            for (int y = gy - 1; y <= gy + 2; y++)
                if (Mathf.Abs((x - 60) / 40f) + Mathf.Abs((y - gy - 0.5f) / 2.5f) < 1.1f) c.Put(x, y, new Color32(20, 14, 24, 70));
        for (int i = 0; i < 3; i++)
        {
            int tx = 30 + i * 30;
            for (int y = hover - 9; y < hover; y++)
            {
                byte a = (byte)(60 + (y - hover + 9) * 18 + ((frame + i) % 2) * 20);
                c.Put(tx, y, new Color32(P.Neon[3].r, P.Neon[3].g, P.Neon[3].b, a));
                c.Put(tx + 1, y, new Color32(P.Neon[2].r, P.Neon[2].g, P.Neon[2].b, a));
            }
            c.Rect(tx - 2, hover - 1, tx + 3, hover + 1, P.Starmetal[1]);
        }
        var hull = new[] { new Vector2(6, hover + 2), new Vector2(104, hover + 2), new Vector2(116, hover + 12), new Vector2(104, hover + 18), new Vector2(10, hover + 18), new Vector2(4, hover + 10) };
        c.Polygon(hull, l => l.y > hover + 15 ? P.Astral[3] : l.y > hover + 8 ? P.Astral[2] : P.Astral[1]);
        for (int x = 8; x <= 112; x++) c.Put(x, hover + 9, P.Gold[2]);
        for (int i = 0; i < 4; i++)
        {
            var ramp = i % 4 == 0 ? P.Red : i % 4 == 1 ? P.Gold : i % 4 == 2 ? P.Green : P.Blue;
            int x0 = 12 + i * 20;
            c.Rect(x0, hover + 19, x0 + 17, hover + 30, ramp[1]);
            c.Rect(x0 + 1, hover + 20, x0 + 16, hover + 29, ramp[2]);
            for (int x = x0 + 3; x < x0 + 16; x += 4) c.Rect(x, hover + 20, x, hover + 29, ramp[1]);
            c.Rect(x0, hover + 30, x0 + 17, hover + 30, ramp[3]);
        }
        c.Ellipse(new Vector2(106, hover + 20), 7, 5, P.Neon, l => l.y >= hover + 17);
        c.Put(108, hover + 23, P.White);
        if (moving) for (int y = hover + 6; y <= hover + 13; y++) c.Put(2 - (frame % 2), y, new Color32(P.Neon[3].r, P.Neon[3].g, P.Neon[3].b, 120));
        return new Vector2(4, hover + 10);
    }
}
