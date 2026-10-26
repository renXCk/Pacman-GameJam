using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Maze
{
    // Task A3: bakes the NavMesh for the maze right after it is built, so ghosts (and the test
    // agent in the tests) can walk every corridor. Lives on the same GameObject as MazeBuilder,
    // which calls OnMazeBuilt itself, so no script order is involved.
    //
    // The NavMesh is baked from the maze's colliders:
    //  - the wall cubes are marked Not Walkable, so the mesh never covers wall tops;
    //  - the ghost house door is on the Ignore Raycast layer and left out, so ghosts walk
    //    through it while its collider still blocks the player.
    // The surface sits on the maze geometry object, so it is removed and rebuilt with the maze.
    public sealed class MazeNavMesh : MonoBehaviour, IMazeListener
    {
        // The built-in "Not Walkable" NavMesh area.
        public const int NotWalkableArea = 1;

        public NavMeshSurface Surface { get; private set; }
        public bool HasNavMesh => Surface != null && Surface.navMeshData != null;

        public void OnMazeBuilt(MazeBuilder builder)
        {
            // The old surface (if any) went away with the old geometry.
            Surface = null;

            NavMeshModifier notWalkable = builder.Walls.gameObject.AddComponent<NavMeshModifier>();
            notWalkable.overrideArea = true;
            notWalkable.area = NotWalkableArea;
            notWalkable.applyToChildren = true;

            NavMeshSurface surface = builder.Geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << MazeBuilder.DoorLayer);
            surface.BuildNavMesh();
            Surface = surface;

            WarnIfOff(builder, builder.Grid.PlayerStart, "the player start");
            WarnIfOff(builder, builder.Grid.GhostExit, "the ghost house exit");
        }

        // Whether a world position is on (or within tolerance of) the baked NavMesh.
        public static bool IsOnNavMesh(Vector3 position, float tolerance = 0.5f) =>
            NavMesh.SamplePosition(position, out NavMeshHit _, tolerance, NavMesh.AllAreas);

        static void WarnIfOff(MazeBuilder builder, TileCoord tile, string what)
        {
            if (!IsOnNavMesh(builder.TileCenter(tile)))
                Debug.LogWarning($"The NavMesh does not cover {what} at {tile}. The bake may have failed.", builder);
        }
    }
}
