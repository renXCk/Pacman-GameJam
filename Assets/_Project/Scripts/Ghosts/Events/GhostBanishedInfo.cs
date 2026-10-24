using Game.Core;
using UnityEngine;

namespace Game.Ghosts
{
    // Payload of the GhostBanished channel.
    public readonly struct GhostBanishedInfo
    {
        public readonly GhostConfig Ghost;

        // 1 for the first ghost banished with this flare, 2 for the second, and so on.
        public readonly int Combo;

        // Points for this banish (200, 400, 800, 1600 by default). Score adds these as they are.
        public readonly int Points;

        public readonly Vector3 Position;

        public GhostBanishedInfo(GhostConfig ghost, int combo, int points, Vector3 position)
        {
            Ghost = ghost;
            Combo = combo;
            Points = points;
            Position = position;
        }

        public override string ToString() => $"{(Ghost != null ? Ghost.DisplayName : "ghost")} x{Combo} +{Points}";
    }
}
