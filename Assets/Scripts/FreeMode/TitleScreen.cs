using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title screen: the game's name and main menu over a living montage of towns, one age after
/// another (Stone Age to Universal Age), built in a throwaway world that is never saved
/// (Settlement.MenuMode). Continue, New Game and Load leave it through the usual change of game.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    // The montage picks an age at random each time (never the same one twice running).
    private const float AgeSeconds = 14f, FadeSeconds = 0.8f;

    private FreeModeController controller;
    private Settlement settlement;
    private RectTransform root;
    private Image fade;
    private TextMeshProUGUI ageLine, status;
    private Button continueButton;
    private GamesPanel games;
    private System.Action rescale;

    private void OnDestroy()
    {
        if (rescale != null) GameSettings.Changed -= rescale;
    }

    public static void Create(FreeModeController controller)
    {
        var go = new GameObject("TitleScreen");
        go.transform.SetParent(controller.transform, false);
        var t = go.AddComponent<TitleScreen>();
        t.controller = controller;
        t.settlement = controller.Settlement;
        t.Build();
    }

    // ---------- Building the screen ----------

    private void Build()
    {
        var canvasObject = new GameObject("TitleCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        GameSettings.ApplyUIScale(scaler);
        rescale = () => { if (scaler != null) GameSettings.ApplyUIScale(scaler); };
        GameSettings.Changed += rescale;
        canvasObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)canvasObject.transform;

        // The fade between towns darkens the world only (it sits under the menu).
        fade = UIBuilder.Panel("Fade", root, Color.black);
        UIBuilder.Stretch(fade.rectTransform);
        fade.raycastTarget = false;

        // A dark band down the left so the menu reads over any town.
        var band = UIBuilder.Panel("Band", root, new Color(0.03f, 0.03f, 0.05f, 0.72f));
        UIBuilder.Anchor(band.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(560, 0));
        band.raycastTarget = false;

        var column = UIBuilder.VerticalPanel("Menu", root, 0, 10);
        column.GetComponent<Image>().color = Color.clear;
        UIBuilder.Anchor(column, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 30), new Vector2(400, 0));

        var title = UIBuilder.Text("Title", column, "<b>AGE OF\nSTICKMAN</b>", 76);
        title.color = new Color(1f, 0.86f, 0.45f);
        title.lineSpacing = -18;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
        ageLine = UIBuilder.Text("Age", column, "", 22);
        ageLine.color = new Color(0.8f, 0.85f, 0.95f);
        Spacer(column, 26);

        continueButton = MenuButton(column, "Continue", OnContinue);
        MenuButton(column, "New Game", () => Games().OpenTab(true));
        MenuButton(column, "Load Game", () => Games().OpenTab(false));
        MenuButton(column, "Settings", () => SettingsPanel.Open(root));
        MenuButton(column, "Controls", () => ControlsPanel.Open(root));
        MenuButton(column, "Credits", () => CreditsPanel.Open(root));
#if !UNITY_WEBGL || UNITY_EDITOR
        MenuButton(column, "Quit", Quit);
