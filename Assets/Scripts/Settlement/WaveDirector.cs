using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An attack on the settlement.</summary>
public class Wave
{
    public List<UnitDefinition> units = new List<UnitDefinition>();

    /// <summary>Sum of UnitPower of every unit.</summary>
    public float strength;
}

/// <summary>
/// Implemented by the free mode scene. When one is registered, waves are fought for real
/// on screen; otherwise they are resolved by comparing strengths.
/// The battlefield must call Settlement.ApplyRaid for enemies that get through.
/// </summary>
public interface ILiveBattlefield
{
    void SpawnWave(Wave wave);
}

/// <summary>Sends random enemy waves at the settlement, scaled by the player's age and research.</summary>
public class WaveDirector : MonoBehaviour
{
    public ILiveBattlefield LiveBattlefield { get; set; }

    public event Action<Wave> WaveLaunched;

    /// <summary>Raised for auto-resolved waves. Second value: severity 0..1 (0 = fully held).</summary>
    public event Action<Wave, float> WaveAutoResolved;

    private Settlement settlement;
    private SettlementState State => settlement.State;
    private GameDatabase Database => settlement.Database;

    public float SecondsUntilWave => State.secondsUntilWave;

    public void Initialise(Settlement owner)
    {
        settlement = owner;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null || !settlement.IsFounded)
        {
            return;
        }
        State.secondsUntilWave -= Time.deltaTime;
        if (State.secondsUntilWave <= 0)
        {
            State.secondsUntilWave = UnityEngine.Random.Range(
                Database.waveIntervalRange.x,
                Database.waveIntervalRange.y
            );
            Launch(Generate());
        }
    }

    /// <summary>Build a random wave from enemy units of the current and earlier ages.</summary>
    public Wave Generate()
    {
        var wave = new Wave();
        var pool = new List<UnitDefinition>();
        for (int i = 0; i <= State.ageIndex; i++)
        {
            var age = Database.GetAge(i);
            if (age == null)
            {
                continue;
            }
            foreach (var unit in age.enemyUnits)
            {
                if (unit != null && Settlement.UnitPower(unit) > 0)
                {
                    pool.Add(unit);
                }
            }
        }
        if (pool.Count == 0)
        {
            return wave;
        }

        // Budget in power: the threat level counted in this age's enemies, so each age's waves
        // keep pace with that age's (roughly twice as strong) units.
        float agePower = 0f;
        int ageUnits = 0;
        var currentAge = Database.GetAge(State.ageIndex);
        if (currentAge != null)
        {
            foreach (var unit in currentAge.enemyUnits)
            {
                if (unit != null)
                {
                    agePower += Settlement.UnitPower(unit);
                    ageUnits++;
                }
            }
        }
        agePower = ageUnits > 0 ? agePower / ageUnits : 1f;
        float budget = settlement.ThreatLevel * agePower;
        int guard = 200;
        while (guard-- > 0)
        {
            var unit = pool[UnityEngine.Random.Range(0, pool.Count)];
            float power = Settlement.UnitPower(unit);
            // Always send at least one unit; then stop once the budget is spent.
            if (wave.units.Count > 0 && wave.strength + power > budget)
            {
                break;
            }
            wave.units.Add(unit);
            wave.strength += power;
        }
        return wave;
    }

    public void Launch(Wave wave)
    {
        if (wave.units.Count == 0)
        {
            return;
        }
        WaveLaunched?.Invoke(wave);

        if (LiveBattlefield != null)
        {
            LiveBattlefield.SpawnWave(wave);
        }
        else
        {
            AutoResolve(wave);
        }
    }

    /// <summary>Settle a wave nobody is watching: defence vs attack strength.</summary>
    public float AutoResolve(Wave wave)
    {
        float defense = settlement.DefensePower;
        float severity = wave.strength <= defense ? 0 : (wave.strength - defense) / wave.strength;

        State.wavesSurvived++;
        if (severity > 0)
        {
            settlement.ApplyRaid(severity);
        }
        else
        {
            Debug.Log($"[Settlement] A wave of {wave.units.Count} was driven off (attack {wave.strength:0.0} vs defence {defense:0.0}).");
        }
        WaveAutoResolved?.Invoke(wave, severity);
        return severity;
    }
}
