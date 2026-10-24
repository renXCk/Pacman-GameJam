using Game.Core;
using Game.Events;
using UnityEngine;

namespace Game.Sandbox
{
    // Stand-in for Dev A's flare pickup: a glowing trigger that raises FlareCollected,
    // and comes back at the start of every level.
    [RequireComponent(typeof(Collider))]
    public sealed class SandboxFlarePickup : MonoBehaviour
    {
        [SerializeField] VoidEventChannel flareCollected;
        [SerializeField] LevelConfigEventChannel levelStarted;
        [SerializeField] string playerTag = "Player";

        Collider trigger;
        Renderer[] renderers;
        Light[] lights;

        void Awake()
        {
            trigger = GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            lights = GetComponentsInChildren<Light>();
        }

        void OnEnable()
        {
            if (levelStarted != null)
                levelStarted.Raised += OnLevelStarted;
        }

        void OnDisable()
        {
            if (levelStarted != null)
                levelStarted.Raised -= OnLevelStarted;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag))
                return;

            Show(false);
            flareCollected.Raise();
        }

        void OnLevelStarted(LevelConfig level) => Show(true);

        void Show(bool visible)
        {
            trigger.enabled = visible;
            foreach (Renderer item in renderers)
                item.enabled = visible;
            foreach (Light item in lights)
                item.enabled = visible;
        }
    }
}
