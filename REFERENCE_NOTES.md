# Reference Notes

This document credits all external code, libraries, and projects that were used
as direct references, inspiration, or copied (with attribution) during the
development of InSongLeaderboardSocket.

---

## Direct Code References

### PatsanMCK/InSongLeaderboard (MIT License)
- https://github.com/PatsanMCK/InSongLeaderboard
- Fork of Kylemc1413/InSongLeaderboard

**Files adapted from this project:**

- **LeaderboardInfo.cs** - Data model for a single leaderboard score entry.
  Adapted from `Leaderboardinfo.cs`. The constructor and field layout were
  taken directly; fields were given default values for nullable compatibility.

- **LeaderboardSource.cs** - Enums (LeaderboardSource, LeaderboardScope) and
  the LeaderboardRequest class. Taken directly from `LeaderboardSource.cs`.
  The ScoreSaberDifficulty property, ScoreSaberMode property, and ClampCount
  method are identical.

- **LeaderboardApiClient.cs** - The ScoreSaber and BeatLeader API client.
  Heavily adapted from `LeaderboardApiClient.cs`. The API URLs, response
  parsing logic (TryParseScores, ParseScoreToken, FindScoresArray, ReadString,
  ReadInt, ReadFloat, ReadModifiers), deduplication, pagination, personal best
  merging, and avatar normalization are all based on the original code. The main
  change was replacing UnityWebRequest/coroutines with synchronous WebClient
  calls and removing Unity-specific types.

- **HarmonyPatches.cs** - The Harmony patch that captures BeatmapKey on map
  launch. Adapted from `Patches.cs`. The TargetMethods() approach and the
  Postfix signature are taken directly from the reference mod.

- **Plugin.cs** - The main plugin entry point. The overall structure (Harmony
  patching, BSEvents subscriptions, score controller hookup, player info
  retrieval via GetUserInfo) was inspired by the reference mod's `Plugin.cs`.
  The score tracking logic, leaderboard fetching flow, and personal best
  marking were adapted from the reference.

### ReadieFur/BSDataPuller (GPL-3.0 License)
- https://github.com/ReadieFur/BSDataPuller

**Consulted for:**

- WebSocket server architecture. BSDataPuller uses the websocket-sharp library
  for its WebSocket server on port 2946. After review, we adopted the same
  approach and switched our WebSocketServer.cs from a hand-rolled HttpListener
  implementation to websocket-sharp.

- The reactive event model (data objects fire OnUpdate events) inspired our
  approach of broadcasting state on every ScoreController.scoreDidChangeEvent.

### Kylemc1413/InSongLeaderboard (Original)
- https://github.com/Kylemc1413/InSongLeaderboard
- The original InSongLeaderboard mod that PatsanMCK's version is forked from.

### Aeroluna/Technicolor (MIT License)
- https://github.com/Aeroluna/Technicolor

**Consulted for:**

- BSML settings page multi-page pattern using `<settings-submenu>` with
  `<clickable-text click-event="back">` for navigation. The pref-width
  constraint on horizontals inside submenu pages was also adapted from
  Technicolor's approach.

---

## Self-Authored Code

The following files were written from scratch for this project:

- **OverlayServer.cs** / **WebSocketServer.cs** - Uses websocket-sharp's
  `HttpServer` class directly (not `WebSocketServer`) to serve the overlay HTML
  over HTTP alongside WebSocket connections on the same port. Broadcasts JSON
  state to all connected overlay clients.

- **overlay.html** - Full-featured OBS Browser Source overlay with DOM diffing
  (no visual flicker), player-following windowing, rank-up animations,
  fade transitions, and live config updates from the server.

- **PluginConfig.cs** - IPA Config Store integration for persistent settings
  (rows, images, stats, source, font size, compact mode, background opacity,
  show-when-idle).

- **SettingsHost.cs** + **Views/Settings.bsml** - BSML settings panel with
  live-updating controls (no Apply button). Uses `BSMLSettings.AddSettingsMenu`
  with `[UIValue]` bindings to PluginConfig.

- **OverlayState.cs** - JSON-serializable state object broadcast to overlays,
  including a nested config section for live setting reflection.

- **Plugin.cs** (gameplay integration) - Harmony patching, BSEvents
  subscriptions, ScoreController hookup, leaderboard fetching/sorting,
  player rank tracking, replay detection, and BSML settings registration.

- **HarmonyPatches.cs** - Captures BeatmapKey on scene transition via Harmony
  prefix/postfix on StandardLevelScenesTransitionSetupDataSO.Init. Simplified
  from the reference.

---

## Libraries and Frameworks Used

### Hard Dependencies (referenced in code)
- BSIPA (^4.3.0) - Beat Saber mod loader framework (MIT)
- BS_Utils - Game events, level data, user info via `GetUserInfo` (MIT)
- HarmonyLib (0Harmony) - Runtime method patching (MIT)
- Newtonsoft.Json - JSON serialization/deserialization (MIT)
- websocket-sharp - WebSocket server implementation (MIT)
- BeatSaberMarkupLanguage (BSML) - In-game settings UI framework (MIT)

### Build Dependencies (NuGet/packages)
- BeatSaberModdingTools.Tasks - Build tooling, manifest generation (MIT)
- Microsoft.NETFramework.ReferenceAssemblies - .NET 4.7.2 reference assemblies (MIT)

### Load-Order Only (no DLL references)
- ScoreSaber - `LoadAfter` ensures we load after it so its leaderboard data is
  available; we query its public HTTP API (`scoresaber.com/api/...`) directly
- BeatLeader - `LoadAfter` ensures we load after it; we query its public HTTP
  API (`api.beatleader.xyz/...`) directly

Note: ScoreSaber and BeatLeader are NOT hard dependencies. The mod works without
them installed (it just won't have leaderboard data). The `LoadAfter` entries
in Directory.Build.props are BSIPA load-ordering hints, not dependency declarations.

---

## License Compliance

- InSongLeaderboard code is adapted under MIT license (compatible with any use)
- BSDataPuller is GPL-3.0; we consulted its architecture but did not copy code
- websocket-sharp is MIT licensed (same library used by BSDataPuller)
- All Beat Saber modding tools and libraries used are MIT licensed
