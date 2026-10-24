using UnityEngine;

namespace Game.Core
{
    // The three Pac-Man pickups, remade: pellets are power cells, power pellets are flares,
    // fruit is a battery.
    public enum PickupKind
    {
        Cell,
        Flare,
        Battery
    }

    // One asset per pickup type. Dev A's pickup prefabs each carry one and raise PickupCollected
    // with it, so score, audio and the HUD can tell pickups apart without knowing the prefab.
    [CreateAssetMenu(fileName = "PickupConfig", menuName = "Game/Configs/Pickup Config")]
    public sealed class PickupConfig : ScriptableObject
    {
        [SerializeField] PickupKind kind = PickupKind.Cell;

        [Tooltip("Name shown in UI, for example Power Cell.")]
        [SerializeField] string displayName = "Power Cell";

        [Tooltip("Points for collecting it. Batteries use the level's battery points instead " +
                 "(LevelConfig > Bonus); this value is only their fallback when no level is running.")]
        [Min(0)] [SerializeField] int basePoints = 10;

        [Tooltip("Whether collecting it counts toward finishing the level, like pellets and power pellets. " +
                 "Off for batteries, which are a bonus.")]
        [SerializeField] bool countsTowardLevel = true;

        public PickupKind Kind => kind;
        public string DisplayName => displayName;
        public int BasePoints => basePoints;
        public bool CountsTowardLevel => countsTowardLevel;

        // Points this pickup is worth on the given level.
        public int PointsFor(LevelConfig level)
        {
            if (kind == PickupKind.Battery && level != null)
                return level.BatteryPoints;

            return basePoints;
        }
    }
}
