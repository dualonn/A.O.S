using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// A dungeon as a small underground level. Troops sent into a dungeon's mouth go down into a
/// chain of rooms joined by corridors, far from the map. Each room's creatures wake when the party
/// walks in; clearing a room raises the next gate and the party moves on, room by room, to the
/// throne room and the Barrow King, then the hoard, then back up to the surface.
/// </summary>
public class DungeonRun : MonoBehaviour
{
    public static readonly List<DungeonRun> Active = new List<DungeonRun>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Active.Clear();

    public class Room
    {
        public RectInt rect;            // floor, in tiles
        public RectInt gate;            // corridor tiles blocked until this room is cleared (none for the last)
        public bool spawned, cleared, throne;
        public readonly List<Enemy> foes = new List<Enemy>();
        public SpriteRenderer dark, gateSprite;
        public Vector3 Centre => new Vector3(rect.center.x, rect.center.y, 0);
    }

    public SiteState Site { get; private set; }
    public int Tier { get; private set; }
    public readonly List<Room> Rooms = new List<Room>();
    public readonly List<Troop> Party = new List<Troop>();
    public string Title { get; private set; }
    public RectInt Area { get; private set; }
    public Rect WorldRect => new Rect(Area.x, Area.y, Area.width, Area.height);
    public bool Finished { get; private set; }

    /// <summary>The room the party is working through (the first not yet cleared).</summary>
    public int Current { get; private set; }

    private FreeModeController controller;
    private readonly List<Troop> onTheWay = new List<Troop>();
    private Vector3 entry, chestPos;
    private SpriteRenderer chest;
    private Texture2D levelTexture;
    private bool started, looted;
    private float leaveAt = -1f;

    private Settlement Settlement => controller.Settlement;

    // ---------- Setting up ----------

    /// <summary>The run for this dungeon (made on first use).</summary>
    public static DungeonRun For(FreeModeController owner, SiteState site)
    {
        foreach (var run in Active) if (run != null && run.Site == site) return run;
        var go = new GameObject("Dungeon");
        go.transform.SetParent(owner.transform, false);
        var r = go.AddComponent<DungeonRun>();
        r.Build(owner, site);
        Active.Add(r);
        return r;
    }

    /// <summary>The run whose level contains this point, if any.</summary>
    public static DungeonRun At(Vector3 world)
    {
        foreach (var run in Active)
            if (run != null && run.WorldRect.Contains(world)) return run;
        return null;
    }

    private void Build(FreeModeController owner, SiteState site)
    {
        controller = owner;
        Site = site;
        Tier = Settlement.SiteTier(site);
        var rng = new System.Random(Settlement.State.mapSeed ^ site.cell.x * 7919 ^ site.cell.y * 104729);
        string[] names = { "Barrow", "Crypt", "Tomb", "Halls", "Catacombs", "Undervault" };
        string[] owners = { "the Forgotten", "the Hollow King", "the Last Legion", "Old Tharn", "the Grey Queen", "the Nameless" };
        Title = $"The {names[rng.Next(names.Length)]} of {owners[rng.Next(owners.Length)]}";

        // Far off to the side of the world: each dungeon gets its own spot. It stays in the same
        // north-south band as the map, because depth sorting comes from height on the map.
        int index = Settlement.State.sites.IndexOf(site);
        int baseY = Settlement.Map.Revealed.height > 0 ? Settlement.Map.Revealed.y : 900;
        var origin = new Vector2Int(20000 + index * 600, baseY);

        // Rooms in a row, joined by 3-wide corridors; the last is the throne room.
        int count = Mathf.Clamp(4 + Tier, 4, 7);
        int mid = origin.y + 12, x = origin.x + 3;
        for (int i = 0; i < count; i++)
        {
            bool throne = i == count - 1;
            int w = throne ? 18 : rng.Next(11, 16), h = throne ? 13 : rng.Next(8, 12);
            int y0 = mid - h / 2 + (throne ? 0 : rng.Next(-1, 2));
            y0 = Mathf.Clamp(y0, mid - h + 3, mid - 2);
            var room = new Room { rect = new RectInt(x, y0, w, h), throne = throne };
            x += w;
            if (!throne)
            {
                room.gate = new RectInt(x + 1, mid - 1, 1, 3);   // the gate sits in the corridor just past the room
                x += 5;
            }
            Rooms.Add(room);
        }
        Area = new RectInt(origin.x, origin.y, x - origin.x + 3, 26);

        // Carve it into the map: floors walkable, gates shut, everything else solid rock.
        Settlement.Map.StampInterior(Area, (tx, ty) => IsFloor(tx, ty) && !InGate(tx, ty));
        entry = new Vector3(Rooms[0].rect.xMin + 1.5f, Rooms[0].rect.center.y, 0);
        var throneRoom = Rooms[count - 1];
        chestPos = new Vector3(throneRoom.rect.xMax - 3f, throneRoom.rect.center.y, 0);

        DrawLevel(rng);
        foreach (var room in Rooms)
        {
            // darkness over rooms the party hasn't reached
            room.dark = Quad("Dark", new Rect(room.rect.x - 1, room.rect.y - 1, room.rect.width + 2, room.rect.height + 3), new Color(0.02f, 0.01f, 0.03f, 0.88f), 25);
            if (room.gate.width > 0) room.gateSprite = GateSprite(room.gate);
        }
    }

