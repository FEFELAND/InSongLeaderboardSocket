using System.Collections.Generic;
using Newtonsoft.Json;

namespace InSongLeaderboardSocket;

internal sealed class OverlayState
{
    [JsonProperty("songName")] public string SongName { get; set; } = "";
    [JsonProperty("songAuthor")] public string SongAuthor { get; set; } = "";
    [JsonProperty("difficulty")] public string Difficulty { get; set; } = "";
    [JsonProperty("playerScore")] public int PlayerScore { get; set; }
    [JsonProperty("playerAccuracy")] public float PlayerAccuracy { get; set; }
    [JsonProperty("playerRank")] public int PlayerRank { get; set; }
    [JsonProperty("playerAccuracyRank")] public int PlayerAccuracyRank { get; set; }
    [JsonProperty("playerModifiers")] public string PlayerModifiers { get; set; } = "";
    [JsonProperty("leaderboard")] public List<LeaderboardEntry> Leaderboard { get; set; } = new();
    [JsonProperty("isActive")] public bool IsActive { get; set; }
    [JsonProperty("config")] public OverlayConfig Config { get; set; } = new();

    internal sealed class LeaderboardEntry
    {
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("score")] public int Score { get; set; }
        [JsonProperty("accuracy")] public float Accuracy { get; set; }
        [JsonProperty("avatarUrl")] public string AvatarUrl { get; set; } = "";
        [JsonProperty("isCurrentPlayer")] public bool IsCurrentPlayer { get; set; }
        [JsonProperty("isPersonalBest")] public bool IsPersonalBest { get; set; }
    }

    internal sealed class OverlayConfig
    {
        [JsonProperty("rows")] public int Rows { get; set; } = 10;
        [JsonProperty("showImages")] public bool ShowImages { get; set; }
        [JsonProperty("showStats")] public bool ShowStats { get; set; }
        [JsonProperty("showWhenIdle")] public bool ShowWhenIdle { get; set; } = true;
        [JsonProperty("fontSizeBase")] public int FontSizeBase { get; set; } = 25;
        [JsonProperty("backgroundOpacity")] public int BackgroundOpacity { get; set; } = 100;
        [JsonProperty("source")] public string Source { get; set; } = "BeatLeader";
        [JsonProperty("playerColor")] public string PlayerColor { get; set; } = "#66ccff";
        [JsonProperty("pbColor")] public string PbColor { get; set; } = "#ffcc33";
        [JsonProperty("textColor")] public string TextColor { get; set; } = "#ffffff";
        [JsonProperty("alignment")] public string Alignment { get; set; } = "left";
        [JsonProperty("fadeDepth")] public int FadeDepth { get; set; } = 5;
        [JsonProperty("rankByAccuracy")] public bool RankByAccuracy { get; set; }
    }

    public string ToJson() => JsonConvert.SerializeObject(this);
}
