using System.Collections.Generic;
using Game.Systems;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // Builds the MainMenu and Game scenes and their uGUI screens, and wires every serialized
    // field to the assets from SystemsSetup. The look is a plain dark placeholder for Dev D.
    static class SystemsSceneBuilder
    {
        static readonly Color Background = new(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color Overlay = new(0f, 0f, 0f, 0.88f);
        static readonly Color TextColor = new(0.86f, 0.84f, 0.80f, 1f);
        static readonly Color DimText = new(0.55f, 0.53f, 0.50f, 1f);
        static readonly Color Accent = new(0.72f, 0.08f, 0.06f, 1f);
        static readonly Color ButtonColor = new(0.09f, 0.09f, 0.10f, 1f);

        static Sprite uiSprite;
        static Sprite backgroundSprite;
        static Sprite knobSprite;

        public static void BuildMainMenu(string path)
        {
            LoadBuiltinSprites();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets only now: a new Single scene unloads unused assets, which would leave
            // references taken earlier pointing at destroyed objects (saved as None).
            SystemsSetup.Assets a = SystemsSetup.CreateAssets();

            CreateCamera("Menu Camera");
            var systems = new GameObject("Systems");
            // Same wiring as in the Game scene; here it only loads and announces the saved score.
            Wire(systems.AddComponent<HighScoreSystem>(),
                ("rules", a.Rules), ("scoreChanged", a.ScoreChanged), ("gameStateChanged", a.GameStateChanged),
                ("mainMenuRequested", a.MainMenuRequested), ("highScoreChanged", a.HighScoreChanged));
            Wire(systems.AddComponent<AudioSettingsLoader>(), ("ui", a.Ui));
            CreateEventSystem();

            RectTransform canvas = CreateCanvas("Main Menu Canvas");
            Panel(canvas, "Background", Background);

            TMP_Text title = NewText(canvas, "Title", "", 120, TextColor, TextAlignmentOptions.Center);
            Place(title, new Vector2(0.5f, 0.78f), new Vector2(1400f, 180f));
            title.characterSpacing = 12f;

            TMP_Text highScore = NewText(canvas, "High Score", "", 36, DimText, TextAlignmentOptions.Center);
            Place(highScore, new Vector2(0.5f, 0.64f), new Vector2(800f, 60f));

            RectTransform column = Column(canvas, "Buttons", new Vector2(0.5f, 0.38f), new Vector2(480f, 380f));
            Button play = MenuButton(column, "Play", "PLAY");
            Button controlsButton = MenuButton(column, "Controls", "CONTROLS");
            Button settingsButton = MenuButton(column, "Settings", "SETTINGS");
            Button quit = MenuButton(column, "Quit", "QUIT");

            ControlsView controls = BuildControls(canvas, a);
            SettingsView settings = BuildSettings(canvas, a);

            var menu = canvas.gameObject.AddComponent<MainMenuView>();
            Wire(menu,
                ("rules", a.Rules), ("ui", a.Ui), ("title", title), ("highScoreText", highScore),
                ("playButton", play), ("controlsButton", controlsButton), ("settingsButton", settingsButton),
                ("quitButton", quit), ("controls", controls), ("settings", settings),
                ("highScoreChanged", a.HighScoreChanged));

            Save(scene, path);
        }

        public static void BuildGame(string path)
        {
            LoadBuiltinSprites();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets only now: a new Single scene unloads unused assets, which would leave
            // references taken earlier pointing at destroyed objects (saved as None).
            SystemsSetup.Assets a = SystemsSetup.CreateAssets();

            // Only until Dev A's player brings its own camera and AudioListener.
            CreateCamera("Placeholder Camera (delete when the player exists)");

            var systems = new GameObject("Systems");
            Wire(systems.AddComponent<GameManager>(),
                ("rules", a.Rules), ("levels", a.Levels), ("progress", a.Progress),
                ("playerCaught", a.PlayerCaught), ("livesChanged", a.LivesChanged),
                ("restartRequested", a.RestartRequested), ("mainMenuRequested", a.MainMenuRequested),
                ("gameStateChanged", a.GameStateChanged), ("levelStarted", a.LevelStarted),
                ("levelNumberChanged", a.LevelNumberChanged), ("levelComplete", a.LevelComplete),
                ("newGameStarted", a.NewGameStarted), ("lifeLost", a.LifeLost));
            Wire(systems.AddComponent<ScoreSystem>(),
                ("rules", a.Rules), ("flare", a.Flare),
                ("newGameStarted", a.NewGameStarted), ("levelStarted", a.LevelStarted),
                ("pickupCollected", a.PickupCollected), ("flareCollected", a.FlareCollected),
                ("ghostBanished", a.GhostBanished),
                ("scoreChanged", a.ScoreChanged), ("extraLifeAwarded", a.ExtraLifeAwarded));
            Wire(systems.AddComponent<LivesSystem>(),
                ("rules", a.Rules), ("newGameStarted", a.NewGameStarted), ("lifeLost", a.LifeLost),
                ("extraLifeAwarded", a.ExtraLifeAwarded), ("livesChanged", a.LivesChanged));
            Wire(systems.AddComponent<HighScoreSystem>(),
                ("rules", a.Rules), ("scoreChanged", a.ScoreChanged), ("gameStateChanged", a.GameStateChanged),
                ("mainMenuRequested", a.MainMenuRequested), ("highScoreChanged", a.HighScoreChanged));
            Wire(systems.AddComponent<PauseController>(),
                ("gameStateChanged", a.GameStateChanged), ("pauseRequested", a.PauseRequested),
                ("pauseChanged", a.PauseChanged));
            Wire(systems.AddComponent<CursorController>(),
                ("gameStateChanged", a.GameStateChanged), ("pauseChanged", a.PauseChanged));
            Wire(systems.AddComponent<AudioSettingsLoader>(), ("ui", a.Ui));

            CreateEventSystem();
            new GameObject("--- Maze goes here ---");

            RectTransform canvas = CreateCanvas("Game Canvas");
            BuildHud(canvas, a);
            BuildPauseMenu(canvas, a, out RectTransform pauseRoot);
            BuildGameOver(canvas, a);

            // Last child draws on top, so the controls screen covers the pause menu.
            ControlsView controls = BuildControls(canvas, a);
            Wire(pauseRoot.GetComponent<PauseMenuView>(), ("controls", controls));

            Save(scene, path);
        }

        static void BuildHud(RectTransform canvas, SystemsSetup.Assets a)
        {
            RectTransform hud = Stretch(new GameObject("HUD", typeof(RectTransform)), canvas);

            TMP_Text score = NewText(hud, "Score", "", 40, TextColor, TextAlignmentOptions.TopLeft);
            Corner(score, new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(600f, 60f));
            TMP_Text highScore = NewText(hud, "High Score", "", 32, DimText, TextAlignmentOptions.Top);
            Corner(highScore, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 50f));
            TMP_Text level = NewText(hud, "Level", "", 40, TextColor, TextAlignmentOptions.TopRight);
            Corner(level, new Vector2(1f, 1f), new Vector2(-48f, -36f), new Vector2(400f, 60f));

            TMP_Text livesLabel = NewText(hud, "Lives Label", "", 28, DimText, TextAlignmentOptions.BottomLeft);
            Corner(livesLabel, new Vector2(0f, 0f), new Vector2(48f, 90f), new Vector2(300f, 40f));
            var icons = new List<Object>();
            for (int i = 0; i < a.Rules.MaxDisplayedLives; i++)
            {
                Image icon = NewImage(hud, $"Life {i + 1}", Accent, knobSprite);
                Corner(icon, new Vector2(0f, 0f), new Vector2(48f + i * 44f, 40f), new Vector2(32f, 32f));
                icons.Add(icon);
            }

            TMP_Text batteryLabel = NewText(hud, "Battery Label", "", 28, DimText, TextAlignmentOptions.BottomRight);
            Corner(batteryLabel, new Vector2(1f, 0f), new Vector2(-48f, 90f), new Vector2(300f, 40f));
            Image batteryBack = NewImage(hud, "Battery Bar", new Color(1f, 1f, 1f, 0.08f), uiSprite);
            Corner(batteryBack, new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(320f, 28f));
            Image batteryFill = NewImage(batteryBack.rectTransform, "Fill", TextColor, uiSprite);
            Stretch(batteryFill.gameObject, batteryBack.rectTransform, 4f);
            batteryFill.type = UnityEngine.UI.Image.Type.Filled;
            batteryFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

            TMP_Text message = NewText(hud, "State Message", "", 96, Accent, TextAlignmentOptions.Center);
            Place(message, new Vector2(0.5f, 0.55f), new Vector2(1600f, 160f));
            message.characterSpacing = 16f;

            var view = hud.gameObject.AddComponent<HudView>();
            Wire(view,
                ("ui", a.Ui), ("scoreText", score), ("highScoreText", highScore), ("levelText", level),
                ("livesLabel", livesLabel), ("batteryLabel", batteryLabel), ("batteryFill", batteryFill),
                ("messageText", message),
                ("scoreChanged", a.ScoreChanged), ("highScoreChanged", a.HighScoreChanged),
                ("livesChanged", a.LivesChanged), ("levelNumberChanged", a.LevelNumberChanged),
                ("batteryChanged", a.BatteryChanged), ("gameStateChanged", a.GameStateChanged));

            var so = new SerializedObject(view);
            SystemsSetup.SetArray(so.FindProperty("lifeIcons"), icons);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPauseMenu(RectTransform canvas, SystemsSetup.Assets a, out RectTransform root)
        {
            root = Stretch(new GameObject("Pause Menu", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", Overlay);

            TMP_Text title = NewText(panel.rectTransform, "Title", "", 96, TextColor, TextAlignmentOptions.Center);
            Place(title, new Vector2(0.5f, 0.72f), new Vector2(1000f, 140f));

            RectTransform column = Column(panel.rectTransform, "Buttons", new Vector2(0.5f, 0.42f), new Vector2(480f, 290f));
            Button resume = MenuButton(column, "Resume", "RESUME");
            Button controls = MenuButton(column, "Controls", "CONTROLS");
            Button mainMenu = MenuButton(column, "Main Menu", "MAIN MENU");

            Wire(root.gameObject.AddComponent<PauseMenuView>(),
                ("ui", a.Ui), ("panel", panel.gameObject), ("title", title),
                ("resumeButton", resume), ("controlsButton", controls), ("mainMenuButton", mainMenu),
                ("pauseChanged", a.PauseChanged), ("pauseRequested", a.PauseRequested),
                ("mainMenuRequested", a.MainMenuRequested));
        }

        static void BuildGameOver(RectTransform canvas, SystemsSetup.Assets a)
        {
            RectTransform root = Stretch(new GameObject("Game Over", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", Overlay);

            TMP_Text title = NewText(panel.rectTransform, "Title", "", 110, Accent, TextAlignmentOptions.Center);
            Place(title, new Vector2(0.5f, 0.74f), new Vector2(1400f, 160f));
            title.characterSpacing = 16f;
            TMP_Text finalScore = NewText(panel.rectTransform, "Final Score", "", 48, TextColor, TextAlignmentOptions.Center);
            Place(finalScore, new Vector2(0.5f, 0.6f), new Vector2(1000f, 70f));
            TMP_Text newHighScore = NewText(panel.rectTransform, "New High Score", "", 40, Accent, TextAlignmentOptions.Center);
            Place(newHighScore, new Vector2(0.5f, 0.53f), new Vector2(1000f, 60f));

            RectTransform column = Column(panel.rectTransform, "Buttons", new Vector2(0.5f, 0.33f), new Vector2(480f, 190f));
            Button retry = MenuButton(column, "Retry", "RETRY");
            Button mainMenu = MenuButton(column, "Main Menu", "MAIN MENU");

            Wire(root.gameObject.AddComponent<GameOverView>(),
                ("rules", a.Rules), ("ui", a.Ui), ("panel", panel.gameObject), ("title", title),
                ("finalScoreText", finalScore), ("newHighScoreText", newHighScore),
                ("retryButton", retry), ("mainMenuButton", mainMenu),
                ("gameStateChanged", a.GameStateChanged), ("newGameStarted", a.NewGameStarted),
                ("scoreChanged", a.ScoreChanged), ("highScoreChanged", a.HighScoreChanged),
                ("restartRequested", a.RestartRequested), ("mainMenuRequested", a.MainMenuRequested));
        }

        static ControlsView BuildControls(RectTransform canvas, SystemsSetup.Assets a)
        {
            RectTransform root = Stretch(new GameObject("Controls", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", new Color(Background.r, Background.g, Background.b, 0.97f));

            TMP_Text title = NewText(panel.rectTransform, "Title", "", 80, TextColor, TextAlignmentOptions.Center);
            Place(title, new Vector2(0.5f, 0.8f), new Vector2(1000f, 120f));
            TMP_Text body = NewText(panel.rectTransform, "Body", "", 40, TextColor, TextAlignmentOptions.TopLeft);
            Place(body, new Vector2(0.5f, 0.5f), new Vector2(900f, 360f));

            RectTransform column = Column(panel.rectTransform, "Buttons", new Vector2(0.5f, 0.2f), new Vector2(480f, 90f));
            Button back = MenuButton(column, "Back", "BACK");

            var view = root.gameObject.AddComponent<ControlsView>();
            Wire(view, ("ui", a.Ui), ("panel", panel.gameObject), ("title", title), ("body", body), ("backButton", back));
            return view;
        }

        static SettingsView BuildSettings(RectTransform canvas, SystemsSetup.Assets a)
        {
            RectTransform root = Stretch(new GameObject("Settings", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", new Color(Background.r, Background.g, Background.b, 0.97f));

            TMP_Text title = NewText(panel.rectTransform, "Title", "", 80, TextColor, TextAlignmentOptions.Center);
            Place(title, new Vector2(0.5f, 0.78f), new Vector2(1000f, 120f));
            TMP_Text label = NewText(panel.rectTransform, "Volume Label", "", 36, DimText, TextAlignmentOptions.Center);
            Place(label, new Vector2(0.5f, 0.58f), new Vector2(800f, 60f));

            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources
            {
                standard = uiSprite, background = backgroundSprite, knob = knobSprite
            });
            sliderObject.name = "Volume Slider";
            sliderObject.transform.SetParent(panel.rectTransform, false);
            Place(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(700f, 30f));
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect.GetComponent<Image>().color = Accent;

            RectTransform column = Column(panel.rectTransform, "Buttons", new Vector2(0.5f, 0.25f), new Vector2(480f, 90f));
            Button back = MenuButton(column, "Back", "BACK");

            var view = root.gameObject.AddComponent<SettingsView>();
            Wire(view, ("ui", a.Ui), ("panel", panel.gameObject), ("title", title), ("volumeLabel", label),
                ("volumeSlider", slider), ("backButton", back));
            return view;
        }

        // --- Small helpers ------------------------------------------------------------------

        static void CreateCamera(string name)
        {
            var go = new GameObject(name, typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static RectTransform CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go.GetComponent<RectTransform>();
        }

        static Image Panel(RectTransform parent, string name, Color color)
        {
            Image image = NewImage(parent, name, color, null);
            Stretch(image.gameObject, parent);
            return image;
        }

        static Image NewImage(RectTransform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite == uiSprite && sprite != null)
                image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        static TMP_Text NewText(RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform Column(RectTransform parent, string name, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var rect = go.GetComponent<RectTransform>();
            Place(rect, anchor, size);
            return rect;
        }

        static Button MenuButton(RectTransform parent, string name, string label)
        {
            Image image = NewImage(parent, name, Color.white, uiSprite);
            image.rectTransform.sizeDelta = new Vector2(480f, 72f);
            var button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.22f, 0.05f, 0.04f, 1f);
            colors.selectedColor = new Color(0.22f, 0.05f, 0.04f, 1f);
            colors.pressedColor = Accent;
            button.colors = colors;

            TMP_Text text = NewText(image.rectTransform, "Label", label, 36, TextColor, TextAlignmentOptions.Center);
            Stretch(text.gameObject, image.rectTransform);
            text.characterSpacing = 6f;
            return button;
        }

        static RectTransform Stretch(GameObject go, RectTransform parent, float inset = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        static void Place(Component component, Vector2 anchor, Vector2 size)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void Corner(Component component, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in fields)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void LoadBuiltinSprites()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            backgroundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        static void Save(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"Built {path}");
        }
    }
}
