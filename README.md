# Blackout Maze

*(working title, TODO: final name)*

Game Jam 1: Retro Revolution. **Pac-Man (1980) remade as a first-person survival horror game.**

## Group members

TODO: add names

| Member | Role |
| --- | --- |
| TODO | Dev A: maze, first-person player, flashlight, pickups, collision detection |
| TODO | Dev B: ghost AI |
| TODO | Dev C: event framework, configs, game flow, score and lives, UI, docs |
| TODO | Dev D: art, lighting, audio, QA |

## Description

You are trapped in a pitch-black maze with only a flashlight. Collect every power cell to restore the lights and reach the next floor, while four specters hunt you with the personalities of Blinky, Pinky, Inky and Clyde. Lighting a flare terrifies them so you can banish them for growing combo points; a battery recharges your flashlight; and three catches end the run. It is Pac-Man's loop (clear the maze, avoid the ghosts, turn the tables with a power pellet), seen from inside the maze, in the dark, where you can no longer see who is coming.

## Controls

*(Draft, to confirm with Dev A once the player controller exists.)*

| Input | Action |
| --- | --- |
| WASD / Arrow keys | Move |
| Mouse | Look |
| Left click | Toggle flashlight |
| Esc / Gamepad Start | Pause |

## Running the project

1. Install Unity **6000.6.0f1** with Unity Hub, and [Git LFS](https://git-lfs.com) (`git lfs install`).
2. Clone the repo and open the folder in Unity Hub.
3. Open `Assets/_Project/Scenes/MainMenu.unity` and press Play, or open `Assets/_Project/Scenes/Game.unity` to start straight in the game (it starts by itself).

The build settings list `MainMenu` first, then `Game`.

## Folder overview

| Path | Contents |
| --- | --- |
| `Assets/_Project/Scripts/Events` | ScriptableObject event channels (the game's event bus) |
| `Assets/_Project/Scripts/Core` | Configs (`GameRulesConfig`, `LevelConfig`, `PickupConfig`, ...) and runtime assets (`LevelProgress`, `TransformAnchor`) |
| `Assets/_Project/Scripts/Systems` | GameManager, score, lives, high score, pause, cursor |
| `Assets/_Project/Scripts/UI` | HUD, menus, pause, game over, controls, settings |
| `Assets/_Project/Scripts/Ghosts` | Ghost AI |
| `Assets/_Project/ScriptableObjects` | Config, channel and runtime assets |
| `Assets/_Project/Scenes` | `MainMenu`, `Game` |
| `Assets/_Project/Editor` | Builders under the **Game** menu, inspectors |
| `Assets/_Project/Tests` | EditMode and PlayMode tests |
| `Assets/_Project/Dev/GhostSandbox` | Test-only ghost sandbox (not in the build) |
| `Docs` | Package guides, architecture reference, slide deck outline |
| `tools` | `check-packages.py` (Unity Registry only check) |

## Status

| Part | Owner | State |
| --- | --- | --- |
| Event channels, configs, GameManager flow, score, lives, high score, pause, all UI | Dev C | Done, tested (EditMode + PlayMode) |
| Ghost AI | Dev B | Done on its branch, tested in its sandbox |
| Maze, player, flashlight, pickups | Dev A | Not started in this repo yet. The `Game` scene has an empty `--- Maze goes here ---` root |
| Art, lighting, audio | Dev D | Not started. UI is a plain dark placeholder |

Today the `Game` scene runs the full state flow (Ready, Playing, pause, caught, game over, retry, main menu) without a maze. It is not yet playable as a game.

## Documentation

- [Docs/Architecture-ScriptableObjects-and-Events.md](Docs/Architecture-ScriptableObjects-and-Events.md): how the two core features (Scriptable Objects, Event Broadcasting) drive the game
- [Docs/Package-C-Systems.md](Docs/Package-C-Systems.md): systems and UI, and how to plug into them
- [Docs/Package-B-Ghost-AI.md](Docs/Package-B-Ghost-AI.md): ghost AI
- [Docs/Deck/Deck-Outline.md](Docs/Deck/Deck-Outline.md): technical documentation slide deck content
- [CONTRIBUTING.md](CONTRIBUTING.md): branches, PRs, scene rules, tests

## Jam requirements

Unity Registry packages only (checked by `tools/check-packages.py` and CI), 3D, one playable level, Pac-Man's gameplay loop. The project implements **Scriptable Objects** and **Event Broadcasting** at its core, plus Lighting, Physics and Collision Detection (Dev A, Dev D).
