using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Quest objectives on the map: a banner where a hero must go, a lair where a named boss sleeps
/// (it wakes, with its followers, when a hero comes near), a pennant over the camp or ruin a quest
/// points at, and a marker for each on the zoomed-out maps. Tells the settlement when a hero
/// arrives or a boss falls.
/// </summary>
public class QuestMarkers : MonoBehaviour
{
    public static QuestMarkers Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private FreeModeController controller;
    private Settlement settlement;
    private float nextSync;

    private const float ReachRadius = 2.6f, WakeRadius = 7f, GuardRadius = 7f;

    private class Mark
    {
        public QuestState quest;
        public QuestStep step;
        public GameObject root;
        public SpriteRenderer body, mapIcon;
        public TextMeshPro label;
        public Enemy boss;
        public bool spawned, slain;
        public HeroState waker;
        public readonly List<Enemy> minions = new List<Enemy>();
    }

    private readonly Dictionary<int, Mark> marks = new Dictionary<int, Mark>();

    public static QuestMarkers Create(FreeModeController owner)
    {
        var go = new GameObject("QuestMarkers");
        go.transform.SetParent(owner.transform, false);
        var m = go.AddComponent<QuestMarkers>();
        m.controller = owner;
        m.settlement = owner.Settlement;
        Instance = m;
        return m;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Where a quest's current objective is, if it has one on this world's map.</summary>
    public static bool TryGetSpot(Settlement s, QuestState q, out Vector2 spot)
    {
        spot = default;
        var step = Settlement.CurrentStep(q);
        if (step == null || q.world != s.State.currentWorld) return false;
        foreach (var (quest, st) in s.QuestTargetsHere())
            if (quest == q) { spot = st.spot; return true; }
        return false;
    }

    /// <summary>Glide the camera to a quest's objective, with a ring to show it.</summary>
    public void ShowOnMap(QuestState q)
    {
        if (!TryGetSpot(settlement, q, out var spot)) return;
        controller.PanTo(spot);
        RingPulse.Spawn(spot, 3f, new Color(1f, 0.85f, 0.35f, 0.9f));
    }

    private void Update()
    {
        if (controller == null || settlement == null || settlement.State == null) return;
        if (Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + 0.25f;
            Sync();
            foreach (var m in new List<Mark>(marks.Values)) Tick(m);
        }
        UpdateIcons();
    }

    // ---------- Keeping a mark for each objective ----------

    private void Sync()
    {
        var targets = settlement.QuestTargetsHere();
        var seen = new HashSet<int>();
        foreach (var (q, s) in targets)
        {
            seen.Add(q.id);
            if (marks.TryGetValue(q.id, out var m))
            {
                if (m.step == s) continue;
                Remove(m, false);   // the quest moved on a step
            }
            marks[q.id] = Make(q, s);
        }
        foreach (var id in new List<int>(marks.Keys))
            if (!seen.Contains(id)) { Remove(marks[id], true); }
    }

    private Mark Make(QuestState q, QuestStep s)
    {
        var art = QuestArt.Get();
        var m = new Mark { quest = q, step = s };
        m.root = new GameObject($"Quest {q.id} {s.kind}");
        m.root.transform.SetParent(transform, false);
        m.root.transform.position = new Vector3(s.spot.x, s.spot.y, 0);

        m.body = new GameObject("Body").AddComponent<SpriteRenderer>();
        m.body.transform.SetParent(m.root.transform, false);
        bool raised = s.kind == QuestStepKind.ClearCamp || s.kind == QuestStepKind.ClearSite;
        float lift = raised ? 3.2f : 0f;
        m.body.transform.localPosition = new Vector3(0, lift, BuildingView.DepthFor(s.spot.y + (raised ? -2f : 0.2f)));
        m.body.sortingOrder = BuildingView.SortingOrder + (raised ? 2 : 0);
        Sprite sprite = null;
        if (art != null)
            sprite = s.kind == QuestStepKind.Slay ? art.LairFor(q.theme) : raised ? art.pennant : art.banner;
        if (sprite == null)
        {
            sprite = SimpleSprites.Ring;
            m.body.color = new Color(1f, 0.85f, 0.35f, 0.9f);
            m.body.transform.localScale = Vector3.one * 2.5f;
        }
        m.body.sprite = sprite;

        m.mapIcon = new GameObject("MapIcon").AddComponent<SpriteRenderer>();
        m.mapIcon.transform.SetParent(transform, false);
        m.mapIcon.sprite = art != null && art.icon != null ? art.icon : SimpleSprites.Ring;
        m.mapIcon.sortingOrder = 31;
        m.mapIcon.enabled = false;

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(m.root.transform, false);
        labelObject.transform.localPosition = new Vector3(0, lift + 4.2f, 0);
        m.label = labelObject.AddComponent<TextMeshPro>();
        m.label.alignment = TextAlignmentOptions.Center;
        m.label.fontSize = 4f;
        m.label.outlineWidth = 0.25f;
        m.label.outlineColor = new Color32(0, 0, 0, 220);
        m.label.rectTransform.sizeDelta = new Vector2(12, 2f);
        m.label.sortingOrder = 8;
        return m;
    }

    private void Remove(Mark m, bool questGone)
    {
        if (questGone && m.boss != null && m.boss.IsAlive)
        {
            // Given up with the boss still standing: whatever woke goes back to sleep.
            Destroy(m.boss.gameObject);
            foreach (var e in m.minions) if (e != null && e.IsAlive) Destroy(e.gameObject);
        }
        if (m.root != null) Destroy(m.root);
        if (m.mapIcon != null) Destroy(m.mapIcon.gameObject);
        marks.Remove(m.quest.id);
    }

    // ---------- What happens at each ----------

    private void Tick(Mark m)
    {
        var s = m.step;
        m.label.text = Hovered(m) ? $"<color=#ffd98a>{m.quest.title}</color>\n<size=75%>{s.text}</size>" : "";
        switch (s.kind)
        {
            case QuestStepKind.Travel:
            {
                var hero = NearestHero(s.spot, s.count < 0 ? 5f : ReachRadius);   // (home: anywhere round the town centre)
                if (hero != null)
                {
                    GameAudio.Play("discover", s.spot);
                    settlement.QuestReached(m.quest.id, hero.Hero);
                }
                break;
            }
            case QuestStepKind.Slay:
                TickLair(m);
                break;
        }
    }

    private void TickLair(Mark m)
    {
        var s = m.step;
        if (!m.spawned)
        {
            var hero = NearestHero(s.spot, WakeRadius);
            if (hero == null) return;
            m.waker = hero.Hero;
            Wake(m);
            return;
        }
        m.minions.RemoveAll(e => e == null || !e.IsAlive);
        if (m.slain) return;
        if (m.boss != null && !m.boss.IsAlive)
        {
            m.slain = true;
            var hero = NearestHero(s.spot, 25f);
            settlement.QuestBossSlain(m.quest.id, hero != null ? hero.Hero : m.waker);
        }
        else if (m.boss == null)
        {
            m.spawned = false;   // gone some other way: it waits at its lair again
        }
    }

    /// <summary>The named boss and its followers wake at the lair.</summary>
    private void Wake(Mark m)
    {
        var q = m.quest;
        var s = m.step;
        var def = settlement.QuestBossDef(q, s);
        m.spawned = true;
        if (def == null || def.prefab == null)
        {
            // Nothing to fight in this build: the lair is simply empty.
            m.slain = true;
            settlement.QuestBossSlain(q.id, m.waker);
            return;
        }
        Vector3 centre = new Vector3(s.spot.x, s.spot.y, 0);
        Vector3 at = centre + Vector3.down * 0.8f;
        if (!settlement.Map.IsWalkable(at)) at = centre;
        var go = controller.SpawnUnit(def.prefab, at);
        m.boss = go != null ? go.GetComponent<Enemy>() : null;
        if (m.boss != null)
        {
            m.boss.StartGuard(centre, GuardRadius);
            m.boss.SetGear(settlement.QuestBossBoost(q, def));
            m.boss.SetBuildingModifier(new UnitStatModifier());
            m.boss.health = m.boss.maxHealth;
            var tag = go.GetComponent<Boss>();
            if (tag == null) tag = go.AddComponent<Boss>();
            tag.customTitle = s.name;
        }
        var followers = settlement.QuestMinionDefs(q);
        int count = followers.Count > 0 ? settlement.QuestMinionCount(q) : 0;
        for (int i = 0; i < count; i++)
        {
            var unit = followers[i % followers.Count];
            Vector2 off = Random.insideUnitCircle.normalized * Random.Range(1.4f, 2.6f);
            var p = centre + new Vector3(off.x, off.y, 0);
            if (!settlement.Map.IsWalkable(p)) p = at;
            var mg = controller.SpawnUnit(unit.prefab, p);
            var e = mg != null ? mg.GetComponent<Enemy>() : null;
            if (e == null) continue;
            e.StartGuard(centre, GuardRadius);
            m.minions.Add(e);
        }
        RingPulse.Spawn(centre, 3.5f, new Color(0.95f, 0.35f, 0.25f, 0.8f));
        GameAudio.Play("boss_warcry", centre);
        controller.ShowMessage($"<color=#fd6>{s.name}</color> wakes at its lair!");
    }

    // ---------- Helpers ----------

    private static Troop NearestHero(Vector2 at, float range)
    {
        Troop best = null;
        float bestD = range;
        foreach (var u in Unit.Living)
        {
            if (!(u is Troop t) || t.Hero == null || !t.IsAlive || t.Team != Team.Player) continue;
            float d = Vector2.Distance(t.transform.position, at);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    private bool Hovered(Mark m)
    {
        var mouse = SettlementGrid.MouseWorld();
        return Vector2.Distance(mouse, m.step.spot) < (m.step.kind == QuestStepKind.Slay ? 2.2f : 1.4f);
    }

    /// <summary>Quest markers on the zoomed-out maps keep a fixed size on screen.</summary>
    private void UpdateIcons()
    {
        var cam = Camera.main;
        if (cam == null) return;
        bool zoomedOut = controller.Grid.CurrentDetail != SettlementGrid.Detail.Tiles;
        float screenSize = cam.orthographicSize * 0.12f;
        foreach (var m in marks.Values)
        {
            if (m.mapIcon == null) continue;
            m.mapIcon.enabled = zoomedOut && m.mapIcon.sprite != null;
            if (!m.mapIcon.enabled) continue;
            m.mapIcon.transform.position = new Vector3(m.step.spot.x, m.step.spot.y, -1.1f);
            float h = Mathf.Max(0.01f, m.mapIcon.sprite.bounds.size.y);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            m.mapIcon.transform.localScale = Vector3.one * screenSize * 0.8f * pulse / h;
        }
    }
}
