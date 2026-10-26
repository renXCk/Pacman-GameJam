using System.Collections.Generic;
using UnityEngine;

namespace Game.Maze
{
    // Task A7 (optional): warp doors. Every 'W' tile on the map becomes a trigger, and walking into
    // one carries the player to its partner (see WarpPairs). Sits on the same GameObject as
    // MazeBuilder. A map with no 'W' tiles gets nothing from this, so Level 1 is unaffected.
    //
    // The ghosts do not use warps: a NavMesh link would be needed for that, and it is on the cut
    // list. For them a warp door is an ordinary dead-end tile.
    public sealed class MazeWarps : MonoBehaviour, IMazeListener
    {
        // After a warp the player stands inside the other door's trigger. This keeps them from
        // being sent straight back.
        public const float CooldownSeconds = 0.75f;

        const string PlayerTag = "Player";
        const string ContainerName = "Warp Doors";

        [Tooltip("The glowing pad drawn on each warp tile. Set up by Game > Pickups > Add Battery Rules And Warps.")]
        [SerializeField] Material padMaterial;

        readonly List<WarpDoor> doors = new List<WarpDoor>();
        Transform container;
        float lastWarp = float.NegativeInfinity;

        public IReadOnlyList<WarpDoor> Doors => doors;
        public int PairCount => doors.Count / 2;

        // How many times the player has been carried across since the maze was built.
        public int WarpCount { get; private set; }

        public void OnMazeBuilt(MazeBuilder builder)
        {
            Clear();

            MazeGrid grid = builder.Grid;
            var unpaired = new List<TileCoord>();
            List<WarpPair> pairs = WarpPairs.Compute(grid, unpaired);

            for (int i = 0; i < unpaired.Count; i++)
                Debug.LogWarning($"Warp door at {unpaired[i]} has no partner in its row or column, so it does nothing.", this);

            if (pairs.Count == 0)
                return;

            float size = builder.TileSize;
            container = new GameObject(ContainerName).transform;
            container.SetParent(builder.transform, false);

            for (int i = 0; i < pairs.Count; i++)
            {
                WarpDoor a = SpawnDoor(grid, pairs[i].A, size);
                WarpDoor b = SpawnDoor(grid, pairs[i].B, size);
                a.SetPartner(b);
                b.SetPartner(a);
            }
        }

        public void Clear()
        {
            doors.Clear();
            WarpCount = 0;
            lastWarp = float.NegativeInfinity;

            if (container == null)
                return;

            GameObject old = container.gameObject;
            container = null;

            // Hide it first: Destroy only takes effect at the end of the frame.
            old.SetActive(false);
            if (Application.isPlaying)
                Destroy(old);
            else
                DestroyImmediate(old);
        }

        // Something touched a warp door. Only the Player is carried across. Returns true if it was.
        public bool Touch(WarpDoor source, Collider who)
        {
            if (source == null || source.Partner == null || who == null || !who.CompareTag(PlayerTag))
                return false;

            if (Time.time - lastWarp < CooldownSeconds)
                return false;

            lastWarp = Time.time;
            WarpCount++;

            Transform body = who.transform;
            Vector3 exit = source.Partner.transform.position;
            Vector3 destination = new Vector3(exit.x, body.position.y, exit.z);

            // A CharacterController puts the body back where it was unless it is off while it moves.
            var controller = who.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                body.position = destination;
                controller.enabled = true;
            }
            else
            {
                body.position = destination;
            }

            return true;
        }

        WarpDoor SpawnDoor(MazeGrid grid, TileCoord tile, float size)
        {
            var root = new GameObject($"Warp Door {tile.Row},{tile.Col}");
            root.transform.SetParent(container, false);
            root.transform.localPosition = grid.TileToWorld(tile, size) + Vector3.up;

            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size * 0.8f, 2f, size * 0.8f);

            // A flat pad on the floor so the player can see where the warp is. No collider, no light.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            Collider padCollider = pad.GetComponent<Collider>();
            if (padCollider != null)
                DestroyImmediate(padCollider);

            pad.transform.SetParent(root.transform, false);
            pad.transform.localPosition = new Vector3(0f, -0.97f, 0f);
            pad.transform.localScale = new Vector3(size * 0.8f, 0.05f, size * 0.8f);

            var padRenderer = pad.GetComponent<Renderer>();
            padRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            padRenderer.receiveShadows = false;
            if (padMaterial != null)
                padRenderer.sharedMaterial = padMaterial;

            var door = root.AddComponent<WarpDoor>();
            door.Initialise(this, tile);
            doors.Add(door);
            return door;
        }
    }
}
