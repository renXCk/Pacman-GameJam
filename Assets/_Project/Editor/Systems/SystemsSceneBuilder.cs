using System.Collections.Generic;
using Game.Core;
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
    // field to the assets from SystemsSetup. Colors, font and effect strengths come from
    // UiTheme; words from UiConfig.
    //
    // Press Start 2P sits on an 8 px grid, so every font size here is a multiple of 8 and every
    // widget is placed at whole-pixel offsets from a corner or the center (at the 1920 x 1080
    // reference), never at a fractional anchor that would put glyphs on half pixels.
    static class SystemsSceneBuilder
    {
        const string ConfigFolder = "Assets/_Project/ScriptableObjects/Configs/";

        static UiTheme theme;
        static UiConfig ui;
        static SystemsSetup.Assets assets;

        public static void BuildMainMenu(string path)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets only now: a new Single scene unloads unused assets, which would leave
            // references taken earlier pointing at destroyed objects (saved as None).
            Begin(SystemsSetup.CreateAssets());
            SystemsSetup.Assets a = assets;

            CreateCamera("Menu Camera");
            var systems = new GameObject("Systems");
            // Same wiring as in the Game scene; here it only loads and announces the saved score.
            Wire(systems.AddComponent<HighScoreSystem>(),
                ("rules", a.Rules), ("scoreChanged", a.ScoreChanged), ("gameStateChanged", a.GameStateChanged),
                ("mainMenuRequested", a.MainMenuRequested), ("highScoreChanged", a.HighScoreChanged));
            Wire(systems.AddComponent<AudioSettingsLoader>(), ("ui", a.Ui));
            CreateEventSystem();

            RectTransform canvas = CreateCanvas("Main Menu Canvas", 0);
            Panel(canvas, "Background", theme.Background);

            TMP_Text highScore = NewText(canvas, "High Score", string.Format(ui.HighScoreFormat, 0), 32, theme.Text, TextAlignmentOptions.Top);
            At(highScore, Top, new Vector2(0f, -56f), new Vector2(768f, 96f));

            TMP_Text title = NewText(canvas, "Title", ui.GameTitle, 96, theme.Accent, TextAlignmentOptions.Center, neon: true);
            At(title, Center, new Vector2(0f, 264f), new Vector2(1600f, 160f));
            AddEffect(title, TextEffect.Mode.Flicker);

            BuildGhostRow(canvas, new Vector2(0f, 104f));

            RectTransform column = Column(canvas, "Buttons", Center, new Vector2(0f, -168f), new Vector2(512f, 288f));
            Button play = MenuButton(column, "Play", "PLAY");
            Button controlsButton = MenuButton(column, "Controls", "CONTROLS");
            Button settingsButton = MenuButton(column, "Settings", "SETTINGS");
            Button quit = MenuButton(column, "Quit", "QUIT");

            TMP_Text hint = NewText(canvas, "Hint", ui.MenuHint, 16, theme.DimText, TextAlignmentOptions.Bottom);
            At(hint, Bottom, new Vector2(0f, 64f), new Vector2(1024f, 32f));
            AddEffect(hint, TextEffect.Mode.Blink);

            ControlsView controls = BuildControls(canvas);
            SettingsView settings = BuildSettings(canvas);

            var menu = canvas.gameObject.AddComponent<MainMenuView>();
            Wire(menu,
                ("rules", a.Rules), ("ui", a.Ui), ("title", title), ("hintText", hint), ("highScoreText", highScore),
                ("playButton", play), ("controlsButton", controlsButton), ("settingsButton", settingsButton),
                ("quitButton", quit), ("controls", controls), ("settings", settings),
                ("highScoreChanged", a.HighScoreChanged));

            BuildScreenEffects();
            Save(scene, path);
        }

        public static void BuildGame(string path)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Begin(SystemsSetup.CreateAssets());
            SystemsSetup.Assets a = assets;

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

            RectTransform canvas = CreateCanvas("Game Canvas", 0);
            BuildHud(canvas);
            BuildPauseMenu(canvas, out RectTransform pauseRoot);
            BuildGameOver(canvas);

            // Last child draws on top, so the controls screen covers the pause menu.
            ControlsView controls = BuildControls(canvas);
            Wire(pauseRoot.GetComponent<PauseMenuView>(), ("controls", controls));

            BuildScreenEffects();
            Save(scene, path);
        }

        static void Begin(SystemsSetup.Assets loaded)
        {
            assets = loaded;
            theme = loaded.Theme;
            ui = loaded.Ui;
        }

        // --- Screens ---------------------------------------------------------------------------

        static void BuildHud(RectTransform canvas)
        {
            SystemsSetup.Assets a = assets;
            RectTransform hud = Stretch(new GameObject("HUD", typeof(RectTransform)), canvas);

            // Arcade layout: 1UP score top left, HIGH SCORE top center, level top right.
            TMP_Text score = NewText(hud, "Score", string.Format(ui.ScoreFormat, 0), 32, theme.Text, TextAlignmentOptions.TopLeft);
            At(score, TopLeft, new Vector2(64f, -48f), new Vector2(512f, 96f));
            TMP_Text highScore = NewText(hud, "High Score", string.Format(ui.HighScoreFormat, 0), 32, theme.Text, TextAlignmentOptions.Top);
            At(highScore, Top, new Vector2(0f, -48f), new Vector2(768f, 96f));
            TMP_Text level = NewText(hud, "Level", string.Format(ui.LevelFormat, 1), 32, theme.Text, TextAlignmentOptions.TopRight);
            At(level, TopRight, new Vector2(-64f, -48f), new Vector2(512f, 64f));

            TMP_Text livesLabel = NewText(hud, "Lives Label", ui.LivesLabel, 16, theme.DimText, TextAlignmentOptions.BottomLeft);
            At(livesLabel, BottomLeft, new Vector2(64f, 112f), new Vector2(256f, 32f));
            var icons = new List<Object>();
            Sprite lifeSprite = UiArtGenerator.LoadSprite(UiArtGenerator.LifeIcon);
            for (int i = 0; i < a.Rules.MaxDisplayedLives; i++)
            {
                Image icon = NewImage(hud, $"Life {i + 1}", theme.LifeIcon, lifeSprite);
                At(icon, BottomLeft, new Vector2(64f + i * 48f, 64f), new Vector2(32f, 32f));
                icon.enabled = i < a.Rules.StartingLives;
                icons.Add(icon);
            }

            TMP_Text batteryLabel = NewText(hud, "Battery Label", ui.BatteryLabel, 16, theme.DimText, TextAlignmentOptions.BottomRight);
            At(batteryLabel, BottomRight, new Vector2(-64f, 112f), new Vector2(256f, 32f));

            int count = theme.BatterySegments;
            const float cell = 24f, gap = 8f;
            float inner = count * cell + (count - 1) * gap;
            Image frame = NewImage(hud, "Battery Frame", theme.DimText, UiArtGenerator.LoadSprite(UiArtGenerator.Frame));
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.5f;
            At(frame, BottomRight, new Vector2(-64f, 56f), new Vector2(inner + 24f, 48f));
            var segments = new List<Object>();
            for (int i = 0; i < count; i++)
            {
                Image segment = NewImage(frame.rectTransform, $"Cell {i + 1}", theme.Text, null);
                At(segment, Left, new Vector2(12f + i * (cell + gap), 0f), new Vector2(cell, 24f));
                segments.Add(segment);
            }

            TMP_Text message = NewText(hud, "State Message", ui.MessageFor(GameState.Ready), 64, theme.Ready, TextAlignmentOptions.Center, neon: true);
            At(message, Center, new Vector2(0f, 64f), new Vector2(1536f, 128f));
            AddEffect(message, TextEffect.Mode.Pulse);

            var view = hud.gameObject.AddComponent<HudView>();
            Wire(view,
                ("ui", a.Ui), ("theme", theme), ("scoreText", score), ("highScoreText", highScore), ("levelText", level),
                ("livesLabel", livesLabel), ("batteryLabel", batteryLabel), ("messageText", message),
                ("scoreChanged", a.ScoreChanged), ("highScoreChanged", a.HighScoreChanged),
                ("livesChanged", a.LivesChanged), ("levelNumberChanged", a.LevelNumberChanged),
                ("batteryChanged", a.BatteryChanged), ("gameStateChanged", a.GameStateChanged));
            SetArray(view, "lifeIcons", icons);
            SetArray(view, "batterySegments", segments);
        }

        static void BuildPauseMenu(RectTransform canvas, out RectTransform root)
        {
            SystemsSetup.Assets a = assets;
            root = Stretch(new GameObject("Pause Menu", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", theme.Overlay);
            RectTransform box = FramedBox(panel.rectTransform, new Vector2(704f, 512f));

            TMP_Text title = NewText(box, "Title", ui.PausedTitle, 64, theme.Text, TextAlignmentOptions.Center, neon: true);
            At(title, Top, new Vector2(0f, -64f), new Vector2(640f, 96f));

            RectTransform column = Column(box, "Buttons", Center, new Vector2(0f, -64f), new Vector2(512f, 208f));
            Button resume = MenuButton(column, "Resume", "RESUME");
            Button controls = MenuButton(column, "Controls", "CONTROLS");
            Button mainMenu = MenuButton(column, "Main Menu", "MAIN MENU");

            Wire(root.gameObject.AddComponent<PauseMenuView>(),
                ("ui", a.Ui), ("panel", panel.gameObject), ("title", title),
                ("resumeButton", resume), ("controlsButton", controls), ("mainMenuButton", mainMenu),
                ("pauseChanged", a.PauseChanged), ("pauseRequested", a.PauseRequested),
                ("mainMenuRequested", a.MainMenuRequested));
            panel.gameObject.SetActive(false);
        }

        static void BuildGameOver(RectTransform canvas)
        {
            SystemsSetup.Assets a = assets;
            RectTransform root = Stretch(new GameObject("Game Over", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", theme.Overlay);

            // Same size and place as the HUD's GAME OVER message under it, so when this screen
            // fades in the words stay put and the score and buttons appear beneath them.
            TMP_Text title = NewText(panel.rectTransform, "Title", ui.MessageFor(GameState.GameOver), 64, theme.Accent, TextAlignmentOptions.Center, neon: true);
            At(title, Center, new Vector2(0f, 64f), new Vector2(1536f, 128f));
            AddEffect(title, TextEffect.Mode.Flicker);
            TMP_Text finalScore = NewText(panel.rectTransform, "Final Score", string.Format(ui.FinalScoreFormat, 0), 32, theme.Text, TextAlignmentOptions.Center);
            At(finalScore, Center, new Vector2(0f, -48f), new Vector2(1024f, 64f));
            TMP_Text newHighScore = NewText(panel.rectTransform, "New High Score", ui.NewHighScoreText, 24, theme.Ready, TextAlignmentOptions.Center);
            At(newHighScore, Center, new Vector2(0f, -112f), new Vector2(1024f, 48f));
            AddEffect(newHighScore, TextEffect.Mode.Blink);

            RectTransform column = Column(panel.rectTransform, "Buttons", Center, new Vector2(0f, -248f), new Vector2(512f, 136f));
            Button retry = MenuButton(column, "Retry", "RETRY");
            Button mainMenu = MenuButton(column, "Main Menu", "MAIN MENU");

            Wire(root.gameObject.AddComponent<GameOverView>(),
                ("rules", a.Rules), ("ui", a.Ui), ("panel", panel.gameObject), ("title", title),
                ("finalScoreText", finalScore), ("newHighScoreText", newHighScore),
                ("retryButton", retry), ("mainMenuButton", mainMenu),
                ("gameStateChanged", a.GameStateChanged), ("newGameStarted", a.NewGameStarted),
                ("scoreChanged", a.ScoreChanged), ("highScoreChanged", a.HighScoreChanged),
                ("restartRequested", a.RestartRequested), ("mainMenuRequested", a.MainMenuRequested));
            panel.gameObject.SetActive(false);
        }

        static ControlsView BuildControls(RectTransform canvas)
        {
            RectTransform root = Stretch(new GameObject("Controls", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", theme.Overlay);
            RectTransform box = FramedBox(panel.rectTransform, new Vector2(1024f, 560f));

            TMP_Text title = NewText(box, "Title", ui.ControlsTitle, 48, theme.Accent, TextAlignmentOptions.Center, neon: true);
            At(title, Top, new Vector2(0f, -64f), new Vector2(960f, 80f));
            // The font is monospaced, so the spaces in UiConfig's lines keep the two columns aligned.
            TMP_Text body = NewText(box, "Body", string.Join("\n", ui.ControlLines), 24, theme.Text, TextAlignmentOptions.TopLeft);
            At(body, Top, new Vector2(0f, -176f), new Vector2(640f, 256f));
            body.lineSpacing = 64f;

            RectTransform column = Column(box, "Buttons", Bottom, new Vector2(0f, 56f), new Vector2(512f, 56f));
            Button back = MenuButton(column, "Back", "BACK");

            var view = root.gameObject.AddComponent<ControlsView>();
            Wire(view, ("ui", assets.Ui), ("panel", panel.gameObject), ("title", title), ("body", body), ("backButton", back));
            panel.gameObject.SetActive(false);
            return view;
        }

        static SettingsView BuildSettings(RectTransform canvas)
        {
            RectTransform root = Stretch(new GameObject("Settings", typeof(RectTransform)), canvas);
            Image panel = Panel(root, "Panel", theme.Overlay);
            RectTransform box = FramedBox(panel.rectTransform, new Vector2(1024f, 640f));

            TMP_Text title = NewText(box, "Title", ui.SettingsTitle, 48, theme.Accent, TextAlignmentOptions.Center, neon: true);
            At(title, Top, new Vector2(0f, -64f), new Vector2(960f, 80f));

            TMP_Text volumeLabel = NewText(box, "Volume Label", ui.VolumeLabel, 24, theme.DimText, TextAlignmentOptions.Center);
            At(volumeLabel, Center, new Vector2(0f, 112f), new Vector2(768f, 48f));
            Slider slider = BuildSlider(box, new Vector2(0f, 48f));

            TMP_Text effectsLabel = NewText(box, "Effects Label", ui.EffectsLabel, 24, theme.DimText, TextAlignmentOptions.Center);
            At(effectsLabel, Center, new Vector2(0f, -40f), new Vector2(768f, 48f));
            RectTransform toggleRow = Column(box, "Effects Toggle", Center, new Vector2(0f, -104f), new Vector2(512f, 56f));
            Button effectsButton = MenuButton(toggleRow, "Effects Button", ui.OnText);
            TMP_Text effectsValue = effectsButton.GetComponentInChildren<TMP_Text>();

            RectTransform column = Column(box, "Buttons", Bottom, new Vector2(0f, 56f), new Vector2(512f, 56f));
            Button back = MenuButton(column, "Back", "BACK");

            var view = root.gameObject.AddComponent<SettingsView>();
            Wire(view, ("ui", assets.Ui), ("panel", panel.gameObject), ("title", title), ("volumeLabel", volumeLabel),
                ("volumeSlider", slider), ("effectsLabel", effectsLabel), ("effectsButton", effectsButton),
                ("effectsValue", effectsValue), ("backButton", back),
                ("screenEffectsChanged", assets.ScreenEffectsChanged));
            panel.gameObject.SetActive(false);
            return view;
        }

        // Blinky, Pinky, Inky and Clyde in their own colors, like the arcade's attract screen.
        static void BuildGhostRow(RectTransform canvas, Vector2 offset)
        {
            Sprite body = UiArtGenerator.LoadSprite(UiArtGenerator.GhostBody);
            Sprite eyes = UiArtGenerator.LoadSprite(UiArtGenerator.GhostEyes);
            string[] names = { "Blinky", "Pinky", "Inky", "Clyde" };

            for (int i = 0; i < names.Length; i++)
            {
                var config = AssetDatabase.LoadAssetAtPath<GhostConfig>($"{ConfigFolder}GhostConfig_{names[i]}.asset");
                Color color = config != null ? config.Color : theme.Accent;
                Vector2 position = offset + new Vector2((i - 1.5f) * 112f, 0f);

                Image ghost = NewImage(canvas, $"Ghost {names[i]}", color, body);
                At(ghost, Center, position, new Vector2(56f, 56f));
                Image eye = NewImage(ghost.rectTransform, "Eyes", Color.white, eyes);
                Stretch(eye.gameObject, ghost.rectTransform);
            }
        }

        // Scanlines, vignette and grain on their own canvas above everything. No raycaster, so it
        // never blocks a click.
        static void BuildScreenEffects()
        {
            var go = new GameObject("Screen Effects", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var rect = go.GetComponent<RectTransform>();

            RectTransform effects = Stretch(new GameObject("Effects", typeof(RectTransform)), rect);

            // Bottom to top: grain, then scanlines, then the vignette, so the edges darken the grain too.
            RawImage grain = NewRawImage(effects, "Grain", UiArtGenerator.LoadTexture(UiArtGenerator.Grain), theme.GrainOpacity);
            grain.uvRect = new Rect(0f, 0f, 1920f / 256f, 1080f / 256f);
            RawImage scanlines = NewRawImage(effects, "Scanlines", UiArtGenerator.LoadTexture(UiArtGenerator.Scanlines), theme.ScanlineOpacity);
            scanlines.uvRect = new Rect(0f, 0f, 1f, 1080f / 3f);
            Image vignette = NewImage(effects, "Vignette", new Color(0f, 0f, 0f, theme.VignetteOpacity), UiArtGenerator.LoadSprite(UiArtGenerator.Vignette));
            Stretch(vignette.gameObject, effects);

            Wire(go.AddComponent<ScreenEffectsOverlay>(),
                ("ui", ui), ("theme", theme), ("effectsRoot", effects.gameObject),
                ("scanlines", scanlines), ("vignette", vignette), ("grain", grain),
                ("screenEffectsChanged", assets.ScreenEffectsChanged));
        }

        // --- Widgets ---------------------------------------------------------------------------

        static Slider BuildSlider(RectTransform parent, Vector2 offset)
        {
            var go = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            At(go.GetComponent<RectTransform>(), Center, offset, new Vector2(640f, 32f));

            Image background = NewImage(go.GetComponent<RectTransform>(), "Background", theme.BatteryEmpty, null);
            Stretch(background.gameObject, go.GetComponent<RectTransform>(), 0f, 8f);

            RectTransform fillArea = Stretch(new GameObject("Fill Area", typeof(RectTransform)), go.GetComponent<RectTransform>(), 0f, 8f);
            Image fill = NewImage(fillArea, "Fill", theme.Accent, null);
            Stretch(fill.gameObject, fillArea);

            RectTransform handleArea = Stretch(new GameObject("Handle Area", typeof(RectTransform)), go.GetComponent<RectTransform>(), 8f, 0f);
            Image handle = NewImage(handleArea, "Handle", theme.Text, null);
            handle.rectTransform.sizeDelta = new Vector2(16f, 0f);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = ui.DefaultVolume;
            slider.colors = ButtonColors();
            return slider;
        }

        // A frame in the accent color with a black interior, centered in `parent`.
        static RectTransform FramedBox(RectTransform parent, Vector2 size)
        {
            Image box = NewImage(parent, "Box", theme.Background, null);
            At(box, Center, Vector2.zero, size);
            Image frame = NewImage(box.rectTransform, "Frame", theme.Accent, UiArtGenerator.LoadSprite(UiArtGenerator.Frame));
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.5f;
            Stretch(frame.gameObject, box.rectTransform);
            return box.rectTransform;
        }

        // Text-only arcade button: the label itself takes the tint (off-white, red when
        // highlighted or selected by pad, yellow when pressed).
        static Button MenuButton(RectTransform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(512f, 56f);

            TMP_Text text = NewText(go.GetComponent<RectTransform>(), "Label", label, 32, Color.white, TextAlignmentOptions.Center);
            Stretch(text.gameObject, go.GetComponent<RectTransform>());
            text.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = text;
            button.colors = ButtonColors();
            return button;
        }

        static ColorBlock ButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = theme.Text;
            colors.highlightedColor = theme.Accent;
            colors.selectedColor = theme.Accent;
            colors.pressedColor = theme.Ready;
            colors.disabledColor = theme.DimText;
            colors.fadeDuration = 0.05f;
            return colors;
        }

        static void AddEffect(TMP_Text text, TextEffect.Mode mode)
        {
            var effect = text.gameObject.AddComponent<TextEffect>();
            Wire(effect, ("target", text), ("ui", ui), ("theme", theme), ("screenEffectsChanged", assets.ScreenEffectsChanged));
            var so = new SerializedObject(effect);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Small helpers ---------------------------------------------------------------------

        static readonly Vector2 Center = new(0.5f, 0.5f);
        static readonly Vector2 Top = new(0.5f, 1f);
        static readonly Vector2 Bottom = new(0.5f, 0f);
        static readonly Vector2 Left = new(0f, 0.5f);
        static readonly Vector2 TopLeft = new(0f, 1f);
        static readonly Vector2 TopRight = new(1f, 1f);
        static readonly Vector2 BottomLeft = new(0f, 0f);
        static readonly Vector2 BottomRight = new(1f, 0f);

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

        static RectTransform CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
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
            image.raycastTarget = false;
            return image;
        }

        static RawImage NewRawImage(RectTransform parent, string name, Texture texture, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            Stretch(go, parent);
            return image;
        }

        static TMP_Text NewText(RectTransform parent, string name, string text, int size, Color color,
            TextAlignmentOptions alignment, bool neon = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = theme.Font;
            if (neon)
                label.fontSharedMaterial = theme.NeonMaterial;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform Column(RectTransform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var rect = go.GetComponent<RectTransform>();
            At(rect, anchor, offset, size);
            return rect;
        }

        static RectTransform Stretch(GameObject go, RectTransform parent, float insetX = 0f, float insetY = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(insetX, insetY);
            rect.offsetMax = new Vector2(-insetX, -insetY);
            return rect;
        }

        // Anchors and pivots at the same point, then offsets by whole pixels.
        static void At(Component component, Vector2 anchor, Vector2 offset, Vector2 size)
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

        static void SetArray(Object target, string field, List<Object> items)
        {
            var so = new SerializedObject(target);
            SystemsSetup.SetArray(so.FindProperty(field), items);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Save(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"Built {path}");
        }
    }
}
