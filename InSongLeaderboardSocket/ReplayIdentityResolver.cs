using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace InSongLeaderboardSocket;

internal static class ReplayIdentityResolver
{
    private static readonly string[] ReplayTypeHints = { "Replay", "Playback", "Replayer", "ScoreSaber", "BeatLeader" };
    private static readonly string[] PlayerIdMemberHints = { "playerId", "platformUserId", "steamId", "steamID", "userId", "uid", "accountId", "id" };
    private static readonly string[] PlayerNameMemberHints = { "playerName", "playerNameInGame", "userName", "username", "displayName", "profileName", "nickname" };
    private static readonly string[] PlayerNestedHints = { "player", "playerInfo", "playerData", "user", "owner", "profile", "replay", "replayData", "currentReplay", "activeReplay", "score", "session" };

    internal static bool TryResolveActiveReplayIdentity(BeatmapKey beatmapKey, out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        if (TryResolveFromUnityObjects(out playerId, out playerName))
            return true;

        if (TryResolveFromLoadedReplayReferences(out playerId, out playerName))
            return true;

        return false;
    }

    private static bool TryResolveFromUnityObjects(out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        UnityEngine.Object[] objects;
        try
        {
            objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
        }
        catch
        {
            return false;
        }

        foreach (var candidate in objects)
        {
            if (candidate == null)
                continue;

            if (!LooksReplayRelevant(candidate.GetType()))
                continue;

            if (!IsActiveUnityObject(candidate))
                continue;

            if (TryReadIdentity(candidate, out var id, out var name) && !string.IsNullOrWhiteSpace(id))
            {
                playerId = id;
                playerName = name;
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveUnityObject(UnityEngine.Object obj)
    {
        if (obj is Behaviour behaviour)
            return behaviour.isActiveAndEnabled;
        if (obj is GameObject go)
            return go.activeInHierarchy;
        return true;
    }

    private static bool TryResolveFromLoadedReplayReferences(out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (!LooksReplayRelevant(type))
                    continue;

                if (TryReadStaticIdentity(type, out var id, out var name) && !string.IsNullOrWhiteSpace(id))
                {
                    playerId = id;
                    playerName = name;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadStaticIdentity(Type type, out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            object? value;
            try
            {
                value = property.GetValue(null, null);
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (TryReadIdentity(value, out playerId, out playerName))
                return true;
        }

        foreach (var field in type.GetFields(flags))
        {
            object? value;
            try
            {
                value = field.GetValue(null);
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (TryReadIdentity(value, out playerId, out playerName))
                return true;
        }

        return false;
    }

    private static bool TryReadIdentity(object root, out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return TryReadIdentity(root, visited, 0, out playerId, out playerName);
    }

    private static bool TryReadIdentity(object root, HashSet<object> visited, int depth, out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        if (root == null || depth > 4)
            return false;

        if (root is string)
            return false;

        if (!visited.Add(root))
            return false;

        var type = root.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var property in type.GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            object? value;
            try
            {
                value = property.GetValue(root, null);
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (TryReadFromMember(property.Name, type, value, out playerId, out playerName))
                return true;

            if (ShouldRecurse(property.Name, value.GetType(), depth))
            {
                if (TryReadIdentity(value, visited, depth + 1, out playerId, out playerName))
                    return true;
            }
        }

        foreach (var field in type.GetFields(flags))
        {
            object? value;
            try
            {
                value = field.GetValue(root);
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (TryReadFromMember(field.Name, type, value, out playerId, out playerName))
                return true;

            if (ShouldRecurse(field.Name, value.GetType(), depth))
            {
                if (TryReadIdentity(value, visited, depth + 1, out playerId, out playerName))
                    return true;
            }
        }

        return false;
    }

    private static bool TryReadFromMember(string memberName, Type containerType, object value, out string playerId, out string playerName)
    {
        playerId = string.Empty;
        playerName = string.Empty;

        if (MatchesMember(memberName, PlayerIdMemberHints))
        {
            playerId = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(playerId);
        }

        if (IsPlayerNameMember(memberName, containerType))
        {
            playerName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(playerName);
        }

        return false;
    }

    private static bool ShouldRecurse(string memberName, Type memberType, int depth)
    {
        if (depth >= 4)
            return false;

        if (memberType == typeof(string) || memberType.IsPrimitive || memberType.IsEnum)
            return false;

        if (LooksReplayRelevant(memberType))
            return true;

        return MatchesMember(memberName, PlayerNestedHints);
    }

    private static bool IsPlayerNameMember(string memberName, Type containerType)
    {
        if (string.Equals(memberName, "name", StringComparison.OrdinalIgnoreCase))
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(containerType))
                return false;
        }

        if (MatchesMember(memberName, PlayerNameMemberHints))
            return true;

        if (!string.Equals(memberName, "name", StringComparison.OrdinalIgnoreCase))
            return false;

        var typeName = containerType.FullName ?? containerType.Name;
        if (typeName.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Behaviour", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return typeName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("User", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Info", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Identity", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesMember(string memberName, IEnumerable<string> hints) =>
        hints.Any(hint => string.Equals(memberName, hint, StringComparison.OrdinalIgnoreCase));

    private static bool LooksReplayRelevant(Type type)
    {
        var fullName = type.FullName ?? type.Name;

        if (fullName.IndexOf("Recorder", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fullName.IndexOf("Recording", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        if (fullName.IndexOf("InSongLeaderboard", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return ReplayTypeHints.Any(hint => fullName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
