using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A raider camp on the free mode map: tents round a fire, guarded by a few raiders. Raids set
/// out from it. Troops on Attack burn it down for loot; raiders move back in later.
/// </summary>
public class CampView : MonoBehaviour
{
    /// <summary>Every camp standing in the scene (troops look here for targets).</summary>
    public static readonly List<CampView> Live = new List<CampView>();

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Live.Clear();

    public CampState State { get; private set; }
    public int Tier { get; private set; }
    public bool IsStanding => health > 0;

    private FreeModeController controller;
    private SpriteRenderer body;
    private SpriteRenderer barBack, barFill;
    private TextMeshPro label;
    private List<Sprite> bannerFrames, fireFrames;
    private SpriteRenderer banner, fire;
    private float health, maxHealth;
    private float shake;
    private readonly List<Enemy> guards = new List<Enemy>();
    private float nextGuardCheck;
    private int guardCount;
    private Enemy chief;

    /// <summary>
    /// The stronger camps (Bronze Age and up) often have a raider chief. Fixed per camp, and more
    /// likely each time raiders move back in.
    /// </summary>
    public bool HasChief
    {
        get
        {
            if (Tier < 1) return false;
            uint h = (uint)(State.cell.x * 73856093) ^ (uint)(State.cell.y * 19349663);
            float roll = (h % 1000) / 1000f;
            return roll < 0.3f + 0.15f * Tier + 0.2f * State.extraTier;
        }
    }

    /// <summary>The chief guarding this camp, if alive.</summary>
    public Enemy Chief => chief != null && chief.IsAlive ? chief : null;

    private const float GuardRadius = 5f;

    public void Init(FreeModeController owner, CampState state, int tier)
    {
        controller = owner;
        State = state;
        Tier = tier;
        name = $"Raider camp {state.zoneXY}";
        maxHealth = health = 400f + tier * 400f;
        guardCount = 3 + tier * 2;

        var grid = owner.Grid;
        Vector3 corner = grid.CellToWorld(state.cell);

        // Each piece sorts by where it stands (its foot, in tiles up the camp), like units do.
        transform.position = new Vector3(corner.x, corner.y, 0);
        var art = ArtLibrary.Load();
        body = Piece("Ground", art != null ? art.campGround : null, 3.2f, BuildingView.SortingOrder - 2); // flat: always under
        Piece("BackTent", art != null ? art.campBackTent : null, 1.55f, BuildingView.SortingOrder);
        banner = Piece("Banner", null, 1.6f, BuildingView.SortingOrder);
        Piece("FrontTent", art != null ? art.campFrontTent : null, 0.95f, BuildingView.SortingOrder);
        fire = Piece("Fire", null, 0.92f, BuildingView.SortingOrder);
        Piece("Loot", art != null ? art.campLoot : null, 0.25f, BuildingView.SortingOrder);
        bannerFrames = art != null ? art.campBanner : null;
        fireFrames = art != null ? art.campFire : null;

        float size = CampState.Size * grid.cellSize;
        barBack = Quad("BarBack", new Vector2(size * 0.2f, size + 0.15f), new Vector2(size * 0.6f, 0.16f), new Color(0, 0, 0, 0.6f), 6);
        barFill = Quad("BarFill", new Vector2(size * 0.2f, size + 0.15f), new Vector2(size * 0.6f, 0.16f), new Color(0.9f, 0.3f, 0.25f), 7);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(size / 2, size + 0.65f, 0);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(8, 1.5f);
        label.sortingOrder = 8;

        Live.Add(this);
    }

    private SpriteRenderer Piece(string objectName, Sprite sprite, float footTiles, int order)
    {
        var grid = controller.Grid;
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localScale = Vector3.one * grid.cellSize;
        float worldFoot = transform.position.y + footTiles * grid.cellSize;
        sr.transform.localPosition = new Vector3(0, 0, BuildingView.DepthFor(worldFoot));
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private SpriteRenderer Quad(string objectName, Vector2 offset, Vector2 size, Color color, int order)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = offset;
        sr.transform.localScale = new Vector3(size.x, size.y, 1);
        sr.sprite = SimpleSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public Vector3 Centre => controller.Grid.CellToWorld(State.cell) + Vector3.one * (CampState.Size * 0.5f * controller.Grid.cellSize);

    /// <summary>The point on the camp's footprint nearest 'from'.</summary>
    public Vector3 ClosestPoint(Vector3 from)
    {
        Vector3 min = controller.Grid.CellToWorld(State.cell);
        Vector3 max = min + Vector3.one * CampState.Size * controller.Grid.cellSize;
        return new Vector3(Mathf.Clamp(from.x, min.x, max.x), Mathf.Clamp(from.y, min.y, max.y), 0);
    }

    public void TakeHit(float amount)
    {
        if (health <= 0)
        {
            return;
        }
        health = Mathf.Max(0, health - amount);
        shake = 0.15f;
        if (health <= 0)
        {
            controller.CampDestroyed(this);
        }
    }

    /// <summary>The standing camp whose footprint (plus a little margin) is under this point, or null.</summary>
    public static CampView At(Vector3 world, float margin = 0.4f)
    {
        foreach (var camp in Live)
        {
            if (camp != null && camp.IsStanding && Vector2.Distance(world, camp.ClosestPoint(world)) <= margin) return camp;
        }
        return null;
    }

    /// <summary>The nearest standing camp within 'range' of 'from', or null.</summary>
    public static CampView Nearest(Vector3 from, float range)
    {
        CampView best = null;
        float bestDistance = range;
        foreach (var camp in Live)
        {
            if (camp == null || !camp.IsStanding) continue;
            float d = Vector2.Distance(from, camp.ClosestPoint(from));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = camp;
            }
        }
        return best;
    }

