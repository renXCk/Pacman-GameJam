using Game.Core;
using Game.Ghosts;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class GhostTargetingTests
    {
        const float Tile = 2f;

        static ChaseContext Context(Vector3 ghost, Vector3 player, Vector3 facing, Vector3? partner = null)
        {
            return new ChaseContext
            {
                Ghost = ghost,
                Player = player,
                PlayerFacing = facing,
                Corner = new Vector3(-30f, 0f, -30f),
                Partner = partner,
                TileSize = Tile
            };
        }

        static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");
        }

        [Test]
        public void BlinkyTargetsThePlayer()
        {
            var context = Context(new Vector3(30f, 0f, 30f), new Vector3(10f, 0f, 5f), Vector3.forward);

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Blinky, 0, 0f, context), new Vector3(10f, 0f, 5f));
        }

        [Test]
        public void PinkyTargetsTilesAheadOfWhereThePlayerFaces()
        {
            var context = Context(new Vector3(30f, 0f, 30f), Vector3.zero, Vector3.forward);

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Pinky, 4, 0f, context), new Vector3(0f, 0f, 8f));
        }

        [Test]
        public void PinkyIgnoresLookingUpOrDown()
        {
            // A first-person player looking up still faces +Z along the floor.
            var context = Context(new Vector3(30f, 0f, 30f), Vector3.zero, new Vector3(0f, 5f, 3f));

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Pinky, 4, 0f, context), new Vector3(0f, 0f, 8f));
        }

        [Test]
        public void PinkyTargetsThePlayerWhenThereIsNoFacing()
        {
            var context = Context(new Vector3(30f, 0f, 30f), new Vector3(4f, 0f, 4f), Vector3.up);

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Pinky, 4, 0f, context), new Vector3(4f, 0f, 4f));
        }

        [Test]
        public void InkyMirrorsBlinkyThroughThePointAheadOfThePlayer()
        {
            // Pivot is 2 tiles ahead: (0, 0, 4). Blinky at (2, 0, 0) mirrors to (-2, 0, 8).
            var context = Context(new Vector3(30f, 0f, 30f), Vector3.zero, Vector3.forward, new Vector3(2f, 0f, 0f));

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Inky, 2, 0f, context), new Vector3(-2f, 0f, 8f));
        }

        [Test]
        public void InkyWithoutAPartnerTargetsThePlayer()
        {
            var context = Context(new Vector3(30f, 0f, 30f), new Vector3(1f, 0f, 1f), Vector3.forward);

            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Inky, 2, 0f, context), new Vector3(1f, 0f, 1f));
        }

        [Test]
        public void ClydeChasesFromFarAway()
        {
            // 20 m away, shy distance 8 tiles = 16 m.
            var context = Context(new Vector3(20f, 0f, 0f), Vector3.zero, Vector3.forward);

            Assert.That(GhostTargeting.IsShy(context, 8f), Is.False);
            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Clyde, 0, 8f, context), Vector3.zero);
        }

        [Test]
        public void ClydeHeadsForHisCornerWhenClose()
        {
            var context = Context(new Vector3(10f, 0f, 0f), Vector3.zero, Vector3.forward);

            Assert.That(GhostTargeting.IsShy(context, 8f), Is.True);
            AssertNear(GhostTargeting.ChaseTarget(ChaseRule.Clyde, 0, 8f, context), context.Corner);
        }

        [Test]
        public void TargetsStayAtTheGhostsFloorHeight()
        {
            // The player's transform may sit above the floor; the target must not.
            var context = Context(new Vector3(30f, 0.1f, 30f), new Vector3(10f, 1.6f, 5f), Vector3.forward);

            Assert.That(GhostTargeting.ChaseTarget(ChaseRule.Blinky, 0, 0f, context).y, Is.EqualTo(0.1f).Within(1e-5));
        }
    }
}
