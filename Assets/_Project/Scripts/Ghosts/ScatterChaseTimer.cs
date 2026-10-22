using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Ghosts
{
    // The global mode clock: Scatter and Chase alternate on the LevelConfig schedule, and a
    // flare overrides both with Frightened. Plain C# so it can be unit tested; GhostDirector
    // ticks it and broadcasts every change on the ModeChanged channel.
    public sealed class ScatterChaseTimer
    {
        IReadOnlyList<float> phases = Array.Empty<float>();
        int phaseIndex;
        float phaseLeft = float.PositiveInfinity;
        float frightenedLeft;

        // Even phases are Scatter, odd phases are Chase.
        public GhostMode BaseMode => phaseIndex % 2 == 0 ? GhostMode.Scatter : GhostMode.Chase;
        public bool IsFrightened => frightenedLeft > 0f;
        public GhostMode Mode => IsFrightened ? GhostMode.Frightened : BaseMode;
        public float FrightenedLeft => frightenedLeft;

        // Seconds left in the current mode, or 0 when it lasts until something else changes it.
        public float ModeSecondsLeft
        {
            get
            {
                if (IsFrightened)
                    return frightenedLeft;

                return float.IsPositiveInfinity(phaseLeft) ? 0f : phaseLeft;
            }
        }

        public GhostModeChange Current => new(Mode, ModeSecondsLeft);

        // Starts the schedule over from its first Scatter phase.
        public void Reset(IReadOnlyList<float> scatterChaseSeconds)
        {
            phases = scatterChaseSeconds ?? Array.Empty<float>();
            phaseIndex = 0;
            frightenedLeft = 0f;
            phaseLeft = PhaseLength(0);
            SkipFinishedPhases();
        }

        // A flare. A second flare while frightened restarts the countdown.
        public void StartFrightened(float seconds)
        {
            if (seconds > 0f)
                frightenedLeft = seconds;
        }

        // Advances the clock and returns true when Mode changed.
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return false;

            GhostMode before = Mode;

            // The Scatter/Chase clock is paused while ghosts are frightened, as in the original.
            if (IsFrightened)
            {
                frightenedLeft = Mathf.Max(0f, frightenedLeft - deltaTime);
            }
            else
            {
                phaseLeft -= deltaTime;
                SkipFinishedPhases();
            }

            return Mode != before;
        }

        void SkipFinishedPhases()
        {
            while (phaseLeft <= 0f)
            {
                phaseIndex++;
                phaseLeft += PhaseLength(phaseIndex);
            }
        }

        // Past the end of the schedule the next mode lasts forever.
        float PhaseLength(int index) => index < phases.Count ? Mathf.Max(0f, phases[index]) : float.PositiveInfinity;
    }
}
