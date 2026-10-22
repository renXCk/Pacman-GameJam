using Game.Core;
using UnityEngine;

namespace Game.Ghosts
{
    // Everything a Chase rule needs to know, in world space.
    public struct ChaseContext
    {
        public Vector3 Ghost;
        public Vector3 Player;
        public Vector3 PlayerFacing;  // any length; only the horizontal direction is used
        public Vector3 Corner;        // this ghost's scatter corner
        public Vector3? Partner;      // Inky's partner (Blinky), when it is in play
        public float TileSize;
    }

    // The four Chase-mode targeting rules from the original, as pure math.
    // Ghost snaps the result onto the NavMesh afterwards.
    public static class GhostTargeting
    {
        public static Vector3 ChaseTarget(ChaseRule rule, int aheadTiles, float shyDistanceTiles, in ChaseContext context)
        {
            Vector3 target = rule switch
            {
                ChaseRule.Pinky => Ahead(context, aheadTiles),
                ChaseRule.Inky => context.Partner.HasValue ? Mirror(context.Partner.Value, Ahead(context, aheadTiles)) : context.Player,
                ChaseRule.Clyde => IsShy(context, shyDistanceTiles) ? context.Corner : context.Player,
                _ => context.Player
            };

            // Keep the target at the ghost's floor height so NavMesh sampling finds the corridor.
            target.y = context.Ghost.y;
            return target;
        }

        // Clyde gives up the chase when he gets close to the player.
        public static bool IsShy(in ChaseContext context, float shyDistanceTiles) =>
            FlatDistance(context.Ghost, context.Player) < shyDistanceTiles * context.TileSize;

        // A point some tiles ahead of where the player is facing.
        public static Vector3 Ahead(in ChaseContext context, int tiles) =>
            context.Player + Flat(context.PlayerFacing) * (tiles * context.TileSize);

        public static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Inky: double the vector from the partner to the pivot point.
        static Vector3 Mirror(Vector3 partner, Vector3 pivot) => pivot + (pivot - partner);
    }
}
