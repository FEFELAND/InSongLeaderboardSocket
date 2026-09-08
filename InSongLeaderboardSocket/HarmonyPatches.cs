using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace InSongLeaderboardSocket;

[HarmonyPatch]
internal static class LevelLaunchPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(StandardLevelScenesTransitionSetupDataSO)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "Init")
            .Where(method => method.GetParameters().Any(p =>
                p.Name == "beatmapKey" &&
                (p.ParameterType == typeof(BeatmapKey) ||
                 p.ParameterType.GetElementType() == typeof(BeatmapKey))));
    }

    private static void Postfix(in BeatmapKey beatmapKey)
    {
        Plugin.CaptureBeatmap(beatmapKey);
    }
}
