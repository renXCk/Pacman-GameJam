using System;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using Game.Sandbox;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Builds the ghost package's assets and its test maze.
    //   Game > Ghosts > Create Missing Ghost Assets: configs, channels, runtime set, prefabs. Never overwrites.
    //   Game > Ghosts > Rebuild Ghost Sandbox Scene: the test-only maze, rebuilt from the map below.
    // Batch mode: Unity -batchmode -projectPath <path> -executeMethod Game.EditorTools.GhostSetup.BuildAll -quit
    public static class GhostSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string PrefabFolder = Root + "/Prefabs/Ghosts";
        const string MaterialFolder = Root + "/Art/Materials";
        const string SandboxFolder = Root + "/Dev/GhostSandbox";
        const string SandboxMaterialFolder = SandboxFolder + "/Materials";
        public const string SandboxScenePath = SandboxFolder + "/GhostSandbox.unity";
        const string SandboxNavMeshPath = SandboxFolder + "/GhostSandbox_NavMesh.asset";

        const float TileSize = 2f;
        const float WallHeight = 2.5f;

        // Ignore Raycast. The house door sits on this layer so the NavMesh bake leaves it out:
        // ghosts walk through it, while its collider still blocks the player.
        const int DoorLayer = 2;

        // # wall   . corridor   o flare   G house exit (Blinky starts here)
        // - house door   H ghost house   P player start
        static readonly string[] Map =
        {
            "#####################",
            "#.........#.........#",
            "#o###.###.#.###.###o#",
            "#...................#",
            "#.###.#.#####.#.###.#",
            "#.....#...#...#.....#",
            "#####.###.#.###.#####",
            "#####.#...G...#.#####",
            "#####.#.##-##.#.#####",
            "#####...#HHH#...#####",
            "#####.#.#####.#.#####",
            "#####.#...P...#.#####",
            "#####.#.#####.#.#####",
            "#.........#.........#",
            "#.###.###.#.###.###.#",
            "#o..#...........#..o#",
            "###.#.#.#####.#.#.###",
            "#.....#...#...#.....#",
            "#.#######.#.#######.#",
            "#...................#",
            "#####################"
        };

        sealed class GhostAssets
        {
            public GhostAIConfig Ai;
            public LevelConfig[] Levels;
            public GhostConfig Blinky, Pinky, Inky, Clyde;
            public VoidEventChannel FlareCollected, PlayerCaught;
            public GameStateEventChannel GameStateChanged;
            public LevelConfigEventChannel LevelStarted;
            public GhostModeEventChannel ModeChanged;
            public GhostBanishedEventChannel GhostBanished;
            public GhostRuntimeSet GhostSet;
            public TransformAnchor PlayerAnchor;
            public Material GhostBody, GhostEyes;
            public GameObject BlinkyPrefab, PinkyPrefab, InkyPrefab, ClydePrefab;
        }

        [MenuItem("Game/Ghosts/Create Missing Ghost Assets")]
        public static void CreateMissingGhostAssets()
        {
            CreateGhostAssets();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Game/Ghosts/Rebuild Ghost Sandbox Scene")]
        public static void RebuildSandbox()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildSandbox();
            AssetDatabase.SaveAssets();
        }

        public static void BuildAll()
        {
            BuildSandbox();
            AssetDatabase.SaveAssets();
            Debug.Log("GhostSetup.BuildAll finished");
        }

        static GhostAssets CreateGhostAssets()
        {
            foreach (string folder in new[] { ConfigFolder, EventFolder, RuntimeFolder, PrefabFolder, MaterialFolder, SandboxMaterialFolder })
                EnsureFolder(folder);

            var a = new GhostAssets();
            a.Ai = LoadOrCreate<GhostAIConfig>(ConfigFolder + "/GhostAIConfig.asset");

            // Each replay gets faster ghosts and a shorter stun time.
            a.Levels = new[]
            {
                LevelAsset("LevelConfig_Level1", 3.8f, 2.2f, 8f, new[] { 7f, 20f, 7f, 20f, 5f }, 6f, 2f),
                LevelAsset("LevelConfig_Level2", 4.2f, 2.4f, 8.5f, new[] { 7f, 20f, 7f, 20f, 5f }, 4.5f, 1.5f),
                LevelAsset("LevelConfig_Level3", 4.6f, 2.6f, 9f, new[] { 5f, 20f, 5f, 20f, 5f }, 3f, 1.2f)
            };

            a.Blinky = GhostConfigAsset("GhostConfig_Blinky", "Blinky", new Color(1f, 0.12f, 0.1f), ChaseRule.Blinky, MazeCorner.TopRight, false, 0f, 0, 0f, null);
            a.Pinky = GhostConfigAsset("GhostConfig_Pinky", "Pinky", new Color(1f, 0.45f, 0.8f), ChaseRule.Pinky, MazeCorner.TopLeft, true, 1f, 4, 0f, null);
            a.Inky = GhostConfigAsset("GhostConfig_Inky", "Inky", new Color(0.15f, 0.9f, 1f), ChaseRule.Inky, MazeCorner.BottomRight, true, 4f, 2, 0f, a.Blinky);
            a.Clyde = GhostConfigAsset("GhostConfig_Clyde", "Clyde", new Color(1f, 0.55f, 0.1f), ChaseRule.Clyde, MazeCorner.BottomLeft, true, 8f, 0, 8f, null);

            a.FlareCollected = ChannelAsset<VoidEventChannel>("FlareCollected",
                "Raised by pickups (Dev A) when the player collects a flare. Heard by GhostDirector (all ghosts frightened), lighting, audio, score.");
            a.PlayerCaught = ChannelAsset<VoidEventChannel>("PlayerCaught",
                "Raised by GhostDirector when a ghost catches the player. Heard by GameManager, lives UI, lighting, audio.");
            a.GameStateChanged = ChannelAsset<GameStateEventChannel>("GameStateChanged",
                "Raised by GameManager (Dev C). Heard by GhostDirector, UI, audio, player input.");
            a.LevelStarted = ChannelAsset<LevelConfigEventChannel>("LevelStarted",
                "Raised by GameManager with the level's LevelConfig, before GameStateChanged(Ready). Heard by GhostDirector. Proposed new channel.");
            a.ModeChanged = ChannelAsset<GhostModeEventChannel>("ModeChanged",
                "Raised by GhostDirector's mode timer. Heard by every Ghost and by audio (siren).");
            a.GhostBanished = ChannelAsset<GhostBanishedEventChannel>("GhostBanished",
                "Raised by GhostDirector when the player touches a frightened ghost. Carries combo and points. Heard by score, audio, VFX.");

            a.GhostSet = LoadOrCreate<GhostRuntimeSet>(RuntimeFolder + "/GhostRuntimeSet.asset");
            a.PlayerAnchor = LoadOrCreate<TransformAnchor>(RuntimeFolder + "/PlayerAnchor.asset");

            a.GhostBody = MaterialAsset(MaterialFolder + "/M_GhostBody_Placeholder.mat", Color.white, Color.white);
            a.GhostEyes = MaterialAsset(MaterialFolder + "/M_GhostEyes_Placeholder.mat", Color.white, Color.white * 2f);

            GameObject basePrefab = GhostBasePrefab(a);
            a.BlinkyPrefab = GhostVariant(basePrefab, "Ghost_Blinky", a.Blinky);
            a.PinkyPrefab = GhostVariant(basePrefab, "Ghost_Pinky", a.Pinky);
            a.InkyPrefab = GhostVariant(basePrefab, "Ghost_Inky", a.Inky);
            a.ClydePrefab = GhostVariant(basePrefab, "Ghost_Clyde", a.Clyde);
            return a;
        }

        static LevelConfig LevelAsset(string file, float speed, float frightenedSpeed, float eatenSpeed,
            float[] scatterChase, float frightenedSeconds, float warningSeconds)
        {
            return LoadOrCreate<LevelConfig>($"{ConfigFolder}/{file}.asset", so =>
            {
                so.FindProperty("ghostSpeed").floatValue = speed;
                so.FindProperty("frightenedSpeed").floatValue = frightenedSpeed;
                so.FindProperty("eatenSpeed").floatValue = eatenSpeed;
                so.FindProperty("frightenedSeconds").floatValue = frightenedSeconds;
                so.FindProperty("frightenedWarningSeconds").floatValue = warningSeconds;

                SerializedProperty phases = so.FindProperty("scatterChaseSeconds");
                phases.arraySize = scatterChase.Length;
                for (int i = 0; i < scatterChase.Length; i++)
                    phases.GetArrayElementAtIndex(i).floatValue = scatterChase[i];
            });
        }

        static GhostConfig GhostConfigAsset(string file, string displayName, Color color, ChaseRule rule, MazeCorner corner,
            bool startsInHouse, float releaseDelay, int aheadTiles, float shyTiles, GhostConfig partner)
        {
            return LoadOrCreate<GhostConfig>($"{ConfigFolder}/{file}.asset", so =>
            {
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("color").colorValue = color;
                so.FindProperty("chaseRule").enumValueIndex = (int)rule;
                so.FindProperty("scatterCorner").enumValueIndex = (int)corner;
                so.FindProperty("startsInHouse").boolValue = startsInHouse;
                so.FindProperty("releaseDelay").floatValue = releaseDelay;
                so.FindProperty("aheadTiles").intValue = aheadTiles;
                so.FindProperty("shyDistanceTiles").floatValue = shyTiles;
                so.FindProperty("partner").objectReferenceValue = partner;
            });
        }

        static T ChannelAsset<T>(string file, string description) where T : EventChannelBase
        {
            return LoadOrCreate<T>($"{EventFolder}/{file}.asset", so => so.FindProperty("description").stringValue = description);
        }

        static GameObject GhostBasePrefab(GhostAssets a)
        {
            string path = PrefabFolder + "/Ghost_Base.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            var root = new GameObject("Ghost_Base");

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 1.8f;
            agent.speed = 3.8f;
            agent.acceleration = 40f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.enabled = false;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            trigger.center = new Vector3(0f, 0.9f, 0f);

            var ghost = root.AddComponent<Ghost>();
            Edit(ghost, so =>
            {
                so.FindProperty("runtimeSet").objectReferenceValue = a.GhostSet;
                so.FindProperty("modeChanged").objectReferenceValue = a.ModeChanged;
            });

            Renderer bodyRenderer = Primitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.9f, a.GhostBody);
            Primitive(PrimitiveType.Sphere, "Eye_L", root.transform, new Vector3(-0.17f, 1.3f, 0.36f), Vector3.one * 0.18f, a.GhostEyes)
                .shadowCastingMode = ShadowCastingMode.Off;
            Primitive(PrimitiveType.Sphere, "Eye_R", root.transform, new Vector3(0.17f, 1.3f, 0.36f), Vector3.one * 0.18f, a.GhostEyes)
                .shadowCastingMode = ShadowCastingMode.Off;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1f, 0f);
            glow.type = LightType.Point;
            glow.range = 4.5f;
            glow.intensity = 1.5f;
            glow.shadows = LightShadows.None;

            var visuals = root.AddComponent<GhostVisuals>();
            Edit(visuals, so =>
            {
                so.FindProperty("ghost").objectReferenceValue = ghost;
                so.FindProperty("body").objectReferenceValue = bodyRenderer;
                so.FindProperty("glow").objectReferenceValue = glow;
            });

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Saving an instance of the base prefab makes a Prefab Variant: one model swap on the base updates all four.
        static GameObject GhostVariant(GameObject basePrefab, string name, GhostConfig config)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = name;
            Edit(instance.GetComponent<Ghost>(), so => so.FindProperty("config").objectReferenceValue = config);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static void BuildSandbox()
        {
            // Open the new scene first: NewScene unloads assets nothing is holding, which would
            // leave references to freshly loaded assets pointing at destroyed objects.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GhostAssets a = CreateGhostAssets();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.035f);
            RenderSettings.skybox = null;

            Material floorMaterial = MaterialAsset(SandboxMaterialFolder + "/M_SandboxFloor.mat", new Color(0.06f, 0.06f, 0.07f), Color.black);
            Material wallMaterial = MaterialAsset(SandboxMaterialFolder + "/M_SandboxWall.mat", new Color(0.18f, 0.19f, 0.25f), Color.black);
            Material doorMaterial = MaterialAsset(SandboxMaterialFolder + "/M_SandboxDoor.mat", new Color(1f, 0.5f, 0.8f), new Color(0.6f, 0.2f, 0.45f));
            Material flareMaterial = MaterialAsset(SandboxMaterialFolder + "/M_SandboxFlare.mat", new Color(1f, 0.55f, 0.2f), new Color(3f, 1.4f, 0.4f));

            int rows = Map.Length;
            int cols = Map[0].Length;

            // Maze geometry, with the NavMesh baked from its colliders.
            var maze = new GameObject("Maze (test only)");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(maze.transform);
            floor.transform.position = new Vector3((cols - 1) * TileSize * 0.5f, -0.1f, (rows - 1) * TileSize * 0.5f);
            floor.transform.localScale = new Vector3(cols * TileSize, 0.2f, rows * TileSize);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var walls = new GameObject("Walls");
            walls.transform.SetParent(maze.transform);
            var notWalkable = walls.AddComponent<NavMeshModifier>();
            notWalkable.overrideArea = true;
            notWalkable.area = 1; // Not Walkable, so the NavMesh never covers the wall tops
            notWalkable.applyToChildren = true;

            for (int row = 0; row < rows; row++)
            {
                // One cube per horizontal run of wall tiles keeps the object count down.
                int col = 0;
                while (col < cols)
                {
                    if (Map[row][col] != '#')
                    {
                        col++;
                        continue;
                    }

                    int start = col;
                    while (col < cols && Map[row][col] == '#')
                        col++;

                    int length = col - start;
                    Vector3 center = (Tile(row, start) + Tile(row, col - 1)) * 0.5f + Vector3.up * (WallHeight * 0.5f);
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = $"Wall {row},{start}";
                    wall.transform.SetParent(walls.transform);
                    wall.transform.position = center;
                    wall.transform.localScale = new Vector3(length * TileSize, WallHeight, TileSize);
                    wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                }
            }

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "House Door (blocks player only)";
            door.layer = DoorLayer;
            door.transform.SetParent(maze.transform);
            door.transform.position = Tile(Find('-')) + Vector3.up * (WallHeight * 0.5f);
            door.transform.localScale = new Vector3(TileSize, WallHeight, 0.3f);
            door.GetComponent<Renderer>().sharedMaterial = doorMaterial;

            var surface = maze.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << DoorLayer);
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(SandboxNavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, SandboxNavMeshPath);

            // Maze points for the ghosts. Scatter corners are the corridor tiles nearest each corner.
            var layoutObject = new GameObject("Ghost Maze Layout");
            var layout = layoutObject.AddComponent<GhostMazeLayout>();
            Vector2Int[] house = FindAll('H');
            layout.Configure(TileSize,
                Marker("House Exit", Tile(Find('G')), layoutObject.transform),
                Marker("House Inside", Tile(house[house.Length / 2]), layoutObject.transform),
                Marker("Corner Top Left", Tile(1, 1), layoutObject.transform),
                Marker("Corner Top Right", Tile(1, cols - 2), layoutObject.transform),
                Marker("Corner Bottom Left", Tile(rows - 2, 1), layoutObject.transform),
                Marker("Corner Bottom Right", Tile(rows - 2, cols - 2), layoutObject.transform));

            var director = new GameObject("Ghost Director").AddComponent<GhostDirector>();
            Edit(director, so =>
            {
                so.FindProperty("level").objectReferenceValue = a.Levels[0];
                so.FindProperty("ai").objectReferenceValue = a.Ai;
                so.FindProperty("ghosts").objectReferenceValue = a.GhostSet;
                so.FindProperty("layout").objectReferenceValue = layout;
                so.FindProperty("player").objectReferenceValue = a.PlayerAnchor;
                so.FindProperty("gameStateChanged").objectReferenceValue = a.GameStateChanged;
                so.FindProperty("levelStarted").objectReferenceValue = a.LevelStarted;
                so.FindProperty("flareCollected").objectReferenceValue = a.FlareCollected;
                so.FindProperty("modeChanged").objectReferenceValue = a.ModeChanged;
                so.FindProperty("playerCaught").objectReferenceValue = a.PlayerCaught;
                so.FindProperty("ghostBanished").objectReferenceValue = a.GhostBanished;
            });

            var ghostsParent = new GameObject("Ghosts").transform;
            SpawnGhost(a.BlinkyPrefab, Tile(Find('G')), ghostsParent);
            SpawnGhost(a.PinkyPrefab, Tile(house[1]), ghostsParent);
            SpawnGhost(a.InkyPrefab, Tile(house[0]), ghostsParent);
            SpawnGhost(a.ClydePrefab, Tile(house[2]), ghostsParent);

            SandboxPlayer player = CreatePlayer(a, Tile(Find('P')));

            foreach (Vector2Int flare in FindAll('o'))
                CreateFlare(a, Tile(flare), flareMaterial);

            var overview = new GameObject("Overview Camera (Tab)").AddComponent<Camera>();
            overview.orthographic = true;
            overview.orthographicSize = rows * TileSize * 0.5f + 1f;
            overview.transform.SetPositionAndRotation(
                new Vector3((cols - 1) * TileSize * 0.5f, 60f, (rows - 1) * TileSize * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            overview.depth = 10f;
            overview.clearFlags = CameraClearFlags.SolidColor;
            overview.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            overview.enabled = false;

            var flow = new GameObject("Game Flow (stand-in for GameManager)").AddComponent<StandaloneGameFlow>();
            Edit(flow, so =>
            {
                SerializedProperty levels = so.FindProperty("levels");
                levels.arraySize = a.Levels.Length;
                for (int i = 0; i < a.Levels.Length; i++)
                    levels.GetArrayElementAtIndex(i).objectReferenceValue = a.Levels[i];

                so.FindProperty("player").objectReferenceValue = player;
                so.FindProperty("ghosts").objectReferenceValue = a.GhostSet;
                so.FindProperty("overviewCamera").objectReferenceValue = overview;
                so.FindProperty("levelStarted").objectReferenceValue = a.LevelStarted;
                so.FindProperty("gameStateChanged").objectReferenceValue = a.GameStateChanged;
                so.FindProperty("flareCollected").objectReferenceValue = a.FlareCollected;
                so.FindProperty("playerCaught").objectReferenceValue = a.PlayerCaught;
                so.FindProperty("ghostBanished").objectReferenceValue = a.GhostBanished;
                so.FindProperty("modeChanged").objectReferenceValue = a.ModeChanged;
            });

            EditorSceneManager.SaveScene(scene, SandboxScenePath);
        }

        static SandboxPlayer CreatePlayer(GhostAssets a, Vector3 position)
        {
            var root = new GameObject("Player (test only)") { tag = "Player" };
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            Edit(root.AddComponent<TransformAnchorBinder>(), so => so.FindProperty("anchor").objectReferenceValue = a.PlayerAnchor);

            var head = new GameObject("Head") { tag = "MainCamera" };
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = head.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            head.AddComponent<AudioListener>();

            var flashlight = new GameObject("Flashlight").AddComponent<Light>();
            flashlight.transform.SetParent(head.transform, false);
            flashlight.transform.localPosition = new Vector3(0f, -0.05f, 0.1f);
            flashlight.type = LightType.Spot;
            flashlight.range = 18f;
            flashlight.spotAngle = 55f;
            flashlight.innerSpotAngle = 30f;
            flashlight.intensity = 6f;
            flashlight.color = new Color(1f, 0.95f, 0.85f);
            flashlight.shadows = LightShadows.Soft;

            Edit(flashlight.gameObject.AddComponent<FlashlightBeamSensor>(), so =>
            {
                so.FindProperty("ghosts").objectReferenceValue = a.GhostSet;
                so.FindProperty("ai").objectReferenceValue = a.Ai;
            });

            var player = root.AddComponent<SandboxPlayer>();
            Edit(player, so =>
            {
                so.FindProperty("head").objectReferenceValue = head.transform;
                so.FindProperty("flashlight").objectReferenceValue = flashlight;
            });
            return player;
        }

        static void CreateFlare(GhostAssets a, Vector3 position, Material material)
        {
            var flare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flare.name = "Flare (test only)";
            flare.transform.position = position + Vector3.up * 0.8f;
            flare.transform.localScale = Vector3.one * 0.4f;
            flare.GetComponent<Renderer>().sharedMaterial = material;

            var trigger = flare.GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2f; // 0.8 m in the world, after the 0.4 scale

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(flare.transform, false);
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.shadows = LightShadows.None;

            Edit(flare.AddComponent<SandboxFlarePickup>(), so =>
            {
                so.FindProperty("flareCollected").objectReferenceValue = a.FlareCollected;
                so.FindProperty("levelStarted").objectReferenceValue = a.LevelStarted;
            });
        }

        static void SpawnGhost(GameObject prefab, Vector3 position, Transform parent)
        {
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ghost.transform.SetParent(parent);
            ghost.transform.position = position;
        }

        static Transform Marker(string name, Vector3 position, Transform parent)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent);
            marker.position = position;
            return marker;
        }

        static Renderer Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            // Visual only: the ghost's single trigger handles contact, and the flashlight ray must not hit the body.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        // Row 0 is the top of the map (+Z); column 0 is the left (-X).
        static Vector3 Tile(int row, int col) => new(col * TileSize, 0f, (Map.Length - 1 - row) * TileSize);
        static Vector3 Tile(Vector2Int tile) => Tile(tile.x, tile.y);

        static Vector2Int Find(char symbol) => FindAll(symbol)[0];

        static Vector2Int[] FindAll(char symbol)
        {
            var found = new System.Collections.Generic.List<Vector2Int>();
            for (int row = 0; row < Map.Length; row++)
            {
                for (int col = 0; col < Map[row].Length; col++)
                {
                    if (Map[row][col] == symbol)
                        found.Add(new Vector2Int(row, col));
                }
            }

            if (found.Count == 0)
                throw new InvalidOperationException($"The sandbox map has no '{symbol}'.");
            return found.ToArray();
        }

        static Material MaterialAsset(string path, Color baseColor, Color emission)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", baseColor);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T LoadOrCreate<T>(string path, Action<SerializedObject> setup = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            if (setup != null)
                Edit(asset, setup);
            return asset;
        }

        static void Edit(Object target, Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
