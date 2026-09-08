# 1.0.0
### Initial public release
- Real-time in-song leaderboard overlay for OBS, served by the game over HTTP and pushed over WebSocket (port 38452).
- ScoreSaber and BeatLeader support with paginated top-score fetching, avatars and personal bests.
- BeatLeader scope (Global / Country / Following) auto-detected from the in-game menu.
- DOM-diffing overlay with player-following window, rank-up animation, edge fade, idle mode and live config.
- Replay identity resolution for watching replays.
- In-game BSML settings (rows, images, stats, alignment, colors, font size, fade and more).
- Standalone hosted overlay available at https://fefeland.github.io/InSongLeaderboardWSOverlay/
- Rework of InSongLeaderboard: replaces the in-world board with a WebSocket-served HTML overlay.