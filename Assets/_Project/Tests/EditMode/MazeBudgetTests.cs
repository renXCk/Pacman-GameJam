using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Task A8: the object budget for Level 1, worked out from the map alone. The maze merges each
    // row of wall tiles into one cube, and every pickup is one trigger plus one small mesh, so the
    // scene stays small enough for 60 FPS. These numbers are limits to keep an eye on, not targets.
    public sealed class MazeBudgetTests
    {
        const string Level1Path = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        // Generous: Level 1 uses far fewer. A map that goes past these needs a second look.
        public const int MaxWallCubes = 150;
        public const int MaxPickups = 300;

        static MazeGrid LoadLevel1()
        {
            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(map, Is.Not.Null, "Missing " + Level1Path);
            return MazeGrid.Parse(map.text);
        }

        [Test]
        public void Level1_MergesWallTilesIntoFewCubes()
        {
            MazeGrid grid = LoadLevel1();

            int wallTiles = grid.Count(MazeTile.Wall);
            int cubes = MazeWallRuns.Compute(grid).Count;

            Assert.That(cubes, Is.LessThan(wallTiles), "merging wall runs saves objects");
            Assert.That(cubes, Is.LessThanOrEqualTo(MaxWallCubes), $"{cubes} wall cubes for {wallTiles} wall tiles");
        }

        [Test]
        public void Level1_HasAReasonableNumberOfPickups()
        {
            MazeGrid grid = LoadLevel1();

            int pickups = grid.Count(MazeTile.Cell) + grid.Count(MazeTile.Flare) + grid.Count(MazeTile.Battery);

            Assert.That(pickups, Is.LessThanOrEqualTo(MaxPickups), $"{pickups} pickups");
            Assert.That(pickups, Is.GreaterThanOrEqualTo(grid.CompletionTotal), "the pickups include every cell and flare");
        }
    }
}
