using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Task A1: the maze text map, its parser and every way a map can be wrong.
    public sealed class MazeGridTests
    {
        const string Level1Path = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        // A small valid maze. The error tests change one row of it. The ghost house is in the
        // middle with its exit 'G' above the door '-', and the player starts in the bottom corridor.
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

        static string Join(IEnumerable<string> rows) => string.Join("\n", rows);

        static string[] WithRow(int index, string text)
        {
            var copy = (string[])Valid.Clone();
            copy[index] = text;
            return copy;
        }

        static MazeGrid LoadLevel1()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(asset, Is.Not.Null, $"Missing {Level1Path}");
            return MazeGrid.Parse(asset.text);
        }

        static void AssertRejected(string text, string expectedFragment)
        {
            bool ok = MazeGrid.TryParse(text, out MazeGrid grid, out List<string> errors);

            Assert.That(ok, Is.False, "The map should have been rejected.");
            Assert.That(grid, Is.Null);
            Assert.That(errors.Any(e => e.Contains(expectedFragment)), Is.True,
                $"Expected an error containing \"{expectedFragment}\" but got:\n- " + string.Join("\n- ", errors));
        }

        // ---- The Level 1 map file ----

        [Test]
        public void Level1_LoadsFromTheMapFile()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.Rows, Is.EqualTo(21));
            Assert.That(grid.Cols, Is.EqualTo(21));
        }

        [Test]
        public void Level1_HasTheExpectedTiles()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.Count(MazeTile.Cell), Is.EqualTo(183));
            Assert.That(grid.Count(MazeTile.Flare), Is.EqualTo(4));
            Assert.That(grid.Count(MazeTile.Battery), Is.EqualTo(2));
            Assert.That(grid.Count(MazeTile.GhostHouse), Is.EqualTo(3));
            Assert.That(grid.Count(MazeTile.WarpDoor), Is.EqualTo(0));
            Assert.That(grid.PlayerStart, Is.EqualTo(new TileCoord(11, 10)));
            Assert.That(grid.GhostExit, Is.EqualTo(new TileCoord(7, 10)));
            Assert.That(grid.HouseDoor, Is.EqualTo(new TileCoord(8, 10)));
        }

        [Test]
        public void Level1_CompletionTotalCountsCellsAndFlaresButNotBatteries()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.CompletionTotal, Is.EqualTo(183 + 4));
        }

        [Test]
        public void Level1_BatteriesSitInTheCorridorUnderTheGhostHouse()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.Find(MazeTile.Battery), Is.EqualTo(new[] { new TileCoord(11, 8), new TileCoord(11, 12) }));
        }

        [Test]
        public void Level1_ScatterCornersAreTheCorridorTilesNearestEachCorner()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.CornerTile(MazeCorner.TopLeft), Is.EqualTo(new TileCoord(1, 1)));
            Assert.That(grid.CornerTile(MazeCorner.TopRight), Is.EqualTo(new TileCoord(1, 19)));
            Assert.That(grid.CornerTile(MazeCorner.BottomLeft), Is.EqualTo(new TileCoord(19, 1)));
            Assert.That(grid.CornerTile(MazeCorner.BottomRight), Is.EqualTo(new TileCoord(19, 19)));
        }

        // ---- Small maps ----

        [Test]
        public void SmallMap_ParsesAndCountsItsTiles()
        {
            MazeGrid grid = MazeGrid.Parse(Join(Valid));

            Assert.That(grid.Rows, Is.EqualTo(7));
            Assert.That(grid.Cols, Is.EqualTo(9));
            Assert.That(grid.PlayerStart, Is.EqualTo(new TileCoord(5, 4)));
            Assert.That(grid.GhostExit, Is.EqualTo(new TileCoord(1, 4)));
            Assert.That(grid.HouseDoor, Is.EqualTo(new TileCoord(2, 4)));
            Assert.That(grid.Count(MazeTile.GhostHouse), Is.EqualTo(3));
            Assert.That(grid.CompletionTotal, Is.EqualTo(18));
        }

        [Test]
        public void Parse_IgnoresCommentsBlankLinesWindowsLineEndingsAndTheByteOrderMark()
        {
            string text = "\uFEFF; a comment\r\n\r\n" + string.Join("\r\n", Valid) + "\r\n\r\n; trailing comment\r\n";

            MazeGrid grid = MazeGrid.Parse(text);

            Assert.That(grid.Rows, Is.EqualTo(7));
            Assert.That(grid.Cols, Is.EqualTo(9));
        }

        [Test]
        public void TileAt_OutsideTheMapIsWallAndNotWalkable()
        {
            MazeGrid grid = MazeGrid.Parse(Join(Valid));

            Assert.That(grid.TileAt(-1, 0), Is.EqualTo(MazeTile.Wall));
            Assert.That(grid.TileAt(0, 99), Is.EqualTo(MazeTile.Wall));
            Assert.That(grid.IsWalkable(-1, -1), Is.False);
            Assert.That(grid.Contains(6, 8), Is.True);
            Assert.That(grid.Contains(7, 0), Is.False);
            Assert.That(grid.IsWalkable(5, 4), Is.True);
            Assert.That(grid.IsWalkable(0, 0), Is.False);
        }

        // ---- World positions ----

        [Test]
        public void TileToWorld_PutsRowZeroAtTheTopAndColumnZeroAtTheLeft()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.TileToWorld(0, 0, 2f), Is.EqualTo(new Vector3(0f, 0f, 40f)));
            Assert.That(grid.TileToWorld(20, 20, 2f), Is.EqualTo(new Vector3(40f, 0f, 0f)));
            Assert.That(grid.TileToWorld(grid.PlayerStart, 2f), Is.EqualTo(new Vector3(20f, 0f, 18f)));
        }

        [Test]
        public void WorldToTile_RoundsToTheNearestTile()
        {
            MazeGrid grid = LoadLevel1();

            Assert.That(grid.WorldToTile(new Vector3(20.4f, 0f, 17.6f), 2f), Is.EqualTo(new TileCoord(11, 10)));
        }

        [Test]
        public void EveryTileSurvivesAWorldRoundTrip()
        {
            MazeGrid grid = LoadLevel1();

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    Vector3 world = grid.TileToWorld(row, col, 3f);
                    Assert.That(grid.WorldToTile(world, 3f), Is.EqualTo(new TileCoord(row, col)));
                }
            }
        }

        // ---- Maps that must be rejected ----

        [Test]
        public void Rejects_AnEmptyMap()
        {
            AssertRejected("; only a comment\n\n", "empty");
            AssertRejected(null, "empty");
        }

        [Test]
        public void Rejects_RowsOfDifferentLengths()
        {
            AssertRejected(Join(WithRow(2, "#.##-##.#.")), "has 10 tiles");
        }

        [Test]
        public void Rejects_AnUnknownGlyph()
        {
            AssertRejected(Join(WithRow(5, "#...P?.o#")), "unknown glyph '?'");
        }

        [Test]
        public void Rejects_AMapWithNoPlayerStart()
        {
            AssertRejected(Join(WithRow(5, "#......o#")), "'P'");
        }

        [Test]
        public void Rejects_TwoPlayerStarts()
        {
            AssertRejected(Join(WithRow(1, "#...G..P#")), "found 2");
        }

        [Test]
        public void Rejects_AMapWithNoGhostExit()
        {
            AssertRejected(Join(WithRow(1, "#.......#")), "'G'");
        }

        [Test]
        public void Rejects_AMapWithNoGhostHouse()
        {
            AssertRejected(Join(WithRow(3, "#.#...#.#")), "'H'");
        }

        [Test]
        public void Rejects_AMapWithNoHouseDoor()
        {
            AssertRejected(Join(WithRow(2, "#.##.##.#")), "'-'");
        }

        [Test]
        public void Rejects_ADoorThatDoesNotTouchTheGhostExit()
        {
            AssertRejected(Join(WithRow(1, "#.G.....#")), "must touch");
        }

        [Test]
        public void Rejects_AnOpenTileOnTheEdgeOfTheMap()
        {
            AssertRejected(Join(WithRow(1, "....G...#")), "edge of the map");
        }

        [Test]
        public void Rejects_AMapWithNothingToCollect()
        {
            string text = Join(Valid).Replace('.', '_').Replace('o', '_');

            AssertRejected(text, "at least one power cell");
        }

        [Test]
        public void Rejects_ASingleWarpDoor()
        {
            AssertRejected(Join(WithRow(3, "W.#HHH#.#")), "pairs");
        }

        [Test]
        public void Rejects_AWalkableAreaThePlayerCannotReach()
        {
            string[] rows =
            {
                "#########",
                "#...G...#",
                "#.##-##.#",
                "#.#HHH#.#",
                "#.#####.#",
                "#...P..o#",
                "#.#####.#",
                "#.#...#.#", // the three tiles in the middle are sealed off by walls
                "#########"
            };

            AssertRejected(Join(rows), "cannot be reached");
        }

        [Test]
        public void ParseThrowsOneExceptionListingEveryProblem()
        {
            // No ghost exit and no player start.
            string[] rows = (string[])Valid.Clone();
            rows[1] = "#.......#";
            rows[5] = "#......o#";
            string text = Join(rows);

            var exception = Assert.Throws<MazeParseException>(() => MazeGrid.Parse(text));

            Assert.That(exception.Errors.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(exception.Message, Does.Contain("'P'"));
            Assert.That(exception.Message, Does.Contain("'G'"));
        }

        // ---- Warp doors ----

        static readonly string[] SplitByAWall =
        {
            "#########",
            "#...G#..#",
            "#.##-##.#",
            "W.#HHH#.W", // a warp door on each side of the wall
            "#.#####.#",
            "#...P#.o#",
            "#########"
        };

        [Test]
        public void WarpDoorsJoinTwoPartsOfTheMaze()
        {
            MazeGrid grid = MazeGrid.Parse(Join(SplitByAWall));

            Assert.That(grid.Count(MazeTile.WarpDoor), Is.EqualTo(2));
        }

        [Test]
        public void WithoutTheWarpDoorsTheSameMapIsSplitInTwo()
        {
            string text = Join(SplitByAWall).Replace('W', '#');

            AssertRejected(text, "cannot be reached");
        }
    }
}
