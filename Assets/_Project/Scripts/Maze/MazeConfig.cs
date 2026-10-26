using UnityEngine;

namespace Game.Maze
{
    // Everything the maze builder needs that is not the map itself: which map file to load,
    // how big the tiles and walls are, and the materials and prefabs to build with.
    // One asset is enough until there are several maze layouts.
    [CreateAssetMenu(fileName = "MazeConfig", menuName = "Game/Configs/Maze Config")]
    public sealed class MazeConfig : ScriptableObject
    {
        [Header("Map")]
        [Tooltip("The maze text map, for example Maze_Level1.txt.")]
        [SerializeField] TextAsset mapFile;

        [Header("Size (meters)")]
        [Tooltip("Width of one maze tile. The art guide says 3.")]
        [Min(0.5f)] [SerializeField] float tileSize = 3f;

        [Tooltip("Height of the walls. The art guide says 3.3.")]
        [Min(0.5f)] [SerializeField] float wallHeight = 3.3f;

        [Header("Materials")]
        [SerializeField] Material wallMaterial;
        [SerializeField] Material floorMaterial;
        [SerializeField] Material doorMaterial;

        [Header("Ghosts")]
        [Tooltip("Blinky, Pinky, Inky, Clyde, in that order. Blinky starts outside the house, the rest inside.")]
        [SerializeField] GameObject[] ghostPrefabs;

        public TextAsset MapFile => mapFile;
        public float TileSize => tileSize;
        public float WallHeight => wallHeight;
        public Material WallMaterial => wallMaterial;
        public Material FloorMaterial => floorMaterial;
        public Material DoorMaterial => doorMaterial;
        public GameObject[] GhostPrefabs => ghostPrefabs;
    }
}
