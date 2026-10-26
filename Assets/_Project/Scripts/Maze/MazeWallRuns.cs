using System.Collections.Generic;

namespace Game.Maze
{
    // One horizontal run of wall tiles on the map. The builder makes one cube per run
    // instead of one per tile, which keeps the object count (and the NavMesh bake) small.
    public readonly struct WallRun
    {
        public int Row { get; }
        public int StartCol { get; }
        public int Length { get; }

        public WallRun(int row, int startCol, int length)
        {
            Row = row;
            StartCol = startCol;
            Length = length;
        }

        public int EndCol => StartCol + Length - 1;

        public override string ToString() => $"(row {Row}, cols {StartCol}-{EndCol})";
    }

    public static class MazeWallRuns
    {
        // Every wall tile belongs to exactly one run, in reading order.
        public static List<WallRun> Compute(MazeGrid grid)
        {
            var runs = new List<WallRun>();

            for (int row = 0; row < grid.Rows; row++)
            {
                int col = 0;
                while (col < grid.Cols)
                {
                    if (grid.TileAt(row, col) != MazeTile.Wall)
                    {
                        col++;
                        continue;
                    }

                    int start = col;
                    while (col < grid.Cols && grid.TileAt(row, col) == MazeTile.Wall)
                        col++;

                    runs.Add(new WallRun(row, start, col - start));
                }
            }

            return runs;
        }
    }
}