    private bool IsFloor(int x, int y)
    {
        foreach (var r in Rooms)
            if (r.rect.Contains(new Vector2Int(x, y))) return true;
        // corridors between rooms
        for (int i = 0; i < Rooms.Count - 1; i++)
            if (x >= Rooms[i].rect.xMax && x < Rooms[i + 1].rect.xMin && y >= Area.y + 11 && y <= Area.y + 13) return true;
        return false;
    }

    private bool InGate(int x, int y)
    {
        foreach (var r in Rooms)
            if (!r.cleared && r.gate.width > 0 && r.gate.Contains(new Vector2Int(x, y))) return true;
        return false;
    }

    // ---------- Each frame ----------

    /// <summary>Send these troops down: they walk to the dungeon's mouth and go in.</summary>
    public void Enlist(IEnumerable<Troop> troops)
    {
        Vector3 mouth = (Vector3)Site.Centre + Vector3.down * (Site.Size * 0.5f);
        foreach (var t in troops)
        {
            if (t == null || !t.IsAlive || t.Dungeon != null || onTheWay.Contains(t)) continue;
            onTheWay.Add(t);
            t.OrderMoveTo(mouth + (Vector3)(Random.insideUnitCircle * 0.8f));
        }
    }

    private void Update()
    {
        if (controller == null || Finished) return;
        // Troops arriving at the mouth go down.
        Vector3 mouth = (Vector3)Site.Centre + Vector3.down * (Site.Size * 0.5f);
        for (int i = onTheWay.Count - 1; i >= 0; i--)
        {
            var t = onTheWay[i];
            if (t == null || !t.IsAlive) { onTheWay.RemoveAt(i); continue; }
            if (Vector2.Distance(t.transform.position, mouth) < 1.6f)
            {
                onTheWay.RemoveAt(i);
                Party.Add(t);
                t.EnterDungeon(this, entry + (Vector3)(Random.insideUnitCircle * 0.8f));
                if (!started)
                {
                    started = true;
                    controller.ShowMessage($"Your troops descend into {Title}.");
                    GameAudio.UI("dungeon_enter");
                    controller.FocusDungeon(this);
                }
                RallyParty();
            }
        }
        Party.RemoveAll(t => t == null || !t.IsAlive);
        if (started && Party.Count == 0 && onTheWay.Count == 0)
        {
            Fail();
            return;
        }
        if (!started) return;

        if (leaveAt > 0)
        {
            if (Time.time >= leaveAt) LeaveWithParty();
            return;
        }
        if (Current >= Rooms.Count)
        {
            // All rooms clear: claim the hoard.
            if (!looted && NearestPartyTo(chestPos) < 1.5f) OpenHoard();
            return;
        }
        var room = Rooms[Current];
        if (!room.spawned && PartyInside(room)) WakeRoom(room);
        if (room.spawned)
        {
            room.foes.RemoveAll(e => e == null || !e.IsAlive);
            if (room.foes.Count == 0) ClearRoom(room);
        }
        FadeDarkness();
    }

