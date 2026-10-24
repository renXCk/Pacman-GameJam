using System;
using UnityEngine;

namespace Game.Core
{
    // How many power cells are left in the current maze. Dev A's maze calls Begin when it builds
    // or restores the cells and Collect for each one picked up; GameManager listens for Completed.
    // Neither side knows the other exists, and nobody has to count pickups in the scene.
    [CreateAssetMenu(fileName = "LevelProgress", menuName = "Game/Runtime/Level Progress")]
    public sealed class LevelProgress : ScriptableObject
    {
        [NonSerialized] readonly LevelProgressCounter counter = new();

        // Raised on Begin and on every Collect, for HUD counters or audio.
        public event Action<LevelProgress> Changed;

        // Raised once per Begin, when the last cell is collected.
        public event Action Completed;

        public int Total => counter.Total;
        public int Collected => counter.Collected;
        public int Remaining => counter.Remaining;
        public bool IsComplete => counter.IsComplete;

        // The project runs with domain reload off, so a runtime asset keeps its state between
        // Play sessions. Start clean whenever the asset is (re)loaded.
        void OnEnable() => counter.Begin(0);

        public void Begin(int totalCells)
        {
            counter.Begin(totalCells);
            Changed?.Invoke(this);
        }

        public void Collect()
        {
            bool finished = counter.Collect();
            Changed?.Invoke(this);

            if (finished)
                Completed?.Invoke();
        }
    }

    // The counting rules on their own, so they are unit tested without an asset.
    public sealed class LevelProgressCounter
    {
        public int Total { get; private set; }
        public int Collected { get; private set; }
        public int Remaining => Total - Collected;
        public bool IsComplete { get; private set; }

        // A total of 0 means no maze has been built yet; such a level never completes.
        public void Begin(int total)
        {
            Total = Mathf.Max(0, total);
            Collected = 0;
            IsComplete = false;
        }

        // Returns true only for the collect that finishes the level.
        public bool Collect()
        {
            if (IsComplete || Total == 0)
                return false;

            Collected++;
            if (Collected < Total)
                return false;

            IsComplete = true;
            return true;
        }
    }
}
