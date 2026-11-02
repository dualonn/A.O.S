using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A player unit. Fights enemies near it, can be given move orders, and returns to its
/// rally point (where it spawned or was last sent) when there is nothing to fight.
/// </summary>
public class Troop : Unit
{
    [Tooltip("Move orders are walked at moveSpeed times this.")]
    public float moveOrderSpeedMultiplier = 1.5f;

    public override Team Team => Team.Player;
    public bool IsHovered { get; private set; }

    /// <summary>The building that trained this troop (free mode).</summary>
    public BuildingView Home { get; set; }

    /// <summary>Sent somewhere by the player, and holding there.</summary>
    public bool HasOrder { get; private set; }

    private SpriteRenderer selectionRing;

    /// <summary>Set when this troop is a hero (named, saved, wears gear).</summary>
    public HeroState Hero { get; private set; }

    private TextMeshPro nameTag;
    private SpriteRenderer heroGlow;

    /// <summary>Turn this troop into the given hero: name tag and a gold ring under its feet.</summary>
    public void MakeHero(HeroState hero)
    {
        Hero = hero;
        if (heroGlow == null)
        {
            heroGlow = new GameObject("HeroGlow").AddComponent<SpriteRenderer>();
            heroGlow.transform.SetParent(transform, false);
            heroGlow.sprite = SimpleSprites.Ring;
            heroGlow.color = new Color(1f, 0.78f, 0.25f, 0.65f);
            heroGlow.sortingOrder = BuildingView.SortingOrder - 1;
            heroGlow.transform.localPosition = new Vector3(0, 0.02f, 0);
            heroGlow.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
        }
        if (nameTag == null)
        {
            var tagObject = new GameObject("HeroName");
            tagObject.transform.SetParent(transform, false);
            tagObject.transform.localPosition = new Vector3(0, 1.05f, 0);
            nameTag = tagObject.AddComponent<TextMeshPro>();
            nameTag.alignment = TextAlignmentOptions.Center;
            nameTag.fontSize = 2.4f;
            nameTag.outlineWidth = 0.25f;
            nameTag.outlineColor = new Color32(0, 0, 0, 220);
            nameTag.color = new Color(1f, 0.85f, 0.4f);
            nameTag.rectTransform.sizeDelta = new Vector2(6, 0.8f);
            nameTag.sortingOrder = 8;
        }
        RefreshHeroTag();
    }

    public void RefreshHeroTag()
    {
        if (nameTag != null && Hero != null)
        {
            nameTag.text = $"{Hero.name} <size=75%>Lv {Hero.level + 1}</size>";
        }
    }

    private void LateUpdate()
    {
        // Units face left by turning round; keep the name readable.
        if (nameTag != null) nameTag.transform.rotation = Quaternion.identity;
    }

    protected override void OnKilled(Unit victim)
    {
        if (Hero != null && Settlement.Instance != null && victim != null)
        {
            Settlement.Instance.GainXp(Hero, 4f + Settlement.UnitPower(victim.definition) * 6f);
            RefreshHeroTag();
        }
    }

    /// <summary>Show or hide the ring under this troop's feet.</summary>
    public void SetSelected(bool selected)
    {
        if (selected && selectionRing == null)
        {
            selectionRing = new GameObject("SelectionRing").AddComponent<SpriteRenderer>();
            selectionRing.transform.SetParent(transform, false);
            selectionRing.sprite = SimpleSprites.Ring;
            selectionRing.color = new Color(1f, 0.92f, 0.3f, 0.9f);
            selectionRing.sortingOrder = BuildingView.SortingOrder - 1; // always under the body
            selectionRing.transform.localPosition = new Vector3(0, 0.02f, 0f);
            selectionRing.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        }
        if (selectionRing != null)
        {
            selectionRing.enabled = selected;
        }
    }

    /// <summary>Free mode: Defend only fights inside the town zone; Attack fights anywhere.</summary>
    public TroopStance Stance { get; private set; } = TroopStance.Attack;

