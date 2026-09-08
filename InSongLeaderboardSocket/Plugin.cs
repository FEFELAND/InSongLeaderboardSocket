using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BS_Utils.Gameplay;
using BS_Utils.Utilities;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using System.Reflection;
using UnityEngine;
using Config = IPA.Config.Config;
using IPALogger = IPA.Logging.Logger;

namespace InSongLeaderboardSocket;

[Plugin(RuntimeOptions.SingleStartInit)]
public class Plugin
{
    private const string HarmonyId = "com.fefeland.InSongLeaderboardSocket";

    internal static IPALogger Log { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;

    internal static string CurrentPlayerId = string.Empty;
    internal static string CurrentPlayerName = string.Empty;
    internal static string PlatformPlayerId = string.Empty;
    internal static string PlatformPlayerName = string.Empty;

    private Harmony? _harmony;
    private OverlayServer? _wsServer;
    private OverlayState _overlayState = new();
    private readonly List<LeaderboardInfo> _remoteScores = new();
    private readonly object _fetchLock = new();
    private ScoreController? _scoreController;
    private bool _leaderboardLoaded;
    private bool _isReplay;
    private string _currentPlayerAvatar = string.Empty;
    private PluginConfig? _config;

    internal static BeatmapKey CurrentBeatmapKey;
    internal static bool HasCurrentBeatmapKey;

    [Init]
    public Plugin(IPALogger logger, Config config)
    {
        Instance = this;
        Log = logger;

        _config = config.Generated<PluginConfig>();
        PluginConfig.Instance = _config;
        _config.ConfigChanged += OnConfigChanged;

        Log.Info("InSongLeaderboardSocket initialized.");
    }

    [OnStart]
    public void OnApplicationStart()
    {
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll();

        _wsServer = new OverlayServer(38452);
        _wsServer.Start();

        BSEvents.gameSceneLoaded += OnGameSceneLoaded;
        BSEvents.menuSceneLoaded += OnMenuSceneLoaded;

        RuntimeHooks.EnsureCreated();
        RuntimeHooks.ScheduleSettingsMenuRegistration();
        RuntimeHooks.StartBackgroundUpdateChecks();
        _ = FetchUserInfoAsync();

        BroadcastConfig();
        Log.Info("Harmony patches applied, WebSocket server started on port 38452.");
    }

    [OnExit]
    public void OnApplicationQuit()
    {
        BSEvents.gameSceneLoaded -= OnGameSceneLoaded;
        BSEvents.menuSceneLoaded -= OnMenuSceneLoaded;

        _wsServer?.Stop();
        _wsServer = null;

        try { _harmony?.UnpatchSelf(); }
        catch { }

        Log.Info("InSongLeaderboardSocket unloaded.");
    }

    internal static void CaptureBeatmap(BeatmapKey beatmapKey)
    {
        CurrentBeatmapKey = beatmapKey;
        HasCurrentBeatmapKey = true;
        Log.Info(string.Format("Captured beatmap: {0} / {1}", beatmapKey.levelId, beatmapKey.difficulty));
    }

    private void OnConfigChanged()
    {
        BroadcastConfig();
    }

    private void BroadcastConfig()
    {
        if (_config == null) return;
        _overlayState.Config.Rows = _config.Rows;
        _overlayState.Config.ShowImages = _config.ShowImages;
        _overlayState.Config.ShowStats = _config.ShowStats;
        _overlayState.Config.ShowWhenIdle = _config.ShowWhenIdle;
        _overlayState.Config.FontSizeBase = _config.FontSizeBase;
        _overlayState.Config.BackgroundOpacity = _config.BackgroundOpacity;
        _overlayState.Config.Source = _config.Source;
        _overlayState.Config.PlayerColor = ColorToHex(_config.PlayerColor);
        _overlayState.Config.PbColor = ColorToHex(_config.PbColor);
        _overlayState.Config.TextColor = ColorToHex(_config.TextColor);
        _overlayState.Config.Alignment = _config.Alignment;
        _overlayState.Config.FadeDepth = _config.FadeDepth;
        _overlayState.Config.RankByAccuracy = _config.RankByAccuracy;
        BroadcastState();
    }

    private void OnMenuSceneLoaded()
    {
        CleanupGameScene();

        ApplyPlatformPlayerContext();

        _ = FetchUserInfoAsync();
    }

    private void CleanupGameScene()
    {
        BroadcastState();

        _leaderboardLoaded = false;
        _remoteScores.Clear();
        _isReplay = false;
        _currentPlayerAvatar = string.Empty;

        if (_scoreController != null)
        {
            _scoreController.scoreDidChangeEvent -= OnScoreChanged;
            _scoreController = null;
        }

        _overlayState = new OverlayState { IsActive = false };
        BroadcastConfig();
    }

    private void OnGameSceneLoaded()
    {
        if (!BS_Utils.Plugin.LevelData.IsSet ||
            BS_Utils.Plugin.LevelData.Mode != Mode.Standard)
        {
            return;
        }

        Log.Info("Game scene loaded, setting up leaderboard...");

        _leaderboardLoaded = false;
        _remoteScores.Clear();
        _isReplay = false;

        _overlayState = new OverlayState
        {
            IsActive = true,
            SongName = GetSongName(),
            SongAuthor = GetSongAuthor(),
            Difficulty = CurrentBeatmapKey.difficulty.ToString()
        };

        _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().LastOrDefault();
        if (_scoreController != null)
        {
            _scoreController.scoreDidChangeEvent += OnScoreChanged;
        }

        if (TryResolveReplayIdentity())
        {
            _isReplay = IsSteamId(CurrentPlayerId) &&
                        !string.Equals(CurrentPlayerId, PlatformPlayerId, StringComparison.OrdinalIgnoreCase);
        }

        if (!_isReplay)
            ApplyPlatformPlayerContext();

        BroadcastConfig();

        Task.Run(() => FetchLeaderboard());
    }

    private void OnScoreChanged(int score, int modifiedScore)
    {
        _overlayState.PlayerScore = modifiedScore > 0 ? modifiedScore : score;

        if (_scoreController != null)
        {
            var maxPossible = _scoreController.immediateMaxPossibleModifiedScore;
            _overlayState.PlayerAccuracy = maxPossible > 0 ? (float)_overlayState.PlayerScore / maxPossible : 0f;
        }

        UpdatePlayerRank();
        BroadcastState();
    }

    private void FetchLeaderboard()
    {
        lock (_fetchLock)
        {
            try
            {
                var playerIdSnapshot = CurrentPlayerId;
                var replaySnapshot = _isReplay;
                var levelId = CurrentBeatmapKey.levelId.Replace("custom_level_", "");
                var source = _config?.Source ?? "ScoreSaber";
                var scope = source == "BeatLeader"
                    ? DetectBeatLeaderScope()
                    : LeaderboardScope.Global;

                var request = new LeaderboardRequest
                {
                    Hash = levelId,
                    Difficulty = CurrentBeatmapKey.difficulty.ToString(),
                    Mode = CurrentBeatmapKey.beatmapCharacteristic.serializedName,
                    PlayerId = CurrentPlayerId,
                    Scope = scope,
                    Count = 50
                };

                Log.Info(string.Format("Fetching leaderboard: hash={0}, diff={1}, mode={2}, scope={3}", request.Hash, request.Difficulty, request.Mode, request.Scope));

                List<LeaderboardInfo>? scores = null;

                if (source == "ScoreSaber")
                {
                    scores = LeaderboardApiClient.FetchScores(LeaderboardSource.ScoreSaber, request);
                }
                else if (source == "BeatLeader")
                {
                    scores = LeaderboardApiClient.FetchScores(LeaderboardSource.BeatLeader, request);
                }

                if (scores != null && scores.Count > 0)
                {
                    if (!string.Equals(playerIdSnapshot, CurrentPlayerId, StringComparison.OrdinalIgnoreCase) ||
                        replaySnapshot != _isReplay)
                    {
                        Log.Info("Leaderboard fetch result ignored because the active player context changed while loading.");
                        return;
                    }

                    _remoteScores.Clear();
                    _remoteScores.AddRange(scores);
                    _leaderboardLoaded = true;
                    Log.Info(string.Format("Leaderboard loaded: {0} scores", _remoteScores.Count));

                    var pb = _remoteScores.FirstOrDefault(s =>
                        string.Equals(s.playerId, CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
                        ?? _remoteScores.FirstOrDefault(s => s.isPersonalBest);
                    if (pb != null)
                        pb.isPersonalBest = true;

                    var playerMissingFromLeaderboard = !_remoteScores.Any(s =>
                        string.Equals(s.playerId, CurrentPlayerId, StringComparison.OrdinalIgnoreCase));

                    if (_isReplay && playerMissingFromLeaderboard && !string.IsNullOrEmpty(CurrentPlayerId) &&
                        !string.Equals(CurrentPlayerId, PlatformPlayerId, StringComparison.OrdinalIgnoreCase))
                    {
                        string? apiName = null;
                        if (source == "BeatLeader")
                        {
                            apiName = LeaderboardApiClient.FetchPlayerName(LeaderboardSource.BeatLeader, CurrentPlayerId);
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.BeatLeader, CurrentPlayerId);
                        }
                        else if (source == "ScoreSaber")
                        {
                            apiName = LeaderboardApiClient.FetchPlayerName(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                        }
                        else
                        {
                            apiName = LeaderboardApiClient.FetchPlayerName(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                            if (string.IsNullOrEmpty(apiName))
                                apiName = LeaderboardApiClient.FetchPlayerName(LeaderboardSource.BeatLeader, CurrentPlayerId);
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                            if (string.IsNullOrEmpty(_currentPlayerAvatar))
                                _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.BeatLeader, CurrentPlayerId);
                        }

                        if (!string.IsNullOrEmpty(apiName) && !string.Equals(CurrentPlayerName, apiName, StringComparison.Ordinal))
                        {
                            CurrentPlayerName = apiName;
                            Log.Debug("Fetched replay player name from API: " + CurrentPlayerName);
                        }
                    }
                    else if (!_isReplay && playerMissingFromLeaderboard && !string.IsNullOrEmpty(CurrentPlayerId))
                    {
                        if (source == "BeatLeader")
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.BeatLeader, CurrentPlayerId);
                        else if (source == "ScoreSaber")
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                        else
                        {
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.ScoreSaber, CurrentPlayerId);
                            if (string.IsNullOrEmpty(_currentPlayerAvatar))
                                _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.BeatLeader, CurrentPlayerId);
                        }
                    }

                    RebuildLeaderboardEntries();
                    BroadcastState();
                }
                else
                {
                    Log.Warn("No leaderboard scores found from selected source.");
                    if (!string.IsNullOrEmpty(CurrentPlayerId))
                    {
                        var sourceEnum = source == "BeatLeader"
                            ? LeaderboardSource.BeatLeader
                            : LeaderboardSource.ScoreSaber;
                        _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(sourceEnum, CurrentPlayerId);
                        if (string.IsNullOrEmpty(_currentPlayerAvatar) && sourceEnum != LeaderboardSource.BeatLeader)
                            _currentPlayerAvatar = LeaderboardApiClient.FetchPlayerAvatar(LeaderboardSource.BeatLeader, CurrentPlayerId);
                        BroadcastState();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to fetch leaderboard: " + ex.Message);
            }
        }
    }

    private void RebuildLeaderboardEntries()
    {
        var all = new List<LeaderboardInfo>();

        var matchingRemote = _remoteScores.FirstOrDefault(s =>
            string.Equals(s.playerId, CurrentPlayerId, StringComparison.OrdinalIgnoreCase));

        if (matchingRemote != null)
            matchingRemote.isPersonalBest = true;

        foreach (var remote in _remoteScores)
        {
            if (string.Equals(remote.playerId, CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                if (!_remoteScores.Any(s => s.isCurrentPlayer))
                {
                    var pbEntry = CloneScore(remote);
                    pbEntry.isPersonalBest = true;
                    pbEntry.isCurrentPlayer = false;
                    all.Add(pbEntry);
                }
            }
            else
            {
                all.Add(CloneScore(remote));
            }
        }

        if (matchingRemote != null && !string.IsNullOrEmpty(matchingRemote.playerName) &&
            !string.Equals(CurrentPlayerName, matchingRemote.playerName, StringComparison.Ordinal))
        {
            CurrentPlayerName = matchingRemote.playerName;
            Plugin.Log.Debug("Updated current player name from leaderboard: " + CurrentPlayerName);
        }

        if (_isReplay && string.IsNullOrEmpty(CurrentPlayerName))
            CurrentPlayerName = "Spectating";

        var playerName = string.IsNullOrEmpty(CurrentPlayerName) ? "Player" : CurrentPlayerName;
        var playerAvatar = matchingRemote?.avatarUrl ?? _currentPlayerAvatar;
        var player = new LeaderboardInfo(
            CurrentPlayerId,
            playerName,
            _overlayState.PlayerScore,
            0,
            playerAvatar,
            true,
            _overlayState.PlayerAccuracy,
            _overlayState.PlayerModifiers,
            false);

        all.Add(player);

        all.Sort((a, b) => b.playerScore.CompareTo(a.playerScore));

        for (var i = 0; i < all.Count; i++)
            all[i].playerPosition = i + 1;

        _overlayState.Leaderboard.Clear();

        foreach (var entry in all)
        {
            _overlayState.Leaderboard.Add(new OverlayState.LeaderboardEntry
            {
                Rank = entry.playerPosition,
                Name = entry.playerName,
                Score = entry.playerScore,
                Accuracy = entry.playerAccuracy > 1.5f ? entry.playerAccuracy / 100f : entry.playerAccuracy,
                AvatarUrl = entry.avatarUrl ?? "",
                IsCurrentPlayer = entry.isCurrentPlayer,
                IsPersonalBest = entry.isPersonalBest
            });
        }

        var playerIndex = all.FindIndex(s => s.isCurrentPlayer);
        _overlayState.PlayerRank = playerIndex + 1;
    }

    private void UpdatePlayerRank()
    {
        var currentAcc = _overlayState.PlayerAccuracy;
        if (_remoteScores.Count > 0 && currentAcc > 0)
        {
            var accRank = 1;
            foreach (var s in _remoteScores)
                if (s.playerAccuracy > currentAcc) accRank++;
            _overlayState.PlayerAccuracyRank = accRank;
        }
        else
        {
            _overlayState.PlayerAccuracyRank = 0;
        }

        if (!_leaderboardLoaded)
        {
            var playerName = string.IsNullOrEmpty(CurrentPlayerName) ? "Player" : CurrentPlayerName;
            _overlayState.Leaderboard.Clear();
            _overlayState.Leaderboard.Add(new OverlayState.LeaderboardEntry
            {
                Rank = 1,
                Name = playerName,
                Score = _overlayState.PlayerScore,
                Accuracy = _overlayState.PlayerAccuracy,
                AvatarUrl = _currentPlayerAvatar,
                IsCurrentPlayer = true,
                IsPersonalBest = false
            });
            BroadcastState();
            return;
        }

        RebuildLeaderboardEntries();
    }

    internal static string? GetCurrentStateJson()
    {
        return Instance?._overlayState?.ToJson();
    }

    private void BroadcastState()
    {
        try
        {
            var json = _overlayState.ToJson();
            _wsServer?.Broadcast(json);
        }
        catch (Exception ex)
        {
            Log.Warn("Broadcast error: " + ex.Message);
        }
    }

    private string GetSongName()
    {
        try
        {
            if (!HasCurrentBeatmapKey) return "Unknown";
            if (!BS_Utils.Plugin.LevelData.IsSet) return "Unknown Song";

            var previewLevels = Resources.FindObjectsOfTypeAll<BeatmapLevelSO>();
            foreach (var level in previewLevels)
            {
                if (level.levelID == CurrentBeatmapKey.levelId)
                    return level.songName;
            }

            return CurrentBeatmapKey.levelId;
        }
        catch { return "Unknown Song"; }
    }

    private string GetSongAuthor()
    {
        try
        {
            if (!HasCurrentBeatmapKey) return "";
            if (!BS_Utils.Plugin.LevelData.IsSet) return "";

            var previewLevels = Resources.FindObjectsOfTypeAll<BeatmapLevelSO>();
            foreach (var level in previewLevels)
            {
                if (level.levelID == CurrentBeatmapKey.levelId)
                    return level.songAuthorName;
            }

            return "";
        }
        catch { return ""; }
    }

    private static LeaderboardInfo CloneScore(LeaderboardInfo source)
    {
        return new LeaderboardInfo(
            source.playerId,
            source.playerName,
            source.playerScore,
            source.playerPosition,
            source.avatarUrl,
            false,
            source.playerAccuracy,
            source.modifiers,
            source.isPersonalBest,
            source.playerBaseScore);
    }

    private static string ColorToHex(Color c)
    {
        return $"#{(byte)(c.r * 255):x2}{(byte)(c.g * 255):x2}{(byte)(c.b * 255):x2}";
    }

    private void ApplyPlatformPlayerContext()
    {
        if (!string.IsNullOrWhiteSpace(PlatformPlayerId))
            CurrentPlayerId = PlatformPlayerId;

        if (!string.IsNullOrWhiteSpace(PlatformPlayerName))
            CurrentPlayerName = PlatformPlayerName;
    }

    private static bool IsSteamId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 17)
            return false;
        if (!id.StartsWith("7656", StringComparison.Ordinal))
            return false;
        foreach (var c in id)
        {
            if (!char.IsDigit(c))
                return false;
        }
        return true;
    }

    private LeaderboardScope DetectBeatLeaderScope()
    {
        try
        {
            var blAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "BeatLeader");
            if (blAssembly == null)
                return LeaderboardScope.Global;

            var scopeFromValue = (Enum value) =>
            {
                var name = value.ToString();
                if (name.IndexOf("Friends", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Follow", StringComparison.OrdinalIgnoreCase) >= 0)
                    return LeaderboardScope.Following;
                if (name.IndexOf("Country", StringComparison.OrdinalIgnoreCase) >= 0)
                    return LeaderboardScope.Country;
                return LeaderboardScope.Global;
            };

            var stateType = blAssembly.GetType("BeatLeader.LeaderboardState");
            if (stateType != null)
            {
                var scopeProp = stateType.GetProperty("ScoresScope",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (scopeProp != null)
                {
                    var val = scopeProp.GetValue(null);
                    if (val is Enum e)
                        return scopeFromValue(e);
                }
            }

            foreach (var type in blAssembly.GetTypes())
            {
                if (type.IsEnum || type.IsInterface) continue;
                try
                {
                    var member = type.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                        .FirstOrDefault(m => (m is FieldInfo || m is PropertyInfo) &&
                            m.Name.IndexOf("Scope", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (member == null) continue;

                    object? val = member is FieldInfo f ? f.GetValue(null)
                        : member is PropertyInfo p ? p.GetValue(null) : null;
                    if (val is Enum e)
                        return scopeFromValue(e);
                }
                catch { }
            }
        }
        catch { }
        return LeaderboardScope.Global;
    }

    internal bool TryResolveReplayIdentity()
    {
        if (!ReplayIdentityResolver.TryResolveActiveReplayIdentity(CurrentBeatmapKey, out var replayPlayerId, out var replayPlayerName))
            return false;

        if (string.IsNullOrWhiteSpace(replayPlayerId))
        {
            Log.Debug("Replay identity resolver returned empty player ID; ignoring.");
            return false;
        }

        var changed = false;

        if (!string.Equals(CurrentPlayerId, replayPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            CurrentPlayerId = replayPlayerId;
            changed = true;
        }

        if (string.Equals(replayPlayerId, PlatformPlayerId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(PlatformPlayerName))
        {
            if (!string.Equals(CurrentPlayerName, PlatformPlayerName, StringComparison.Ordinal))
            {
                CurrentPlayerName = PlatformPlayerName;
                changed = true;
                RefreshCurrentLeaderboardEntryName(PlatformPlayerName);
            }
        }
        else if (!string.IsNullOrWhiteSpace(replayPlayerName) &&
                 !string.Equals(CurrentPlayerName, replayPlayerName, StringComparison.Ordinal))
        {
            CurrentPlayerName = replayPlayerName;
            changed = true;
            RefreshCurrentLeaderboardEntryName(replayPlayerName);
        }

        if (!_isReplay)
        {
            _isReplay = true;
            changed = true;
        }

        if (changed)
        {
            Log.Info(string.Format("Replay identity resolved: id={0}, name={1}", CurrentPlayerId, CurrentPlayerName));
            if (_overlayState.IsActive && _leaderboardLoaded)
                Task.Run(() => FetchLeaderboard());
        }

        return true;
    }

    private void RefreshCurrentLeaderboardEntryName(string playerName)
    {
        var current = _overlayState.Leaderboard.FirstOrDefault(entry => entry.IsCurrentPlayer);
        if (current == null)
            return;

        if (!string.Equals(current.Name, playerName, StringComparison.Ordinal))
        {
            current.Name = playerName;
            BroadcastState();
        }
    }

    private async Task FetchUserInfoAsync()
    {
        try
        {
            var userInfo = await GetUserInfo.GetUserAsync();
            PlatformPlayerId = userInfo.platformUserId;
            PlatformPlayerName = userInfo.userName;

            if (!_isReplay)
                ApplyPlatformPlayerContext();

            Log.Debug(string.Format(
                "Platform user: id={0}, name={1}, currentId={2}, currentName={3}",
                PlatformPlayerId,
                PlatformPlayerName,
                CurrentPlayerId,
                CurrentPlayerName));
        }
        catch (Exception ex)
        {
            Log.Warn("Could not read platform user: " + ex.Message);
        }
    }
}
