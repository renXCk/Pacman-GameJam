using System.Collections;
using Game.Core;
using Game.Events;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // Animates a text's alpha: Flicker (a faulty light, counts as a screen effect and obeys the
    // player's setting), Blink (the arcade's flashing 1UP) or Pulse (a slow breath). Runs on
    // unscaled time so pause screens still animate. Timings come from UiTheme.
    public sealed class TextEffect : MonoBehaviour
    {
        public enum Mode
        {
            Flicker,
            Blink,
            Pulse
        }

        [SerializeField] Mode mode = Mode.Flicker;
        [SerializeField] TMP_Text target;

        [Header("Data")]
        [SerializeField] UiConfig ui;
        [SerializeField] UiTheme theme;

        [Header("Listens to")]
        [Tooltip("Flicker only: turns off with the player's Screen Effects setting.")]
        [SerializeField] BoolEventChannel screenEffectsChanged;

        void OnEnable()
        {
            if (screenEffectsChanged != null) screenEffectsChanged.Raised += OnEffectsChanged;
            Restart(mode != Mode.Flicker || ScreenEffectsSettings.IsOn(ui));
        }

        void OnDisable()
        {
            if (screenEffectsChanged != null) screenEffectsChanged.Raised -= OnEffectsChanged;
            StopAllCoroutines();
            target.alpha = 1f;
        }

        void OnEffectsChanged(bool on)
        {
            if (mode == Mode.Flicker)
                Restart(on);
        }

        void Restart(bool animate)
        {
            StopAllCoroutines();
            target.alpha = 1f;
            if (animate)
                StartCoroutine(mode switch { Mode.Blink => Blink(), Mode.Pulse => Pulse(), _ => Flicker() });
        }

        IEnumerator Flicker()
        {
            while (true)
            {
                // Random gaps (exponential, so flickers cluster like a failing tube).
                float gap = theme.FlickersPerSecond > 0f ? -Mathf.Log(1f - Random.value) / theme.FlickersPerSecond : 1f;
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, gap));
                if (theme.FlickersPerSecond <= 0f)
                    continue;

                int dips = Random.Range(1, 4);
                for (int i = 0; i < dips; i++)
                {
                    target.alpha = Random.Range(theme.FlickerMinAlpha, 1f);
                    yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.08f));
                    target.alpha = 1f;
                    yield return new WaitForSecondsRealtime(Random.Range(0.02f, 0.06f));
                }
            }
        }

        IEnumerator Blink()
        {
            var half = new WaitForSecondsRealtime(theme.BlinkPeriod * 0.5f);
            while (true)
            {
                target.alpha = 1f;
                yield return half;
                target.alpha = 0f;
                yield return half;
            }
        }

        IEnumerator Pulse()
        {
            while (true)
            {
                float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * theme.PulseSpeed * 2f * Mathf.PI);
                target.alpha = Mathf.Lerp(theme.PulseMinAlpha, 1f, wave);
                yield return null;
            }
        }
    }
}
