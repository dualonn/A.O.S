using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The random events on the map: a fallen star or a lost cart waiting to be fetched (any of your
/// troops that reach it takes what's there), and a merchant caravan (or star trader) that comes
/// in to the town centre while its offer is open.
/// </summary>
public class EventVisitors : MonoBehaviour
{
    private Settlement settlement;
    private readonly Dictionary<int, GameObject> pickups = new Dictionary<int, GameObject>();
    private GameObject caravan;
    private SpriteRenderer caravanSprite;
    private int caravanFor = -1;
    private Vector3 caravanFrom, caravanTo;
    private float caravanT, checkAt;

    private const float Reach = 1.3f;

    public static EventVisitors Create(Settlement settlement)
    {
        var go = new GameObject("EventVisitors");
        var v = go.AddComponent<EventVisitors>();
        v.settlement = settlement;
        settlement.WorldSwitched += v.ClearAll;
        return v;
    }

    private void OnDestroy()
    {
        if (settlement != null) settlement.WorldSwitched -= ClearAll;
    }

    private void ClearAll()
    {
        foreach (var go in pickups.Values) if (go != null) Destroy(go);
        pickups.Clear();
        if (caravan != null) Destroy(caravan);
        caravan = null;
        caravanFor = -1;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        var art = EventArt.Get();
        UpdateCaravan(art);
        if (Time.unscaledTime < checkAt) return;
        checkAt = Time.unscaledTime + 0.2f;

        var here = settlement.PickupsHere;
        var keep = new HashSet<int>();
        foreach (var p in here)
        {
            keep.Add(p.id);
            if (!pickups.TryGetValue(p.id, out var go) || go == null)
            {
                go = new GameObject("Pickup " + p.label);
                go.transform.position = new Vector3(p.position.x, p.position.y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art != null ? (p.look == "meteor" ? art.meteor : art.cart) : null;
                sr.sortingOrder = BuildingView.SortingOrder;
                pickups[p.id] = go;
            }
            // Any of your troops close enough takes it.
            foreach (var u in Unit.Living)
            {
                if (u is Troop t && t.IsAlive && Vector2.Distance(t.transform.position, p.position) <= Reach)
                {
                    settlement.CollectPickup(p.id);
                    break;
                }
            }
        }
        var gone = new List<int>();
        foreach (var kv in pickups) if (!keep.Contains(kv.Key) || !settlement.PickupsHere.Exists(x => x.id == kv.Key)) gone.Add(kv.Key);
        foreach (int id in gone) { if (pickups[id] != null) Destroy(pickups[id]); pickups.Remove(id); }
    }

    /// <summary>The merchants come in while their offer stands, and leave when it's settled.</summary>
    private void UpdateCaravan(EventArt art)
    {
        var merchant = settlement.PendingHere.Find(e => e.kind == "Merchant");
        var controller = FreeModeController.Instance;
        if (merchant == null || controller == null)
        {
            if (caravan != null) { Destroy(caravan); caravan = null; caravanFor = -1; }
            return;
        }
        bool ship = settlement.State.ageIndex >= 12 || settlement.HasStarport;
        if (caravanFor != merchant.id || caravan == null)
        {
            if (caravan != null) Destroy(caravan);
            caravanFor = merchant.id;
            caravan = new GameObject(ship ? "Star Trader" : "Caravan");
            caravanSprite = caravan.AddComponent<SpriteRenderer>();
            caravanSprite.sortingOrder = BuildingView.SortingOrder + 1;
            caravanTo = controller.TownCenterPosition + new Vector3(3.5f, -2.5f, 0);
            caravanFrom = caravanTo + (ship ? new Vector3(0, 12f, 0) : new Vector3(12f, -3f, 0));
            caravanT = 0f;
        }
        caravanT = Mathf.Min(1f, caravanT + Time.deltaTime / 8f);
        caravan.transform.position = Vector3.Lerp(caravanFrom, caravanTo, Mathf.SmoothStep(0, 1, caravanT));
        if (art == null) return;
        if (ship) caravanSprite.sprite = art.starTrader;
        else if (art.caravanWalk.Count > 0)
            caravanSprite.sprite = caravanT < 1f ? art.caravanWalk[Mathf.FloorToInt(Time.time * 6f) % art.caravanWalk.Count] : art.caravanWalk[0];
        caravanSprite.flipX = !ship;
    }
}
