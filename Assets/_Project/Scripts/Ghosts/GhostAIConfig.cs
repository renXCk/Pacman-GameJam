using UnityEngine;

namespace Game.Ghosts
{
    // Ghost tuning that is the same on every level. Per-level numbers (speeds, mode timer,
    // stun time) live in LevelConfig; per-ghost numbers live in GhostConfig.
    [CreateAssetMenu(fileName = "GhostAIConfig", menuName = "Game/Configs/Ghost AI Config")]
    public sealed class GhostAIConfig : ScriptableObject
    {
        [Header("Performance")]
        [Tooltip("Seconds between destination updates for each ghost. The spec asks for 0.2 to 0.3.")]
        [Range(0.05f, 1f)] [SerializeField] float repathInterval = 0.25f;

        [Tooltip("Seconds between flashlight beam checks.")]
        [Range(0.02f, 0.5f)] [SerializeField] float beamCheckInterval = 0.1f;

        [Header("Movement")]
        [Tooltip("A ghost this close to a point (m) counts as there.")]
        [Min(0.05f)] [SerializeField] float arriveDistance = 0.6f;

        [Tooltip("How far a target point (m) may be moved to land on the NavMesh.")]
        [Min(0.1f)] [SerializeField] float navMeshSnapDistance = 8f;

        [Tooltip("Scatter: once at its corner, a ghost roams within this many tiles of it.")]
        [Min(0f)] [SerializeField] float scatterRoamTiles = 3f;

        [Tooltip("Frightened: how far away (tiles) a ghost looks for somewhere to flee to.")]
        [Min(1f)] [SerializeField] float fleeTiles = 6f;

        [Tooltip("Frightened: spots compared each time a ghost picks where to flee. More is smarter but costs more.")]
        [Range(1, 8)] [SerializeField] int fleeCandidates = 4;

        [Tooltip("Seconds a banished ghost waits in the house before it leaves again.")]
        [Min(0f)] [SerializeField] float reviveSeconds = 1f;

        [Header("Flashlight")]
        [Tooltip("Speed multiplier for a ghost caught in the flashlight beam.")]
        [Range(0f, 1f)] [SerializeField] float litSpeedMultiplier = 0.5f;

        [Tooltip("Layers that block the beam, such as walls. Trigger colliders never block it.")]
        [SerializeField] LayerMask beamBlockers = Physics.DefaultRaycastLayers;

        [Header("Catch and banish")]
        [Tooltip("Tag on the player's collider.")]
        [SerializeField] string playerTag = "Player";

        [Tooltip("Points for the 1st, 2nd, 3rd and 4th ghost banished with one flare.")]
        [SerializeField] int[] banishComboPoints = { 200, 400, 800, 1600 };

        public float RepathInterval => repathInterval;
        public float BeamCheckInterval => beamCheckInterval;
        public float ArriveDistance => arriveDistance;
        public float NavMeshSnapDistance => navMeshSnapDistance;
        public float ScatterRoamTiles => scatterRoamTiles;
        public float FleeTiles => fleeTiles;
        public int FleeCandidates => fleeCandidates;
        public float ReviveSeconds => reviveSeconds;
        public float LitSpeedMultiplier => litSpeedMultiplier;
        public LayerMask BeamBlockers => beamBlockers;
        public string PlayerTag => playerTag;

        // Points for the combo-th ghost banished with one flare. Past the end of the list,
        // every further ghost is worth the last value.
        public int BanishPoints(int combo)
        {
            if (banishComboPoints == null || banishComboPoints.Length == 0)
                return 0;

            int index = Mathf.Clamp(combo - 1, 0, banishComboPoints.Length - 1);
            return banishComboPoints[index];
        }
    }
}
