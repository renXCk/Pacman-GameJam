using System;
using System.Collections;
using Game.Core;
using Game.Events;
using Game.Ghosts;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// Core audio manager implementing zero-allocation spatial audio pooling, 
    /// dynamic arcade horror ambient drones/sirens, and event-driven SFX playback.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Persistence")]
        [Tooltip("Preserve across scene reloads if spawned at root.")]
        [SerializeField] bool isPersistent = false;

        [Header("Listens to - Dev C Event Channels")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] GhostModeEventChannel modeChanged;
        [SerializeField] PickupEventChannel pickupCollected;
        [SerializeField] VoidEventChannel flareCollected;
        [SerializeField] GhostBanishedEventChannel ghostBanished;
        [SerializeField] VoidEventChannel playerCaught;
        [SerializeField] VoidEventChannel levelComplete;
        [SerializeField] BoolEventChannel pauseChanged;
        [SerializeField] VoidEventChannel extraLifeAwarded;

        [Header("Ambient & Siren Audio Cues")]
        [SerializeField] AudioCue ambientDroneCue;
        [SerializeField] AudioCue scatterChaseSirenCue;
        [SerializeField] AudioCue frightenedSirenCue;
        [SerializeField] AudioCue readyJingleCue;
        [SerializeField] AudioCue gameOverCue;

        [Header("Gameplay SFX Audio Cues")]
        [SerializeField] AudioCue cellCollectCue;
        [SerializeField] AudioCue batteryCollectCue;
        [SerializeField] AudioCue flareIgniteCue;
        [SerializeField] AudioCue ghostBanishedCue;
        [SerializeField] AudioCue playerCaughtCue;
        [SerializeField] AudioCue levelCompleteCue;
        [SerializeField] AudioCue extraLifeCue;

        [Header("Siren & Transition Tuning")]
        [Range(0.1f, 3f)]
        [SerializeField] float sirenCrossfadeDuration = 0.5f;
        [Range(0.1f, 3f)]
        [SerializeField] float ambientFadeDuration = 1.0f;

        [Header("3D Spatial Audio Pool")]
        [Range(4, 32)]
        [SerializeField] int spatialPoolSize = 16;
        [SerializeField] AudioMixerGroup defaultSfxGroup;

        // Dedicated audio sources for background and stingers
        AudioSource ambientSource;
        AudioSource sirenSourceA;
        AudioSource sirenSourceB;
        AudioSource stingerSource;
        bool activeSirenIsA = true;

        // Spatial Audio Source Pool
        struct PooledSource
        {
            public AudioSource Source;
            public float PlaybackEndTime;
        }
        PooledSource[] pool;

        // Runtime states
        GameState currentGameState = GameState.Boot;
        GhostMode currentGhostMode = GhostMode.Scatter;
        bool isWakaStepA = false;
        Coroutine sirenFadeCoroutine;
        Coroutine ambientFadeCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (isPersistent && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (AudioListener.volume <= 0.001f)
            {
                AudioListener.volume = 0.85f;
            }

            InitializeSources();
            InitializePool();
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
            if (pickupCollected != null) pickupCollected.Raised += OnPickupCollected;
            if (flareCollected != null) flareCollected.Raised += OnFlareCollected;
            if (ghostBanished != null) ghostBanished.Raised += OnGhostBanished;
            if (playerCaught != null) playerCaught.Raised += OnPlayerCaught;
            if (levelComplete != null) levelComplete.Raised += OnLevelComplete;
            if (pauseChanged != null) pauseChanged.Raised += OnPauseChanged;
            if (extraLifeAwarded != null) extraLifeAwarded.Raised += OnExtraLifeAwarded;
        }

        void OnDisable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (modeChanged != null) modeChanged.Raised -= OnModeChanged;
            if (pickupCollected != null) pickupCollected.Raised -= OnPickupCollected;
            if (flareCollected != null) flareCollected.Raised -= OnFlareCollected;
            if (ghostBanished != null) ghostBanished.Raised -= OnGhostBanished;
            if (playerCaught != null) playerCaught.Raised -= OnPlayerCaught;
            if (levelComplete != null) levelComplete.Raised -= OnLevelComplete;
            if (pauseChanged != null) pauseChanged.Raised -= OnPauseChanged;
            if (extraLifeAwarded != null) extraLifeAwarded.Raised -= OnExtraLifeAwarded;
        }

        #region Initialization

        void InitializeSources()
        {
            ambientSource = CreateChildSource("AmbientDrone_Source", spatialBlend: 0f, loop: true);
            sirenSourceA = CreateChildSource("Siren_Source_A", spatialBlend: 0f, loop: true);
            sirenSourceB = CreateChildSource("Siren_Source_B", spatialBlend: 0f, loop: true);
            stingerSource = CreateChildSource("Stinger_Source", spatialBlend: 0f, loop: false);
        }

        void InitializePool()
        {
            GameObject poolContainer = new GameObject("[SpatialAudioPool]");
            poolContainer.transform.SetParent(transform);

            pool = new PooledSource[spatialPoolSize];
            for (int i = 0; i < spatialPoolSize; i++)
            {
                GameObject sourceObj = new GameObject($"SpatialSource_{i:00}");
                sourceObj.transform.SetParent(poolContainer.transform);

                AudioSource src = sourceObj.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1.0f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 1.0f;
                src.maxDistance = 25.0f;
                src.dopplerLevel = 0.0f;
                src.outputAudioMixerGroup = defaultSfxGroup;

                pool[i] = new PooledSource
                {
                    Source = src,
                    PlaybackEndTime = 0f
                };
            }
        }

        AudioSource CreateChildSource(string name, float spatialBlend, bool loop)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatialBlend;
            src.loop = loop;
            src.dopplerLevel = 0f;
            return src;
        }

        #endregion

        #region Spatial & 2D Audio Pooling API

        /// <summary>
        /// Plays a sound cue positioned in 3D world space using a pre-allocated AudioSource from the pool.
        /// Eliminates runtime GC allocation.
        /// </summary>
        public AudioSource PlaySpatial(AudioCue cue, Vector3 position, float pitchMultiplier = 1f, float volumeMultiplier = 1f)
        {
            if (cue == null) return null;

            AudioSource source = AcquirePooledSource();
            if (source == null) return null;

            source.transform.position = position;
            cue.Play(source, pitchMultiplier, volumeMultiplier);

            // Record playback duration to allow intelligent source stealing if the pool is exhausted
            float duration = (source.clip != null) ? (source.clip.length / Mathf.Max(0.1f, source.pitch)) : 1f;
            UpdateSourceEndTime(source, Time.time + duration);

            return source;
        }

        /// <summary>
        /// Plays a sound cue in 2D space (stereo) using the pool.
        /// </summary>
        public AudioSource Play2D(AudioCue cue, float pitchMultiplier = 1f, float volumeMultiplier = 1f)
        {
            if (cue == null) return null;

            AudioSource source = AcquirePooledSource();
            if (source == null) return null;

            cue.Play(source, pitchMultiplier, volumeMultiplier);
            source.spatialBlend = 0f;

            float duration = (source.clip != null) ? (source.clip.length / Mathf.Max(0.1f, source.pitch)) : 1f;
            UpdateSourceEndTime(source, Time.time + duration);

            return source;
        }

        AudioSource AcquirePooledSource()
        {
            if (pool == null || pool.Length == 0) return null;

            float currentTime = Time.time;

            // 1. First priority: Find an idle source that finished playback
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].Source.isPlaying || currentTime >= pool[i].PlaybackEndTime)
                {
                    return pool[i].Source;
                }
            }

            // 2. Fallback: Steal the source whose playback will end soonest
            int bestIndex = 0;
            float lowestEndTime = float.MaxValue;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].PlaybackEndTime < lowestEndTime)
                {
                    lowestEndTime = pool[i].PlaybackEndTime;
                    bestIndex = i;
                }
            }

            pool[bestIndex].Source.Stop();
            return pool[bestIndex].Source;
        }

        void UpdateSourceEndTime(AudioSource source, float endTime)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].Source == source)
                {
                    pool[i].PlaybackEndTime = endTime;
                    return;
                }
            }
        }

        #endregion

        #region Event Callbacks

        void OnGameStateChanged(GameState newState)
        {
            currentGameState = newState;

            switch (newState)
            {
                case GameState.Boot:
                case GameState.Menu:
                    StopSirensImmediate();
                    FadeOutAmbient(0.5f);
                    break;

                case GameState.Ready:
                    StopSirensImmediate();
                    if (readyJingleCue != null)
                    {
                        readyJingleCue.Play(stingerSource);
                    }
                    FadeInAmbient(1.5f);
                    break;

                case GameState.Playing:
                    if (!ambientSource.isPlaying)
                    {
                        FadeInAmbient(0.8f);
                    }
                    UpdateSirenForMode(currentGhostMode, instant: false);
                    break;

                case GameState.PlayerCaught:
                    StopSirensImmediate();
                    FadeOutAmbient(0.1f);
                    break;

                case GameState.LevelComplete:
                    StopSirensImmediate();
                    FadeOutAmbient(0.5f);
                    break;

                case GameState.GameOver:
                    StopSirensImmediate();
                    FadeOutAmbient(0.5f);
                    if (gameOverCue != null)
                    {
                        gameOverCue.Play(stingerSource);
                    }
                    break;
            }
        }

        void OnModeChanged(GhostModeChange modeChange)
        {
            currentGhostMode = modeChange.Mode;

            if (currentGameState == GameState.Playing)
            {
                UpdateSirenForMode(currentGhostMode, instant: false);
            }
        }

        void OnPickupCollected(PickupConfig config)
        {
            if (config == null) return;

            switch (config.Kind)
            {
                case PickupKind.Cell:
                    // Classic Pac-Man waka cadence: alternate pitch slightly between consecutive cells
                    float pitch = isWakaStepA ? 1.05f : 0.95f;
                    isWakaStepA = !isWakaStepA;
                    Play2D(cellCollectCue, pitchMultiplier: pitch);
                    break;

                case PickupKind.Battery:
                    Play2D(batteryCollectCue);
                    break;
            }
        }

        void OnFlareCollected()
        {
            Play2D(flareIgniteCue);
        }

        void OnGhostBanished(GhostBanishedInfo info)
        {
            // Classic arcade exponential combo pitch scaling:
            // Combo 1 (200 pts)  -> 1.00x
            // Combo 2 (400 pts)  -> 1.25x
            // Combo 3 (800 pts)  -> 1.50x
            // Combo 4 (1600 pts) -> 1.75x
            float comboPitch = 1.0f + Mathf.Clamp(info.Combo - 1, 0, 3) * 0.25f;

            PlaySpatial(ghostBanishedCue, info.Position, pitchMultiplier: comboPitch);
        }

        void OnPlayerCaught()
        {
            if (playerCaughtCue != null)
            {
                playerCaughtCue.Play(stingerSource);
            }
        }

        void OnLevelComplete()
        {
            if (levelCompleteCue != null)
            {
                levelCompleteCue.Play(stingerSource);
            }
        }

        void OnPauseChanged(bool isPaused)
        {
            if (isPaused)
            {
                if (ambientSource.isPlaying) ambientSource.Pause();
                if (sirenSourceA.isPlaying) sirenSourceA.Pause();
                if (sirenSourceB.isPlaying) sirenSourceB.Pause();
                if (stingerSource.isPlaying) stingerSource.Pause();

                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].Source.isPlaying)
                        pool[i].Source.Pause();
                }
            }
            else
            {
                ambientSource.UnPause();
                sirenSourceA.UnPause();
                sirenSourceB.UnPause();
                stingerSource.UnPause();

                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i].Source.UnPause();
                }
            }
        }

        void OnExtraLifeAwarded()
        {
            Play2D(extraLifeCue);
        }

        #endregion

        #region Siren & Ambient Dynamic Modulation

        void UpdateSirenForMode(GhostMode mode, bool instant)
        {
            AudioCue targetCue = (mode == GhostMode.Frightened) ? frightenedSirenCue : scatterChaseSirenCue;
            if (targetCue == null) return;

            if (sirenFadeCoroutine != null)
            {
                StopCoroutine(sirenFadeCoroutine);
            }

            if (instant)
            {
                AudioSource active = activeSirenIsA ? sirenSourceA : sirenSourceB;
                AudioSource inactive = activeSirenIsA ? sirenSourceB : sirenSourceA;

                targetCue.Play(active);
                inactive.Stop();
            }
            else
            {
                sirenFadeCoroutine = StartCoroutine(CrossfadeSirenRoutine(targetCue, sirenCrossfadeDuration));
            }
        }

        IEnumerator CrossfadeSirenRoutine(AudioCue newCue, float duration)
        {
            AudioSource currentSource = activeSirenIsA ? sirenSourceA : sirenSourceB;
            AudioSource nextSource = activeSirenIsA ? sirenSourceB : sirenSourceA;
            activeSirenIsA = !activeSirenIsA;

            newCue.Play(nextSource);
            float targetVolume = nextSource.volume;
            nextSource.volume = 0f;

            float startVolume = currentSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                nextSource.volume = Mathf.Lerp(0f, targetVolume, t);
                currentSource.volume = Mathf.Lerp(startVolume, 0f, t);

                yield return null;
            }

            nextSource.volume = targetVolume;
            currentSource.Stop();
            currentSource.volume = startVolume;
            sirenFadeCoroutine = null;
        }

        void StopSirensImmediate()
        {
            if (sirenFadeCoroutine != null)
            {
                StopCoroutine(sirenFadeCoroutine);
                sirenFadeCoroutine = null;
            }

            sirenSourceA.Stop();
            sirenSourceB.Stop();
        }

        void FadeInAmbient(float duration)
        {
            if (ambientDroneCue == null) return;

            if (ambientFadeCoroutine != null)
            {
                StopCoroutine(ambientFadeCoroutine);
            }

            ambientFadeCoroutine = StartCoroutine(FadeInAmbientRoutine(duration));
        }

        void FadeOutAmbient(float duration)
        {
            if (ambientFadeCoroutine != null)
            {
                StopCoroutine(ambientFadeCoroutine);
            }

            ambientFadeCoroutine = StartCoroutine(FadeOutAmbientRoutine(duration));
        }

        IEnumerator FadeInAmbientRoutine(float duration)
        {
            ambientDroneCue.Play(ambientSource);
            float targetVolume = ambientSource.volume;
            ambientSource.volume = 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ambientSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                yield return null;
            }

            ambientSource.volume = targetVolume;
            ambientFadeCoroutine = null;
        }

        IEnumerator FadeOutAmbientRoutine(float duration)
        {
            float startVolume = ambientSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ambientSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            ambientSource.Stop();
            ambientSource.volume = startVolume;
            ambientFadeCoroutine = null;
        }

        #endregion
    }
}
