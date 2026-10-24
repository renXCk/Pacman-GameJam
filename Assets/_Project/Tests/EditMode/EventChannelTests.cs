using Game.Core;
using Game.Events;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // The payload channels added for Package C: a raise reaches every listener, and a listener
    // that unsubscribes stops hearing.
    public class EventChannelTests
    {
        [Test]
        public void IntChannelReachesEveryListenerUntilTheyLeave()
        {
            var channel = ScriptableObject.CreateInstance<IntEventChannel>();
            int first = 0, second = 0;
            void First(int value) => first = value;
            void Second(int value) => second = value;

            channel.Raised += First;
            channel.Raised += Second;
            Assert.That(channel.ListenerCount, Is.EqualTo(2));
            channel.Raise(300);
            channel.Raised -= Second;
            channel.Raise(500);

            Assert.That(first, Is.EqualTo(500));
            Assert.That(second, Is.EqualTo(300), "unsubscribed listener kept the old value");
            Assert.That(channel.ListenerCount, Is.EqualTo(1));
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void FloatChannelCarriesItsValue()
        {
            var channel = ScriptableObject.CreateInstance<FloatEventChannel>();
            float heard = -1f;
            channel.Raised += value => heard = value;

            channel.Raise(0.25f);

            Assert.That(heard, Is.EqualTo(0.25f));
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void BoolChannelReachesEveryListener()
        {
            var channel = ScriptableObject.CreateInstance<BoolEventChannel>();
            bool a = false, b = false;
            channel.Raised += value => a = value;
            channel.Raised += value => b = value;

            channel.Raise(true);

            Assert.That(a && b, Is.True);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void PickupChannelCarriesTheConfig()
        {
            var channel = ScriptableObject.CreateInstance<PickupEventChannel>();
            var cell = ScriptableObject.CreateInstance<PickupConfig>();
            PickupConfig heard = null;
            void Listener(PickupConfig value) => heard = value;

            channel.Raised += Listener;
            channel.Raise(cell);
            Assert.That(heard, Is.SameAs(cell));

            channel.Raised -= Listener;
            heard = null;
            channel.Raise(cell);
            Assert.That(heard, Is.Null);
            Assert.That(channel.ListenerCount, Is.EqualTo(0));

            Object.DestroyImmediate(channel);
            Object.DestroyImmediate(cell);
        }

        [Test]
        public void VoidChannelCountsListeners()
        {
            var channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            void Listener() { }

            channel.Raised += Listener;
            Assert.That(channel.ListenerCount, Is.EqualTo(1));
            channel.Raised -= Listener;
            Assert.That(channel.ListenerCount, Is.EqualTo(0));

            Object.DestroyImmediate(channel);
        }
    }
}
