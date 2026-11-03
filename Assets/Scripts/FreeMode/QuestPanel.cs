using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hero quests in the HUD, kept out of the top bar:
/// - a "Quests" section under Goals in the research panel: each quest and its current step
///   (hover for the whole quest), offers waiting, and the button for the quest log;
/// - the offer card (like the event cards): who asks, the story, the steps and the reward, with
///   Accept / Not now / Later;
/// - the quest log: quests under way with their steps, rewards, "Show on map", deliveries and
///   giving up, plus the tales and relics won so far.
/// </summary>
public class QuestPanel : MonoBehaviour
{
    private Settlement settlement;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private string sectionKey;

    private RectTransform card;
    private TextMeshProUGUI cardTimer;
    private int cardShownId = -1;
    private bool cardOpen;

    private RectTransform log;
    private RectTransform logBody;
    private string logKey;
    private int selected = -1;
    private bool showTales;

    private float refreshAt;

    public static QuestPanel Create(Transform root, Settlement settlement, Transform list, Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        var go = new GameObject("QuestPanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<QuestPanel>();
        p.settlement = settlement;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        p.Build((RectTransform)go.transform, list);
        settlement.QuestOffered += p.OnOffered;
        return p;
    }

    private void OnDestroy()
    {
        if (settlement != null) settlement.QuestOffered -= OnOffered;
    }

    private void Build(RectTransform rt, Transform list)
    {
        section = new CollapsibleSection("Quests", list, "Quests", new Color(1f, 0.78f, 0.4f));
        section.Toggled += _ => fit?.Invoke();
        section.Root.SetSiblingIndex(Mathf.Min(1, list.childCount - 1));   // just under Goals
        section.SetVisible(false);

        card = UIBuilder.VerticalPanel("QuestCard", rt, 12, 6);
        UIBuilder.Anchor(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -106), new Vector2(470, 0));
        card.gameObject.SetActive(false);

        log = UIBuilder.VerticalPanel("QuestLog", rt, 12, 6);
        UIBuilder.Anchor(log, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(600, 0));
        var head = UIBuilder.Row("Head", log, 8);
        UIBuilder.Text("Title", head, "<b>Quest Log</b>", 24).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => SetLog(false), out var closeLabel, 32, 16);
        closeLabel.text = "Close";
        closeLabel.alignment = TextAlignmentOptions.Center;
        close.GetComponent<LayoutElement>().preferredWidth = 90;
        logBody = UIBuilder.Rect("Body", log);
        var layout = logBody.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        log.gameObject.SetActive(false);
    }

    private void OnOffered(QuestState q)
    {
        cardOpen = true;
        cardShownId = -1;
        refreshAt = 0;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.5f;
        RefreshSection();
        RefreshCard();
        if (log.gameObject.activeSelf) RefreshLog();
    }

    // ---------- The section under Goals ----------

    private void RefreshSection()
    {
        var quests = settlement.Quests;
        var offers = settlement.QuestOffersHere;
        var sb = new StringBuilder();
        foreach (var o in offers) sb.Append('o').Append(o.id);
        foreach (var q in quests.active) sb.Append('q').Append(q.id).Append(':').Append(q.step).Append(settlement.QuestStepProgress(q, Settlement.CurrentStep(q) ?? new QuestStep()));
        sb.Append('t').Append(quests.tales.Count);
        string key = sb.ToString();
        bool any = offers.Count + quests.active.Count + quests.tales.Count + quests.completed > 0;
        section.SetVisible(any);
        if (key == sectionKey) return;
        sectionKey = key;
        foreach (Transform child in section.Body) Destroy(child.gameObject);
        section.SetNote(quests.active.Count > 0 ? $"{quests.active.Count}/{Settlement.MaxActiveQuests} under way" : quests.completed > 0 ? $"{quests.completed} done" : "");
        foreach (var o in offers)
        {
            var offer = o;
            var b = UIBuilder.Button("Offer" + o.id, section.Body, () => { cardOpen = true; cardShownId = -1; refreshAt = 0; }, out var label, 30, 15);
            label.text = $"<color=#fd6><b>!</b> {o.title}</color>";
            UIBuilder.OnHover(b.gameObject, () => setTip(() => "<b>A quest is offered</b>\nClick to read the card.\n\n" + settlement.QuestTooltip(offer)), () => hideTip());
        }
        foreach (var q in quests.active)
        {
            var quest = q;
            var s = Settlement.CurrentStep(q);
            var b = UIBuilder.Button("Quest" + q.id, section.Body, () => { selected = quest.id; SetLog(true); }, out var label, 44, 14);
            string where = q.world != settlement.State.currentWorld ? $" <color=#9cf>[{settlement.QuestWorldName(q)}]</color>" : "";
            label.text = $"<color=#ffd98a>{q.title}</color>{where}\n<size=85%><color=#ccc>{(s != null ? s.text : "")} {(s != null ? settlement.QuestStepProgress(q, s) : "")}</color></size>";
            UIBuilder.OnHover(b.gameObject, () => setTip(() => settlement.QuestTooltip(quest) + "\n<color=#aaa>Click to open the quest log.</color>"), () => hideTip());
        }
        var open = UIBuilder.Button("OpenLog", section.Body, () => SetLog(!log.gameObject.activeSelf), out var openLabel, 28, 14);
        openLabel.text = "<color=#aaa>Quest log...</color>";
        UIBuilder.OnHover(open.gameObject, () => setTip(() => "<b>Quest log</b>\nYour quests, their steps and rewards, and the tales and relics won so far.\nWanderers, lore stones, ruins, shrines, freed prisoners and native chiefs offer quests now and then."), () => hideTip());
        fit?.Invoke();
    }

    // ---------- The offer card ----------

    private void RefreshCard()
    {
        var offers = settlement.QuestOffersHere;
        var top = offers.Count > 0 ? offers[0] : null;
        if (top == null) cardOpen = false;
        // An event card waiting has the same spot under the top bar: it goes first.
        bool show = cardOpen && top != null && settlement.PendingHere.Count == 0;
        card.gameObject.SetActive(show);
        if (!show) return;
        if (top.id != cardShownId) BuildCard(top);
        if (cardTimer != null)
        {
            float left = Mathf.Max(0, top.expiresAt - settlement.State.playTime);
            cardTimer.text = $"<size=80%><color=#aaa>The offer stands for {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}</color></size>";
        }
    }

    private void BuildCard(QuestState q)
    {
        cardShownId = q.id;
        foreach (Transform child in card) Destroy(child.gameObject);
        var art = QuestArt.Get();
        var head = UIBuilder.Row("Head", card, 8);
        if (art != null && art.icon != null) UIBuilder.Icon("Icon", head, art.icon, 40);
        UIBuilder.Text("Title", head, $"<b>{q.title}</b>", 22);
        var sb = new StringBuilder();
        sb.Append($"<color=#aaa>{q.giver}:</color>\n<i>{q.intro}</i>\n");
        for (int i = 0; i < q.steps.Count; i++) sb.Append($"<color=#fd6>{i + 1}.</color> {q.steps[i].text}\n");
        sb.Append($"<color=#8f8>Reward: {settlement.QuestRewardText(q)}.</color>");
        UIBuilder.Text("Text", card, sb.ToString(), 16);
        bool can = settlement.CanAcceptQuest(q, out var why);
        var accept = UIBuilder.Button("Accept", card, () => { settlement.AcceptQuest(q.id); Close(); }, out var acceptLabel, why != null ? 48 : 34, 16);
        acceptLabel.text = "<b>Take up the quest</b>" + (why != null ? $"\n<size=80%><color=#f88>{why}</color></size>" : "");
        accept.interactable = can;
        var foot = UIBuilder.Row("Foot", card, 8);
        cardTimer = UIBuilder.Text("Timer", foot, "", 15);
        cardTimer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var no = UIBuilder.Button("Decline", foot, () => { settlement.DeclineQuest(q.id); Close(); }, out var noLabel, 30, 15);
        noLabel.text = "Not now";
        no.GetComponent<LayoutElement>().preferredWidth = 100;
        var later = UIBuilder.Button("Later", foot, () => { cardOpen = false; card.gameObject.SetActive(false); }, out var laterLabel, 30, 15);
        laterLabel.text = "Later";
        later.GetComponent<LayoutElement>().preferredWidth = 80;
    }

    private void Close()
    {
        cardShownId = -1;
        refreshAt = 0;
        cardOpen = settlement.QuestOffersHere.Count > 0;
        sectionKey = null;
        logKey = null;
    }

    // ---------- The quest log ----------

    private void SetLog(bool open)
    {
        log.gameObject.SetActive(open);
        logKey = null;
        if (open) RefreshLog();
    }

    private void RefreshLog()
    {
        var quests = settlement.Quests;
        var sb = new StringBuilder();
        sb.Append(selected).Append(showTales).Append(settlement.State.currentWorld);
        foreach (var q in quests.active)
        {
            var s = Settlement.CurrentStep(q);
            sb.Append('|').Append(q.id).Append(':').Append(q.step);
            if (s != null) { sb.Append(settlement.QuestStepProgress(q, s)); if (s.kind == QuestStepKind.Deliver) sb.Append(settlement.CanDeliver(q, out _)); }
        }
        foreach (var o in settlement.QuestOffersHere) sb.Append('o').Append(o.id);
        sb.Append('t').Append(quests.tales.Count).Append('r').Append(quests.relics.Count);
        string key = sb.ToString();
        if (key == logKey) return;
        logKey = key;
        foreach (Transform child in logBody) Destroy(child.gameObject);

        if (quests.active.Count == 0)
            UIBuilder.Text("Empty", logBody, "<size=85%><color=#aaa>No quests under way. Wanderers, lore stones, ruins, shrines, freed prisoners and native chiefs will bring you some; take them up from their card.</color></size>", 16);
        if (selected < 0 && quests.active.Count > 0) selected = quests.active[0].id;
        foreach (var q in quests.active) QuestBlock(q);

        foreach (var o in settlement.QuestOffersHere)
        {
            var b = UIBuilder.Button("Offer" + o.id, logBody, () => { cardOpen = true; cardShownId = -1; refreshAt = 0; }, out var label, 30, 15);
            label.text = $"<color=#fd6>! Offered:</color> {o.title} <color=#aaa>(read the card)</color>";
        }

        int tales = quests.tales.Count, relics = quests.relics.Count;
        if (tales + relics > 0)
        {
            var t = UIBuilder.Button("Tales", logBody, () => { showTales = !showTales; logKey = null; RefreshLog(); }, out var tLabel, 30, 15);
            tLabel.text = $"<color=#fd6>{(showTales ? "v" : ">")}</color> Tales and relics <color=#aaa>({tales} tales, {relics} relics, {quests.completed} quests done)</color>";
            if (showTales)
            {
                for (int i = tales - 1; i >= 0 && i >= tales - 8; i--)
                {
                    var tale = quests.tales[i];
                    var row = UIBuilder.Button("Tale" + i, logBody, () => { }, out var label, 26, 14);
                    label.text = $"<color=#e8c890>{tale.title}</color>";
                    UIBuilder.OnHover(row.gameObject, () => setTip(() => $"<b>{tale.title}</b>\n<i>{tale.text}</i>"), () => hideTip());
                }
                for (int i = relics - 1; i >= 0 && i >= relics - 8; i--)
                {
                    var relic = quests.relics[i];
                    var item = settlement.GetItem(relic.uid);
                    if (item == null) continue;
                    var row = UIBuilder.Button("Relic" + i, logBody, () => { }, out var label, 26, 14);
                    var wearer = settlement.WhoWears(item.uid);
                    label.text = $"<color=#{Items.RarityHex(item.rarity)}>{item.name}</color>{(wearer != null ? $" <color=#aaa>(worn by {wearer.name})</color>" : "")}";
                    UIBuilder.OnHover(row.gameObject, () => setTip(() => $"<b><color=#{Items.RarityHex(item.rarity)}>{item.name}</color></b>\n<i>{relic.lore}</i>\n<color=#aaa>From: {relic.quest}</color>\n" +
                        Items.Describe(Items.StatsOf(item, settlement.GetTemplate(item.template)))), () => hideTip());
                }
            }
        }
        var boons = settlement.QuestBoonsHeld();
        if (boons.Count > 0)
        {
            var row = UIBuilder.Button("Boons", logBody, () => { }, out var label, 26, 14);
            label.text = $"<color=#9df>Boons won: {boons.Count}/{settlement.QuestBoons().Count}</color>";
            UIBuilder.OnHover(row.gameObject, () =>
            {
                var b = new StringBuilder("<b>Boons for the realm</b>\n");
                foreach (var x in settlement.QuestBoonsHeld()) b.Append($"<color=#9df>{x.displayName}</color>: {x.description}\n");
                setTip(() => b.ToString().TrimEnd());
            }, () => hideTip());
        }
    }

    private void QuestBlock(QuestState q)
    {
        var s = Settlement.CurrentStep(q);
        bool here = q.world == settlement.State.currentWorld;
        bool open = selected == q.id;
        var head = UIBuilder.Button("Quest" + q.id, logBody, () => { selected = open ? 0 : q.id; logKey = null; RefreshLog(); }, out var headLabel, 32, 16);
        headLabel.text = $"<color=#fd6>{(open ? "v" : ">")}</color> <b>{q.title}</b>" + (settlement.State.worlds.Count > 1 ? $" <color=#9cf>[{settlement.QuestWorldName(q)}]</color>" : "");
        if (!open) return;

        var sb = new StringBuilder();
        sb.Append($"<size=90%><color=#aaa>{q.giver}:</color> <i>{q.intro}</i></size>\n");
        for (int i = 0; i < q.steps.Count; i++)
        {
            var st = q.steps[i];
            string mark = st.done ? "<color=#8f8>+</color>" : i == q.step ? "<color=#fd6>></color>" : "<color=#888>-</color>";
            string line = st.done ? $"<color=#9a9>{st.text}</color>" : i == q.step ? $"{st.text} <color=#aaa>{settlement.QuestStepProgress(q, st)}</color>" : $"<color=#999>{st.text}</color>";
            sb.Append($"{mark} {line}\n");
        }
        if (s != null && (s.kind == QuestStepKind.Travel || s.kind == QuestStepKind.Slay))
            sb.Append(s.kind == QuestStepKind.Slay
                ? "<size=85%><color=#f98>Bring a hero near the lair to wake it, with troops to back them up.</color></size>\n"
                : "<size=85%><color=#aaa>Select a hero and right-click the banner to send them.</color></size>\n");
        if (!here) sb.Append($"<size=85%><color=#9cf>This quest waits on {settlement.QuestWorldName(q)}.</color></size>\n");
        sb.Append($"<color=#8f8>Reward: {settlement.QuestRewardText(q)}.</color>");
        UIBuilder.Text("Detail", logBody, sb.ToString(), 15);

        var row = UIBuilder.Row("Actions", logBody, 6);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        bool hasSpot = QuestMarkers.Instance != null && QuestMarkers.TryGetSpot(settlement, q, out _);
        var show = UIBuilder.Button("Show", row, () => { QuestMarkers.Instance?.ShowOnMap(q); }, out var showLabel, 30, 15);
        showLabel.text = "Show on map";
        showLabel.alignment = TextAlignmentOptions.Center;
        show.interactable = hasSpot;
        if (s != null && s.kind == QuestStepKind.Deliver)
        {
            bool can = settlement.CanDeliver(q, out _);
            var give = UIBuilder.Button("Deliver", row, () => { settlement.QuestDeliver(q.id); logKey = null; }, out var giveLabel, 30, 15);
            var res = settlement.Database.GetResource(s.resource);
            giveLabel.text = $"Deliver {s.amount} {(res != null ? res.displayName : s.resource)}";
            giveLabel.alignment = TextAlignmentOptions.Center;
            give.interactable = can;
        }
        var quit = UIBuilder.Button("Abandon", row, () => { settlement.AbandonQuest(q.id); selected = -1; logKey = null; }, out var quitLabel, 30, 15);
        quitLabel.text = "<color=#f99>Give up</color>";
        quitLabel.alignment = TextAlignmentOptions.Center;
        quit.image.color = new Color(0.32f, 0.16f, 0.16f);
    }
}
