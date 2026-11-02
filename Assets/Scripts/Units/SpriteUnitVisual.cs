using System.Collections.Generic;
using UnityEngine;

/// <summary>What a Unit tells its visuals. Units without one fall back to their Animator.</summary>
public interface IUnitVisual
{
    void SetMoving(bool moving);

    /// <summary>Start a swing; the strike frame should appear 'hitDelay' seconds from now.</summary>
    void PlayAttack(float hitDelay);

    void PlayDeath();

    /// <summary>0..1. The bar is hidden at full health.</summary>
    void SetHealth(float share);
}

/// <summary>What kind of weapon a unit's animation is built around, so heroes only take gear that fits it.</summary>
public enum WeaponClass { Melee, Bow, Thrown, None }

/// <summary>
/// Where a hero's gear sits on one animation frame (sprite-local units, feet at 0,0). Head, body
/// and trinkets turn with the figure's lean; the weapon has its own angle.
/// </summary>
[System.Serializable]
public struct GearAnchor
{
    public Vector2 head, neck, torso, hip, shield, grip, handL;
    public float lean, weaponAngle;
    public bool shieldShown;
}

/// <summary>
/// Frame-by-frame sprite animation for units in the flat stickman style:
/// idle, walk cycle, a swing timed to land with the damage, and a fall when killed.
/// Also draws a small health bar (only when hurt) and depth-sorts with buildings.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteUnitVisual : MonoBehaviour, IUnitVisual
{
    public Sprite idle;
    public List<Sprite> walk = new List<Sprite>();
    public List<Sprite> attack = new List<Sprite>();
    public List<Sprite> death = new List<Sprite>();

    [Tooltip("The attack frame where the weapon connects.")]
    public int impactFrame = 2;

    public float walkFps = 9f;
    public float deathFrameTime = 0.14f;

    [Tooltip("Height of the health bar above the unit's feet.")]
    public float barHeight = 1.0f;

    [Header("Hero look (generated)")]
    [Tooltip("The same frames without helmet, shield or melee weapon: a hero wears their own.")]
    public Sprite heroIdle;
    public List<Sprite> heroWalk = new List<Sprite>();
    public List<Sprite> heroAttack = new List<Sprite>();
    public List<Sprite> heroDeath = new List<Sprite>();
    [Tooltip("Gear positions for the 12 frames: idle, walk 0-3, attack 0-3, death 0-2.")]
    public List<GearAnchor> anchors = new List<GearAnchor>();
    public WeaponClass weaponClass = WeaponClass.Melee;
    [Tooltip("What the hero shows in a slot with nothing equipped (the unit's usual kit).")]
    public GearLook defaultHead, defaultShield, defaultWeapon;

    public bool HasHeroRig => heroIdle != null && anchors != null && anchors.Count == 12;

    private SpriteRenderer body;
    private SpriteRenderer barBack;
    private SpriteRenderer barFill;
    private bool moving;
    private bool dead;
    private float attackStart = -99f;
    private float attackFrameTime = 0.1f;
    private float deathStart;
    private float walkTime;
    private int frame; // 0 idle, 1-4 walk, 5-8 attack, 9-11 death

    private class Overlay
    {
        public SpriteRenderer main, trim;
        public GearLook look = (GearLook)(-1);
        public Rarity rarity;
    }
    private Overlay[] overlays;

    private const float BarWidth = 0.5f;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        body.sortingOrder = BuildingView.SortingOrder;
        barBack = MakeBar("HealthBack", new Color(0, 0, 0, 0.65f), 7);
        barFill = MakeBar("HealthFill", new Color(0.3f, 0.9f, 0.3f), 8);
        SetHealth(1f);
    }

    private SpriteRenderer MakeBar(string objectName, Color color, int order)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = new Vector3(-BarWidth / 2, barHeight, 0);
        sr.transform.localScale = new Vector3(BarWidth, 0.07f, 1);
        sr.sprite = SimpleSprites.Square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public void SetMoving(bool isMoving) => moving = isMoving;

    public void PlayAttack(float hitDelay)
    {
        attackStart = Time.time;
        // Spread the frames before the impact over the wind-up so the strike lands on time.
        attackFrameTime = impactFrame > 0 ? hitDelay / impactFrame : 0.1f;
    }

    public void PlayDeath()
    {
        dead = true;
        deathStart = Time.time;
        barBack.enabled = barFill.enabled = false;
    }

    public void SetHealth(float share)
    {
        if (dead || barBack == null)
        {
            return;
        }
        bool hurt = share < 0.999f;
        barBack.enabled = barFill.enabled = hurt;
        if (hurt)
        {
            barFill.transform.localScale = new Vector3(BarWidth * Mathf.Clamp01(share), 0.07f, 1);
            barFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.9f, 0.3f), share);
        }
    }

    private void Update()
    {
        if (dead)
        {
            int i = Mathf.Min(death.Count - 1, (int)((Time.time - deathStart) / deathFrameTime));
            if (i >= 0)
            {
                body.sprite = death[i];
                frame = 9 + Mathf.Min(i, 2);
            }
            return;
        }

        float sinceAttack = Time.time - attackStart;
        if (attack.Count > 0 && sinceAttack < attackFrameTime * attack.Count)
        {
            int i = Mathf.Clamp((int)(sinceAttack / attackFrameTime), 0, attack.Count - 1);
            body.sprite = attack[i];
            frame = 5 + Mathf.Min(i, 3);
            return;
        }

        if (moving && walk.Count > 0)
        {
            walkTime += Time.deltaTime;
            int i = (int)(walkTime * walkFps) % walk.Count;
            body.sprite = walk[i];
            frame = 1 + Mathf.Min(i, 3);
        }
        else
        {
            body.sprite = idle;
            frame = 0;
        }
    }

    private void LateUpdate()
    {
        // Lower on screen = nearer the viewer, same rule as buildings and villagers.
        var p = transform.position;
        transform.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
        PlaceOverlays();
    }

    // ---------- Hero gear ----------

    /// <summary>Swap to the bare hero body; gear is then drawn on top by ShowGear.</summary>
    public void UseHeroBody()
    {
        if (!HasHeroRig || overlays != null)
        {
            return;
        }
        idle = heroIdle;
        walk = heroWalk;
        attack = heroAttack;
        death = heroDeath;
        overlays = new Overlay[5];
        for (int i = 0; i < 5; i++)
        {
            var main = new GameObject(((GearSlot)i).ToString()).AddComponent<SpriteRenderer>();
            main.transform.SetParent(transform, false);
            main.sortingOrder = body.sortingOrder;
            var trim = new GameObject("Trim").AddComponent<SpriteRenderer>();
            trim.transform.SetParent(main.transform, false);
            trim.sortingOrder = body.sortingOrder;
            overlays[i] = new Overlay { main = main, trim = trim };
        }
    }

    /// <summary>
    /// Show what a hero wears: one look and rarity per slot (Head, Body, Weapon, Shield, Trinket).
    /// An empty slot (GearLook.None) falls back to the unit's usual helmet, shield or weapon.
    /// </summary>
    public void ShowGear(IList<GearLook> looks, IList<Rarity> rarities)
    {
        UseHeroBody();
        if (overlays == null)
        {
            return;
        }
        var art = ArtLibrary.Load();
        for (int i = 0; i < 5; i++)
        {
            var look = i < looks.Count ? looks[i] : GearLook.None;
            var rarity = i < rarities.Count ? rarities[i] : Rarity.Common;
            if (look == GearLook.None)
            {
                look = i == (int)GearSlot.Head ? defaultHead : i == (int)GearSlot.Shield ? defaultShield
                     : i == (int)GearSlot.Weapon ? defaultWeapon : GearLook.None;
                rarity = Rarity.Common;
            }
            var o = overlays[i];
            if (o.look == look && o.rarity == rarity)
            {
                continue;
            }
            o.look = look;
            o.rarity = rarity;
            int index = (int)look;
            o.main.sprite = art != null && index < art.wornGear.Count ? art.wornGear[index] : null;
            o.trim.sprite = art != null && index < art.wornGearTrim.Count && rarity > Rarity.Common ? art.wornGearTrim[index] : null;
            o.trim.color = Items.RarityColors[(int)rarity];
        }
        PlaceOverlays();
    }

    // Drawn just in front of the body in this order: armour, helmet, trinket, shield, weapon.
    private static readonly int[] OverlayDepth = { 2, 1, 5, 4, 3 }; // indexed by GearSlot

    private void PlaceOverlays()
    {
        if (overlays == null || frame >= anchors.Count)
        {
            return;
        }
        var a = anchors[frame];
        // Units face left by turning round, which points local z away from the camera.
        float toCamera = Vector3.Dot(transform.forward, Vector3.forward) >= 0 ? -1f : 1f;
        for (int i = 0; i < 5; i++)
        {
            var o = overlays[i];
            bool shown = o.main.sprite != null && (i != (int)GearSlot.Shield || a.shieldShown);
            o.main.enabled = shown;
            o.trim.enabled = shown && o.trim.sprite != null;
            if (!shown)
            {
                continue;
            }
            Vector2 at;
            float angle = a.lean;
            switch ((GearSlot)i)
            {
                case GearSlot.Head: at = a.head; break;
                case GearSlot.Body: at = a.torso; break;
                case GearSlot.Weapon: at = a.grip; angle = a.weaponAngle; break;
                case GearSlot.Shield: at = a.shield; break;
                default:
                    at = o.look == GearLook.Wreath ? a.head : o.look == GearLook.Horn ? a.hip : o.look == GearLook.Ring ? a.handL : a.neck;
                    break;
            }
            o.main.transform.localPosition = new Vector3(at.x, at.y, toCamera * OverlayDepth[i] * 0.00002f);
            o.main.transform.localRotation = Quaternion.Euler(0, 0, angle);
            o.trim.transform.localPosition = new Vector3(0, 0, toCamera * 0.00001f);
            if (o.rarity == Rarity.Legendary)
            {
                // A slow shimmer on legendary trim.
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + i);
                o.trim.color = Color.Lerp(Items.RarityColors[(int)Rarity.Legendary], Color.white, t * 0.6f);
            }
        }
    }
}
