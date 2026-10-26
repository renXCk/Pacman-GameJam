using System.Collections.Generic;

namespace Game.Maze
{
    // Two warp door tiles that lead to each other.
    public readonly struct WarpPair
    {
        public TileCoord A { get; }
        public TileCoord B { get; }

        public WarpPair(TileCoord a, TileCoord b)
        {
            A = a;
            B = b;
        }
    }

    // Task A7 (optional): which 'W' tiles belong together. The map only says where the warp doors
    // are, so the rule is: two doors in the same row are a pair (the left and right edges of a
    // maze), otherwise two doors in the same column (the top and bottom edges). Plain C#, so it is
    // unit tested without a scene.
    public static class WarpPairs
    {
        // The pairs, in reading order of their first door. Doors that find no partner are added to
        // unpaired when it is given.
        public static List<WarpPair> Compute(MazeGrid grid, List<TileCoord> unpaired = null)
        {
            var pairs = new List<WarpPair>();
            IReadOnlyList<TileCoord> doors = grid.Find(MazeTile.WarpDoor);
            var used = new bool[doors.Count];

            for (int i = 0; i < doors.Count; i++)
            {
                if (used[i])
                    continue;

                used[i] = true;
                int partner = FindPartner(doors, used, i, sameRow: true);
                if (partner < 0)
                    partner = FindPartner(doors, used, i, sameRow: false);

                if (partner < 0)
                {
                    if (unpaired != null)
                        unpaired.Add(doors[i]);
                    continue;
                }

                used[partner] = true;
                pairs.Add(new WarpPair(doors[i], doors[partner]));
            }

            return pairs;
        }

        static int FindPartner(IReadOnlyList<TileCoord> doors, bool[] used, int index, bool sameRow)
        {
            for (int j = index + 1; j < doors.Count; j++)
            {
                if (used[j])
                    continue;

                bool match = sameRow ? doors[j].Row == doors[index].Row : doors[j].Col == doors[index].Col;
                if (match)
                    return j;
            }

            return -1;
        }
    }
}
