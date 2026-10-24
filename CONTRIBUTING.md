# Contributing

Short rules so four people can work in one Unity project without breaking each other.

## Setup

- Unity **6000.6.0f1** (the version in `ProjectSettings/ProjectVersion.txt`). Do not upgrade the project in a feature branch.
- Install [Git LFS](https://git-lfs.com) and run `git lfs install` once. Models, textures, audio, video and fonts go through LFS (see `.gitattributes`).
- Serialization is Force Text, so scenes, prefabs and assets are readable YAML.

## Branches

- Name: `feature/<ID>-<short-name>`, for example `feature/A2-player-controller`, `feature/C-systems-ui`.
- Branch from the latest `main`. Rebase or merge `main` in before opening the PR.
- Never push to `main` directly.

## Pull requests

- Fill in the PR template's definition of done.
- Someone other than the author reviews and merges.
- Keep PRs to one work package. Smaller is easier to review in a jam.
- CI runs `tools/check-packages.py` on every PR that touches `Packages/manifest.json`.

## Packages

Only Unity Registry packages (jam rule). Asset Store content is limited to environment, UI, audio and particle assets. Before adding a package, run:

```
python tools/check-packages.py
```

## Scenes and prefabs

- **One person per scene at a time.** Say in the team chat before you open a shared scene for editing. Unity scene merges are painful.
- Prefer prefabs: put your work in a prefab and drop it into the scene once, then edit the prefab.
- `Assets/_Project/Scenes/Game.unity` is the main scene. Dev A adds the maze under `--- Maze goes here ---`.
- The `Game > Systems > Build ...` menu items never overwrite an existing scene or config asset.
- Commit every `.meta` file with its asset. A missing or changed `.meta` breaks references silently.

## Code

- Runtime code: `Assets/_Project/Scripts` (assembly `Game.Runtime`). Editor tools: `Assets/_Project/Editor`. Tests: `Assets/_Project/Tests`.
- Systems talk through ScriptableObject event channels (`Scripts/Events`). No `FindObjectOfType`, `GameObject.Find`, static singletons or `SendMessage`.
- Tunable numbers live in config assets (`ScriptableObjects/Configs`), not in code.
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- The project runs with domain reload disabled (Enter Play Mode Options). Static fields and `[NonSerialized]` ScriptableObject state survive between Play sessions, so reset them explicitly.
- See `Docs/Architecture-ScriptableObjects-and-Events.md` before adding a system.

## Commits

Conventional style, present tense, scoped by package:

```
feat(core): add int, float and bool event channels
feat(ghosts): ...
fix(ui): ...
docs: ...
test: ...
chore: ...
```

## Tests

**Window > General > Test Runner**, run EditMode and PlayMode. Command line (close the editor first):

```
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results-edit.xml
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results-play.xml
```
