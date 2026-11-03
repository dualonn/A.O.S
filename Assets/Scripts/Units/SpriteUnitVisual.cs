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
public enum WeaponClass { Melee, Bow, Thrown, None, Crossbow, Staff, Musket, Cannon }

/// <summary>
/// Frame-by-frame sprite animation for units in pixel art:
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

    [Tooltip("Width of the health bar.")]
    public float barWidth = 0.5f;

    [Tooltip("Show the health bar even at full health (bosses).")]
    public bool alwaysShowBar;

    [Header("Look (generated)")]
    [Tooltip("How this unit is drawn: heroes are redrawn from it in their own gear.")]
    public FigureStyle figure;
    [Tooltip("Pixels per world unit of the frames (matches the terrain's pixel size once the unit is scaled).")]
    public float pixelsPerUnit = 32f;
    public WeaponClass weaponClass = WeaponClass.Melee;

    public bool HasHeroRig => figure != null && pixelsPerUnit > 0f;

    private SpriteRenderer body;
    private SpriteRenderer barBack;
    private SpriteRenderer barFill;
    private bool moving;
    private bool dead;
    private float attackStart = -99f;
    private float attackFrameTime = 0.1f;
    private float deathStart;
    private float walkTime;
    private readonly List<Object> heroArt = new List<Object>();
    private string heroLookKey;

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
        sr.transform.localPosition = new Vector3(-barWidth / 2, barHeight, 0);
        sr.transform.localScale = new Vector3(barWidth, alwaysShowBar ? 0.1f : 0.07f, 1);
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
        bool hurt = share < 0.999f || alwaysShowBar;
        barBack.enabled = barFill.enabled = hurt;
        if (hurt)
        {
            barFill.transform.localScale = new Vector3(barWidth * Mathf.Clamp01(share), barBack.transform.localScale.y, 1);
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
            }
            return;
        }

        float sinceAttack = Time.time - attackStart;
        if (attack.Count > 0 && sinceAttack < attackFrameTime * attack.Count)
        {
            int i = Mathf.Clamp((int)(sinceAttack / attackFrameTime), 0, attack.Count - 1);
            body.sprite = attack[i];
            return;
        }

        if (moving && walk.Count > 0)
        {
            walkTime += Time.deltaTime;
            int i = (int)(walkTime * walkFps) % walk.Count;
            body.sprite = walk[i];
        }
        else
        {
            body.sprite = idle;
        }
    }

    private void LateUpdate()
    {
        // Lower on screen = nearer the viewer, same rule as buildings and villagers.
        var p = transform.position;
        transform.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
    }

    // ---------- Hero gear ----------

    /// <summary>
    /// Show what a hero wears: one look and rarity per slot (Head, Body, Weapon, Shield, Trinket).
    /// The figure is redrawn with that gear, so every piece sits where it should (a weapon in the
    /// far hand behind the body, a shield on the near arm in front). Empty slots keep the unit's kit.
    /// </summary>
    public void ShowGear(IList<GearLook> looks, IList<Rarity> rarities)
    {
        if (!HasHeroRig) return;
        var style = figure.Clone();
        style.trims = new Rarity[5];
        var key = new System.Text.StringBuilder();
        for (int i = 0; i < 5; i++)
        {
            var look = i < looks.Count ? looks[i] : GearLook.None;
            var rarity = i < rarities.Count ? rarities[i] : Rarity.Common;
            key.Append((int)look).Append(':').Append((int)rarity).Append(',');
            if (look == GearLook.None) continue;
            switch ((GearSlot)i)
            {
                case GearSlot.Head: style.head = look; break;
                case GearSlot.Body: style.body = look; break;
                case GearSlot.Weapon: if (look != GearLook.Bow || style.weapon == GearLook.Bow) style.weapon = look; break;
                case GearSlot.Shield: style.shield = look; break;
                case GearSlot.Trinket: style.trinket = look; break;
            }
            style.trims[i] = rarity;
        }
        if (key.ToString() == heroLookKey) return;
        heroLookKey = key.ToString();

        foreach (var o in heroArt) if (o != null) Destroy(o);
        heroArt.Clear();
        var pivot = PixelFigure.PivotFor(style);
        var frames = new Sprite[PixelFigure.FrameCount];
        for (int f = 0; f < frames.Length; f++)
        {
            var tex = PixelFigure.Draw(style, f).ToTexture();
            tex.name = $"Hero frame {f}";
            frames[f] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            heroArt.Add(tex);
            heroArt.Add(frames[f]);
        }
        idle = frames[0];
        walk = new List<Sprite> { frames[1], frames[2], frames[3], frames[4] };
        attack = new List<Sprite> { frames[5], frames[6], frames[7], frames[8] };
        death = new List<Sprite> { frames[9], frames[10], frames[11] };
        if (body != null && !dead) body.sprite = idle;
    }

    private void OnDestroy()
    {
        foreach (var o in heroArt) if (o != null) Destroy(o);
    }
}
