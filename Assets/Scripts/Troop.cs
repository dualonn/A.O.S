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
            var look = GetComponent<SpriteUnitVisual>();
            tagObject.transform.localPosition = new Vector3(0, look != null ? look.barHeight + 0.5f : 1.05f, 0);
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

    private static readonly GearLook[] wornLooks = new GearLook[5];
    private static readonly Rarity[] wornRarities = new Rarity[5];

    /// <summary>Draw this hero's equipped gear on them (cheap to call often: nothing changes unless the gear did).</summary>
    public void ShowHeroGear(Settlement settlement)
    {
        if (Hero == null || settlement == null) return;
        var look = GetComponent<SpriteUnitVisual>();
        if (look == null) return;
        settlement.WornLooks(Hero, wornLooks, wornRarities);
        look.ShowGear(wornLooks, wornRarities);
    }

    public void RefreshHeroTag()
    {
        if (nameTag != null && Hero != null)
        {
            string path = Hero.heroClass != HeroClass.None ? $" {Hero.heroClass}" : "";
            nameTag.text = $"{Hero.name} <size=75%>Lv {Hero.level + 1}{path}</size>";
        }
    }

    private void LateUpdate()
    {
        // Units face left by turning round; keep the name readable.
        if (nameTag != null) nameTag.transform.rotation = Quaternion.identity;
    }

    private float nextHeroAbility;

    /// <summary>A hero's path ability, used on its own in a fight.</summary>
    protected override void UseAbility(Unit target)
    {
        if (Hero == null || Hero.heroClass == HeroClass.None || target == null || Time.time < nextHeroAbility) return;
        float distance = Vector2.Distance(transform.position, target.transform.position);
        switch (Hero.heroClass)
        {
            case HeroClass.Warrior:
            {
                if (distance > attackRange + 0.8f) return;
                foreach (var u in Living)
                    if (u != null && u.Team != Team && CanHit(u) && Vector2.Distance(u.transform.position, transform.position) <= 1.9f)
                        u.TakeDamage(Mathf.RoundToInt(attackPower * 0.7f), this);
                Puff.Spawn(transform.position + Vector3.up * 0.3f, 0.9f, new Color(1f, 1f, 1f, 0.7f), 7, 0.35f);
                nextHeroAbility = Time.time + 8f;
                break;
            }
            case HeroClass.Ranger:
            {
                if (!IsRanged) return;
                int shots = 0;
                foreach (var u in Living)
                {
                    if (shots >= 3) break;
                    if (u == null || u == target || u.Team == Team || !CanHit(u)) continue;
                    if (Vector2.Distance(u.transform.position, transform.position) > detectionRange) continue;
                    Projectile.Fire(definition.projectile, LaunchPoint, u, definition.projectileSpeed, attackPower, this, definition.projectileArc, definition.splashRadius);
                    shots++;
                }
                if (shots == 0) return;
                nextHeroAbility = Time.time + 9f;
                break;
            }
            case HeroClass.Mage:
            {
                if (distance > Mathf.Max(attackRange, 4f) + 0.5f) return;
                Vector3 at = target.transform.position;
                foreach (var u in Living)
                    if (u != null && u.Team != Team && u.Targetable && Vector2.Distance(u.transform.position, at) <= 1.7f)
                        u.TakeDamage(Mathf.RoundToInt(attackPower * 1.4f), this);
                Puff.Blast(at, 1.7f, true);
                nextHeroAbility = Time.time + 7f;
                break;
            }
            case HeroClass.Priest:
            {
                bool anyHurt = false;
                foreach (var u in Living)
                {
                    if (u == null || u.Team != Team || !u.IsAlive || u.health >= u.maxHealth) continue;
                    if (Vector2.Distance(u.transform.position, transform.position) > 3.5f) continue;
                    u.Heal(Mathf.RoundToInt(u.maxHealth * 0.2f));
                    anyHurt = true;
                }
                if (!anyHurt) return;
                Puff.Spawn(transform.position + Vector3.up * 0.4f, 1.2f, new Color(1f, 0.9f, 0.45f, 0.75f), 8, 0.6f);
                nextHeroAbility = Time.time + 11f;
                break;
            }
        }
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
        if (Dungeon != null) return;   // the dungeon party moves together
        destination.z = transform.position.z;
        rallyPoint = destination;
        hasMoveOrder = true;
        HasOrder = true;
        TargetCamp = null;
    }

    // ---------- Dungeons ----------

    /// <summary>The dungeon this troop is down in (null = on the surface).</summary>
    public DungeonRun Dungeon { get; private set; }

    /// <summary>Go down into a dungeon level, appearing at 'at'.</summary>
    public void EnterDungeon(DungeonRun run, Vector3 at)
    {
        Dungeon = run;
        TargetCamp = null;
        campOrder = false;
        hasMoveOrder = false;
        HasOrder = true;
        at.z = transform.position.z;
        transform.position = at;
        rallyPoint = at;
    }

    /// <summary>Where the dungeon party gathers next (the troop still fights on the way).</summary>
    public void SetDungeonRally(Vector3 point)
    {
        point.z = transform.position.z;
        rallyPoint = point;
        hasMoveOrder = false;
    }

    /// <summary>Climb back out at 'at' and head home.</summary>
    public void LeaveDungeon(Vector3 at, Vector3 homeRally)
    {
        Dungeon = null;
        at.z = transform.position.z;
        transform.position = at;
        ReturnHome(homeRally);
    }

    /// <summary>The raider camp this troop is set on burning down (null = none).</summary>
    public CampView TargetCamp { get; private set; }

    /// <summary>
    /// Burn down this camp. The troop sticks to it until it falls: it only fights raiders that
    /// get right up to it, rather than chasing every guard the camp sends out.
    /// </summary>
    public void OrderAttackCamp(CampView camp)
    {
        TargetCamp = camp;
        HasOrder = true;
        campOrder = true;
        hasMoveOrder = false;
    }

    private bool campOrder;

    /// <summary>Drop the player's order: go back to the home building's rally point.</summary>
    public void ReturnHome(Vector3 homeRally)
    {
        if (Dungeon != null) return;   // busy underground
        HasOrder = false;
        hasMoveOrder = false;
        TargetCamp = null;
        SetRallyPoint(homeRally);
    }

    // While walking to a move order, ignore enemies.
    protected override bool ShouldEngage() => !hasMoveOrder;

    /// <summary>Troops on Attack go for raider camps within this many tiles once no raiders are about.</summary>
    public const float CampHuntRange = 60f;

    // ---------- Medics ----------

    public bool IsHealer => definition != null && definition.healer;

    /// <summary>How close a medic needs to be to bandage someone.</summary>
    private const float HealReach = 1.4f;

    /// <summary>
    /// A medic's work: go to the most badly hurt troop nearby and bandage them (each go heals the
    /// medic's attack power); with nobody hurt, keep close to the nearest troops. True while busy.
    /// </summary>
    private bool TendWounded()
    {
        Troop patient = null, company = null;
        float worst = 0.999f, nearest = 20f;
        float range = definition.healRange;
        // At home, a medic looks after the whole settlement; out with a party (or sent somewhere,
        // or underground) only the troops around it.
        bool atHome = Dungeon == null && !HasOrder && defendArea != null && InDefendArea(transform.position);
        foreach (var u in Living)
        {
            if (!(u is Troop t) || t == this || !t.IsAlive || t.Dungeon != Dungeon) continue;
            float d = Vector2.Distance(t.transform.position, transform.position);
            if (!t.IsHealer && d < nearest) { nearest = d; company = t; }
            float share = t.health / (float)Mathf.Max(1, t.maxHealth);
            if (share >= 0.999f || (t == unreachable && Time.time < unreachableUntil)) continue;
            if (d > range && !(atHome && InDefendArea(t.transform.position))) continue;
            // Spread out: leave someone another medic is already seeing to, unless they're far worse off.
            if (t != tending && TendedByAnother(t)) share += 0.3f;
            // And finish with someone before going off to another who's only a little worse.
            if (t == tending) share -= 0.2f;
            if (share < worst)
            {
                worst = share;
                patient = t;
            }
        }
        if (patient != tending)
        {
            tending = patient;
            crowded = false;
            progressCheckAt = 0;
        }
        if (patient != null)
        {
            Vector3 at = patient.transform.position;
            float gap = Vector2.Distance(at, transform.position);
            // Units keep a little personal space; in a crowd, a few steps off is close enough.
            if (gap > (crowded ? CrowdedReach : HealReach))
            {
                if (!Stuck(gap))
                {
                    MoveTowards(at, moveSpeed);
                    return true;
                }
                if (gap > CrowdedReach)
                {
                    // Can't get there (water, a building in the way): leave them for a while.
                    unreachable = patient;
                    unreachableUntil = Time.time + 8f;
                    tending = null;
                    return TendWounded();
                }
                crowded = true;
            }
            progressCheckAt = 0;
            SetMoving(false);
            Face(at);
            if (CanSwing)
            {
                var p = patient;
                int amount = Mathf.Max(1, attackPower);
                StartSwingAt(() =>
                {
                    if (p == null || !p.IsAlive) return;
                    p.Heal(amount);
                    RingPulse.Spawn(p.transform.position, 0.45f, new Color(0.45f, 1f, 0.5f, 0.85f));
                });
            }
            return true;
        }
        // Nobody hurt: stay with the troops (out in the field, or down a dungeon), a step behind.
        // Once close, wait there: walking back to the rally point would just pull us away again.
        if (company != null && (Dungeon != null || company.HasOrder || company.TargetCamp != null || Stance == TroopStance.Attack))
        {
            if (nearest > 2.5f)
                MoveTowards(company.transform.position + (company.transform.position - transform.position).normalized * -1.5f, moveSpeed);
            else
                SetMoving(false);
            return true;
        }
        return false;
    }

    /// <summary>How close a medic gets to bandage someone stood in a crowd it can't push through.</summary>
    private const float CrowdedReach = 3f;

    private Troop tending, unreachable;
    private bool crowded;
    private float progressCheckAt, progressGap, unreachableUntil;

    private bool TendedByAnother(Troop patient)
    {
        foreach (var u in Living)
            if (u is Troop m && m != this && m.IsHealer && m.IsAlive && m.tending == patient) return true;
        return false;
    }

    /// <summary>No closer to the patient than a couple of seconds ago?</summary>
    private bool Stuck(float gap)
    {
        if (Time.time < progressCheckAt) return false;
        bool stuck = progressCheckAt > 0 && gap > progressGap - 0.3f;
        progressGap = gap;
        progressCheckAt = Time.time + 2f;
        return stuck;
    }

    /// <summary>Troops you've sent somewhere take on enemies within this many tiles of themselves.</summary>
    public const float OrderFightRange = 10f;

    /// <summary>Any troop this close to a camp, with nothing left to fight, sets about burning it.</summary>
    public const float CampNearRange = 9f;

    private float raidCheckAt;
    private bool homeRaided;

    /// <summary>
    /// Is a raid on its way to (or in) this troop's home settlement? Any raider within raiding
    /// range of home counts, from the moment it sets out. (Checked once a second.)
    /// </summary>
    private bool HomeUnderRaid()
    {
        if (Time.time < raidCheckAt) return homeRaided;
        raidCheckAt = Time.time + 1f;
        homeRaided = false;
        if (defendArea == null) return false;
        float reach = Settlement.Instance != null ? Settlement.Instance.Database.campRaidRange : 110f;
        foreach (var u in Living)
        {
            if (u is Enemy e && e.IsAlive && e.IsRaider && defendArea.DistanceTo(e.transform.position) <= reach)
            {
                homeRaided = true;
                break;
            }
        }
        return homeRaided;
    }

    protected override void Idle()
    {
        if (IsHealer && TendWounded()) return;
        if (Dungeon != null)
        {
            MoveTowards(rallyPoint, moveSpeed);
            return;
        }
        // (A burnt camp is destroyed, so it can also read as null here.)
        if (HasOrder && !hasMoveOrder && (TargetCamp == null || !TargetCamp.IsStanding) && campOrder)
        {
            campOrder = false;
            // Burnt down: the job is done, head back.
            TargetCamp = null;
            HasOrder = false; // back to its usual rally point
        }
        if (TargetCamp != null && TargetCamp.IsStanding && !IsHealer)
        {
            // Raiders at home come first for a camp we took on ourselves (one you ordered stays).
            // (A camp we're already standing at gets burnt regardless.)
            if (!campOrder && HomeUnderRaid() && !CampClose(TargetCamp)) TargetCamp = null;
            else
            {
                AttackCamp(TargetCamp);
                return;
            }
        }
        // With nobody left to fight and a camp right here (or, on Attack, anywhere in reach): burn it
        // down and stick to it, unless you've sent us elsewhere. While home is raided, Attack troops
        // still burn a camp right beside them, but don't go hunting further afield.
        if (!IsHealer && !hasMoveOrder)
        {
            bool raided = HomeUnderRaid();
            float range = !raided && hasDefendZone && Stance == TroopStance.Attack ? CampHuntRange : CampNearRange;
            if (raided && Stance != TroopStance.Attack) range = 0f;
            var camp = CampView.Nearest(transform.position, range);
            if (camp != null)
            {
                TargetCamp = camp;
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

    private bool CampClose(CampView camp) =>
        camp != null && Vector2.Distance(transform.position, camp.ClosestPoint(transform.position)) <= CampNearRange;

    /// <summary>How close a raider must be before a troop set on a camp turns to fight it.</summary>
    private const float CampGuardReach = 1.3f;

    protected override Unit SelectTarget(List<Unit> candidates)
    {
        if (IsHealer) return null;   // medics don't fight
        // Underground: fight what's in this room, not through the walls.
        if (Dungeon != null)
        {
            var me = transform.position;
            candidates = candidates.FindAll(u => Vector2.Distance(u.transform.position, me) < 9f && Mathf.Abs(u.transform.position.x - me.x) < 9f);
            return candidates.Count == 0 ? null : base.SelectTarget(candidates);
        }
        // On Attack, a camp we come close to gets burnt down, whatever raiders are doing elsewhere.
        if (Stance == TroopStance.Attack && !hasMoveOrder && (TargetCamp == null || !TargetCamp.IsStanding))
        {
            var near = CampView.Nearest(transform.position, CampNearRange);
            if (near != null) TargetCamp = near;
        }
        // Set on a camp: only fight raiders right beside us, never chase.
        if (TargetCamp != null && TargetCamp.IsStanding)
        {
            float reach = CampGuardReach + personalSpace;
            candidates = candidates.FindAll(u => Vector2.Distance(u.transform.position, transform.position) <= reach);
            return candidates.Count == 0 ? null : base.SelectTarget(candidates);
        }

        // Sent somewhere (on your order): fight what comes near, don't chase things across the map.
        if (HasOrder)
        {
            var me = transform.position;
            candidates = candidates.FindAll(u => Vector2.Distance(u.transform.position, me) <= OrderFightRange);
            if (candidates.Count == 0) return null;
        }

        // Defending: ignore raiders outside the town; return home once they leave it.
        // (Troops you've sent somewhere fight whatever they meet there.)
        if (hasDefendZone && Stance == TroopStance.Defend && !HasOrder)
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
