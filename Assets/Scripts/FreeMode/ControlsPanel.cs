using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Controls window: every key and mouse control, grouped, read-only. Opens from the HUD or the
/// title screen: ControlsPanel.Open(canvasRoot). Keep the list in step with the input code
/// (CameraController, BuildingPlacer, FreeModeController, FreeModeHUD, GuideHints, XRecordsPanel).
/// </summary>
public static class ControlsPanel
{
    private static RectTransform window;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        window = null;
    }

    // Each group: a title, then pairs of (control, what it does).
    private static readonly (string title, string[] rows)[] Groups =
    {
        ("Camera", new[]
        {
            "W A S D / arrow keys", "Pan the map",
            "Middle mouse drag", "Drag the map",
            "Mouse wheel", "Zoom in and out at the pointer",
            "+ / -  (or keypad + / -)", "Zoom in and out",
        }),
        ("Building", new[]
        {
            "Left click", "Place the building you picked",
            "Shift + left click", "Place it and keep placing more",
            "Left drag", "Walls, roads, rail and aqueducts: drag out a line, release to build",
            "Right click / Esc", "Stop placing",
            "B", "Fold or unfold the build menu",
            "R", "Fold or unfold the research panel",
        }),
        ("Selection & army", new[]
        {
            "Left click", "Select a building or troop",
            "Left drag", "Box-select (troops first, otherwise buildings)",
            "Shift + click / drag", "Add to or take from the selection",
            "Double-click a building", "Select every building of that type on screen",
            "Ctrl + double-click", "...of that type on the whole map",
            "Double-click a troop", "Select every troop of that kind on screen",
            "Right click (troops selected)", "Move there and hold the spot",
            "Right click a raider camp", "Attack the camp",
            "Right click a dungeon", "Send the troops into the dungeon",
            "Click a raider camp", "Talk to the raiders (nothing selected)",
            "Esc", "Clear the selection",
        }),
        ("Windows", new[]
        {
            "H / F1", "Open or close the help",
            "Esc", "Close the window on top",
            "Esc", "Pause menu (in game, with nothing open)",
        }),
        ("Game speed", new[]
        {
            "[  /  ]", "Slower / faster",
            "P / Pause", "Pause or resume",
        }),
    };

    public static bool IsOpen => window != null && window.gameObject.activeSelf;

    /// <summary>Show the window on this canvas (built the first time, or again if it was destroyed).</summary>
    public static void Open(Transform canvasRoot)
    {
        if (canvasRoot == null) return;
        if (window == null) Build(canvasRoot);
        else if (window.parent != canvasRoot) window.SetParent(canvasRoot, false);
        window.gameObject.SetActive(true);
        HUDWindows.Opened(window);   // closes the other centre windows, and draws this one on top
    }

    public static void Close()
    {
        if (window == null) return;
        window.gameObject.SetActive(false);
        HUDWindows.Closed(window);
    }

    public static void Toggle(Transform canvasRoot)
    {
        if (IsOpen) Close();
        else Open(canvasRoot);
    }

    private static void Build(Transform root)
    {
        window = UIBuilder.Window("ControlsWindow", root, 640, "<b>Controls</b>", Close);
        HUDWindows.Register(window, Close);
        var list = UIBuilder.ScrollList("List", window, out var element, 3);
        element.preferredHeight = 600;

        foreach (var group in Groups)
        {
            var heading = UIBuilder.Text(group.title, list, $"<b>{group.title}</b>", 18);
            heading.color = new Color(0.8f, 0.85f, 0.95f);
            heading.margin = new Vector4(0, 8, 0, 2);
            for (int i = 0; i + 1 < group.rows.Length; i += 2)
                ControlRow(list, group.rows[i], group.rows[i + 1], (i / 2) % 2 == 0);
        }
        window.gameObject.SetActive(false);
    }

    /// <summary>Control on the left, what it does on the right; every other row lightly shaded.</summary>
    private static void ControlRow(Transform parent, string control, string action, bool shaded)
    {
        var back = UIBuilder.Panel("Row", parent, new Color(1, 1, 1, shaded ? 0.04f : 0f));
        back.raycastTarget = false;
        var layout = back.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 3, 3);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var key = UIBuilder.Text("Control", back.transform, control, UIBuilder.FontSmall + 1, TextAlignmentOptions.MidlineLeft);
        key.richText = false;
        key.color = new Color(1f, 0.88f, 0.6f);
        key.fontStyle = FontStyles.Bold;
        var keyElement = key.gameObject.AddComponent<LayoutElement>();
        keyElement.preferredWidth = keyElement.minWidth = 230;

        var what = UIBuilder.Text("Action", back.transform, action, UIBuilder.FontSmall + 1, TextAlignmentOptions.MidlineLeft);
        what.richText = false;
        what.textWrappingMode = TextWrappingModes.Normal;
        what.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }
}