    private bool PartyInside(Room room)
    {
        var r = new Rect(room.rect.x, room.rect.y, room.rect.width, room.rect.height);
        foreach (var t in Party) if (r.Contains(t.transform.position)) return true;
        return false;
    }

    private float NearestPartyTo(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (var t in Party) best = Mathf.Min(best, Vector2.Distance(t.transform.position, p));
        return best;
    }

    private void WakeRoom(Room room)
    {
        room.spawned = true;
        var db = Settlement.Database;
        int n = 2 + Tier + Current;
        if (room.throne)
        {
            Spawn(room, db.barrowKing, room.rect.xMax - 4);
            for (int i = 0; i < 2 + Tier; i++) Spawn(room, i % 2 == 0 ? db.skeleton : db.skeletonArcher, room.rect.xMax - 6);
            controller.ShowMessage($"{(db.barrowKing != null ? db.barrowKing.displayName : "The king")} rises from his throne!");
            return;
        }
        for (int i = 0; i < n; i++)
        {
            UnitDefinition unit = (Current % 3) switch
            {
                1 => i % 3 == 2 ? db.skeleton : db.giantSpider,
                2 => i % 2 == 0 ? db.skeletonArcher : db.skeleton,
                _ => i % 4 == 3 ? db.giantSpider : db.skeleton,
            };
            Spawn(room, unit, room.rect.xMax - 3);
        }
    }

    private void Spawn(Room room, UnitDefinition unit, int atX)
    {
        if (unit == null || unit.prefab == null) return;
        var at = new Vector3(atX + Random.Range(-1.5f, 1.5f), room.rect.center.y + Random.Range(-2.5f, 2.5f), 0);
        var e = controller.SpawnUnit(unit.prefab, at).GetComponent<Enemy>();
        if (e == null) return;
        e.StartGuard(room.Centre, Mathf.Max(room.rect.width, room.rect.height) * 0.6f);
        room.foes.Add(e);
    }

    private void ClearRoom(Room room)
    {
        room.cleared = true;
        int done = Current + 1;
        Current++;
        if (room.gate.width > 0)
        {
            for (int y = room.gate.yMin; y < room.gate.yMax; y++)
                for (int x = room.gate.xMin; x < room.gate.xMax; x++)
                    Settlement.Map.SetInteriorFloor(x, y, true);
            if (room.gateSprite != null) room.gateSprite.enabled = false;
        }
        if (Current < Rooms.Count)
        {
            controller.ShowMessage(Rooms[Current].throne ? "The way to the throne room is open..." : $"Room {done} cleared. The gate rises.");
        }
        else
        {
            controller.ShowMessage("The throne room falls silent. The hoard is yours!");
            chest.sprite = ChestSprite(open: false);
            chest.enabled = true;
        }
        RallyParty();
    }

    /// <summary>Everyone heads for the next room (or the hoard).</summary>
    private void RallyParty()
    {
        Vector3 goal = Current < Rooms.Count ? Rooms[Current].Centre + Vector3.left * (Rooms[Current].rect.width * 0.2f) : chestPos + Vector3.left;
        int i = 0, n = Party.Count;
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
        foreach (var t in Party)
        {
            int row = i / columns, col = i % columns;
            t.SetDungeonRally(goal + new Vector3(-col * 0.7f, (row - (n - 1) / columns * 0.5f) * 0.7f, 0));
            i++;
        }
        if (controller.Viewing == this) controller.PanTo(goal);   // the camera follows the party on
    }

    private void OpenHoard()
    {
        looted = true;
        chest.sprite = ChestSprite(open: true);
        GameAudio.UI("chest_open");
        Settlement.ClearDungeon(Site);
        leaveAt = Time.time + 3f;
    }

