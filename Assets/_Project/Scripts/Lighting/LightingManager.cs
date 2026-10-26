using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;

namespace Game.Lighting
{
    /// <summary>
    /// Central manager for scene ambient illumination, horror lighting events,
    /// coordination with PostProcessRig, and strict enforcement of the 6-8 dynamic light budget.
    /// </summary>
    public sealed class LightingManager : MonoBehaviour
    {
        public static LightingManager Instance { get; private set; }

        [Header("Event Channels - Dev C")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] GhostModeEventChannel modeChanged;
        [SerializeField] VoidEventChannel flareCollected;
        [SerializeField] VoidEventChannel playerCaught;
        [SerializeField] VoidEventChannel levelComplete;
        [SerializeField] FloatEventChannel batteryChanged;

        [Header("Subsystems")]
        [SerializeField] PostProcessRig postProcessRig;
        [SerializeField] FlashlightController flashlight;
        [SerializeField] TransformAnchor playerAnchor;

        [Header("Ambient Lighting Palette")]
        [Tooltip("Base ambient color during normal maze gameplay (dim horror baseline).")]
        [SerializeField] Color baseAmbientColor = new Color(0.003f, 0.003f, 0.005f);
        [Tooltip("Ambient light tint during Frightened phase (cold cyan/blue).")]
        [SerializeField] Color frightenedAmbientColor = new Color(0.01f, 0.04f, 0.08f);
        [Tooltip("Ambient light tint during player catch (near pure pitch black).")]
        [SerializeField] Color caughtAmbientColor = Color.black;

        [Header("Dynamic Light Budget Enforcement")]
        [Tooltip("Strict budget of simultaneous active dynamic lights (spec mandates 6 to 8).")]
        [Range(6, 8)]
        [SerializeField] int maxDynamicLights = 8;

        public struct TrackedLight
        {
            public Light Light;
            public int Priority; // Higher = more essential (100 = Flashlight, 50 = Ghosts, 10 = Props)
        }

        readonly List<TrackedLight> registeredLights = new(16);
        Coroutine ambientFadeCoroutine;
        Coroutine pulseCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black; // Start pure black for Ready fade-up
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void OnEnable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised += OnGameStateChanged;
            if (modeChanged != null) modeChanged.Raised += OnModeChanged;
            if (flareCollected != null) flareCollected.Raised += OnFlareCollected;
            if (playerCaught != null) playerCaught.Raised += OnPlayerCaught;
            if (levelComplete != null) levelComplete.Raised += OnLevelComplete;
            if (batteryChanged != null) batteryChanged.Raised += OnBatteryChanged;
        }

