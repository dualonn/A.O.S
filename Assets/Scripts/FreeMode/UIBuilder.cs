using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small helpers for building uGUI from code.</summary>
public static class UIBuilder
{
    public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.28f, 1f);
    public static readonly Color ButtonDisabledColor = new Color(0.16f, 0.16f, 0.18f, 1f);
    /// <summary>Small cards that sit over the map (advisor, hints).</summary>
    public static readonly Color CardColor = new Color(0.07f, 0.08f, 0.1f, 0.9f);
    public static readonly Color TooltipColor = new Color(0.03f, 0.03f, 0.05f, 0.95f);
    /// <summary>The highlight on the tab that's showing.</summary>
    public static readonly Color TabActiveColor = new Color(0.35f, 0.3f, 0.42f, 1f);

    // Standard sizes, so windows and buttons look alike.
    public const float ButtonSmall = 28, ButtonNormal = 36, ButtonLarge = 52;
    public const float FontTitle = 22, FontBody = 16, FontSmall = 14;
    public const float TabHeight = 30, TabFont = 15;

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt, float padding = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.one * padding;
        rt.offsetMax = -Vector2.one * padding;
    }

    /// <summary>Optional rounded 9-sliced sprite for panels and buttons (set from the ArtLibrary).</summary>
    public static Sprite PanelSprite;

    public static Image Panel(string name, Transform parent, Color color)
    {
        var rt = Rect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        if (PanelSprite != null)
        {
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
        }
        return image;
    }

    public static Image Icon(string name, Transform parent, Sprite sprite, float size)
    {
        var rt = Rect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var element = rt.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = size;
        element.preferredHeight = element.minHeight = size;
        rt.sizeDelta = new Vector2(size, size);
        return image;
    }

    /// <summary>A row that lays out its children left to right.</summary>
    public static RectTransform Row(string name, Transform parent, float spacing = 6)
    {
        var rt = Rect(name, parent);
        var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rt;
    }

    /// <summary>A panel that stacks its children vertically and grows to fit them.</summary>
    public static RectTransform VerticalPanel(string name, Transform parent, int padding = 10, float spacing = 6)
    {
        var panel = Panel(name, parent, PanelColor);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return panel.rectTransform;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size = 20,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var rt = Rect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>
    /// A button with a label. ('multiLine' is kept for callers that ask for wrapping; labels wrap anyway.)
    /// </summary>
    public static Button Button(string name, Transform parent, UnityAction onClick, out TextMeshProUGUI label,
        float height = 52, float fontSize = 18, Sprite icon = null, bool multiLine = false)
    {
        var image = Panel(name, parent, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        button.onClick.AddListener(() => GameAudio.UI("click"));

        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;

        label = Text("Label", image.transform, "", fontSize);
        Stretch(label.rectTransform, 8);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        // (Labels keep TextMeshPro's usual wrapping. "..." cut-offs are no good here: TextMeshPro also
        // cuts vertically, and many buttons give their label less height than one line, which then
        // vanishes altogether.)

        if (icon != null)
        {
            float iconSize = height - 10;
            var iconImage = Icon("Icon", image.transform, icon, iconSize);
            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(6, 0);
            label.rectTransform.offsetMin = new Vector2(iconSize + 14, label.rectTransform.offsetMin.y);
        }
        return button;
    }

    /// <summary>Call 'enter'/'exit' when the pointer moves over/off a UI element.</summary>
    public static void OnHover(GameObject target, UnityAction enter, UnityAction exit)
    {
        // Only listens for enter/exit, so scroll and drag still reach a scroll list underneath
        // (an EventTrigger would swallow them).
        var hover = target.AddComponent<HoverHandler>();
        hover.enter = enter;
        hover.exit = exit;
    }

    /// <summary>A one-line text box with grey hint text; 'onChanged' runs on every keystroke.</summary>
    public static TMP_InputField SearchField(string name, Transform parent, string hint, UnityAction<string> onChanged, float height = 34)
    {
        var image = Panel(name, parent, new Color(0f, 0f, 0f, 0.4f));
        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = element.minHeight = height;
        var area = Rect("TextArea", image.transform);
        Stretch(area, 8);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = Text("Hint", area, hint, 16, TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform);
        placeholder.color = new Color(1, 1, 1, 0.4f);
        placeholder.fontStyle = FontStyles.Italic;
        var text = Text("Text", area, "", 16, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform);
        var field = image.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.onValueChanged.AddListener(onChanged);
        return field;
    }

    /// <summary>
    /// A labelled slider row: the caption ('name') on the left, the bar in the middle and a value
    /// label on the right (returned through 'label'; fill it in from 'onChange').
    /// </summary>
    public static Slider Slider(string name, Transform parent, float min, float max, float value,
        UnityAction<float> onChange, out TextMeshProUGUI label, float height = 32)
    {
        var row = Row(name, parent, 10);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        var caption = Text("Caption", row, name, FontBody, TextAlignmentOptions.MidlineLeft);
        var captionElement = caption.gameObject.AddComponent<LayoutElement>();
        captionElement.preferredWidth = captionElement.minWidth = 160;

        var root = Rect("Slider", row);
        var element = root.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1;
        element.preferredHeight = element.minHeight = height;

        // The groove, a little thinner than the row
        var back = Panel("Background", root, new Color(0f, 0f, 0f, 0.45f));
        Anchor(back.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));

        var fillArea = Rect("Fill Area", root);
        Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12, 10));
        var fill = Panel("Fill", fillArea, TabActiveColor);
        fill.rectTransform.sizeDelta = new Vector2(12, 0);

        var handleArea = Rect("Handle Slide Area", root);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(8, 0);
        handleArea.offsetMax = new Vector2(-8, 0);
        var handle = Panel("Handle", handleArea, new Color(0.85f, 0.85f, 0.9f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(16, -8);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;   // arrow keys pan the camera, not the slider
        slider.navigation = nav;
        if (onChange != null) slider.onValueChanged.AddListener(onChange);

        label = Text("Value", row, "", FontBody, TextAlignmentOptions.MidlineRight);
        var valueElement = label.gameObject.AddComponent<LayoutElement>();
        valueElement.preferredWidth = valueElement.minWidth = 64;
        return slider;
    }

    /// <summary>The standard close button: a small "X" (30 high, 34 wide) for a window's header row.</summary>
    public static Button CloseButton(Transform parent, UnityAction onClose)
    {
        var close = Button("Close", parent, onClose, out var label, 30, 16);
        var element = close.GetComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 34;
        label.text = "X";
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return close;
    }

    /// <summary>
    /// A window: a panel that grows with its contents, centred on the screen, with a header row
    /// (the title, then the X). Add things to the header (e.g. a crest at index 0) through 'header'.
    /// </summary>
    public static RectTransform Window(string name, Transform parent, float width, string title, UnityAction onClose,
        out TextMeshProUGUI titleLabel, out RectTransform header, int padding = 12)
    {
        var panel = VerticalPanel(name, parent, padding, 6);
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(width, 0));
        header = Row("Head", panel, 8);
        titleLabel = Text("Title", header, title, FontTitle);
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;
        titleLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        CloseButton(header, onClose);
        return panel;
    }

    public static RectTransform Window(string name, Transform parent, float width, string title, UnityAction onClose) =>
        Window(name, parent, width, title, onClose, out _, out _);

    /// <summary>A row of equal tabs (30 high, 15 point). 'onPick' gets the tab's index.</summary>
    public static System.Collections.Generic.List<Button> TabRow(Transform parent, string[] names, UnityAction<int> onPick)
    {
        var row = Row("Tabs", parent, 4);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        var buttons = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var b = Button(names[i], row, () => onPick(index), out var label, TabHeight, TabFont);
            label.text = names[i];
            label.alignment = TextAlignmentOptions.Center;
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            buttons.Add(b);
        }
        return buttons;
    }

    /// <summary>Light up the tab that's showing.</summary>
    public static void SetTab(System.Collections.Generic.IList<Button> tabs, int active, Color? activeColor = null)
    {
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i] != null) tabs[i].image.color = i == active ? activeColor ?? TabActiveColor : ButtonColor;
    }

    /// <summary>
    /// A number in a few characters: 950, 9,870, 23.8k, 1.2M. Whole numbers unless 'fraction', which
    /// keeps one decimal on small numbers (0.5, 12.5).
    /// </summary>
    public static string Short(float value, bool fraction = false)
    {
        if (fraction && Mathf.Abs(value) < 100f && !Mathf.Approximately(value, Mathf.Round(value))) return value.ToString("0.#");
        float v = Mathf.Floor(value);
        if (v < 10000f) return v.ToString("#,0");
        if (v < 1000000f) return (v / 1000f).ToString(v < 100000f ? "0.0" : "0") + "k";
        return (v / 1000000f).ToString("0.0") + "M";
    }

    /// <summary>True while the player is typing in a text box (so keys don't move the camera).</summary>
    public static bool Typing
    {
        get
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        }
    }

    /// <summary>
    /// A vertical scroll list: a masked viewport, a slim scrollbar that hides when not needed and a
    /// content area that stacks its children. Returns the content; size the list through 'element'.
    /// </summary>
    public static RectTransform ScrollList(string name, Transform parent, out LayoutElement element, float spacing = 6)
    {
        var root = Rect(name, parent);
        element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 200;
        // An invisible backdrop so the wheel works over gaps between items too.
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        var viewport = Rect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var track = Panel("Scrollbar", root, new Color(1, 1, 1, 0.06f));
        Anchor(track.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(8, 0));
        var handle = Panel("Handle", track.transform, new Color(1, 1, 1, 0.35f));
        Stretch(handle.rectTransform);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4;
        return content;
    }
}

