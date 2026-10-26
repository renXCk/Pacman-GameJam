using Game.Core;
using Game.Maze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Task A5: the pickup assets the maze uses, and the Level 1 map's pickup spots.
    public sealed class PickupConfigTests
    {
        const string ConfigFolder = "Assets/_Project/ScriptableObjects/Configs/";
        const string MapPath = "Assets/_Project/Data/Mazes/Maze_Level1.txt";

        static PickupConfig Load(string name)
        {
            var config = AssetDatabase.LoadAssetAtPath<PickupConfig>(ConfigFolder + name + ".asset");
            Assert.That(config, Is.Not.Null, "Missing " + ConfigFolder + name + ".asset");
            return config;
        }

        [Test]
        public void TheCellConfigCountsTowardTheLevel()
        {
            PickupConfig config = Load("PickupConfig_Cell");
            Assert.That(config.Kind, Is.EqualTo(PickupKind.Cell));
            Assert.That(config.CountsTowardLevel, Is.True);
        }

        [Test]
        public void TheFlareConfigCountsTowardTheLevel()
        {
            PickupConfig config = Load("PickupConfig_Flare");
            Assert.That(config.Kind, Is.EqualTo(PickupKind.Flare));
            Assert.That(config.CountsTowardLevel, Is.True);
        }

        [Test]
        public void TheBatteryConfigIsABonusThatNeverCounts()
        {
            PickupConfig config = Load("PickupConfig_Battery");
            Assert.That(config.Kind, Is.EqualTo(PickupKind.Battery));
            Assert.That(config.CountsTowardLevel, Is.False);
        }

        [Test]
        public void Level1_HasCellsFlaresAndBatterySpotsUnderTheGhostHouse()
        {
            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath);
            Assert.That(map, Is.Not.Null, "Missing " + MapPath);

            MazeGrid grid = MazeGrid.Parse(map.text);
            Assert.That(grid.Count(MazeTile.Cell), Is.GreaterThan(0));
            Assert.That(grid.Count(MazeTile.Flare), Is.GreaterThan(0));
            Assert.That(grid.Count(MazeTile.Battery), Is.GreaterThan(0));
            Assert.That(grid.CompletionTotal, Is.EqualTo(grid.Count(MazeTile.Cell) + grid.Count(MazeTile.Flare)),
                "batteries are not part of the level total");

            // Row 0 is the top of the map, so "under the house" means a larger row than the house door.
            int doorRow = grid.HouseDoor.Row;
            foreach (TileCoord spot in grid.Find(MazeTile.Battery))
                Assert.That(spot.Row, Is.GreaterThan(doorRow), $"battery spot {spot.Row},{spot.Col} is under the ghost house");
        }
    }
}
