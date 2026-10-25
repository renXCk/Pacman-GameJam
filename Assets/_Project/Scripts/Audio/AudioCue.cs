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

        /// <summary>
        /// Selects an audio clip at random, avoiding immediate repetition when multiple clips are provided.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0)
                return null;

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
