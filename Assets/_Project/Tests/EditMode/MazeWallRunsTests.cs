using System.Collections.Generic;
using System.Linq;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Task A2: the wall merging that decides how many cubes the builder makes.
    public sealed class MazeWallRunsTests
    {
        const string Level1Path = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        // The same small maze as MazeGridTests.
        static readonly string[] Valid =
        {
            "#########",
            "#...G...#",
            "#.##-##.#",
            "#.#HHH#.#",
            "#.#####.#",
            "#...P..o#",
            "#########"
        };

        static MazeGrid Small() => MazeGrid.Parse(string.Join("\n", Valid));

        static MazeGrid Level1()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(asset, Is.Not.Null, $"Missing {Level1Path}");
            return MazeGrid.Parse(asset.text);
        }

        static int WallTiles(MazeGrid grid) => grid.Count(MazeTile.Wall);

        [Test]
        public void SmallMaze_HasTheExpectedRuns()
        {
            List<WallRun> runs = MazeWallRuns.Compute(Small());

            Assert.That(runs.Count, Is.EqualTo(17));
            Assert.That(runs.Sum(r => r.Length), Is.EqualTo(39));

            // The full top and bottom rows are one run each.
            Assert.That(runs[0].Row, Is.EqualTo(0));
            Assert.That(runs[0].StartCol, Is.EqualTo(0));
            Assert.That(runs[0].Length, Is.EqualTo(9));
            Assert.That(runs[runs.Count - 1].Row, Is.EqualTo(6));
            Assert.That(runs[runs.Count - 1].Length, Is.EqualTo(9));
        }

        [Test]
        public void SmallMaze_ASplitRowGivesTwoRuns()
        {
            // Row 2 is "#.##-##.#": wall, gap, wall pair, door, wall pair, gap, wall.
            List<WallRun> row2 = MazeWallRuns.Compute(Small()).Where(r => r.Row == 2).ToList();

            Assert.That(row2.Count, Is.EqualTo(4));
            Assert.That(row2[1].StartCol, Is.EqualTo(2));
            Assert.That(row2[1].Length, Is.EqualTo(2));
            Assert.That(row2[2].StartCol, Is.EqualTo(5));
            Assert.That(row2[2].Length, Is.EqualTo(2));
        }

        [Test]
        public void SmallMaze_ARunNeverIncludesAWalkableTile()
        {
            MazeGrid grid = Small();

            foreach (WallRun run in MazeWallRuns.Compute(grid))
            {
                for (int col = run.StartCol; col <= run.EndCol; col++)
                    Assert.That(grid.TileAt(run.Row, col), Is.EqualTo(MazeTile.Wall), $"run {run} covers a non-wall tile at col {col}");
            }
        }

        [Test]
        public void SmallMaze_EveryWallTileIsCoveredExactlyOnce()
        {
            MazeGrid grid = Small();
            var covered = new HashSet<TileCoord>();

            foreach (WallRun run in MazeWallRuns.Compute(grid))
            {
                for (int col = run.StartCol; col <= run.EndCol; col++)
                    Assert.That(covered.Add(new TileCoord(run.Row, col)), Is.True, $"tile in run {run} is covered twice");
            }

            Assert.That(covered.Count, Is.EqualTo(WallTiles(grid)));
        }

        [Test]
        public void SmallMaze_RunsComeInReadingOrder()
        {
            List<WallRun> runs = MazeWallRuns.Compute(Small());

            for (int i = 1; i < runs.Count; i++)
            {
                bool later = runs[i].Row > runs[i - 1].Row ||
                             (runs[i].Row == runs[i - 1].Row && runs[i].StartCol > runs[i - 1].EndCol);
                Assert.That(later, Is.True, $"{runs[i - 1]} then {runs[i]}");
            }
        }

        [Test]
        public void Level1_HasTheExpectedRunCount()
        {
            MazeGrid grid = Level1();
            List<WallRun> runs = MazeWallRuns.Compute(grid);

            Assert.That(runs.Count, Is.EqualTo(92));
            Assert.That(runs.Sum(r => r.Length), Is.EqualTo(246));
            Assert.That(runs.Sum(r => r.Length), Is.EqualTo(WallTiles(grid)));
        }
    }
}
