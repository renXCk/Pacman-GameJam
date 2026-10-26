using System;
using System.Collections.Generic;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Event-driven VFX manager managing zero-allocation pooled particle effects
    /// for flare bursts, cell collections, spectral ghost banishment, and death distortion.
    /// </summary>
    public sealed class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Event Channels - Dev C")]
        [SerializeField] PickupEventChannel pickupCollected;
        [SerializeField] VoidEventChannel flareCollected;
        [SerializeField] GhostBanishedEventChannel ghostBanished;
        [SerializeField] VoidEventChannel playerCaught;

        [Header("Scene References")]
        [SerializeField] TransformAnchor playerAnchor;

        [Header("VFX Particle Prefabs")]
        [Tooltip("High-intensity omnidirectional spark and smoke burst.")]
        [SerializeField] ParticleSystem flareIgnitionPrefab;

        [Tooltip("Subtle, non-obtrusive emissive implosion/sparkle at cell coordinates.")]
        [SerializeField] ParticleSystem cellCollectPrefab;

        [Tooltip("Spectral dissipation burst emitting the ghost's signature color.")]
        [SerializeField] ParticleSystem ghostBanishPrefab;

        [Tooltip("Immediate screen-space horror distortion particle burst.")]
        [SerializeField] ParticleSystem playerCaughtPrefab;

        [Header("Pool Allocations")]
        [Range(4, 32)]
        [SerializeField] int cellCollectPoolSize = 16;
        [Range(2, 12)]
        [SerializeField] int ghostBanishPoolSize = 6;
        [Range(1, 4)]
        [SerializeField] int flarePoolSize = 2;
        [Range(1, 4)]
        [SerializeField] int playerCaughtPoolSize = 2;

        struct PooledParticleSystem
        {
            public ParticleSystem System;
            public float ReturnTime;
        }

        PooledParticleSystem[] cellPool;
        PooledParticleSystem[] ghostBanishPool;
        PooledParticleSystem[] flarePool;
        PooledParticleSystem[] playerCaughtPool;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePools();
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
            if (pickupCollected != null) pickupCollected.Raised += OnPickupCollected;
            if (flareCollected != null) flareCollected.Raised += OnFlareCollected;
            if (ghostBanished != null) ghostBanished.Raised += OnGhostBanished;
            if (playerCaught != null) playerCaught.Raised += OnPlayerCaught;
        }

        void OnDisable()
        {
            if (pickupCollected != null) pickupCollected.Raised -= OnPickupCollected;
            if (flareCollected != null) flareCollected.Raised -= OnFlareCollected;
            if (ghostBanished != null) ghostBanished.Raised -= OnGhostBanished;
            if (playerCaught != null) playerCaught.Raised -= OnPlayerCaught;
        }

        #region Pool Setup

        void InitializePools()
        {
            GameObject container = new GameObject("[VFXPool]");
            container.transform.SetParent(transform);

            cellPool = CreatePool(cellCollectPrefab, cellCollectPoolSize, "CellCollectFX", container.transform);
            ghostBanishPool = CreatePool(ghostBanishPrefab, ghostBanishPoolSize, "GhostBanishFX", container.transform);
            flarePool = CreatePool(flareIgnitionPrefab, flarePoolSize, "FlareIgnitionFX", container.transform);
            playerCaughtPool = CreatePool(playerCaughtPrefab, playerCaughtPoolSize, "PlayerCaughtFX", container.transform);
        }

        PooledParticleSystem[] CreatePool(ParticleSystem prefab, int size, string namePrefix, Transform parent)
        {
            if (prefab == null) return Array.Empty<PooledParticleSystem>();

            var pool = new PooledParticleSystem[size];
            for (int i = 0; i < size; i++)
            {
                ParticleSystem instance = Instantiate(prefab, parent);
                instance.name = $"{namePrefix}_{i:00}";
                instance.gameObject.SetActive(false);

                pool[i] = new PooledParticleSystem
                {
                    System = instance,
                    ReturnTime = 0f
                };
            }
            return pool;
        }

        ParticleSystem AcquireFromPool(PooledParticleSystem[] pool)
        {
            if (pool == null || pool.Length == 0) return null;

            float currentTime = Time.time;

            // 1. First priority: find inactive / completed particle system
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].System.isPlaying || currentTime >= pool[i].ReturnTime)
                {
                    pool[i].ReturnTime = currentTime + pool[i].System.main.duration;
                    return pool[i].System;
                }
            }

            // 2. Steal the oldest instance if pool is exhausted
            int oldestIndex = 0;
            float lowestEndTime = float.MaxValue;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].ReturnTime < lowestEndTime)
                {
                    lowestEndTime = pool[i].ReturnTime;
                    oldestIndex = i;
                }
            }

            pool[oldestIndex].System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pool[oldestIndex].ReturnTime = currentTime + pool[oldestIndex].System.main.duration;
            return pool[oldestIndex].System;
        }

        #endregion

        #region Public Playback API

        /// <summary>
        /// Plays a subtle, non-obtrusive cell collection sparkle at specified coordinates.
        /// </summary>
        public void PlayCellCollect(Vector3 worldPosition)
        {
            ParticleSystem ps = AcquireFromPool(cellPool);
            if (ps == null) return;

            ps.transform.position = worldPosition;
            ps.gameObject.SetActive(true);
            ps.Play();
        }

        /// <summary>
        /// Detonates an intense omnidirectional flare spark burst.
        /// </summary>
        public void PlayFlareIgnition(Vector3 worldPosition)
        {
            ParticleSystem ps = AcquireFromPool(flarePool);
            if (ps == null) return;

            ps.transform.position = worldPosition;
            ps.gameObject.SetActive(true);
            ps.Play();
        }

        /// <summary>
        /// Plays a spectral banish dissipation burst tinted with the ghost's signature color.
        /// </summary>
        public void PlayGhostBanish(Vector3 worldPosition, Color ghostColor)
        {
            ParticleSystem ps = AcquireFromPool(ghostBanishPool);
            if (ps == null) return;

            ps.transform.position = worldPosition;

            // Apply ghost's signature emissive color to the particle emitter
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(ghostColor);

            ps.gameObject.SetActive(true);
            ps.Play();
        }

        /// <summary>
        /// Triggers camera-aligned horror screen distortion on death.
        /// </summary>
        public void PlayPlayerCaught(Vector3 position, Quaternion rotation)
        {
            ParticleSystem ps = AcquireFromPool(playerCaughtPool);
            if (ps == null) return;

            ps.transform.SetPositionAndRotation(position, rotation);
            ps.gameObject.SetActive(true);
            ps.Play();
        }

        #endregion

        #region Event Callbacks

        void OnPickupCollected(PickupConfig config)
        {
            if (config == null) return;

            Vector3 spawnPos = (playerAnchor != null && playerAnchor.IsSet) 
                ? playerAnchor.Value.position + playerAnchor.Value.forward * 0.8f + Vector3.up * 0.2f 
                : ((Camera.main != null) ? Camera.main.transform.position + Camera.main.transform.forward * 0.8f : Vector3.zero);

            PlayCellCollect(spawnPos);
        }

        void OnFlareCollected()
        {
            Vector3 spawnPos = (playerAnchor != null && playerAnchor.IsSet)
                ? playerAnchor.Value.position
                : ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);

            PlayFlareIgnition(spawnPos);
        }

        void OnGhostBanished(GhostBanishedInfo info)
        {
            Color ghostColor = (info.Ghost != null) ? info.Ghost.Color : Color.cyan;
            PlayGhostBanish(info.Position, ghostColor);
        }

        void OnPlayerCaught()
        {
            Vector3 camPos = (Camera.main != null) ? Camera.main.transform.position + Camera.main.transform.forward * 0.4f : Vector3.zero;
            Quaternion camRot = (Camera.main != null) ? Camera.main.transform.rotation : Quaternion.identity;

            PlayPlayerCaught(camPos, camRot);
        }

        #endregion
    }
}