/// <summary>Pointer enter/exit callbacks without catching any other events.</summary>
public class HoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UnityAction enter, exit;

    private bool hovered;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        enter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        exit?.Invoke();
    }

    // Hidden while hovered (e.g. its section folded): drop the tooltip too.
    private void OnDisable()
    {
        if (hovered)
        {
            hovered = false;
            exit?.Invoke();
        }
    }
}

/// <summary>
/// A titled section that folds open and shut: a header bar (arrow, title, a short note on the
/// right) above a body that holds the section's items.
/// </summary>
public class CollapsibleSection
{
    public readonly RectTransform Root;
    public readonly RectTransform Body;
    public bool Open { get; private set; } = true;

    /// <summary>Raised when the player clicks the header (not when the code sets it).</summary>
    public event System.Action<bool> Toggled;

    private readonly TextMeshProUGUI arrow;
    private readonly TextMeshProUGUI note;

    public CollapsibleSection(string name, Transform parent, string title, Color accent)
    {
        Root = UIBuilder.Rect(name, parent);
        var layout = Root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var header = UIBuilder.Button("Header", Root, () =>
        {
            SetOpen(!Open);
            Toggled?.Invoke(Open);
        }, out var titleLabel, 36, 19);
        header.image.color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.95f);
        titleLabel.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(accent)}>{title}</color></b>";
        titleLabel.rectTransform.offsetMin = new Vector2(30, titleLabel.rectTransform.offsetMin.y);

        arrow = UIBuilder.Text("Arrow", header.transform, ">", 20, TextAlignmentOptions.Center);
        UIBuilder.Anchor(arrow.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16, 0), new Vector2(20, 20));
        arrow.color = accent;

        note = UIBuilder.Text("Note", header.transform, "", 15, TextAlignmentOptions.MidlineRight);
        UIBuilder.Stretch(note.rectTransform, 10);
        note.color = new Color(1, 1, 1, 0.65f);

        Body = UIBuilder.Rect("Body", Root);
        var bodyLayout = Body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 5;
        bodyLayout.padding = new RectOffset(6, 0, 0, 4);
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        Open = open;
        Body.gameObject.SetActive(open);
        // The arrow points right when shut and down when open.
        arrow.rectTransform.localEulerAngles = new Vector3(0, 0, open ? -90 : 0);
    }

    public void SetNote(string text) => note.text = text;

    public void SetVisible(bool visible)
    {
        if (Root.gameObject.activeSelf != visible)
        {
            Root.gameObject.SetActive(visible);
        }
    }
}
