using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The random events' card: a title, a few lines of what's happening and up to three choices
/// (each saying what it costs and does). It never stops the game. Put it away with "Later" and it
/// waits behind a small badge under the top bar until it passes; the badge also lists the effects
/// in force and what happened recently.
/// </summary>
public class EventPanel : MonoBehaviour
{
    private Settlement settlement;
    private Button badge;
    private TextMeshProUGUI badgeLabel;
    private RectTransform card;
    private RectTransform cardBody;
    private RectTransform history;
    private TextMeshProUGUI historyText;
    private int shownId = -1, shownLog = -1;
    // (the log is capped, so its length alone stops changing: the last line and the effects count too)
    private string shownLast;
    private int shownEffects = -1;
    private readonly List<(Button button, EventChoice choice)> choiceButtons = new List<(Button, EventChoice)>();
    private bool cardOpen;
    private float refreshAt;
    private TextMeshProUGUI timerLabel;

    public static EventPanel Create(Transform root, Settlement settlement)
    {
        var go = new GameObject("EventPanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var image = go.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;
        var p = go.AddComponent<EventPanel>();
        p.settlement = settlement;
        p.Build((RectTransform)go.transform);
        settlement.EventArrived += p.OnArrived;
        EventVisitors.Create(settlement);
        return p;
    }

    private void OnDestroy()
    {
        if (settlement != null) settlement.EventArrived -= OnArrived;
    }

    private void Build(RectTransform rt)
    {
        badge = UIBuilder.Button("EventBadge", rt, ToggleHistory, out badgeLabel, 36, 18);
        UIBuilder.Anchor((RectTransform)badge.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(-136, -64), new Vector2(64, 36));
        badgeLabel.alignment = TextAlignmentOptions.Center;

        card = UIBuilder.VerticalPanel("EventCard", rt, 12, 6);
        UIBuilder.Anchor(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -106), new Vector2(460, 0));
        cardBody = card;   // (VerticalPanel already fits its height)
        card.gameObject.SetActive(false);

        history = UIBuilder.VerticalPanel("EventHistory", rt, 10, 4);
        UIBuilder.Anchor(history, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(-136, -104), new Vector2(380, 0));
        historyText = UIBuilder.Text("Lines", history, "", 15);
        history.gameObject.SetActive(false);
    }

    private void OnArrived(PendingEvent e)
    {
        GameAudio.UI("event_card");
        cardOpen = true;
        shownId = -1;
        refreshAt = 0;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.25f;
        var pending = settlement.PendingHere;
        var effects = settlement.EffectsHere();
        bool any = pending.Count > 0 || effects.Count > 0 || settlement.EventHistory.Count > 0;
        badge.gameObject.SetActive(any);
        if (!any) { card.gameObject.SetActive(false); history.gameObject.SetActive(false); return; }
        badgeLabel.text = pending.Count > 0 ? $"<color=#fd6><b>! {pending.Count}</b></color>" : effects.Count > 0 ? "<color=#9cf>*</color>" : "<color=#aaa>...</color>";

        var top = pending.Count > 0 ? pending[0] : null;
        if (top == null) cardOpen = false;
        card.gameObject.SetActive(cardOpen && top != null);
        if (cardOpen && top != null && top.id != shownId) BuildCard(top);
        if (cardOpen && top != null && timerLabel != null)
        {
            float left = Mathf.Max(0, top.expiresAt - settlement.State.playTime);
            timerLabel.text = $"<size=80%><color=#aaa>Passes in {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}</color></size>";
        }
        if (cardOpen && top != null)
            foreach (var (cb, cc) in choiceButtons) if (cb != null) cb.interactable = cc.why == null && settlement.CanAfford(cc.cost);
        var log = settlement.EventHistory;
        string last = log.Count > 0 ? log[log.Count - 1] : null;
        if (history.gameObject.activeSelf && (log.Count != shownLog || !ReferenceEquals(last, shownLast) || effects.Count != shownEffects)) RefreshHistory();
    }

    private void BuildCard(PendingEvent e)
    {
        shownId = e.id;
        foreach (Transform child in cardBody) Destroy(child.gameObject);
        timerLabel = null;
        choiceButtons.Clear();
        var view = settlement.Describe(e);
        if (view == null) { card.gameObject.SetActive(false); return; }
        var art = EventArt.Get();
        var head = UIBuilder.Row("Head", cardBody, 8);
        var icon = art != null ? art.IconFor(e.kind) : null;
        if (icon != null) UIBuilder.Icon("Icon", head, icon, 40);
        UIBuilder.Text("Title", head, $"<b>{view.title}</b>", 22);
        UIBuilder.Text("Text", cardBody, view.text, 16);
        for (int i = 0; i < view.choices.Count; i++)
        {
            int index = i;
            var c = view.choices[i];
            var sb = new StringBuilder($"<b>{c.label}</b>");
            if (c.cost.Count > 0) sb.Append($"\n<size=80%><color=#fc8>Costs {Settlement.Describe(c.cost)}</color></size>");
            if (!string.IsNullOrEmpty(c.result)) sb.Append($"\n<size=80%><color=#9e9>{c.result}</color></size>");
            if (c.why != null) sb.Append($"\n<size=80%><color=#f88>{c.why}</color></size>");
            var b = UIBuilder.Button("Choice" + i, cardBody, () => Choose(e.id, index), out var label, 30 + 18 * (sb.ToString().Split('\n').Length - 1), 16);
            label.text = sb.ToString();
            b.interactable = c.why == null && settlement.CanAfford(c.cost);
            choiceButtons.Add((b, c));
        }
        var foot = UIBuilder.Row("Foot", cardBody, 8);
        timerLabel = UIBuilder.Text("Timer", foot, "", 15);
        var later = UIBuilder.Button("Later", foot, () => { cardOpen = false; card.gameObject.SetActive(false); }, out var laterLabel, 30, 15);
        laterLabel.text = "Later";
        later.GetComponent<LayoutElement>().preferredWidth = 90;
    }

    private void Choose(int id, int choice)
    {
        string result = settlement.ResolveEvent(id, choice);
        shownId = -1;
        refreshAt = 0;
        if (result == null) return;
        // The next card (if another is waiting) shows straight away; otherwise the card closes.
        cardOpen = settlement.PendingHere.Count > 0;
    }

    private void ToggleHistory()
    {
        var pending = settlement.PendingHere;
        if (pending.Count > 0 && !cardOpen)
        {
            cardOpen = true;
            shownId = -1;
            refreshAt = 0;
            return;
        }
        history.gameObject.SetActive(!history.gameObject.activeSelf);
        if (history.gameObject.activeSelf) RefreshHistory();
    }

    private void RefreshHistory()
    {
        var log = settlement.EventHistory;
        shownLog = log.Count;
        shownLast = log.Count > 0 ? log[log.Count - 1] : null;
        shownEffects = settlement.EffectsHere().Count;
        var sb = new StringBuilder();
        var effects = settlement.EffectsHere();
        if (effects.Count > 0)
        {
            sb.AppendLine("<b>In force</b>");
            foreach (var x in effects) sb.AppendLine($"<color=#9cf>{x}</color>");
            sb.AppendLine();
        }
        sb.AppendLine("<b>Recent events</b>");
        if (log.Count == 0) sb.AppendLine("<color=#aaa>Nothing yet.</color>");
        for (int i = log.Count - 1; i >= 0 && i >= log.Count - 10; i--) sb.AppendLine($"<size=90%>{log[i]}</size>");
        historyText.text = sb.ToString();
    }
}
