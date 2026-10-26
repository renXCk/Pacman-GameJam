using UnityEngine;

namespace Game.Maze
{
    // Task A4 (support): moves the player's spawn marker to the maze's player start tile, turned
    // toward the longest open stretch. Sits on the same GameObject as MazeBuilder, which calls
    // OnMazeBuilt itself, so the marker is right before the first round starts.
    public sealed class MazePlayerSpawn : MonoBehaviour, IMazeListener
    {
        [Tooltip("The marker the PlayerController spawns at. Set up by Game > Player > Add Player To Game Scene.")]
        [SerializeField] Transform spawnPoint;

        public Transform SpawnPoint => spawnPoint;

        public void OnMazeBuilt(MazeBuilder builder)
        {
            if (spawnPoint == null)
            {
                Debug.LogError("MazePlayerSpawn has no spawn point. Run Game > Player > Add Player To Game Scene.", this);
                return;
            }

            TileCoord start = builder.Grid.PlayerStart;
            spawnPoint.SetPositionAndRotation(
                builder.TileCenter(start),
                Quaternion.Euler(0f, FacingYaw(builder.Grid, start), 0f));
        }

        // The heading, in degrees, that looks down the longest run of open tiles from a tile.
        // North (up the map) is 0, east 90, south 180, west 270. A tie goes to the first of those.
        public static float FacingYaw(MazeGrid grid, TileCoord tile)
        {
            int[] rowStep = { -1, 0, 1, 0 };
            int[] colStep = { 0, 1, 0, -1 };

            int bestDirection = 0;
            int bestRun = -1;

            for (int direction = 0; direction < 4; direction++)
            {
                int run = 0;
                int row = tile.Row + rowStep[direction];
                int col = tile.Col + colStep[direction];
                while (grid.IsWalkable(row, col))
                {
                    run++;
                    row += rowStep[direction];
                    col += colStep[direction];
                }

                if (run > bestRun)
                {
                    bestRun = run;
                    bestDirection = direction;
                }
            }

            return bestDirection * 90f;
        }
    }
}
