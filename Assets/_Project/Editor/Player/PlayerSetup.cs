using System.IO;
using Game.Core;
using Game.Events;
using Game.Maze;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Game > Player > Add Player To Game Scene
    // Puts the first-person player into Game.unity: the body, the head and camera, the spawn marker,
    // and the link that moves the marker to the maze's player start. It replaces Dev C's placeholder
    // camera. Safe to run twice. Needs the maze first (Game > Maze > Add Maze To Game Scene).
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Player).
    public static class PlayerSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string GameScenePath = Root + "/Scenes/Game.unity";
        const string PlaceholderCameraName = "Placeholder Camera (delete when the player exists)";

        [MenuItem("Game/Player/Add Player To Game Scene")]
        public static void AddToGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Open the scene first, then load the assets. Opening a scene unloads assets nothing is
            // holding, so an asset loaded earlier would be saved into the scene as a dead reference.
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var builder = Object.FindAnyObjectByType<MazeBuilder>();
            if (builder == null)
                throw new System.InvalidOperationException("Game.unity has no maze yet. Run Game > Maze > Add Maze To Game Scene first.");

            var playerConfig = Load<PlayerConfig>(ConfigFolder + "/PlayerConfig.asset");
            var gameStateChanged = Load<GameStateEventChannel>(EventFolder + "/GameStateChanged.asset");
            var anchor = Load<TransformAnchor>(RuntimeFolder + "/PlayerAnchor.asset");

            Transform spawnMarker = WireSpawnMarker(builder);
            PlayerController player = EnsurePlayer(spawnMarker);

            Edit(player, so =>
            {
                Set(so, "config", playerConfig);
                Set(so, "spawnPoint", spawnMarker);
                Set(so, "gameStateChanged", gameStateChanged);
                Set(so, "head", EnsureHead(player.transform));
            });

            var binder = EnsureComponent<TransformAnchorBinder>(player.gameObject);
            Edit(binder, so => Set(so, "anchor", anchor));

            EnsureCamera(player.transform.Find("Head"));
            RemovePlaceholderCamera();

            // The player starts where the marker is, so the Scene view shows it on the right tile.
            player.transform.SetPositionAndRotation(spawnMarker.position + Vector3.up * 0.05f, Quaternion.Euler(0f, spawnMarker.eulerAngles.y, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = player.gameObject;
            Debug.Log("[Player] Game.unity now has the Player. Press Play: WASD to walk, mouse to look.");
        }

        // The marker the player spawns at. MazePlayerSpawn moves it to the start tile at runtime.
        static Transform WireSpawnMarker(MazeBuilder builder)
        {
            var spawner = EnsureComponent<MazePlayerSpawn>(builder.gameObject);

            Transform marker = builder.transform.Find("Player Start");
            if (marker == null)
            {
                marker = new GameObject("Player Start").transform;
                marker.SetParent(builder.transform, false);
            }

            // Park it on the start tile for the Scene view (the same rule the maze uses at runtime).
            MazeConfig config = builder.Config;
            MazeGrid grid = MazeGrid.Parse(config.MapFile.text);
            TileCoord start = grid.PlayerStart;
            marker.SetPositionAndRotation(
                grid.TileToWorld(start, config.TileSize),
                Quaternion.Euler(0f, MazePlayerSpawn.FacingYaw(grid, start), 0f));

            Edit(spawner, so => Set(so, "spawnPoint", marker));
            return marker;
        }

        static PlayerController EnsurePlayer(Transform spawnMarker)
        {
            var existing = Object.FindAnyObjectByType<PlayerController>();
            if (existing != null)
                return existing;

            var root = new GameObject("Player");
            root.tag = "Player";
            root.transform.SetPositionAndRotation(spawnMarker.position, spawnMarker.rotation);

            // The sizes from the art guide: a 1.8 m, 0.4 m radius capsule with its feet on the floor.
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;
            body.slopeLimit = 45f;
            body.skinWidth = 0.05f;

            return root.AddComponent<PlayerController>();
        }

        static Transform EnsureHead(Transform player)
        {
            Transform head = player.Find("Head");
            if (head == null)
            {
                head = new GameObject("Head").transform;
                head.SetParent(player, false);
            }

            head.localPosition = new Vector3(0f, 1.6f, 0f);
            head.localRotation = Quaternion.identity;
            return head;
        }

        static void EnsureCamera(Transform head)
        {
            var camera = head.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Player Camera");
                cameraObject.transform.SetParent(head, false);
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.gameObject.tag = "MainCamera";
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            // The URP camera settings. Post-processing is on so Dev D's volume affects this camera.
            var data = EnsureComponent<UniversalAdditionalCameraData>(camera.gameObject);
            data.renderPostProcessing = true;
        }

        // Only one camera and one audio listener may exist, so Dev C's stand-in goes.
        static void RemovePlaceholderCamera()
        {
            GameObject placeholder = GameObject.Find(PlaceholderCameraName);
            if (placeholder != null)
                Object.DestroyImmediate(placeholder);
        }

        static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static void Set(SerializedObject so, string field, Object value) =>
            so.FindProperty(field).objectReferenceValue = value;

        static void Edit(Object target, System.Action<SerializedObject> change)
        {
            var so = new SerializedObject(target);
            change(so);
            so.ApplyModifiedPropertiesWithoutUndo();
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
    }
}
