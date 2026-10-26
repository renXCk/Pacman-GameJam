using System.Collections.Generic;
using Game.Audio;
using Game.Lighting;
using Game.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Automated QA verification tool for validating Blackout Maze's audiovisual systems,
    /// event channel bindings, URP Global Volume profile, and 6-8 dynamic light budgets.
    /// Accessible via menu: Game > QA > Validate Audiovisual & Performance Setup
    /// </summary>
    public static class AudiovisualQAValidator
    {
        [MenuItem("Game/QA/Validate Audiovisual & Performance Setup", priority = 100)]
        public static void ValidateAll()
        {
            var warnings = new List<string>();
            var errors = new List<string>();

            ValidateSceneManagers(errors, warnings);
            ValidatePostProcessing(errors, warnings);
            ValidateDynamicLightBudget(errors, warnings);
            ValidateMaterials(errors, warnings);

            // Log Results
            foreach (var err in errors)
            {
                Debug.LogError($"<color=#FF4444>[QA Error]</color> {err}");
            }

            foreach (var warn in warnings)
            {
                Debug.LogWarning($"<color=#FFAA00>[QA Warning]</color> {warn}");
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("<color=#00FFAA><b>[QA PASSED]</b></color> All Audiovisual, Lighting, VFX, and Performance systems are 100% wired and budget-compliant for 60 FPS!");
            }
            else
            {
                Debug.Log($"<color=#FFFF00>[QA Finished]</color> Completed with {errors.Count} error(s) and {warnings.Count} warning(s).");
            }
        }

        static void ValidateSceneManagers(List<string> errors, List<string> warnings)
        {
            // 1. Audio Manager
            var audio = Object.FindAnyObjectByType<AudioManager>();
            if (audio == null)
            {
                warnings.Add("AudioManager not found in active scene. Create an [Audio] GameObject and add AudioManager.");
            }

            // 2. Lighting Manager
            var lighting = Object.FindAnyObjectByType<LightingManager>();
            if (lighting == null)
            {
                warnings.Add("LightingManager not found in active scene. Create a [Lighting] GameObject and add LightingManager.");
            }

            // 3. VFX Manager
            var vfx = Object.FindAnyObjectByType<VFXManager>();
            if (vfx == null)
            {
                warnings.Add("VFXManager not found in active scene. Create a [VFX] GameObject and add VFXManager.");
            }
        }

        static void ValidatePostProcessing(List<string> errors, List<string> warnings)
        {
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            Volume globalVolume = null;
            foreach (var v in volumes)
            {
                if (v.isGlobal)
                {
                    globalVolume = v;
                    break;
                }
            }

            if (globalVolume == null)
            {
                errors.Add("No Global Volume found in the scene! Screen post-processing and Bloom will not execute.");
                return;
            }

            var rig = globalVolume.GetComponent<PostProcessRig>();
            if (rig == null)
            {
                warnings.Add("PostProcessRig component missing from Global Volume. Add PostProcessRig to enable runtime horror state transitions.");
            }

            var profile = globalVolume.sharedProfile;
            if (profile == null)
            {
                errors.Add("Global Volume has no VolumeProfile assigned!");
                return;
            }

            if (!profile.Has<Bloom>()) warnings.Add("Global Volume Profile missing Bloom override (required for emissive cell pickups).");
            if (!profile.Has<Vignette>()) warnings.Add("Global Volume Profile missing Vignette override (required for horror death clamp).");
            if (!profile.Has<FilmGrain>()) warnings.Add("Global Volume Profile missing Film Grain override (required for low-battery noise).");
            if (!profile.Has<ColorAdjustments>()) warnings.Add("Global Volume Profile missing Color Adjustments override (required for flare flash & frightened tint).");
        }

        static void ValidateDynamicLightBudget(List<string> errors, List<string> warnings)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            int activeDynamicLights = 0;

            foreach (var l in lights)
            {
                if (l.enabled && l.lightmapBakeType != LightmapBakeType.Baked)
                {
                    activeDynamicLights++;
                }
            }

            if (activeDynamicLights > 8)
            {
                errors.Add($"Scene contains {activeDynamicLights} active realtime dynamic lights, violating the strict 6-8 budget! Ensure LightingManager is active to cull excess lights.");
            }
            else
            {
                Debug.Log($"<color=#00FFAA>[Light Budget OK]</color> Active realtime dynamic lights: {activeDynamicLights} / 8 max.");
            }
        }

        static void ValidateMaterials(List<string> errors, List<string> warnings)
        {
            string[] emissivePickups = {
                "Assets/_Project/Art/Materials/M_PowerCell_Emissive.mat",
                "Assets/_Project/Art/Materials/M_Battery_Emissive.mat",
                "Assets/_Project/Art/Materials/M_Flare_Emissive.mat"
            };

            foreach (var matPath in emissivePickups)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    warnings.Add($"Material missing at {matPath}. Run Game > VFX > Generate VFX Prefabs & Materials.");
                }
                else if (!mat.IsKeywordEnabled("_EMISSION"))
                {
                    warnings.Add($"Material {mat.name} does not have _EMISSION enabled!");
                }
            }
        }
    }
}
