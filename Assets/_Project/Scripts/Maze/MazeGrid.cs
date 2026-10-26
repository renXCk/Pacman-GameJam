using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Maze
{
    // Thrown by MazeGrid.Parse when the text map is not a valid maze. Errors lists every problem.
    public sealed class MazeParseException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public MazeParseException(IReadOnlyList<string> errors)
            : base("The maze map is not valid:\n- " + string.Join("\n- ", errors))
        {
            Errors = errors;
        }
    }

    // The maze as data (task A1): a text map turned into a grid of tile types, with the checks that
    // keep a broken map from reaching the builder (A2). It holds no scene objects, so it is unit
    // tested on its own. Row 0 is the top of the map (+Z) and column 0 is the left (-X), the same
    // layout the ghost sandbox uses.
    //
    // Map text rules: one character per tile (see MazeGlyphs), every row the same length, blank
    // lines and lines starting with ';' are ignored. Error messages say "line N" for a position in
    // the text and "(row R, col C)" for a position on the map, both counted from the top left.
    public sealed class MazeGrid
    {
        const int MaxListedErrors = 3;

        static readonly List<TileCoord> NoTiles = new List<TileCoord>();

        readonly MazeTile[,] tiles;
        readonly Dictionary<MazeTile, List<TileCoord>> byType = new Dictionary<MazeTile, List<TileCoord>>();

        public int Rows { get; }
        public int Cols { get; }

        MazeGrid(MazeTile[,] tiles)
        {
            this.tiles = tiles;
            Rows = tiles.GetLength(0);
            Cols = tiles.GetLength(1);

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    MazeTile tile = tiles[row, col];
                    if (!byType.TryGetValue(tile, out List<TileCoord> list))
                    {
                        list = new List<TileCoord>();
                        byType[tile] = list;
                    }

                    list.Add(new TileCoord(row, col));
                }
            }
        }

        // The single tiles every valid map has. Parse guarantees they exist.
        public TileCoord PlayerStart => Find(MazeTile.PlayerStart)[0];
        public TileCoord GhostExit => Find(MazeTile.GhostExit)[0];
        public TileCoord HouseDoor => Find(MazeTile.HouseDoor)[0];

        // What to pass to LevelProgress.Begin: cells and flares count toward the level, batteries do not.
        public int CompletionTotal => Count(MazeTile.Cell) + Count(MazeTile.Flare);

        public bool Contains(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Cols;

        // Anything outside the map counts as wall.
        public MazeTile TileAt(int row, int col) => Contains(row, col) ? tiles[row, col] : MazeTile.Wall;
        public MazeTile TileAt(TileCoord tile) => TileAt(tile.Row, tile.Col);

        public bool IsWalkable(int row, int col) => TileAt(row, col) != MazeTile.Wall;

        // Every tile of one type, in reading order (top to bottom, left to right).
        public IReadOnlyList<TileCoord> Find(MazeTile tile) =>
            byType.TryGetValue(tile, out List<TileCoord> list) ? list : NoTiles;

        public int Count(MazeTile tile) => Find(tile).Count;

        // The world position of a tile's center on the floor. The same rule as the ghost sandbox:
        // x grows with the column, z grows toward the top of the map.
        public Vector3 TileToWorld(int row, int col, float tileSize) =>
            new Vector3(col * tileSize, 0f, (Rows - 1 - row) * tileSize);

        public Vector3 TileToWorld(TileCoord tile, float tileSize) => TileToWorld(tile.Row, tile.Col, tileSize);

        // The tile a world position is on. It can be outside the map; check with Contains.
        public TileCoord WorldToTile(Vector3 world, float tileSize)
        {
            int col = Mathf.RoundToInt(world.x / tileSize);
            int row = Rows - 1 - Mathf.RoundToInt(world.z / tileSize);
            return new TileCoord(row, col);
        }

        // The ordinary corridor tile closest to a map corner. This is where the ghosts' scatter
        // corner markers go. Ties go to the first tile in reading order.
        public TileCoord CornerTile(MazeCorner corner)
        {
            bool top = corner == MazeCorner.TopLeft || corner == MazeCorner.TopRight;
            bool left = corner == MazeCorner.TopLeft || corner == MazeCorner.BottomLeft;
            int targetRow = top ? 0 : Rows - 1;
            int targetCol = left ? 0 : Cols - 1;

            TileCoord best = PlayerStart;
            int bestDistance = int.MaxValue;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    if (!IsCorridor(tiles[row, col]))
                        continue;

                    int distance = Math.Abs(row - targetRow) + Math.Abs(col - targetCol);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = new TileCoord(row, col);
                    }
                }
            }

            return best;
        }

        static bool IsCorridor(MazeTile tile) =>
            tile == MazeTile.Path || tile == MazeTile.Cell || tile == MazeTile.Flare || tile == MazeTile.Battery;

        // Parses and checks a map. Throws MazeParseException listing every problem.
        public static MazeGrid Parse(string text)
        {
            if (TryParse(text, out MazeGrid grid, out List<string> errors))
                return grid;

            throw new MazeParseException(errors);
        }

        // Returns false with a list of errors (and a null grid) when the map is not valid.
        public static bool TryParse(string text, out MazeGrid grid, out List<string> errors)
        {
            grid = null;
            errors = new List<string>();

            var lines = new List<string>();
            var lineNumbers = new List<int>();
            ReadMapLines(text, lines, lineNumbers);

            if (lines.Count == 0)
            {
                errors.Add("The map is empty.");
                return false;
            }

            // Shape first: the later checks need a rectangle of known glyphs.
            int cols = lines[0].Length;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length != cols)
                    errors.Add($"Line {lineNumbers[i]} has {lines[i].Length} tiles, expected {cols} (the length of the first map row).");
            }

            if (errors.Count > 0)
                return false;

            var parsed = new MazeTile[lines.Count, cols];
            for (int row = 0; row < lines.Count; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (MazeGlyphs.TryGetTile(lines[row][col], out MazeTile tile))
                        parsed[row, col] = tile;
                    else
                        errors.Add($"Line {lineNumbers[row]}, character {col + 1}: unknown glyph '{lines[row][col]}'.");
                }
            }

            if (errors.Count > 0)
                return false;

            var candidate = new MazeGrid(parsed);
            candidate.Validate(errors);
            if (errors.Count > 0)
                return false;

            grid = candidate;
            return true;
        }

        // Splits the text into map rows, dropping blank lines, ';' comments and trailing spaces.
        static void ReadMapLines(string text, List<string> lines, List<int> lineNumbers)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string[] raw = text.TrimStart('\uFEFF').Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i].TrimEnd();
                if (line.Length == 0 || line[0] == ';')
                    continue;

                lines.Add(line);
                lineNumbers.Add(i + 1);
            }
        }

        // The rules a map must follow for the builder, the ghosts and LevelProgress to work.
        void Validate(List<string> errors)
        {
            int starts = Count(MazeTile.PlayerStart);
            if (starts != 1)
                errors.Add($"The map needs exactly one 'P' (player start), found {starts}.");

            int exits = Count(MazeTile.GhostExit);
            if (exits != 1)
                errors.Add($"The map needs exactly one 'G' (ghost house exit), found {exits}.");

            int houseTiles = Count(MazeTile.GhostHouse);
            if (houseTiles < 1)
                errors.Add("The map needs at least one 'H' (ghost house tile), found 0.");

            int doors = Count(MazeTile.HouseDoor);
            if (doors != 1)
                errors.Add($"The map needs exactly one '-' (ghost house door), found {doors}.");

            if (doors == 1 && !DoorTouchesExitAndHouse(HouseDoor))
                errors.Add($"The ghost house door at {HouseDoor} must touch the exit 'G' on one side and a house tile 'H' on the other.");

            CheckEdges(errors);

            if (CompletionTotal < 1)
                errors.Add("The map needs at least one power cell '.' or flare 'o'; a level with none can never be completed.");

            int warps = Count(MazeTile.WarpDoor);
            if (warps % 2 != 0)
                errors.Add($"Warp doors 'W' come in pairs, found {warps}.");

            if (starts == 1)
                CheckReachable(errors);
        }

        // The ghosts walk from the exit tile through the door into the house, so all three must line up.
        bool DoorTouchesExitAndHouse(TileCoord door)
        {
            bool touchesExit = false;
            bool touchesHouse = false;

            for (int i = 0; i < 4; i++)
            {
                int row = door.Row + (i == 0 ? 1 : i == 1 ? -1 : 0);
                int col = door.Col + (i == 2 ? 1 : i == 3 ? -1 : 0);
                MazeTile neighbor = TileAt(row, col);
                touchesExit |= neighbor == MazeTile.GhostExit;
                touchesHouse |= neighbor == MazeTile.GhostHouse;
            }

            return touchesExit && touchesHouse;
        }

        // The edge of the map must be solid, so the player cannot walk out of the maze. Warp doors are the exception.
        void CheckEdges(List<string> errors)
        {
            int open = 0;
            TileCoord first = default;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    bool onEdge = row == 0 || col == 0 || row == Rows - 1 || col == Cols - 1;
                    MazeTile tile = tiles[row, col];
                    if (!onEdge || tile == MazeTile.Wall || tile == MazeTile.WarpDoor)
                        continue;

                    if (open < MaxListedErrors)
                        errors.Add($"{new TileCoord(row, col)} is on the edge of the map, so it must be a wall '#' or a warp door 'W'.");
                    else if (open == MaxListedErrors)
                        first = new TileCoord(row, col);

                    open++;
                }
            }

            if (open > MaxListedErrors)
                errors.Add($"{open - MaxListedErrors} more edge tiles are open (the next one is {first}).");
        }

        // Every walkable tile must be reachable from the player start. Reaching one warp door
        // reaches all of them, because a warp door leads to its partner.
        void CheckReachable(List<string> errors)
        {
            var reached = new bool[Rows, Cols];
            var queue = new Queue<TileCoord>();
            IReadOnlyList<TileCoord> warps = Find(MazeTile.WarpDoor);
            bool warpsUsed = false;

            void Reach(int row, int col)
            {
                if (!IsWalkable(row, col) || reached[row, col])
                    return;

                reached[row, col] = true;
                queue.Enqueue(new TileCoord(row, col));
            }

            TileCoord start = PlayerStart;
            Reach(start.Row, start.Col);

            while (queue.Count > 0)
            {
                TileCoord current = queue.Dequeue();

                if (!warpsUsed && tiles[current.Row, current.Col] == MazeTile.WarpDoor)
                {
                    warpsUsed = true;
                    for (int i = 0; i < warps.Count; i++)
                        Reach(warps[i].Row, warps[i].Col);
                }

                Reach(current.Row + 1, current.Col);
                Reach(current.Row - 1, current.Col);
                Reach(current.Row, current.Col + 1);
                Reach(current.Row, current.Col - 1);
            }

            int unreachable = 0;
            TileCoord firstUnreachable = default;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    if (!IsWalkable(row, col) || reached[row, col])
                        continue;

                    if (unreachable == 0)
                        firstUnreachable = new TileCoord(row, col);

                    unreachable++;
                }
            }

            if (unreachable > 0)
                errors.Add($"{unreachable} walkable tiles cannot be reached from the player start (the first is {firstUnreachable}).");
        }
    }
}
