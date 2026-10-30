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

    private SpriteRenderer body;
    private SpriteRenderer barBack;
    private SpriteRenderer barFill;
    private bool moving;
    private bool dead;
    private float attackStart = -99f;
    private float attackFrameTime = 0.1f;
    private float deathStart;
    private float walkTime;

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
            }
            return;
        }

        float sinceAttack = Time.time - attackStart;
        if (attack.Count > 0 && sinceAttack < attackFrameTime * attack.Count)
        {
            body.sprite = attack[Mathf.Clamp((int)(sinceAttack / attackFrameTime), 0, attack.Count - 1)];
            return;
        }

        if (moving && walk.Count > 0)
        {
            walkTime += Time.deltaTime;
            body.sprite = walk[(int)(walkTime * walkFps) % walk.Count];
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
}
