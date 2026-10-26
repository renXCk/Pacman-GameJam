using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Maze
{
    // Anything on the same GameObject as the MazeBuilder that wants to react once the maze has been
    // built (the NavMesh baker, the ghost spawner). The builder calls them itself, in the order
    // they appear on the object, so nothing depends on script execution order.
    public interface IMazeListener
    {
        void OnMazeBuilt(MazeBuilder builder);
    }

    // Task A2: turns the maze map into walls, a floor and the ghost house door.
    //
    // It builds in Awake, before almost everything else, because the first LevelStarted is raised
    // in GameManager.Start and the ghosts need the maze layout by then. Put it on a GameObject at
    // the world origin: the map's top-left tile is column 0, and z grows toward the top of the map.
    [DefaultExecutionOrder(-200)]
    public sealed class MazeBuilder : MonoBehaviour
    {
        // The Ignore Raycast layer. The house door sits on it so the NavMesh bake leaves it out
        // (ghosts walk through it) while its collider still blocks the player.
        public const int DoorLayer = 2;

        const string GeometryName = "Maze Geometry";
        const float DoorThickness = 0.3f;

        [SerializeField] MazeConfig config;

        [Tooltip("Build in Awake. Turn off only for tests that call Build themselves.")]
        [SerializeField] bool buildOnAwake = true;

        Transform geometry;
        Transform walls;
        Transform houseDoor;

        public MazeConfig Config => config;
        public MazeGrid Grid { get; private set; }
        public bool IsBuilt => Grid != null;
        public float TileSize => config != null ? config.TileSize : 3f;

        // Parent of every wall cube. The NavMesh step marks it Not Walkable.
        public Transform Walls => walls;
        public Transform Geometry => geometry;
        public Transform HouseDoor => houseDoor;

        // Raised at the end of Build, after the IMazeListener components have run.
        public event Action<MazeBuilder> Built;

        // The world position of the middle of a tile, on the floor.
        public Vector3 TileCenter(TileCoord tile) => transform.TransformPoint(Grid.TileToWorld(tile, TileSize));
        public Vector3 TileCenter(int row, int col) => TileCenter(new TileCoord(row, col));

        void Awake()
        {
            if (buildOnAwake)
                Build();
        }

        // Loads the configured map and builds it. Safe to call again: the old maze is removed first.
        public void Build()
        {
            if (config == null || config.MapFile == null)
            {
                Debug.LogError("MazeBuilder has no MazeConfig with a map file.", this);
                return;
            }

            if (!MazeGrid.TryParse(config.MapFile.text, out MazeGrid grid, out List<string> errors))
            {
                Debug.LogError($"The maze map {config.MapFile.name} is not valid:\n- " + string.Join("\n- ", errors), this);
                return;
            }

            Build(grid);
        }

        public void Build(MazeGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (config == null)
            {
                Debug.LogError("MazeBuilder has no MazeConfig.", this);
                return;
            }

            Clear();
            Grid = grid;

            geometry = new GameObject(GeometryName).transform;
            geometry.SetParent(transform, false);

            BuildFloor(grid);
            BuildWalls(grid);
            BuildHouseDoor(grid);

            // The colliders were just created: make the physics scene see them now, not next frame,
            // so a NavMesh bake or a raycast in the same frame finds them.
            Physics.SyncTransforms();

            IMazeListener[] listeners = GetComponents<IMazeListener>();
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].OnMazeBuilt(this);

            Built?.Invoke(this);
        }

        // Removes the built maze.
        public void Clear()
        {
            Grid = null;
            if (geometry == null)
                return;

            GameObject old = geometry.gameObject;
            geometry = null;
            walls = null;
            houseDoor = null;

            // Hide it first: Destroy only takes effect at the end of the frame.
            old.SetActive(false);
            if (Application.isPlaying)
                Destroy(old);
            else
                DestroyImmediate(old);
        }

        [ContextMenu("Rebuild Maze")]
        void RebuildFromMenu() => Build();

        void BuildFloor(MazeGrid grid)
        {
            float size = config.TileSize;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(geometry, false);
            floor.transform.localPosition = new Vector3((grid.Cols - 1) * size * 0.5f, -0.1f, (grid.Rows - 1) * size * 0.5f);
            floor.transform.localScale = new Vector3(grid.Cols * size, 0.2f, grid.Rows * size);
            ApplyMaterial(floor, config.FloorMaterial);
        }

        void BuildWalls(MazeGrid grid)
        {
            float size = config.TileSize;
            float height = config.WallHeight;

            walls = new GameObject("Walls").transform;
            walls.SetParent(geometry, false);

            List<WallRun> runs = MazeWallRuns.Compute(grid);
            for (int i = 0; i < runs.Count; i++)
            {
                WallRun run = runs[i];
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall {run.Row},{run.StartCol}";
                wall.transform.SetParent(walls, false);

                float x = (run.StartCol + run.EndCol) * 0.5f * size;
                float z = (grid.Rows - 1 - run.Row) * size;
                wall.transform.localPosition = new Vector3(x, height * 0.5f, z);
                wall.transform.localScale = new Vector3(run.Length * size, height, size);
                ApplyMaterial(wall, config.WallMaterial);
            }
        }

        void BuildHouseDoor(MazeGrid grid)
        {
            float size = config.TileSize;
            float height = config.WallHeight;
            TileCoord door = grid.HouseDoor;

            // The door fills the gap in the house wall. The ghosts pass through it vertically on the
            // map (exit above, house below) in the normal layout; if the house is turned sideways
            // the door turns with it.
            bool passageRunsAlongZ = IsHouseTile(grid, door.Row - 1, door.Col) || IsHouseTile(grid, door.Row + 1, door.Col);

            GameObject doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.name = "House Door (blocks player only)";
            doorObject.layer = DoorLayer;
            doorObject.transform.SetParent(geometry, false);
            doorObject.transform.localPosition = grid.TileToWorld(door, size) + Vector3.up * (height * 0.5f);
            doorObject.transform.localScale = passageRunsAlongZ
                ? new Vector3(size, height, DoorThickness)
                : new Vector3(DoorThickness, height, size);
            ApplyMaterial(doorObject, config.DoorMaterial);
            houseDoor = doorObject.transform;
        }

        static bool IsHouseTile(MazeGrid grid, int row, int col)
        {
            MazeTile tile = grid.TileAt(row, col);
            return tile == MazeTile.GhostExit || tile == MazeTile.GhostHouse;
        }

        static void ApplyMaterial(GameObject target, Material material)
        {
            if (material != null)
                target.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
