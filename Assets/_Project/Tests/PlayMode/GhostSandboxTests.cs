#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using Game.Sandbox;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    // Plays the ghost sandbox scene and drives it through the same channels GameManager will use.
    public class GhostSandboxTests
    {
        const string ScenePath = "Assets/_Project/Dev/GhostSandbox/GhostSandbox.unity";
        const string EventFolder = "Assets/_Project/ScriptableObjects/Events/";
        const string ChaseForever = "{\"scatterChaseSeconds\":[0]}";

        GhostDirector director;
        StandaloneGameFlow flow;
        SandboxPlayer player;
        Light flashlight;
        Ghost blinky, pinky, inky, clyde;

        GameStateEventChannel gameState;
        LevelConfigEventChannel levelStarted;
        VoidEventChannel flareCollected;
        VoidEventChannel playerCaught;
        GhostBanishedEventChannel ghostBanished;
        GhostModeEventChannel modeChanged;

        int caughtCount;
        readonly List<GhostBanishedInfo> banished = new();
        readonly List<GhostModeChange> modes = new();
        readonly List<Object> created = new();

        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            // The tests play the part of GameManager themselves.
            flow = InScene<StandaloneGameFlow>();
            flow.StopAllCoroutines();
            flow.enabled = false;

            director = InScene<GhostDirector>();
            player = InScene<SandboxPlayer>();
            flashlight = player.GetComponentInChildren<Light>();

            foreach (Ghost ghost in AllGhosts())
            {
                switch (ghost.Config.ChaseRule)
                {
                    case ChaseRule.Blinky: blinky = ghost; break;
                    case ChaseRule.Pinky: pinky = ghost; break;
                    case ChaseRule.Inky: inky = ghost; break;
                    case ChaseRule.Clyde: clyde = ghost; break;
                }
            }

            gameState = Load<GameStateEventChannel>("GameStateChanged");
            levelStarted = Load<LevelConfigEventChannel>("LevelStarted");
            flareCollected = Load<VoidEventChannel>("FlareCollected");
            playerCaught = Load<VoidEventChannel>("PlayerCaught");
            ghostBanished = Load<GhostBanishedEventChannel>("GhostBanished");
            modeChanged = Load<GhostModeEventChannel>("ModeChanged");

            playerCaught.Raised += OnCaught;
            ghostBanished.Raised += OnBanished;
            modeChanged.Raised += OnModeChanged;

            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            playerCaught.Raised -= OnCaught;
            ghostBanished.Raised -= OnBanished;
            modeChanged.Raised -= OnModeChanged;

            Time.timeScale = 1f;
            foreach (Object item in created)
                Object.Destroy(item);

            created.Clear();
            banished.Clear();
            modes.Clear();
            caughtCount = 0;
        }

        [UnityTest]
        public IEnumerator ReadyPutsEveryGhostHomeAndFrozen()
        {
            gameState.Raise(GameState.Ready);
            yield return new WaitForSeconds(1f);

            foreach (Ghost ghost in AllGhosts())
                Assert.That(ghost.IsFrozen, Is.True, ghost.name);

            Assert.That(blinky.State, Is.EqualTo(GhostState.Scatter), "Blinky starts outside the house");
            Assert.That(pinky.State, Is.EqualTo(GhostState.InHouse));
            Assert.That(inky.State, Is.EqualTo(GhostState.InHouse));
            Assert.That(clyde.State, Is.EqualTo(GhostState.InHouse));
            Assert.That(blinky.DestinationUpdates, Is.EqualTo(0), "frozen ghosts never path");
        }

        [UnityTest]
        public IEnumerator GhostsLeaveTheHouseOnTheirOwnSchedule()
        {
            StartRound();
            yield return new WaitForSeconds(0.5f);
            Assert.That(pinky.State, Is.EqualTo(GhostState.InHouse));

            yield return WaitFor(() => pinky.State != GhostState.InHouse, 2f, "Pinky to leave");
            Assert.That(inky.State, Is.EqualTo(GhostState.InHouse));
            Assert.That(clyde.State, Is.EqualTo(GhostState.InHouse));

            yield return WaitFor(() => pinky.State == GhostState.Scatter, 5f, "Pinky to reach the maze");
            yield return WaitFor(() => inky.State != GhostState.InHouse, 5f, "Inky to leave");
            Assert.That(clyde.State, Is.EqualTo(GhostState.InHouse), "Clyde leaves last");
        }

        [UnityTest]
        public IEnumerator ChasingGhostsHuntDownAStandingPlayer()
        {
            StartRound(Level(ChaseForever));
            yield return WaitFor(() => blinky.HasDestination, 1f, "Blinky's first destination");

            Assert.That(blinky.State, Is.EqualTo(GhostState.Chase));
            Assert.That(GhostTargeting.FlatDistance(blinky.Destination, player.transform.position), Is.LessThan(1f),
                "Blinky aims at the player");

            yield return WaitFor(() => caughtCount > 0, 20f, "a ghost to catch the player");
            Assert.That(caughtCount, Is.EqualTo(1));
            Assert.That(director.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator PinkyAimsAheadOfThePlayer()
        {
            StartRound(Level(ChaseForever));
            yield return WaitFor(() => pinky.State == GhostState.Chase, 5f, "Pinky to start chasing");
            yield return new WaitForSeconds(director.AI.RepathInterval + 0.05f);

            Vector3 playerPosition = player.transform.position;
            Vector3 fourTilesAhead = playerPosition + player.transform.forward * (4 * director.Layout.TileSize);

            Assert.That(GhostTargeting.FlatDistance(pinky.Destination, fourTilesAhead), Is.LessThan(3f));
            Assert.That(GhostTargeting.FlatDistance(pinky.Destination, playerPosition), Is.GreaterThan(4f));
        }

        [UnityTest]
        public IEnumerator FlareFrightensGhostsInTheMazeAndTheyRecover()
        {
            const string json = "{\"scatterChaseSeconds\":[60],\"frightenedSeconds\":1.5," +
                                "\"frightenedWarningSeconds\":0.5,\"frightenedSpeed\":1.7}";
            StartRound(Level(json));
            yield return WaitFor(() => pinky.State == GhostState.Scatter, 5f, "Pinky in the maze");

            flareCollected.Raise();

            Assert.That(modes[^1].Mode, Is.EqualTo(GhostMode.Frightened));
            Assert.That(modes[^1].Seconds, Is.EqualTo(1.5f).Within(1e-3));
            Assert.That(blinky.State, Is.EqualTo(GhostState.Frightened));
            Assert.That(pinky.State, Is.EqualTo(GhostState.Frightened));
            Assert.That(clyde.State, Is.EqualTo(GhostState.InHouse), "ghosts in the house are scared when they come out");

            float expected = blinky.IsLit ? 1.7f * director.AI.LitSpeedMultiplier : 1.7f;
            Assert.That(blinky.GetComponent<NavMeshAgent>().speed, Is.EqualTo(expected).Within(1e-3));

            yield return WaitFor(() => blinky.IsRecovering, 1.5f, "the recovery warning");
            yield return WaitFor(() => blinky.State == GhostState.Scatter, 2f, "Blinky to recover");
            Assert.That(modes[^1].Mode, Is.EqualTo(GhostMode.Scatter));
        }

        [UnityTest]
        public IEnumerator TouchingFrightenedGhostsBanishesThemWithARisingCombo()
        {
            StartRound(Level("{\"scatterChaseSeconds\":[60],\"frightenedSeconds\":30}"));
            yield return WaitFor(() => pinky.State == GhostState.Scatter, 5f, "Pinky in the maze");

            flareCollected.Raise();
            yield return Touch(blinky);
            yield return Touch(pinky);

            Assert.That(caughtCount, Is.EqualTo(0));
            Assert.That(banished.Count, Is.EqualTo(2));
            Assert.That(banished[0].Ghost, Is.SameAs(blinky.Config));
            Assert.That(banished[0].Combo, Is.EqualTo(1));
            Assert.That(banished[0].Points, Is.EqualTo(200));
            Assert.That(banished[1].Combo, Is.EqualTo(2));
            Assert.That(banished[1].Points, Is.EqualTo(400));
            Assert.That(blinky.State, Is.EqualTo(GhostState.Eaten));

            // The eyes go home, revive, and come back out unafraid of the same flare.
            yield return WaitFor(() => blinky.State == GhostState.InHouse, 10f, "Blinky's eyes to reach the house");
            yield return WaitFor(() => blinky.State == GhostState.Scatter, 6f, "Blinky to come back out");
            Assert.That(director.IsFrightened, Is.True, "the flare is still running");
        }

        [UnityTest]
        public IEnumerator TwoHuntingGhostsTouchingAtOnceCatchThePlayerOnce()
        {
            StartRound();
            yield return WaitFor(() => pinky.IsDangerous, 5f, "Pinky to leave the house");

            WarpToPlayer(blinky);
            WarpToPlayer(pinky);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(caughtCount, Is.EqualTo(1));
            Assert.That(banished, Is.Empty);
            Assert.That(director.IsRunning, Is.False);
            foreach (Ghost ghost in AllGhosts())
                Assert.That(ghost.IsFrozen, Is.True, ghost.name);
        }

        [UnityTest]
        public IEnumerator ReadyAfterACatchSendsEveryoneHome()
        {
            gameState.Raise(GameState.Ready);
            Vector3 blinkyHome = blinky.transform.position;
            Vector3 pinkyHome = pinky.transform.position;
            gameState.Raise(GameState.Playing);

            yield return WaitFor(() => pinky.State == GhostState.Scatter, 5f, "Pinky in the maze");
            yield return Touch(blinky);
            Assert.That(caughtCount, Is.EqualTo(1));

            gameState.Raise(GameState.PlayerCaught);
            gameState.Raise(GameState.Ready);

            Assert.That(Vector3.Distance(blinky.transform.position, blinkyHome), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(pinky.transform.position, pinkyHome), Is.LessThan(0.1f));
            Assert.That(pinky.State, Is.EqualTo(GhostState.InHouse));
            Assert.That(blinky.IsFrozen, Is.True);
        }

        [UnityTest]
        public IEnumerator FlashlightSlowsAGhostInItsBeamButNotThroughWalls()
        {
            // Ready places the ghosts and freezes them; the beam check runs regardless.
            gameState.Raise(GameState.Ready);
            NavMeshAgent agent = blinky.GetComponent<NavMeshAgent>();
            float normal = director.Level.GhostSpeed;

            // Blinky starts straight ahead of the player, but behind the ghost house wall.
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            yield return new WaitForSeconds(0.3f);
            Assert.That(blinky.IsLit, Is.False, "the wall blocks the beam");

            // Three tiles down the player's own corridor, in the open.
            agent.Warp(player.transform.position + Vector3.right * 6f);
            player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            yield return new WaitForSeconds(0.3f);
            Assert.That(blinky.IsLit, Is.True);
            Assert.That(agent.speed, Is.EqualTo(normal * director.AI.LitSpeedMultiplier).Within(1e-3));

            player.transform.rotation = Quaternion.LookRotation(Vector3.left);
            yield return new WaitForSeconds(0.3f);
            Assert.That(blinky.IsLit, Is.False, "outside the cone");
            Assert.That(agent.speed, Is.EqualTo(normal).Within(1e-3));

            player.transform.rotation = Quaternion.LookRotation(Vector3.right);
            flashlight.enabled = false;
            yield return new WaitForSeconds(0.3f);
            Assert.That(blinky.IsLit, Is.False, "the flashlight is off");
        }

        [UnityTest]
        public IEnumerator GhostsChooseANewDestinationOnATimerNotEveryFrame()
        {
            StartRound(Level(ChaseForever));
            int startFrame = Time.frameCount;
            yield return new WaitForSeconds(3f);

            int frames = Time.frameCount - startFrame;
            int mostAllowed = Mathf.CeilToInt(3f / director.AI.RepathInterval) + 3;
            foreach (Ghost ghost in AllGhosts())
                Assert.That(ghost.DestinationUpdates, Is.LessThanOrEqualTo(mostAllowed), ghost.name);

            Assert.That(blinky.DestinationUpdates, Is.GreaterThan(0));
            Assert.That(frames, Is.GreaterThan(mostAllowed), "more frames ran than paths were requested");
        }

        [UnityTest]
        public IEnumerator LevelStartedAppliesTheNewLevelsSpeeds()
        {
            StartRound();
            yield return null;

            levelStarted.Raise(Level("{\"ghostSpeed\":5.5}"));

            float expected = blinky.IsLit ? 5.5f * director.AI.LitSpeedMultiplier : 5.5f;
            Assert.That(blinky.GetComponent<NavMeshAgent>().speed, Is.EqualTo(expected).Within(1e-3));
        }

        // Not part of the suite: renders a few views to GHOST_CAPTURE_DIR for a person to look at.
        [UnityTest, Explicit("Writes screenshots for a person to look at")]
        public IEnumerator CaptureViews()
        {
            string folder = Environment.GetEnvironmentVariable("GHOST_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set GHOST_CAPTURE_DIR to capture.");
            Directory.CreateDirectory(folder);

            StartRound();
            yield return new WaitForSeconds(6f);
            Capture(player.GetComponentInChildren<Camera>(), Path.Combine(folder, "player_view.png"), 1280, 720);

            Color dark = RenderSettings.ambientLight;
            RenderSettings.ambientLight = Color.white * 0.35f;
            Camera overview = OverviewCamera();
            Capture(overview, Path.Combine(folder, "overview_scatter.png"), 900, 900);

            var angled = new GameObject("Angled capture").AddComponent<Camera>();
            created.Add(angled.gameObject);
            angled.enabled = false;
            angled.fieldOfView = 55f;
            angled.transform.SetPositionAndRotation(new Vector3(20f, 22f, -6f), Quaternion.Euler(52f, 0f, 0f));
            Capture(angled, Path.Combine(folder, "angled_scatter.png"), 1280, 720);

            flareCollected.Raise();
            yield return new WaitForSeconds(1.5f);
            Capture(overview, Path.Combine(folder, "overview_frightened.png"), 900, 900);
            Capture(angled, Path.Combine(folder, "angled_frightened.png"), 1280, 720);
            RenderSettings.ambientLight = dark;
        }

        void StartRound(LevelConfig level = null)
        {
            if (level != null)
                levelStarted.Raise(level);

            gameState.Raise(GameState.Ready);
            gameState.Raise(GameState.Playing);
        }

        // A LevelConfig with the given fields changed, for example {"frightenedSeconds":1.5}.
        LevelConfig Level(string json)
        {
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            JsonUtility.FromJsonOverwrite(json, level);
            created.Add(level);
            return level;
        }

        IEnumerator Touch(Ghost ghost)
        {
            WarpToPlayer(ghost);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        void WarpToPlayer(Ghost ghost) => ghost.GetComponent<NavMeshAgent>().Warp(player.transform.position);

        static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            float end = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > end)
                    Assert.Fail($"Timed out after {timeout} s waiting for {what}.");
                yield return null;
            }
        }

        IEnumerable<Ghost> AllGhosts()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Ghost ghost in root.GetComponentsInChildren<Ghost>(true))
                    yield return ghost;
            }
        }

        Camera OverviewCamera()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.StartsWith("Overview Camera"))
                    return root.GetComponent<Camera>();
            }

            return null;
        }

        static T InScene<T>() where T : Component
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        static T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(EventFolder + name + ".asset");

        static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var request = new RenderPipeline.StandardRequest { destination = target };

            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }

        void OnCaught() => caughtCount++;
        void OnBanished(GhostBanishedInfo info) => banished.Add(info);
        void OnModeChanged(GhostModeChange change) => modes.Add(change);
    }
}
#endif
