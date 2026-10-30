using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Shows the stats of the troop under the mouse.</summary>
public class UIManagement : MonoBehaviour
{
    public static UIManagement Instance { get; private set; }

    [Tooltip("Fallback icons by Troop.ID, used when a troop has no UnitDefinition icon.")]
    public List<Sprite> images = new List<Sprite>();
    public Image troopIconImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI attackPowerText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI attackSpeedText;

    private Troop shownTroop;

    void Awake()
    {
        Instance = this;
        Clear();
    }

    public void ShowTroop(Troop troop)
    {
        shownTroop = troop;
    }

    public void HideTroop(Troop troop)
    {
        // Only clear if this troop is the one being shown, so other troops can't blank the panel.
        if (shownTroop == troop)
        {
            shownTroop = null;
        }
    }

    void Update()
    {
        if (shownTroop == null)
        {
            Clear();
            return;
        }

        SetText(healthText, $"{shownTroop.health}/{shownTroop.maxHealth}");
        SetText(attackPowerText, shownTroop.attackPower.ToString());
        SetText(speedText, shownTroop.moveSpeed.ToString("0.0"));
        SetText(attackSpeedText, $"{shownTroop.attackCooldown:0.0}s");

        if (troopIconImage != null)
        {
            Sprite icon = shownTroop.definition != null ? shownTroop.definition.icon : null;
            if (icon == null && shownTroop.ID >= 0 && shownTroop.ID < images.Count)
            {
                icon = images[shownTroop.ID];
            }
            troopIconImage.sprite = icon;
        }
    }

    private void Clear()
    {
        SetText(healthText, "");
        SetText(attackPowerText, "");
        SetText(speedText, "");
        SetText(attackSpeedText, "");
        if (troopIconImage != null)
        {
            troopIconImage.sprite = null;
        }
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
