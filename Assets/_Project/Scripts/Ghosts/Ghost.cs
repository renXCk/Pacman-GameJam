using Game.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Game.Ghosts
{
    // One specter. A small state machine picks a target point for the current state and a
    // NavMeshAgent walks there. A new destination is chosen only every RepathInterval seconds,
    // never every frame. GhostDirector resets, freezes and starts the ghost; global mode
    // changes arrive on the ModeChanged channel.
    [RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
    public sealed class Ghost : MonoBehaviour, ILightReceiver
    {
        const int RandomPointAttempts = 4;

        [SerializeField] GhostConfig config;
        [SerializeField] GhostRuntimeSet runtimeSet;

        [Header("Listens to")]
        [SerializeField] GhostModeEventChannel modeChanged;

        [Header("Local hook for this prefab's visuals and sounds")]
        [SerializeField] UnityEvent<GhostState> stateChanged = new();

        NavMeshAgent agent;
        GhostDirector director;
        GhostState state = GhostState.InHouse;
        GhostMode baseMode = GhostMode.Scatter;
        Vector3 homePosition;
        Quaternion homeRotation;
        bool frozen = true;
        bool lit;
        float nextThinkTime;
        float releaseAtRoundTime;
        int banishedInCycle = -1;
        Vector3 aimPoint;
        Vector3 destination;
        bool hasDestination;
        Vector3 roamPoint;
        bool hasRoamPoint;

        public GhostConfig Config => config;
        public GhostState State => state;
        public GhostDirector Director => director;
        public UnityEvent<GhostState> StateChanged => stateChanged;
        public bool IsFrozen => frozen;
        public bool IsLit => lit;
        public Vector3 Destination => destination;
        public bool HasDestination => hasDestination;

        // Path requests made so far. Shown in the Inspector and used by tests to prove throttling.
        public int DestinationUpdates { get; private set; }

        // Touching the player in these states costs the player a life.
        public bool IsDangerous => state == GhostState.Scatter || state == GhostState.Chase || state == GhostState.LeavingHouse;

        // The last seconds of Frightened, when the ghost flashes before recovering.
        public bool IsRecovering => state == GhostState.Frightened && director != null && director.FrightenedEnding;

        // Where the flashlight beam aims its line-of-sight check, and how wide the ghost is.
        public Vector3 BeamPoint => transform.position + Vector3.up * (agent.height * 0.5f);
        public float BodyRadius => agent.radius;
        public bool CanBeLit => state != GhostState.InHouse && state != GhostState.Eaten;

        internal GhostModeEventChannel ModeChannel => modeChanged;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // Ghosts pass through each other like the originals, so two of them never jam a corridor.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.autoBraking = false;

            // The agent stays off until the director places the ghost, so a maze that bakes its
            // NavMesh at runtime can spawn ghosts before the bake.
            agent.enabled = false;

            // Kinematic so the catch trigger fires against the player's CharacterController.
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        void OnEnable()
        {
            if (modeChanged != null)
                modeChanged.Raised += OnModeChanged;

            if (runtimeSet != null)
                runtimeSet.Add(this);
        }

        void OnDisable()
        {
            if (modeChanged != null)
                modeChanged.Raised -= OnModeChanged;

            if (runtimeSet != null)
                runtimeSet.Remove(this);
        }

        void Update()
        {
            if (frozen || director == null || !agent.isOnNavMesh)
                return;

            if (Time.time < nextThinkTime)
                return;

            nextThinkTime = Time.time + director.AI.RepathInterval;
            Think();
        }

        // Called by GhostDirector when this ghost joins its maze.
        internal void Bind(GhostDirector owner) => director = owner;

        // Back to the start position for a new round. Stays frozen until Resume.
        internal void ResetToHome()
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);

            if (!agent.enabled)
                agent.enabled = true;

            if (!agent.Warp(homePosition))
                Debug.LogWarning($"{name} has no NavMesh at its start position.", this);

            lit = false;
            banishedInCycle = -1;
            baseMode = director.BaseMode;
            releaseAtRoundTime = config.ReleaseDelay;
            hasDestination = false;

            SetState(config.StartsInHouse ? GhostState.InHouse : StateFor(baseMode), force: true);
            Freeze();
        }

        internal void Freeze()
        {
            frozen = true;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        internal void Resume()
        {
            frozen = false;

            if (agent.isOnNavMesh)
                agent.isStopped = false;

            // Spread the first decision over one interval so the ghosts don't all path on the same frame.
            nextThinkTime = Time.time + Random.Range(0f, director.AI.RepathInterval);
        }

        internal void Banish()
        {
            banishedInCycle = director.FrightenedCycle;
            SetState(GhostState.Eaten);
        }

        internal void RefreshSpeed() => ApplySpeed();

        public void SetLit(bool value)
        {
            if (lit == value)
                return;

            lit = value;
            ApplySpeed();
        }

        void OnModeChanged(GhostModeChange change)
        {
            if (director == null)
                return;

            bool inMaze = state == GhostState.Scatter || state == GhostState.Chase || state == GhostState.Frightened;

            if (change.Mode == GhostMode.Frightened)
            {
                // Ghosts still in the house get scared when they come out (see EnterMaze).
                if (inMaze)
                    SetState(GhostState.Frightened);
                return;
            }

            baseMode = change.Mode;
            if (inMaze)
                SetState(StateFor(baseMode));
        }

        void Think()
        {
            GhostMazeLayout layout = director.Layout;

            switch (state)
            {
                case GhostState.InHouse:
                    if (director.RoundTime >= releaseAtRoundTime)
                        SetState(GhostState.LeavingHouse);
                    break;

                case GhostState.LeavingHouse:
                    if (IsAt(layout.HouseExit))
                        EnterMaze();
                    else
                        MoveTo(layout.HouseExit);
                    break;

                case GhostState.Scatter:
                    MoveTo(Roam(layout.Corner(config.ScatterCorner)));
                    break;

                case GhostState.Chase:
                    MoveTo(director.HasPlayer ? ChaseTarget() : Roam(layout.Corner(config.ScatterCorner)));
                    break;

                case GhostState.Frightened:
                    MoveTo(FleeTarget());
                    break;

                case GhostState.Eaten:
                    if (IsAt(layout.HouseInside))
                        Revive();
                    else
                        MoveTo(layout.HouseInside);
                    break;
            }
        }

        Vector3 ChaseTarget()
        {
            Vector3 corner = director.Layout.Corner(config.ScatterCorner);
            Ghost partner = director.FindGhost(config.Partner);

            var context = new ChaseContext
            {
                Ghost = transform.position,
                Player = director.PlayerPosition,
                PlayerFacing = director.PlayerFacing,
                Corner = corner,
                Partner = partner != null ? partner.transform.position : null,
                TileSize = director.Layout.TileSize
            };

            // A shy Clyde roams his corner instead of standing on it.
            if (config.ChaseRule == ChaseRule.Clyde && GhostTargeting.IsShy(context, config.ShyDistanceTiles))
                return Roam(corner);

            return GhostTargeting.ChaseTarget(config.ChaseRule, config.AheadTiles, config.ShyDistanceTiles, context);
        }

        // Head for a point; once close, keep roaming around it like the originals circling their block.
        Vector3 Roam(Vector3 center)
        {
            if (hasRoamPoint && !IsAt(roamPoint))
                return roamPoint;

            float radius = director.AI.ScatterRoamTiles * director.Layout.TileSize;
            if (GhostTargeting.FlatDistance(transform.position, center) > radius)
            {
                hasRoamPoint = false;
                return center;
            }

            roamPoint = RandomPointNear(center, radius);
            hasRoamPoint = true;
            return roamPoint;
        }

        // Pick a reachable spot away from the player, and keep it until the ghost gets there
        // or the player ends up closer to that spot than the ghost is.
        Vector3 FleeTarget()
        {
            Vector3 position = transform.position;
            bool hasPlayer = director.HasPlayer;
            Vector3 player = director.PlayerPosition;

            if (hasRoamPoint && !IsAt(roamPoint) &&
                (!hasPlayer || GhostTargeting.FlatDistance(roamPoint, player) >= GhostTargeting.FlatDistance(position, player)))
                return roamPoint;

            float radius = director.AI.FleeTiles * director.Layout.TileSize;
            Vector3 best = position;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < director.AI.FleeCandidates; i++)
            {
                Vector3 candidate = RandomPointNear(position, radius);
                float score = hasPlayer ? GhostTargeting.FlatDistance(candidate, player) : Random.value;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            roamPoint = best;
            hasRoamPoint = true;
            return best;
        }

        void EnterMaze()
        {
            SetState(director.ShouldScare(banishedInCycle) ? GhostState.Frightened : StateFor(baseMode));
        }

        void Revive()
        {
            releaseAtRoundTime = director.RoundTime + director.AI.ReviveSeconds;
            SetState(GhostState.InHouse);
        }

        void MoveTo(Vector3 point)
        {
            aimPoint = point;

            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, director.AI.NavMeshSnapDistance, agent.areaMask))
                return;

            // Skip the path request when the target barely moved and the agent is already on its way.
            if (hasDestination && agent.hasPath && (hit.position - destination).sqrMagnitude < 0.04f)
                return;

            destination = hit.position;
            hasDestination = true;
            agent.SetDestination(destination);
            DestinationUpdates++;
        }

        Vector3 RandomPointNear(Vector3 center, float radius)
        {
            for (int i = 0; i < RandomPointAttempts; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, director.Layout.TileSize, agent.areaMask))
                    return hit.position;
            }

            return center;
        }

        bool IsAt(Vector3 point) => GhostTargeting.FlatDistance(transform.position, point) <= director.AI.ArriveDistance;

        void SetState(GhostState next, bool force = false)
        {
            if (state == next && !force)
                return;

            state = next;
            hasRoamPoint = false;
            ApplySpeed();

            // React on the next frame instead of waiting out the interval.
            nextThinkTime = Time.time;
            stateChanged.Invoke(state);
        }

        void ApplySpeed()
        {
            if (director == null)
                return;

            LevelConfig level = director.Level;
            float speed = state switch
            {
                GhostState.Frightened => level.FrightenedSpeed,
                GhostState.Eaten => level.EatenSpeed,
                _ => level.GhostSpeed
            };

            if (lit && CanBeLit)
                speed *= director.AI.LitSpeedMultiplier;

            agent.speed = speed;
        }

        static GhostState StateFor(GhostMode mode) => mode == GhostMode.Chase ? GhostState.Chase : GhostState.Scatter;

        // Collision Detection: a trigger on the ghost, so no per-frame distance checks.
        void OnTriggerEnter(Collider other) => CheckContact(other);

        // Also checked while overlapping, so a ghost that stops being frightened on top of the player still catches.
        void OnTriggerStay(Collider other) => CheckContact(other);

        void CheckContact(Collider other)
        {
            if (frozen || director == null)
                return;

            if (other.CompareTag(director.AI.PlayerTag))
                director.ReportContact(this);
        }

        // Debug: the raw aim point (wire sphere), where it landed on the NavMesh (solid) and the path.
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || !hasDestination)
                return;

            Color color = config != null ? config.Color : Color.white;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(aimPoint, 0.5f);
            Gizmos.DrawSphere(destination, 0.2f);

            if (agent == null || !agent.enabled || !agent.hasPath)
                return;

            Vector3[] corners = agent.path.corners;
            Vector3 lift = Vector3.up * 0.1f;
            for (int i = 1; i < corners.Length; i++)
                Gizmos.DrawLine(corners[i - 1] + lift, corners[i] + lift);
        }
    }
}
