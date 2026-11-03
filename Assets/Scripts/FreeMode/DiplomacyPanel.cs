using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Talking to the people of a world: a small badge under the top bar (on worlds with natives)
/// showing where you stand, and a panel, opened from it or by clicking one of their camps, with
/// gifts, tribute, peace, trade and alliance. At home, clicking a raider camp offers to buy the
/// bandits off. Also brings allied warriors into the scene when they come to help.
/// </summary>
public class DiplomacyPanel : MonoBehaviour
{
    public static DiplomacyPanel Instance { get; private set; }

    private Settlement settlement;
    private RectTransform panel, actions;
    private TextMeshProUGUI title, info, badgeLabel, note;
    private Button badge;
    private float refreshAt;

    // Allied warriors in the scene, and when each goes home.
    private readonly List<(Enemy unit, float leaveAt)> allies = new List<(Enemy, float)>();

    private const float RaidHelpSeconds = 120f, WarPartySeconds = 150f;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static DiplomacyPanel Create(Transform root, Settlement settlement)
    {
        var go = new GameObject("Diplomacy", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<DiplomacyPanel>();
        p.settlement = settlement;
        p.Build((RectTransform)go.transform);
        Instance = p;
        if (settlement.Waves != null) settlement.Waves.WaveLaunched += p.OnWave;
        return p;
    }

    /// <summary>Open the panel for the world you're on (a camp was clicked). False if there's no panel.</summary>
    public static bool Open(Settlement s)
    {
        if (Instance == null || s == null || s.State == null) return false;
        Instance.Show(true);
        return true;
    }

    private void OnDestroy()
    {
        if (settlement != null && settlement.Waves != null) settlement.Waves.WaveLaunched -= OnWave;
        if (Instance == this) Instance = null;
    }

    private void Build(RectTransform root)
    {
        // The badge: who lives here and where you stand.
        badge = UIBuilder.Button("DiplomacyBadge", root, () => Show(!IsOpen), out badgeLabel, 32, 15);
        UIBuilder.Anchor((RectTransform)badge.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -64), new Vector2(220, 32));
        badgeLabel.alignment = TextAlignmentOptions.Center;
        badge.gameObject.SetActive(false);

        panel = UIBuilder.VerticalPanel("DiplomacyPanel", root, 12, 6);
        UIBuilder.Anchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -100), new Vector2(380, 0));
        var head = UIBuilder.Row("Head", panel, 6);
        title = UIBuilder.Text("Title", head, "", 22);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => Show(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;
        info = UIBuilder.Text("Info", panel, "", 15);
        info.textWrappingMode = TextWrappingModes.Normal;
        actions = UIBuilder.VerticalPanel("Actions", panel, 0, 4);
        actions.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        note = UIBuilder.Text("Note", panel, "", 14);
        note.textWrappingMode = TextWrappingModes.Normal;
        panel.gameObject.SetActive(false);
    }

    private void Show(bool open)
    {
        panel.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        if (open)
        {
            panel.SetAsLastSibling();
            var tip = panel.parent.parent != null ? panel.parent.parent.Find("Tooltip") : null;
            if (tip != null) tip.SetAsLastSibling();
            note.text = "";
            Rebuild();
        }
    }

    private void Update()
    {
        // Allies go home when their time is up (before the beguiling wears off and they turn on you).
        for (int i = allies.Count - 1; i >= 0; i--)
        {
            var (u, leaveAt) = allies[i];
            if (u == null || !u.IsAlive) { allies.RemoveAt(i); continue; }
            if (Time.time >= leaveAt) { Puff.Spawn(u.transform.position + Vector3.up * 0.4f, 0.6f, new Color(0.6f, 1f, 0.7f, 0.7f), 5, 0.4f); Destroy(u.gameObject); allies.RemoveAt(i); }
        }
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.5f;
        var f = settlement.NativesHere;
        bool showBadge = f != null && settlement.IsFounded;
        if (badge.gameObject.activeSelf != showBadge) badge.gameObject.SetActive(showBadge);
        if (showBadge)
        {
            var s = settlement.StandingHere;
            string col = ColorUtility.ToHtmlStringRGB(Settlement.StandingColour(s));
            string extra = settlement.Diplomacy.peace ? " · at peace" : settlement.Diplomacy.war ? " · at war" : "";
            badgeLabel.text = $"{f.name}: <color=#{col}>{Settlement.StandingName(s)}</color>{extra}";
        }
        if (IsOpen) RefreshInfo();
    }

    // ---------- Contents ----------

    private void RefreshInfo()
    {
        var f = settlement.NativesHere;
        var d = settlement.Diplomacy;
        float now = settlement.State.playTime;
        string paused = now < d.tributeUntil ? $"\n<color=#9f9>No raids here for {(d.tributeUntil - now) / 60f:0.0} more min.</color>" : "";
        if (f == null)
        {
            title.text = "<b>Bandits</b>";
            info.text = "The raider camps here are bandits. They'll never make peace, but they can be bought off for a while." + paused;
            return;
        }
        var s = settlement.StandingHere;
        string col = ColorUtility.ToHtmlStringRGB(Settlement.StandingColour(s));
        title.text = $"<b>The {f.name}</b>";
        string state = d.war ? "At war." : d.alliance ? "Allies: their warriors come when you're raided here." : d.trade ? "At peace, and trading." : d.peace ? "At peace: their camps leave your troops alone and they don't raid." : "No treaty: they raid you like anyone else.";
        info.text = $"Standing: <color=#{col}>{Settlement.StandingName(s)}</color> ({d.standing:0})\n{state}\n" +
                    $"<size=85%><color=#bbb>They value {Res(f.wants)}, and trade {Res(f.offers)} and their {f.goodsName}s. Killing their people and burning their camps sets them against you; a Xenologist here makes good will go further.</color></size>" + paused;
    }

    private string Res(string id) => settlement.Database.GetResource(id)?.displayName ?? id;

    private void Rebuild()
    {
        foreach (Transform child in actions) Destroy(child.gameObject);
        RefreshInfo();
        var f = settlement.NativesHere;
        var d = settlement.Diplomacy;
        if (f == null)
        {
            Action($"Buy them off ({Settlement.CostText(settlement.TributeCost())})", "Raids here stop for 8 minutes.", true, (out string w) => settlement.PayTribute(out w));
            return;
        }
        Action($"Send a gift ({Settlement.CostText(settlement.GiftCost())})", "Raises their standing.", true, (out string w) => settlement.SendGift(out w));
        Action($"Pay tribute ({Settlement.CostText(settlement.TributeCost())})", "They won't raid you for 8 minutes.", !d.peace, (out string w) => settlement.PayTribute(out w));
        if (!d.peace)
            Action($"Offer peace ({Settlement.CostText(settlement.PeaceCost())})", "Needs them Neutral or better.", d.standing >= Settlement.PeaceAt, (out string w) => settlement.OfferPeace(out w));
        if (d.peace && !d.trade)
            Action("Open trade", "Needs them Friendly, at peace.", d.standing >= Settlement.TradeAt, (out string w) => settlement.OpenTrade(out w));
        if (d.trade)
        {
            var (give, get, ok) = settlement.TradeOffer();
            if (ok) Action($"Trade {give.amount} {give.resource?.displayName} for {get.amount} {get.resource?.displayName}", "Better rates the more they like you.", true, (out string w) => settlement.Trade(out w));
            Action($"Buy a {f.goodsName} ({Settlement.CostText(settlement.GoodsCost())})", "Their own craft, for a hero (finer from allies).", true, (out string w) => settlement.BuyGoods(out w));
        }
        if (d.trade && !d.alliance)
            Action($"Form an alliance ({Settlement.CostText(settlement.AllianceCost())})", "Needs them very friendly (75).", d.standing >= Settlement.AllianceAt, (out string w) => settlement.FormAlliance(out w));
        if (d.alliance)
            Action($"Ask for a war party ({Settlement.CostText(settlement.WarPartyCost())})", "Warriors stand with you for a while.", true, (out string w) =>
            {
                if (!settlement.AskWarParty(out w)) return false;
                SpawnAllies(4, WarPartySeconds);
                return true;
            });
        if (!d.war)
            Action("<color=#f88>Declare war</color>", "Ends any treaty; they'll raid harder.", true, (out string w) => { settlement.DeclareWar(); w = null; return true; });
    }

    private delegate bool Act(out string why);

    private void Action(string text, string hint, bool enabled, Act act)
    {
        var b = UIBuilder.Button("Action", actions, () =>
        {
            bool ok = act(out string why);
            Rebuild();
            note.text = ok ? "" : $"<color=#f99>{why}</color>";
            if (!ok) GameAudio.UI("error");
        }, out var label, 34, 15);
        label.text = $"{text}\n<size=75%><color=#ccc>{hint}</color></size>";
        label.alignment = TextAlignmentOptions.Left;
        b.GetComponent<LayoutElement>().preferredHeight = 44;
        b.interactable = enabled;
    }

    // ---------- Allies in the scene ----------

    private void OnWave(Wave wave)
    {
        if (settlement == null || settlement.OffWorld || settlement.Waves.LiveBattlefield == null) return;
        if (settlement.NativesHere == null || !settlement.Diplomacy.alliance) return;
        SpawnAllies(3, RaidHelpSeconds);
    }

    /// <summary>A few of their warriors appear near your town and fight for you for a while.</summary>
    private void SpawnAllies(int count, float seconds)
    {
        var controller = FreeModeController.Instance;
        var kinds = settlement.NativeUnits();
        if (controller == null || kinds.Count == 0) return;
        Vector3 home = controller.TownCenterPosition;
        GameAudio.Play("war_declared", home, 0.6f);
        var map = settlement.Map;
        for (int i = 0; i < count; i++)
        {
            var def = kinds[i % kinds.Count];
            if (def.prefab == null) continue;
            Vector3 at = home;
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2 o = Random.insideUnitCircle * 4f + Random.insideUnitCircle.normalized * 3f;
                var p = home + new Vector3(o.x, o.y, 0);
                if (map == null || map.IsWalkable(p)) { at = p; break; }
            }
            var e = controller.SpawnUnit(def.prefab, at).GetComponent<Enemy>();
            if (e == null) continue;
            e.Charm(seconds + 5f);
            e.detectionRange = Mathf.Max(e.detectionRange, 14f);
            allies.Add((e, Time.time + seconds));
        }
    }
}
