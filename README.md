# Pacman Horror (Blackout Maze)

Game Jam 1: Retro Revolution. **Pac-Man (1980) remade as a first-person survival horror game in Unity 3D.**

## Group members

| Member | Role |
| --- | --- |
| Andrea Bayos | Dev A: maze, first-person player, flashlight, pickups, collision detection |
| Elkan La Madrid | Dev B: ghost AI, targeting algorithms, mode cycling |
| Aaron Gonzales | Dev C: event framework, configs, game flow, score and lives, UI, docs |
| Duncan Marcaida | Dev D: art, lighting, audio, VFX, QA validation |

## Description

You are trapped in a pitch-black maze with only a flashlight. Collect every glowing power cell to restore power and advance to the next floor, while four relentless specters hunt you with the iconic arcade personalities of Blinky, Pinky, Inky, and Clyde. 

Lighting an emergency flare floods corridors with blinding light, terrifying the ghosts so you can banish them for escalating combo multipliers ($200 \rightarrow 400 \rightarrow 800 \rightarrow 1600$ points); scavenged batteries replenish dying flashlight reserves; and three catches end your run. It is Pac-Man's legendary loop (clear the maze, avoid the ghosts, turn the tables with a power pellet), experienced from inside the claustrophobic corridors in near-complete darkness, where survival depends on your light and sound.

## Controls

Supports both Keyboard/Mouse and Gamepad controllers:

| Input | Gamepad | Action |
| --- | --- | --- |
| **WASD** / **Arrow keys** | **Left Stick** | Walk / Strafe |
| **Mouse** | **Right Stick** | Look (pitch & yaw) |
| **F** / **Left Click** | **Right Shoulder (RB / R1)** | Toggle Flashlight |
| **Esc** | **Start / Menu** | Pause / Resume |

## Running the project

