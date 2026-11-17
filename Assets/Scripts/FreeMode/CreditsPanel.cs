using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Credits window: who made the game, the sounds and music (Resources/Audio/CREDITS, every
/// line shown as written, since attribution licences ask for that) and the fonts. Scrolls.
/// Opens from the HUD or the title screen: CreditsPanel.Open(canvasRoot).
/// </summary>
public static class CreditsPanel
{
    /// <summary>The name on the "Made by" line.</summary>
    public const string Author = "Dualonn";

    private static RectTransform window;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        window = null;
    }

    private static readonly Color HeadingColor = new Color(0.8f, 0.85f, 0.95f);
    private static readonly Color FaintColor = new Color(1f, 1f, 1f, 0.7f);

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
        window = UIBuilder.Window("CreditsWindow", root, 680, "<b>Credits</b>", Close);
        HUDWindows.Register(window, Close);
        var list = UIBuilder.ScrollList("List", window, out var element, 4);
        element.preferredHeight = 620;

        // The game
        Line(list, "<b>Age of Stickman</b>", 30, TextAlignmentOptions.Center, true).margin = new Vector4(0, 8, 0, 0);
        Line(list, "Made by " + Author, UIBuilder.FontBody + 2, TextAlignmentOptions.Center, false);
        Line(list, "Version " + Application.version, UIBuilder.FontSmall, TextAlignmentOptions.Center, false).color = FaintColor;
        Line(list, "Built with Unity", UIBuilder.FontSmall, TextAlignmentOptions.Center, false).color = FaintColor;

        // Sounds and music
        Heading(list, "Sounds and music");
        var credits = Resources.Load<TextAsset>("Audio/CREDITS");
        if (credits == null)
        {
            Line(list, "(The sound credits file is missing.)", UIBuilder.FontSmall, TextAlignmentOptions.Left, false).color = FaintColor;
        }
        else
        {
            var paragraphs = Paragraphs(credits.text);
            for (int p = 0; p < paragraphs.Count; p++)
            {
                // The first paragraph is the file's own introduction; the rest are lists of packs and tracks.
                if (p > 0) SubHeading(list, Linked(paragraphs[p]) ? "Tracks and recordings" : "Sound packs");
                foreach (var line in paragraphs[p])
                {
                    var text = Line(list, line, UIBuilder.FontSmall - 1, TextAlignmentOptions.Left, false);
                    text.richText = false;   // shown exactly as written
                    if (p == 0) text.color = FaintColor;
                }
            }
        }

        // Fonts
        Heading(list, "Fonts");
        Line(list, "LiberationSans (the TextMesh Pro default font) - SIL Open Font License", UIBuilder.FontSmall - 1, TextAlignmentOptions.Left, false);
        Line(list, " ", 10, TextAlignmentOptions.Left, false);
        window.gameObject.SetActive(false);
    }

    /// <summary>The file's blank-line-separated blocks of lines (a leading byte-order mark dropped).</summary>
    private static List<List<string>> Paragraphs(string text)
    {
        var result = new List<List<string>>();
        List<string> current = null;
        foreach (var raw in text.TrimStart('﻿').Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { current = null; continue; }
            if (current == null) { current = new List<string>(); result.Add(current); }
            current.Add(line);
        }
        return result;
    }

    private static bool Linked(List<string> lines)
    {
        foreach (var line in lines)
            if (line.Contains("http")) return true;
        return false;
    }

    private static void Heading(Transform parent, string title)
    {
        var text = Line(parent, $"<b>{title}</b>", 20, TextAlignmentOptions.Left, true);
        text.color = HeadingColor;
        text.margin = new Vector4(0, 14, 0, 2);
    }

    private static void SubHeading(Transform parent, string title)
    {
        var text = Line(parent, $"<b>{title}</b>", UIBuilder.FontSmall + 1, TextAlignmentOptions.Left, true);
        text.color = HeadingColor;
        text.margin = new Vector4(0, 8, 0, 0);
    }

    private static TextMeshProUGUI Line(Transform parent, string content, float size, TextAlignmentOptions align, bool rich)
    {
        var text = UIBuilder.Text("Line", parent, content, size, align);
        text.richText = rich;   // plain lines show "<" and "&" as written
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = new Vector4(4, 0, 12, 0);
        return text;
    }
}
