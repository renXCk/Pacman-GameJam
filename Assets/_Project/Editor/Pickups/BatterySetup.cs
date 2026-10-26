using System.IO;
using Game.Core;
using Game.Events;
using Game.Maze;
using Game.Pickups;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Game > Pickups > Add Battery Rules And Warps
    // Adds the battery spawn rules (BatteryDirector) and the warp doors (MazeWarps) to the maze
    // object in Game.unity and wires them. Safe to run twice. Needs the pickups first
    // (Game > Pickups > Add Pickups To Game Scene).
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Pickups).
    public static class BatterySetup
    {
        const string Root = "Assets/_Project";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string MaterialFolder = Root + "/Art/Materials";
        const string GameScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Game/Pickups/Add Battery Rules And Warps")]
        public static void AddToGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Open the scene first, then load the assets (see PickupSetup for why).
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var pickups = Object.FindAnyObjectByType<MazePickups>();
            if (pickups == null)
                throw new System.InvalidOperationException("Game.unity has no MazePickups yet. Run Game > Pickups > Add Pickups To Game Scene first.");

            var progress = Load<LevelProgress>(RuntimeFolder + "/LevelProgress.asset");
            var levelStarted = Load<LevelConfigEventChannel>(EventFolder + "/LevelStarted.asset");
            var gameStateChanged = Load<GameStateEventChannel>(EventFolder + "/GameStateChanged.asset");
            Material padMaterial = FindOrCreatePadMaterial();

            GameObject maze = pickups.gameObject;

            var director = maze.GetComponent<BatteryDirector>();
            if (director == null)
                director = maze.AddComponent<BatteryDirector>();

            Edit(director, so =>
            {
                Set(so, "pickups", pickups);
                Set(so, "progress", progress);
                Set(so, "levelStarted", levelStarted);
                Set(so, "gameStateChanged", gameStateChanged);
            });

            var warps = maze.GetComponent<MazeWarps>();
            if (warps == null)
                warps = maze.AddComponent<MazeWarps>();

            Edit(warps, so => Set(so, "padMaterial", padMaterial));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = maze;
            Debug.Log("[Pickups] Game.unity now has the battery rules and warp doors. Batteries start hidden and appear as you clear the level.");
        }

        static Material FindOrCreatePadMaterial()
        {
            const string name = "M_WarpPad_Placeholder";
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            EnsureFolder(MaterialFolder);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var color = new Color(0.2f, 0.9f, 1f);
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException($"{so.targetObject.GetType().Name} has no field {field}.");
            property.objectReferenceValue = value;
        }

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
