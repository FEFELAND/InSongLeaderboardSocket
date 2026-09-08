using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InSongLeaderboardSocket;

internal class SettingsHost
{
    private static readonly SettingsHost Host = new();

    // GitHub update banner (top of the settings). Checked in the background every
    // 10 minutes via RuntimeHooks and refreshed immediately when the settings open
    // if the last check is stale. Best-effort: any failure just leaves a neutral
    // message and never throws.
    private const string UpdateCheckUrl = "https://api.github.com/repos/FEFELAND/InSongLeaderboardSocket/releases/latest";
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(10);
    private static bool _updateCheckInProgress;
    private static DateTime _lastUpdateCheckUtc;
    private static string _updateStatus = "";

    private static readonly Version? CurrentAssemblyVersion =
        typeof(SettingsHost).Assembly.GetName().Version;

    [UIComponent("update-status-text")]
    private readonly TextMeshProUGUI _updateStatusText = null!;

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

        ApplyDisclaimerVisibility();

        ApplyUpdateStatusText();

        // Refresh the banner right away if the last check is stale (the background
        // loop keeps it fresh while the game runs).
        if (!_updateCheckInProgress && DateTime.UtcNow - _lastUpdateCheckUtc > UpdateCheckInterval)
            RuntimeHooks.RunCoroutine(RunUpdateCheck());
    }

    private static string FormatVersion(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

    /// <summary>Parses "v1.2.0", "1.2.0", "1.2.0-beta1", etc. into a comparable Version.</summary>
    private static Version? ParseVersionTag(string tag)
    {
        var s = tag.TrimStart('v', 'V');
        var dash = s.IndexOf('-');
        if (dash >= 0) s = s.Substring(0, dash);
        return Version.TryParse(s, out var v) ? v : null;
    }

    private static void SetUpdateStatusText(string text, string color)
    {
        var colored = "<color=" + color + ">" + text + "</color>";
        _updateStatus = colored;
        var comp = Host._updateStatusText;
        if (comp != null)
            comp.text = colored;
    }

    private void ApplyUpdateStatusText()
    {
        if (_updateStatusText == null)
            return;

        _updateStatusText.text = string.IsNullOrEmpty(_updateStatus)
            ? "Checking for updates..."
            : _updateStatus;
    }

    /// <summary>
    /// Fetches the latest GitHub release tag and compares it to the running
    /// assembly version. Runs on the main thread via a coroutine and always
    /// degrades to a neutral message (a 404 just means no public release yet).
    /// </summary>
    internal static IEnumerator RunUpdateCheck()
    {
        if (_updateCheckInProgress)
            yield break;

        _updateCheckInProgress = true;
        SetUpdateStatusText("Checking for updates...", "#9ca3af");

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(UpdateCheckUrl))
        {
            req.SetRequestHeader("User-Agent", "InSongLeaderboardSocket");
            req.timeout = 10;

            // Yield outside try/catch - Unity forbids yield inside catch blocks.
            yield return req.SendWebRequest();
            _lastUpdateCheckUtc = DateTime.UtcNow;

            try
            {
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var root = Newtonsoft.Json.Linq.JObject.Parse(req.downloadHandler.text);
                    var tag = root["tag_name"]?.ToString() ?? "";
                    var latest = ParseVersionTag(tag);
                    var current = CurrentAssemblyVersion;
                    if (latest != null && current != null)
                    {
                        if (latest > current)
                            SetUpdateStatusText("Update available: " + tag, "#fbbf24");
                        else
                            SetUpdateStatusText("Up to date: " + FormatVersion(current), "#4ade80");
                    }
                    else
                    {
                        SetUpdateStatusText("Couldn't read update info", "#9ca3af");
                    }
                }
                else if (req.responseCode == 404)
                {
                    // No release published yet (or repo not public).
                    SetUpdateStatusText("No GitHub release yet - nothing to update", "#9ca3af");
                }
                else
                {
                    SetUpdateStatusText("Couldn't check for updates", "#9ca3af");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Update check: {ex.Message}");
                SetUpdateStatusText("Couldn't check for updates", "#9ca3af");
            }
        }

        _updateCheckInProgress = false;
    }

    // ---------------------------------------------------------------------
    // First-time disclaimer: a set of plain BSML rows at the top of the
    // settings page. They are hidden by default so users who already accepted
    // see nothing, and #post-parse turns them on until "I understand" is
    // clicked. Once accepted the flag persists in the config forever.
    // ---------------------------------------------------------------------

    private static readonly string[] DisclaimerObjectIds =
    {
        "disclaimer-header",
        "disclaimer-text-1",
        "disclaimer-text-2",
        "disclaimer-footer",
        "disclaimer-button-row",
    };

    [UIObject("disclaimer-header")]
    private readonly GameObject _disclaimerHeader = null!;

    [UIObject("disclaimer-text-1")]
    private readonly GameObject _disclaimerText1 = null!;

    [UIObject("disclaimer-text-2")]
    private readonly GameObject _disclaimerText2 = null!;

    [UIObject("disclaimer-footer")]
    private readonly GameObject _disclaimerFooter = null!;

    [UIObject("disclaimer-button-row")]
    private readonly GameObject _disclaimerButtonRow = null!;

    private void ApplyDisclaimerVisibility()
    {
        var show = PluginConfig.Instance != null && !PluginConfig.Instance.ClickedDisclaimer;
        Plugin.Log.Debug($"Disclaimer visibility: show={show}, config.ClickedDisclaimer={PluginConfig.Instance?.ClickedDisclaimer}");

        SetDisclaimerVisible(show);
        RestrictSettings(show);
    }

    // When the disclaimer is showing, hide every other row in the settings
    // page so there is nothing left to scroll to or click but the disclaimer.
    private void RestrictSettings(bool disclaimerShowing)
    {
        var parent = _disclaimerHeader != null ? _disclaimerHeader.transform.parent : null;
        if (parent == null)
        {
            Plugin.Log.Warn("Disclaimer: could not locate the settings container.");
            return;
        }

        var hidden = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i).gameObject;
            if (!IsDisclaimerObject(child))
            {
                child.SetActive(!disclaimerShowing);
                hidden++;
            }
        }

        Plugin.Log.Debug($"Disclaimer: settings restricted to disclaimer only, toggled {hidden} setting row(s).");
    }

    private bool IsDisclaimerObject(GameObject go) =>
        ReferenceEquals(go, _disclaimerHeader) ||
        ReferenceEquals(go, _disclaimerText1) ||
        ReferenceEquals(go, _disclaimerText2) ||
        ReferenceEquals(go, _disclaimerFooter) ||
        ReferenceEquals(go, _disclaimerButtonRow);

    private void SetDisclaimerVisible(bool visible)
    {
        foreach (var id in DisclaimerObjectIds)
        {
            var go = GetDisclaimerObject(id);
            if (go != null)
                go.SetActive(visible);
            else
                Plugin.Log.Warn($"Disclaimer object '{id}' not bound.");
        }
    }

    private GameObject? GetDisclaimerObject(string id) => id switch
    {
        "disclaimer-header" => _disclaimerHeader,
        "disclaimer-text-1" => _disclaimerText1,
        "disclaimer-text-2" => _disclaimerText2,
        "disclaimer-footer" => _disclaimerFooter,
        "disclaimer-button-row" => _disclaimerButtonRow,
        _ => null,
    };

    [UIAction("accept-disclaimer")]
    private void AcceptDisclaimer()
    {
        Plugin.Log.Debug("InSongLB disclaimer accepted by user.");

        if (PluginConfig.Instance != null)
            PluginConfig.Instance.ClickedDisclaimer = true;

        // Re-run the visibility logic: with the flag now set this hides the
        // disclaimer rows and brings back all the settings rows that the
        // disclaimer page had hidden.
        ApplyDisclaimerVisibility();
        RuntimeHooks.RunCoroutine(RefreshLayoutAfterDisclaimer());
    }

    private static System.Collections.IEnumerator RefreshLayoutAfterDisclaimer()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
    }
}
