using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace InSongLeaderboardSocket;

internal static class LeaderboardApiClient
{
    private const string ScoreSaberBaseUrl = "https://scoresaber.com";
    private const string BeatLeaderBaseUrl = "https://api.beatleader.xyz";

    public static List<LeaderboardInfo>? FetchScores(
        LeaderboardSource source,
        LeaderboardRequest request)
    {
        try
        {
            return source == LeaderboardSource.BeatLeader
                ? FetchBeatLeaderScores(request)
                : FetchScoreSaberScores(request);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("FetchScores failed: " + ex.Message);
            return null;
        }
    }

    public static string FetchPlayerAvatar(LeaderboardSource source, string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return string.Empty;

        try
        {
            var escapedId = Uri.EscapeDataString(playerId);
            var url = source == LeaderboardSource.BeatLeader
                ? BeatLeaderBaseUrl + "/player/" + escapedId
                : ScoreSaberBaseUrl + "/api/v2/players/" + escapedId + "/basic";

            var json = DownloadString(url);
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            var root = JToken.Parse(json!);
            var player = root["player"] ?? root["data"] ?? root;
            var avatar = ReadString(player, "avatar", "profilePicture", "profilePictureUrl");
            return NormalizeAvatarUrl(avatar, source);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string FetchPlayerName(LeaderboardSource source, string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return string.Empty;

        try
        {
            var escapedId = Uri.EscapeDataString(playerId);
            var url = source == LeaderboardSource.BeatLeader
                ? BeatLeaderBaseUrl + "/player/" + escapedId
                : ScoreSaberBaseUrl + "/api/v2/players/" + escapedId + "/basic";

            var json = DownloadString(url);
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            var root = JToken.Parse(json!);
            var player = root["player"] ?? root["data"] ?? root;
            return ReadString(player, "name", "playerName", "username", "displayName", "playerNameInGame");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<LeaderboardInfo>? FetchBeatLeaderScores(LeaderboardRequest request)
    {
        var count = LeaderboardRequest.ClampCount(request.Count);
        var scope = request.Scope == LeaderboardScope.Country
            ? "country"
            : request.Scope == LeaderboardScope.Following ? "friends" : "global";
        var playerQuery = string.IsNullOrEmpty(request.PlayerId)
            ? string.Empty
            : "&player=" + Uri.EscapeDataString(request.PlayerId);
        var pageSize = Math.Min(50, count);
        var pageCount = Math.Max(1, (count + pageSize - 1) / pageSize);
        var allScores = new List<LeaderboardInfo>();

        for (var page = 1; page <= pageCount && allScores.Count < count; page++)
        {
            var url = string.Format(
                "{0}/v3/scores/{1}/{2}/{3}/general/{4}/page?page={5}&count={6}{7}",
                BeatLeaderBaseUrl,
                Uri.EscapeDataString(request.Hash),
                Uri.EscapeDataString(request.Difficulty),
                Uri.EscapeDataString(request.Mode),
                scope, page, pageSize, playerQuery);

            var json = DownloadString(url);
            if (json == null)
                break;

            if (!TryParseScores(json, LeaderboardSource.BeatLeader, out var pageScores) || pageScores.Count == 0)
                break;

            allScores.AddRange(pageScores);
            if (pageScores.Count < pageSize)
                break;
        }

        return allScores.Count > 0 ? LimitScores(DeduplicateScores(allScores), count) : null;
    }

    private static List<LeaderboardInfo>? FetchScoreSaberScores(LeaderboardRequest request)
    {
        var count = LeaderboardRequest.ClampCount(request.Count);
        var mode = Uri.EscapeDataString(request.ScoreSaberMode);
        var hash = Uri.EscapeDataString(request.Hash);
        var routeDifficulties = new[] { request.ScoreSaberDifficulty.ToString(), Uri.EscapeDataString(request.Difficulty) };

        foreach (var difficulty in routeDifficulties.Distinct())
        {
            var scores = new List<LeaderboardInfo>();
            var pageSize = Math.Min(50, count);
            var pageCount = Math.Max(1, (count + pageSize - 1) / pageSize);

            for (var page = 1; page <= pageCount && scores.Count < count; page++)
            {
                var url = string.Format(
                    "{0}/api/v2/leaderboards/hash/{1}/{2}/{3}/scores?page={4}&limit={5}",
                    ScoreSaberBaseUrl, hash, mode, difficulty, page, pageSize);

                var json = DownloadString(url);
                if (json == null)
                    break;

                if (!TryParseScores(json, LeaderboardSource.ScoreSaber, out var pageScores) || pageScores.Count == 0)
                    break;

                scores.AddRange(pageScores);
                if (pageScores.Count < pageSize)
                    break;
            }

            if (scores.Count > 0)
            {
                var personalBest = FetchScoreSaberPersonalBest(request, difficulty);
                if (personalBest != null)
                    MergePersonalBest(scores, personalBest);
                return LimitScores(DeduplicateScores(scores), count);
            }
        }

        // Legacy v1 fallback
        var legacyScores = new List<LeaderboardInfo>();
        var maxLegacyPages = Math.Max(1, (count + 11) / 12);

        for (var page = 1; page <= maxLegacyPages && legacyScores.Count < count; page++)
        {
            var url = string.Format(
                "{0}/api/leaderboard/by-hash/{1}/scores?difficulty={2}&gameMode={3}&page={4}&withMetadata=false",
                ScoreSaberBaseUrl, hash, request.ScoreSaberDifficulty, mode, page);

            var json = DownloadString(url);
            if (json == null)
                break;

            if (!TryParseScores(json, LeaderboardSource.ScoreSaber, out var pageScores) || pageScores.Count == 0)
                break;

            legacyScores.AddRange(pageScores);
        }

        if (legacyScores.Count > 0)
        {
            var personalBest = FetchScoreSaberPersonalBest(request, request.ScoreSaberDifficulty.ToString());
            if (personalBest != null)
                MergePersonalBest(legacyScores, personalBest);
            return LimitScores(DeduplicateScores(legacyScores), count);
        }

        return null;
    }

    private static LeaderboardInfo? FetchScoreSaberPersonalBest(LeaderboardRequest request, string difficulty)
    {
        if (string.IsNullOrEmpty(request.PlayerId))
            return null;

        try
        {
            var url = string.Format(
                "{0}/api/v2/players/{1}/scores/hash/{2}/{3}/{4}",
                ScoreSaberBaseUrl,
                Uri.EscapeDataString(request.PlayerId),
                Uri.EscapeDataString(request.Hash),
                Uri.EscapeDataString(request.ScoreSaberMode),
                Uri.EscapeDataString(difficulty));

            var json = DownloadString(url);
            if (json == null)
                return null;

            var root = JToken.Parse(json);
            var objectRoot = root as JObject;
            var scoreToken = objectRoot?["data"] ?? root;
            var pb = ParseScoreToken(scoreToken, LeaderboardSource.ScoreSaber, 1, true);
            if (pb != null)
                pb.isPersonalBest = true;
            return pb;
        }
        catch
        {
            return null;
        }
    }

    private static string? DownloadString(string url)
    {
        using var client = new WebClient();
        client.Headers.Add("Accept", "application/json");
        client.Headers.Add("User-Agent", "InSongLeaderboardSocket/1.0 BeatSaber/1.40.8");
        return client.DownloadString(url);
    }

    private static bool TryParseScores(string json, LeaderboardSource source, out List<LeaderboardInfo> result)
    {
        result = new List<LeaderboardInfo>();
        try
        {
            var root = JToken.Parse(json);
            var array = FindScoresArray(root);
            if (array == null)
                return false;

            var fallbackRank = 1;
            foreach (var token in array)
            {
                var parsed = ParseScoreToken(token, source, fallbackRank, false);
                if (parsed != null)
                    result.Add(parsed);
                fallbackRank++;
            }

            var objectRoot = root as JObject;
            var supplementalToken = objectRoot?["selection"]
                ?? objectRoot?["playerScore"]
                ?? objectRoot?["leaderboardInfo"]?["playerScore"];
            var supplemental = ParseScoreToken(supplementalToken, source, fallbackRank, true);
            if (supplemental != null)
            {
                var identity = ScoreIdentity(supplemental);
                if (!result.Any(s => ScoreIdentity(s) == identity))
                    result.Add(supplemental);
            }

            result.Sort((a, b) => a.playerPosition.CompareTo(b.playerPosition));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static LeaderboardInfo? ParseScoreToken(JToken? token, LeaderboardSource source, int fallbackRank, bool personalBest)
    {
        if (token is not JObject score)
            return null;

        var player = score["player"] as JObject
            ?? score["leaderboardPlayerInfo"] as JObject
            ?? score["playerInfo"] as JObject;

        var name = ReadString(player, "name", "playerNameInGame", "username");
        if (string.IsNullOrEmpty(name))
            name = ReadString(score, "playerName", "name");
        if (personalBest && string.IsNullOrEmpty(name))
            name = Plugin.CurrentPlayerName ?? "Player";

        var scoreValue = ReadInt(score, "modifiedScore", "baseScore", "score");
        var baseScore = ReadInt(score, "unmodifiedScore", "baseScore");
        if (string.IsNullOrEmpty(name) || scoreValue < 0)
            return null;

        var rank = ReadInt(score, "rank", "responseRank");
        if (rank <= 0)
            rank = fallbackRank;

        var playerId = ReadString(player, "id", "playerId");
        if (string.IsNullOrEmpty(playerId))
            playerId = ReadString(score, "playerId");
        if (personalBest && string.IsNullOrEmpty(playerId))
            playerId = Plugin.CurrentPlayerId;

        var avatar = ReadString(player, "avatar", "profilePicture", "profilePictureUrl");

        return new LeaderboardInfo(
            playerId, name, scoreValue, rank,
            NormalizeAvatarUrl(avatar, source),
            false,
            ReadFloat(score, "accuracy", "acc"),
            ReadModifiers(score),
            personalBest, baseScore);
    }

    private static JArray? FindScoresArray(JToken root)
    {
        if (root is JArray arr)
            return arr;

        foreach (var name in new[] { "data", "scores", "items", "results" })
        {
            var value = root[name];
            if (value is JArray a) return a;
            if (value is JObject o)
            {
                foreach (var n in new[] { "data", "scores", "items", "results" })
                {
                    if (o[n] is JArray inner)
                        return inner;
                }
            }
        }
        return null;
    }

    private static string ReadString(JToken? token, params string[] names)
    {
        if (token == null) return string.Empty;
        foreach (var name in names)
        {
            var value = token[name];
            if (value != null && value.Type != JTokenType.Null)
                return value.ToString();
        }
        return string.Empty;
    }

    private static int ReadInt(JToken? token, params string[] names)
    {
        var text = ReadString(token, names);
        return int.TryParse(text, out var value) ? value : -1;
    }

    private static float ReadFloat(JToken? token, params string[] names)
    {
        var text = ReadString(token, names);
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return value;
        return -1f;
    }

    private static string ReadModifiers(JToken score)
    {
        if (score == null) return string.Empty;
        var token = score["mods"] ?? score["modifiers"];
        if (token is JArray array)
            return string.Join(",", array.Values<string>().Where(v => !string.IsNullOrEmpty(v)));
        return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
    }

    private static string NormalizeAvatarUrl(string avatar, LeaderboardSource source)
    {
        if (string.IsNullOrWhiteSpace(avatar)) return string.Empty;
        if (avatar.StartsWith("//")) return "https:" + avatar;
        if (avatar.StartsWith("/"))
            return (source == LeaderboardSource.BeatLeader ? BeatLeaderBaseUrl : ScoreSaberBaseUrl) + avatar;
        return avatar;
    }

    private static string ScoreIdentity(LeaderboardInfo score) =>
        string.IsNullOrEmpty(score.playerId) ? score.playerName + "\n" + score.playerScore : score.playerId;

    private static IEnumerable<LeaderboardInfo> DeduplicateScores(IEnumerable<LeaderboardInfo> scores) =>
        scores.GroupBy(ScoreIdentity).Select(g => g.First());

    private static List<LeaderboardInfo> LimitScores(IEnumerable<LeaderboardInfo> scores, int count)
    {
        var all = scores.OrderBy(s => s.playerPosition <= 0 ? int.MaxValue : s.playerPosition).ToList();
        var limited = all.Take(count).ToList();
        var pb = all.FirstOrDefault(s => s.isPersonalBest);
        if (pb != null && !limited.Contains(pb))
            limited.Add(pb);
        return limited;
    }

    private static void MergePersonalBest(IList<LeaderboardInfo> scores, LeaderboardInfo personalBest)
    {
        var identity = ScoreIdentity(personalBest);
        var existing = scores.FirstOrDefault(s => ScoreIdentity(s) == identity);
        if (existing == null)
        {
            scores.Add(personalBest);
        }
        else
        {
            existing.isPersonalBest = true;
            if (existing.playerAccuracy < 0f && personalBest.playerAccuracy >= 0f)
                existing.playerAccuracy = personalBest.playerAccuracy;
        }
    }
}
