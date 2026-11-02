using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>What a boss does besides hitting things.</summary>
public enum BossAbility { None, Slam, WarCry, Summon }

/// <summary>
/// A named raider chief: big, tough, with a signature move (a ground slam, a war cry that
/// drives the raiders on, or calling in more raiders). Leads some raids and guards the stronger
/// camps. Always drops rare or better gear when it falls.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class Boss : MonoBehaviour
{
    public static readonly List<Boss> Live = new List<Boss>();

    private Enemy enemy;
    private float nextAbility;
    private bool fallen;
    private TextMeshPro nameTag;

    public string Title => enemy.definition != null ? enemy.definition.displayName : "Raider chief";
    public bool IsAlive => enemy != null && enemy.IsAlive;
    public Enemy Unit => enemy;

    private void Awake() => enemy = GetComponent<Enemy>();
    private void OnEnable() => Live.Add(this);
    private void OnDisable() => Live.Remove(this);

    private void Start()
    {
        nextAbility = Time.time + 4f;
        var look = GetComponent<SpriteUnitVisual>();
        float height = look != null ? look.barHeight : 1f;
        var tagObject = new GameObject("BossName");
        tagObject.transform.SetParent(transform, false);
        tagObject.transform.localPosition = new Vector3(0, height + 0.2f, 0);
        var prefab = enemy.definition != null ? enemy.definition.prefab : null;
        float scale = Mathf.Max(0.01f, prefab != null ? prefab.transform.localScale.x : 1f); // undo the boss's own size
        tagObject.transform.localScale = Vector3.one / scale; // same text size as hero names
        nameTag = tagObject.AddComponent<TextMeshPro>();
        nameTag.alignment = TextAlignmentOptions.Center;
        nameTag.fontSize = 2.6f;
        nameTag.fontStyle = FontStyles.Bold;
        nameTag.outlineWidth = 0.25f;
        nameTag.outlineColor = new Color32(0, 0, 0, 220);
        nameTag.color = new Color(1f, 0.45f, 0.35f);
        nameTag.rectTransform.sizeDelta = new Vector2(8, 0.8f);
        nameTag.sortingOrder = 8;
        nameTag.text = Title;
    }

    private void Update()
    {
        if (enemy == null)
        {
            return;
        }
        if (!enemy.IsAlive)
        {
            if (!fallen)
            {
                fallen = true;
                Fallen();
            }
            return;
        }
        var def = enemy.definition;
        var target = enemy.currentTarget;
        if (def == null || target == null || Time.time < nextAbility)
        {
            return;
        }
        float distance = Vector2.Distance(target.transform.position, transform.position);
        switch (def.bossAbility)
        {
            case BossAbility.Slam:
                if (distance > def.abilityRadius) return;
                Slam(def);
                break;
            case BossAbility.WarCry:
                if (distance > 6f) return;
                WarCry(def);
                break;
            case BossAbility.Summon:
                if (distance > 8f) return;
                Summon(def);
                break;
            default:
                return;
        }
        nextAbility = Time.time + def.abilityCooldown;
    }

    private void LateUpdate()
    {
        if (nameTag != null) nameTag.transform.rotation = Quaternion.identity; // readable when facing left
    }

    /// <summary>Pound the ground: hurts and throws back every enemy close by.</summary>
    private void Slam(UnitDefinition def)
    {
        int damage = Mathf.RoundToInt(enemy.attackPower * def.abilityPower);
        var hit = new List<Unit>();
        foreach (var u in global::Unit.Living)
        {
            if (u != null && u.IsAlive && u.Team != enemy.Team && Vector2.Distance(u.transform.position, transform.position) <= def.abilityRadius)
            {
                hit.Add(u);
            }
        }
        foreach (var u in hit) u.TakeDamage(damage, enemy);
        RingPulse.Spawn(transform.position, def.abilityRadius, new Color(0.65f, 0.5f, 0.3f, 0.8f));
    }

    /// <summary>A roar that drives nearby raiders on: harder hits and faster feet for a while.</summary>
    private void WarCry(UnitDefinition def)
    {
        foreach (var u in global::Unit.Living)
        {
            if (u is Enemy e && e.IsAlive && Vector2.Distance(u.transform.position, transform.position) <= 7f)
            {
                e.Rally(def.abilityPower, 8f);
            }
        }
        RingPulse.Spawn(transform.position, 7f, new Color(0.95f, 0.25f, 0.2f, 0.7f));
    }

    /// <summary>Call more raiders to the fight.</summary>
    private void Summon(UnitDefinition def)
    {
        var controller = FreeModeController.Instance;
        if (def.summons == null || def.summons.prefab == null || controller == null)
        {
            return;
        }
        for (int i = 0; i < def.summonCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * 1.2f;
            var spawn = transform.position + new Vector3(offset.x, offset.y, 0);
            if (!controller.Settlement.Map.IsWalkable(spawn)) spawn = transform.position;
            var go = controller.SpawnUnit(def.summons.prefab, spawn);
            var e = go != null ? go.GetComponent<Enemy>() : null;
            if (e != null) e.JoinSameJob(enemy);
        }
        RingPulse.Spawn(transform.position, 2f, new Color(0.25f, 0.2f, 0.2f, 0.8f));
    }

    private void Fallen()
    {
        var settlement = Settlement.Instance;
        FreeModeController.Instance?.ShowMessage($"{Title} has fallen!");
        if (settlement == null || enemy.definition == null)
        {
            return;
        }
        // Always something good, a bit above your level.
        settlement.GainLoot(0.4f, 0.6f, $"on {Title}", Rarity.Rare);
    }
}

/// <summary>A ring that swells and fades: shows where an ability landed.</summary>
public class RingPulse : MonoBehaviour
{
    private SpriteRenderer ring;
    private float radius, start;
    private Color color;
    private const float Duration = 0.45f;

    public static void Spawn(Vector3 at, float radius, Color color)
    {
        var go = new GameObject("RingPulse");
        go.transform.position = new Vector3(at.x, at.y, BuildingView.DepthFor(at.y) - 0.001f);
        var pulse = go.AddComponent<RingPulse>();
        pulse.ring = go.AddComponent<SpriteRenderer>();
        pulse.ring.sprite = SimpleSprites.Ring;
        pulse.ring.sortingOrder = BuildingView.SortingOrder - 1;
        pulse.radius = radius;
        pulse.color = color;
        pulse.start = Time.time;
    }

    private void Update()
    {
        float t = (Time.time - start) / Duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        // The ring sprite is a flat ellipse 0.8 units across at scale 1.
        float size = radius / 0.4f * Mathf.Lerp(0.3f, 1f, t);
        transform.localScale = new Vector3(size, size, 1f);
        ring.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
    }
}
