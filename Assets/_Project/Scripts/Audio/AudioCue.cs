using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// AudioCue encapsulates sound variations, mixer routing, randomized dynamics, 
    /// and spatial properties into a reusable ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCue_New", menuName = "Game/Audio/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("Array of audio clips for variation. A clip is selected at random upon playback.")]
        [SerializeField] AudioClip[] clips = Array.Empty<AudioClip>();

        [Header("Audio Mixer Routing")]
        [Tooltip("Target AudioMixerGroup for this cue.")]
        [SerializeField] AudioMixerGroup outputGroup;

        [Header("Dynamics & Randomization")]
        [Tooltip("Random volume range [X = min, Y = max].")]
        [SerializeField] Vector2 volumeRange = new Vector2(0.9f, 1.0f);

        [Tooltip("Random pitch range [X = min, Y = max].")]
        [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Whether the audio source should loop indefinitely.")]
        [SerializeField] bool loop = false;

        [Header("3D Spatial Settings (Default Fallbacks)")]
        [Range(0f, 1f)]
        [Tooltip("0 = 2D (headphone stereo), 1 = 3D (spatial world position).")]
        [SerializeField] float spatialBlend = 0f;

        [Min(0.1f)]
        [SerializeField] float minDistance = 1f;

        [Min(1f)]
        [SerializeField] float maxDistance = 25f;

        [SerializeField] AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        // Tracks last played clip index to prevent back-to-back duplicate clips when >= 2 clips exist.
        [NonSerialized] int lastClipIndex = -1;

        public AudioClip[] Clips => clips;
        public AudioMixerGroup OutputGroup => outputGroup;
        public Vector2 VolumeRange => volumeRange;
        public Vector2 PitchRange => pitchRange;
        public bool Loop => loop;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public AudioRolloffMode RolloffMode => rolloffMode;

        static readonly System.Collections.Generic.Dictionary<string, AudioClip> FallbackClipCache = new();

        /// <summary>
        /// Selects an audio clip at random, avoiding immediate repetition when multiple clips are provided.
        /// If no audio clips are configured, synthesizes an authentic procedural arcade-horror waveform.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (clips != null && clips.Length > 0)
            {
                if (clips.Length == 1)
                    return clips[0];

                int index;
                int attempts = 0;
                do
                {
                    index = UnityEngine.Random.Range(0, clips.Length);
                    attempts++;
                } while (index == lastClipIndex && attempts < 5);

                lastClipIndex = index;
                return clips[index];
            }

            return GetProceduralFallbackClip();
        }

        AudioClip GetProceduralFallbackClip()
        {
            string cueKey = string.IsNullOrEmpty(name) ? "AudioCue_Default" : name;
            if (FallbackClipCache.TryGetValue(cueKey, out AudioClip cached) && cached != null)
                return cached;

            const int SampleRate = 44100;
            float duration = 0.25f;
            Func<float, float> waveFunc;

            if (cueKey.Contains("Cell") || cueKey.Contains("Waka"))
            {
                duration = 0.12f;
                waveFunc = t =>
                {
                    float progress = t / 0.12f;
                    float freq = Mathf.Lerp(340f, 180f, progress);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Sin(progress * Mathf.PI) * 0.7f;
                };
            }
            else if (cueKey.Contains("Scatter") || cueKey.Contains("Chase"))
            {
                duration = 0.5f;
                waveFunc = t =>
                {
                    float progress = t / 0.5f;
                    float freq = Mathf.Lerp(220f, 440f, Mathf.PingPong(progress * 2f, 1f));
                    float phase = 2f * Mathf.PI * freq * t;
                    return (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase)) * 0.5f;
                };
            }
            else if (cueKey.Contains("Frightened"))
            {
                duration = 0.25f;
                waveFunc = t =>
                {
                    float freq = 600f + 150f * Mathf.Sin(2f * Mathf.PI * 12f * t);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                };
            }
            else if (cueKey.Contains("Drone") || cueKey.Contains("Ambient"))
            {
                duration = 4.0f;
                waveFunc = t =>
                {
                    float sub = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f;
                    float harmonic = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.2f;
                    float beat = Mathf.Sin(2f * Mathf.PI * 57.5f * t) * 0.25f;
                    float lfo = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                    return (sub + harmonic + beat) * lfo * 0.6f;
                };
            }
            else if (cueKey.Contains("Ready"))
            {
                duration = 2.4f;
                float[] notes = { 493.88f, 987.77f, 739.99f, 622.25f, 987.77f, 739.99f, 622.25f };
                float noteLen = 0.32f;
                waveFunc = t =>
                {
                    int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                    float localT = t % noteLen;
                    float env = Mathf.Exp(-localT * 7.5f);
                    return Mathf.Sin(2f * Mathf.PI * notes[idx] * localT) * env * 0.6f;
                };
            }
            else if (cueKey.Contains("Battery"))
            {
                duration = 0.4f;
                waveFunc = t =>
                {
                    float progress = t / 0.4f;
                    float freq = Mathf.Lerp(523.25f, 1046.50f, progress);
                    float env = Mathf.Exp(-progress * 5f);
                    return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.55f;
                };
            }
            else if (cueKey.Contains("Flare"))
            {
                duration = 1.0f;
                waveFunc = t =>
                {
                    float progress = t / 1.0f;
                    float env = Mathf.Exp(-progress * 4f);
                    float noise = (Mathf.Sin(t * 14125.7f) * 43758.5f % 1f - 0.5f) * 1.2f;
                    float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 40f, progress) * t) * 0.7f;
                    return (noise * 0.5f + sub) * env * 0.7f;
                };
            }
            else if (cueKey.Contains("Banished"))
            {
                duration = 0.8f;
                waveFunc = t =>
                {
                    float progress = t / 0.8f;
                    float freq = Mathf.Lerp(880f, 180f, progress * progress);
                    float env = Mathf.Exp(-progress * 4f);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Sin(2f * Mathf.PI * 45f * t) * env * 0.75f;
                };
            }
            else if (cueKey.Contains("Caught"))
            {
                duration = 1.2f;
                waveFunc = t =>
                {
                    float progress = t / 1.2f;
                    float env = Mathf.Exp(-progress * 3.5f);
                    float diss = Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 932.33f * t);
                    float impact = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f;
                    return (diss * 0.4f + impact) * env * 0.8f;
                };
            }
            else if (cueKey.Contains("LevelComplete"))
            {
                duration = 2.0f;
                float[] notes = { 261.63f, 329.63f, 392f, 523.25f, 659.25f, 783.99f, 1046.50f };
                float noteLen = 0.28f;
                waveFunc = t =>
                {
                    int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                    float localT = t % noteLen;
                    float env = Mathf.Exp(-localT * 3f);
                    return Mathf.Sin(2f * Mathf.PI * notes[idx] * localT) * env * 0.6f;
                };
            }
            else if (cueKey.Contains("GameOver"))
            {
                duration = 2.0f;
                float[] notes = { 261.63f, 207.65f, 174.61f, 130.81f };
                float noteLen = 0.45f;
                waveFunc = t =>
                {
                    int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                    float localT = t % noteLen;
                    float env = Mathf.Exp(-localT * 4f);
                    return Mathf.Sin(2f * Mathf.PI * notes[idx] * localT) * env * 0.6f;
                };
            }
            else if (cueKey.Contains("ExtraLife"))
            {
                duration = 0.35f;
                waveFunc = t =>
                {
                    float freq = (t < 0.175f) ? 784f : 1046.50f;
                    float env = Mathf.Exp(-(t % 0.175f) * 8f);
                    return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
                };
            }
            else
            {
                duration = 0.15f;
                waveFunc = t => Mathf.Sin(2f * Mathf.PI * 440f * t) * Mathf.Exp(-t * 10f) * 0.5f;
            }

            int totalSamples = (int)(duration * SampleRate);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                samples[i] = Mathf.Clamp(waveFunc(t), -1f, 1f);
            }

            AudioClip synthClip = AudioClip.Create($"Procedural_{cueKey}", totalSamples, 1, SampleRate, false);
            synthClip.SetData(samples, 0);
            FallbackClipCache[cueKey] = synthClip;
            return synthClip;
        }

        /// <summary>
        /// Configures the provided AudioSource with this cue's properties and initiates playback.
        /// </summary>
        public void Play(AudioSource source, float pitchMultiplier = 1f, float volumeMultiplier = 1f)
        {
            if (source == null) return;

            AudioClip clip = GetRandomClip();
            if (clip == null) return;

            source.clip = clip;
            source.outputAudioMixerGroup = outputGroup;
            source.volume = UnityEngine.Random.Range(volumeRange.x, volumeRange.y) * Mathf.Clamp01(volumeMultiplier);
            source.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y) * Mathf.Max(0.1f, pitchMultiplier);
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = rolloffMode;

            source.Play();
        }
    }
}
