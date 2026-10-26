using System;
using Game.Core;
using Game.Events;
using Game.Player;
using UnityEngine;

namespace Game.Pickups
{
    // Task A5: one pickup in the maze, a power cell, a flare or a battery. It is a trigger: when the
    // Player walks into it, it disappears and does what its kind says.
    //
    //   Cell    raises PickupCollected(its PickupConfig) and counts toward finishing the level.
    //   Flare   raises FlareCollected (no PickupCollected) and counts toward finishing the level.
    //   Battery raises PickupCollected(its PickupConfig), recharges the flashlight, and never counts.
    //
    // MazePickups creates these from the map and calls Initialise on each; nothing is set in the
    // Inspector, so there is no scene wiring to forget.
    public sealed class Pickup : MonoBehaviour
    {
        public const string PlayerTag = "Player";

        // How fast a flare's glow swells and shrinks, in radians per second.
        const float PulseSpeed = 6f;

        PickupConfig config;
        LevelProgress progress;
        PickupEventChannel pickupCollected;
        VoidEventChannel flareCollected;

        Collider trigger;
        Renderer[] renderers;
        Transform visual;
        Vector3 visualScale = Vector3.one;
        float pulseAmount;

        bool collected;
        bool shown = true;
        bool cached;

        // Raised after the pickup did its job (events raised, progress counted, flashlight charged).
        public event Action<Pickup> Collected;

        public PickupConfig Config => config;
        public PickupKind Kind => config != null ? config.Kind : PickupKind.Cell;

        // Taken since the last Restore.
        public bool IsCollected => collected;

        // Visible and waiting to be taken.
        public bool IsAvailable => shown && !collected;

        public void Initialise(
            PickupConfig pickupConfig,
            LevelProgress levelProgress,
            PickupEventChannel pickupEvent,
            VoidEventChannel flareEvent,
            Transform visualChild,
            float pulse)
        {
            config = pickupConfig;
            progress = levelProgress;
            pickupCollected = pickupEvent;
            flareCollected = flareEvent;
            visual = visualChild;
            visualScale = visualChild != null ? visualChild.localScale : Vector3.one;
            pulseAmount = Mathf.Max(0f, pulse);

            cached = false;
            Cache();
            collected = false;
            SetVisible(true);
        }

        void Awake() => Cache();

        void Cache()
        {
            if (cached)
                return;

            cached = true;
            trigger = GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update()
        {
            if (pulseAmount <= 0f || visual == null || !shown)
                return;

            float swell = 1f + pulseAmount * Mathf.Sin(Time.time * PulseSpeed);
            visual.localScale = visualScale * swell;
        }

        void OnTriggerEnter(Collider other) => Touch(other);

        // Something touched the pickup. Only the Player counts. Tests call this directly.
        public bool Touch(Collider other)
        {
            if (other == null || !other.CompareTag(PlayerTag))
                return false;

            PlayerFlashlight flashlight = other.GetComponentInParent<PlayerFlashlight>();
            if (flashlight == null)
                flashlight = other.transform.root.GetComponentInChildren<PlayerFlashlight>();

            return Collect(flashlight);
        }

        // Takes the pickup. Returns false if it was not available (already taken, or hidden).
        // The flashlight is only needed by batteries.
        public bool Collect(PlayerFlashlight flashlight)
        {
            if (config == null || !IsAvailable)
                return false;

            collected = true;
            SetVisible(false);

            switch (config.Kind)
            {
                case PickupKind.Cell:
                    // Score hears the cell first, so the last cell still scores before the level ends.
                    if (pickupCollected != null)
                        pickupCollected.Raise(config);
                    if (progress != null)
                        progress.Collect();
                    break;

                case PickupKind.Flare:
                    if (flareCollected != null)
                        flareCollected.Raise();
                    if (progress != null)
                        progress.Collect();
                    break;

                case PickupKind.Battery:
                    if (pickupCollected != null)
                        pickupCollected.Raise(config);
                    if (flashlight != null)
                        flashlight.Recharge();
                    break;
            }

            Collected?.Invoke(this);
            return true;
        }

        // Back in the maze, ready to be taken again.
        public void Restore()
        {
            collected = false;
            SetVisible(true);
        }

        // Out of the maze but not taken: a battery that has not spawned yet, or has timed out.
        public void Hide()
        {
            collected = false;
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            Cache();
            shown = visible;

            if (trigger != null)
                trigger.enabled = visible;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }

            if (visible && visual != null)
                visual.localScale = visualScale;
        }
    }
}
