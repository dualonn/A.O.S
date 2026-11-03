using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small button under the top bar naming the world you're looking at; click it for a list of
/// your worlds and switch between them. Hidden while you only have one world.
/// </summary>
public class WorldSwitcher : MonoBehaviour
{
    private Settlement settlement;
    private Button button;
    private TextMeshProUGUI label;
    private RectTransform list;
    private readonly List<GameObject> rows = new List<GameObject>();
    private float refreshAt;
    private int shownCount = -1;

    public static WorldSwitcher Create(Transform root, Settlement settlement)
    {
        var go = new GameObject("WorldSwitcher", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        UIBuilder.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(260, 36));
        var s = go.AddComponent<WorldSwitcher>();
        s.settlement = settlement;
        s.button = UIBuilder.Button("Current", rt, s.Toggle, out s.label, 36, 17);
        UIBuilder.Stretch((RectTransform)s.button.transform);
        s.label.alignment = TextAlignmentOptions.Center;
        s.list = UIBuilder.VerticalPanel("Worlds", rt, 8, 4);
        UIBuilder.Anchor(s.list, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(300, 0));
        var fit = s.list.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        s.list.gameObject.SetActive(false);
        s.Refresh();
        return s;
    }

    private void Update()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.5f;
        Refresh();
    }

    private void Refresh()
    {
        if (settlement == null || settlement.State == null) return;
        var worlds = settlement.State.worlds;
        bool many = worlds.Count > 1;
        button.gameObject.SetActive(many);
        if (!many) { list.gameObject.SetActive(false); return; }
        var current = settlement.CurrentWorld;
        label.text = $"<b>{current.name}</b>  <size=80%><color=#9cf>{WorldProfile.Get(current.profile).displayName}</color>  v</size>";
        if (shownCount != worlds.Count) BuildRows();
    }

    private void BuildRows()
    {
        foreach (var r in rows) Destroy(r);
        rows.Clear();
        var worlds = settlement.State.worlds;
        shownCount = worlds.Count;
        for (int i = 0; i < worlds.Count; i++)
        {
            int index = i;
            var w = worlds[i];
            var b = UIBuilder.Button("World" + i, list, () => Pick(index), out var text, 40, 16);
            text.text = $"<b>{w.name}</b>\n<size=80%><color=#aaa>{WorldProfile.Get(w.profile).displayName}{(settlement.IsWorldFounded(w) ? "" : " · not settled")}</color></size>";
            rows.Add(b.gameObject);
        }
    }

    private void Toggle()
    {
        if (!list.gameObject.activeSelf) BuildRows();
        list.gameObject.SetActive(!list.gameObject.activeSelf);
    }

    private void Pick(int world)
    {
        list.gameObject.SetActive(false);
        settlement.SwitchWorld(world);
    }
}
