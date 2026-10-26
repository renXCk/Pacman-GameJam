using System.Collections.Generic;
using Game.Maze;
using NUnit.Framework;

namespace Game.Tests
{
    // Task A7 (optional): which warp doors belong together.
    public sealed class WarpPairsTests
    {
        // A map with no warp doors, like Level 1.
        const string NoWarps =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P..o#\n" +
            "#########\n";

        // One pair, left and right edge of the same row.
        const string OnePair =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "W.b.P..oW\n" +
            "#########\n";

        // A left/right pair and a top/bottom pair.
        const string TwoPairs =
            "####W####\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "W.b.P..oW\n" +
            "####W####\n";

        // Four doors where two have no partner in their row or column.
        const string Strays =
            "##W######\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "W.b.P..oW\n" +
            "######W##\n";

        [Test]
        public void AMapWithoutWarpDoors_HasNoPairs()
        {
            var unpaired = new List<TileCoord>();
            List<WarpPair> pairs = WarpPairs.Compute(MazeGrid.Parse(NoWarps), unpaired);

            Assert.That(pairs.Count, Is.EqualTo(0));
            Assert.That(unpaired.Count, Is.EqualTo(0));
        }

        [Test]
        public void TwoDoorsInTheSameRow_AreAPair()
        {
            List<WarpPair> pairs = WarpPairs.Compute(MazeGrid.Parse(OnePair));

            Assert.That(pairs.Count, Is.EqualTo(1));
            Assert.That(pairs[0].A, Is.EqualTo(new TileCoord(5, 0)));
            Assert.That(pairs[0].B, Is.EqualTo(new TileCoord(5, 8)));
        }

        [Test]
        public void TopAndBottomDoorsPairByColumn_WhenTheyShareNoRow()
        {
            List<WarpPair> pairs = WarpPairs.Compute(MazeGrid.Parse(TwoPairs));

            Assert.That(pairs.Count, Is.EqualTo(2));
            Assert.That(pairs[0].A, Is.EqualTo(new TileCoord(0, 4)));
            Assert.That(pairs[0].B, Is.EqualTo(new TileCoord(6, 4)));
            Assert.That(pairs[1].A, Is.EqualTo(new TileCoord(5, 0)));
            Assert.That(pairs[1].B, Is.EqualTo(new TileCoord(5, 8)));
        }

        [Test]
        public void DoorsWithoutAPartner_AreReportedAndLeftOut()
        {
            var unpaired = new List<TileCoord>();
            List<WarpPair> pairs = WarpPairs.Compute(MazeGrid.Parse(Strays), unpaired);

            Assert.That(pairs.Count, Is.EqualTo(1));
            Assert.That(pairs[0].A, Is.EqualTo(new TileCoord(5, 0)));
            Assert.That(unpaired.Count, Is.EqualTo(2));
            Assert.That(unpaired, Does.Contain(new TileCoord(0, 2)));
            Assert.That(unpaired, Does.Contain(new TileCoord(6, 6)));
        }
    }
}