1. Install Unity **6000.6.0f1** with Unity Hub, and [Git LFS](https://git-lfs.com) (`git lfs install`).
2. Clone the repository and open the folder in Unity Hub.
3. Open `Assets/_Project/Scenes/MainMenu.unity` and press **Play**, or open `Assets/_Project/Scenes/Game.unity` to jump straight into the maze.

The build settings list `MainMenu` first (Scene 0), followed by `Game` (Scene 1).

### Automated Setup & QA Tools

If opening a clean clone or resetting scene setups, use the automated builder tools located under the **Game** menu:
- **Game > QA > Setup Developer D Audiovisual Systems**: Deploys lighting, volume profiles, audio managers, VFX pools, and ghost meshes into `Game.unity`.
- **Game > QA > Validate Audiovisual & Performance Setup**: Audits dynamic light budgets ($\le 8$), event channel bindings, and zero-allocation configs for locked 60 FPS.
- **Game > Maze > Validate Maze Setup**: Checks maze tile rules, player spawn, ghost home points, and pickup metrics.
- **Game > Audio > Generate Audio Assets & Cues**: Procedurally synthesizes retro-arcade horror audio waveforms and AudioCue ScriptableObjects.
- **Game > Systems > Generate UI Art**: Regenerates procedural UI textures (scanlines, vignette, film grain, life icons).

## Folder overview

| Path | Contents |
| --- | --- |
| `Assets/_Project/Scripts/Core` | Game configurations (`GameRulesConfig`, `LevelConfig`, `PlayerConfig`, `PickupConfig`) and runtime assets (`LevelProgress`, `TransformAnchor`) |
| `Assets/_Project/Scripts/Events` | ScriptableObject event channels and generic listeners (the decoupled event bus) |
| `Assets/_Project/Scripts/Systems` | `GameManager` state machine, score system, lives system, persistent high score, pause, cursor controller |
| `Assets/_Project/Scripts/UI` | HUD view, main menu, pause menu, game over view, controls guide, settings view, CRT post-effects |
| `Assets/_Project/Scripts/Player` | Kinematic `CharacterController` first-person locomotion, head look, flashlight switch, battery discharge |
| `Assets/_Project/Scripts/Maze` | Text map parser, procedural maze builder, runtime NavMesh surface baker, ghost anchors, warp corridors |
| `Assets/_Project/Scripts/Pickups` | Trigger-based power cells, emergency flares, and dynamic battery spawning (`BatteryDirector`) |
| `Assets/_Project/Scripts/Ghosts` | Ghost FSM (`Scatter`, `Chase`, `Frightened`, `Eaten`), targeting algorithms, flashlight slowdown sensor |
| `Assets/_Project/Scripts/Lighting` | Central `LightingManager`, camera-tethered `FlashlightController`, dynamic light budgeter, `PostProcessRig` |
| `Assets/_Project/Scripts/Audio` | `AudioManager`, `AudioCue` ScriptableObjects, zero-allocation spatial audio pooling, siren crossfading |
| `Assets/_Project/Scripts/VFX` | `VFXManager`, zero-allocation pre-allocated particle pools (flare blast, cell collect, banish, jumpscare) |
| `Assets/_Project/ScriptableObjects` | Channel assets (`Events/`), configurations (`Configs/`), audio cues (`Audio/`), and runtime sets (`Runtime/`) |
| `Assets/_Project/Art` | Materials, custom 3D Pac-Man ghost meshes, textures, pixel fonts, and horror URP Volume Profile |
| `Assets/_Project/Prefabs` | 3D ghost prefabs/variants, pooled VFX prefabs (`PF_CellCollectFX`, `PF_FlareIgnitionFX`, `PF_GhostBanishFX`, `PF_PlayerCaughtFX`) |
| `Assets/_Project/Data/Mazes` | Plaintext maze layout matrices (e.g. `Maze_Level1.txt`) |
| `Assets/_Project/Scenes` | `MainMenu.unity`, `Game.unity` |
| `Assets/_Project/Editor` | Automation builders under the **Game** menu, custom inspectors, procedural asset generators, and QA validators |
| `Assets/_Project/Tests` | Comprehensive automated test suites (40+ EditMode and 35+ PlayMode tests) |
| `Assets/_Project/Dev/GhostSandbox` | Isolated ghost AI testing sandbox |
| `Docs` | Architecture guides, art/metrics guide, package technical docs, and presentation slide deck outline |
| `tools` | Registry package compliance checker (`check-packages.py`) |

## Status

| Part | Owner | State |
| --- | --- | --- |
| **Event Architecture & Game Systems** | Dev C | **Done & Verified**: Decoupled ScriptableObject event bus, GameManager state machine, lives/score systems, JSON high scores, pause controller. |
| **UI & CRT Horror Presentation** | Dev C | **Done & Verified**: Retro arcade HUD, menus, Press Start 2P typography, CRT scanlines, vignette, and animated film grain. |
| **Maze, Player, Pickups & Collisions** | Dev A | **Done & Integrated**: Procedural text-map maze generation, CharacterController first-person movement, trigger pickups, dynamic battery spawning, warp corridors. |
| **Ghost AI & Mode Cycling** | Dev B | **Done & Integrated**: 4 unique targeting personalities (Blinky, Pinky, Inky, Clyde), Scatter/Chase timers, flare fright state, house routing, flashlight slowdown. |
| **Art, Lighting, Audio, VFX & QA** | Dev D | **Done & Integrated**: URP post-processing stack (ACES, Bloom, horror grain), dynamic spotlight with sway & low-battery flicker, 6–8 dynamic light budget for locked 60 FPS, procedural audio synthesizer & spatial AudioCue manager, pooled VFX, 3D ghost meshes, automated QA suite. |

The project is **fully playable from end to end**: Main Menu $\rightarrow$ Ready countdown $\rightarrow$ Active First-Person Maze $\rightarrow$ Score/Lives/High Score loop $\rightarrow$ Level Complete progression / Game Over retry screen.

## Jam Requirements & Architecture

- **The game is 3D**: First-person 3D maze, 3D CharacterController physics, 3D ghost meshes, and dynamic real-time lighting.
- **At least 1 playable level**: Fully playable maze level (`Maze_Level1.txt`) that escalates difficulty on successive loops.
- **Base gameplay loop**: Direct 1-to-1 parity with Pac-Man (1980) reimagined as first-person survival horror.
- **Unity Registry packages only**: Universal Render Pipeline (`com.unity.render-pipelines.universal`), Input System (`com.unity.inputsystem`), AI Navigation (`com.unity.ai.navigation`), and TextMeshPro (`com.unity.textmeshpro`). Verified by `tools/check-packages.py`.
- **Core Feature 1 (Scriptable Objects)**: All game balance data, level curves, player attributes, ghost profiles, audio cues, and runtime collections reside in ScriptableObjects with zero hardcoded values.
- **Core Feature 2 (Event Broadcasting)**: Completely decoupled publisher-subscriber architecture using ScriptableObject event channels. No polling loops, singletons, or direct inter-system references.
- **Secondary Feature 1 (Lighting)**: Dynamic camera spotlight with ghost slowdown raycasting and battery flicker, Bloom-driven emissive pickups, reactive horror post-processing, strictly budgeted ($\le 8$ active lights) for locked 60 FPS.
- **Secondary Feature 2 (Collision Detection)**: Kinematic CharacterController movement, static trigger-based pickup collection, and spherical proximity zones for ghost captures and banishments.

## Documentation

- [Docs/Architecture-ScriptableObjects-and-Events.md](Docs/Architecture-ScriptableObjects-and-Events.md): Core ScriptableObject event architecture and decoupled channel bus
- [Docs/Art-and-Level-Design-Guide.md](Docs/Art-and-Level-Design-Guide.md): Visual aesthetics, corridor/capsule metrics, lighting budget, and pickup specifications
- [Docs/Package-D-Audiovisual-QA.md](Docs/Package-D-Audiovisual-QA.md): URP post-processing, event-to-listener matrix, zero-allocation pooling, and 60 FPS performance checklist
- [Docs/DevA_Handoff.md](Docs/DevA_Handoff.md): Procedural maze builder, player locomotion, pickup triggers, battery director, and warp doors
- [Docs/DevA_Collision_And_Player_Slides.md](Docs/DevA_Collision_And_Player_Slides.md): Collision detection and player physics presentation slide content
- [Docs/Package-B-Ghost-AI.md](Docs/Package-B-Ghost-AI.md): Ghost AI state machines, targeting algorithms, and house behaviors
- [Docs/Package-C-Systems.md](Docs/Package-C-Systems.md): Core systems, GameManager flow, score/lives, and UI views
- [Docs/Deck/Deck-Outline.md](Docs/Deck/Deck-Outline.md): Technical presentation slide deck outline
- [CONTRIBUTING.md](CONTRIBUTING.md): Git branching rules, PR guidelines, scene discipline, and coding standards

## Testing

Run tests via Unity Test Runner (**Window > General > Test Runner**):
- **EditMode Tests**: 40+ unit tests covering event channels, configurations, high score persistence, maze grid parsing, wall runs, audio cues, and lighting rules.
- **PlayMode Tests**: 35+ integration tests covering player movement, collision detection, flashlight battery depletion, ghost behavior in the maze, battery spawning, and scene transitions.

Command line execution:
```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results-edit.xml
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results-play.xml
```

## Credits

- **Font**: [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P) by CodeMan38, SIL Open Font License 1.1 (`Assets/_Project/Art/Fonts/OFL.txt`).
- **Procedural Audio**: Authentic 16-bit PCM retro-horror audio files and AudioCue assets generated in code via `AudioAssetGenerator` (**Game > Audio > Generate Audio Assets & Cues**).
- **Procedural 3D Art & VFX**: 3D Pac-Man horror ghost meshes generated via `GhostMeshGenerator`; bloom emissive materials and pooled particle systems generated via `VFXAssetGenerator`.
- **Procedural UI Textures**: Scanlines, grain, vignette, life icons, and ghost HUD icons generated via `UiArtGenerator` (**Game > Systems > Generate UI Art**).
