using System.Collections.Generic;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;

namespace Game.Lighting
{
    /// <summary>
    /// Controls the player's camera-tethered flashlight Spotlight.
    /// Handles camera follow/sway, battery-driven dimming, horror low-battery flicker, 
    /// and cone raycasting against ILightReceiver targets (ghosts) for movement slowdown.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlashlightController : MonoBehaviour
    {
        [Header("Tethering & Sway")]
        [Tooltip("The camera transform this flashlight follows. If null, falls back to Camera.main.")]
        [SerializeField] Transform cameraTransform;
        [Tooltip("Optional transform anchor for the player camera.")]
        [SerializeField] TransformAnchor playerAnchor;
        [Range(5f, 50f)]
        [SerializeField] float followSpeed = 25f;
        [Range(5f, 50f)]
        [SerializeField] float rotationLag = 18f;

        [Header("Light Optics Tuning")]
        [Min(0f)]
        [SerializeField] float maxIntensity = 3.5f;
        [Range(20f, 80f)]
        [SerializeField] float maxSpotAngle = 55f;
        [Range(10f, 60f)]
        [SerializeField] float minSpotAngle = 35f;
        [Min(5f)]
        [SerializeField] float beamRange = 18f;

        [Header("Battery Dynamics")]
        [SerializeField] FloatEventChannel batteryChanged;
        [Tooltip("Fraction of battery below which low-battery flicker begins.")]
        [Range(0.05f, 0.3f)]
        [SerializeField] float lowBatteryThreshold = 0.15f;
        [Range(0.01f, 0.05f)]
        [SerializeField] float emptyBatteryThreshold = 0.02f;
        [Range(1f, 30f)]
        [SerializeField] float flickerFrequency = 18f;

        [Header("ILightReceiver Sensor (Ghost Slowdown)")]
        [SerializeField] GhostRuntimeSet ghostSet;
        [SerializeField] LayerMask beamBlockers = Physics.DefaultRaycastLayers;
        [Range(0.05f, 0.3f)]
        [SerializeField] float beamCheckInterval = 0.1f;

        Light spotLight;
        float currentBattery = 1.0f;
        float nextBeamCheckTime;
        bool isFlickeringOff;

        // Tracks lit receivers to cleanly clear when out of beam or disabled
        readonly List<ILightReceiver> activeLitReceivers = new(4);

        public Light LightComponent => spotLight;
        public float CurrentBattery => currentBattery;
        public bool IsShining => spotLight != null && spotLight.enabled && spotLight.intensity > 0.01f && !isFlickeringOff;

        void Awake()
        {
            spotLight = GetComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = beamRange;
            spotLight.intensity = maxIntensity;
            spotLight.spotAngle = maxSpotAngle;
        }

        void OnEnable()
        {
            if (batteryChanged != null)
            {
                batteryChanged.Raised += OnBatteryChanged;
            }
        }

        void OnDisable()
        {
            if (batteryChanged != null)
            {
                batteryChanged.Raised -= OnBatteryChanged;
            }

            UnlightAll();
        }

        void Start()
        {
            ResolveCameraTarget();
        }

        void Update()
        {
            UpdateTetherTransform();
            UpdateLightIntensityAndFlicker();

            if (Time.time >= nextBeamCheckTime)
            {
                nextBeamCheckTime = Time.time + beamCheckInterval;
                EvaluateBeamReceivers();
            }
        }

        void ResolveCameraTarget()
        {
            if (cameraTransform == null && playerAnchor != null && playerAnchor.IsSet)
            {
                cameraTransform = playerAnchor.Value;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        void UpdateTetherTransform()
        {
            if (cameraTransform == null)
            {
                ResolveCameraTarget();
                if (cameraTransform == null) return;
            }

            // Smooth position follow
            transform.position = Vector3.Lerp(transform.position, cameraTransform.position, Time.deltaTime * followSpeed);

            // Immersive horror flashlight rotation lag
            transform.rotation = Quaternion.Slerp(transform.rotation, cameraTransform.rotation, Time.deltaTime * rotationLag);
        }

        void UpdateLightIntensityAndFlicker()
        {
            if (currentBattery <= emptyBatteryThreshold)
            {
                spotLight.intensity = 0f;
                isFlickeringOff = true;
                return;
            }

            // Normalize effective battery scale between empty threshold and 1.0
            float batteryFactor = Mathf.Clamp01((currentBattery - emptyBatteryThreshold) / (1f - emptyBatteryThreshold));

            // Dynamic beam narrowing as battery depletes
            spotLight.spotAngle = Mathf.Lerp(minSpotAngle, maxSpotAngle, batteryFactor);

            float targetIntensity = maxIntensity * Mathf.Pow(batteryFactor, 0.75f);
            isFlickeringOff = false;

            // Low-battery horror flicker simulation below threshold
            if (currentBattery <= lowBatteryThreshold)
            {
                float flickerNoise = Mathf.PerlinNoise(Time.time * flickerFrequency, 0f);
                float severity = 1f - (currentBattery / lowBatteryThreshold); // 0 to 1 as battery dies

                // Random micro-dropout
                if (flickerNoise < 0.25f * severity)
                {
                    targetIntensity = 0f;
                    isFlickeringOff = true;
                }
                else
                {
                    // Irregular voltage jitter
                    targetIntensity *= Mathf.Lerp(0.3f, 1f, flickerNoise);
                }
            }

            spotLight.intensity = targetIntensity;
        }

        void OnBatteryChanged(float newBatteryFraction)
        {
            currentBattery = Mathf.Clamp01(newBatteryFraction);
        }

        #region Light Receiver Detection & Slowdown

        void EvaluateBeamReceivers()
        {
            if (!IsShining)
            {
                UnlightAll();
                return;
            }

            Vector3 origin = spotLight.transform.position;
            Vector3 forward = spotLight.transform.forward;
            float rangeSqr = spotLight.range * spotLight.range;
            float halfAngle = spotLight.spotAngle * 0.5f;

            // Fast evaluation against known GhostRuntimeSet if assigned
            if (ghostSet != null && ghostSet.Count > 0)
            {
                for (int i = 0; i < ghostSet.Count; i++)
                {
                    Ghost ghost = ghostSet.Items[i];
                    if (ghost == null) continue;

                    bool inBeam = IsGhostInCone(ghost, origin, forward, rangeSqr, halfAngle);
                    ghost.SetLit(inBeam);

                    if (inBeam)
                    {
                        if (!activeLitReceivers.Contains(ghost))
                            activeLitReceivers.Add(ghost);
                    }
                    else
                    {
                        activeLitReceivers.Remove(ghost);
                    }
                }
            }
        }

        bool IsGhostInCone(Ghost ghost, Vector3 origin, Vector3 forward, float rangeSqr, float halfAngle)
        {
            if (!ghost.CanBeLit) return false;

            Vector3 toGhost = ghost.BeamPoint - origin;
            float distSqr = toGhost.sqrMagnitude;
            if (distSqr > rangeSqr) return false;

            float distance = Mathf.Sqrt(distSqr);
            if (distance < 0.01f) return true;

            // Cone angle test with ghost radius slack
            float slack = Mathf.Atan2(ghost.BodyRadius, distance) * Mathf.Rad2Deg;
            if (Vector3.Angle(forward, toGhost) > halfAngle + slack)
                return false;

            // Occlusion raycast against walls and obstacles
            float rayLength = Mathf.Max(0f, distance - ghost.BodyRadius);
            return !Physics.Raycast(origin, toGhost / distance, rayLength, beamBlockers, QueryTriggerInteraction.Ignore);
        }

        void UnlightAll()
        {
            for (int i = 0; i < activeLitReceivers.Count; i++)
            {
                activeLitReceivers[i]?.SetLit(false);
            }
            activeLitReceivers.Clear();
        }

        #endregion
    }
}
