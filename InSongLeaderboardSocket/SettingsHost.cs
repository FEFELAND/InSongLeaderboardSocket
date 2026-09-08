using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace InSongLeaderboardSocket;

internal class SettingsHost
{
    private static readonly SettingsHost Host = new();

    internal static bool TryRegister()
    {
        try
        {
            if (BSMLSettings.Instance == null)
                return false;

            BSMLSettings.Instance.AddSettingsMenu("InSongLB-WS", "InSongLeaderboardSocket.Views.Settings.bsml", Host);
            Plugin.Log.Debug("InSongLeaderboard settings menu registered.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug("Settings menu registration attempt failed: " + ex.Message);
            return false;
        }
    }

    [UIValue("rows")] public int Rows
    {
        get => PluginConfig.Instance?.Rows ?? 10;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.Rows = value; }
    }

    [UIValue("show-images")] public bool ShowImages
    {
        get => PluginConfig.Instance?.ShowImages ?? false;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.ShowImages = value; }
    }

    [UIValue("show-stats")] public bool ShowStats
    {
        get => PluginConfig.Instance?.ShowStats ?? false;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.ShowStats = value; }
    }

    [UIValue("show-when-idle")] public bool ShowWhenIdle
    {
        get => PluginConfig.Instance?.ShowWhenIdle ?? true;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.ShowWhenIdle = value; }
    }

    [UIValue("font-size-base")] public int FontSizeBase
    {
        get => PluginConfig.Instance?.FontSizeBase ?? 25;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.FontSizeBase = value; }
    }

    [UIValue("background-opacity")] public int BackgroundOpacity
    {
        get => PluginConfig.Instance?.BackgroundOpacity ?? 100;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.BackgroundOpacity = value; }
    }

    [UIValue("source")] public string Source
    {
        get => PluginConfig.Instance?.Source ?? "BeatLeader";
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.Source = value; }
    }

    [UIValue("source-choices")]
    public List<object> SourceChoices => new() { "ScoreSaber", "BeatLeader" };

    [UIValue("alignment")] public string Alignment
    {
        get => PluginConfig.Instance?.Alignment ?? "left";
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.Alignment = value; }
    }

    [UIValue("alignment-choices")]
    public List<object> AlignmentChoices => new() { "left", "right" };

    [UIValue("player-color")] public Color PlayerColor
    {
        get => PluginConfig.Instance?.PlayerColor ?? new Color(0.4f, 0.8f, 1f);
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.PlayerColor = value; }
    }

    [UIValue("pb-color")] public Color PbColor
    {
        get => PluginConfig.Instance?.PbColor ?? new Color(1f, 0.8f, 0.2f);
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.PbColor = value; }
    }

    [UIValue("text-color")] public Color TextColor
    {
        get => PluginConfig.Instance?.TextColor ?? new Color(1f, 1f, 1f);
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.TextColor = value; }
    }

    [UIValue("fade-depth")] public int FadeDepth
    {
        get => PluginConfig.Instance?.FadeDepth ?? 5;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.FadeDepth = value; }
    }

    [UIValue("rank-by-accuracy")] public bool RankByAccuracy
    {
        get => PluginConfig.Instance?.RankByAccuracy ?? false;
        set { if (PluginConfig.Instance != null) PluginConfig.Instance.RankByAccuracy = value; }
    }

    private static readonly string OverlayUrl = "https://fefeland.github.io/InSongLeaderboardWSOverlay/";

    [UIValue("watermark-text")]
    public string WatermarkText { get; } = ComputeWatermarkText();

    private static string ComputeWatermarkText()
    {
        var v = typeof(SettingsHost).Assembly.GetName().Version;
        var vs = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
        return $"Version: {vs} | mod was vibe-coded";
    }

    [UIComponent("watermark-btn")]
    private readonly Button _watermarkBtn = null!;

    [UIValue("about-description")]
    public string AboutDescription =>
        "A mod that displays your current standing among the top 50 scores of a leaderboard to a web overlay. " +
        "Intended to be overlayed in OBS or anything you'd like.\n" +
        "As stated in the main page, all vibe-code, sorry. " +
        "But making sure things work as I intended them to.\n" +
        "-FEFELAND";

    [UIValue("about-references")]
    public string AboutReferences =>
        "PatsanMCK/InSongLeaderboard\n" +
        "ReadieFur/BSDataPuller\n" +
        "Kylemc1413/InSongLeaderboard\n" +
        "Aeroluna/Technicolor";

    [UIAction("copy-url-click")]
    public void CopyUrl()
    {
        var te = new TextEditor();
        te.text = OverlayUrl;
        te.SelectAll();
        te.Copy();
    }

    [UIAction("#post-parse")]
    private void PostParse()
    {
        try
        {
            if (_watermarkBtn == null) return;
            _watermarkBtn.interactable = false;
            var img = _watermarkBtn.GetComponent<Image>();
            if (img != null) img.color = Color.clear;
        }
        catch { }
    }
}
