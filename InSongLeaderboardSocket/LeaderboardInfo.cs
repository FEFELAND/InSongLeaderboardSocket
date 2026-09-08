namespace InSongLeaderboardSocket;

#pragma warning disable CS8618
public class LeaderboardInfo
{
    public string playerId = string.Empty;
    public string playerName = string.Empty;
    public string avatarUrl = string.Empty;
    public int playerPosition;
    public int playerScore;
    public int playerBaseScore;
    public float playerAccuracy;
    public string modifiers = string.Empty;
    public bool isCurrentPlayer;
    public bool isPersonalBest;

    public LeaderboardInfo() { }

    public LeaderboardInfo(
        string id,
        string name,
        int score,
        int position,
        string avatar,
        bool currentPlayer,
        float accuracy = -1f,
        string scoreModifiers = "",
        bool personalBest = false,
        int baseScore = -1)
    {
        playerId = id ?? string.Empty;
        playerName = name;
        avatarUrl = avatar ?? string.Empty;
        playerScore = score;
        playerBaseScore = baseScore;
        playerPosition = position;
        playerAccuracy = accuracy;
        modifiers = scoreModifiers ?? string.Empty;
        isCurrentPlayer = currentPlayer;
        isPersonalBest = personalBest;
    }
}
#pragma warning restore CS8618
