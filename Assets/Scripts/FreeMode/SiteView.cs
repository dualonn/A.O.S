using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A wild site on the map and what happens there:
/// - Ruins: skeletons wake when your troops come near; beat them and open the chest.
/// - Dungeons: a level of their own underground (DungeonRun): room by room to the Barrow King.
/// - Lairs: wolves, giant spiders or a troll that only fight what comes close.
/// - Shrines: a hero who kneels there earns a blessing for the realm.
/// - Caches and lost caravans: walk a unit over one.
/// - Lore stones: a unit nearby reads it into the journal.
/// </summary>
public class SiteView : MonoBehaviour
{
    public static readonly List<SiteView> Live = new List<SiteView>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Live.Clear();

    public SiteState State { get; private set; }
    public int Tier { get; private set; }

    private FreeModeController controller;
    private SpriteRenderer body;
    private TextMeshPro label;
    private readonly List<Enemy> guards = new List<Enemy>();
    private bool spawned;
    private float nextCheck;
    private Enemy king;
    private bool wasDone;

    private const float GuardRadius = 5f;

    public void Init(FreeModeController owner, SiteState state, int tier)
    {
        controller = owner;
        State = state;
        Tier = tier;
        name = $"{state.kind} {state.zoneXY}";
        var grid = owner.Grid;
        Vector3 corner = grid.CellToWorld(state.cell);
        transform.position = new Vector3(corner.x, corner.y, 0);
        body = new GameObject("Body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(transform, false);
        // Sorted by its front edge, like buildings and units.
        float foot = state.kind == SiteKind.Dungeon || state.kind == SiteKind.Lair ? 0.9f : 0.15f;
        body.transform.localPosition = new Vector3(0, 0, BuildingView.DepthFor(transform.position.y + foot));
        body.sortingOrder = BuildingView.SortingOrder;
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(state.Size * 0.5f, state.Size + 0.7f, 0);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(9, 1.6f);
        label.sortingOrder = 8;
        wasDone = state.done;
        UpdateSprite();
        Live.Add(this);
    }

    private Vector3 Centre => (Vector3)State.Centre;

    /// <summary>The site under this point (with a little margin), if any.</summary>
    public static SiteView At(Vector3 world, float margin = 0.4f)
    {
        foreach (var s in Live)
        {
            if (s == null || !s.gameObject.activeSelf) continue;
            var r = new Rect(s.State.cell.x - margin, s.State.cell.y - margin, s.State.Size + margin * 2, s.State.Size + margin * 2);
            if (r.Contains(world)) return s;
        }
        return null;
    }

    private void UpdateSprite()
    {
        var art = ArtLibrary.Load();
        if (art == null) return;
        bool done = State.done;
        bool alien = controller.Settlement.OnAlienWorld;
        Sprite s = alien ? State.kind switch
        {
            SiteKind.Ruins => done ? art.alienRuinsLooted : art.alienRuins,
            SiteKind.Dungeon => done ? art.alienVaultSealed : art.alienVault,
            SiteKind.Shrine => done ? art.alienMonolithUsed : art.alienMonolith,
            SiteKind.Cache => art.alienCrash,
            _ => null,
        } : null;
        if (s == null) s = State.kind switch
        {
            SiteKind.Ruins => done ? art.siteRuinsLooted : art.siteRuins,
            SiteKind.Dungeon => done ? art.siteDungeonSealed : art.siteDungeon,
            SiteKind.Lair => art.siteLairs.Count > 0 ? art.siteLairs[State.variant % art.siteLairs.Count] : null,
            SiteKind.Shrine => done ? art.siteShrineUsed : art.siteShrine,
            SiteKind.Cache => State.variant % 3 == 0 ? art.siteCaravan : art.siteCache,
            SiteKind.Vein => art.siteVeinIds.IndexOf(State.veinId) is int v && v >= 0 && v < art.siteVeins.Count ? art.siteVeins[v] : art.siteRuinsLooted,
            _ => done ? art.siteLoreStoneRead : art.siteLoreStone,
        };
        body.sprite = s;
        body.color = State.kind == SiteKind.Lair && done ? new Color(0.8f, 0.8f, 0.8f) : Color.white;
    }

    // ---------- Each frame ----------

    private void Update()
    {
        if (controller == null || State == null) return;
        if (State.done != wasDone)
        {
            // Reset (a dungeon refilled, a lair reoccupied) or finished elsewhere.
            wasDone = State.done;
            spawned = false;
            king = null;
            UpdateSprite();
        }
        UpdateLabel();
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        // A mine built on a cleared vein covers it.
        if (State.kind == SiteKind.Vein) body.enabled = !(State.done && controller.Settlement.BuildingOn(State) != null);
        if (State.done) return;

        switch (State.kind)
        {
            case SiteKind.Ruins: TickRuins(); break;
            case SiteKind.Vein: TickVein(); break;
            case SiteKind.Dungeon: break;   // a level of its own: see DungeonRun
            case SiteKind.Lair: TickLair(); break;
            case SiteKind.Shrine: TickShrine(); break;
            case SiteKind.Cache:
                if (NearestPlayerUnit(1.4f) != null) Done(() => controller.Settlement.TakeCache(State));
                break;
            case SiteKind.LoreStone:
                if (NearestPlayerUnit(1.8f) != null) Done(() => controller.Settlement.ReadLore(State));
                break;
        }
    }

    private void Done(System.Action action)
    {
        action();
        wasDone = State.done;
        UpdateSprite();
        if (State.kind == SiteKind.Cache) gameObject.SetActive(false);
    }

    private void TickRuins()
    {
        if (!spawned && NearestPlayerUnit(12f) != null)
        {
            spawned = true;
            var db = controller.Settlement.Database;
            for (int i = 0; i < 2 + Tier; i++) SpawnGuard(i % 3 == 2 && db.skeletonArcher != null ? db.skeletonArcher : db.skeleton, GuardRadius);
        }
        guards.RemoveAll(g => g == null || !g.IsAlive);
        if (spawned && guards.Count == 0 && NearestPlayerUnit(2.6f) != null)
        {
            Done(() => controller.Settlement.OpenRuins(State));
        }
    }

    /// <summary>An old mine: spiders and the restless dead of its miners guard the seam.</summary>
    private void TickVein()
    {
        var db = controller.Settlement.Database;
        if (!spawned && NearestPlayerUnit(12f) != null)
        {
            spawned = true;
            for (int i = 0; i < 3 + Tier; i++)
                SpawnGuard(i % 3 == 1 && db.giantSpider != null ? db.giantSpider : i % 3 == 2 && db.skeletonArcher != null ? db.skeletonArcher : db.skeleton, GuardRadius);
        }
        guards.RemoveAll(g => g == null || !g.IsAlive);
        if (spawned && guards.Count == 0 && NearestPlayerUnit(3f) != null)
        {
            Done(() => controller.Settlement.ClearVein(State));
        }
    }

    private void TickLair()
    {
        var db = controller.Settlement.Database;
        if (!spawned)
        {
            spawned = true;
            int kind = State.variant % 3;
            if (kind == Settlement.LairTroll)
            {
                Spawn(db.troll, GuardRadius);
                for (int i = 0; i < Tier; i++) SpawnGuard(db.wolf, GuardRadius);
            }
            else
            {
                var unit = kind == Settlement.LairSpiders ? db.giantSpider : db.wolf;
                for (int i = 0; i < 3 + Tier; i++) SpawnGuard(unit, GuardRadius);
            }
        }
        guards.RemoveAll(g => g == null || !g.IsAlive);
        if (guards.Count == 0 && (king == null || !king.IsAlive) && NearestPlayerUnit(14f) != null)
        {
            Done(() => controller.Settlement.ClearLair(State));
        }
    }

    private void TickShrine()
    {
        foreach (var u in Unit.Living)
        {
            if (u is Troop t && t.Hero != null && t.IsAlive && Vector2.Distance(t.transform.position, Centre) < 2.2f)
            {
                Done(() => controller.Settlement.UseShrine(State, t.Hero));
                return;
            }
        }
    }

    // ---------- Helpers ----------

    private void SpawnGuard(UnitDefinition unit, float radius)
    {
        var e = Spawn(unit, radius);
        if (e != null) guards.Add(e);
    }

    private Enemy Spawn(UnitDefinition unit, float radius)
    {
        if (unit == null || unit.prefab == null) return null;
        Vector3 at = Centre + (Vector3)(Random.insideUnitCircle * 1.5f) + Vector3.down * 0.5f;
        if (!controller.Settlement.Map.IsWalkable(at)) at = Centre + Vector3.down * (State.Size * 0.5f + 0.6f);
        var e = controller.SpawnUnit(unit.prefab, at).GetComponent<Enemy>();
        if (e != null) e.StartGuard(Centre, radius);
        if (State.kind == SiteKind.Lair && unit == controller.Settlement.Database.troll) king = e;
        return e;
    }

    private Unit NearestPlayerUnit(float range)
    {
        Unit best = null;
        float bestD = range;
        foreach (var u in Unit.Living)
        {
            if (u == null || !u.IsAlive || u.Team != Team.Player) continue;
            float d = Vector2.Distance(u.transform.position, Centre);
            if (d < bestD) { bestD = d; best = u; }
        }
        return best;
    }

    private void UpdateLabel()
    {
        var grid = controller.Grid;
        bool hovered = new RectInt(State.cell, Vector2Int.one * State.Size).Contains(grid.WorldToCell(SettlementGrid.MouseWorld()));
        label.text = hovered ? Describe() : "";
    }

    private string Describe()
    {
        string age = controller.Settlement.Database.GetAge(Tier)?.displayName ?? "";
        string title, hint;
        bool alien = controller.Settlement.OnAlienWorld;
        switch (State.kind)
        {
            case SiteKind.Ruins:
                title = alien ? "Precursor ruins" : "Ancient ruins";
                hint = State.done ? "Already searched." : $"{age} dangers. Something guards an old chest.";
                break;
            case SiteKind.Dungeon:
                title = alien ? "Precursor vault" : "Dungeon";
                hint = State.done ? $"Quiet for now. It fills up again in {Mathf.CeilToInt(State.resetIn / 60f)} min." : "Select troops and right-click it to send them down. Fight room by room to the king and his hoard.";
                break;
            case SiteKind.Lair:
                int kind = State.variant % 3;
                title = kind == Settlement.LairTroll ? "Troll's cave" : kind == Settlement.LairSpiders ? "Spider nest" : "Wolf den";
                hint = State.done ? "Empty, for now." : "Its creatures attack anything that comes close.";
                break;
            case SiteKind.Shrine:
                title = alien ? "Alien monolith" : "Shrine";
                hint = State.done ? "Its blessing is given." : alien ? "Bring a hero to touch it: its makers left a gift in it." : "Bring a hero here to kneel for a blessing.";
                break;
            case SiteKind.Vein:
                var dep = controller.Settlement.VeinDeposit(State);
                var mine = controller.Settlement.VeinMineFor(State);
                if (State.done && controller.Settlement.BuildingOn(State) != null) return "";
                title = $"Old mine: rich {(dep != null ? dep.displayName : "ore")} seam";
                hint = State.done
                    ? $"Cleared. A {(mine != null ? mine.displayName : "mine")} built on it makes {controller.Settlement.Database.veinOutput:0}x as much as on a full deposit."
                    : $"{age} dangers lurk in the tunnels. Clear them to work the seam.";
                break;
            case SiteKind.Cache:
                title = alien ? "Crash site" : State.variant % 3 == 0 ? "Lost caravan" : "Buried cache";
                hint = "Walk any unit over it.";
                break;
            default:
                title = "Lore stone";
                hint = State.done ? "Read. See the Journal." : "Send any unit to read it.";
                break;
        }
        return $"<color=#ffd98a>{title}</color>\n<size=75%>{hint}</size>";
    }

    public void Remove()
    {
        foreach (var g in guards) if (g != null) Destroy(g.gameObject);
        Destroy(gameObject);
    }

    private void OnDestroy() => Live.Remove(this);
}
