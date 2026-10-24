#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Events;
using Game.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    // Drives GameManager, LivesSystem and PauseController through freshly made channels and
    // short timings, the same way the ghosts and the maze will: only through events.
    public class GameManagerTests
    {
        const float Short = 0.05f;

        readonly List<Object> created = new();
        readonly List<string> log = new();
        readonly List<LevelConfig> started = new();

        GameManager manager;
        PauseController pause;
        LevelProgress progress;
        LevelConfig level1, level2;

        GameStateEventChannel gameStateChanged;
        LevelConfigEventChannel levelStarted;
        IntEventChannel livesChanged, levelNumberChanged;
        VoidEventChannel playerCaught, levelComplete, newGameStarted, lifeLost, extraLifeAwarded,
            restartRequested, mainMenuRequested;
        BoolEventChannel pauseRequested, pauseChanged;
        bool? lastPause;

        [SetUp]
        public void Build()
        {
            var rules = Make<GameRulesConfig>();
            Set(rules, so =>
            {
                so.FindProperty("startingLives").intValue = 3;
                so.FindProperty("readySeconds").floatValue = Short;
                so.FindProperty("caughtSeconds").floatValue = Short;
                so.FindProperty("levelCompleteSeconds").floatValue = Short;
                so.FindProperty("gameOverSeconds").floatValue = Short;
            });

            level1 = Make<LevelConfig>();
            level2 = Make<LevelConfig>();
            var levels = Make<LevelSequence>();
            Set(levels, so =>
            {
                SerializedProperty list = so.FindProperty("levels");
                list.arraySize = 2;
                list.GetArrayElementAtIndex(0).objectReferenceValue = level1;
                list.GetArrayElementAtIndex(1).objectReferenceValue = level2;
            });
            progress = Make<LevelProgress>();

            gameStateChanged = Make<GameStateEventChannel>();
            levelStarted = Make<LevelConfigEventChannel>();
            livesChanged = Make<IntEventChannel>();
            levelNumberChanged = Make<IntEventChannel>();
            playerCaught = Make<VoidEventChannel>();
            levelComplete = Make<VoidEventChannel>();
            newGameStarted = Make<VoidEventChannel>();
            lifeLost = Make<VoidEventChannel>();
            extraLifeAwarded = Make<VoidEventChannel>();
            restartRequested = Make<VoidEventChannel>();
            mainMenuRequested = Make<VoidEventChannel>();
            pauseRequested = Make<BoolEventChannel>();
            pauseChanged = Make<BoolEventChannel>();

            // Inactive while wiring, so OnEnable runs once with every field set.
            var go = new GameObject("Game Systems (test)");
            go.SetActive(false);
            created.Add(go);

            manager = go.AddComponent<GameManager>();
            Wire(manager,
                ("rules", rules), ("levels", levels), ("progress", progress),
                ("playerCaught", playerCaught), ("livesChanged", livesChanged),
                ("restartRequested", restartRequested), ("mainMenuRequested", mainMenuRequested),
                ("gameStateChanged", gameStateChanged), ("levelStarted", levelStarted),
                ("levelNumberChanged", levelNumberChanged), ("levelComplete", levelComplete),
                ("newGameStarted", newGameStarted), ("lifeLost", lifeLost));
            Set(manager, so => so.FindProperty("autoStart").boolValue = false);

            Wire(go.AddComponent<LivesSystem>(),
                ("rules", rules), ("newGameStarted", newGameStarted), ("lifeLost", lifeLost),
                ("extraLifeAwarded", extraLifeAwarded), ("livesChanged", livesChanged));

            pause = go.AddComponent<PauseController>();
            Wire(pause, ("gameStateChanged", gameStateChanged), ("pauseRequested", pauseRequested),
                ("pauseChanged", pauseChanged));

            gameStateChanged.Raised += OnState;
            levelStarted.Raised += OnLevelStarted;
            levelComplete.Raised += OnLevelComplete;
            pauseChanged.Raised += OnPauseChanged;

            go.SetActive(true);
        }

        [TearDown]
        public void CleanUp()
        {
            gameStateChanged.Raised -= OnState;
            levelStarted.Raised -= OnLevelStarted;
            levelComplete.Raised -= OnLevelComplete;
            pauseChanged.Raised -= OnPauseChanged;

            foreach (Object item in created)
                Object.DestroyImmediate(item);

            created.Clear();
            log.Clear();
            started.Clear();
            lastPause = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator NewGameRaisesLevelStartedThenReadyThenPlaying()
        {
            manager.BeginGame();

            Assert.That(started, Is.EqualTo(new[] { level1 }));
            Assert.That(manager.Lives, Is.EqualTo(3));
            yield return WaitFor(GameState.Playing);

            Assert.That(log, Is.EqualTo(new[] { "LevelStarted", "Ready", "Playing" }));
        }

        [UnityTest]
        public IEnumerator CatchCostsALifeThenReadyAndPlayingAgain()
        {
            manager.BeginGame();
            yield return WaitFor(GameState.Playing);
            log.Clear();

            playerCaught.Raise();
            Assert.That(manager.State, Is.EqualTo(GameState.PlayerCaught));
            Assert.That(manager.Lives, Is.EqualTo(2));

            yield return WaitFor(GameState.Playing);
            Assert.That(log, Is.EqualTo(new[] { "PlayerCaught", "Ready", "Playing" }));
            Assert.That(started.Count, Is.EqualTo(1), "a catch does not restart the level (cells stay collected)");
        }

        [UnityTest]
        public IEnumerator ThirdCatchIsGameOver()
        {
            manager.BeginGame();

            for (int i = 0; i < 3; i++)
            {
                yield return WaitFor(GameState.Playing);
                playerCaught.Raise();
            }

            yield return WaitFor(GameState.GameOver);
            Assert.That(manager.Lives, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator RetryAfterGameOverStartsAFreshGame()
        {
            manager.BeginGame();
            for (int i = 0; i < 3; i++)
            {
                yield return WaitFor(GameState.Playing);
                playerCaught.Raise();
            }
            yield return WaitFor(GameState.GameOver);

            restartRequested.Raise();

            Assert.That(manager.Lives, Is.EqualTo(3));
            Assert.That(manager.LevelIndex, Is.EqualTo(0));
            yield return WaitFor(GameState.Playing);
        }

        [UnityTest]
        public IEnumerator CatchOutsidePlayingIsIgnored()
        {
            manager.BeginGame();
            Assert.That(manager.State, Is.EqualTo(GameState.Ready));

            playerCaught.Raise();

            Assert.That(manager.State, Is.EqualTo(GameState.Ready));
            Assert.That(manager.Lives, Is.EqualTo(3));
            yield return WaitFor(GameState.Playing);
        }

        [UnityTest]
        public IEnumerator LastCellCompletesTheLevelAndStartsTheNextConfig()
        {
            manager.BeginGame();
            yield return WaitFor(GameState.Playing);

            progress.Begin(2);
            progress.Collect();
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));

            progress.Collect();
            Assert.That(manager.State, Is.EqualTo(GameState.LevelComplete));
            Assert.That(log, Has.Member("LevelComplete channel"));

            yield return WaitFor(GameState.Playing);
            Assert.That(started, Is.EqualTo(new[] { level1, level2 }));
            Assert.That(manager.LevelIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PauseFreezesTimeOnlyWhilePlayingAndAlwaysRestoresIt()
        {
            manager.BeginGame();
            pause.SetPaused(true);
            Assert.That(pause.IsPaused, Is.False, "no pause during Ready");

            yield return WaitFor(GameState.Playing);
            pause.SetPaused(true);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(lastPause, Is.True);

            pauseRequested.Raise(false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(lastPause, Is.False);

            // Leaving Playing (here a catch) always resumes.
            pause.SetPaused(true);
            playerCaught.Raise();
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return WaitFor(GameState.Playing);
        }

        [UnityTest]
        public IEnumerator DisablingPauseControllerRestoresTime()
        {
            manager.BeginGame();
            yield return WaitFor(GameState.Playing);
            pause.SetPaused(true);

            pause.enabled = false;

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        IEnumerator WaitFor(GameState state)
        {
            float giveUp = Time.realtimeSinceStartup + 3f;
            while (manager.State != state && Time.realtimeSinceStartup < giveUp)
                yield return null;

            Assert.That(manager.State, Is.EqualTo(state));
        }

        void OnState(GameState state) => log.Add(state.ToString());

        void OnLevelStarted(LevelConfig config)
        {
            started.Add(config);
            log.Add("LevelStarted");
        }

        void OnLevelComplete() => log.Add("LevelComplete channel");

        void OnPauseChanged(bool paused) => lastPause = paused;

        T Make<T>() where T : ScriptableObject
        {
            var item = ScriptableObject.CreateInstance<T>();
            created.Add(item);
            return item;
        }

        static void Set(Object target, Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(Object target, params (string field, Object value)[] fields)
        {
            Set(target, so =>
            {
                foreach ((string field, Object value) in fields)
                    so.FindProperty(field).objectReferenceValue = value;
            });
        }
    }
}
#endif
