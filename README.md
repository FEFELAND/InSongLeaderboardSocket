# InSongLeaderboardSocket
A Beat Saber mod that shows a real-time in-song leaderboard overlay for OBS, streamed over WebSocket. | [Changelog](CHANGELOG.md) | [Reference Notes](REFERENCE_NOTES.md)

Built as a rework of [InSongLeaderboard](https://github.com/PatsanMCK/InSongLeaderboard) (itself a rework of [Kylemc1413's original](https://github.com/Kylemc1413/InSongLeaderboard)). Instead of drawing a board directly in the game world, this version serves an HTML overlay you load in OBS as a Browser Source, and pushes live leaderboard updates to it over WebSocket as you play.
Viewers  get a smooth, configurable leaderboard that follows your live rank as you climb.

> ⚠ Before you continue: Unfortunately this mod is entirely vibe-coded. But this does not mean things go untested. If you are ok with that, carry on. ⚠

# Features:
- Live in-song leaderboard for **ScoreSaber** and **BeatLeader**. BeatLeader scope (Global / Country / Following) is auto-detected from the in-game menu.
- **OBS-ready overlay** Use the [hosted standalone overlay](https://fefeland.github.io/InSongLeaderboardWSOverlay/).
- **Replay support** - Watching replays work just the same as if you were playing.
- **Player-following window** - entries scroll so you always stay visible near the bottom of the board.
- **Rank-up animation** - a sweep effect whenever you gain a position.
- **Personal best badges** - PB entries marked with a gold accent (or as configured).
- **Edge fade** - top/bottom edges fade out depending on your position.
- **Idle mode** - keeps a placeholder board on screen in the menu if you want it (for editing).
- **Live configuration** - every setting is applied in real time over the WebSocket (no restarts).
- **In-game BSML settings** - rows, images, stats, alignment, colors, fade, font size and more.
- **`Rank By Accuracy`** - optionally position your live entry by accuracy instead of raw score.
- Fetches top scores with pagination, player avatars and personal bests from both APIs.
- No dependency on the ScoreSaber/BeatLeader mods - queries their public HTTP APIs directly.

# Installing / Setting Up
### Dependencies:
- Your usual core mods: `BSIPA`, `BS_Utils`, `BSML`
- `websocket-sharp`

### Adding the mod:
- Grab the latest release from the [releases page](https://github.com/FEFELAND/InSongLeaderboardSocket/releases).
- Drop `InSongLeaderboardSocket.dll` into your `Plugins` folder.
- Tweak settings in-game under `Settings -> Mod Settings -> InSongLB-WS`.

### OBS setup:
1. Launch the game.
2. Add a **Browser Source** in OBS and point it to:
   - `https://fefeland.github.io/InSongLeaderboardWSOverlay/`
3. Enter a song - the board updates live as your score changes.

# Attributions
This project adapts code from a few places. Please see the [Reference Notes](REFERENCE_NOTES.md) and [Third Party Notices](ThirdPartyNotices.md) for full details.
- [PatsanMCK/InSongLeaderboard](https://github.com/PatsanMCK/InSongLeaderboard) (MIT) - API clients, score models, Harmony patch patterns, plugin structure.
- [ReadieFur/BSDataPuller](https://github.com/ReadieFur/BSDataPuller) (GPL-3.0) - consulted for WebSocket server architecture (no code copied).
- [Aeroluna/Technicolor](https://github.com/Aeroluna/Technicolor) (MIT) - consulted for BSML settings page patterns.