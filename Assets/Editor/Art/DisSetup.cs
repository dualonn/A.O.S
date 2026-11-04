using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// The counters to natural disasters (see DisSim.cs), each in its age:
/// - Well (Stone Age) -> Fire Watch (Medieval) -> Fire Station (Industrial) -> Fire Drone Hub (Information):
///   crews that put out fires round them, and clean water against sickness. Each ranks up into the next.
/// - Earthen Levee (Bronze Age) -> Flood Barrier (Modern): wall lines that hold back floodwater and lava.
/// - Seismoscope (Classical): early warning of earthquakes and eruptions.
/// - Ash Shelter (Iron Age) and Shield Dome (Astral Age): cover from falling ash, and from meteors.
/// - Research: Raised Foundations (Medieval, flood damage halved), Quake-proof Frames (Enlightenment, quake damage -40%).
/// Creates or updates only its own assets and art; safe to run again. (Tools > Age of Stickman > Setup Disasters.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Disasters")]
    public static void SetupDisasters()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Disasters: stop playing first.");
            return;
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age stoneAge = AgeNamed("StoneAge"), bronze = AgeNamed("BronzeAge"), ironAge = AgeNamed("IronAge"), classical = AgeNamed("ClassicalAge"),
            medieval = AgeNamed("MedievalAge"), enlightenment = AgeNamed("EnlightenmentAge"), industrial = AgeNamed("IndustrialAge"),
            modern = AgeNamed("ModernAge"), information = AgeNamed("InformationAge"), astral = AgeNamed("AstralAge");
        if (stoneAge == null || information == null)
        {
            Debug.LogError("Setup Disasters: the ages aren't set up yet.");
            return;
        }
        ironAge ??= bronze;
        astral ??= information;
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), bronzeR = Res(db, "Bronze"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), steel = Res(db, "Steel"), coal = Res(db, "Coal"), coin = Res(db, "Coin"), power = Res(db, "PowerCells"),
            chips = Res(db, "Microchips"), starmetal = Res(db, "Starmetal"), mana = Res(db, "Mana");
        List<ResourceAmount> C(params object[] pairs)
        {
            // Like Cost(), but skips resources this game doesn't have.
            var list = new List<ResourceAmount>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if (pairs[i] is ResourceType r && r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            return list;
        }

        // ---------- Research ----------
        Technology T(string id, string name, Age age, string description, float time, List<ResourceAmount> cost)
        {
            var t = Tech(db, id, name, age, description, time, cost, null);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>();
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            t.navigationRange = 0f;
            t.driveSpeed = 0f;
            t.maxLevel = 1;
            EditorUtility.SetDirty(t);
            return t;
        }
        T(DisCatalog.TechRaised, "Raised Foundations", medieval,
            "Stone footings and stilts lift floors above the floodline: floods do half the damage to your buildings.",
            90f, C(stone, 400, wood, 300));
        T(DisCatalog.TechQuakeFrames, "Quake-proof Frames", enlightenment,
            "A cage of crossed timbers inside every wall bends instead of breaking: earthquakes do 40% less damage.",
            120f, C(wood, 600, stone, 400, coin, 200));

        // ---------- Buildings ----------
        BuildingDefinition B(string id, string display, Age age, Vector2Int art, string desc, List<ResourceAmount> cost, int hp, int workers, float buildTime)
        {
            var b = Building(db, id, display, age, null, new Vector2Int(2, 2), art, desc, cost, buildTime, hp, workers, null);
            b.wallKind = WallTile.None;
            b.pieces = new List<Sprite>();
            b.canBuildOnHills = false;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.upgrades = new List<BuildingUpgrade>();
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.defensePower = 0f;
            b.catchmentRadius = 0;
            b.waterBonus = 0f;
            b.sortLine = 0f;
            b.maxCount = 0;
            b.rankUpTo = null;
            b.isTownCenter = false;
            EditorUtility.SetDirty(b);
            return b;
        }
        string FireText(int r) => $"<color=#fc8>Protects {r} tiles round it from fire</color>: its crew put fires out and slow them spreading.";
        string WaterText(int r) => $"<color=#9cf>Clean water</color>: sickness is less likely within {r} tiles, and does less harm there.";
        var g = DisCatalog.Guards;
        var well = B(DisCatalog.Well, "Well", stoneAge, new Vector2Int(2, 2),
            "A stone-lined well with a windlass and a leather bucket. " + FireText(g[DisCatalog.Well][0].radius) + " " + WaterText(g[DisCatalog.Well][1].radius),
            C(wood, 40, stone, 40), 220, 0, 12f);
        var watch = B(DisCatalog.FireWatch, "Fire Watch", medieval, new Vector2Int(2, 3),
            "A timber watchtower with a fire bell, a hand pump and racks of leather buckets. " + FireText(g[DisCatalog.FireWatch][0].radius) + " " + WaterText(g[DisCatalog.FireWatch][1].radius),
            C(wood, 220, stone, 120, iron, 20), 600, 2, 25f);
        var station = B(DisCatalog.FireStation, "Fire Station", industrial, new Vector2Int(2, 3),
            "A red-brick fire station and its steam pumper, with a hose-drying tower. " + FireText(g[DisCatalog.FireStation][0].radius) + " " + WaterText(g[DisCatalog.FireStation][1].radius),
            C(stone, 400, steel, 80, coal, 40, wood, 100), 1400, 3, 35f);
        var drones = B(DisCatalog.FireDrones, "Fire Drone Hub", information, new Vector2Int(2, 3),
            "A white hub where firefighting drones dock, with a water tank and a sprinkler main. " + FireText(g[DisCatalog.FireDrones][0].radius) + " " + WaterText(g[DisCatalog.FireDrones][1].radius),
            C(steel, 300, chips, 40, power, 40, stone, 200), 2400, 2, 40f);
        RiverRankUp(well, watch, null, 0.8f, 30f);
        RiverRankUp(watch, station, null, 0.8f, 35f);
        RiverRankUp(station, drones, null, 0.8f, 40f);

        var seismo = B(DisCatalog.Seismoscope, "Seismoscope", classical, new Vector2Int(2, 3),
            "A bronze urn ringed by eight dragons, each with a ball in its mouth: when the earth stirs, a ball drops into a toad below. " +
            $"<color=#fc8>Warns of earthquakes and eruptions on this world {Mathf.RoundToInt(DisCatalog.SeismoWarning)} seconds earlier</color>, and people braced in time take 15% less quake damage. One is enough.",
            C(bronzeR ?? stone, 120, stone, 200, gold, 30), 500, 1, 25f);
        seismo.maxCount = 1;
        seismo.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Signal Drums", description = $"Warnings come {Mathf.RoundToInt(DisCatalog.SeismoPerUpgrade)} seconds sooner.", requirements = new Requirements { cost = C(wood, 150, gold, 40) }, upgradeTime = 25f },
            new BuildingUpgrade { name = "Pendulum Vault", description = $"Warnings come {Mathf.RoundToInt(DisCatalog.SeismoPerUpgrade)} seconds sooner.", requirements = new Requirements { cost = C(stone, 300, bronzeR ?? stone, 100, gold, 60) }, upgradeTime = 30f },
        };
        EditorUtility.SetDirty(seismo);

        var shelter = B(DisCatalog.AshShelter, "Ash Shelter", ironAge, new Vector2Int(2, 2),
            $"A stone vault dug into the ground, roofed with turf. <color=#fc8>Protects {g[DisCatalog.AshShelter][0].radius} tiles round it from falling ash</color>, and the people living there shelter in it when a mountain erupts.",
            C(stone, 160, wood, 60), 700, 0, 20f);
        var dome = B(DisCatalog.ShieldDome, "Shield Dome", astral, new Vector2Int(2, 3),
            $"A pylon that throws a dome of force over the town. <color=#fc8>Stops meteors and falling ash within {g[DisCatalog.ShieldDome][0].radius} tiles</color>.",
            C(starmetal ?? steel, 120, power, 80, steel, 200, mana, 40), 3000, 2, 40f);

        // ---------- Levees: wall lines along the bank ----------
        BuildingDefinition Levee(string id, string display, Age age, string desc, List<ResourceAmount> cost, int hp)
        {
            var b = Building(db, id, display, age, null, Vector2Int.one, Vector2Int.one, desc, cost, 4f, hp, 0, null);
            b.wallKind = WallTile.Wall;
            b.canBuildOnHills = true;
            b.defensePower = 0.03f;
            b.production = new List<ResourceRate>();
            b.inputs = new List<ResourceRate>();
            b.upgrades = new List<BuildingUpgrade>();
            b.looks = new List<BuildingLook>();
            b.trainsUnit = null;
            b.maxUnits = 0;
            b.housing = 0;
            b.towerRange = 0f;
            b.rankUpTo = null;
            EditorUtility.SetDirty(b);
            return b;
        }
        const string leveeHow = " Drag out a line along the riverbank: <color=#9cf>floodwater and lava can't get past it</color>. It blocks everyone, like a wall (leave gaps by your bridges).";
        var levee = Levee(DisCatalog.Levee, "Earthen Levee", bronze, "A bank of packed earth faced with turf." + leveeHow, C(stone, 3, wood, 2), 500);
        var barrier = Levee(DisCatalog.FloodBarrier, "Flood Barrier", modern, "A wall of poured concrete with a steel coping." + leveeHow, C(stone, 20, steel, 3), 2600);
        RiverRankUp(levee, barrier, null, 0.8f, 6f);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only this setup's own) ----------
        foreach (var b in new[] { well, watch, station, drones, seismo, shelter, dome }) GenerateDisLooks(b);
        levee.pieces = DisWallPieces(levee.id, new PxWallStyle { ramp = new[] { P.Hex("3e2c1e"), P.Hex("6a4c32"), P.Hex("8a6844"), P.Hex("6f9a44") }, height = 12, kind = 99 }, true);
        levee.icon = levee.pieces[3];
        EditorUtility.SetDirty(levee);
        barrier.pieces = DisWallPieces(barrier.id, new PxWallStyle { ramp = ConcreteRamp, height = 20, kind = 3 }, false);
        barrier.icon = barrier.pieces[3];
        EditorUtility.SetDirty(barrier);
        AssetDatabase.SaveAssets();
        Debug.Log("Disasters set up: Well, Fire Watch, Fire Station, Fire Drone Hub, Seismoscope, Ash Shelter, Shield Dome, Earthen Levee, Flood Barrier and 2 research.");
    }

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> DisBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { DisCatalog.Well, (PxDisWell, 4, 3f) }, { DisCatalog.FireWatch, (PxDisFireWatch, 4, 4f) },
            { DisCatalog.FireStation, (PxDisFireStation, 4, 5f) }, { DisCatalog.FireDrones, (PxDisFireDrones, 4, 6f) },
            { DisCatalog.Seismoscope, (PxDisSeismoscope, 4, 3f) }, { DisCatalog.AshShelter, (PxDisAshShelter, 4, 3f) },
            { DisCatalog.ShieldDome, (PxDisShieldDome, 4, 5f) },
        };

    /// <summary>Draw one disaster building's looks (like GenerateBuildingLooks, for this file's art only).</summary>
    private static void GenerateDisLooks(BuildingDefinition def)
    {
        if (!DisBuildingArt.TryGetValue(def.id, out var art)) return;
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = Mathf.RoundToInt(def.ArtSize.x * TilePx), artH = Mathf.RoundToInt(def.ArtSize.y * TilePx);
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
        EditorUtility.SetDirty(def);
    }

    /// <summary>Wall pieces in a style of their own; 'earth' turns the plain face into packed earth and turf.</summary>
    private static List<Sprite> DisWallPieces(string id, PxWallStyle s, bool earth)
    {
        string dir = $"Buildings/{id}";
        ResetDir(dir);
        var list = new List<Sprite>();
        var face = s.ramp[2];
        for (int part = 0; part < WallPartCount; part++)
        {
            var c = new PixelCanvas(WallCanvasW, WallCanvasH);
            PxWallPart(c, 32, 32, s, part);
            if (earth)
            {
                for (int y = 0; y < c.H; y++)
                    for (int x = 0; x < c.W; x++)
                    {
                        var px = c.Get(x, y);
                        if (px.a == 0 || px.r != face.r || px.g != face.g || px.b != face.b) continue;
                        // world-aligned so neighbouring pieces join up
                        int u = ((x - 32) % 32 + 32) % 32, h = (u * 37 + y * 61 + (u ^ y) * 13) % 97;
                        var col = h < 6 ? P.Stone[2] : h < 9 ? P.Stone[1] : h < 22 ? P.Hex("745638") : h < 30 ? P.Hex("9a7850") : face;
                        if (c.Get(x, y + 1).g > 140 && h % 3 == 0) col = P.Grass[1];   // turf hanging over the top edge
                        c.Put(x, y, col);
                    }
            }
            list.Add(SavePixel(c, $"{dir}/{id}_part{part}.png", PixelsPerTile, new Vector2(32f / WallCanvasW, 32f / WallCanvasH)));
        }
        return list;
    }

    // ---------- The buildings ----------

    /// <summary>A stone-lined well: round kerb, windlass, and a bucket going up and down.</summary>
    private static void PxDisWell(K k)
    {
        k.Ground(4, 2, 60, 30, P.Dirt);
        // the kerb: a ring of fieldstones
        k.Ellipse(32, 14, 15, 7, P.Stone);
        for (int x = 18; x <= 46; x++)
        {
            int course = ((x - 18) / 5) % 2;
            for (int y = 8; y <= 18; y++)
            {
                if (y > 14 + Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - (x - 32f) * (x - 32f) / 225f)) * 6f)) continue;
                var col = (x - 18) % 5 == 0 || (y - course * 2) % 4 == 0 ? P.Stone[1] : (x + y) % 7 == 0 ? P.Stone[3] : P.Stone[2];
                k.Px(x, y, col);
            }
        }
        k.Ellipse(32, 18, 12, 4.5f, P.Stone);
        k.Ellipse(32, 18, 9.5f, 3f, new[] { P.Coal[0], P.Coal[0], P.Coal[1], P.Coal[1] });
        k.Ellipse(32, 17.5f, 7f, 1.6f, new[] { P.Water[0], P.Water[0], P.Water[1], P.Water[1] });   // dark water deep down
        // windlass posts and beam
        k.Rect(17, 16, 19, 40, P.Wood[2]); k.VLine(19, 16, 40, P.Wood[1]);
        k.Rect(45, 16, 47, 40, P.Wood[2]); k.VLine(47, 16, 40, P.Wood[1]);
        k.HLine(16, 50, 38, P.Wood[1]); k.HLine(16, 50, 39, P.Wood[3]); k.HLine(16, 50, 37, P.Wood[2]);
        k.Rect(49, 33, 50, 38, P.Wood[1]); k.HLine(50, 53, 33, P.Wood[2]);   // the crank
        // a little shingled roof over it
        k.Roof(13, 51, 41, 9, K.RoofType.Shingle, 9);
        // the rope and bucket (wound up and down)
        int by = 22 + (k.Frame < 2 ? k.Frame * 4 : (4 - k.Frame) * 4);
        k.VLine(32, by + 5, 37, P.Linen[1]);
        k.Rect(29, by, 35, by + 4, P.Wood[2]); k.HLine(29, 35, by + 2, P.Iron[2]); k.HLine(29, 35, by + 4, P.Wood[3]);
        // buckets waiting for a fire, and a trough
        k.Barrel(6, 4);
        k.Rect(48, 4, 58, 7, P.Wood[1]); k.Rect(49, 6, 57, 7, P.Water[2]);
        k.Pot(8, 12, P.Leather);
        k.Finish();
    }

    /// <summary>A timber fire watchtower with a bell, a hand pump and buckets.</summary>
    private static void PxDisFireWatch(K k)
    {
        k.Ground(2, 2, 62, 30, P.Dirt);
        // the shed: buckets, ladders and the pump
        int top = Hall(k, 28, 60, 6, 18, K.Mat.Plaster, K.RoofType.Tile, 12, 8, windows: 0);
        k.Rect(30, 30 + 6, 32, 38, P.Wood[1]);
        for (int i = 0; i < 4; i++) { k.Rect(31 + i * 7, 16, 34 + i * 7, 19, P.Leather[1]); k.HLine(31 + i * 7, 34 + i * 7, 20, P.Leather[3]); }   // buckets on pegs
        // the tower: four posts, cross braces, a platform and the bell
        k.Rect(6, 6, 8, 70, P.Wood[2]); k.VLine(8, 6, 70, P.Wood[1]);
        k.Rect(22, 6, 24, 70, P.Wood[2]); k.VLine(24, 6, 70, P.Wood[1]);
        for (int y = 12; y < 64; y += 14) { k.Line(8, y, 22, y + 12, P.Wood[1]); k.Line(22, y, 8, y + 12, P.Wood[2]); }
        k.Rect(4, 68, 26, 71, P.Wood[2]); k.HLine(4, 26, 72, P.Wood[3]); k.HLine(4, 26, 68, P.Wood[1]);
        for (int x = 4; x <= 26; x += 4) k.VLine(x, 72, 77, P.Wood[1]);
        k.HLine(4, 26, 77, P.Wood[2]);
        k.Roof(2, 28, 80, 10, K.RoofType.Shingle, 10);
        // the bell swings
        int swing = k.Frame == 1 ? 1 : k.Frame == 3 ? -1 : 0;
        k.Ellipse(15 + swing, 74.5f, 3.2f, 3.5f, P.Bronze);
        k.HLine(11 + swing, 19 + swing, 71, P.Bronze[1]);
        k.Px(15 + swing * 2, 70, P.Bronze[3]);
        // ladder
        for (int y = 8; y < 68; y += 4) k.HLine(10, 20, y, P.Wood[3]);
        // the hand pump on its cart, hose coiled
        k.Rect(40, 2, 54, 8, P.Red[1]); k.HLine(40, 54, 8, P.Red[3]);
        k.Ellipse(42, 2, 2.5f, 2.5f, P.Wood); k.Ellipse(52, 2, 2.5f, 2.5f, P.Wood);
        int arm = k.Frame % 2 == 0 ? 3 : -3;
        k.VLine(47, 8, 13, P.Iron[2]); k.Line(47 - 6, 13 + arm, 47 + 6, 13 - arm, P.Wood[1]);
        k.Ellipse(58, 5, 3, 2, P.Leather);
        k.Banner(28, top + 6, 10, P.Red, false);
        k.Finish();
    }

    /// <summary>A red-brick fire station: an arched engine door, a steam pumper, a hose tower.</summary>
    private static void PxDisFireStation(K k)
    {
        k.Ground(2, 2, 62, 24, P.Stone);
        var brick = P.Red;
        void Bricks(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int row = (y - y0) / 3, bx = (x + (row % 2) * 3) % 6;
                    k.Px(x, y, (y - y0) % 3 == 0 || bx == 0 ? P.Stone[2] : (x * 7 + row * 5) % 13 == 0 ? brick[0] : brick[1]);
                }
            for (int y = y0; y <= y1; y++) { k.Darken(x1, y, 0.3f); k.Darken(x1 - 1, y, 0.15f); }
        }
        // the hose tower
        Bricks(44, 6, 58, 76);
        k.Rect(43, 76, 59, 78, P.Stone[3]);
        k.Roof(42, 60, 79, 8, K.RoofType.Slate, 8);
        for (int y = 20; y < 70; y += 14) k.Window(48, y, 6, 7, false);
        // hoses hung to dry
        k.VLine(51, 40, 64, P.Leather[2]); k.VLine(53, 44, 64, P.Leather[1]);
        // the hall
        Bricks(4, 6, 44, 42);
        k.Rect(3, 42, 45, 45, P.Stone[3]); k.HLine(3, 45, 46, P.Stone[1]);
        for (int x = 4; x <= 44; x += 5) k.Px(x, 47, P.Stone[2]);
        // arched engine door, open, the pumper inside
        k.Rect(10, 6, 36, 30, P.Coal[1]);
        for (int x = 9; x <= 37; x++)
        {
            float t = (x - 23f) / 14f;
            int arch = 30 + Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * 5f);
            for (int y = 30; y <= arch; y++) k.Px(x, y, y == arch ? P.Stone[3] : P.Coal[1]);
        }
        k.Rect(12, 8, 34, 18, P.Red[2]); k.HLine(12, 34, 18, P.Red[3]); k.HLine(12, 34, 12, P.Gold[2]);   // the engine
        k.Rect(14, 18, 22, 27, P.Brass[2]); k.VLine(14, 18, 27, P.Brass[3]); k.VLine(22, 18, 27, P.Brass[1]);   // its boiler
        k.Rect(17, 27, 19, 31, P.Iron[1]);
        k.Ellipse(16, 7, 3, 3, P.Iron); k.Ellipse(31, 7, 3, 3, P.Iron);
        k.Rect(26, 18, 33, 20, P.Wood[2]);   // the crew's bench
        k.Banner(6, 47, 12, P.Red, false);
        // a sign over the door
        k.Rect(14, 37, 32, 41, P.Coal[2]); k.HLine(16, 30, 39, P.Gold[3]);
        k.Finish();
        k.Smoke(18, 34, 0.8f);
        if (k.Frame % 2 == 0) k.Px(18, 33, P.Fire[3]);
    }

    /// <summary>A white drone hub: landing pads, a water tank, two drones on patrol.</summary>
    private static void PxDisFireDrones(K k)
    {
        k.Ground(2, 2, 62, 24, P.Stone);
        var w = P.Chrome;
        // the hub
        k.Rect(4, 6, 46, 34, w[3]);
        for (int y = 6; y <= 34; y++) { k.Px(4, y, w[2]); k.Darken(46, y, 0.25f); }
        k.HLine(4, 46, 34, w[2]); k.HLine(4, 46, 22, P.Red[2]); k.HLine(4, 46, 21, P.Red[1]);   // a red band
        k.Rect(10, 6, 24, 19, P.Coal[1]); k.HLine(10, 24, 19, w[1]);   // bay door
        for (int x = 28; x < 44; x += 5) k.Rect(x, 26, x + 3, 30, P.Neon[2]);   // lit windows
        // landing pad on the roof
        k.Rect(6, 35, 44, 40, w[2]); k.HLine(6, 44, 40, w[3]);
        k.HLine(20, 30, 37, P.Gold[3]); k.VLine(25, 36, 39, P.Gold[3]);
        // the tank
        k.Rect(48, 6, 60, 50, P.Water[2]);
        for (int y = 6; y <= 50; y++) { k.Px(48, y, P.Water[3]); k.Px(60, y, P.Water[1]); }
        for (int y = 12; y <= 50; y += 9) k.HLine(48, 60, y, w[1]);
        k.Ellipse(54, 51, 6.5f, 2.2f, w);
        k.VLine(54, 6, 0, w[1]);   // the main running out to the town
        // the drones bob over the roof, rotors spinning
        void Drone(int x, int y)
        {
            k.Rect(x - 3, y, x + 3, y + 2, w[2]); k.HLine(x - 3, x + 3, y + 2, w[3]);
            k.Px(x, y - 1, P.Red[3]);
            int blur = k.Frame % 2;
            k.HLine(x - 7 + blur, x - 3, y + 3, w[1]); k.HLine(x + 3, x + 7 - blur, y + 3, w[1]);
            k.Px(x - 1, y - 2, P.Water[3]); k.Px(x + 1, y - 2, P.Water[3]);   // nozzle
        }
        int bob = k.Frame < 2 ? k.Frame : 4 - k.Frame;
        Drone(16, 50 + bob);
        Drone(36, 60 - bob);
        k.Finish();
        if (k.Frame % 2 == 0) { k.Px(54, 54, P.Neon[3]); k.Px(25, 41, P.Neon[3]); }
    }

    /// <summary>A pavilion over the bronze seismoscope: eight dragons round an urn, toads below.</summary>
    private static void PxDisSeismoscope(K k)
    {
        k.Ground(2, 2, 62, 30, P.Dirt);
        // stepped stone platform
        k.Rect(4, 4, 60, 9, P.Stone[2]); k.HLine(4, 60, 9, P.Stone[3]);
        k.Rect(8, 10, 56, 13, P.Stone[2]); k.HLine(8, 56, 13, P.Stone[4]);
        // columns and a tiled roof with upturned eaves
        k.Rect(10, 14, 13, 44, P.Red[1]); k.VLine(10, 14, 44, P.Red[2]);
        k.Rect(51, 14, 54, 44, P.Red[1]); k.VLine(51, 14, 44, P.Red[2]);
        k.Rect(8, 44, 56, 47, P.Wood[1]); k.HLine(8, 56, 47, P.Gold[2]);
        k.Roof(2, 62, 48, 14, K.RoofType.Tile, 14);
        k.Px(2, 49, P.Tile[3]); k.Px(1, 50, P.Tile[3]); k.Px(62, 49, P.Tile[3]); k.Px(63, 50, P.Tile[3]);   // upturned corners
        // the urn
        k.Ellipse(32, 26, 10, 11, P.Bronze);
        k.Rect(25, 35, 39, 38, P.Bronze[1]); k.Ellipse(32, 38.5f, 7, 2, P.Bronze);
        // dragons' heads round it, each holding a ball (one drops on the last frame)
        int[] dx = { -10, -6, 0, 6, 10 };
        for (int i = 0; i < dx.Length; i++)
        {
            int x = 32 + dx[i], y = 30 - Mathf.Abs(dx[i]) / 4;
            k.Rect(x - 1, y, x + 1, y + 2, P.Gold[2]); k.Px(x, y + 3, P.Gold[3]);
            bool dropped = i == 3 && k.Frame == 3;
            if (!dropped) k.Px(x, y - 1, P.Gold[3]);
        }
        // the toads, mouths open
        for (int i = 0; i < 4; i++) { int x = 20 + i * 8; k.Ellipse(x, 15.5f, 2.5f, 1.8f, P.Bronze); k.Px(x, 17, P.Coal[1]); }
        if (k.Frame == 3) k.Px(38, 18, P.Gold[3]);   // the ball in a toad's mouth
        if (k.Level >= 1)
        {
            // signal drums beside the steps
            k.Ellipse(58, 18, 4, 5, P.Red); k.HLine(54, 62, 18, P.Gold[2]);
            k.Line(55, 26, 59, 23, P.Wood[1]);
        }
        if (k.Level >= 2)
        {
            // the pendulum vault: a heavy bob hung from the beam
            int sw = k.Frame == 1 ? 1 : k.Frame == 3 ? -1 : 0;
            k.VLine(32 + sw, 38, 44, P.Iron[2]);
            k.Ellipse(32 + sw * 2, 41, 1.6f, 1.6f, P.Iron);
            k.Banner(4, 14, 18, P.Gold, false);
        }
        k.Finish();
    }

    /// <summary>A stone vault half sunk in the ground, turf on top, a heavy door and an air pipe.</summary>
    private static void PxDisAshShelter(K k)
    {
        k.Ground(2, 2, 62, 28, P.Dirt);
        k.Mound(32, 14, 28, 20, P.Stone);
        // the arched door, braced with iron
        k.Rect(24, 4, 40, 20, P.Stone[1]);
        for (int x = 26; x <= 38; x++)
        {
            float t = (x - 32f) / 6.5f;
            int arch = 16 + Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * 3f);
            for (int y = 4; y <= arch; y++) k.Px(x, y, (x - 26) % 3 == 0 ? P.Wood[1] : P.Wood[2]);
        }
        k.HLine(26, 38, 9, P.Iron[2]); k.HLine(26, 38, 15, P.Iron[2]);
        k.Px(36, 11, P.Iron[3]);
        // steps down to it
        for (int i = 0; i < 3; i++) k.HLine(22 - i * 2, 42 + i * 2, 3 - i, P.Stone[2 + (i % 2)]);
        // the air pipe and its cap
        k.Rect(48, 26, 50, 38, P.Iron[2]); k.VLine(48, 26, 38, P.Iron[3]);
        k.HLine(46, 52, 39, P.Iron[1]); k.HLine(46, 52, 40, P.Iron[2]);
        // a lamp by the door, lit
        k.VLine(18, 4, 16, P.Wood[1]);
        k.Rect(17, 16, 19, 19, k.Frame % 2 == 0 ? P.Glow[3] : P.Glow[2]);
        k.Finish();
    }

    /// <summary>A shield pylon: a glowing core, and the edge of its force dome shimmering overhead.</summary>
    private static void PxDisShieldDome(K k)
    {
        k.Ground(2, 2, 62, 26, P.Stone);
        var a = P.Astral;
        // the base
        k.Rect(12, 4, 52, 12, a[1]); k.HLine(12, 52, 12, a[3]); k.HLine(12, 52, 8, P.Gold[2]);
        // the pylon
        for (int y = 12; y < 64; y++)
        {
            int half = 8 - (y - 12) / 9;
            for (int x = 32 - half; x <= 32 + half; x++)
                k.Px(x, y, x == 32 - half ? a[3] : x == 32 + half ? a[0] : (y % 8 == 0 ? P.Gold[2] : a[2]));
        }
        // the core, pulsing
        float pulse = 0.5f + 0.5f * Mathf.Sin(k.Frame * Mathf.PI / 2f);
        k.Ellipse(32, 66, 5f + pulse, 5f + pulse, P.Neon);
        k.Px(31, 68, P.White);
        // emitter fins
        k.Line(22, 56, 27, 62, a[2], 2); k.Line(42, 56, 37, 62, a[2], 2);
        k.Finish();
        // the dome's edge, translucent, drawn after the outline
        for (int x = 0; x <= 64; x++)
        {
            float t = (x - 32f) / 34f;
            if (Mathf.Abs(t) > 1f) continue;
            int y = 18 + Mathf.RoundToInt(Mathf.Sqrt(1f - t * t) * 72f);
            byte alpha = (byte)(((x + k.Frame * 4) % 16 < 8) ? 150 : 90);
            k.Px(x, y, new Color32(140, 230, 255, alpha));
            k.Px(x, y - 1, new Color32(90, 190, 240, (byte)(alpha / 2)));
        }
    }
}
