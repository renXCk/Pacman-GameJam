using UnityEngine;

namespace Game.Lighting
{
    /// <summary>
    /// Attach to any dynamic light (Ghost point light, pickup halo, flare) to register it
    /// with the LightingManager for strict 6-8 light budget enforcement.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class DynamicLightSource : MonoBehaviour
    {
        [Tooltip("Priority for budget arbitration. 100 = Flashlight, 90 = Flare, 50 = Ghosts, 10 = Props/Pickups.")]
        [Range(1, 100)]
        [SerializeField] int priority = 50;

        Light lightComponent;

        void Awake()
        {
            lightComponent = GetComponent<Light>();
        }

        void OnEnable()
        {
            if (LightingManager.Instance != null && lightComponent != null)
            {
                LightingManager.Instance.RegisterDynamicLight(lightComponent, priority);
            }
        }

        void OnDisable()
        {
            if (LightingManager.Instance != null && lightComponent != null)
            {
                LightingManager.Instance.UnregisterDynamicLight(lightComponent);
            }
        }
    }
}
