using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Lighting
{
    /// <summary>
    /// Modulates a global URP Volume profile at runtime for survival horror visual states.
    /// Safely clones the profile in memory to guarantee disk assets are never mutated.
    /// Controls Bloom, Vignette, Film Grain, and Color Adjustments.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public sealed class PostProcessRig : MonoBehaviour
    {
        [Header("Volume Reference")]
        [SerializeField] Volume volume;

        [Header("Default Visual Baseline")]
        [SerializeField] float defaultBloomIntensity = 1.2f;
        [SerializeField] float defaultVignetteIntensity = 0.35f;
        [SerializeField] float defaultFilmGrainIntensity = 0.25f;
        [SerializeField] Color defaultColorFilter = Color.white;

        [Header("Horror State Presets")]
        [Tooltip("Cold cyan/blue tint applied during the frightened ghost phase.")]
        [SerializeField] Color frightenedColorFilter = new Color(0.45f, 0.75f, 1.0f);
        [Tooltip("Violent screen vignette clamp during player caught.")]
        [Range(0.5f, 1.0f)]
        [SerializeField] float caughtVignetteIntensity = 0.85f;
        [Range(0.5f, 1.0f)]
        [SerializeField] float caughtFilmGrainIntensity = 0.95f;

        // Cached runtime overrides
        Bloom bloom;
        Vignette vignette;
        FilmGrain filmGrain;
        ColorAdjustments colorAdjustments;

        Coroutine transitionCoroutine;

        void Awake()
        {
            if (volume == null)
            {
                volume = GetComponent<Volume>();
            }

            // Clone shared profile into runtime instance to protect disk asset
            if (volume != null && volume.sharedProfile != null)
            {
                volume.profile = Instantiate(volume.sharedProfile);
            }

            CacheOverrides();
        }

        void OnDestroy()
        {
            if (volume != null && volume.profile != null)
            {
                Destroy(volume.profile);
            }
        }

        void CacheOverrides()
        {
            if (volume == null || volume.profile == null) return;

            volume.profile.TryGet(out bloom);
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out filmGrain);
            volume.profile.TryGet(out colorAdjustments);

            // Ensure baseline properties are active
            if (bloom != null)
            {
                bloom.intensity.value = defaultBloomIntensity;
            }

            if (vignette != null)
            {
                vignette.intensity.value = defaultVignetteIntensity;
            }

            if (filmGrain != null)
            {
                filmGrain.intensity.value = defaultFilmGrainIntensity;
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = defaultColorFilter;
                colorAdjustments.postExposure.value = 0f;
                colorAdjustments.saturation.value = 0f;
            }
        }

        #region Public Visual Modulation API

        /// <summary>
        /// Instantly explodes exposure and bloom for a flare burst, then decays to cold blue frightened tint.
        /// </summary>
        public void TriggerFlareFlash(float flashDuration = 0.35f, float peakExposure = 3.5f)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(FlareFlashRoutine(flashDuration, peakExposure));
        }

        /// <summary>
        /// Transitions color filter smoothly back to normal when Frightened mode ends.
        /// </summary>
        public void RevertFrightenedPalette(float duration = 1.0f)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(ColorFilterRoutine(defaultColorFilter, duration));
        }

        /// <summary>
        /// Clamps screen vignette, desaturates scene to monochrome, and maxes film grain upon player death.
        /// </summary>
        public void TriggerPlayerCaughtDistortion()
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);

            if (vignette != null) vignette.intensity.value = caughtVignetteIntensity;
            if (filmGrain != null) filmGrain.intensity.value = caughtFilmGrainIntensity;

            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = -100f; // Complete monochromatic shock
                colorAdjustments.contrast.value = 40f;     // Harsh stark contrast
                colorAdjustments.postExposure.value = -1.2f;
            }
        }

        /// <summary>
        /// Smoothly restores visual state to normal gameplay baseline.
        /// </summary>
        public void ResetToBaseline(float duration = 0.8f)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(ResetBaselineRoutine(duration));
        }

        /// <summary>
        /// Dynamically intensifies film grain and vignette as battery drops below 15%.
        /// </summary>
        public void UpdateBatteryVisualNoise(float batteryFraction)
        {
            if (batteryFraction > 0.15f)
            {
                if (filmGrain != null) filmGrain.intensity.value = defaultFilmGrainIntensity;
                return;
            }

            float severity = 1f - Mathf.Clamp01(batteryFraction / 0.15f);
            if (filmGrain != null)
            {
                filmGrain.intensity.value = Mathf.Lerp(defaultFilmGrainIntensity, 0.65f, severity);
            }
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(defaultVignetteIntensity, 0.55f, severity);
            }
        }

        #endregion

        #region Transition Coroutines

        IEnumerator FlareFlashRoutine(float flashDuration, float peakExposure)
        {
            if (colorAdjustments == null) yield break;

            // Instant blinding white flash
            colorAdjustments.postExposure.value = peakExposure;
            if (bloom != null) bloom.intensity.value = defaultBloomIntensity * 4.0f;

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / flashDuration;

                colorAdjustments.postExposure.value = Mathf.Lerp(peakExposure, 0.2f, t);
                colorAdjustments.colorFilter.value = Color.Lerp(Color.white, frightenedColorFilter, t);

                if (bloom != null)
                {
                    bloom.intensity.value = Mathf.Lerp(defaultBloomIntensity * 4.0f, defaultBloomIntensity * 1.5f, t);
                }

                yield return null;
            }

            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.colorFilter.value = frightenedColorFilter;
            transitionCoroutine = null;
        }

        IEnumerator ColorFilterRoutine(Color targetColor, float duration)
        {
            if (colorAdjustments == null) yield break;

            Color startColor = colorAdjustments.colorFilter.value;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                colorAdjustments.colorFilter.value = Color.Lerp(startColor, targetColor, elapsed / duration);
                yield return null;
            }

            colorAdjustments.colorFilter.value = targetColor;
            transitionCoroutine = null;
        }

        IEnumerator ResetBaselineRoutine(float duration)
        {
            float elapsed = 0f;
            float startVignette = vignette != null ? vignette.intensity.value : defaultVignetteIntensity;
            float startGrain = filmGrain != null ? filmGrain.intensity.value : defaultFilmGrainIntensity;
            float startSat = colorAdjustments != null ? colorAdjustments.saturation.value : 0f;
            float startExp = colorAdjustments != null ? colorAdjustments.postExposure.value : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                if (vignette != null) vignette.intensity.value = Mathf.Lerp(startVignette, defaultVignetteIntensity, t);
                if (filmGrain != null) filmGrain.intensity.value = Mathf.Lerp(startGrain, defaultFilmGrainIntensity, t);
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.value = Mathf.Lerp(startSat, 0f, t);
                    colorAdjustments.postExposure.value = Mathf.Lerp(startExp, 0f, t);
                    colorAdjustments.colorFilter.value = Color.Lerp(colorAdjustments.colorFilter.value, defaultColorFilter, t);
                    colorAdjustments.contrast.value = Mathf.Lerp(colorAdjustments.contrast.value, 0f, t);
                }

                yield return null;
            }

            CacheOverrides();
            transitionCoroutine = null;
        }

        #endregion
    }
}
