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
        var trigger = target.AddComponent<EventTrigger>();
        var onEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        onEnter.callback.AddListener(_ => enter());
        trigger.triggers.Add(onEnter);
        var onExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        onExit.callback.AddListener(_ => exit());
        trigger.triggers.Add(onExit);
    }
}
