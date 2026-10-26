using System.Collections.Generic;
using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Task A8: the validator that the Game > Maze > Validate Maze Setup menu runs.
    public sealed class MazeValidatorTests
    {
        const string Level1Path = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        [Test]
        public void ALegalMap_HasNoProblems()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            MazeValidator.CheckMapText("Test", "#########\n#...G...#\n#.##-##.#\n#.#HHH#.#\n#.#####.#\n#.b.P..o#\n#########\n", errors, warnings);

            Assert.That(errors, Is.Empty);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void AMapThatDoesNotParse_ReportsEveryProblemWithItsName()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // No player start, and the right edge of the first row is open.
            MazeValidator.CheckMapText("Broken", "#########\n#...G....\n#.##-##.#\n#.#HHH#.#\n#.#####.#\n#.b....o#\n#########\n", errors, warnings);

            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(2));
            foreach (string error in errors)
                Assert.That(error, Does.StartWith("Broken: "));
        }

        [Test]
        public void AMapBreakingADesignRule_IsReportedWithItsName()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // A battery above the house.
            MazeValidator.CheckMapText("Odd", "#########\n#.b.G...#\n#.##-##.#\n#.#HHH#.#\n#.#####.#\n#.o.P..o#\n#########\n", errors, warnings);

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.StartWith("Odd: "));
        }

        [Test]
        public void Level1_PassesEveryMapRule()
        {
            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(Level1Path);
            Assert.That(map, Is.Not.Null, "Missing " + Level1Path);

            var errors = new List<string>();
            var warnings = new List<string>();
            MazeValidator.CheckMapText(map.name, map.text, errors, warnings);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(warnings, Is.Empty, string.Join("\n", warnings));
        }

        // Needs every setup menu run: Maze, Player, Flashlight, Pickups, Battery Rules And Warps.
        [Test]
        public void TheRepository_HasNoMazePickupOrPlayerErrors()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            int count = MazeValidator.Run(errors, warnings);

            Assert.That(count, Is.EqualTo(0), "Run Game > Maze > Validate Maze Setup for details:\n" + string.Join("\n", errors));
            Assert.That(errors, Is.Empty);
        }
    }
}
