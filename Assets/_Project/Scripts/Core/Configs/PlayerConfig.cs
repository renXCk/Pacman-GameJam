using UnityEngine;

namespace Game.Core
{
    // Player and flashlight tuning. Dev A's player controller and flashlight read it; the HUD
    // only hears the battery level through BatteryChanged. Defined here so the numbers live in
    // one asset from day one.
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Game/Configs/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Walking speed in m/s. Compare with LevelConfig ghost speeds (3.8 on level 1).")]
        [Min(0f)] [SerializeField] float moveSpeed = 4.5f;

        [Tooltip("Degrees of turn per unit of mouse delta.")]
        [Range(0.01f, 1f)] [SerializeField] float lookSensitivity = 0.12f;

        [Header("Flashlight battery")]
        [Tooltip("Battery charge when full.")]
        [Min(1f)] [SerializeField] float flashlightMaxBattery = 100f;

        [Tooltip("Charge used per second while the flashlight is on.")]
        [Min(0f)] [SerializeField] float drainPerSecond = 2f;

        [Tooltip("Charge restored by one battery pickup.")]
        [Min(0f)] [SerializeField] float rechargeAmount = 50f;

        [Tooltip("Below this fraction (0 to 1) the battery counts as empty and the light goes out.")]
        [Range(0f, 1f)] [SerializeField] float batteryEmptyThreshold = 0.02f;

        public float MoveSpeed => moveSpeed;
        public float LookSensitivity => lookSensitivity;
        public float FlashlightMaxBattery => flashlightMaxBattery;
        public float DrainPerSecond => drainPerSecond;
        public float RechargeAmount => rechargeAmount;
        public float BatteryEmptyThreshold => batteryEmptyThreshold;
    }
}
