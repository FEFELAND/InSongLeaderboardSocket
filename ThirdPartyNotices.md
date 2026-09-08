# Third-Party Notices

This project incorporates or was adapted from material from the following third-party software, used under license.

## PatsanMCK/InSongLeaderboard (MIT)

Source code was directly adapted from this project: the leaderboard data models (`LeaderboardInfo.cs`, `LeaderboardSource.cs`), the ScoreSaber/BeatLeader API client (`LeaderboardApiClient.cs`), the Harmony patch pattern (`HarmonyPatches.cs` from `Patches.cs`) and the plugin structure/hookups in `Plugin.cs`.

- Source: https://github.com/PatsanMCK/InSongLeaderboard
- Author: PatsanMCK (a rework of https://github.com/Kylemc1413/InSongLeaderboard by Kylemc1413)
- License: MIT

```
MIT License

Copyright (c) 2021 Kylemc1413

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ReadieFur/BSDataPuller (GPL-3.0)

Consulted for WebSocket server architecture and reactive event model. No code was copied; the approach informed the design of `WebSocketServer.cs`.

- Source: https://github.com/ReadieFur/BSDataPuller
- License: GPL-3.0

## Aeroluna/Technicolor (MIT)

Consulted for BSML settings page patterns (submenu navigation, pref-width layout constraints). Adapted into `Views/Settings.bsml`.

- Source: https://github.com/Aeroluna/Technicolor
- License: MIT

## Libraries and Frameworks

- BSIPA (MIT) - Beat Saber mod loader
- BS_Utils (MIT) - game events / user info
- HarmonyLib / 0Harmony (MIT) - runtime method patching
- Newtonsoft.Json (MIT) - JSON serialization
- websocket-sharp (MIT) - WebSocket server
- BeatSaberMarkupLanguage / BSML (MIT) - in-game settings UI
- BeatSaberModdingTools.Tasks (MIT) - build tooling / manifest generation