    private void Update()
    {
        int frame = (int)(Time.time * 6f) + (State.zoneXY.x * 7 + State.zoneXY.y * 13 & 15);
        if (bannerFrames != null && bannerFrames.Count > 0) banner.sprite = bannerFrames[frame % bannerFrames.Count];
        if (fireFrames != null && fireFrames.Count > 0) fire.sprite = fireFrames[frame % fireFrames.Count];
        if (shake > 0)
        {
            // Rattle the whole camp a little when hit.
            shake -= Time.deltaTime;
            Vector3 corner = controller.Grid.CellToWorld(State.cell);
            Vector2 jitter = shake > 0 ? Random.insideUnitCircle * 0.05f : Vector2.zero;
            transform.position = new Vector3(corner.x + jitter.x, corner.y + jitter.y, 0);
        }

        bool hurt = health < maxHealth;
        var grid = controller.Grid;
        bool hovered = new RectInt(State.cell, Vector2Int.one * CampState.Size).Contains(grid.WorldToCell(SettlementGrid.MouseWorld()));
        barBack.enabled = barFill.enabled = hurt || hovered;
        barFill.transform.localScale = new Vector3(barBack.transform.localScale.x * health / maxHealth, barFill.transform.localScale.y, 1);
        string age = controller.Settlement.Database.GetAge(Tier)?.displayName ?? "";
        label.text = hovered
            ? $"<color=#ff9a8a>Raider camp</color>\n<size=75%>{age} raiders{(Chief != null ? $", led by <color=#ff8a70>{Chief.definition.displayName}</color>" : "")}. Select troops and right-click it to burn it down.</size>"
            : "";

        KeepGuards();
    }

    /// <summary>Keep a few raiders posted at the camp (lost ones come back slowly).</summary>
    private void KeepGuards()
    {
        if (chief != null && !chief.IsAlive)
        {
            State.bossDefeated = true; // stays dead until raiders move back in
            chief = null;
        }
        if (Time.time < nextGuardCheck)
        {
            return;
        }
        if (chief == null && !State.bossDefeated && HasChief)
        {
            var boss = PickChief();
            if (boss != null && boss.prefab != null)
            {
                var e = controller.SpawnUnit(boss.prefab, Centre + new Vector3(0.6f, -1.8f, 0)).GetComponent<Enemy>();
                if (e != null)
                {
                    e.StartGuard(Centre, GuardRadius);
                    chief = e;
                }
            }
        }
        guards.RemoveAll(g => g == null || !g.IsAlive);
        bool firstFill = nextGuardCheck == 0f;
        nextGuardCheck = Time.time + 45f;
        int wanted = guardCount - guards.Count;
        for (int i = 0; i < wanted && (firstFill || i < 1); i++)
        {
            var unit = PickGuard();
            if (unit == null || unit.prefab == null) break;
            Vector2 offset = Random.insideUnitCircle * 2.5f;
            var go = controller.SpawnUnit(unit.prefab, Centre + new Vector3(offset.x, offset.y, 0));
            var enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.StartGuard(Centre, GuardRadius);
                guards.Add(enemy);
            }
        }
    }

    /// <summary>A chief of the camp's age (or the latest age that has one).</summary>
    private UnitDefinition PickChief()
    {
        var db = controller.Settlement.Database;
        for (int i = Tier; i >= 0; i--)
        {
            var age = db.GetAge(i);
            if (age != null && age.bosses.Count > 0)
            {
                uint h = (uint)(State.cell.x * 83492791) ^ (uint)(State.cell.y * 2971215073u);
                return age.bosses[(int)(h % (uint)age.bosses.Count)];
            }
        }
        return null;
    }

    private UnitDefinition PickGuard()
    {
        var pool = new List<UnitDefinition>();
        var db = controller.Settlement.Database;
        for (int i = 0; i <= Tier; i++)
        {
            var age = db.GetAge(i);
            if (age == null) continue;
            foreach (var unit in age.enemyUnits)
            {
                // Mostly the camp's own age.
                if (unit != null && (i == Tier || Random.value < 0.35f)) pool.Add(unit);
            }
        }
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    /// <summary>Burnt down: the guards scatter.</summary>
    public void Remove()
    {
        foreach (var guard in guards)
        {
            if (guard != null) Destroy(guard.gameObject);
        }
        // The chief doesn't run: it stays by the ashes until someone kills it (and takes its gear).
        guards.Clear();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Live.Remove(this);
    }
}
