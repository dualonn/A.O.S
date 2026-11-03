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
    private float nextTroopSync;           // keeping troops' orders in line runs a few times a second, not every frame
    private Settlement.BuildingStats? syncedStats;

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
        // Sort by the foot of the front wall, a little way up the footprint in this 3/4 view, so a
        // unit standing at the wall (feet in front of it) draws in front.
        // Flat buildings (fields) set their own line further back.
        float frontWall = (def.sortLine > 0 ? def.sortLine : Mathf.Min(0.5f, def.size.y * 0.25f)) * grid.cellSize;
        transform.position = new Vector3(corner.x, corner.y, DepthFor(corner.y + frontWall));

        selectionOutline = MakeQuad("Selection", new Vector2(-Margin, -Margin), size + Vector2.one * Margin * 2,
            new Color(1f, 0.9f, 0.2f, 0.35f), -8);
        selectionOutline.enabled = false;

        if (def.prefab != null)
        {
            var visual = Instantiate(def.prefab, transform);
            visual.transform.position = grid.AreaCenter(state.cell, def.size);
        }
        else if (def.icon != null || def.looks.Count > 0)
        {
            body = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            var first = CurrentFrame() ?? def.icon;
            body.sprite = first;
            // Art is drawn one unit per tile with its pivot on the footprint's corner; it may
            // reach past the footprint (roofs and flags over the tiles behind, smoke above).
            body.transform.localScale = Vector3.one * grid.cellSize;
            body.transform.localPosition = Vector3.zero;
            body.sortingOrder = SortingOrder;
            animationPhase = Random.Range(0f, 10f);
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
            // Tile it over the footprint (stretching pixel art would warp it).
            scaffold.drawMode = SpriteDrawMode.Tiled;
            scaffold.tileMode = SpriteTileMode.Continuous;
            scaffold.size = new Vector2(size.x, size.y);
            scaffold.sortingOrder = -5;
        }

        float barWidth = Mathf.Min(size.x - 0.3f, 1.6f);
        float barX = (size.x - barWidth) / 2;
        float top = def.ArtSize.y * grid.cellSize;
        barBack = MakeQuad("BarBack", new Vector2(barX, top + 0.05f), new Vector2(barWidth, 0.16f), new Color(0, 0, 0, 0.6f), 6);
        barFill = MakeQuad("BarFill", new Vector2(barX, top + 0.05f), new Vector2(barWidth, 0.16f), Color.green, 7);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(size.x / 2, top + 0.55f, 0);
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

    /// <summary>Highlight as selected. 'showName' is off in a group, so many labels don't clutter the view.</summary>
    public void SetSelected(bool isSelected, bool showName = true)
    {
        selected = isSelected && showName;
        selectionOutline.enabled = isSelected;
    }

    private void Update()
    {
        hovered = !controller.Placer.IsPlacing && controller.Grid != null &&
                  Contains(controller.Grid.WorldToCell(SettlementGrid.MouseWorld()));
        UnityEngine.Profiling.Profiler.BeginSample("BuildingView.Refresh");
        Refresh();
        UnityEngine.Profiling.Profiler.EndSample();
        Animate();
        UpdateWallPiece();
        UpdateShake();
        UnityEngine.Profiling.Profiler.BeginSample("BuildingView.TrainTroops");
        TrainTroops();
        UnityEngine.Profiling.Profiler.EndSample();
        TowerAttack();
    }

    // ---------- Walls and gates ----------

    private int wallMask = -1;
    private bool gateOpen;
    private float nextGateCheck;

    /// <summary>Which sides join other walls (N=1, E=2, S=4, W=8); picks the matching piece.</summary>
    public void SetWallMask(int mask)
    {
        wallMask = mask;
        UpdateWallPiece(true);
    }

    private void UpdateWallPiece(bool force = false)
    {
        if (!Definition.IsWall || body == null || wallMask < 0 || Definition.pieces.Count == 0)
        {
            return;
        }
        if (Definition.wallKind == WallTile.Gate && Time.time >= nextGateCheck)
        {
            // Open for any of your units close by (and always shown open once ruined).
            nextGateCheck = Time.time + 0.2f * Mathf.Max(1f, Time.timeScale * 0.5f);
            Vector3 centre = controller.Grid.AreaCenter(State.cell, Definition.size);
            bool open = State.health <= 0 || Unit.FindNearestEnemy(Team.Enemy, centre, 1.6f) != null;
            if (open != gateOpen)
            {
                gateOpen = open;
                force = true;
                if (State.health > 0) GameAudio.Play(open ? "gate_open" : "gate_close", centre, 0.6f);
            }
        }
        foreach (var run in backRuns)
        {
            if (run != null) run.color = body.color;
        }
        if (!force)
        {
            return;
        }
        if (Definition.wallKind == WallTile.Gate)
        {
            // Pieces: east-west closed, east-west open, north-south closed, north-south open.
            bool eastWest = (wallMask & (2 | 8)) != 0 || (wallMask & (1 | 4)) == 0;
            int index = (eastWest ? 0 : 2) + (gateOpen ? 1 : 0);
            var piece = Definition.pieces[Mathf.Min(index, Definition.pieces.Count - 1)];
            if (piece != null) body.sprite = piece;
            return;
        }
        // Wall: a front piece (post, east end, west end or east-west run) plus runs going back.
        bool east = (wallMask & 2) != 0, west = (wallMask & 8) != 0;
        bool ne = (wallMask & 16) != 0, se = (wallMask & 32) != 0, sw = (wallMask & 64) != 0, nw = (wallMask & 128) != 0;
        if (east && west)
        {
            body.sprite = WallPart(3);
        }
        else if (east)
        {
            // Carries on into a diagonal from the middle: open there so they join flush.
            body.sprite = WallPart(nw || sw ? 8 : 1);
        }
        else if (west)
        {
            body.sprite = WallPart(ne || se ? 7 : 2);
        }
        else
        {
            // Middle of a north-south or diagonal run: no post, so the run stays one piece.
            int links = 0;
            foreach (int bit in new[] { 1, 4, 16, 32, 64, 128 })
            {
                if ((wallMask & bit) != 0) links++;
            }
            body.sprite = links >= 2 ? null : WallPart(0);
        }
        // Runs: north (back), and diagonals dropping to the tiles in front.
        // At the top end of a slanted run the slope comes towards the viewer, so it's drawn
        // in front of the end post rather than behind it.
        bool endPost = body.sprite == WallPart(0);
        SetBackRun(0, (wallMask & 1) != 0 ? WallPart(4) : null);
        SetBackRun(1, se ? WallPart(5) : null, endPost);
        SetBackRun(2, sw ? WallPart(6) : null, endPost);
    }

    private readonly SpriteRenderer[] backRuns = new SpriteRenderer[3];

    private Sprite WallPart(int part) => part < Definition.pieces.Count ? Definition.pieces[part] : null;

    /// <summary>The runs back to the next tiles: drawn just behind this tile's front piece.</summary>
    private void SetBackRun(int slot, Sprite sprite, bool inFront = false)
    {
        if (sprite == null)
        {
            if (backRuns[slot] != null) backRuns[slot].enabled = false;
            return;
        }
        if (backRuns[slot] == null)
        {
            backRuns[slot] = new GameObject("WallRun").AddComponent<SpriteRenderer>();
            backRuns[slot].transform.SetParent(body.transform, false);
            backRuns[slot].transform.localPosition = new Vector3(0, 0, 0.002f);
            backRuns[slot].sortingOrder = SortingOrder;
        }
        backRuns[slot].transform.localPosition = new Vector3(0, 0, inFront ? -0.002f : 0.002f);
        backRuns[slot].sprite = sprite;
        backRuns[slot].enabled = true;
        backRuns[slot].color = body.color;
    }

    // ---------- Animation and looks ----------

    private float animationPhase;

    /// <summary>The frame to show now: this upgrade level's look, looping (still while building or ruined).</summary>
    private Sprite CurrentFrame()
    {
        var looks = Definition.looks;
        if (looks == null || looks.Count == 0)
        {
            return null;
        }
        var look = looks[Mathf.Clamp(State.upgradeLevel, 0, looks.Count - 1)];
        if (look.frames == null || look.frames.Count == 0)
        {
            return null;
        }
        bool active = State.IsBuilt && State.health > 0;
        int frame = active ? (int)((Time.time + animationPhase) * look.fps) % look.frames.Count : 0;
        return look.frames[frame];
    }

    private void Animate()
    {
        if (body == null)
        {
            return;
        }
        var frame = CurrentFrame();
        if (frame != null && body.sprite != frame)
        {
            body.sprite = frame;
        }
    }

    // ---------- Towers (crewed by a visible operator) ----------

    private const float OperatorFrameTime = 0.11f;
    private float nextTowerShot;
    private SpriteRenderer towerOperator;
    private float shootStart = -99f;
    private Unit pendingTarget;
    private bool releasePending;

    /// <summary>Crew sprites are cut at the waist 8 art pixels above their feet (32 art pixels per tile).</summary>
    private const float CrewCutAbovePivot = 8f / 32f;

    private SpriteRenderer TowerOperator()
    {
        if (towerOperator == null && Definition.operatorIdle != null)
        {
            towerOperator = new GameObject("Operator").AddComponent<SpriteRenderer>();
            towerOperator.transform.SetParent(transform, false);
            // Same scale as troops in the field.
            towerOperator.transform.localScale = Vector3.one * controller.unitScale;
            towerOperator.sprite = Definition.operatorIdle;
            towerOperator.sortingOrder = SortingOrder;
        }
        if (towerOperator != null)
        {
            // Upgrades that make the tower taller lift the operator with the deck.
            float lift = 0f;
            for (int i = 0; i < State.upgradeLevel && i < Definition.upgrades.Count; i++)
            {
                lift += Definition.upgrades[i].operatorLift;
            }
            // Just in front of the tower: crew sprites are cut at the waist, and that cut sits on the
            // parapet top (operatorOffset), so the archer reads as standing behind it.
            float pivotToCut = CrewCutAbovePivot;
            towerOperator.transform.localPosition = new Vector3(Definition.operatorOffset.x, Definition.operatorOffset.y + lift - pivotToCut, -0.001f);
        }
        return towerOperator;
    }

    /// <summary>Towers shoot the nearest raider in range; the operator draws and looses each arrow.</summary>
    private void TowerAttack()
    {
        if (Definition.towerRange <= 0)
        {
            return;
        }
        bool working = controller.Settlement.IsWorking(State, Definition);
        var op = TowerOperator();
        if (op != null)
        {
            op.enabled = working; // nobody on a ruined or unfinished tower
            AnimateOperator(op);
        }
        if (!working)
        {
            return;
        }

        var stats = Settlement.StatsOf(Definition, State.upgradeLevel);
        Vector3 launch = op != null ? op.transform.position + Vector3.up * 0.45f
            : controller.Grid.AreaCenter(State.cell, Definition.size) + Vector3.up * Definition.size.y * 0.45f;

        // Release on the right frame of the draw animation.
        if (releasePending && Time.time >= shootStart + OperatorFrameTime * Definition.operatorReleaseFrame)
        {
            releasePending = false;
            if (pendingTarget != null && pendingTarget.IsAlive)
            {
                if (Definition.towerProjectile != null)
                {
                    if (op == null && Definition.towerMuzzle != Vector2.zero)
                    {
                        // Out of the gun's barrel, which the turret has swung round to face the target.
                        Vector3 corner = controller.Grid.CellToWorld(State.cell);
                        float mx = turretFacesLeft ? Definition.size.x - Definition.towerMuzzle.x : Definition.towerMuzzle.x;
                        launch = corner + new Vector3(mx, Definition.towerMuzzle.y, 0);
                    }
                    Projectile.Fire(Definition.towerProjectile, launch, pendingTarget, Definition.towerProjectileSpeed, stats.towerDamage, null, Definition.towerProjectileArc,
                        Definition.towerSplash);
                    GameAudio.Fire(Definition.towerSplash > 0f ? WeaponClass.Cannon : Definition.towerMuzzleSmoke ? WeaponClass.Musket : WeaponClass.Bow, launch);
                    if (Definition.towerMuzzleSmoke)
                        Puff.Smoke(launch + (pendingTarget.transform.position - launch).normalized * 0.5f);
                }
                else
                {
                    pendingTarget.TakeDamage(stats.towerDamage);
                }
            }
        }

        if (Time.time < nextTowerShot)
        {
            return;
        }
        // Short-handed towers shoot slower; paused ones not at all.
        float staff = controller.Settlement.StaffingOf(State);
        if (staff <= 0f)
        {
            nextTowerShot = Time.time + 0.5f;
            return;
        }
        var target = Unit.FindNearestEnemy(Team.Player, launch, stats.towerRange);
        if (target == null)
        {
            nextTowerShot = Time.time + 0.25f; // look again shortly
            return;
        }
        nextTowerShot = Time.time + Definition.towerCooldown / Mathf.Max(0.25f, staff);
        pendingTarget = target;
        releasePending = true;
        if (op == null && Definition.towerMuzzle != Vector2.zero)
            FaceTurret(target.transform.position.x < controller.Grid.CellToWorld(State.cell).x + Definition.size.x * 0.5f);
        shootStart = Time.time;
        if (op != null)
        {
            op.flipX = target.transform.position.x < op.transform.position.x;
        }
    }

    private bool turretFacesLeft;

    /// <summary>
    /// Swing a crewless turret round to face left or right: its art is mirrored about the
    /// footprint's middle (the base is symmetrical, so it reads as the gun turning).
    /// </summary>
    private void FaceTurret(bool left)
    {
        if (body == null || left == turretFacesLeft) return;
        turretFacesLeft = left;
        body.flipX = left;
        if (!bodyRestKnown) { bodyRestPosition = body.transform.localPosition; bodyRestKnown = true; }
        bodyRestPosition.x = left ? Definition.size.x * controller.Grid.cellSize : 0f;
        body.transform.localPosition = bodyRestPosition;
    }

    private void AnimateOperator(SpriteRenderer op)
    {
        var frames = Definition.operatorShoot;
        float since = Time.time - shootStart;
        if (frames != null && frames.Count > 0 && since < OperatorFrameTime * frames.Count)
        {
            op.sprite = frames[Mathf.Clamp((int)(since / OperatorFrameTime), 0, frames.Count - 1)];
        }
        else
        {
            op.sprite = Definition.operatorIdle;
        }
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
        if (Definition.IsWall)
        {
            // Walls come in dozens: only show a bar when damaged or pointed at.
            showBar = damaged || ((!built || State.upgrading) && (selected || hovered));
        }
        barBack.enabled = barFill.enabled = showBar;
        barFill.transform.localScale = new Vector3(barBack.transform.localScale.x * Mathf.Clamp01(fill), barFill.transform.localScale.y, 1);
        barFill.color = !built ? new Color(0.4f, 0.7f, 1f)
            : damaged ? Color.Lerp(Color.red, Color.green, healthShare)
            : new Color(1f, 0.85f, 0.3f);

        // Label: name (with upgrade level) on hover/selection, plus any status worth knowing.
        string status = !built ? "<color=#9cf>Building</color>"
            : ruined ? "<color=#ff7070>Ruined</color>"
            : State.rankingUp ? "<color=#fd6>Ranking up</color>"
            : State.upgrading ? "<color=#fd6>Upgrading</color>"
            : State.Paused ? "<color=#aaa>Paused</color>"
            : ProblemText();
        string name = State.upgradeLevel > 0 ? $"{Definition.displayName} <size=75%>Lv {State.upgradeLevel + 1}</size>" : Definition.displayName;
        bool showName = selected || hovered;
        if (Definition.IsWall && !showName)
        {
            status = ""; // no "Building" over every wall piece
        }
        label.text = showName && status.Length > 0 ? $"{name}\n<size=75%>{status}</size>"
            : showName ? name
            : status.Length > 0 ? $"<size=75%>{status}</size>"
            : "";
    }

    /// <summary>Short of workers or inputs (shown on the label even when not hovered).</summary>
    private string ProblemText()
    {
        var settlement = controller.Settlement;
        if (Definition.workers > 0 && settlement.StaffingOf(State) < 0.999f)
        {
            return "<color=#fc6>Short of workers</color>";
        }
        if (Definition.inputs.Count > 0 && settlement.InputSupplyOf(State) < 0.999f)
        {
            foreach (var input in Definition.inputs)
            {
                if (input.resource != null && settlement.GetAmount(input.resource) < 1f)
                {
                    return $"<color=#fc6>Needs {input.resource.displayName}</color>";
                }
            }
            return "<color=#fc6>Low on inputs</color>";
        }
        return "";
    }

    // ---------- Heroes who live here ----------

    private readonly Dictionary<int, Troop> heroes = new Dictionary<int, Troop>();

    /// <summary>A troop from this building has just been made a hero: it stops counting as a troop.</summary>
    public void AdoptAsHero(Troop troop, HeroState hero)
    {
        troops.Remove(troop);
        heroes[hero.id] = troop;
        troop.MakeHero(hero);
    }

    /// <summary>Keep this building's heroes on the field (and bring them back once recovered).</summary>
    private void KeepHeroes(Settlement.BuildingStats stats)
    {
        var settlement = controller.Settlement;
        if (Definition.trainsUnit == null || !settlement.IsWorking(State, Definition))
        {
            return;
        }
        foreach (var hero in settlement.State.heroes)
        {
            if (!settlement.HeroHere(hero))
            {
                if (heroes.TryGetValue(hero.id, out var away))
                {
                    if (away != null) Destroy(away.gameObject);
                    heroes.Remove(hero.id);
                }
                continue;
            }
            // Heroes whose home is gone move in with the first troop building that notices.
            if (hero.homeBuilding != State.instanceId && !settlement.State.buildings.Exists(b => b.instanceId == hero.homeBuilding))
            {
                hero.homeBuilding = State.instanceId;
            }
            if (hero.homeBuilding != State.instanceId)
            {
                continue;
            }
            heroes.TryGetValue(hero.id, out var troop);
            if (troop == null || !troop.IsAlive)
            {
                if (settlement.IsDown(hero))
                {
                    continue;
                }
                var unit = settlement.FindUnit(hero.unitId) ?? Definition.trainsUnit;
                if (unit == null || unit.prefab == null) continue;
                var spawn = controller.Grid.AreaCenter(State.cell, Definition.size);
                troop = controller.SpawnUnit(unit.prefab, spawn).GetComponent<Troop>();
                if (troop == null) continue;
                troop.Home = this;
                troop.SetRallyPoint(controller.GetRallyPoint(this));
                troop.SetStance(State.stance, controller.TownZoneFor(transform.position), controller.SightRange);
                troop.MakeHero(hero);
                heroes[hero.id] = troop;
            }
            troop.SetGear(settlement.HeroStats(hero));
            troop.SetBuildingModifier(stats.trainedUnits);
            troop.ShowHeroGear(settlement);
        }
    }

    private void TrainTroops()
    {
        troops.RemoveAll(t => t == null || !t.IsAlive);
        if (Definition.trainsUnit != null)
        {
            controller.Settlement.ReportLiveTroops(State, troops.Count);
        }
        // Keep the troops' orders, upgrades and home settlement's bounds in line with this building
        // (a few times a second is plenty, and it's costly with a big army).
        bool sync = syncedStats == null || Time.unscaledTime >= nextTroopSync;
        if (sync)
        {
            nextTroopSync = Time.unscaledTime + 0.4f;
            syncedStats = Settlement.StatsOf(Definition, State.upgradeLevel);
        }
        var stats = syncedStats.Value;
        if (sync && troops.Count > 0)
        {
            var zone = controller.TownZoneFor(transform.position);
            var defendArea = controller.SettlementFor(transform.position);
            float alertRange = controller.Settlement.Database.defendAlertRange;
            foreach (var t in troops)
            {
                if (t.Stance != State.stance)
                {
                    t.SetStance(State.stance, zone, controller.SightRange);
                    t.ReturnHome(controller.GetRallyPoint(this)); // a new stance replaces any order
                }
                t.SetDefendZone(zone);
                t.SetDefendArea(defendArea, alertRange);
                t.SetBuildingModifier(stats.trainedUnits);
            }
        }
        if (sync) KeepHeroes(stats);
        if (sync) foreach (var h in heroes.Values)
        {
            if (h == null || !h.IsAlive) continue;
            if (h.Stance != State.stance)
            {
                h.SetStance(State.stance, controller.TownZoneFor(transform.position), controller.SightRange);
                h.ReturnHome(controller.GetRallyPoint(this));
            }
            h.SetDefendArea(controller.SettlementFor(transform.position), controller.Settlement.Database.defendAlertRange);
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
        // Training needs the upkeep paid, and goes as fast as the building is staffed.
        if (controller.Settlement.UpkeepShort)
        {
            return;
        }
        trainTimer += Time.deltaTime * controller.Settlement.StaffingOf(State);
        if (trainTimer < controller.troopTrainSeconds)
        {
            return;
        }
        trainTimer = 0;

        Vector3 spawn = controller.Grid.AreaCenter(State.cell, Definition.size);
        var troop = controller.SpawnUnit(unit.prefab, spawn).GetComponent<Troop>();
        if (troop != null)
        {
            troop.Home = this;
            troop.SetRallyPoint(controller.GetRallyPoint(this));
            troop.SetStance(State.stance, controller.TownZoneFor(transform.position), controller.SightRange);
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
        foreach (var hero in heroes.Values)
        {
            if (hero != null)
            {
                Destroy(hero.gameObject);
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
