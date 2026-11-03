using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// In-between steps so building lines don't jump an age:
/// - Iron Age Riding School (Horse Archers), between the Bronze Age Chariot Yard and the Classical Stables.
/// - Renaissance Finery Forge, between the Medieval Steelworks and the Enlightenment Blast Furnace.
/// Safe to run again. (Tools > Age of Stickman > Fill Age Gaps.)
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> AgeLinkBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "RidingSchool", (PxRidingSchool, 4, 3f) }, { "FineryForge", (PxFineryForge, 4, 6f) },
        };

    [MenuItem("Tools/Age of Stickman/Fill Age Gaps")]
    public static void FillAgeGaps()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age iron = db.ages.Find(a => a != null && a.name == "IronAge");
        Age renaissance = db.ages.Find(a => a != null && a.name == "RenaissanceAge");
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), ironRes = Res(db, "Iron"),
            bronze = Res(db, "Bronze"), charcoal = Res(db, "Charcoal"), steel = Res(db, "Steel"), tools = Res(db, "Tools"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin");

        // ---------- Iron Age cavalry ----------
        var horseArcher = MedievalUnit("HorseArcher", "Horse Archer", iron, "Horseman", 230, 16, 1.4f, 4.4f, 1.65f);
        horseArcher.upkeep = new List<ResourceRate> { Rate(food, 0.012f) };
        EditorUtility.SetDirty(horseArcher);
        if (!iron.playerUnits.Contains(horseArcher)) iron.playerUnits.Add(horseArcher);
        EditorUtility.SetDirty(iron);
        var riding = InsertStep(db, "ChariotYard", "RidingSchool", "Riding School", iron, db.GetTechnology("Archery"),
            "A timber school round a paddock, where riders learn to shoot from the saddle. Trains Horse Archers.",
            Cost(wood, 180, food, 120, ironRes, 30), Cost(wood, 90, ironRes, 20), 300);
        riding.trainsUnit = horseArcher;
        riding.maxUnits = 3;
        riding.workers = 3;
        riding.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Bigger Paddock", description = "+1 horse archer.", requirements = new Requirements { cost = Cost(food, 150, wood, 80) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Horn Bows", description = "Horse archers hit 20% harder.", requirements = new Requirements { cost = Cost(ironRes, 60, bronze, 40) }, upgradeTime = 25f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.2f } },
        };

        // ---------- Renaissance steel ----------
        var forge = InsertStep(db, "Steelworks", "FineryForge", "Finery Forge", renaissance, db.GetTechnology("Artillery"),
            "A water-driven forge that hammers pig iron into wrought iron and steel for the guns. A little more steel than the old works.",
            Cost(stone, 300, ironRes, 120, steel, 40), Cost(stone, 150, steel, 25), 700);
        forge.workers = 4;
        forge.production = new List<ResourceRate> { Rate(steel, 0.12f) };    // works 0.1 -> forge 0.12 -> blast furnace 0.15
        forge.inputs = new List<ResourceRate> { Rate(ironRes, 0.13f), Rate(charcoal, 0.1f) };
        forge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Tilt Hammer", "+30% steel.", Cost(wood, 200, ironRes, 80), 1.3f),
            Upgrade("Second Hearth", "+30% steel.", Cost(stone, 200, tools, 40), 1.3f),
        };

        EditorUtility.SetDirty(riding);
        EditorUtility.SetDirty(forge);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        GenerateBuildingLooks(riding);
        GenerateBuildingLooks(forge);
        GenerateWarriorsAndCrews(db, new HashSet<string> { "HorseArcher" });
        AssetDatabase.SaveAssets();
        Debug.Log("Age gaps filled.");
    }

    /// <summary>
    /// Put a new building between 'fromId' and whatever it used to rank up into: from -> new -> old target.
    /// The new step keeps the old rank-up's requirements for its own way on.
    /// </summary>
    private static BuildingDefinition InsertStep(GameDatabase db, string fromId, string id, string display, Age age, Technology needs,
        string description, List<ResourceAmount> cost, List<ResourceAmount> rankCost, int health)
    {
        var from = db.GetBuilding(fromId);
        var existing = db.GetBuilding(id);
        var next = from.rankUpTo;
        var nextReq = from.rankUpRequirements;
        float nextTime = from.rankUpTime;
        if (existing != null && next == existing) { next = existing.rankUpTo; nextReq = existing.rankUpRequirements; nextTime = existing.rankUpTime; }
        var def = RankUp(db, fromId, id, display, age, needs, description, cost, rankCost, health);
        def.rankUpTo = next;
        def.rankUpRequirements = nextReq;
        def.rankUpTime = nextTime;
        return def;
    }

    private static void PxRidingSchool(K k)
    {
        // A long timber hall with a thatched roof, a fenced paddock with a straw target, a horse at the rail.
        k.Ground(2, 2, 94, 30);
        k.Wall(6, 14, 50, 30, K.Mat.Logs);
        k.Door(22, 15, 10, 12);
        k.Roof(3, 53, 30, 12, K.RoofType.Thatch, 8);
        k.Fence(56, 92, 4, 6);
        k.Fence(56, 92, 24, 6);
        k.Target(80, 10);
        var coat = P.Horse;
        k.Ellipse(66, 12, 5, 3, coat);
        k.Rect(70, 12 + (k.Frame % 2), 72, 17 + (k.Frame % 2), coat[2]);
        k.VLine(63, 6, 10, coat[1]); k.VLine(69, 6, 10, coat[1]);
        for (int i = 0; i < 1 + k.Level; i++) k.Hay(6 + i * 9, 2);
        if (k.Level >= 1) k.Banner(52, 30, 16);
        k.Finish();
    }

    private static void PxFineryForge(K k)
    {
        // A stone forge by a mill race: the wheel drives a great tilt hammer; a glowing hearth and bars of iron.
        k.Ground(2, 2, 94, 40, P.Dirt);
        int top = Hall(k, 8, 60, 12, 20, K.Mat.Stone, K.RoofType.Tile, 12, 10, windows: 1);
        k.Chimney(16, top + 4, 6, 18, Brick);
        k.Rect(28, 13, 40, 24, P.Coal[1]);
        float glow = 0.5f + 0.5f * k.Wave();
        k.Rect(30, 13, 38, 18, glow > 0.5f ? P.Glow[2] : P.Fire[2]);
        // the wheel and its hammer
        float cx = 78, cy = 22, r = 13;
        k.Rect(64, 4, 94, 9, P.Water[2]); k.HLine(64, 94, 9, P.Water[3]);
        k.Ellipse(cx, cy, r, r, P.Wood, l => (l - new Vector2(cx, cy)).sqrMagnitude > (r - 3) * (r - 3));
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f + k.T / 8f) * Mathf.PI * 2;
            k.Line(cx, cy, cx + Mathf.Cos(a) * (r - 1), cy + Mathf.Sin(a) * (r - 1), P.Wood[1]);
        }
        bool up = k.Frame % 2 == 0;
        k.Line(46, 8, 62, up ? 18 : 12, P.Wood[2], 2);
        k.Rect(44, up ? 10 : 6, 49, up ? 15 : 11, P.Iron[1]);
        for (int i = 0; i < 2 + k.Level; i++) k.Rect(6 + i * 6, 3, 10 + i * 6, 4, P.Iron[2]);   // bars of wrought iron
        if (k.Level >= 2) k.Banner(2, 4, 22);
        k.Finish();
        k.Smoke(19, top + 26, 0.8f, 140);
        if (!up) { k.Px(47, 5, P.Glow[3]); k.Px(50, 7, P.White); }   // sparks under the hammer
    }
}
