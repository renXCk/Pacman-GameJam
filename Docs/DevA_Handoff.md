# Dev A handoff: maze, player, pickups

Everything Dev A built, how it connects to the rest of the game, how to change it, and how to check it.
Written for Dev B, Dev C and Dev D, and for whoever fixes a bug in this area after submission.

## 1. What is in Dev A's area

| Area | What it does | Main files (`Assets/_Project/...`) |
|---|---|---|
| Map format (A1) | Reads the text map into a grid of tiles | `Scripts/Maze/MazeTile.cs`, `TileCoord.cs`, `MazeGrid.cs`, `Data/Mazes/Maze_Level1.txt` |
| Maze builder (A2) | Builds walls, floor and the ghost house door from the map | `Scripts/Maze/MazeBuilder.cs`, `MazeConfig.cs`, `MazeWallRuns.cs` |
| NavMesh and ghosts (A3) | Bakes the NavMesh, places the ghost layout markers, spawns the four ghost prefabs | `Scripts/Maze/MazeNavMesh.cs`, `MazeGhosts.cs` |
| Player (A4) | First-person controller, spawn point | `Scripts/Player/PlayerController.cs`, `Scripts/Maze/MazePlayerSpawn.cs` |
| Pickups (A5) | Power cells, flares and batteries as trigger colliders | `Scripts/Pickups/Pickup.cs`, `MazePickups.cs` |
| Flashlight (A6) | Switch and battery (Dev D's `FlashlightController` draws the beam) | `Scripts/Player/PlayerFlashlight.cs` |
| Battery rules, warps, reset (A7) | When batteries appear; warp doors; new level restores everything | `Scripts/Pickups/BatteryRules.cs`, `BatteryDirector.cs`, `Scripts/Maze/WarpPairs.cs`, `MazeWarps.cs`, `WarpDoor.cs` |
| Checks (A8) | Map design rules and the setup validator | `Scripts/Maze/MazeChecks.cs`, `Editor/Maze/MazeValidator.cs` |

The maze lives on one scene object (`Maze`, at the world origin) that carries `MazeBuilder`, `MazeNavMesh`,
`MazeGhosts`, `MazePlayerSpawn`, `MazePickups`, `BatteryDirector` and `MazeWarps`. `MazeBuilder` builds in
`Awake` (execution order -200) and then calls every `IMazeListener` on the same object, in the order they sit
there. That is how the NavMesh, the ghosts, the player marker, the pickups, the battery rules and the warps all
find a finished maze without depending on script execution order.

## 2. Setting the scene up (menus)

Run once, in this order. Each menu opens `Game.unity` first, wires the scene and saves it. Running one twice is safe.

1. `Game > Maze > Add Maze To Game Scene`
2. `Game > Player > Add Player To Game Scene`
3. `Game > Player > Add Flashlight To Game Scene`
4. `Game > Pickups > Add Pickups To Game Scene`
5. `Game > Pickups > Add Battery Rules And Warps`
6. `Game > Maze > Validate Maze Setup` (changes nothing, reports problems)

Only one person should edit `Game.unity` at a time.

## 3. Events: who raises what, who listens

| Event channel | Raised by | Heard by |
|---|---|---|
| `PickupCollected` (PickupConfig) | a power cell or a battery being taken | Dev C score, Dev D audio and VFX |
| `FlareCollected` | a flare being taken (never `PickupCollected`) | `GhostDirector` (ghosts frightened), Dev D lighting, audio and VFX, Dev C score |
| `BatteryChanged` (0 to 1) | `PlayerFlashlight` | HUD, Dev D `FlashlightController`, audio |
| `LevelStarted` (LevelConfig) | `GameManager` | `MazePickups` (restore pickups, restart `LevelProgress`), `BatteryDirector` (hide batteries), `GhostDirector` |
| `GameStateChanged` | `GameManager` | `PlayerController` (back to spawn on Ready), `PlayerFlashlight` (light on at Ready), `BatteryDirector` (clock runs only in Playing) |
| `NewGameStarted` | `GameManager` | `PlayerFlashlight` (full battery) |
| `PlayerCaught` | Dev B's ghosts | `GameManager` |

Pickup rules:

- **Power cell**: raises `PickupCollected`, then counts toward the level (`LevelProgress.Collect`).
- **Flare**: raises `FlareCollected` only, then counts toward the level.
- **Battery**: raises `PickupCollected`, recharges the flashlight by `PlayerConfig.RechargeAmount`, never counts toward the level.
- The level total is cells plus flares on the map (`MazeGrid.CompletionTotal`). `MazePickups` calls `LevelProgress.Begin` with it on every `LevelStarted`.
- Only an object tagged **Player** takes a pickup. The player must keep that tag, or pickups and ghost catches stop working.

## 4. What resets when

| Moment | Player | Cells and flares | Batteries | Flashlight |
|---|---|---|---|---|
| Caught, lives left (Ready again) | back on the start tile | stay taken | stay as they are; their clock is paused | not refilled |
| Level complete, next level | start tile | all back, count restarts | hidden, rules restart | not refilled |
| New game (Retry) | start tile | all back | hidden | full battery |

## 5. Battery rules

Defaults live on the `BatteryDirector` in the scene:

- A battery appears when 25% and again when 65% of the level's cells and flares are taken (`Spawn At Fractions`).
- It appears on the next free `b` spot on the map, under the ghost house. With no free spot it is skipped.
- It disappears after 20 seconds of playing time (`Lifetime Seconds`) if not taken. The clock stops during Ready and after a catch.
- Taking it or letting it expire frees its spot, but a threshold pays out once per level.
- The score for a battery is one value per level, `LevelConfig > Bonus > Battery Points` (Dev C). The recharge is one value for the whole game, `PlayerConfig > Recharge Amount`.

## 6. The map format

Plain text, one glyph per tile, row 0 is the top of the map. Lines starting with `;` are comments.

| Glyph | Tile |
|---|---|
| `#` | wall |
| `_` | empty path |
| `.` | power cell (counts toward the level) |
| `o` | flare (counts toward the level) |
| `b` | battery spot (a bonus, never counts) |
| `G` | ghost house exit, just outside the door |
| `-` | ghost house door (ghosts pass, the player does not) |
| `H` | ghost house tile |
| `P` | player start |
| `W` | warp door, in pairs on the map edge |

World position of a tile: `x = col * 3`, `z = (rows - 1 - row) * 3`. Tiles are 3 m, walls 3.3 m.

Rules enforced when a map is read (`MazeGrid`): exactly one `P`, one `G` and one `-`, at least one `H`,
the door touches `G` on one side and `H` on the other, the map edge is wall or `W`, at least one cell or
flare, an even number of `W`, and every walkable tile reachable from `P`.

Design rules checked by `MazeChecks` (Game > Maze > Validate Maze Setup):

- Error: a battery spot above the ghost house door.
- Warning: no battery spot, no flare, more than 8 flares, fewer than 10 cells and flares, the player start less
  than 3 tiles from the ghost exit, a dead end, a warp door without a partner.

### Adding or changing a map

1. Tell Dev B and Dev D before changing the format (team rule). A new layout with the same glyphs needs no one's approval.
2. Add the `.txt` under `Data/Mazes/`, or edit `Maze_Level1.txt`.
3. Assign it to the `MazeConfig` asset (`Map File`).
4. Run `Game > Maze > Validate Maze Setup` and fix errors.
5. Press Play and walk it.

## 7. Tuning

| What | Where | Default |
|---|---|---|
| Walking speed | `PlayerConfig > Move Speed` | 4.5 m/s |
| Mouse look | `PlayerConfig > Look Sensitivity` | 0.12 |
| Battery size, drain, recharge | `PlayerConfig` | 100, 2 per second, 50 per battery |
| Tile and wall size | `MazeConfig` | 3 m, 3.3 m (art guide; tell Dev D before changing) |
| When batteries appear and for how long | `BatteryDirector` in the scene | 25%, 65%; 20 s |
| Pickup look | materials on `MazePickups` | emissive materials from Dev D, or the `M_*_Placeholder` ones |

## 8. Known limits

- **Warp doors are for the player only.** Ghosts treat a `W` tile as a dead end; they would need NavMesh links. Level 1 has no `W` tiles.
- **Ghosts and the house door.** The door is on the Ignore Raycast layer (layer 2) so the NavMesh bake leaves it out. Do not change that layer.
- **No domain-reload state.** The project runs with domain reload off, so nothing in Dev A's code uses static state. Keep it that way.
- **Frame rate** has only been measured in the Editor (see `MazePerformanceTests`). The real 60 FPS check is a build.

## 9. Tests

Run both lists in Window > General > Test Runner.

| Area | EditMode | PlayMode |
|---|---|---|
| Map and builder | `MazeGridTests`, `MazeWallRunsTests`, `MazeBudgetTests` | `MazeBuilderTests`, `MazeNavMeshTests`, `MazeGhostsTests`, `MazeGameSceneTests` |
| Player | `MazePlayerSpawnTests` | `PlayerControllerTests`, `PlayerGameSceneTests`, `PlayerCollisionTests` |
| Flashlight | none | `PlayerFlashlightTests`, `FlashlightGameSceneTests` |
| Pickups and rules | `PickupConfigTests`, `BatteryRulesTests`, `WarpPairsTests` | `MazePickupsTests`, `PickupsGameSceneTests`, `BatteryDirectorTests`, `MazeWarpsTests`, `ResetGameSceneTests` |
| Checks and whole game | `MazeChecksTests`, `MazeValidatorTests` | `GameFlowIntegrationTests`, `MazePerformanceTests` |

The PlayMode tests that need the real scene load `Game.unity` and need all the menus in section 2 run.
The other PlayMode tests build their own small maze far from the origin (x and z of 2000 to 7000), so
the colliders already in the open scene cannot affect them.

## 10. If something looks wrong

| Symptom | Likely cause |
|---|---|
| "MazeBuilder has no MazeConfig with a map file" | the scene lost its `MazeConfig` reference. Run `Game > Maze > Add Maze To Game Scene` again (it opens the scene first). |
| Pickups missing, or the level never finishes | `MazePickups` not on the maze object. Run the pickups menu, then validate. |
| Pickups or walls look pink | a material is missing. Re-run the pickups menu, or assign materials on `MazeConfig` and `MazePickups`. |
| Pickups cannot be collected | the player is not tagged `Player`, or `PickupConfig` slots on `MazePickups` hold the wrong kinds. |
| Player moves too slowly in a test | the test must set `InputEnabled = false` and drive `Step` itself. |
| A test sees extra ghosts, colliders or pickups | it must build far from the origin and count only its own objects. |
| A trigger test fails | check the player has a `CharacterController` and moves every frame. |
