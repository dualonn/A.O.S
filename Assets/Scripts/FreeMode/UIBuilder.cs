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

    public static Button Button(string name, Transform parent, UnityAction onClick, out TextMeshProUGUI label,
        float height = 52, float fontSize = 18, Sprite icon = null)
    {
        var image = Panel(name, parent, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;

        label = Text("Label", image.transform, "", fontSize);
        Stretch(label.rectTransform, 8);
        label.alignment = TextAlignmentOptions.MidlineLeft;

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
