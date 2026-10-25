using System.Collections;
using Game.Core;
using Game.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // The CRT layer drawn over every screen: scanlines, a vignette and moving film grain.
    // Strengths come from UiTheme; the whole layer follows the player's Screen Effects setting
    // and hears changes to it live through ScreenEffectsChanged.
    public sealed class ScreenEffectsOverlay : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] UiConfig ui;
        [SerializeField] UiTheme theme;

        [Header("Layers")]
        [SerializeField] GameObject effectsRoot;
        [SerializeField] RawImage scanlines;
        [SerializeField] Image vignette;
        [SerializeField] RawImage grain;

        [Header("Listens to")]
        [SerializeField] BoolEventChannel screenEffectsChanged;

        void Awake()
        {
            SetAlpha(scanlines, theme.ScanlineOpacity);
            SetAlpha(vignette, theme.VignetteOpacity);
            SetAlpha(grain, theme.GrainOpacity);

            // One dark line every 3 screen pixels and 2 x 2 px grain at any resolution.
            scanlines.uvRect = new Rect(0f, 0f, 1f, Screen.height / 3f);
            grain.uvRect = new Rect(0f, 0f, Screen.width / 256f, Screen.height / 256f);
        }

        void OnEnable()
        {
            if (screenEffectsChanged != null) screenEffectsChanged.Raised += Apply;
            Apply(ScreenEffectsSettings.IsOn(ui));
        }

        void OnDisable()
        {
            if (screenEffectsChanged != null) screenEffectsChanged.Raised -= Apply;
            StopAllCoroutines();
        }

        void Apply(bool on)
        {
            effectsRoot.SetActive(on);
            StopAllCoroutines();
            if (on && theme.GrainOpacity > 0f)
                StartCoroutine(MoveGrain());
        }

        // Jumps the grain texture to a random offset a few times a second. Unscaled, so the
        // grain keeps moving on the pause screen.
        IEnumerator MoveGrain()
        {
            var wait = new WaitForSecondsRealtime(1f / theme.GrainFps);
            while (true)
            {
                Rect uv = grain.uvRect;
                uv.position = new Vector2(Random.value, Random.value);
                grain.uvRect = uv;
                yield return wait;
            }
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
