using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InSongLeaderboardSocket;

internal sealed class RuntimeHooks : MonoBehaviour
{
    private static RuntimeHooks? _instance;
    private Coroutine? _settingsRegistrationRoutine;
    private Coroutine? _replayIdentityRoutine;
    private Coroutine? _updateCheckLoop;

    internal static void EnsureCreated()
    {
        if (_instance != null)
            return;

        var go = new GameObject("InSongLeaderboardSocketRuntimeHooks");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RuntimeHooks>();
    }

    internal static void ScheduleSettingsMenuRegistration()
    {
        EnsureCreated();
        _instance?.ScheduleSettingsMenuRegistrationInternal(false);
    }

    internal static void ScheduleReplayIdentityRefresh()
    {
        EnsureCreated();
        _instance?.ScheduleReplayIdentityRefreshInternal();
    }

    internal static void RunCoroutine(IEnumerator routine)
    {
        EnsureCreated();
        if (_instance == null) return;
        _instance.StartCoroutine(routine);
    }

    internal static void StartBackgroundUpdateChecks()
    {
        EnsureCreated();
        _instance?.StartBackgroundUpdateChecksInternal();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleSettingsMenuRegistrationInternal(true);
    }

    private void ScheduleSettingsMenuRegistrationInternal(bool immediate)
    {
        if (_settingsRegistrationRoutine != null)
            StopCoroutine(_settingsRegistrationRoutine);

        _settingsRegistrationRoutine = StartCoroutine(RegisterSettingsMenuWhenReady(immediate));
    }

    private void ScheduleReplayIdentityRefreshInternal()
    {
        if (_replayIdentityRoutine != null)
            StopCoroutine(_replayIdentityRoutine);

        _replayIdentityRoutine = StartCoroutine(RefreshReplayIdentityWhenReady());
    }

    private void StartBackgroundUpdateChecksInternal()
    {
        if (_updateCheckLoop != null)
            return;

        _updateCheckLoop = StartCoroutine(BackgroundUpdateCheckLoop());
    }

    private IEnumerator BackgroundUpdateCheckLoop()
    {
        yield return new WaitForSeconds(5f);

        while (true)
        {
            yield return SettingsHost.RunUpdateCheck();
            yield return new WaitForSeconds(600f);
        }
    }

    private IEnumerator RegisterSettingsMenuWhenReady(bool immediate)
    {
        if (!immediate)
        {
            for (var i = 0; i < 120; i++)
                yield return null;
        }

        var registered = false;
        for (var attempt = 1; attempt <= 240; attempt++)
        {
            if (SettingsHost.TryRegister())
            {
                registered = true;
                break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        if (!registered)
            Plugin.Log.Warn("InSongLeaderboard settings menu registration timed out after 180s.");

        _settingsRegistrationRoutine = null;
    }

    private IEnumerator RefreshReplayIdentityWhenReady()
    {
        var resolved = false;
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            if (Plugin.Instance != null && Plugin.Instance.TryResolveReplayIdentity())
            {
                resolved = true;
                break;
            }

            yield return new WaitForSeconds(0.25f);
        }

        if (!resolved)
            Plugin.Log.Warn("Replay identity was not resolved before the retry window expired.");

        _replayIdentityRoutine = null;
    }
}
