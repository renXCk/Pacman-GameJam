using System.Collections.Generic;

namespace Game.Maze
{
    // What MazeChecks found. Errors break the game; warnings are worth a look but do not.
    public sealed class MazeReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public bool IsClean => Errors.Count == 0 && Warnings.Count == 0;
    }

    // Task A8: design rules for a maze map, on top of what MazeGrid.Parse already enforces (one
    // start, one door, solid edges, everything reachable). These are the rules the pickups, the
    // ghosts and the level designers rely on. Plain C#, so it is unit tested without a scene.
    public static class MazeChecks
    {
        // The start must be at least this many tiles (Manhattan) from the ghost exit, or the ghosts
        // leave the house on top of the player.
        public const int MinStartToExitDistance = 3;

        public const int MinCompletionTotal = 10;
        public const int MaxFlares = 8;

        public static MazeReport Check(MazeGrid grid)
        {
            var report = new MazeReport();
            if (grid == null)
            {
                report.Errors.Add("There is no maze to check.");
                return report;
            }

            CheckBatterySpots(grid, report);
            CheckStartDistance(grid, report);
            CheckPickupCounts(grid, report);
            CheckDeadEnds(grid, report);
            CheckWarps(grid, report);
            return report;
        }

        // Batteries wait under the ghost house. Row 0 is the top of the map, so "under" is a larger row.
        static void CheckBatterySpots(MazeGrid grid, MazeReport report)
        {
            IReadOnlyList<TileCoord> spots = grid.Find(MazeTile.Battery);
            if (spots.Count == 0)
                report.Warnings.Add("The map has no battery spot 'b', so the flashlight can never be recharged.");

            int doorRow = grid.HouseDoor.Row;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Row <= doorRow)
                    report.Errors.Add($"Battery spot {spots[i]} is not under the ghost house (it must be below the door, row {doorRow}).");
            }
        }

        static void CheckStartDistance(MazeGrid grid, MazeReport report)
        {
            TileCoord start = grid.PlayerStart;
            TileCoord exit = grid.GhostExit;
            int distance = System.Math.Abs(start.Row - exit.Row) + System.Math.Abs(start.Col - exit.Col);
            if (distance < MinStartToExitDistance)
                report.Warnings.Add($"The player start {start} is only {distance} tiles from the ghost exit {exit}; the ghosts leave the house on top of the player.");
        }

        static void CheckPickupCounts(MazeGrid grid, MazeReport report)
        {
            int flares = grid.Count(MazeTile.Flare);
            if (flares == 0)
                report.Warnings.Add("The map has no flare 'o', so the player has no way to scare the ghosts.");
            else if (flares > MaxFlares)
                report.Warnings.Add($"The map has {flares} flares; more than {MaxFlares} makes the ghosts too easy.");

            if (grid.CompletionTotal < MinCompletionTotal)
                report.Warnings.Add($"The map has only {grid.CompletionTotal} cells and flares; the level ends almost at once.");
        }

        // A corridor tile with a single open side traps the player against a ghost. The original
        // maze has none, so each one is worth a second look. Only the first few are listed.
        static void CheckDeadEnds(MazeGrid grid, MazeReport report)
        {
            const int Listed = 5;
            int found = 0;

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    if (!IsCorridor(grid.TileAt(row, col)))
                        continue;

                    int open = 0;
                    if (grid.IsWalkable(row + 1, col)) open++;
                    if (grid.IsWalkable(row - 1, col)) open++;
                    if (grid.IsWalkable(row, col + 1)) open++;
                    if (grid.IsWalkable(row, col - 1)) open++;
                    if (open != 1)
                        continue;

                    found++;
                    if (found <= Listed)
                        report.Warnings.Add($"Dead end at {new TileCoord(row, col)}: only one side is open.");
                }
            }

            if (found > Listed)
                report.Warnings.Add($"{found - Listed} more dead ends.");
        }

        static void CheckWarps(MazeGrid grid, MazeReport report)
        {
            var unpaired = new List<TileCoord>();
            WarpPairs.Compute(grid, unpaired);
            for (int i = 0; i < unpaired.Count; i++)
                report.Warnings.Add($"Warp door {unpaired[i]} has no partner in its row or column, so it does nothing.");
        }

        static bool IsCorridor(MazeTile tile) =>
            tile == MazeTile.Path || tile == MazeTile.Cell || tile == MazeTile.Flare ||
            tile == MazeTile.Battery || tile == MazeTile.PlayerStart;
    }
}
