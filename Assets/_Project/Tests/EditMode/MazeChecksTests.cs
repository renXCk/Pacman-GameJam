using System.Linq;
using Game.Maze;
using NUnit.Framework;

namespace Game.Tests
{
    // Task A8: the design rules for a maze map.
    public sealed class MazeChecksTests
    {
        // Nothing to complain about: 17 cells and flares, a battery under the house, no dead ends.
        const string Clean =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P..o#\n" +
            "#########\n";

        static MazeReport Check(string map) => MazeChecks.Check(MazeGrid.Parse(map));

        static int Count(System.Collections.Generic.List<string> messages, string part) =>
            messages.Count(message => message.Contains(part));

        [Test]
        public void ACleanMap_HasNoErrorsAndNoWarnings()
        {
            MazeReport report = Check(Clean);

            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(report.IsClean, Is.True);
        }

        [Test]
        public void ANullMap_IsAnError()
        {
            MazeReport report = MazeChecks.Check(null);
            Assert.That(report.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void ABatteryAboveTheGhostHouse_IsAnError()
        {
            MazeReport report = Check(
                "#########\n" +
                "#.b.G...#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "#.o.P..o#\n" +
                "#########\n");

            Assert.That(report.Errors.Count, Is.EqualTo(1));
            Assert.That(Count(report.Errors, "not under the ghost house"), Is.EqualTo(1));
        }

        [Test]
        public void NoBatterySpot_IsAWarning()
        {
            MazeReport report = Check(
                "#########\n" +
                "#...G...#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "#...P..o#\n" +
                "#########\n");

            Assert.That(report.Errors, Is.Empty);
            Assert.That(Count(report.Warnings, "no battery spot"), Is.EqualTo(1));
        }

        [Test]
        public void NoFlares_IsAWarning()
        {
            MazeReport report = Check(
                "#########\n" +
                "#...G...#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "#.b.P...#\n" +
                "#########\n");

            Assert.That(report.Errors, Is.Empty);
            Assert.That(Count(report.Warnings, "no flare"), Is.EqualTo(1));
        }

        [Test]
        public void AStartRightNextToTheGhostExit_IsAWarning()
        {
            MazeReport report = Check(
                "#########\n" +
                "#...GP..#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "#.b....o#\n" +
                "#########\n");

            Assert.That(report.Errors, Is.Empty);
            Assert.That(Count(report.Warnings, "on top of the player"), Is.EqualTo(1));
        }

        [Test]
        public void ADeadEnd_IsAWarning_ForEachTile()
        {
            MazeReport report = Check(
                "#########\n" +
                "#...G...#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "##b.P..o#\n" +
                "#########\n");

            Assert.That(report.Errors, Is.Empty);
            Assert.That(Count(report.Warnings, "Dead end at"), Is.EqualTo(2));
        }

        [Test]
        public void WarpDoorsWithoutAPartner_AreWarnings()
        {
            MazeReport report = Check(
                "##W######\n" +
                "#...G...#\n" +
                "#.##-##.#\n" +
                "#.#HHH#.#\n" +
                "#.#####.#\n" +
                "W.b.P..oW\n" +
                "######W##\n");

            Assert.That(report.Errors, Is.Empty);
            Assert.That(Count(report.Warnings, "no partner"), Is.EqualTo(2));
        }
    }
}
