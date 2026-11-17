using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game pause menu: Escape, with no window open and nothing selected, stops the game and
/// offers Resume, Save, Settings, Controls, Main menu and Quit.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private static PauseMenu instance;

    private RectTransform root, panel, dim;
    private TextMeshProUGUI note;
    private float speedBefore = 1f;
    private System.Action rescale;

    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

    /// <summary>
    /// Escape while the pause menu is up: it closes (the game carries on). True if it took the key.
    /// (Windows opened from it, like Settings, close first through HUDWindows.)
    /// </summary>
    public static bool HandledEscape
    {
        get
        {
            if (!IsOpen) return false;
            instance.Show(false);
            return true;
        }
    }

    public static void Create(FreeModeController controller)
    {
        var go = new GameObject("PauseMenu");
        go.transform.SetParent(controller.transform, false);
        instance = go.AddComponent<PauseMenu>();
        instance.Build();
    }

    public static void Toggle()
    {
        if (instance != null) instance.Show(!IsOpen);
    }

    private void Build()
    {
        var canvasObject = new GameObject("PauseCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;   // over the HUD (10)
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        GameSettings.ApplyUIScale(scaler);
        rescale = () => { if (scaler != null) GameSettings.ApplyUIScale(scaler); };
        GameSettings.Changed += rescale;
        canvasObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)canvasObject.transform;

        // Dim the game behind, then a small column of choices in the middle.
        dim = UIBuilder.Panel("Dim", root, new Color(0, 0, 0, 0.55f)).rectTransform;
        UIBuilder.Stretch(dim);
        dim.gameObject.SetActive(false);
        panel = UIBuilder.VerticalPanel("Pause", root, 18, 10);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(380, 0));

        var title = UIBuilder.Text("Title", panel, "<b>Paused</b>", 30, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.86f, 0.45f);
        Item("Resume", () => Show(false));
        Item("Save game", SaveNow);
        Item("Settings", () => SettingsPanel.Open(root));
        Item("Controls", () => ControlsPanel.Open(root));
        Item("Main menu", ToTitle);
#if !UNITY_WEBGL || UNITY_EDITOR
        Item("Save and quit", Quit);
#endif
        note = UIBuilder.Text("Note", panel, "", 15, TextAlignmentOptions.Center);
        note.color = new Color(0.75f, 0.8f, 0.85f);
        var version = UIBuilder.Text("Version", panel, "<size=80%><color=#888>v" + Application.version + "</color></size>", 14, TextAlignmentOptions.Center);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.SetActive(false);
    }

    private CanvasGroup panelGroup;

    private void Update()
    {
        // Settings or Controls opened from here: step aside so the window reads cleanly.
        if (!IsOpen || panelGroup == null) return;
        bool behind = HUDWindows.AnyModalOpen;
        panelGroup.alpha = behind ? 0f : 1f;
        panelGroup.interactable = panelGroup.blocksRaycasts = !behind;
    }

    private void Item(string text, UnityEngine.Events.UnityAction onClick)
    {
        var b = UIBuilder.Button(text, panel, () => { GameAudio.UI("click"); onClick(); }, out var label, UIBuilder.ButtonNormal + 6, 20);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
    }

    private void Show(bool open)
    {
        if (panel == null || panel.gameObject.activeSelf == open) return;
        if (open)
        {
            speedBefore = Time.timeScale;
            Time.timeScale = 0f;
            note.text = "";
            dim.gameObject.SetActive(true);
            panel.gameObject.SetActive(true);
            dim.SetAsLastSibling();
            panel.SetAsLastSibling();
        }
        else
        {
            panel.gameObject.SetActive(false);
            dim.gameObject.SetActive(false);
            if (Time.timeScale == 0f) Time.timeScale = speedBefore > 0f ? speedBefore : 1f;
        }
    }

    private void SaveNow()
    {
        if (Settlement.Instance == null) return;
        Settlement.Instance.Save();
        note.text = Settlement.UsingTestSave ? "Saved (to the editor's test save)." : $"Saved at {System.DateTime.Now:HH:mm}.";
    }

    private void ToTitle()
    {
        Time.timeScale = speedBefore > 0f ? speedBefore : 1f;
        if (!Settlement.ReturnToTitle()) { note.text = "Can't leave just now."; Time.timeScale = 0f; }
    }

    private void Quit()
    {
        Settlement.Instance?.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (rescale != null) GameSettings.Changed -= rescale;
        if (instance == this) instance = null;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }
}
