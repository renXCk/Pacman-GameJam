using UnityEngine;

namespace Game.Ghosts
{
    // Placeholder look so the ghosts read in the dark: body tint and a faint light in the
    // ghost's color, blue while frightened, flashing white just before it recovers, eyes only
    // when banished. Dev D replaces this with the final models, materials and lights.
    public sealed class GhostVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Ghost ghost;
        [SerializeField] Renderer body;
        [SerializeField] Light glow;

        [SerializeField] Color frightenedColor = new(0.15f, 0.3f, 1f);
        [SerializeField] Color recoveringColor = Color.white;

        [Tooltip("Flashes per second while a frightened ghost is about to recover.")]
        [Min(0.1f)] [SerializeField] float flashRate = 4f;

        [Min(0f)] [SerializeField] float emissionStrength = 1.5f;
        [Min(0f)] [SerializeField] float glowIntensity = 1.5f;

        [Tooltip("Glow multiplier while only the eyes are left.")]
        [Range(0f, 1f)] [SerializeField] float eyesGlow = 0.25f;

        MaterialPropertyBlock block;

        void Awake() => block = new MaterialPropertyBlock();

        void OnEnable()
        {
            ghost.StateChanged.AddListener(Show);
            Show(ghost.State);
        }

        void OnDisable() => ghost.StateChanged.RemoveListener(Show);

        void Update()
        {
            // Only the flashing warning needs per-frame work; every other look is set on state change.
            if (!ghost.IsRecovering)
                return;

            bool white = Mathf.Repeat(Time.time * flashRate, 1f) < 0.5f;
            Paint(white ? recoveringColor : frightenedColor, glowIntensity);
        }

        void Show(GhostState state)
        {
            bool eyesOnly = state == GhostState.Eaten;
            body.enabled = !eyesOnly;

            if (eyesOnly)
                Paint(Color.white, glowIntensity * eyesGlow);
            else
                Paint(state == GhostState.Frightened ? frightenedColor : ghost.Config.Color, glowIntensity);
        }

        void Paint(Color color, float intensity)
        {
            body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionColorId, color * emissionStrength);
            body.SetPropertyBlock(block);

            if (glow != null)
            {
                glow.color = color;
                glow.intensity = intensity;
            }
        }
    }
}
