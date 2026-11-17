using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the HUD's windows in order. Centre windows (Government, Culture, the Chronicle...) show
/// one at a time; the one opened last is drawn on top, with the tooltip always over it; and Escape
/// closes the top window before it clears the selection. Small drop-downs (the event list, the
/// world list) close on Escape too, but don't push the centre windows away.
/// </summary>
public static class HUDWindows
{
    private class Entry
    {
        public RectTransform window;
        public Action close;
        public bool modal;
    }

    private static readonly List<Entry> entries = new List<Entry>();
    private static readonly List<RectTransform> opened = new List<RectTransform>();   // oldest first
    private static int escapeFrame = -1;
    private static bool escapeClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear();
        opened.Clear();
        escapeFrame = -1;
    }

    /// <summary>
    /// Tell the manager about a window and how to close it. 'modal' windows sit in the middle and
    /// close each other; the others are small drop-downs.
    /// </summary>
    public static void Register(RectTransform window, Action close, bool modal = true)
    {
        if (window == null) return;
        entries.RemoveAll(e => e.window == null || e.window == window);
        entries.Add(new Entry { window = window, close = close, modal = modal });
    }

    /// <summary>Call after showing a window: closes the others it replaces and brings it to the front.</summary>
    public static void Opened(RectTransform window)
    {
        if (window == null) return;
        entries.RemoveAll(e => e.window == null);
        var entry = entries.Find(e => e.window == window);
        bool modal = entry == null || entry.modal;
        foreach (var e in entries.ToArray())
        {
            if (e.window == window || !e.window.gameObject.activeInHierarchy) continue;
            // a centre window closes everything else; a drop-down only the other drop-downs
            if (modal || !e.modal) e.close?.Invoke();
        }
        opened.Remove(window);
        opened.Add(window);
        BringToFront(window);
    }

    /// <summary>Call when a window closes.</summary>
    public static void Closed(RectTransform window) => opened.Remove(window);

    /// <summary>A centre window is open (cards over the map wait until it's closed).</summary>
    public static bool AnyModalOpen
    {
        get
        {
            foreach (var e in entries)
                if (e.modal && e.window != null && e.window.gameObject.activeInHierarchy) return true;
            return false;
        }
    }

    /// <summary>Draw the window over the rest of the HUD (but under the tooltip).</summary>
    public static void BringToFront(RectTransform window)
    {
        Transform t = window;
        Transform top = null;
        while (t != null && t.GetComponent<Canvas>() == null)
        {
            t.SetAsLastSibling();
            top = t;
            t = t.parent;
        }
        if (t != null && top != null)
        {
            var tip = t.Find("Tooltip");
            if (tip != null) tip.SetAsLastSibling();
        }
    }

    /// <summary>Close the window on top. False if none is open.</summary>
    public static bool CloseTop()
    {
        opened.RemoveAll(w => w == null);
        for (int i = opened.Count - 1; i >= 0; i--)
        {
            var w = opened[i];
            if (!w.gameObject.activeInHierarchy) { opened.RemoveAt(i); continue; }
            var entry = entries.Find(e => e.window == w);
            if (entry == null || entry.close == null) { opened.RemoveAt(i); continue; }
            entry.close();
            return true;
        }
        // (opened some other way: any registered window that's showing)
        foreach (var e in entries.ToArray())
        {
            if (e.window == null || !e.window.gameObject.activeInHierarchy || e.close == null) continue;
            e.close();
            return true;
        }
        return false;
    }

    /// <summary>
    /// The one Escape handler: this frame's Escape closed a window (so nothing else should use it).
    /// Safe to call from several places in a frame; only the first call acts. Not while typing.
    /// </summary>
    public static bool EscapeClosedWindow()
    {
        if (Time.frameCount == escapeFrame) return escapeClosed;
        escapeFrame = Time.frameCount;
        escapeClosed = Input.GetKeyDown(KeyCode.Escape) && !UIBuilder.Typing && CloseTop();
        return escapeClosed;
    }
}

/// <summary>
/// Where the HUD's fixed panels are, so cards over the map can sit beside them and follow them
/// when they fold. Canvas units.
/// </summary>
public static class HUDLayout
{
    public static RectTransform Canvas, BuildPanel, ResearchPanel, SelectionPanel, UnitPanel;

    /// <summary>Unfold the side panels (e.g. before the advisor points at something in them).</summary>
    public static Action UnfoldBuild, UnfoldResearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Canvas = BuildPanel = ResearchPanel = SelectionPanel = UnitPanel = null;
        UnfoldBuild = UnfoldResearch = null;
    }

    public static float CanvasWidth => Canvas != null ? Canvas.rect.width : 1920f;

    /// <summary>From the left edge of the screen to the right edge of the build menu.</summary>
    public static float LeftInset => BuildPanel != null && BuildPanel.gameObject.activeInHierarchy
        ? BuildPanel.anchoredPosition.x + BuildPanel.rect.width : 0f;

    /// <summary>From the right edge of the screen to the left edge of the research panel.</summary>
    public static float RightInset => ResearchPanel != null && ResearchPanel.gameObject.activeInHierarchy
        ? -ResearchPanel.anchoredPosition.x + ResearchPanel.rect.width : 0f;

    /// <summary>The top of the bottom-centre panel (selected building or troops), or 0 when neither shows.</summary>
    public static float BottomPanelTop
    {
        get
        {
            float top = 0f;
            foreach (var p in new[] { SelectionPanel, UnitPanel })
                if (p != null && p.gameObject.activeInHierarchy) top = Mathf.Max(top, p.anchoredPosition.y + p.rect.height);
            return top;
        }
    }

    /// <summary>Half the width of the bottom-centre panel, or 0 when neither shows.</summary>
    public static float BottomPanelHalfWidth
    {
        get
        {
            float half = 0f;
            foreach (var p in new[] { SelectionPanel, UnitPanel })
                if (p != null && p.gameObject.activeInHierarchy) half = Mathf.Max(half, p.rect.width * 0.5f);
            return half;
        }
    }
}
