using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// War parties (Settlement/RaidMarch.cs) coming on in the flesh: they spawn where they are on
/// their march (just outside the town, or wherever your troops met them) and come in waves.
/// </summary>
public partial class FreeModeController
{
    private bool partyPending;
    private Vector3 partyOrigin;
    private CampState partyCamp;
    private Wave partyWave;
    private int partyGroups = 1;

    /// <summary>Seconds between the waves of one war party (vanguard, main body, rearguard).</summary>
    private const float PartyWaveGap = 9f;

    /// <summary>Can a war party join now? (Not while the last one is still coming on.)</summary>
    public bool CanTakeWarParty => !waveInProgress || unspawned.Count == 0;

    /// <summary>A war party reaches the town, or is met on its way: its raiders appear at 'at'.</summary>
    public void LandWarParty(Wave wave, Vector3 at, CampState camp, int groups)
    {
        if (wave == null || wave.units.Count == 0) return;
        if (!waveInProgress)
        {
            partyPending = true;
            partyOrigin = at;
            partyCamp = camp;
            partyWave = wave;
            partyGroups = Mathf.Max(1, groups);
            Settlement.Waves.Launch(wave);   // (WaveLaunched still fires: siege engines, allies)
            partyPending = false;
            return;
        }
        // A raid is on already: they join it, all at once.
        var zones = Settlement.SettlementZones();
        foreach (var unit in wave.units)
        {
            if (unit == null || unit.prefab == null) continue;
            Vector3 spawn = at;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 jitter = Random.insideUnitCircle * 2.5f;
                Vector3 p = at + new Vector3(jitter.x, jitter.y, 0);
                if (Settlement.Map.IsWalkable(p) && !Settlement.InAnyZone(zones, p)) { spawn = p; break; }
            }
            var enemy = SpawnUnit(unit.prefab, spawn).GetComponent<Enemy>();
            if (enemy == null) continue;
            enemy.StartRaid(this);
            liveEnemies.Add(enemy);
            enemy.InLiveWave = true;
        }
        GameAudio.UI("raid_warning", 0.7f);
        ShowMessage($"More raiders join the attack from the {CompassName(at - TownCenterPosition)}!");
    }

    /// <summary>SpawnWave asks: is this a war party coming on at a set spot? (Then it starts there.)</summary>
    private Vector3? RaidPartyOrigin(ref CampState camp)
    {
        if (!partyPending) { partyWave = null; return null; }   // an ordinary wave (rebels): no gaps
        partyPending = false;
        camp = partyCamp;
        return partyOrigin;
    }

    /// <summary>A pause between a war party's waves, as it spawns.</summary>
    private float RaidSpawnGap()
    {
        if (partyWave == null) return 0f;
        if (unspawned.Count == 0 || partyGroups <= 1)
        {
            if (unspawned.Count == 0) partyWave = null;
            return 0f;
        }
        int total = partyWave.units.Count;
        int done = total - unspawned.Count;
        int size = Mathf.Max(1, Mathf.CeilToInt(total / (float)partyGroups));
        return done > 0 && done % size == 0 ? PartyWaveGap : 0f;
    }
}
