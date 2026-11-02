using System;
using UnityEngine;

/// <summary>
/// A purely cosmetic townsperson. Lives in a house, walks to workplaces, works a while,
/// carries the goods home, wanders about, and hides indoors during attacks.
/// Has no effect on the simulation.
/// </summary>
public class Villager : MonoBehaviour
{
    private enum Mode { Inside, Walking, Working, Idle }

    public BuildingView Home { get; private set; }
    public bool Leaving { get; private set; }

    private const float Speed = 0.9f;
    private const float WalkFps = 8f;
    private const float WorkFps = 3f;

    private AmbientLife life;
    private ArtLibrary art;
    private SpriteRenderer body;
    private SpriteRenderer carried;
    private Mode mode;
    private float timer;
    private float animTime;
    private Vector3 target;
    private Action onArrive;
    private bool goingHome;

    public void Init(AmbientLife owner, BuildingView home, ArtLibrary library)
    {
        life = owner;
        Home = home;
        art = library;
        name = "Villager";

        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = art.villagerIdle;
        body.sortingOrder = BuildingView.SortingOrder;

        carried = new GameObject("Carried").AddComponent<SpriteRenderer>();
        carried.transform.SetParent(transform, false);
        carried.transform.localPosition = new Vector3(0.12f, 0.78f, -0.001f);
        carried.transform.localScale = Vector3.one * 0.32f;
        carried.sortingOrder = BuildingView.SortingOrder;
        carried.enabled = false;

        // Pixel art: everyone is drawn at the same pixel size (no random scaling).
        transform.localScale = Vector3.one;

        transform.position = life.DoorOf(home);
        StayInside(UnityEngine.Random.Range(0f, 1.5f));
    }

    /// <summary>Walk home and disappear (population went down or the house is gone).</summary>
    public void Leave()
    {
        if (Leaving)
        {
            return;
        }
        Leaving = true;
        if (Home == null || mode == Mode.Inside)
        {
            Destroy(gameObject);
            return;
        }
        GoHome(() => Destroy(gameObject));
    }

    private void Update()
    {
        if (Home == null)
        {
            Destroy(gameObject);
            return;
        }

        // Head indoors when raiders come.
        if (life.UnderAttack && !goingHome && mode != Mode.Inside)
        {
            GoHome(() => StayInside(1f));
        }

        float dt = Time.deltaTime;
        animTime += dt;
        switch (mode)
        {
            case Mode.Inside:
                timer -= dt;
                if (timer <= 0 && !life.UnderAttack && !Leaving)
                {
                    body.enabled = true;
                    transform.position = life.DoorOf(Home);
                    Decide();
                }
                break;

            case Mode.Walking:
                Vector3 pos = transform.position;
                pos.z = 0;
                Vector3 waypoint = pathFollower.NextPoint(pos, target);
                float speed = Speed * (goingHome && life.UnderAttack ? 2f : 1f);
                if (MapNavigator.Current != null)
                {
                    speed *= MapNavigator.Current.SpeedFactor(pos);
                }
                Vector3 next = MapNavigator.Current != null
                    ? MapNavigator.Current.Step(pos, waypoint, speed * dt)
                    : Vector3.MoveTowards(pos, waypoint, speed * dt);
                next.z = target.z;
                transform.position = next;
                Face(waypoint.x - pos.x);
                SetFrame(art.villagerWalk, WalkFps);
                if (Vector2.Distance(next, target) < 0.02f)
                {
                    var arrive = onArrive;
                    onArrive = null;
                    arrive?.Invoke();
                }
                break;

            case Mode.Working:
                SetFrame(art.villagerWork, WorkFps);
                timer -= dt;
                if (timer <= 0)
                {
                    Decide();
                }
                break;

            case Mode.Idle:
                body.sprite = art.villagerIdle;
                timer -= dt;
                if (timer <= 0)
                {
                    Decide();
                }
                break;
        }

        // Depth: lower on screen = nearer the viewer.
        var p = transform.position;
        transform.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
    }

    /// <summary>Pick what to do next.</summary>
    private void Decide()
    {
        goingHome = false;

        // Just finished work: carry what was made back home.
        if (pendingCarry != null)
        {
            carried.sprite = pendingCarry;
            carried.enabled = true;
            pendingCarry = null;
            GoHome(() => { carried.enabled = false; StayInside(UnityEngine.Random.Range(1f, 3f)); });
            return;
        }

        float roll = UnityEngine.Random.value;
        var work = life.RandomWorkplace();
        if (work != null && roll < 0.55f)
        {
            var product = life.ProductOf(work);
            WalkTo(life.DoorOf(work), () =>
            {
                mode = Mode.Working;
                timer = UnityEngine.Random.Range(3f, 7f);
                Face(life.Grid.AreaCenter(work.State.cell, work.Definition.size).x - transform.position.x);
                pendingCarry = product;
            });
        }
        else if (roll < 0.85f)
        {
            WalkTo(life.RandomSpotNear(Home), () =>
            {
                mode = Mode.Idle;
                timer = UnityEngine.Random.Range(1f, 3.5f);
            });
        }
        else
        {
            GoHome(() => StayInside(UnityEngine.Random.Range(2f, 6f)));
        }
    }

    private Sprite pendingCarry;
    private readonly PathFollower pathFollower = new PathFollower();

    private void WalkTo(Vector3 destination, Action arrive)
    {
        target = destination;
        target.z = 0;
        onArrive = arrive;
        mode = Mode.Walking;
    }

    private void GoHome(Action arrive)
    {
        goingHome = true;
        WalkTo(life.DoorOf(Home), arrive);
    }

    private void StayInside(float seconds)
    {
        goingHome = false;
        mode = Mode.Inside;
        timer = seconds;
        body.enabled = false;
        carried.enabled = false;
        if (Leaving)
        {
            Destroy(gameObject);
        }
    }

    private void SetFrame(System.Collections.Generic.List<Sprite> frames, float fps)
    {
        if (frames == null || frames.Count == 0)
        {
            return;
        }
        body.sprite = frames[(int)(animTime * fps) % frames.Count];
    }

    private void Face(float dx)
    {
        if (Mathf.Abs(dx) > 0.01f)
        {
            body.flipX = dx < 0;
            var c = carried.transform.localPosition;
            carried.transform.localPosition = new Vector3(Mathf.Abs(c.x) * (dx < 0 ? -1 : 1), c.y, c.z);
        }
    }
}
