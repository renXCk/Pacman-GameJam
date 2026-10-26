using System.Collections.Generic;
using Game.Core;
using Game.Ghosts;
using UnityEngine;

namespace Game.Maze
{
    // Task A3 (support for Dev B): gives the ghosts their maze. After the maze is built it
    //  1. moves the six GhostMazeLayout markers (house exit, house inside, four scatter corners)
    //     to the right tiles and tells the layout the tile size, and
    //  2. spawns the four ghost prefabs from the MazeConfig: Blinky on the exit tile, the others
    //     in the house.
    // It runs inside MazeBuilder's Awake, before the GhostDirector enables, so the director always
    // finds a complete layout. The ghosts' agents stay off until the director places them in the
    // Ready state, by which time the NavMesh exists.
    public sealed class MazeGhosts : MonoBehaviour, IMazeListener
    {
        // Pinky, Inky and Clyde take the house tiles in this order (reading order, left to right).
        static readonly int[] HouseSlots = { 1, 0, 2 };

        [Header("Ghost layout (set up by Game > Maze > Add Maze To Game Scene)")]
        [SerializeField] GhostMazeLayout layout;
        [SerializeField] Transform houseExit;
        [SerializeField] Transform houseInside;
        [SerializeField] Transform topLeft;
        [SerializeField] Transform topRight;
        [SerializeField] Transform bottomLeft;
        [SerializeField] Transform bottomRight;

        [Header("Spawning")]
        [Tooltip("Turn off to test the maze without ghosts.")]
        [SerializeField] bool spawnGhosts = true;

        readonly List<GameObject> spawned = new List<GameObject>();
        Transform ghostsParent;

        public GhostMazeLayout Layout => layout;
        public IReadOnlyList<GameObject> Spawned => spawned;

        // The tile the banished ghosts go back to: the middle of the house.
        public static TileCoord HouseInsideTile(MazeGrid grid)
        {
            IReadOnlyList<TileCoord> house = grid.Find(MazeTile.GhostHouse);
            return house[house.Count / 2];
        }

        public void OnMazeBuilt(MazeBuilder builder)
        {
            DespawnAll();

            if (!PlaceLayout(builder))
                return;

            if (spawnGhosts)
                SpawnAll(builder);
        }

        bool PlaceLayout(MazeBuilder builder)
        {
            if (layout == null || houseExit == null || houseInside == null ||
                topLeft == null || topRight == null || bottomLeft == null || bottomRight == null)
            {
                Debug.LogError("MazeGhosts is missing its layout or one of its markers. Run Game > Maze > Add Maze To Game Scene.", this);
                return false;
            }

            MazeGrid grid = builder.Grid;
            houseExit.position = builder.TileCenter(grid.GhostExit);
            houseInside.position = builder.TileCenter(HouseInsideTile(grid));
            topLeft.position = builder.TileCenter(grid.CornerTile(MazeCorner.TopLeft));
            topRight.position = builder.TileCenter(grid.CornerTile(MazeCorner.TopRight));
            bottomLeft.position = builder.TileCenter(grid.CornerTile(MazeCorner.BottomLeft));
            bottomRight.position = builder.TileCenter(grid.CornerTile(MazeCorner.BottomRight));

            layout.Configure(builder.TileSize, houseExit, houseInside, topLeft, topRight, bottomLeft, bottomRight);
            return true;
        }

        void SpawnAll(MazeBuilder builder)
        {
            GameObject[] prefabs = builder.Config.GhostPrefabs;
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning("The MazeConfig has no ghost prefabs, so no ghosts were spawned.", this);
                return;
            }

            if (ghostsParent == null)
            {
                ghostsParent = new GameObject("Ghosts").transform;
                ghostsParent.SetParent(transform, false);
            }

            MazeGrid grid = builder.Grid;
            IReadOnlyList<TileCoord> house = grid.Find(MazeTile.GhostHouse);

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    Debug.LogWarning($"Ghost prefab {i} in the MazeConfig is empty.", this);
                    continue;
                }

                // Blinky starts outside, on the exit tile. The rest wait inside the house.
                TileCoord tile = i == 0
                    ? grid.GhostExit
                    : house[HouseSlots[(i - 1) % HouseSlots.Length] % house.Count];

                // Placed at spawn time (not moved afterwards): a ghost remembers where it was
                // when it woke up as its home position.
                GameObject ghost = Instantiate(prefabs[i], builder.TileCenter(tile), Quaternion.identity, ghostsParent);
                spawned.Add(ghost);
            }
        }

        void DespawnAll()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                GameObject ghost = spawned[i];
                if (ghost == null)
                    continue;

                // Disabling first takes the ghost out of the GhostRuntimeSet right away.
                ghost.SetActive(false);
                if (Application.isPlaying)
                    Destroy(ghost);
                else
                    DestroyImmediate(ghost);
            }

            spawned.Clear();
        }
    }
}