        void OnDisable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (modeChanged != null) modeChanged.Raised -= OnModeChanged;
            if (flareCollected != null) flareCollected.Raised -= OnFlareCollected;
            if (playerCaught != null) playerCaught.Raised -= OnPlayerCaught;
            if (levelComplete != null) levelComplete.Raised -= OnLevelComplete;
            if (batteryChanged != null) batteryChanged.Raised -= OnBatteryChanged;
        }

        void Update()
        {
            EnforceDynamicLightBudget();
        }

        #region Event Callbacks

        void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Ready:
                    // Fade up ambient light smoothly from pure black
                    FadeAmbient(baseAmbientColor, 1.5f);
                    if (postProcessRig != null)
                    {
                        postProcessRig.ResetToBaseline(1.2f);
                    }
                    break;

                case GameState.Playing:
                    if (RenderSettings.ambientLight == Color.black)
                    {
                        FadeAmbient(baseAmbientColor, 0.5f);
                    }
                    break;

                case GameState.PlayerCaught:
                    // Instant dynamic light collapse to near pitch-black
                    FadeAmbient(caughtAmbientColor, 0.1f);
                    if (postProcessRig != null)
                    {
                        postProcessRig.TriggerPlayerCaughtDistortion();
                    }
                    break;

                case GameState.LevelComplete:
                    // Stop ambient routines and start pulsing victory flashes
                    if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                    pulseCoroutine = StartCoroutine(PulseLevelCompleteRoutine(2.5f));
                    break;

                case GameState.GameOver:
                    FadeAmbient(Color.black, 0.8f);
                    break;
            }
        }

        void OnModeChanged(GhostModeChange modeChange)
        {
            if (modeChange.Mode == GhostMode.Frightened)
            {
                FadeAmbient(frightenedAmbientColor, 0.35f);
            }
            else
            {
                FadeAmbient(baseAmbientColor, 0.8f);
                if (postProcessRig != null)
                {
                    postProcessRig.RevertFrightenedPalette(0.8f);
                }
            }
        }

        void OnFlareCollected()
        {
            // Instant high-exposure white flash shifting ambient to cold blue
            if (postProcessRig != null)
            {
                postProcessRig.TriggerFlareFlash(0.35f, 3.5f);
            }

            FadeAmbient(frightenedAmbientColor, 0.35f);
        }

        void OnPlayerCaught()
        {
            FadeAmbient(caughtAmbientColor, 0.1f);
            if (postProcessRig != null)
            {
                postProcessRig.TriggerPlayerCaughtDistortion();
            }
        }

        void OnLevelComplete()
        {
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(PulseLevelCompleteRoutine(2.5f));
        }

        void OnBatteryChanged(float batteryFraction)
        {
            if (postProcessRig != null)
            {
                postProcessRig.UpdateBatteryVisualNoise(batteryFraction);
            }
        }

        #endregion

        #region Ambient Modulation

        void FadeAmbient(Color targetColor, float duration)
        {
            if (ambientFadeCoroutine != null) StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = StartCoroutine(FadeAmbientRoutine(targetColor, duration));
        }

        IEnumerator FadeAmbientRoutine(Color targetColor, float duration)
        {
            Color startColor = RenderSettings.ambientLight;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                RenderSettings.ambientLight = Color.Lerp(startColor, targetColor, elapsed / duration);
                yield return null;
            }

            RenderSettings.ambientLight = targetColor;
            ambientFadeCoroutine = null;
        }

        IEnumerator PulseLevelCompleteRoutine(float duration)
        {
            float elapsed = 0f;
            Color pulseColor = new Color(0.2f, 0.4f, 0.8f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                // Fast pulsing sine wave across corridor walls
                float pulse = (Mathf.Sin(elapsed * 12f) + 1f) * 0.5f;
                RenderSettings.ambientLight = Color.Lerp(baseAmbientColor, pulseColor, pulse);
                yield return null;
            }

            RenderSettings.ambientLight = baseAmbientColor;
            pulseCoroutine = null;
        }

        #endregion

        #region Dynamic Light Budget Enforcement (6-8 Lights Max)

        /// <summary>
        /// Registers a dynamic light into the budget manager.
        /// Priority: 100 = Player Flashlight, 90 = Flare, 50 = Ghosts, 10 = Pickups/Environment.
        /// </summary>
        public void RegisterDynamicLight(Light light, int priority)
        {
            if (light == null) return;

            for (int i = 0; i < registeredLights.Count; i++)
            {
                if (registeredLights[i].Light == light)
                {
                    registeredLights[i] = new TrackedLight { Light = light, Priority = priority };
                    return;
                }
            }

            registeredLights.Add(new TrackedLight { Light = light, Priority = priority });
        }

        /// <summary>
        /// Removes a light from tracking when disabled or destroyed.
        /// </summary>
        public void UnregisterDynamicLight(Light light)
        {
            for (int i = registeredLights.Count - 1; i >= 0; i--)
            {
                if (registeredLights[i].Light == light)
                {
                    registeredLights.RemoveAt(i);
                    return;
                }
            }
        }

        void EnforceDynamicLightBudget()
        {
            // Remove any destroyed light references
            for (int i = registeredLights.Count - 1; i >= 0; i--)
            {
                if (registeredLights[i].Light == null)
                {
                    registeredLights.RemoveAt(i);
                }
            }

            if (registeredLights.Count <= maxDynamicLights)
            {
                // All lights fit comfortably within budget
                for (int i = 0; i < registeredLights.Count; i++)
                {
                    if (registeredLights[i].Light != null && !registeredLights[i].Light.enabled)
                    {
                        registeredLights[i].Light.enabled = true;
                    }
                }
                return;
            }

            // Budget exceeded: Sort by priority descending, prioritizing closest to player
            Vector3 playerPos = (playerAnchor != null && playerAnchor.IsSet) 
                ? playerAnchor.Value.position 
                : ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);

            registeredLights.Sort((a, b) =>
            {
                if (b.Priority != a.Priority)
                    return b.Priority.CompareTo(a.Priority);

                float distA = Vector3.SqrMagnitude(a.Light.transform.position - playerPos);
                float distB = Vector3.SqrMagnitude(b.Light.transform.position - playerPos);
                return distA.CompareTo(distB);
            });

            // Keep top N enabled; disable the remainder
            for (int i = 0; i < registeredLights.Count; i++)
            {
                bool shouldBeActive = i < maxDynamicLights;
                if (registeredLights[i].Light.enabled != shouldBeActive)
                {
                    registeredLights[i].Light.enabled = shouldBeActive;
                }
            }
        }

        #endregion
    }
}
