# InSongLeaderboardSocket
A Beat Saber mod that shows a real-time in-song leaderboard overlay for OBS, streamed over WebSocket. | [Changelog](CHANGELOG.md) | [Reference Notes](REFERENCE_NOTES.md)

Built as a rework of [InSongLeaderboard](https://github.com/PatsanMCK/InSongLeaderboard) (itself a rework of [Kylemc1413's original](https://github.com/Kylemc1413/InSongLeaderboard)). Instead of drawing a board directly in the game world, this version serves an HTML overlay you load in OBS as a Browser Source, and pushes live leaderboard updates to it over WebSocket as you play.

No more peeking into your headset to see the board - viewers (and you, on a second screen) get a smooth, configurable leaderboard that follows your live rank as you climb.

> ⚠ Before you continue: Unfortunately this mod is entirely vibe-coded (AI WAS USED). However, this does not mean it goes untested - it's built and used daily. If you are ok with that, carry on. ⚠

# Features:
- Live in-song leaderboard for **ScoreSaber** and **BeatLeader**. BeatLeader scope (Global / Country / Following) is auto-detected from the in-game menu.
- **OBS-ready overlay** served right from the game: add `http://localhost:38452/` as a Browser Source. Alternatively use the [hosted standalone overlay](https://fefeland.github.io/InSongLeaderboardWSOverlay/) pointing at your local port.
- **Replay support** - when watching a replay, the overlay tracks the player being replayed (works with common replay mods).
- **Player-following window** - entries scroll so you always stay visible near the bottom of the board.
- **Rank-up animation** - a sweep effect whenever you gain a position.
- **Personal best badges** - PB entries marked with a gold accent.
- **Edge fade** - top/bottom edges fade out depending on your position and how deep the board is.
- **DOM-diffing renderer** - the overlay reuses elements instead of rebuilding, so there's zero visual flicker in OBS.
- **Idle mode** - keeps a placeholder board on screen in the menu if you want it.
- **Live configuration** - every setting is applied in real time over the WebSocket (no restarts).
- **In-game BSML settings** - rows, images, stats, alignment, colors, fade, font size and more.
- **`Rank By Accuracy`** - optionally position your live entry by accuracy instead of raw score.
- Fetches top scores with pagination, player avatars and personal bests from both APIs.
- No dependency on the ScoreSaber/BeatLeader mods - queries their public HTTP APIs directly.

# Installing / Setting Up
### Dependencies:
- Your usual core mods: `BSIPA`, `BS_Utils`, `BSML`
- `websocket-sharp`, `Newtonsoft.Json`, `HarmonyLib` (bundled with most mods)

### Adding the mod:
- Grab the latest release from the [releases page](https://github.com/FEFELAND/InSongLeaderboardSocket/releases).
- Drop `InSongLeaderboardSocket.dll` into your `Plugins` folder.
- Tweak settings in-game under `Settings -> InSongLB-WS`.

### OBS setup:
1. Launch the game (the mod starts a small HTTP+WS server on your machine).
2. Add a **Browser Source** in OBS and point it to one of:
   - `http://localhost:38452/` (served straight from the game, always in sync)
   - `https://fefeland.github.io/InSongLeaderboardWSOverlay/` (hosted overlay, tells the URL what port to use, default 38452)
3. Enter a song - the board updates live as your score changes.
4. Done. Go make the video.

### Customizing the overlay:
Everything is configurable from the in-game settings menu or live via the overlay (it reads its config from every WebSocket message). You can also override things on the standalone overlay through URL params (`port`, `rows`, `showimages`, `showstats`, `fontsizebase`, `bgopacity`).

# How it works (for the curious)
The mod Harmony-patches the map launch transition to grab the `BeatmapKey`, fetches the top scores from the selected leaderboard API, then hooks `ScoreController.scoreDidChangeEvent`. Every score change rebuilds a state object (song, your rank, the leaderboard, config) and broadcasts it as JSON to every connected overlay over WebSocket - so there is no polling, everything is push-based.

The embedded `overlay.html` renders that state with minimal DOM churn, keeps the window centered on you, and animates your rank-ups. A standalone copy lives in `UploadThese/index.html` and is what's published to the hosted overlay URL.

# Attributions
This project adapts code from a few places. Please see the [Reference Notes](REFERENCE_NOTES.md) and [Third Party Notices](ThirdPartyNotices.md) for full details.
- [PatsanMCK/InSongLeaderboard](https://github.com/PatsanMCK/InSongLeaderboard) (MIT) - API clients, score models, Harmony patch patterns, plugin structure.
- [ReadieFur/BSDataPuller](https://github.com/ReadieFur/BSDataPuller) (GPL-3.0) - consulted for WebSocket server architecture (no code copied).
- [Aeroluna/Technicolor](https://github.com/Aeroluna/Technicolor) (MIT) - consulted for BSML settings page patterns.