#endif
        status = UIBuilder.Text("Status", column, "", 16);
        status.color = new Color(1f, 0.8f, 0.45f);
        status.textWrappingMode = TextWrappingModes.Normal;

        var version = UIBuilder.Text("Version", root, "v" + Application.version, 18, TextAlignmentOptions.BottomRight);
        version.color = new Color(1f, 1f, 1f, 0.55f);
        UIBuilder.Anchor(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(300, 30));

        RefreshContinue();
    }

    private static void Spacer(Transform parent, float height)
    {
        var s = UIBuilder.Rect("Space", parent);
        s.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private Button MenuButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        var b = UIBuilder.Button(text, parent, () => { GameAudio.UI("click"); status.text = ""; onClick(); }, out var label, UIBuilder.ButtonLarge, 24);
        label.text = text;
        b.GetComponent<LayoutElement>().preferredWidth = 360;
        return b;
    }

    private GamesPanel Games()
    {
        if (games == null) games = GamesPanel.Create(root, settlement, null, null, null, null);
        return games;
    }

    private void RefreshContinue()
    {
        bool can = Settlement.CanContinue;
        continueButton.interactable = can;
        continueButton.image.color = can ? UIBuilder.ButtonColor : UIBuilder.ButtonDisabledColor;
        var (summary, _) = Settlement.SlotSummary(Settlement.ActiveSlot);
        var label = continueButton.GetComponentInChildren<TextMeshProUGUI>();
        label.text = can && !string.IsNullOrEmpty(summary) && !Settlement.UsingTestSave
            ? $"Continue\n<size=60%><color=#bbb>{summary}</color></size>" : "Continue";
    }

    private void Update()
    {
        // Escape closes the window on top (Settings, Controls, Credits, Games).
        if (Input.GetKeyDown(KeyCode.Escape) && !UIBuilder.Typing) HUDWindows.EscapeClosedWindow();
    }

    // ---------- Buttons ----------

    private void OnContinue()
    {
        if (!Settlement.ContinueGame(out string why)) status.text = why;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- The montage ----------

    private IEnumerator Start()
    {
        // The game's own HUD and camera controls stay out of the way behind the title.
        yield return null;
        var hudCanvas = GameObject.Find("FreeModeCanvas");
        if (hudCanvas != null) hudCanvas.SetActive(false);
        var hud = FindFirstObjectByType<FreeModeHUD>();
        if (hud != null) hud.enabled = false;
        var cam = Camera.main;
        var camControl = cam != null ? cam.GetComponent<CameraController>() : null;
        if (camControl != null) camControl.enabled = false;
        Time.timeScale = 1f;

        var grid = controller.Grid;
        var home = grid.WorldToCell(cam.transform.position);
        int ages = Mathf.Max(1, settlement.Database.ages.Count), lastAge = -1;
        while (true)
        {
            int age = Random.Range(0, ages);
            if (age == lastAge && ages > 1) age = (age + 1 + Random.Range(0, ages - 1)) % ages;
            lastAge = age;
            yield return Fade(1f);
            // A new patch of land and a new layout every time.
            var centre = settlement.TitleFindCentre(home + new Vector2Int(Random.Range(-28, 29), Random.Range(-22, 23)));
            var core = settlement.TitleBuildTown(age, centre, out var clear, Random.Range(1, int.MaxValue));
            grid.ClearDepositTiles(clear);
            var ageData = settlement.Database.GetAge(age);
            ageLine.text = ageData != null ? $"<i>The {ageData.displayName}</i>" : "";
            RefreshContinue();

            // Frame the town to the right of the menu, then drift slowly across it.
            Vector3 middle = grid.CellToWorld(Vector2Int.zero) + new Vector3(core.center.x, core.center.y, 0);
            float ortho = Mathf.Clamp(Mathf.Max(core.width, core.height) * 0.42f + 3f, 7f, 25f);   // a hamlet close up; a mega-city fills the view (past 26 the map turns to its overview)
            float shift = ortho * cam.aspect * 0.28f;   // half a menu's width, in world units
            Vector3 from = middle + new Vector3(-shift - ortho * 0.18f, -ortho * 0.08f, 0);
            Vector3 to = middle + new Vector3(-shift + ortho * 0.18f, ortho * 0.08f, 0);
            float t0 = Time.unscaledTime;
            StartCoroutine(Fade(0f));
            while (Time.unscaledTime - t0 < AgeSeconds)
            {
                float t = (Time.unscaledTime - t0) / AgeSeconds;
                float e = t * t * (3f - 2f * t);
                var p = Vector3.Lerp(from, to, e);
                cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
                cam.orthographicSize = Mathf.Lerp(ortho * 1.04f, ortho * 0.94f, e);
                yield return null;
            }
        }
    }

    private IEnumerator Fade(float target)
    {
        float start = fade.color.a, t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < FadeSeconds)
        {
            float k = (Time.unscaledTime - t0) / FadeSeconds;
            fade.color = new Color(0, 0, 0, Mathf.Lerp(start, target, k));
            yield return null;
        }
        fade.color = new Color(0, 0, 0, target);
    }
}
