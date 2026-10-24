using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    // Game > Systems > Validate Setup. Opens the game's scenes and prefabs and logs every Game.*
    // component field that points at nothing (an unwired channel or config) and every missing
    // script. An unassigned channel fails silently at runtime, so this is the place to catch it.
    static class SystemsValidator
    {
        const string PrefabFolder = "Assets/_Project/Prefabs";

        // Fields that are empty on purpose. The ghost base prefab has no GhostConfig; each variant sets its own.
        static readonly HashSet<string> EmptyByDesign = new()
        {
            "Assets/_Project/Prefabs/Ghosts/Ghost_Base.prefab: Ghost_Base Ghost.config"
        };

        // Returns the number of problems found.
        public static int Run()
        {
            var problems = new List<string>();

            foreach (string path in new[] { SystemsSetup.MainMenuScenePath, SystemsSetup.GameScenePath })
                CheckScene(path, problems);

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CheckObject(path, prefab, problems);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }

            foreach (string problem in problems)
                Debug.LogWarning($"[Validate] {problem}");

            Debug.Log(problems.Count == 0
                ? "[Validate] Setup OK: every Game component in the scenes and prefabs is wired."
                : $"[Validate] {problems.Count} problem(s) found, see the warnings above.");
            return problems.Count;
        }

        static void CheckScene(string path, List<string> problems)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                problems.Add($"{path} does not exist. Run Game > Systems > Build ... first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
                CheckObject(path, root, problems);

            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }

        static void CheckObject(string context, GameObject root, List<string> problems)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                    problems.Add($"{context}: {PathOf(child)} has a missing script.");

                foreach (MonoBehaviour component in go.GetComponents<MonoBehaviour>())
                {
                    if (component == null || !IsGameType(component))
                        continue;

                    foreach (string field in UnassignedFields(component))
                    {
                        string where = $"{context}: {PathOf(child)} {component.GetType().Name}.{field}";
                        if (!EmptyByDesign.Contains(where))
                            problems.Add($"{where} is not assigned.");
                    }
                }
            }
        }

        static IEnumerable<string> UnassignedFields(Object component)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                    continue;

                if (property.objectReferenceValue == null)
                    yield return property.propertyPath;
            }
        }

        static bool IsGameType(Object component)
        {
            string ns = component.GetType().Namespace;
            return ns != null && (ns == "Game" || ns.StartsWith("Game."));
        }

        static string PathOf(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform t = transform; t != null; t = t.parent)
                names.Push(t.name);
            return string.Join("/", names.ToArray());
        }
    }
}
