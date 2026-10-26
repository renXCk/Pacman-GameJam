using System;

namespace Game.Maze
{
    // A tile position on the map. Row 0 is the top of the map, column 0 is the left.
    public readonly struct TileCoord : IEquatable<TileCoord>
    {
        public int Row { get; }
        public int Col { get; }

        public TileCoord(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(TileCoord other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is TileCoord other && Equals(other);
        public override int GetHashCode() => unchecked(Row * 397) ^ Col;
        public override string ToString() => $"(row {Row}, col {Col})";

        public static bool operator ==(TileCoord a, TileCoord b) => a.Equals(b);
        public static bool operator !=(TileCoord a, TileCoord b) => !a.Equals(b);
    }
}
