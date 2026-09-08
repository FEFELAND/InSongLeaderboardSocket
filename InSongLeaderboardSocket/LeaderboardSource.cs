using System;

namespace InSongLeaderboardSocket;

internal enum LeaderboardSource
{
    ScoreSaber,
    BeatLeader
}

internal enum LeaderboardScope
{
    Global,
    Country,
    Following
}

internal sealed class LeaderboardRequest
{
    public string Hash { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Mode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public LeaderboardScope Scope { get; set; }
    public int Count { get; set; } = 10;

    public int ScoreSaberDifficulty => Difficulty switch
    {
        "Easy" => 1,
        "Normal" => 3,
        "Hard" => 5,
        "Expert" => 7,
        "ExpertPlus" => 9,
        _ => 0
    };

    public string ScoreSaberMode
    {
        get
        {
            if (string.IsNullOrEmpty(Mode) || Mode.StartsWith("Solo", StringComparison.Ordinal))
                return Mode;
            return "Solo" + Mode;
        }
    }

    public static int ClampCount(int value) => Math.Max(10, Math.Min(100, value));
}
