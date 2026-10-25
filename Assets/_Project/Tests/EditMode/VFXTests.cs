using Game.VFX;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class VFXTests
    {
        [Test]
        public void VFXManagerInitializesWithoutException()
        {
            var go = new GameObject("VFXManager");
            var manager = go.AddComponent<VFXManager>();

            Assert.That(VFXManager.Instance, Is.EqualTo(manager));

            Object.DestroyImmediate(go);
            Assert.That(VFXManager.Instance, Is.Null);
        }
    }
}
