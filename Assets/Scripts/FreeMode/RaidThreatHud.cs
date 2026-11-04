using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The top bar's raid chip as a threat meter (Settlement/RaidMarch.cs): "Quiet", "Restless camps",
/// "War party NE ~1 min", "Under attack". Hover it for the camps and war parties; click it to look
/// at the next war party (or the most restless camp).
/// </summary>
public static class RaidThreatHud
{
    private static string cachedTip;
    private static float tipAt;
    private static int focusIndex;

    public static void Hook(GameObject chip, Settlement settlement, Action<Func<string>> show, UnityAction hide)
    {
        if (chip == null || settlement == null) return;
        var image = chip.GetComponent<Image>();
        if (image == null) image = chip.AddComponent<Image>();
        image.color = Color.clear;
        UIBuilder.OnHover(chip, () => show(() => Tip(settlement)), hide);
        var button = chip.GetComponent<Button>();
        if (button == null) button = chip.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Focus(settlement));
    }

    public static string ChipText(Settlement settlement)
    {
        var c = FreeModeController.Instance;
        return settlement.RaidChipText(c != null && c.WaveInProgress);
    }

    private static string Tip(Settlement settlement)
    {
        if (cachedTip != null && Time.unscaledTime < tipAt) return cachedTip;
        tipAt = Time.unscaledTime + 0.5f;
        cachedTip = settlement.RaidTooltip();
        return cachedTip;
    }

    private static void Focus(Settlement settlement)
    {
        var c = FreeModeController.Instance;
        if (c == null) return;
        if (settlement.RaidFocusPoint(focusIndex++, out var point)) c.PanTo(point);
        else c.ShowMessage("No war parties in sight, and no camps in reach of your towns.");
    }
}
