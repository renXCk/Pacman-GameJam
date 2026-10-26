using Game.Maze;
using NUnit.Framework;

namespace Game.Tests
{
    // Task A4: which way the player faces when a round starts.
    public sealed class MazePlayerSpawnTests
    {
        // 9 columns by 7 rows. The player start is changed from test to test.
        static MazeGrid Map(string row3, string row5)
        {
            return MazeGrid.Parse(string.Join("\n", new[]
            {
                "#########",
                "#...G...#",
                "#.##-##.#",
                row3,
                "#.#####.#",
                row5,
                "#########"
            }));
        }

        [Test]
        public void FacesTheLongestOpenStretch_East()
        {
            // P at row 5, col 2: five open tiles to the east, one to the west.
            MazeGrid grid = Map("#.#HHH#.#", "#.P....o#");

            Assert.That(MazePlayerSpawn.FacingYaw(grid, grid.PlayerStart), Is.EqualTo(90f));
        }

        [Test]
        public void FacesTheLongestOpenStretch_West()
        {
            // P at row 5, col 6: one open tile to the east, five to the west.
            MazeGrid grid = Map("#.#HHH#.#", "#o....P.#");

            Assert.That(MazePlayerSpawn.FacingYaw(grid, grid.PlayerStart), Is.EqualTo(270f));
        }

        [Test]
        public void ATieGoesToNorthFirst_ThenEastSouthWest()
        {
            // P at row 3, col 1 in the left corridor: two open tiles north, two south, walls east and west.
            MazeGrid north = Map("#P#HHH#.#", "#......o#");
            Assert.That(MazePlayerSpawn.FacingYaw(north, north.PlayerStart), Is.EqualTo(0f));

            // The original small maze: P at row 5, col 4 has three open tiles east and three west.
            MazeGrid eastWest = Map("#.#HHH#.#", "#...P..o#");
            Assert.That(MazePlayerSpawn.FacingYaw(eastWest, eastWest.PlayerStart), Is.EqualTo(90f));
        }

        [Test]
        public void YawMatchesTheWorldDirections()
        {
            // Z grows toward the top of the map (see MazeGrid.TileToWorld), so north is yaw 0 (+z)
            // and east is yaw 90 (+x).
            MazeGrid grid = Map("#.#HHH#.#", "#.P....o#");
            UnityEngine.Vector3 start = grid.TileToWorld(grid.PlayerStart, 3f);
            UnityEngine.Vector3 east = grid.TileToWorld(new TileCoord(grid.PlayerStart.Row, grid.PlayerStart.Col + 1), 3f);
            UnityEngine.Vector3 facing = UnityEngine.Quaternion.Euler(0f, MazePlayerSpawn.FacingYaw(grid, grid.PlayerStart), 0f) * UnityEngine.Vector3.forward;

            Assert.That(facing.x, Is.EqualTo((east - start).normalized.x).Within(0.001f));
            Assert.That(facing.z, Is.EqualTo((east - start).normalized.z).Within(0.001f));
        }
    }
}
