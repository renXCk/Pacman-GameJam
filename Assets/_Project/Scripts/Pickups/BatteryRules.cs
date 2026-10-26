using System;
using UnityEngine;

namespace Game.Pickups
{
    // Task A7: when a battery appears. Like the fruit in the original game, a battery shows up
    // once the player has cleared a given share of the level. This class is only the counting;
    // BatteryDirector acts on it. Plain C#, so it is unit tested without a scene.
    public sealed class BatteryRules
    {
        readonly float[] thresholds;
        int used;

        // The fractions (0 to 1) of the level's cells and flares at which a battery appears,
        // in any order. Values are kept between 0 and 1.
        public BatteryRules(float[] spawnAtFractions)
        {
            if (spawnAtFractions == null)
                spawnAtFractions = new float[0];

            thresholds = new float[spawnAtFractions.Length];
            for (int i = 0; i < thresholds.Length; i++)
                thresholds[i] = Mathf.Clamp01(spawnAtFractions[i]);

            Array.Sort(thresholds);
        }

        public int ThresholdCount => thresholds.Length;

        // How many batteries have been handed out since Begin.
        public int Spawned => used;

        // A new level: every threshold counts again.
        public void Begin() => used = 0;

        // Cells and flares needed for a threshold. At least one, so a battery never appears
        // before the player has taken anything. The small subtraction keeps a share like 0.2 of 10
        // from rounding up to 3 because 0.2 is not exact in floating point.
        public static int Required(float fraction, int total) =>
            Mathf.Max(1, Mathf.CeilToInt(Mathf.Clamp01(fraction) * total - 0.0001f));

        // How many thresholds were crossed since the last call. Each threshold pays out once.
        public int TakeDue(int collected, int total)
        {
            if (total <= 0)
                return 0;

            int due = 0;
            while (used < thresholds.Length && collected >= Required(thresholds[used], total))
            {
                used++;
                due++;
            }

            return due;
        }
    }
}
