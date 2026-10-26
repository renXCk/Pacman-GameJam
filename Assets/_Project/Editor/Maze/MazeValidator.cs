using System.Collections.Generic;
using Game.Core;
using Game.Maze;
using Game.Pickups;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    // Game > Maze > Validate Maze Setup
    // Task A8: checks Dev A's side of the project in one go: every maze map (parse rules plus the
    // design rules in MazeChecks), the MazeConfig assets, and Game.unity's maze, pickups and player
    // wiring. It changes nothing. Errors mean something is broken; warnings are worth a look.
    // Lives in the Game.Editor assembly (Assets/_Project/Editor/Maze).
    public static class MazeValidator
    {
        const string Root = "Assets/_Project";
        const string MapFolder = Root + "/Data/Mazes";
        const string GameScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Game/Maze/Validate Maze Setup")]
        public static void RunFromMenu()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Run(errors, warnings);

            // Warnings, not errors: a Debug.LogError would also fail any test that runs this.
            foreach (string error in errors)
                Debug.LogWarning($"[MazeValidator] ERROR: {error}");
            foreach (string warning in warnings)
                Debug.LogWarning($"[MazeValidator] warning: {warning}");

            Debug.Log(errors.Count == 0 && warnings.Count == 0
                ? "[MazeValidator] Maze, pickups and player setup OK."
                : $"[MazeValidator] {errors.Count} error(s) and {warnings.Count} warning(s) found, see the warnings above.");
        }

        // Runs every check and returns the number of errors.
        public static int Run(List<string> errors, List<string> warnings)
        {
            CheckMaps(errors, warnings);
            CheckMazeConfigs(errors, warnings);
            CheckGameScene(errors, warnings);
            return errors.Count;
        }

        // ---- maps -------------------------------------------------------------------------

        static void CheckMaps(List<string> errors, List<string> warnings)
        {
            if (!AssetDatabase.IsValidFolder(MapFolder))
            {
                errors.Add($"The maze map folder {MapFolder} does not exist.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { MapFolder });
            if (guids.Length == 0)
                errors.Add($"There are no maze maps in {MapFolder}.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var map = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (map != null)
                    CheckMapText(map.name, map.text, errors, warnings);
            }
        }

        // One map: it must parse, and then it gets the design rules. Problems are prefixed with the name.
        public static void CheckMapText(string name, string text, List<string> errors, List<string> warnings)
        {
            if (!MazeGrid.TryParse(text, out MazeGrid grid, out List<string> parseErrors))
            {
                foreach (string error in parseErrors)
                    errors.Add($"{name}: {error}");
                return;
            }

            MazeReport report = MazeChecks.Check(grid);
            foreach (string error in report.Errors)
                errors.Add($"{name}: {error}");
            foreach (string warning in report.Warnings)
                warnings.Add($"{name}: {warning}");
        }

        // ---- MazeConfig assets ---------------------------------------------------------------

        static void CheckMazeConfigs(List<string> errors, List<string> warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:MazeConfig");
            if (guids.Length == 0)
                errors.Add("There is no MazeConfig asset. Run Game > Maze > Create Maze Assets.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<MazeConfig>(path);
                if (config == null)
                    continue;

                if (config.MapFile == null)
                    errors.Add($"{path}: no map file assigned.");

                if (!Mathf.Approximately(config.TileSize, 3f))
                    warnings.Add($"{path}: tile size is {config.TileSize}, the art guide says 3 (tell Dev D and Dev B before changing it).");
                if (!Mathf.Approximately(config.WallHeight, 3.3f))
                    warnings.Add($"{path}: wall height is {config.WallHeight}, the art guide says 3.3.");

                GameObject[] ghosts = config.GhostPrefabs;
                if (ghosts == null || ghosts.Length != 4)
                    errors.Add($"{path}: needs 4 ghost prefabs (Blinky, Pinky, Inky, Clyde).");
                else
                {
                    for (int i = 0; i < ghosts.Length; i++)
                    {
                        if (ghosts[i] == null)
                            errors.Add($"{path}: ghost prefab {i} is empty.");
                    }
                }

                if (config.WallMaterial == null || config.FloorMaterial == null || config.DoorMaterial == null)
                    warnings.Add($"{path}: a wall, floor or door material is empty, so the maze draws with the default material.");
            }
        }

        // ---- Game.unity ----------------------------------------------------------------------

        static void CheckGameScene(List<string> errors, List<string> warnings)
        {
            if (EditorApplication.isPlaying)
            {
                errors.Add("Stop Play mode before validating the scene.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                errors.Add($"{GameScenePath} does not exist.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(GameScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            try
            {
                CheckSceneObjects(scene, errors, warnings);
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void CheckSceneObjects(Scene scene, List<string> errors, List<string> warnings)
        {
            List<MazeBuilder> builders = FindAll<MazeBuilder>(scene);
            if (builders.Count != 1)
            {
                errors.Add($"Game.unity needs exactly one MazeBuilder, found {builders.Count}. Run Game > Maze > Add Maze To Game Scene.");
                return;
            }

            MazeBuilder builder = builders[0];
            GameObject maze = builder.gameObject;

            // The builder's tile (0, 0) is the world origin; everything else assumes it.
            if (maze.transform.position.sqrMagnitude > 0.0001f ||
                Quaternion.Angle(maze.transform.rotation, Quaternion.identity) > 0.1f ||
                (maze.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            {
                errors.Add($"The maze object \"{maze.name}\" must sit at the origin with no rotation and scale 1.");
            }

            RequireOn<MazeNavMesh>(maze, "Game > Maze > Add Maze To Game Scene", errors);
            RequireOn<MazeGhosts>(maze, "Game > Maze > Add Maze To Game Scene", errors);
            RequireOn<MazePlayerSpawn>(maze, "Game > Player > Add Player To Game Scene", errors);
            MazePickups pickups = RequireOn<MazePickups>(maze, "Game > Pickups > Add Pickups To Game Scene", errors);
            RequireOn<BatteryDirector>(maze, "Game > Pickups > Add Battery Rules And Warps", errors);
            RequireOn<MazeWarps>(maze, "Game > Pickups > Add Battery Rules And Warps", errors);

            if (maze.GetComponentsInChildren<Light>(true).Length > 0)
                warnings.Add("The maze object has a Light under it. Pickups and warp pads must have no lights (Dev D's light budget).");

            if (pickups != null)
                CheckPickupConfigs(pickups, errors);

            List<PlayerController> players = FindAll<PlayerController>(scene);
            if (players.Count != 1)
                errors.Add($"Game.unity needs exactly one PlayerController, found {players.Count}. Run Game > Player > Add Player To Game Scene.");
            else
            {
                PlayerController player = players[0];
                if (!player.CompareTag("Player"))
                    errors.Add("The player is not tagged Player, so pickups and ghosts ignore it.");
                if (player.GetComponent<CharacterController>() == null)
                    errors.Add("The player has no CharacterController.");
                if (player.GetComponentInChildren<Camera>() == null)
                    errors.Add("The player has no camera under it.");
                if (player.GetComponent<PlayerFlashlight>() == null)
                    errors.Add("The player has no PlayerFlashlight. Run Game > Player > Add Flashlight To Game Scene.");
            }

            // Every Dev A component in the scene must have all its references filled in.
            CheckUnassigned<MazeBuilder>(scene, errors);
            CheckUnassigned<MazeGhosts>(scene, errors);
            CheckUnassigned<MazePlayerSpawn>(scene, errors);
            CheckUnassigned<MazePickups>(scene, errors);
            CheckUnassigned<BatteryDirector>(scene, errors);
            CheckUnassigned<MazeWarps>(scene, errors);
            CheckUnassigned<PlayerController>(scene, errors);
            CheckUnassigned<PlayerFlashlight>(scene, errors);
        }

        // The pickup slots must hold the right kind, or a cell would behave like a battery.
        static void CheckPickupConfigs(MazePickups pickups, List<string> errors)
        {
            var so = new SerializedObject(pickups);
            CheckKind(so, "cellConfig", PickupKind.Cell, errors);
            CheckKind(so, "flareConfig", PickupKind.Flare, errors);
            CheckKind(so, "batteryConfig", PickupKind.Battery, errors);
        }

        static void CheckKind(SerializedObject so, string field, PickupKind expected, List<string> errors)
        {
            SerializedProperty property = so.FindProperty(field);
            var config = property != null ? property.objectReferenceValue as PickupConfig : null;
            if (config != null && config.Kind != expected)
                errors.Add($"MazePickups.{field} holds a {config.Kind} config, it needs a {expected} one.");
        }

        static T RequireOn<T>(GameObject target, string menu, List<string> errors) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
                errors.Add($"The maze object has no {typeof(T).Name}. Run {menu}.");
            return component;
        }

        static void CheckUnassigned<T>(Scene scene, List<string> errors) where T : Component
        {
            foreach (T component in FindAll<T>(scene))
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
                        errors.Add($"{component.gameObject.name} {typeof(T).Name}.{property.propertyPath} is not assigned.");
                }
            }
        }

        static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }
    }
}
