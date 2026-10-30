using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows one settlement building in the free mode scene: footprint, label, progress/health bar.
/// Buildings that train units keep their troops alive here while the scene is open.
/// </summary>
public class BuildingView : MonoBehaviour
{
    public BuildingState State { get; private set; }
    public BuildingDefinition Definition { get; private set; }

    private FreeModeController controller;
    private SpriteRenderer body;
    private SpriteRenderer barBack;
    private SpriteRenderer barFill;
    private SpriteRenderer selectionOutline;
    private TextMeshPro label;
    private Color baseColor;
    private readonly List<Troop> troops = new List<Troop>();
    private float trainTimer;

    private const float Margin = 0.08f;

    public void Init(FreeModeController owner, BuildingState state, BuildingDefinition def)
    {
        controller = owner;
        State = state;
        Definition = def;
        name = $"{def.displayName} #{state.instanceId}";

        var grid = owner.Grid;
        Vector2 size = (Vector2)def.size * grid.cellSize;
        transform.position = grid.CellToWorld(state.cell);

        selectionOutline = MakeQuad("Selection", new Vector2(-Margin, -Margin), size + Vector2.one * Margin * 2, Color.yellow, 0);
        selectionOutline.enabled = false;

        if (def.prefab != null)
        {
            var visual = Instantiate(def.prefab, transform);
            visual.transform.position = grid.AreaCenter(state.cell, def.size);
        }
        else
        {
            baseColor = ColorFor(def.id);
            body = MakeQuad("Body", Vector2.one * Margin, size - Vector2.one * Margin * 2, baseColor, 1);
        }

        float barWidth = size.x - 0.4f;
        barBack = MakeQuad("BarBack", new Vector2(0.2f, 0.2f), new Vector2(barWidth, 0.18f), new Color(0, 0, 0, 0.6f), 2);
        barFill = MakeQuad("BarFill", new Vector2(0.2f, 0.2f), new Vector2(barWidth, 0.18f), Color.green, 3);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(size.x / 2, size.y / 2 + 0.15f, 0);
        label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 4.5f;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(0, 0, 0, 200);
        label.rectTransform.sizeDelta = new Vector2(size.x, size.y);
        label.sortingOrder = 4;

        Refresh();
    }

    public bool Contains(Vector2Int cell) => new RectInt(State.cell, Definition.size).Contains(cell);

    public void SetSelected(bool selected) => selectionOutline.enabled = selected;

    private void Update()
    {
        Refresh();
        TrainTroops();
    }

    private void Refresh()
    {
        bool built = State.IsBuilt;
        bool ruined = built && State.health <= 0;
        float healthShare = State.health / Definition.maxHealth;

        if (body != null)
        {
            Color c = baseColor;
            if (!built)
            {
                c.a = 0.35f;
            }
            else if (ruined)
            {
                c = Color.Lerp(baseColor, new Color(0.25f, 0.1f, 0.1f), 0.75f);
            }
            body.color = c;
        }

        // Bar shows build progress while constructing, health afterwards (hidden at full health).
        float fill = built ? healthShare : State.buildProgress;
        bool showBar = !built || healthShare < 0.999f;
        barBack.enabled = barFill.enabled = showBar;
        barFill.transform.localScale = new Vector3(barBack.transform.localScale.x * Mathf.Clamp01(fill), barFill.transform.localScale.y, 1);
        barFill.color = !built ? new Color(0.4f, 0.7f, 1f) : Color.Lerp(Color.red, Color.green, healthShare);

        string status = !built ? $"\n<size=70%>Building {State.buildProgress:P0}</size>"
            : ruined ? "\n<size=70%><color=#ff7070>Ruined</color></size>"
            : "";
        label.text = Definition.displayName + status;
    }

    private void TrainTroops()
    {
        troops.RemoveAll(t => t == null || !t.IsAlive);
        var unit = Definition.trainsUnit;
        if (unit == null || unit.prefab == null || !controller.Settlement.IsWorking(State, Definition))
        {
            return;
        }
        if (troops.Count >= Definition.maxUnits)
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
        var troop = Instantiate(unit.prefab, spawn, Quaternion.identity).GetComponent<Troop>();
        if (troop != null)
        {
            troop.SetRallyPoint(controller.GetRallyPoint());
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

    /// <summary>A stable, muted colour per building type until real art exists.</summary>
    private static Color ColorFor(string id)
    {
        float hue = Mathf.Abs(id.GetHashCode() % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.35f, 0.75f);
    }
}
