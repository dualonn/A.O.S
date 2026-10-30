using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A building that keeps up to maxTroops troops alive around it.</summary>
public class TroopSpawner : MonoBehaviour
{
    [Tooltip("Troop prefabs this building can spawn. Currently always spawns the first.")]
    public List<GameObject> troops = new List<GameObject>();

    [Tooltip("Troops spawned by this building that are still alive.")]
    public List<GameObject> thisTroops = new List<GameObject>();

    [Tooltip("Random delay range (seconds) before each spawn.")]
    public float minR;
    public float maxR;

    [Tooltip("Troops spawn within this distance of the building.")]
    public float radius;
    public int maxTroops;

    void Start()
    {
        StartCoroutine(Spawn());
    }

    private IEnumerator Spawn()
    {
        while (true)
        {
            thisTroops.RemoveAll(t => t == null);

            if (thisTroops.Count < maxTroops && troops.Count > 0)
            {
                yield return new WaitForSeconds(Random.Range(minR, maxR));
                var offset = new Vector3(Random.Range(-radius, radius), Random.Range(-radius, radius), 0);
                thisTroops.Add(Instantiate(troops[0], transform.position + offset, Quaternion.identity));
            }
            else
            {
                yield return null;
            }
        }
    }
}