    private void LeaveWithParty()
    {
        Vector3 mouth = (Vector3)Site.Centre + Vector3.down * (Site.Size * 0.5f + 1f);
        foreach (var t in Party)
        {
            if (t == null) continue;
            t.LeaveDungeon(mouth + (Vector3)(Random.insideUnitCircle * 1.2f), controller.GetRallyPoint(t.Home));
        }
        controller.ShowMessage($"Your troops climb back out of {Title}.");
        End();
    }

    private void Fail()
    {
        controller.ShowMessage($"Your party fell in {Title}. Its dead keep watch until you return.");
        End();
    }

    private void End()
    {
        Finished = true;
        foreach (var room in Rooms)
            foreach (var e in room.foes)
                if (e != null) Destroy(e.gameObject);
        foreach (var t in onTheWay) if (t != null && t.Home != null) t.ReturnHome(controller.GetRallyPoint(t.Home));
        controller.LeaveDungeonView(this);
        Active.Remove(this);
        Destroy(gameObject);
    }

    private void FadeDarkness()
    {
        for (int i = 0; i < Rooms.Count; i++)
        {
            var d = Rooms[i].dark;
            if (d == null) continue;
            float target = i <= Current ? 0f : 0.88f;
            if (i == Current && !Rooms[i].spawned && !PartyNear(Rooms[i])) target = 0.55f;   // a glimpse of the next room
            var c = d.color;
            c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * 1.5f);
            d.color = c;
        }
    }

    private bool PartyNear(Room room) => NearestPartyTo(room.Centre) < room.rect.width;

    private void OnDestroy()
    {
        Active.Remove(this);
        if (levelTexture != null) Destroy(levelTexture);
    }

    // ---------- Drawing the level ----------

    private SpriteRenderer Quad(string objectName, Rect r, Color color, int order)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = SimpleSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 size = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(r.width / size.x, r.height / size.y, 1);
        sr.transform.position = new Vector3(r.center.x, r.center.y, -2f);
        return sr;
    }

    private void DrawLevel(System.Random rng)
    {
        const int T = 32;
        int w = Area.width * T, h = Area.height * T;
        var c = new PixelCanvas(w, h);
        bool Floor(int tx, int ty) => IsFloor(tx + Area.x, ty + Area.y);
        for (int ty = 0; ty < Area.height; ty++)
            for (int tx = 0; tx < Area.width; tx++)
            {
                bool floor = Floor(tx, ty);
                bool face = !floor && Floor(tx, ty - 1);   // a wall seen from the front, standing on the floor below it
                for (int py = 0; py < T; py++)
                    for (int px = 0; px < T; px++)
                    {
                        int x = tx * T + px, y = ty * T + py;
                        Color32 col;
                        if (floor)
                        {
                            // big flagstones, 16 px, offset every other row
                            int sx = (x + ((y / 16) % 2) * 8) % 16, sy = y % 16;
                            bool seam = sx == 0 || sy == 0;
                            int tone = ((x + ((y / 16) % 2) * 8) / 16 * 7 + (y / 16) * 13) % 5;
                            col = seam ? P.Coal[2] : tone == 0 ? PixelCanvas.Mix(P.Stone[1], P.Stone[2], 0.5f) : P.Stone[1];
                            if (!seam && (x * 7 + y * 11) % 97 == 0) col = P.Stone[0];
                            if (!seam && (x * 3 + y * 5) % 151 == 0) col = P.Leaf[1];   // moss
                        }
                        else if (face)
                        {
                            // dressed stone blocks, darker towards the floor
                            int row = py / 8, bx = (px + row * 6) % 16;
                            bool seam = py % 8 == 0 || bx == 0;
                            col = seam ? P.Coal[1] : py > 24 ? P.Stone[2] : P.Stone[1];
                            if (py < 4) col = P.Coal[1];
                        }
                        else
                        {
                            // solid rock above
                            int n = (x * 13 + y * 7) % 23;
                            col = n == 0 ? P.Coal[2] : P.Coal[0];
                        }
                        c.Put(x, y, col);
                    }
            }
        // shadow along the foot of every wall face
        for (int ty = 0; ty < Area.height; ty++)
            for (int tx = 0; tx < Area.width; tx++)
                if (Floor(tx, ty) && !Floor(tx, ty + 1))
                    for (int px = 0; px < T; px++)
                        for (int py = 26; py < 32; py++)
                            c.Put(tx * T + px, ty * T + py, new Color32(10, 6, 14, (byte)(30 + (py - 26) * 18)));

        // Each room: torches on the back wall, bones, pillars; the entrance stairs; the throne.
        foreach (var room in Rooms)
        {
            int rx = (room.rect.x - Area.x) * T, ry = (room.rect.y - Area.y) * T, rw = room.rect.width * T, rh = room.rect.height * T;
            for (int i = 1; i <= 2; i++)
            {
                int tx = rx + rw * i / 3, ty = ry + rh + 12;
                Torch(c, tx, ty);
            }
            for (int i = 0; i < 3; i++) Bones(c, rx + 20 + rng.Next(rw - 60), ry + 10 + rng.Next(rh - 40), rng);
            if (room.rect.width >= 13 && !room.throne)
            {
                Pillar(c, rx + 2 * T, ry + rh - 3 * T);
                Pillar(c, rx + rw - 3 * T, ry + rh - 3 * T);
            }
            if (room.throne)
            {
                // a red carpet to a stone throne
                int cy = ry + rh / 2;
                for (int x = rx + 16; x < rx + rw - 64; x++)
                    for (int y = cy - 12; y <= cy + 12; y++)
                        c.Put(x, y, Mathf.Abs(y - cy) >= 11 ? P.Gold[1] : P.Red[1]);
                Throne(c, rx + rw - 72, cy - 18);
                Banner(c, rx + rw / 3, ry + rh + 2); Banner(c, rx + rw * 2 / 3, ry + rh + 2);
            }
        }
        // entrance stairs going up, on the first room's left wall
        var first = Rooms[0];
        int sx0 = (first.rect.x - Area.x) * T, sy0 = Mathf.RoundToInt((first.rect.center.y - Area.y) * T);
        for (int i = 0; i < 6; i++)
            for (int y = sy0 - 20 + i * 6; y < sy0 - 14 + i * 6; y++)
                for (int x = sx0 - 4; x < sx0 + 28 - i * 4; x++)
                    c.Put(x, y, i % 2 == 0 ? P.Stone[3] : P.Stone[2]);

        levelTexture = c.ToTexture();
        levelTexture.name = Title;
        var sr = new GameObject("Level").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = Sprite.Create(levelTexture, new Rect(0, 0, w, h), Vector2.zero, T, 0, SpriteMeshType.FullRect);
        sr.transform.position = new Vector3(Area.x, Area.y, 50f);   // far back: everything stands on it
        sr.sortingOrder = -50;

        chest = new GameObject("Hoard").AddComponent<SpriteRenderer>();
        chest.transform.SetParent(transform, false);
        chest.transform.position = new Vector3(chestPos.x - 0.5f, chestPos.y - 0.3f, BuildingView.DepthFor(chestPos.y - 0.3f));
        chest.sortingOrder = BuildingView.SortingOrder;
        chest.enabled = false;
    }

    private static void Torch(PixelCanvas c, int x, int y)
    {
        c.Rect(x, y, x + 2, y + 8, P.Iron[1]);
        c.Rect(x - 1, y + 8, x + 3, y + 9, P.Iron[2]);
        c.Rect(x, y + 10, x + 2, y + 14, P.Fire[2]); c.Put(x + 1, y + 15, P.Fire[3]); c.Put(x + 1, y + 11, P.Fire[3]);
        // warm glow on the wall
        for (int yy = y - 10; yy < y + 26; yy++)
            for (int xx = x - 14; xx < x + 16; xx++)
            {
                float d = ((xx - x) * (xx - x) + (yy - y - 12) * (yy - y - 12)) / 196f;
                if (d < 1 && c.Get(xx, yy).a > 0) c.Put(xx, yy, new Color32(255, 170, 70, (byte)(40 * (1 - d))));
            }
    }

    private static void Bones(PixelCanvas c, int x, int y, System.Random rng)
    {
        for (int i = 0; i < 6; i++) c.Put(x + i, y + (i % 3 == 0 ? 1 : 0), P.Bone[2]);
        c.Put(x - 1, y, P.Bone[3]); c.Put(x + 6, y + 1, P.Bone[3]);
        if (rng.Next(3) == 0) { c.Rect(x + 9, y, x + 12, y + 3, P.Bone[2]); c.Put(x + 10, y + 1, P.Coal[1]); c.Put(x + 12, y + 1, P.Coal[1]); }
    }

    private static void Pillar(PixelCanvas c, int x, int y)
    {
        for (int yy = y; yy < y + 70; yy++)
            for (int xx = x; xx < x + 16; xx++)
                c.Put(xx, yy, xx < x + 4 ? P.Stone[3] : xx > x + 11 ? P.Stone[1] : P.Stone[2]);
        c.Rect(x - 3, y + 70, x + 18, y + 74, P.Stone[3]);
        c.Rect(x - 3, y - 4, x + 18, y, P.Stone[1]);
    }

    private static void Throne(PixelCanvas c, int x, int y)
    {
        c.Rect(x, y, x + 36, y + 10, P.Stone[2]);            // dais
        c.Rect(x + 8, y + 10, x + 28, y + 48, P.Stone[1]);   // high back
        c.Rect(x + 10, y + 12, x + 26, y + 46, P.Purple[1]);
        c.Rect(x + 4, y + 10, x + 32, y + 20, P.Stone[2]);   // seat and arms
        c.Rect(x + 15, y + 48, x + 21, y + 54, P.Gold[2]); c.Put(x + 18, y + 55, P.Gold[3]);
    }

    private static void Banner(PixelCanvas c, int x, int y)
    {
        c.Rect(x, y, x + 14, y + 26, P.Purple[2]);
        c.Rect(x, y, x + 2, y + 26, P.Purple[1]);
        for (int i = 0; i < 4; i++) c.Put(x + 3 + i * 3, y - 1, P.Purple[2]);
        c.Rect(x + 5, y + 12, x + 9, y + 16, P.Bone[3]);
    }

    private SpriteRenderer GateSprite(RectInt gate)
    {
        // an iron portcullis filling the corridor
        int w = 32, h = 3 * 32 + 16;
        var c = new PixelCanvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool bar = x % 8 < 3 || y % 16 < 3;
                if (bar) c.Put(x, y, x % 8 == 0 || y % 16 == 0 ? P.Iron[3] : P.Iron[1]);
            }
        for (int x = 0; x < w; x += 8) c.Put(x + 1, 0, P.Iron[3]);
        var tex = c.ToTexture();
        var sr = new GameObject("Gate").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, 32, 0, SpriteMeshType.FullRect);
        sr.transform.position = new Vector3(gate.x, gate.y, BuildingView.DepthFor(gate.y));
        sr.sortingOrder = BuildingView.SortingOrder;
        return sr;
    }

    private static Sprite ChestSprite(bool open)
    {
        var c = new PixelCanvas(20, 18);
        c.Rect(2, 1, 17, 8, P.Wood[2]); c.Rect(2, 8, 17, 8, P.Wood[3]);
        c.Rect(5, 1, 5, 8, P.Gold[1]); c.Rect(14, 1, 14, 8, P.Gold[1]);
        if (open)
        {
            c.Rect(3, 7, 16, 8, P.Gold[2]); c.Rect(4, 9, 15, 10, P.Gold[3]);   // coins heaped up
            c.Rect(2, 11, 17, 16, P.Wood[1]);
        }
        else
        {
            c.Rect(2, 9, 17, 12, P.Wood[3]); c.Rect(9, 6, 10, 10, P.Gold[2]);
        }
        c.Outline(P.Ink, 0.6f);
        var tex = c.ToTexture();
        return Sprite.Create(tex, new Rect(0, 0, 20, 18), new Vector2(0.5f, 0.05f), 32, 0, SpriteMeshType.FullRect);
    }
}
