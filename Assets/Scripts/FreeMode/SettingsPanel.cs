using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Settings window: sound volumes, display (UI scale, and on PC the window mode, screen size
/// and VSync) and game options (autosave, hints). Changes apply at once and are saved through
/// GameSettings. Opens from the HUD or the title screen: SettingsPanel.Open(canvasRoot).
/// </summary>
public static class SettingsPanel
{
    private static RectTransform window, body;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        window = body = null;
    }

    public static bool IsOpen => window != null && window.gameObject.activeSelf;

    /// <summary>Show the window on this canvas (built the first time, or again if it was destroyed).</summary>
    public static void Open(Transform canvasRoot)
    {
        if (canvasRoot == null) return;
        if (window == null) Build(canvasRoot);
        else if (window.parent != canvasRoot) window.SetParent(canvasRoot, false);
        Rebuild();
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
        window = UIBuilder.Window("SettingsWindow", root, 560, "<b>Settings</b>", Close);
        HUDWindows.Register(window, Close);
        body = UIBuilder.Rect("Body", window);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var reset = UIBuilder.Button("Reset", window, () => { GameSettings.ResetToDefaults(); Rebuild(); }, out var resetLabel,
            UIBuilder.ButtonNormal, UIBuilder.FontBody);
        resetLabel.text = "Reset to defaults";
        resetLabel.alignment = TextAlignmentOptions.Center;
        window.gameObject.SetActive(false);
    }

    /// <summary>Fill the window from the current settings.</summary>
    private static void Rebuild()
    {
        for (int i = body.childCount - 1; i >= 0; i--)
        {
            var child = body.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }

        // Audio
        Heading("Audio");
        foreach (var channel in GameSettings.Channels)
        {
            string c = channel;
            TextMeshProUGUI value = null;
            UIBuilder.Slider(c, body, 0f, 1f, GameSettings.GetVolume(c), v =>
            {
                GameSettings.SetVolume(c, v);
                value.text = Percent(v);
            }, out value);
            value.text = Percent(GameSettings.GetVolume(c));
        }

        // Display
        Heading("Display");
        // The scale applies when the slider is let go (redrawing the canvas mid-drag would move the slider under the mouse).
        float pendingScale = GameSettings.UIScale;
        TextMeshProUGUI scaleValue = null;
        var scale = UIBuilder.Slider("UI scale", body, GameSettings.UIScaleMin, GameSettings.UIScaleMax, GameSettings.UIScale, v =>
        {
            pendingScale = Mathf.Round(v / GameSettings.UIScaleStep) * GameSettings.UIScaleStep;
            scaleValue.text = Percent(pendingScale);
        }, out scaleValue);
        scaleValue.text = Percent(GameSettings.UIScale);
        scale.gameObject.AddComponent<PointerUpHandler>().up = () =>
        {
            if (!Mathf.Approximately(pendingScale, GameSettings.UIScale)) GameSettings.UIScale = pendingScale;
        };

#if !UNITY_WEBGL || UNITY_EDITOR
        ToggleRow("Fullscreen", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
        StepperRow("Resolution", () => GameSettings.ResolutionLabel(GameSettings.ResolutionIndex), step =>
        {
            int count = GameSettings.Resolutions.Count;
            if (count == 0) return;
            GameSettings.ResolutionIndex = Mathf.Clamp(GameSettings.ResolutionIndex + step, 0, count - 1);
        });
        ToggleRow("VSync", () => GameSettings.VSync, v => GameSettings.VSync = v);
#endif

        // Game
        Heading("Game");
        StepperRow("Autosave every", () => AutosaveLabel(GameSettings.AutosaveMinutes), step =>
        {
            var options = GameSettings.AutosaveOptions;
            int i = Mathf.Clamp(Array.IndexOf(options, GameSettings.AutosaveMinutes) + step, 0, options.Length - 1);
            GameSettings.AutosaveMinutes = options[i];
        });
        ToggleRow("Show hints", () => GameSettings.ShowHints, v => GameSettings.ShowHints = v);
    }

    private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    private static string AutosaveLabel(float minutes) =>
        minutes < 1f ? Mathf.RoundToInt(minutes * 60f) + " seconds" : minutes == 1f ? "1 minute" : minutes.ToString("0.#") + " minutes";

    private static void Heading(string title)
    {
        var text = UIBuilder.Text(title, body, $"<b>{title}</b>", 18);
        text.color = new Color(0.8f, 0.85f, 0.95f);
        text.margin = new Vector4(0, 6, 0, 0);
    }

    /// <summary>A row with a caption on the left and room for controls on the right.</summary>
    private static RectTransform LabelRow(string caption)
    {
        var row = UIBuilder.Row(caption, body, 10);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = UIBuilder.ButtonSmall + 4;
        var text = UIBuilder.Text("Caption", row, caption, UIBuilder.FontBody, TextAlignmentOptions.MidlineLeft);
        var element = text.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 160;
        element.flexibleWidth = 1;
        return row;
    }

    /// <summary>An On/Off button, in the style of the Disasters switch.</summary>
    private static void ToggleRow(string caption, Func<bool> get, Action<bool> set)
    {
        var row = LabelRow(caption);
        TextMeshProUGUI label = null;
        UnityAction refresh = () => label.text = get() ? "<color=#8f8>On</color>" : "<color=#aaa>Off</color>";
        var button = UIBuilder.Button("Toggle", row, () => { set(!get()); refresh(); }, out label, UIBuilder.ButtonSmall + 4, UIBuilder.FontBody);
        label.alignment = TextAlignmentOptions.Center;
        button.GetComponent<LayoutElement>().preferredWidth = 200;
        refresh();
    }

    /// <summary>"&lt;  value  &gt;": the arrows step the choice down or up.</summary>
    private static void StepperRow(string caption, Func<string> get, Action<int> step)
    {
        var row = LabelRow(caption);
        TextMeshProUGUI value = null;
        StepButton(row, "<", () => { step(-1); value.text = get(); });
        value = UIBuilder.Text("Value", row, get(), UIBuilder.FontBody, TextAlignmentOptions.Center);
        var element = value.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 128;
        StepButton(row, ">", () => { step(1); value.text = get(); });
    }

    private static void StepButton(Transform row, string text, UnityAction onClick)
    {
        var button = UIBuilder.Button(text == "<" ? "Down" : "Up", row, onClick, out var label, UIBuilder.ButtonSmall + 4, UIBuilder.FontBody);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        var element = button.GetComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 32;
    }
}

/// <summary>Runs 'up' when the pointer is let go after pressing on the element (a click or the end of a drag).</summary>
public class PointerUpHandler : MonoBehaviour, IPointerUpHandler
{
    public Action up;
    public void OnPointerUp(PointerEventData eventData) => up?.Invoke();
}
