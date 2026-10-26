using Game.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class LightingTests
    {
        [Test]
        public void DynamicLightBudgetEnforcesMaxLimit()
        {
            var managerObj = new GameObject("LightingManager");
            var manager = managerObj.AddComponent<LightingManager>();

            // Create 10 lights (exceeding budget of 8)
            var lights = new Light[10];
            for (int i = 0; i < 10; i++)
            {
                var lightObj = new GameObject($"TestLight_{i}");
                lights[i] = lightObj.AddComponent<Light>();
                manager.RegisterDynamicLight(lights[i], priority: i * 10);
            }

            // Force update
            var updateMethod = typeof(LightingManager).GetMethod("EnforceDynamicLightBudget", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateMethod.Invoke(manager, null);

            // Count enabled lights
            int enabledCount = 0;
            for (int i = 0; i < 10; i++)
            {
                if (lights[i].enabled) enabledCount++;
            }

            Assert.That(enabledCount, Is.LessThanOrEqualTo(8), "Dynamic lights must not exceed strict budget of 8.");

            for (int i = 0; i < 10; i++)
            {
                Object.DestroyImmediate(lights[i].gameObject);
            }
            Object.DestroyImmediate(managerObj);
        }
    }
}
