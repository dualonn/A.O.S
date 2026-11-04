using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The caravans' looks, by vehicle tier (ox wagon .. hover freighter): looping frames drawn side on,
/// facing right. Made by Tools > Age of Stickman > Setup Caravans (Assets/Resources/TradeLibrary.asset).
/// </summary>
public class TradeLibrary : ScriptableObject
{
    [System.Serializable]
    public class Vehicle
    {
        public string name;
        public List<Sprite> frames = new List<Sprite>();
        public float fps = 6f;
        [Tooltip("Where its smoke or exhaust comes out, in tiles from the pivot (facing right); 0,0 = none.")]
        public Vector2 smoke;
    }

    public List<Vehicle> vehicles = new List<Vehicle>();

    private static TradeLibrary cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    public static TradeLibrary Load()
    {
        if (cached == null) cached = Resources.Load<TradeLibrary>("TradeLibrary");
        return cached;
    }
}
