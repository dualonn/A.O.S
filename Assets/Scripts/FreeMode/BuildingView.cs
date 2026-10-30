using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows one settlement building in the free mode scene: its sprite (scaffolded while
/// building, darkened when ruined), a progress/health bar, and a status label.
/// Buildings that train units keep their troops alive here while the scene is open.
/// </summary>
public class BuildingView : MonoBehaviour
{
    public BuildingState State { get; private set; }
    public BuildingDefinition Definition { get; private set; }

    private FreeModeController controller;
    private SpriteRenderer body;
    private SpriteRenderer scaffold;
    private SpriteRenderer barBack;
    private SpriteRenderer barFill;
    private SpriteRenderer selectionOutline;
    private TextMeshPro label;
    private Color baseColor = Color.white;
    private bool selected;
    private bool hovered;
    private readonly List<Troop> troops = new List<Troop>();
    private float trainTimer;

    /// <summary>Sorting order shared by building sprites and villagers; depth within it comes from z.</summary>
    public const int SortingOrder = -6;

    /// <summary>Things lower on screen get a nearer z, so they draw in front.</summary>
    public static float DepthFor(float worldY) => worldY * 0.01f;

    private const float Margin = 0.06f;
    private static readonly Color RuinedTint = new Color(0.42f, 0.36f, 0.33f);

    public void Init(FreeModeController owner, BuildingState state, BuildingDefinition def)
    {
        controller = owner;
        State = state;
        Definition = def;
        name = $"{def.displayName} #{state.instanceId}";

        var grid = owner.Grid;
        Vector2 size = (Vector2)def.size * grid.cellSize;
        Vector3 corner = grid.CellToWorld(state.cell);
        transform.position = new Vector3(corner.x, corner.y, DepthFor(corner.y));

        selectionOutline = MakeQuad("Selection", new Vector2(-Margin, -Margin), size + Vector2.one * Margin * 2,
            new Color(1f, 0.9f, 0.2f, 0.35f), -8);
        selectionOutline.enabled = false;

        if (def.prefab != null)
        {
            var visual = Instantiate(def.prefab, transform);
            visual.transform.position = grid.AreaCenter(state.cell, def.size);
        }
        else if (def.icon != null)
        {
            body = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            body.sprite = def.icon;
            // Fit the sprite to the footprint whatever its pixels-per-unit.
            Vector2 spriteSize = def.icon.bounds.size;
            body.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1);
            body.transform.localPosition = new Vector3(
                -def.icon.bounds.min.x * body.transform.localScale.x,
                -def.icon.bounds.min.y * body.transform.localScale.y, 0);
            body.sortingOrder = SortingOrder;
        }
        else
        {
            baseColor = ColorFor(def.id);
            body = MakeQuad("Body", Vector2.one * Margin, size - Vector2.one * Margin * 2, baseColor, SortingOrder);
        }

        var art = ArtLibrary.Load();
        if (art != null && art.scaffold != null)
        {
            scaffold = new GameObject("Scaffold").AddComponent<SpriteRenderer>();
            scaffold.transform.SetParent(transform, false);
            scaffold.sprite = art.scaffold;
            Vector2 s = art.scaffold.bounds.size;
            scaffold.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1);
            scaffold.sortingOrder = -5;
        }

        float barWidth = Mathf.Min(size.x - 0.3f, 1.6f);
        float barX = (size.x - barWidth) / 2;
        barBack = MakeQuad("BarBack", new Vector2(barX, size.y + 0.05f), new Vector2(barWidth, 0.16f), new Color(0, 0, 0, 0.6f), 6);
        barFill = MakeQuad("BarFill", new Vector2(barX, size.y + 0.05f), new Vector2(barWidth, 0.16f), Color.green, 7);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(size.x / 2, size.y + 0.55f, 0);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4f;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(Mathf.Max(4, size.x + 2), 1.5f);
        label.sortingOrder = 8;

        Refresh();
    }

    public bool Contains(Vector2Int cell) => new RectInt(State.cell, Definition.size).Contains(cell);

    /// <summary>Brief wobble when hit by a raider.</summary>
    public void Shake()
    {
        shakeTime = 0.18f;
    }

    private float shakeTime;
    private Vector3 bodyRestPosition;
    private bool bodyRestKnown;

    private void UpdateShake()
    {
        if (body == null)
        {
            return;
        }
        if (!bodyRestKnown)
        {
            bodyRestPosition = body.transform.localPosition;
            bodyRestKnown = true;
        }
        if (shakeTime > 0)
        {
            shakeTime -= Time.deltaTime;
            float strength = 0.06f * Mathf.Clamp01(shakeTime / 0.18f);
            body.transform.localPosition = bodyRestPosition + (Vector3)(Random.insideUnitCircle * strength);
        }
        else
        {
            body.transform.localPosition = bodyRestPosition;
        }
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        selectionOutline.enabled = isSelected;
    }

    private void Update()
    {
        hovered = !controller.Placer.IsPlacing && controller.Grid != null &&
                  Contains(controller.Grid.WorldToCell(SettlementGrid.MouseWorld()));
        Refresh();
        UpdateShake();
        TrainTroops();
    }

    private void Refresh()
    {
        bool built = State.IsBuilt;
        bool ruined = built && State.health <= 0;
        var stats = Settlement.StatsOf(Definition, State.upgradeLevel);
        float healthShare = State.health / stats.maxHealth;

        if (body != null)
        {
            Color c = ruined ? baseColor * RuinedTint : baseColor;
            c.a = built ? 1f : 0.45f + 0.4f * State.buildProgress;
            body.color = c;
        }
        if (scaffold != null)
        {
            scaffold.enabled = !built;
        }

        // Bar: build progress while constructing, upgrade progress while upgrading, health once damaged.
        bool damaged = built && State.health < stats.maxHealth;
        float fill = !built ? State.buildProgress : damaged ? healthShare : State.upgradeProgress;
        bool showBar = !built || damaged || State.upgrading;
        barBack.enabled = barFill.enabled = showBar;
        barFill.transform.localScale = new Vector3(barBack.transform.localScale.x * Mathf.Clamp01(fill), barFill.transform.localScale.y, 1);
        barFill.color = !built ? new Color(0.4f, 0.7f, 1f)
            : damaged ? Color.Lerp(Color.red, Color.green, healthShare)
            : new Color(1f, 0.85f, 0.3f);

        // Label: name (with upgrade level) on hover/selection, plus any status worth knowing.
        string status = !built ? "<color=#9cf>Building</color>"
            : ruined ? "<color=#ff7070>Ruined</color>"
            : State.upgrading ? "<color=#fd6>Upgrading</color>"
            : "";
        string name = State.upgradeLevel > 0 ? $"{Definition.displayName} <size=75%>Lv {State.upgradeLevel + 1}</size>" : Definition.displayName;
        bool showName = selected || hovered;
        label.text = showName && status.Length > 0 ? $"{name}\n<size=75%>{status}</size>"
            : showName ? name
            : status.Length > 0 ? $"<size=75%>{status}</size>"
            : "";
    }

    private void TrainTroops()
    {
        troops.RemoveAll(t => t == null || !t.IsAlive);
        var stats = Settlement.StatsOf(Definition, State.upgradeLevel);
        // Keep the troops' orders and upgrades in line with this building.
        foreach (var t in troops)
        {
            if (t.Stance != State.stance)
            {
                t.SetStance(State.stance, controller.TownZone, controller.SightRange);
            }
            t.SetBuildingModifier(stats.trainedUnits);
        }
        var unit = Definition.trainsUnit;
        if (unit == null || unit.prefab == null || !controller.Settlement.IsWorking(State, Definition))
        {
            return;
        }
        if (troops.Count >= stats.maxUnits)
        {
            trainTimer = 0;
            return;
        }
        trainTimer += Time.deltaTime;
        if (trainTimer < controller.troopTrainSeconds)
        {
            return;
        }
        trainTimer = 0;

        Vector3 spawn = controller.Grid.AreaCenter(State.cell, Definition.size);
        var troop = controller.SpawnUnit(unit.prefab, spawn).GetComponent<Troop>();
        if (troop != null)
        {
            troop.SetRallyPoint(controller.GetRallyPoint(this));
            troop.SetStance(State.stance, controller.TownZone, controller.SightRange);
            troop.SetBuildingModifier(stats.trainedUnits);
            troops.Add(troop);
        }
    }

    private void OnDestroy()
    {
        foreach (var troop in troops)
        {
            if (troop != null)
            {
                Destroy(troop.gameObject);
            }
        }
    }

    private SpriteRenderer MakeQuad(string objectName, Vector2 offset, Vector2 size, Color color, int order)
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

    /// <summary>A stable, muted colour per building type, used only when there is no art.</summary>
    private static Color ColorFor(string id)
    {
        float hue = Mathf.Abs(id.GetHashCode() % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.35f, 0.75f);
    }
}