    private Vector3 rallyPoint;
    private bool hasMoveOrder;
    private Rect defendZone;
    private bool hasDefendZone;
    private Settlement.SettlementZone defendArea;
    private float alertRange;

    /// <summary>
    /// Set how this troop behaves (free mode). 'townZone' is the area Defend troops hold;
    /// 'sightRange' is how far they spot raiders.
    /// </summary>
    public void SetStance(TroopStance stance, Rect townZone, float sightRange)
    {
        Stance = stance;
        defendZone = townZone;
        hasDefendZone = true;
        detectionRange = sightRange;
    }

    /// <summary>Update the area a defending troop holds (its settlement grows or changes).</summary>
    public void SetDefendZone(Rect townZone)
    {
        defendZone = townZone;
    }

    /// <summary>
    /// Free mode: defend this settlement, going after any raider within 'range' tiles of its
    /// buildings (so the alarm goes up at the edge of town, not when raiders reach the troops).
    /// </summary>
    public void SetDefendArea(Settlement.SettlementZone settlement, float range)
    {
        defendArea = settlement;
        alertRange = range;
    }

    private bool InDefendArea(Vector3 p) =>
        defendArea != null ? defendArea.DistanceTo(p) <= alertRange : defendZone.Contains(p);

    protected override void Awake()
    {
        base.Awake();
        rallyPoint = transform.position;
    }

    /// <summary>Where the troop returns to when idle. Unlike a move order, it still fights on the way.</summary>
    public void SetRallyPoint(Vector3 point)
    {
        point.z = transform.position.z;
        rallyPoint = point;
    }

    public void OrderMoveTo(Vector3 destination)
    {
        destination.z = transform.position.z;
        rallyPoint = destination;
        hasMoveOrder = true;
        HasOrder = true;
    }

    /// <summary>Drop the player's order: go back to the home building's rally point.</summary>
    public void ReturnHome(Vector3 homeRally)
    {
        HasOrder = false;
        hasMoveOrder = false;
        SetRallyPoint(homeRally);
    }

    // While walking to a move order, ignore enemies.
    protected override bool ShouldEngage() => !hasMoveOrder;

    /// <summary>Troops on Attack go for raider camps within this many tiles once no raiders are about.</summary>
    public const float CampHuntRange = 60f;

    protected override void Idle()
    {
        // Advancing troops with nothing to fight go and burn down nearby raider camps.
        if (!hasMoveOrder && hasDefendZone && Stance == TroopStance.Attack)
        {
            var camp = CampView.Nearest(transform.position, CampHuntRange);
            if (camp != null)
            {
                AttackCamp(camp);
                return;
            }
        }
        float speed = hasMoveOrder ? moveSpeed * moveOrderSpeedMultiplier : moveSpeed;
        if (MoveTowards(rallyPoint, speed))
        {
            hasMoveOrder = false;
        }
    }

    private void AttackCamp(CampView camp)
    {
        Vector3 point = camp.ClosestPoint(transform.position);
        if (Vector2.Distance(transform.position, point) > attackRange * 0.6f)
        {
            MoveTowards(point, moveSpeed);
            return;
        }
        SetMoving(false);
        Face(point);
        if (CanSwing)
        {
            System.Action hit = () =>
            {
                if (camp != null) camp.TakeHit(attackPower);
            };
            StartSwingAt(IsRanged ? () => Shoot(point, hit) : hit);
        }
    }

    protected override Unit SelectTarget(List<Unit> candidates)
    {
        // Defending: ignore raiders outside the town; return home once they leave it.
        if (hasDefendZone && Stance == TroopStance.Defend)
        {
            candidates = candidates.FindAll(u => InDefendArea(u.transform.position));
            if (candidates.Count == 0)
            {
                return null;
            }
        }

        return base.SelectTarget(candidates); // closest
    }

    protected override void Die()
    {
        if (Hero != null && Settlement.Instance != null)
        {
            Settlement.Instance.HeroFell(Hero);
        }
        base.Die();
    }

    public override void SetHovered(bool hovered)
    {
        IsHovered = hovered;
    }
}
