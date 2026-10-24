# Package C: Systems and UI

Owner: Dev C. Branch `feature/C-systems-ui`, built on top of `feature/B1-B8-ghost-ai` (which already contains `M0` and the `C1` event framework draft).

The game's backbone: the ScriptableObject event channels every package talks through, the config assets that hold every tunable number, the GameManager state flow, score, lives, the saved high score, pause, the cursor, and every screen. Nothing in this package calls the player, the maze or the ghosts. It raises and hears channels, so each package can be built and tested on its own and dropped into the `Game` scene without code changes.

## What is in the package

| Path (under `Assets/_Project`) | Contents |
| --- | --- |
| `Scripts/Events/` | `EventChannelBase`, `EventChannel<T>`, `EventListener<T,TChannel>`; channels for Void, `GameState`, `LevelConfig`, int, float, bool, `PickupConfig` |
| `Scripts/Core/Configs/` | `GameRulesConfig`, `PickupConfig`, `PlayerConfig`, `LevelSequence`, `UiConfig`, `LevelConfig` (bonus section added; ghost section is Dev B's), `GhostConfig` |
| `Scripts/Core/` | `GameState`, `LevelProgress` (runtime asset), `TransformAnchor` + `TransformAnchorBinder` |
| `Scripts/Systems/` | `GameManager`, `ScoreSystem` + `ScoreKeeper`, `LivesSystem` + `LivesKeeper`, `HighScoreSystem` + `JsonHighScoreStore`, `PauseController`, `CursorController` |
| `Scripts/UI/` | `HudView`, `MainMenuView`, `PauseMenuView`, `GameOverView`, `ControlsView`, `SettingsView`, `VolumeSettings`, `AudioSettingsLoader` |
| `ScriptableObjects/Configs/` | `GameRulesConfig`, `UiConfig`, `PlayerConfig`, `PickupConfig_Cell/Flare/Battery`, `LevelSequence` (plus Dev B's ghost and level configs) |
| `ScriptableObjects/Events/` | One asset per channel (table below) |
| `ScriptableObjects/Runtime/` | `LevelProgress` (plus Dev B's `GhostRuntimeSet`, `PlayerAnchor`) |
| `Scenes/` | `MainMenu`, `Game` (generated, then owned by the team) |
| `Editor/Systems/` | **Game > Systems** builders, the validator, the channel Inspector (live listener count) |
| `Tests/` | EditMode: `SystemsRulesTests`, `HighScoreStoreTests`, `EventChannelTests`, `ConfigAssetTests`. PlayMode: `GameManagerTests`, `GameSceneTests` |

## How it connects

Every arrow in the game is one of these channels. Raising is synchronous: when `Raise` returns, every listener has run.

| Channel (asset) | Payload | Raised by | Heard by |
| --- | --- | --- | --- |
| `GameStateChanged` | `GameState` | GameManager | Ghosts (B), player input (A), HUD, pause, cursor, game over screen, high score, audio and lighting (D) |
| `LevelStarted` | `LevelConfig` | GameManager, before `Ready` | Ghosts (speeds, mode timer, stun time), score (battery points), maze (A: restore cells) |
| `LevelNumberChanged` | `int` (1-based) | GameManager, just before `LevelStarted` | HUD |
| `LevelComplete` | none | GameManager, right after `GameStateChanged(LevelComplete)` | Audio, lighting (D) |
| `NewGameStarted` | none | GameManager (new game, Retry) | Score and lives (reset), game over screen |
| `LifeLost` | none | GameManager, once per catch | LivesSystem |
| `LivesChanged` | `int` | LivesSystem | GameManager, HUD |
| `ExtraLifeAwarded` | none | ScoreSystem, once per game | LivesSystem, audio (D) |
| `ScoreChanged` | `int` (total) | ScoreSystem | HUD, high score, game over screen, audio (D) |
| `HighScoreChanged` | `int` | HighScoreSystem (at scene start, and when beaten) | HUD, main menu, game over screen |
| `PickupCollected` | `PickupConfig` | Pickups (A): power cells and batteries | Score, audio and VFX (D) |
| `FlareCollected` | none | Flare pickup (A) | Ghosts (all frightened), score (50), lighting and audio (D) |
| `PlayerCaught` | none | GhostDirector (B), once per catch | GameManager, lighting and audio (D) |
| `GhostBanished` | `GhostBanishedInfo` (ghost config, combo 1 to 4, points, position) | GhostDirector (B) | Score (adds `Points` as is), audio and VFX (D) |
| `ModeChanged` | `GhostModeChange` (mode, seconds; 0 = until changed) | GhostDirector (B) | Every ghost, audio siren (D) |
| `BatteryChanged` | `float` 0 to 1 | Flashlight (A), when the charge changes | HUD, audio (D) |
| `PauseRequested` | `bool` | Pause menu Resume button (false) | PauseController |
| `PauseChanged` | `bool` | PauseController | Pause menu, cursor, audio (D) |
| `RestartRequested` | none | Game over Retry button | GameManager |
| `MainMenuRequested` | none | Pause and game over Main Menu buttons | GameManager (loads `MainMenu`), HighScoreSystem (saves) |

Runtime assets (shared state without scene searches): `LevelProgress` (cells left; maze writes, GameManager listens), `PlayerAnchor` (the player's Transform; ghosts read), `GhostRuntimeSet` (live ghosts; director and flashlight beam read).

### Order of events

- **Level start:** `LevelNumberChanged(n)`, `LevelStarted(config)`, `GameStateChanged(Ready)`, wait `readySeconds`, `GameStateChanged(Playing)`.
- **Catch:** `PlayerCaught` (ghosts) → `GameStateChanged(PlayerCaught)`, `LifeLost` → `LivesChanged(n)`, wait `caughtSeconds`, then `Ready` and `Playing` again, or `GameOver` when no lives are left.
- **Last cell:** `LevelProgress.Completed` → `GameStateChanged(LevelComplete)`, `LevelComplete`, wait `levelCompleteSeconds`, then the next level start with the next `LevelConfig`.

## Plugging it in

### Dev A: player

- Listen to `GameStateChanged` (a `GameStateEventListener`, or subscribe in code in `OnEnable`/`OnDisable`):
  - `Ready`: respawn at the start point, input **off**.
  - `Playing`: input **on**.
  - `PlayerCaught`, `LevelComplete`, `GameOver`, `Menu`: input **off**.
- Pause needs nothing from you: `Time.timeScale` is 0, and `CursorController` frees the mouse. If your look code uses unscaled time or raw mouse delta, also ignore input while `PauseChanged` is true.
- Tag the player `Player`, and add `TransformAnchorBinder` pointing at `ScriptableObjects/Runtime/PlayerAnchor` (the ghosts aim with it).
- Read speeds and sensitivity from `ScriptableObjects/Configs/PlayerConfig`.
- Delete `Placeholder Camera (delete when the player exists)` from the `Game` scene once the player brings its own camera and AudioListener.

### Dev A: maze and pickups

- Build the maze under `--- Maze goes here ---` in `Scenes/Game.unity` (or a prefab placed there).
- On `LevelStarted`: restore every cell, flare and battery, then call `LevelProgress.Begin(total)`, where `total` counts the pickups whose `PickupConfig.CountsTowardLevel` is on (cells and flares, like pellets and power pellets).
- **Do not** reset cells on `Ready`. A catch goes back to `Ready` and the cells collected so far must stay collected.
- When the player collects a pickup:
  - power cell or battery: raise `PickupCollected` with its `PickupConfig` (`PickupConfig_Cell`, `PickupConfig_Battery`);
  - flare: raise `FlareCollected` (the ghosts already listen to it);
  - and if `CountsTowardLevel`, call `LevelProgress.Collect()`.
- Flashlight: drain and recharge with `PlayerConfig` (battery pickups recharge `RechargeAmount`), and raise `BatteryChanged(charge / max)` when the value changes. A small threshold (for example every 1%) is enough; there is no need to raise every frame.

### Dev B: ghosts

Nothing new; the ghosts already match. `GameManager` raises `LevelStarted` before `Ready`, ignores `PlayerCaught` outside `Playing`, and pauses with `Time.timeScale` (no `Paused` state). Score adds `GhostBanishedInfo.Points` as it is, so the combo values stay in `GhostAIConfig`. The channel table above lists `LevelStarted` and the payloads of `ModeChanged` and `GhostBanished`, as requested.

### Dev D: audio, lighting, VFX

Listen, never call: `GameStateChanged` (music and light per state), `PickupCollected` (cell / battery by `PickupConfig.Kind`), `FlareCollected`, `GhostBanished` (combo for pitch), `PlayerCaught`, `LevelComplete`, `ExtraLifeAwarded`, `PauseChanged` (duck or pause audio), `ScoreChanged`, `ModeChanged` (siren). Master volume is `AudioListener.volume`, saved in `PlayerPrefs` under `UiConfig.VolumePrefsKey` (`audio.masterVolume`); an `AudioManager` can build on that key. The UI is a plain dark placeholder (near-black, off-white text, one dim red accent) and is safe to restyle: views only need their serialized fields kept.

## Behaviour decisions (ask Dev C before changing)

- **Past the last level**, `LevelSequence` keeps replaying the last (hardest) `LevelConfig`, the way the original stops getting harder. The HUD still counts up (`LEVEL 4`, `LEVEL 5`, ...).
- **Extra life:** once per game at `GameRulesConfig.extraLifeScore` (10,000). 0 turns it off.
- **Game over:** `GAME OVER` shows for `gameOverSeconds`, then the Retry / Main Menu screen appears. It does not return to the menu by itself, so the player can retry at once.
- **High score** is raised live as soon as it is beaten, and written to disk at game over, on Main Menu and when the scene unloads, not on every pickup. A missing or corrupt save file means 0, never an exception. The file is `highscore.json` in `Application.persistentDataPath`, written to a temp file and swapped in.
- **Pause** is not a `GameState`. Esc / gamepad Start toggles it only while `Playing`; leaving `Playing` (caught, level complete, menu) and unloading the scene always restore `Time.timeScale = 1`.
- **Lives** belong to `LivesSystem`. GameManager raises `LifeLost` and reads the answer from `LivesChanged`; if no `LivesSystem` answers it counts locally and logs a warning, so a misconfigured scene still ends.
- **The project runs with domain reload off.** Runtime assets reset themselves (`LevelProgress` on load and on `Begin`), and nothing keeps static state.

## Testing it

- **Game scene on its own:** open `Assets/_Project/Scenes/Game.unity` and press Play. It goes `READY` → playing. Esc pauses. To fake a catch, select `ScriptableObjects/Events/PlayerCaught` and use **Raise** from its context menu (or right-click the `Systems` GameManager > **Debug/Catch Player**); three catches give `GAME OVER`, then Retry / Main Menu. **Debug/Complete Level** jumps to the next level.
- **Trace events:** tick **Log Raises** on any channel asset to log every raise and every listener that hears it. In Play mode each channel's Inspector shows its live listener count.
- **Validate wiring:** **Game > Systems > Validate Setup** lists every unassigned field on a `Game.*` component in both scenes and all prefabs.
- **Tests:** **Window > General > Test Runner**. EditMode covers score points and the extra life, lives, level progress (completes once, `Begin` resets), level order clamping, the high score file (round trip, missing, corrupt), the new channels and the real config assets. PlayMode drives GameManager through catches, game over, retry, level complete and pause with test-made channels, and plays the real `Game` scene from Ready to Game Over.
- **Rebuild:** **Game > Systems > Create Systems Assets / Build Main Menu Scene / Build Game Scene / Update Build Settings**. Builders never overwrite an existing config or channel; rebuilding a scene asks first, because it would also remove the maze.

## What needs the other packages

- The maze, player, pickups and flashlight (A) to make the `Game` scene playable. Until then it runs the full state flow with no maze.
- The ghosts placed in the `Game` scene (B, after A's maze and NavMesh exist): drop a `GhostDirector`, the four ghost prefabs and a `GhostMazeLayout` under the maze.
- Final look, music and sound (D).

## Requests for other devs

- **Dev A:** implement the player and maze contracts above (`GameStateChanged` input rules, `LevelProgress.Begin/Collect`, `PickupCollected`, `FlareCollected`, `BatteryChanged`, `PlayerAnchor`). Confirm the draft controls in `UiConfig` (WASD/arrows, mouse, left click flashlight, Esc pause). Remove the placeholder camera when the player exists.
- **Dev B:** none required. Optional: once the real maze exists, replace `StandaloneGameFlow` in the sandbox with `GameManager`, or keep the sandbox as a test-only scene.
- **Dev D:** hook audio and lighting to the channels listed above; restyle the UI in the scenes (keep each view's serialized fields wired, then run **Validate Setup**).
- **Everyone:** the `Game` scene is shared. Say in the team chat before editing it, and prefer prefabs.
