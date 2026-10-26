namespace Game.Maze
{
    // One square of the maze map (task A1). The text map spells each type with one glyph.
    public enum MazeTile : byte
    {
        Wall,        // '#'
        Path,        // '_' walkable, nothing on it
        Cell,        // '.' power cell, counts toward the level
        Flare,       // 'o' flare, counts toward the level
        Battery,     // 'b' battery spot, a bonus that never counts toward the level
        GhostExit,   // 'G' just outside the ghost house door
        GhostHouse,  // 'H' inside the ghost house
        HouseDoor,   // '-' ghosts walk through it, the player cannot
        PlayerStart, // 'P'
        WarpDoor     // 'W' comes in pairs on the map edge (built in A7)
    }

    // The glyph for each tile type, in one place so the parser, the tests and the docs agree.
    public static class MazeGlyphs
    {
        public const char Wall = '#';
        public const char Path = '_';
        public const char Cell = '.';
        public const char Flare = 'o';
        public const char Battery = 'b';
        public const char GhostExit = 'G';
        public const char GhostHouse = 'H';
        public const char HouseDoor = '-';
        public const char PlayerStart = 'P';
        public const char WarpDoor = 'W';

        public static bool TryGetTile(char glyph, out MazeTile tile)
        {
            switch (glyph)
            {
                case Wall: tile = MazeTile.Wall; return true;
                case Path: tile = MazeTile.Path; return true;
                case Cell: tile = MazeTile.Cell; return true;
                case Flare: tile = MazeTile.Flare; return true;
                case Battery: tile = MazeTile.Battery; return true;
                case GhostExit: tile = MazeTile.GhostExit; return true;
                case GhostHouse: tile = MazeTile.GhostHouse; return true;
                case HouseDoor: tile = MazeTile.HouseDoor; return true;
                case PlayerStart: tile = MazeTile.PlayerStart; return true;
                case WarpDoor: tile = MazeTile.WarpDoor; return true;
                default: tile = MazeTile.Wall; return false;
            }
        }

        public static char ToGlyph(MazeTile tile)
        {
            switch (tile)
            {
                case MazeTile.Path: return Path;
                case MazeTile.Cell: return Cell;
                case MazeTile.Flare: return Flare;
                case MazeTile.Battery: return Battery;
                case MazeTile.GhostExit: return GhostExit;
                case MazeTile.GhostHouse: return GhostHouse;
                case MazeTile.HouseDoor: return HouseDoor;
                case MazeTile.PlayerStart: return PlayerStart;
                case MazeTile.WarpDoor: return WarpDoor;
                default: return Wall;
            }
        }
    }
}
