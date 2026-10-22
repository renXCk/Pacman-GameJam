using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    // Per-level tuning. One asset per level; each replay uses a harder one.
    // Only the ghost section is filled in so far. Dev C owns this asset and adds the rest
    // (battery bonus per level, and anything else GameManager needs).
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Game/Configs/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Ghost speeds (m/s)")]
        [Tooltip("Speed in Scatter and Chase.")]
        [Min(0f)] [SerializeField] float ghostSpeed = 3.8f;

        [Tooltip("Speed while frightened by a flare.")]
        [Min(0f)] [SerializeField] float frightenedSpeed = 2.2f;

        [Tooltip("Speed of a banished ghost's eyes on the way back to the house.")]
        [Min(0f)] [SerializeField] float eatenSpeed = 8f;

        [Header("Ghost mode timer")]
        [Tooltip("Seconds per phase, alternating Scatter, Chase, Scatter, ... starting with Scatter. " +
                 "After the last entry the next mode lasts forever.")]
        [SerializeField] float[] scatterChaseSeconds = { 7f, 20f, 7f, 20f, 5f };

        [Header("Flare")]
        [Tooltip("How long ghosts stay frightened after a flare (the stun time).")]
        [Min(0f)] [SerializeField] float frightenedSeconds = 6f;

        [Tooltip("Ghosts flash for this many seconds before they recover.")]
        [Min(0f)] [SerializeField] float frightenedWarningSeconds = 2f;

        public float GhostSpeed => ghostSpeed;
        public float FrightenedSpeed => frightenedSpeed;
        public float EatenSpeed => eatenSpeed;
        public IReadOnlyList<float> ScatterChaseSeconds => scatterChaseSeconds;
        public float FrightenedSeconds => frightenedSeconds;
        public float FrightenedWarningSeconds => frightenedWarningSeconds;
    }
}
