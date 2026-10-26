using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Ghosts
{
    /// <summary>
    /// Controls the 3D visual appearance and horrifying spectral animations of a Pac-Man ghost:
    /// signature emissive hues, eerie levitation floating, pupil tracking, mouth distortions,
    /// terrified shivering during the Frightened phase, and eyes-only routing when eaten.
    /// </summary>
    public sealed class GhostVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Core References")]
        [SerializeField] Ghost ghost;
        [SerializeField] Renderer body;
        [SerializeField] Light glow;

        [Header("Horror Elements & Face")]
        [Tooltip("Root transform of visual meshes (used for floating hover and banking without affecting NavMesh).")]
        [SerializeField] Transform visualRoot;
        [Tooltip("Transform containing the eyes for eyes-only rendering.")]
        [SerializeField] Transform eyesRoot;
        [Tooltip("Left pupil transform for directional tracking.")]
        [SerializeField] Transform pupilL;
        [Tooltip("Right pupil transform for directional tracking.")]
        [SerializeField] Transform pupilR;
        [Tooltip("Unsettling sunken mouth cavity during chase/scatter.")]
        [SerializeField] Renderer normalMouth;
        [Tooltip("Iconic wavy zig-zag Pac-Man mouth shown when terrified.")]
        [SerializeField] Renderer wavyMouth;

        [Header("Color States")]
        [SerializeField] Color frightenedColor = new(0.15f, 0.3f, 1f);
        [SerializeField] Color recoveringColor = Color.white;

        [Header("Tuning")]
        [Tooltip("Flashes per second while a frightened ghost is about to recover.")]
        [Min(0.1f)] [SerializeField] float flashRate = 4f;

        [Min(0f)] [SerializeField] float emissionStrength = 1.5f;
        [Min(0f)] [SerializeField] float glowIntensity = 1.5f;

        [Tooltip("Glow multiplier while only the eyes are left.")]
        [Range(0f, 1f)] [SerializeField] float eyesGlow = 0.25f;

        [Header("Spectral Levitation & Motion")]
        [SerializeField] float hoverAmplitude = 0.06f;
        [SerializeField] float hoverFrequency = 1.8f;
        [SerializeField] float tiltDegrees = 10f;
        [SerializeField] float pupilMaxOffset = 0.045f;

        MaterialPropertyBlock block;
        NavMeshAgent agent;
        Vector3 initialVisualPos;
        Vector3 initialPupilLPos;
        Vector3 initialPupilRPos;
        float hoverSeed;

        public Renderer BodyRenderer => body;
        public Light GlowLight => glow;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            agent = GetComponent<NavMeshAgent>();

            if (ghost == null) ghost = GetComponent<Ghost>();
            if (body == null)
            {
                var bodyT = transform.Find("VisualRoot/Body") ?? transform.Find("Body");
                if (bodyT != null) body = bodyT.GetComponent<Renderer>();
            }

            if (visualRoot != null)
                initialVisualPos = visualRoot.localPosition;
            else if (body != null)
                initialVisualPos = body.transform.localPosition;

            if (pupilL != null) initialPupilLPos = pupilL.localPosition;
            if (pupilR != null) initialPupilRPos = pupilR.localPosition;

            hoverSeed = Random.Range(0f, 100f);
        }

        void Start()
        {
            if (ghost != null)
            {
                Show(ghost.State);
            }
        }

        void OnEnable()
        {
            if (ghost != null)
            {
                ghost.StateChanged.AddListener(Show);
                Show(ghost.State);
            }
        }

        void OnDisable()
        {
            if (ghost != null)
                ghost.StateChanged.RemoveListener(Show);
        }

        void Update()
        {
            AnimateSpectralLevitation();
            AnimatePupilTracking();

            // Flashing warning during frightened recovery
            if (ghost != null && ghost.IsRecovering)
            {
                bool white = Mathf.Repeat(Time.time * flashRate, 1f) < 0.5f;
                Paint(white ? recoveringColor : frightenedColor, glowIntensity);
            }
        }

        void AnimateSpectralLevitation()
        {
            Transform targetTransform = visualRoot != null ? visualRoot : (body != null ? body.transform : null);
            if (targetTransform == null) return;

            // 1. Sinusoidal hover bobbing
            float hover = Mathf.Sin((Time.time + hoverSeed) * hoverFrequency) * hoverAmplitude;

            // 2. Terrified shivering jitter when frightened
            Vector3 jitter = Vector3.zero;
            if (ghost != null && ghost.State == GhostState.Frightened)
            {
                jitter = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 30f, hoverSeed) - 0.5f) * 0.03f,
                    0f,
                    (Mathf.PerlinNoise(hoverSeed, Time.time * 30f) - 0.5f) * 0.03f
                );
            }

            targetTransform.localPosition = initialVisualPos + Vector3.up * hover + jitter;

            // 3. Banking tilt into direction of travel
            if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.05f)
            {
                Vector3 localVel = transform.InverseTransformDirection(agent.velocity.normalized);
                float pitch = localVel.z * (tiltDegrees * 0.5f);
                float roll = -localVel.x * tiltDegrees;
                targetTransform.localRotation = Quaternion.Euler(pitch, 0f, roll);
            }
            else
            {
                targetTransform.localRotation = Quaternion.Lerp(targetTransform.localRotation, Quaternion.identity, Time.deltaTime * 6f);
            }
        }

        void AnimatePupilTracking()
        {
            if (pupilL == null || pupilR == null) return;

            Vector3 lookDirection = Vector3.forward;

            // If pursuing or moving, gaze in movement direction
            if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.05f)
            {
                lookDirection = transform.InverseTransformDirection(agent.velocity.normalized);
            }

            // In horizontal plane (X = left/right, Y = up/down)
            Vector3 pupilShift = new Vector3(
                Mathf.Clamp(lookDirection.x, -1f, 1f) * pupilMaxOffset,
                Mathf.Clamp(lookDirection.y, -0.6f, 0.6f) * (pupilMaxOffset * 0.5f),
                0f
            );

            pupilL.localPosition = Vector3.Lerp(pupilL.localPosition, initialPupilLPos + pupilShift, Time.deltaTime * 12f);
            pupilR.localPosition = Vector3.Lerp(pupilR.localPosition, initialPupilRPos + pupilShift, Time.deltaTime * 12f);
        }

        public void Show(GhostState state)
        {
            bool eyesOnly = state == GhostState.Eaten;
            bool isFrightened = state == GhostState.Frightened;

            // Body visibility
            if (body != null)
                body.enabled = !eyesOnly;

            // Mouth transitions
            if (normalMouth != null)
                normalMouth.enabled = !eyesOnly && !isFrightened;

            if (wavyMouth != null)
                wavyMouth.enabled = !eyesOnly && isFrightened;

            // Eyes visibility
            if (eyesRoot != null)
                eyesRoot.gameObject.SetActive(true);

            // Coloring
            if (eyesOnly)
            {
                Paint(Color.white, glowIntensity * eyesGlow);
            }
            else
            {
                Color color = isFrightened ? frightenedColor : (ghost != null && ghost.Config != null ? ghost.Config.Color : Color.white);
                Paint(color, glowIntensity);

                if (wavyMouth != null && isFrightened)
                {
                    wavyMouth.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, frightenedColor);
                    block.SetColor(EmissionColorId, frightenedColor * 2.5f);
                    wavyMouth.SetPropertyBlock(block);
                }
            }
        }

        void Paint(Color color, float intensity)
        {
            if (body != null)
            {
                body.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(EmissionColorId, color * emissionStrength);
                body.SetPropertyBlock(block);
            }

            if (glow != null)
            {
                glow.color = color;
                glow.intensity = intensity;
            }
        }
    }
}
