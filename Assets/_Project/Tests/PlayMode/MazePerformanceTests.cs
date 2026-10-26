#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Maze;
using Game.Pickups;
using Game.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A8: the performance side of Dev A's work, checked in the real Game scene. Frame times in
    // the Editor are not reliable enough to fail a test on, so the frame time is only logged; what is
    // asserted is the structure that keeps a frame cheap: few objects, shared materials, no lights
    // and no shadows on pickups.
    public sealed class MazePerformanceTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        // The same limits as MazeBudgetTests (EditMode).
        const int MaxWallCubes = 150;
        const int MaxPickups = 300;

        [UnityTest]
        public IEnumerator GameScene_MazeAndPickupsStayWithinTheObjectBudget()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var manager = Object.FindAnyObjectByType<GameManager>();
            var maze = Object.FindAnyObjectByType<MazeBuilder>();
            var pickups = Object.FindAnyObjectByType<MazePickups>();

            Assert.That(maze, Is.Not.Null, "MazeBuilder: run Game > Maze > Add Maze To Game Scene");
            Assert.That(pickups, Is.Not.Null, "MazePickups: run Game > Pickups > Add Pickups To Game Scene");

            MazeGrid grid = maze.Grid;

            // One cube per run of wall tiles, plus the floor and the ghost house door.
            int wallRuns = MazeWallRuns.Compute(grid).Count;
            Assert.That(maze.Walls.childCount, Is.EqualTo(wallRuns), "one cube per wall run");
            Assert.That(wallRuns, Is.LessThanOrEqualTo(MaxWallCubes));

            // Pickups: every one is a trigger sphere with one mesh, no lights, no shadows.
            Pickup[] all = pickups.Container.GetComponentsInChildren<Pickup>(true);
            Assert.That(all.Length, Is.EqualTo(grid.Count(MazeTile.Cell) + grid.Count(MazeTile.Flare) + grid.Count(MazeTile.Battery)));
            Assert.That(all.Length, Is.LessThanOrEqualTo(MaxPickups));

            Assert.That(pickups.Container.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(0), "no lights on pickups (Dev D's light budget)");

            var materials = new HashSet<Material>();
            Renderer[] renderers = pickups.Container.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(all.Length), "one mesh per pickup");
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), renderer.name + " casts no shadow");
                materials.Add(renderer.sharedMaterial);
            }

            Assert.That(materials.Count, Is.LessThanOrEqualTo(3), "pickups share one material per kind, so they batch");

            foreach (Collider collider in pickups.Container.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.isTrigger, Is.True, "pickup colliders are triggers: no physics cost against the player");

            // Log how a few seconds of play feel. Not asserted: Editor timings jump around.
            float giveUp = Time.realtimeSinceStartup + 8f;
            while (manager.State != GameState.Playing && Time.realtimeSinceStartup < giveUp)
                yield return null;

            int frames = 0;
            float slowest = 0f;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 2f)
            {
                slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
                frames++;
                yield return null;
            }

            float seconds = Time.realtimeSinceStartup - start;
            Debug.Log($"[Performance] Level 1 in the Editor: {wallRuns} wall cubes, {all.Length} pickups, " +
                      $"{frames / seconds:0} fps average over {seconds:0.0}s, slowest frame {slowest * 1000f:0} ms. " +
                      "Measure the build for the real 60 FPS number.");
        }
    }
}
#endif
