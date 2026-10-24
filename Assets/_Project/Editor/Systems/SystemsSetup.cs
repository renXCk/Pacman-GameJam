using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Builds Package C's assets and scenes. Every step is safe to run again: it never overwrites
    // an existing config, channel or scene (scenes ask first in the Editor and are skipped in batch).
    //   Game > Systems > Create Systems Assets
    //   Game > Systems > Build Main Menu Scene / Build Game Scene
    //   Game > Systems > Update Build Settings
    //   Game > Systems > Validate Setup
    // Batch mode, in two runs (the first imports TMP Essentials, which needs its own import pass
    // and exits by itself, so no -quit):
    //   Unity -batchmode -projectPath <path> -executeMethod Game.EditorTools.SystemsSetup.ImportTmpEssentials
    //   Unity -batchmode -projectPath <path> -executeMethod Game.EditorTools.SystemsSetup.BuildAll -quit
    public static class SystemsSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        public const string SceneFolder = Root + "/Scenes";
        public const string MainMenuScenePath = SceneFolder + "/MainMenu.unity";
        public const string GameScenePath = SceneFolder + "/Game.unity";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        // Everything the scenes wire together, loaded or created once.
        public sealed class Assets
        {
            public GameRulesConfig Rules;
            public UiConfig Ui;
            public PlayerConfig Player;
            public PickupConfig Cell, Flare, Battery;
            public LevelSequence Levels;
            public LevelProgress Progress;

            public GameStateEventChannel GameStateChanged;
            public LevelConfigEventChannel LevelStarted;
            public VoidEventChannel PlayerCaught, FlareCollected, LevelComplete, NewGameStarted, LifeLost,
                ExtraLifeAwarded, RestartRequested, MainMenuRequested;
            public GhostBanishedEventChannel GhostBanished;
            public PickupEventChannel PickupCollected;
            public IntEventChannel ScoreChanged, LivesChanged, HighScoreChanged, LevelNumberChanged;
            public FloatEventChannel BatteryChanged;
            public BoolEventChannel PauseChanged, PauseRequested;
        }

        [MenuItem("Game/Systems/Create Systems Assets")]
        public static void CreateSystemsAssetsMenu()
        {
            CreateAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("Systems assets are in place.");
        }

        [MenuItem("Game/Systems/Build Main Menu Scene")]
        public static void BuildMainMenuMenu()
        {
            if (!EnsureTmpEssentials() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!ConfirmOverwrite(MainMenuScenePath))
                return;

            SystemsSceneBuilder.BuildMainMenu(MainMenuScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Game/Systems/Build Game Scene")]
        public static void BuildGameMenu()
        {
            if (!EnsureTmpEssentials() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!ConfirmOverwrite(GameScenePath))
                return;

            SystemsSceneBuilder.BuildGame(GameScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Game/Systems/Update Build Settings")]
        public static void UpdateBuildSettings()
        {
            var wanted = new[] { MainMenuScenePath, GameScenePath }.Where(File.Exists).ToList();
            var scenes = wanted.Select(path => new EditorBuildSettingsScene(path, true)).ToList();

            // Keep anything else already listed, switched off, so nobody loses an entry by surprise.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!wanted.Contains(existing.path))
                    scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"Build settings: {string.Join(", ", wanted)} first and enabled.");
        }

        [MenuItem("Game/Systems/Validate Setup")]
        public static void ValidateMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                SystemsValidator.Run();
        }

        // Batch step 1. Imports TextMesh Pro's default font and settings into Assets/TextMesh Pro.
        // Package import finishes on a later editor tick, so in batch mode run this WITHOUT -quit:
        // it exits by itself once the import completes or fails.
        public static void ImportTmpEssentials()
        {
            if (HasTmpEssentials())
            {
                Debug.Log("TMP Essential Resources already imported.");
                ExitIfBatch(0);
                return;
            }

            AssetDatabase.importPackageCompleted += OnImported;
            AssetDatabase.importPackageFailed += OnImportFailed;
            UnityEditor.AssetPackage.Package.Import(Path.GetFullPath(TmpEssentials), false);
        }

        static void OnImported(string package)
        {
            AssetDatabase.importPackageCompleted -= OnImported;
            AssetDatabase.importPackageFailed -= OnImportFailed;
            Debug.Log($"Imported {package}.");
            ExitIfBatch(0);
        }

        static void OnImportFailed(string package, string error)
        {
            AssetDatabase.importPackageCompleted -= OnImported;
            AssetDatabase.importPackageFailed -= OnImportFailed;
            Debug.LogError($"Could not import {package}: {error}");
            ExitIfBatch(1);
        }

        static void ExitIfBatch(int code)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }

        // Batch step 2. Assets, both scenes (only if missing), build settings, then validation.
        public static void BuildAll()
        {
            if (!HasTmpEssentials())
                throw new InvalidOperationException("Run SystemsSetup.ImportTmpEssentials first (TMP Essential Resources are missing).");

            CreateAssets();
            AssetDatabase.SaveAssets();

            if (!File.Exists(MainMenuScenePath))
                SystemsSceneBuilder.BuildMainMenu(MainMenuScenePath);
            else
                Debug.Log($"{MainMenuScenePath} exists, left as it is.");

            if (!File.Exists(GameScenePath))
                SystemsSceneBuilder.BuildGame(GameScenePath);
            else
                Debug.Log($"{GameScenePath} exists, left as it is.");

            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            int problems = SystemsValidator.Run();
            Debug.Log($"SystemsSetup.BuildAll finished with {problems} validation problem(s).");
        }

        public static Assets CreateAssets()
        {
            foreach (string folder in new[] { ConfigFolder, EventFolder, RuntimeFolder, SceneFolder })
                EnsureFolder(folder);

            var a = new Assets
            {
                Rules = LoadOrCreate<GameRulesConfig>(ConfigFolder + "/GameRulesConfig.asset"),
                Ui = LoadOrCreate<UiConfig>(ConfigFolder + "/UiConfig.asset"),
                Player = LoadOrCreate<PlayerConfig>(ConfigFolder + "/PlayerConfig.asset"),
                Cell = Pickup("PickupConfig_Cell", PickupKind.Cell, "Power Cell", 10, true),
                Flare = Pickup("PickupConfig_Flare", PickupKind.Flare, "Flare", 50, true),
                Battery = Pickup("PickupConfig_Battery", PickupKind.Battery, "Battery", 100, false),
                Progress = LoadOrCreate<LevelProgress>(RuntimeFolder + "/LevelProgress.asset")
            };

            // Dev B's three levels, in order. Each replay is harder; past Level 3 it stays at Level 3.
            var levels = new[] { 1, 2, 3 }
                .Select(n => AssetDatabase.LoadAssetAtPath<LevelConfig>($"{ConfigFolder}/LevelConfig_Level{n}.asset"))
                .Where(level => level != null)
                .ToArray();
            a.Levels = LoadOrCreate<LevelSequence>(ConfigFolder + "/LevelSequence.asset",
                so => SetArray(so.FindProperty("levels"), levels));

            // Dev B's channels are reused as they are; the rest are new.
            a.GameStateChanged = Channel<GameStateEventChannel>("GameStateChanged",
                "Raised by GameManager (Dev C). Heard by GhostDirector, UI, audio, player input.");
            a.LevelStarted = Channel<LevelConfigEventChannel>("LevelStarted",
                "Raised by GameManager with the level's LevelConfig, before GameStateChanged(Ready). Heard by GhostDirector, score, maze.");
            a.PlayerCaught = Channel<VoidEventChannel>("PlayerCaught",
                "Raised by GhostDirector when a ghost catches the player. Heard by GameManager, lives UI, lighting, audio.");
            a.FlareCollected = Channel<VoidEventChannel>("FlareCollected",
                "Raised by pickups (Dev A) when the player collects a flare. Heard by GhostDirector (all ghosts frightened), lighting, audio, score.");
            a.GhostBanished = Channel<GhostBanishedEventChannel>("GhostBanished",
                "Raised by GhostDirector when the player touches a frightened ghost. Carries combo and points. Heard by score, audio, VFX.");

            a.PickupCollected = Channel<PickupEventChannel>("PickupCollected",
                "Raised by pickups (Dev A) with their PickupConfig for power cells and batteries. Heard by score, audio, VFX.");
            a.LevelComplete = Channel<VoidEventChannel>("LevelComplete",
                "Raised by GameManager when the last cell is collected, right after GameStateChanged(LevelComplete). Heard by audio, lighting.");
            a.NewGameStarted = Channel<VoidEventChannel>("NewGameStarted",
                "Raised by GameManager when a new game begins (also on Retry). Heard by score and lives (reset), game over screen.");
            a.LifeLost = Channel<VoidEventChannel>("LifeLost",
                "Raised by GameManager once per catch while Playing. Heard by LivesSystem, which answers on LivesChanged.");
            a.ExtraLifeAwarded = Channel<VoidEventChannel>("ExtraLifeAwarded",
                "Raised by ScoreSystem once per game at GameRulesConfig.extraLifeScore. Heard by LivesSystem, audio.");
            a.RestartRequested = Channel<VoidEventChannel>("RestartRequested",
                "Raised by the game over screen's Retry button. Heard by GameManager.");
            a.MainMenuRequested = Channel<VoidEventChannel>("MainMenuRequested",
                "Raised by the pause and game over Main Menu buttons. Heard by GameManager (loads the menu), HighScoreSystem (saves).");
            a.ScoreChanged = Channel<IntEventChannel>("ScoreChanged",
                "Raised by ScoreSystem with the new total. Heard by HUD, HighScoreSystem, game over screen, audio.");
            a.LivesChanged = Channel<IntEventChannel>("LivesChanged",
                "Raised by LivesSystem with the lives left. Heard by GameManager, HUD.");
            a.HighScoreChanged = Channel<IntEventChannel>("HighScoreChanged",
                "Raised by HighScoreSystem with the saved best at scene start and whenever it is beaten. Heard by HUD, menu, game over screen.");
            a.LevelNumberChanged = Channel<IntEventChannel>("LevelNumberChanged",
                "Raised by GameManager with the 1-based level number, just before LevelStarted. Heard by HUD.");
            a.BatteryChanged = Channel<FloatEventChannel>("BatteryChanged",
                "Raised by the flashlight (Dev A) with its charge from 0 to 1 when it changes. Heard by HUD, audio.");
            a.PauseChanged = Channel<BoolEventChannel>("PauseChanged",
                "Raised by PauseController when the game pauses (true) or resumes (false). Heard by pause menu, cursor, audio.");
            a.PauseRequested = Channel<BoolEventChannel>("PauseRequested",
                "Raised by UI to ask for pause (true) or resume (false), for example the Resume button. Heard by PauseController.");

            return a;
        }

        static PickupConfig Pickup(string file, PickupKind kind, string displayName, int points, bool countsTowardLevel) =>
            LoadOrCreate<PickupConfig>($"{ConfigFolder}/{file}.asset", so =>
            {
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("basePoints").intValue = points;
                so.FindProperty("countsTowardLevel").boolValue = countsTowardLevel;
            });

        static T Channel<T>(string file, string description) where T : EventChannelBase =>
            LoadOrCreate<T>($"{EventFolder}/{file}.asset", so => so.FindProperty("description").stringValue = description);

        static T LoadOrCreate<T>(string path, Action<SerializedObject> setup = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            if (setup != null)
            {
                var so = new SerializedObject(asset);
                setup(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        public static void SetArray(SerializedProperty array, IReadOnlyList<Object> items)
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static bool HasTmpEssentials() => Resources.Load<TMP_Settings>("TMP Settings") != null;

        static bool EnsureTmpEssentials()
        {
            if (HasTmpEssentials())
                return true;

            ImportTmpEssentials();
            EditorUtility.DisplayDialog("TextMesh Pro",
                "TMP Essential Resources were just imported. Run the menu item again once the import finishes.", "OK");
            return false;
        }

        static bool ConfirmOverwrite(string path)
        {
            if (!File.Exists(path))
                return true;

            return EditorUtility.DisplayDialog("Scene exists",
                $"{path} already exists. Rebuilding replaces it, including anything added by hand (the maze too).",
                "Rebuild", "Cancel");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
