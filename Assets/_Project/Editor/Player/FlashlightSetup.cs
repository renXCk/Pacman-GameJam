using System.IO;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using Game.Lighting;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Game > Player > Add Flashlight To Game Scene
    // Adds the flashlight's spot light with Dev D's FlashlightController to Game.unity and the
    // battery and switch (PlayerFlashlight) to the Player. Safe to run twice. Needs the player first
    // (Game > Player > Add Player To Game Scene).
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Player).
    public static class FlashlightSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string GameScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Game/Player/Add Flashlight To Game Scene")]
        public static void AddToGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Open the scene first, then load the assets (see PlayerSetup for why).
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
                throw new System.InvalidOperationException("Game.unity has no player yet. Run Game > Player > Add Player To Game Scene first.");

            var playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera == null)
                throw new System.InvalidOperationException("The Player has no camera under it. Run Game > Player > Add Player To Game Scene again.");

            var playerConfig = Load<PlayerConfig>(ConfigFolder + "/PlayerConfig.asset");
            var anchor = Load<TransformAnchor>(RuntimeFolder + "/PlayerAnchor.asset");
            var ghostSet = Load<GhostRuntimeSet>(RuntimeFolder + "/GhostRuntimeSet.asset");
            var gameStateChanged = Load<GameStateEventChannel>(EventFolder + "/GameStateChanged.asset");
            var newGameStarted = Load<VoidEventChannel>(EventFolder + "/NewGameStarted.asset");
            var batteryChanged = Load<FloatEventChannel>(EventFolder + "/BatteryChanged.asset");

            // Dev D's controller follows the camera from outside it (with a little lag), so the
            // flashlight is its own scene object and not a child of the camera.
            var controller = Object.FindAnyObjectByType<FlashlightController>();
            if (controller == null)
            {
                var lightObject = new GameObject("Flashlight");
                lightObject.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);

                var spot = lightObject.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = new Color(1f, 0.96f, 0.88f);
                spot.intensity = 80f;
                spot.range = 45f;
                spot.spotAngle = 65f;
                spot.innerSpotAngle = 35.75f;
                spot.shadows = LightShadows.Soft;

                controller = lightObject.AddComponent<FlashlightController>();
            }

            Edit(controller, so =>
            {
                Set(so, "cameraTransform", playerCamera.transform);
                Set(so, "playerAnchor", anchor);
                Set(so, "batteryChanged", batteryChanged);
                Set(so, "ghostSet", ghostSet);
            });

            var flashlight = player.GetComponent<PlayerFlashlight>();
            if (flashlight == null)
                flashlight = player.gameObject.AddComponent<PlayerFlashlight>();

            Light spotLight = controller.GetComponent<Light>();
            Edit(flashlight, so =>
            {
                Set(so, "config", playerConfig);
                Set(so, "flashlight", spotLight);
                Set(so, "gameStateChanged", gameStateChanged);
                Set(so, "newGameStarted", newGameStarted);
                Set(so, "batteryChanged", batteryChanged);
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("[Flashlight] Game.unity now has the Flashlight. F or a click toggles it; the battery drains while it is on.");
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
