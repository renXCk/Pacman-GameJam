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
    // Game > Pickups > Add Pickups To Game Scene
    // Puts MazePickups on the MazeBuilder object in Game.unity and wires it to the three pickup
    // configs, the emissive materials, LevelProgress and the event channels. Safe to run twice.
    // Needs the maze first (Game > Maze > Add Maze To Game Scene).
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Pickups).
    public static class PickupSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/ScriptableObjects/Configs";
        const string EventFolder = Root + "/ScriptableObjects/Events";
        const string RuntimeFolder = Root + "/ScriptableObjects/Runtime";
        const string MaterialFolder = Root + "/Art/Materials";
        const string GameScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Game/Pickups/Add Pickups To Game Scene")]
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

            var cellConfig = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Cell.asset");
            var flareConfig = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Flare.asset");
            var batteryConfig = Load<PickupConfig>(ConfigFolder + "/PickupConfig_Battery.asset");
            var progress = Load<LevelProgress>(RuntimeFolder + "/LevelProgress.asset");
            var levelStarted = Load<LevelConfigEventChannel>(EventFolder + "/LevelStarted.asset");
            var pickupCollected = Load<PickupEventChannel>(EventFolder + "/PickupCollected.asset");
            var flareCollected = Load<VoidEventChannel>(EventFolder + "/FlareCollected.asset");

            EnsureFolder(MaterialFolder);
            Material cellMaterial = FindOrCreateMaterial(new[] { "M_PowerCell_Emissive", "M_PowerCell" }, "M_PowerCell_Placeholder", new Color(1f, 0.85f, 0.2f), 2f);
            Material flareMaterial = FindOrCreateMaterial(new[] { "M_Flare_Emissive", "M_Flare" }, "M_Flare_Placeholder", new Color(1f, 0.25f, 0.2f), 3f);
            Material batteryMaterial = FindOrCreateMaterial(new[] { "M_Battery_Emissive", "M_Battery" }, "M_Battery_Placeholder", new Color(0.2f, 1f, 0.55f), 2.5f);

            var pickups = builder.GetComponent<MazePickups>();
            if (pickups == null)
                pickups = builder.gameObject.AddComponent<MazePickups>();

            Edit(pickups, so =>
            {
                Set(so, "cellConfig", cellConfig);
                Set(so, "flareConfig", flareConfig);
                Set(so, "batteryConfig", batteryConfig);
                Set(so, "cellMaterial", cellMaterial);
                Set(so, "flareMaterial", flareMaterial);
                Set(so, "batteryMaterial", batteryMaterial);
                Set(so, "progress", progress);
                Set(so, "levelStarted", levelStarted);
                Set(so, "pickupCollected", pickupCollected);
                Set(so, "flareCollected", flareCollected);
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = builder.gameObject;
            Debug.Log("[Pickups] Game.unity now has MazePickups on the maze. Press Play: power cells, flares and batteries fill the maze.");
        }

        // Dev D's emissive material if it exists (by name), otherwise a plain glowing placeholder.
        static Material FindOrCreateMaterial(string[] preferredNames, string placeholderName, Color color, float glow)
        {
            for (int i = 0; i < preferredNames.Length; i++)
            {
                foreach (string guid in AssetDatabase.FindAssets(preferredNames[i] + " t:Material"))
                {
                    var found = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (found != null && found.name == preferredNames[i])
                        return found;
                }
            }

            string path = $"{MaterialFolder}/{placeholderName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader) { name = placeholderName };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.3f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * glow);
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
                throw new System.InvalidOperationException($"MazePickups has no field {field}.");
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
