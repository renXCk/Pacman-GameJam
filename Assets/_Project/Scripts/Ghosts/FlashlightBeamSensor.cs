using UnityEngine;

namespace Game.Ghosts
{
    // Put this on the flashlight's spot light. A few times a second it checks each ghost:
    // in range, inside the cone and not behind a wall means lit, and a lit ghost is slowed.
    // A ghost out of range costs one distance check; only nearby ghosts get the angle test
    // and the raycast.
    [RequireComponent(typeof(Light))]
    public sealed class FlashlightBeamSensor : MonoBehaviour
    {
        [SerializeField] GhostRuntimeSet ghosts;
        [SerializeField] GhostAIConfig ai;

        Light beam;
        float nextCheckTime;

        public int LitCount { get; private set; }

        void Awake() => beam = GetComponent<Light>();

        void OnEnable()
        {
            if (ghosts != null && ai != null)
                return;

            Debug.LogError($"{name} needs a GhostRuntimeSet and a GhostAIConfig to light ghosts.", this);
            enabled = false;
        }

        void OnDisable() => UnlightAll();

        void Update()
        {
            if (Time.time < nextCheckTime)
                return;

            nextCheckTime = Time.time + ai.BeamCheckInterval;
            CheckBeam();
        }

        void CheckBeam()
        {
            if (!IsShining())
            {
                UnlightAll();
                return;
            }

            Vector3 origin = beam.transform.position;
            Vector3 forward = beam.transform.forward;
            float rangeSqr = beam.range * beam.range;
            float halfAngle = beam.spotAngle * 0.5f;
            int litCount = 0;

            for (int i = 0; i < ghosts.Count; i++)
            {
                Ghost ghost = ghosts.Items[i];
                bool lit = ghost.CanBeLit && InBeam(ghost, origin, forward, rangeSqr, halfAngle);
                ghost.SetLit(lit);
                if (lit)
                    litCount++;
            }

            LitCount = litCount;
        }

        // Off, out of battery (zero intensity) or not a spot light: lights nothing.
        bool IsShining() => beam.isActiveAndEnabled && beam.intensity > 0f && beam.type == LightType.Spot;

        bool InBeam(Ghost ghost, Vector3 origin, Vector3 forward, float rangeSqr, float halfAngle)
        {
            Vector3 toGhost = ghost.BeamPoint - origin;
            float distanceSqr = toGhost.sqrMagnitude;
            if (distanceSqr > rangeSqr)
                return false;

            float distance = Mathf.Sqrt(distanceSqr);
            if (distance < 0.01f)
                return true;

            // Widen the cone by the ghost's own size so a ghost at the edge of the beam still counts.
            float slack = Mathf.Atan2(ghost.BodyRadius, distance) * Mathf.Rad2Deg;
            if (Vector3.Angle(forward, toGhost) > halfAngle + slack)
                return false;

            // Stop short of the ghost so its own colliders never block the check.
            float rayLength = Mathf.Max(0f, distance - ghost.BodyRadius);
            return !Physics.Raycast(origin, toGhost / distance, rayLength, ai.BeamBlockers, QueryTriggerInteraction.Ignore);
        }

        void UnlightAll()
        {
            LitCount = 0;
            if (ghosts == null)
                return;

            for (int i = 0; i < ghosts.Count; i++)
                ghosts.Items[i].SetLit(false);
        }
    }
}
