using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The season and weather in the top bar: two small icons (hover for what they do). Before the
/// icons are made it shows the season's name instead.
/// </summary>
public class WeatherIndicator : MonoBehaviour
{
    private Settlement settlement;
    private Image seasonIcon, weatherIcon;
    private TextMeshProUGUI label;
    private float refreshIn;

    /// <summary>Put the indicator on the top bar, just left of 'rightOffset' from its right edge.</summary>
    public static WeatherIndicator Create(RectTransform bar, Settlement settlement, Action<Func<string>> showTooltip, UnityAction hideTooltip, float rightOffset = -606f)
    {
        var row = UIBuilder.Row("SeasonWeather", bar, 4);
        UIBuilder.Anchor(row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(rightOffset, 0), new Vector2(84, 40));
        row.gameObject.AddComponent<Image>().color = Color.clear;   // so the whole thing takes the hover
        var view = row.gameObject.AddComponent<WeatherIndicator>();
        view.settlement = settlement;
        var art = WeatherArt.Load();
        view.seasonIcon = UIBuilder.Icon("Season", row, null, 34);
        view.weatherIcon = UIBuilder.Icon("Weather", row, null, 34);
        view.label = UIBuilder.Text("Label", row, "", 18, TextAlignmentOptions.MidlineLeft);
        view.label.textWrappingMode = TextWrappingModes.NoWrap;
        UIBuilder.OnHover(row.gameObject, () => showTooltip(settlement.SeasonWeatherTooltip), hideTooltip);
        view.Refresh();
        return view;
    }

    private void Update()
    {
        refreshIn -= Time.unscaledDeltaTime;
        if (refreshIn > 0) return;
        refreshIn = 0.5f;
        Refresh();
    }

    private void Refresh()
    {
        if (settlement == null) return;
        var art = WeatherArt.Load();
        var season = settlement.CurrentSeason;
        var weather = settlement.CurrentWeather;
        Sprite s = art != null ? art.SeasonIcon(season) : null, w = art != null ? art.WeatherIcon(weather) : null;
        seasonIcon.sprite = s;
        weatherIcon.sprite = w;
        seasonIcon.gameObject.SetActive(s != null);
        weatherIcon.gameObject.SetActive(w != null);
        // No icons yet: a short name instead.
        bool text = s == null || w == null;
        label.gameObject.SetActive(text);
        if (text) label.text = $"{Settlement.SeasonName(season)}";
    }
}
