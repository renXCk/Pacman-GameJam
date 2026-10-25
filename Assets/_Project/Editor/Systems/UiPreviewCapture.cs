using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    // Game > Systems > Capture UI Preview. Renders the main menu, settings, HUD, pause, game over
    // and controls screens to 1920 x 1080 PNGs in Recordings/UiPreview (git-ignored), without
    // entering Play mode and without saving the scenes. Useful for checking the look and for
    // deck screenshots.
    // Batch (needs a graphics device, so no -nographics):
    //   Unity -batchmode -projectPath <path> -executeMethod Game.EditorTools.UiPreviewCapture.CaptureAll -quit [-captureOut <folder>]
    public static class UiPreviewCapture
    {
        const int Width = 1920;
        const int Height = 1080;

        [MenuItem("Game/Systems/Capture UI Preview")]
        public static void CaptureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string original = EditorSceneManager.GetActiveScene().path;
            string folder = CaptureAll();
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
            EditorUtility.RevealInFinder(folder);
        }

        public static string CaptureAll()
        {
            string folder = OutputFolder();
            Directory.CreateDirectory(folder);

            Capture(SystemsSetup.MainMenuScenePath, folder, "1-main-menu");
            Capture(SystemsSetup.MainMenuScenePath, folder, "2-settings", "Main Menu Canvas/Settings/Panel");
            Capture(SystemsSetup.GameScenePath, folder, "3-hud");
            Capture(SystemsSetup.GameScenePath, folder, "4-pause", "Game Canvas/Pause Menu/Panel");
            Capture(SystemsSetup.GameScenePath, folder, "5-game-over", "Game Canvas/Game Over/Panel");
            Capture(SystemsSetup.GameScenePath, folder, "6-controls", "Game Canvas/Controls/Panel");

            Debug.Log($"UI previews written to {folder}");
            return folder;
        }

        static void Capture(string scenePath, string folder, string name, string showPath = null)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (showPath != null)
            {
                FindInScene(showPath).SetActive(true);

                // The HUD's state message would not be showing behind these screens in play.
                if (showPath.StartsWith("Game Canvas/"))
                    FindInScene("Game Canvas/HUD/State Message").SetActive(false);
            }

            var cameraObject = new GameObject("Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            // sRGB, like the screen: a linear target would brighten the faint grain and vignette.
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;

            // Overlay canvases do not render into a camera's texture; switch them for the capture.
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f + (100 - canvas.sortingOrder) * 0.01f;
            }

            foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>())
                text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        // GameObject.Find skips inactive objects, so walk from the root by name.
        static GameObject FindInScene(string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != parts[0])
                    continue;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);
                if (current != null)
                    return current.gameObject;
            }

            throw new InvalidOperationException($"No object at {path}.");
        }

        static string OutputFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-captureOut")
                    return args[i + 1];
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "UiPreview"));
        }
    }
}
