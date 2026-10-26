using System.Collections.Generic;
using Game.Core;
using Game.Events;
using Game.Maze;
using UnityEngine;

namespace Game.Pickups
{
    // Task A5: fills the maze with pickups. Sits on the same GameObject as MazeBuilder, which calls
    // OnMazeBuilt itself once the walls exist. Every '.' on the map becomes a power cell, every 'o' a
    // flare and every 'b' a battery, all as trigger colliders under one "Pickups" object.
    //
    // It also owns the level reset: when a new level starts (LevelStarted) every pickup comes back
    // and LevelProgress is restarted with the number of cells and flares on the map. A catch does
    // not reset anything here: the cells the player took stay taken.
    public sealed class MazePickups : MonoBehaviour, IMazeListener
    {
        // Sizes from the art guide: height above the floor, and the radius of the trigger sphere.
        public const float CellHeight = 0.9f;
        public const float CellRadius = 0.45f;
        public const float FlareHeight = 1.0f;
        public const float FlareRadius = 0.6f;
        public const float BatteryHeight = 0.8f;
        public const float BatteryRadius = 0.5f;

        const string ContainerName = "Pickups";

        [Header("Pickup types")]
        [SerializeField] PickupConfig cellConfig;
        [SerializeField] PickupConfig flareConfig;
        [SerializeField] PickupConfig batteryConfig;

        [Header("Materials (emissive, no lights on pickups)")]
        [SerializeField] Material cellMaterial;
        [SerializeField] Material flareMaterial;
        [SerializeField] Material batteryMaterial;

        [Header("Level progress")]
        [Tooltip("The runtime asset the game manager watches. Restarted on every LevelStarted.")]
        [SerializeField] LevelProgress progress;

        [Header("Listens to")]
        [SerializeField] LevelConfigEventChannel levelStarted;

        [Header("Raises")]
        [SerializeField] PickupEventChannel pickupCollected;
        [SerializeField] VoidEventChannel flareCollected;

        readonly List<Pickup> cells = new List<Pickup>();
        readonly List<Pickup> flares = new List<Pickup>();
        readonly List<Pickup> batteries = new List<Pickup>();

        Transform container;
        MazeGrid grid;
        bool batteriesManaged;

        public IReadOnlyList<Pickup> Cells => cells;
        public IReadOnlyList<Pickup> Flares => flares;
        public IReadOnlyList<Pickup> Batteries => batteries;

        // The Pickups object the pickups are children of. Null until the maze has been built.
        public Transform Container => container;

        // What the level needs: cells plus flares, never batteries. Same number as MazeGrid.CompletionTotal.
        public int CompletionTotal => cells.Count + flares.Count;

        // Pickups taken so far among the ones that count toward the level.
        public int CollectedCount
        {
            get
            {
                int taken = 0;
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i].IsCollected) taken++;
                for (int i = 0; i < flares.Count; i++)
                    if (flares[i].IsCollected) taken++;
                return taken;
            }
        }

        void OnEnable()
        {
            if (levelStarted != null)
                levelStarted.Raised += OnLevelStarted;
        }

        void OnDisable()
        {
            if (levelStarted != null)
                levelStarted.Raised -= OnLevelStarted;
        }

        public void OnMazeBuilt(MazeBuilder builder)
        {
            Clear();

            grid = builder.Grid;
            float size = builder.TileSize;

            container = new GameObject(ContainerName).transform;
            container.SetParent(builder.transform, false);

            IReadOnlyList<TileCoord> cellTiles = grid.Find(MazeTile.Cell);
            for (int i = 0; i < cellTiles.Count; i++)
                cells.Add(Spawn(cellConfig, cellMaterial, "Cell", cellTiles[i], size, CellHeight, CellRadius,
                    PrimitiveType.Sphere, new Vector3(0.28f, 0.28f, 0.28f), 0f));

            IReadOnlyList<TileCoord> flareTiles = grid.Find(MazeTile.Flare);
            for (int i = 0; i < flareTiles.Count; i++)
                flares.Add(Spawn(flareConfig, flareMaterial, "Flare", flareTiles[i], size, FlareHeight, FlareRadius,
                    PrimitiveType.Sphere, new Vector3(0.6f, 0.6f, 0.6f), 0.12f));

            IReadOnlyList<TileCoord> batteryTiles = grid.Find(MazeTile.Battery);
            for (int i = 0; i < batteryTiles.Count; i++)
            {
                Pickup battery = Spawn(batteryConfig, batteryMaterial, "Battery", batteryTiles[i], size, BatteryHeight, BatteryRadius,
                    PrimitiveType.Cylinder, new Vector3(0.3f, 0.22f, 0.3f), 0f);
                batteries.Add(battery);

                if (batteriesManaged)
                    battery.Hide();
            }
        }

        // A battery rule (BatteryDirector) takes over when the batteries appear. While it does,
        // batteries start hidden and a new level does not bring them back by itself.
        public void SetBatteriesManaged(bool managed)
        {
            batteriesManaged = managed;
            if (!managed)
                return;

            for (int i = 0; i < batteries.Count; i++)
                batteries[i].Hide();
        }

        // Every pickup back in the maze, cells and flares not taken, and the level count restarted.
        public void BeginLevel()
        {
            RestoreAll();

            if (progress != null && grid != null)
                progress.Begin(CompletionTotal);
        }

        // Puts the pickups back without touching LevelProgress.
        public void RestoreAll()
        {
            for (int i = 0; i < cells.Count; i++)
                cells[i].Restore();
            for (int i = 0; i < flares.Count; i++)
                flares[i].Restore();

            for (int i = 0; i < batteries.Count; i++)
            {
                if (batteriesManaged)
                    batteries[i].Hide();
                else
                    batteries[i].Restore();
            }
        }

        void OnLevelStarted(LevelConfig level) => BeginLevel();

        // Removes the pickups. The next Build puts them back.
        public void Clear()
        {
            cells.Clear();
            flares.Clear();
            batteries.Clear();
            grid = null;

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

        Pickup Spawn(PickupConfig config, Material material, string label, TileCoord tile, float tileSize,
            float height, float triggerRadius, PrimitiveType shape, Vector3 visualScale, float pulse)
        {
            var root = new GameObject($"{label} {tile.Row},{tile.Col}");
            root.transform.SetParent(container, false);
            root.transform.localPosition = grid.TileToWorld(tile, tileSize) + Vector3.up * height;

            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = triggerRadius;

            // The visual is a plain mesh. Its own collider is removed on the spot (not at the end of
            // the frame) so it can never block the player or a raycast.
            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = "Visual";
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                DestroyImmediate(visualCollider);

            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualScale;
            var meshRenderer = visual.GetComponent<Renderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (material != null)
                meshRenderer.sharedMaterial = material;

            var pickup = root.AddComponent<Pickup>();
            pickup.Initialise(config, progress, pickupCollected, flareCollected, visual.transform, pulse);
            return pickup;
        }
    }
}
