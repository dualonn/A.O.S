using UnityEngine;

/// <summary>
/// The decorations a comfortable home puts out in front (more as it grows prosperous and grand).
/// Added to house views by the census panel; checks its home's comfort every couple of seconds.
/// </summary>
public class PeopleHouseDecor : MonoBehaviour
{
    private BuildingView view;
    private Settlement settlement;
    private SpriteRenderer sr;
    private float checkAt;
    private int shown = -1;

    public static void Attach(BuildingView view, Settlement settlement, float cellSize)
    {
        if (view == null || view.GetComponent<PeopleHouseDecor>() != null) return;
        var d = view.gameObject.AddComponent<PeopleHouseDecor>();
        d.view = view;
        d.settlement = settlement;
        var go = new GameObject("Comfort");
        go.transform.SetParent(view.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.004f);   // just in front of the house
        go.transform.localScale = Vector3.one * cellSize;
        d.sr = go.AddComponent<SpriteRenderer>();
        d.sr.sortingOrder = BuildingView.SortingOrder;
        d.sr.enabled = false;
        d.checkAt = Time.unscaledTime + Random.Range(0f, 2f);
    }

    private void Update()
    {
        if (Time.unscaledTime < checkAt || view == null || settlement == null || settlement.State == null) return;
        checkAt = Time.unscaledTime + 2f;
        var state = view.State;
        int level = state != null && state.IsBuilt && state.health > 0 ? settlement.PeopleComfortOf(state.instanceId) : 0;
        if (level == shown) return;
        shown = level;
        var art = PeopleArt.Get();
        var def = view.Definition;
        var sprite = art != null && def != null ? art.DecorFor(def.age != null ? def.age.order : 0, level) : null;
        sr.sprite = sprite;
        sr.enabled = sprite != null;
    }
}
