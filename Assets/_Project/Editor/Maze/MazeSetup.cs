using System.IO;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using Game.Maze;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Dev A's setup menu.
    //   Game > Maze > Create Maze Assets       : the MazeConfig asset and placeholder materials. Never overwrites.
    //   Game > Maze > Add Maze To Game Scene   : puts the maze, its NavMesh bake, the ghost layout, the
    //                                            ghost spawner and a GhostDirector into Game.unity. Safe to run twice.
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Maze).
    public static class MazeSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string MaterialFolder = Root + "/Art/Materials";
        const string MapPath = Root + "/Data/Mazes/Maze_Level1.txt";
        const string ConfigPath = ConfigFolder + "/MazeConfig.asset";
        const string GameScenePath = Root + "/Scenes/Game.unity";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string GhostPrefabFolder = Root + "/Prefabs/Ghosts";

        // Dev C's stand-ins. The maze takes the empty marker's place and the camera is pointed at it.
        const string MazeMarkerName = "--- Maze goes here ---";
        const string PlaceholderCameraName = "Placeholder Camera (delete when the player exists)";

        [MenuItem("Game/Maze/Create Maze Assets")]
        public static void CreateAssetsMenu()
        {
            CreateAssets();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Game/Maze/Add Maze To Game Scene")]
        public static void AddToGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Open the scene first, then load the assets. Opening a scene unloads assets nothing is
            // holding, so a config loaded earlier would be saved into the scene as a dead reference.
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            MazeConfig config = CreateAssets();

            var builder = Object.FindAnyObjectByType<MazeBuilder>();
            if (builder == null)
            {
                GameObject root = GameObject.Find(MazeMarkerName);
                if (root == null)
                    root = new GameObject();

                root.name = "Maze";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                builder = root.AddComponent<MazeBuilder>();
            }

            Edit(builder, so => so.FindProperty("config").objectReferenceValue = config);
            if (new SerializedObject(builder).FindProperty("config").objectReferenceValue == null)
                throw new System.InvalidOperationException("The MazeBuilder did not keep its MazeConfig. Assign it by hand in the Inspector.");

            // The listeners run in the order they sit on the object: NavMesh first, then ghosts.
            EnsureComponent<MazeNavMesh>(builder.gameObject);
            var ghosts = EnsureComponent<MazeGhosts>(builder.gameObject);
            GhostMazeLayout layout = WireGhostLayout(builder, ghosts, config);
            WireGhostDirector(layout);
            PointPlaceholderCameraAtMaze(config);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = builder.gameObject;
            Debug.Log("[Maze] Game.unity now has the Maze (builder, NavMesh, ghosts) and a Ghost Director. Press Play to see it.");
        }

        // Returns the config, creating it and its materials when they are missing.
        public static MazeConfig CreateAssets()
        {
            EnsureFolder(ConfigFolder);
            EnsureFolder(MaterialFolder);

            var map = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath);
            if (map == null)
                throw new FileNotFoundException($"Missing {MapPath}. Add the A1 files first.");

            Material wall = PlaceholderMaterial("M_MazeWall_Placeholder", new Color(0.35f, 0.38f, 0.5f));
            Material floor = PlaceholderMaterial("M_MazeFloor_Placeholder", new Color(0.12f, 0.12f, 0.14f));
            Material door = PlaceholderMaterial("M_MazeDoor_Placeholder", new Color(1f, 0.5f, 0.8f));

            var config = AssetDatabase.LoadAssetAtPath<MazeConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MazeConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // Only fill what is empty, so a hand-assigned material or a different map is never replaced.
            Edit(config, so =>
            {
                FillIfEmpty(so, "mapFile", map);
                FillIfEmpty(so, "wallMaterial", wall);
                FillIfEmpty(so, "floorMaterial", floor);
                FillIfEmpty(so, "doorMaterial", door);

                // Blinky, Pinky, Inky, Clyde. Only filled when the list is empty.
                SerializedProperty prefabs = so.FindProperty("ghostPrefabs");
                if (prefabs.arraySize == 0)
                {
                    string[] names = { "Blinky", "Pinky", "Inky", "Clyde" };
                    prefabs.arraySize = names.Length;
                    for (int i = 0; i < names.Length; i++)
                    {
                        prefabs.GetArrayElementAtIndex(i).objectReferenceValue =
                            Load<GameObject>($"{GhostPrefabFolder}/Ghost_{names[i]}.prefab");
                    }
                }
            });

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        // The ghost layout needs its six marker objects assigned in the scene (Dev C's validator flags
        // empty fields). The markers are created here, parked on the right tiles for the Scene view,
        // and MazeGhosts moves them again at runtime. An existing layout and markers are reused.
        static GhostMazeLayout WireGhostLayout(MazeBuilder builder, MazeGhosts ghosts, MazeConfig config)
        {
            var layout = Object.FindAnyObjectByType<GhostMazeLayout>();
            if (layout == null)
            {
                var layoutObject = new GameObject("Ghost Maze Layout");
                layoutObject.transform.SetParent(builder.transform, false);
                layout = layoutObject.AddComponent<GhostMazeLayout>();
            }

            MazeGrid grid = MazeGrid.Parse(config.MapFile.text);
            float size = config.TileSize;

            var layoutSo = new SerializedObject(layout);
            layoutSo.FindProperty("tileSize").floatValue = size;
            Transform exit = Marker(layoutSo, "houseExit", "House Exit", layout.transform, grid.TileToWorld(grid.GhostExit, size));
            Transform inside = Marker(layoutSo, "houseInside", "House Inside", layout.transform, grid.TileToWorld(MazeGhosts.HouseInsideTile(grid), size));
            Transform tl = Marker(layoutSo, "topLeft", "Corner Top Left", layout.transform, grid.TileToWorld(grid.CornerTile(MazeCorner.TopLeft), size));
            Transform tr = Marker(layoutSo, "topRight", "Corner Top Right", layout.transform, grid.TileToWorld(grid.CornerTile(MazeCorner.TopRight), size));
            Transform bl = Marker(layoutSo, "bottomLeft", "Corner Bottom Left", layout.transform, grid.TileToWorld(grid.CornerTile(MazeCorner.BottomLeft), size));
            Transform br = Marker(layoutSo, "bottomRight", "Corner Bottom Right", layout.transform, grid.TileToWorld(grid.CornerTile(MazeCorner.BottomRight), size));
            layoutSo.ApplyModifiedPropertiesWithoutUndo();

            Edit(ghosts, so =>
            {
                so.FindProperty("layout").objectReferenceValue = layout;
                so.FindProperty("houseExit").objectReferenceValue = exit;
                so.FindProperty("houseInside").objectReferenceValue = inside;
                so.FindProperty("topLeft").objectReferenceValue = tl;
                so.FindProperty("topRight").objectReferenceValue = tr;
                so.FindProperty("bottomLeft").objectReferenceValue = bl;
                so.FindProperty("bottomRight").objectReferenceValue = br;
            });

            return layout;
        }

        // Returns the layout's marker for one field, creating and assigning it when the field is empty.
        static Transform Marker(SerializedObject layoutSo, string field, string markerName, Transform parent, Vector3 position)
        {
            SerializedProperty property = layoutSo.FindProperty(field);
            var marker = property.objectReferenceValue as Transform;
            if (marker == null)
            {
                marker = new GameObject(markerName).transform;
                marker.SetParent(parent, false);
                marker.position = position;
                property.objectReferenceValue = marker;
            }

            return marker;
        }

        // The GhostDirector runs the ghosts. It is wired the same way Dev B's sandbox wires it.
        // Anything already assigned is left alone.
        static void WireGhostDirector(GhostMazeLayout layout)
        {
            var director = Object.FindAnyObjectByType<GhostDirector>();
            if (director == null)
                director = new GameObject("Ghost Director").AddComponent<GhostDirector>();

            Edit(director, so =>
            {
                FillIfEmpty(so, "level", Load<LevelConfig>(ConfigFolder + "/LevelConfig_Level1.asset"));
                FillIfEmpty(so, "ai", Load<GhostAIConfig>(ConfigFolder + "/GhostAIConfig.asset"));
                FillIfEmpty(so, "ghosts", Load<GhostRuntimeSet>(RuntimeFolder + "/GhostRuntimeSet.asset"));
                FillIfEmpty(so, "layout", layout);
                FillIfEmpty(so, "player", Load<TransformAnchor>(RuntimeFolder + "/PlayerAnchor.asset"));
                FillIfEmpty(so, "gameStateChanged", Load<GameStateEventChannel>(EventFolder + "/GameStateChanged.asset"));
                FillIfEmpty(so, "levelStarted", Load<LevelConfigEventChannel>(EventFolder + "/LevelStarted.asset"));
                FillIfEmpty(so, "flareCollected", Load<VoidEventChannel>(EventFolder + "/FlareCollected.asset"));
                FillIfEmpty(so, "modeChanged", Load<GhostModeEventChannel>(EventFolder + "/ModeChanged.asset"));
                FillIfEmpty(so, "playerCaught", Load<VoidEventChannel>(EventFolder + "/PlayerCaught.asset"));
                FillIfEmpty(so, "ghostBanished", Load<GhostBanishedEventChannel>(EventFolder + "/GhostBanished.asset"));
            });
        }

        static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        // Loads an asset by its usual path, or finds it by name if it was moved.
        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            string name = Path.GetFileNameWithoutExtension(path);
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}"))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == name)
                    return asset;
            }

            throw new FileNotFoundException($"Could not find {typeof(T).Name} \"{name}\" (expected at {path}).");
        }

        static void PointPlaceholderCameraAtMaze(MazeConfig config)
        {
            GameObject cameraObject = GameObject.Find(PlaceholderCameraName);
            if (cameraObject == null || config.MapFile == null)
                return;

            var camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
                return;

            MazeGrid grid = MazeGrid.Parse(config.MapFile.text);
            float size = config.TileSize;

            // Straight down from above the middle of the map, so Play shows the whole maze.
            cameraObject.transform.SetPositionAndRotation(
                new Vector3((grid.Cols - 1) * size * 0.5f, 60f, (grid.Rows - 1) * size * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = grid.Rows * size * 0.5f + 2f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 100f);
        }

        // An URP Lit material that glows a little, so the maze is visible before any lights exist.
        // Dev D's materials replace these later: drag them into the MazeConfig asset.
        static Material PlaceholderMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.8f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void FillIfEmpty(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property.objectReferenceValue == null)
                property.objectReferenceValue = value;
        }

        static void Edit(Object target, System.Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
