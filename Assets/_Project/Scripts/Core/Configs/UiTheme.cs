using TMPro;
using UnityEngine;

namespace Game.Core
{
    // The look of every screen: font, colors, the battery bar and the screen effects. Views and
    // the scene builder read it, so restyling the game means editing this one asset.
    // The defaults are a retro arcade horror look: the 1980 cabinet's pixel font and READY!
    // yellow, on black, with blood red and faint CRT scanlines.
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Game/Configs/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Font")]
        [Tooltip("Press Start 2P is drawn on an 8 px grid: use sizes that are multiples of 8 so it stays sharp.")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Same font with a soft red underlay, for titles and state messages.")]
        [SerializeField] Material neonMaterial;

        [Header("Colors")]
        [SerializeField] Color background = new(0.01f, 0.01f, 0.02f, 1f);
        [Tooltip("Behind the pause, game over, controls and settings screens.")]
        [SerializeField] Color overlay = new(0f, 0f, 0f, 0.9f);
        [SerializeField] Color text = new(0.92f, 0.90f, 0.86f, 1f);
        [SerializeField] Color dimText = new(0.50f, 0.48f, 0.46f, 1f);
        [Tooltip("Blood red: titles, CAUGHT, GAME OVER, button highlight, frames.")]
        [SerializeField] Color accent = new(0.86f, 0.06f, 0.04f, 1f);
        [Tooltip("READY! is yellow, as in the original.")]
        [SerializeField] Color ready = new(1f, 0.85f, 0.10f, 1f);
        [SerializeField] Color lifeIcon = new(1f, 0.85f, 0.10f, 1f);
        [SerializeField] Color batteryFull = new(0.92f, 0.90f, 0.86f, 1f);
        [SerializeField] Color batteryLow = new(0.86f, 0.06f, 0.04f, 1f);
        [Tooltip("Unlit battery segments.")]
        [SerializeField] Color batteryEmpty = new(1f, 1f, 1f, 0.08f);

        [Header("Battery bar")]
        [Tooltip("Below this charge (0 to 1) the bar turns red.")]
        [Range(0f, 1f)] [SerializeField] float lowBatteryThreshold = 0.25f;

        [Tooltip("Number of cells in the HUD battery bar.")]
        [Range(1, 20)] [SerializeField] int batterySegments = 8;

        [Header("Text effects")]
        [Tooltip("Flicker: how dark the text dips (alpha) during a flicker.")]
        [Range(0f, 1f)] [SerializeField] float flickerMinAlpha = 0.55f;

        [Tooltip("Flicker: average flickers per second. Kept low so it reads as a faulty light, not noise.")]
        [Range(0f, 5f)] [SerializeField] float flickersPerSecond = 0.35f;

        [Tooltip("Blink: seconds for one on/off cycle, like the arcade's flashing 1UP.")]
        [Range(0.2f, 3f)] [SerializeField] float blinkPeriod = 1f;

        [Tooltip("Pulse: cycles per second for the state message.")]
        [Range(0.1f, 5f)] [SerializeField] float pulseSpeed = 1.5f;

        [Tooltip("Pulse: lowest alpha of the state message.")]
        [Range(0f, 1f)] [SerializeField] float pulseMinAlpha = 0.7f;

        [Header("Screen effects (subtle by default; players can turn them off in Settings)")]
        // The project renders in linear color space, where a little white over black reads as a lot:
        // grain at 0.035 already looks like a grey haze. Keep it tiny.
        [Range(0f, 1f)] [SerializeField] float scanlineOpacity = 0.15f;
        [Range(0f, 1f)] [SerializeField] float vignetteOpacity = 0.6f;
        [Range(0f, 0.1f)] [SerializeField] float grainOpacity = 0.006f;

        [Tooltip("How many times a second the film grain moves.")]
        [Range(1f, 60f)] [SerializeField] float grainFps = 12f;

        public TMP_FontAsset Font => font;
        public Material NeonMaterial => neonMaterial;
        public Color Background => background;
        public Color Overlay => overlay;
        public Color Text => text;
        public Color DimText => dimText;
        public Color Accent => accent;
        public Color Ready => ready;
        public Color LifeIcon => lifeIcon;
        public Color BatteryEmpty => batteryEmpty;
        public float LowBatteryThreshold => lowBatteryThreshold;
        public int BatterySegments => batterySegments;
        public float FlickerMinAlpha => flickerMinAlpha;
        public float FlickersPerSecond => flickersPerSecond;
        public float BlinkPeriod => blinkPeriod;
        public float PulseSpeed => pulseSpeed;
        public float PulseMinAlpha => pulseMinAlpha;
        public float ScanlineOpacity => scanlineOpacity;
        public float VignetteOpacity => vignetteOpacity;
        public float GrainOpacity => grainOpacity;
        public float GrainFps => grainFps;

        // Color of the HUD's center message for a state.
        public Color ColorFor(GameState state) => state switch
        {
            GameState.Ready => ready,
            GameState.PlayerCaught => accent,
            GameState.GameOver => accent,
            _ => text
        };

        public Color BatteryColor(float charge) => charge < lowBatteryThreshold ? batteryLow : batteryFull;

        // How many of `segments` battery cells are lit for a charge from 0 to 1. Any charge above
        // zero lights at least one cell, so an almost empty battery still shows.
        public static int LitSegments(float charge, int segments)
        {
            if (segments <= 0 || charge <= 0f)
                return 0;

            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(charge) * segments), 1, segments);
        }

        // Used by the editor builder when it creates the asset.
        public void Assign(TMP_FontAsset fontAsset, Material neon)
        {
            font = fontAsset;
            neonMaterial = neon;
        }
    }
}
