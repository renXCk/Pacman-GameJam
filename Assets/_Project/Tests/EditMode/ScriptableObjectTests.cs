using Game.Core;
using Game.Events;
using Game.Ghosts;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class ScriptableObjectTests
    {
        [Test]
        public void BanishComboPointsDoubleThenStayAtTheTop()
        {
            var ai = ScriptableObject.CreateInstance<GhostAIConfig>();

            Assert.That(ai.BanishPoints(1), Is.EqualTo(200));
            Assert.That(ai.BanishPoints(2), Is.EqualTo(400));
            Assert.That(ai.BanishPoints(3), Is.EqualTo(800));
            Assert.That(ai.BanishPoints(4), Is.EqualTo(1600));
            Assert.That(ai.BanishPoints(5), Is.EqualTo(1600));

            Object.DestroyImmediate(ai);
        }

        [Test]
        public void VoidChannelReachesEveryListenerUntilTheyLeave()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            int first = 0, second = 0;
            void First() => first++;
            void Second() => second++;

            channel.Raised += First;
            channel.Raised += Second;
            channel.Raise();
            channel.Raised -= Second;
            channel.Raise();

            Assert.That(first, Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(1));
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void TypedChannelCarriesItsValue()
        {
            var channel = ScriptableObject.CreateInstance<GameStateEventChannel>();
            GameState heard = GameState.Boot;
            channel.Raised += state => heard = state;

            channel.Raise(GameState.PlayerCaught);

            Assert.That(heard, Is.EqualTo(GameState.PlayerCaught));
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void AnchorOnlyClearsForTheTransformItHolds()
        {
            var anchor = ScriptableObject.CreateInstance<TransformAnchor>();
            var player = new GameObject("player").transform;
            var other = new GameObject("other").transform;

            anchor.Set(player);
            anchor.Clear(other);
            Assert.That(anchor.Value, Is.SameAs(player));

            anchor.Clear(player);
            Assert.That(anchor.IsSet, Is.False);

            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(other.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }
}
