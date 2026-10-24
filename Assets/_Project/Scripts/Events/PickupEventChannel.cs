using Game.Core;
using UnityEngine;

namespace Game.Events
{
    // PickupCollected: raised by a pickup (Dev A) with its PickupConfig when the player collects
    // a power cell or a battery. Heard by score, audio and VFX. Flares use FlareCollected instead,
    // because the ghosts already listen to that one.
    [CreateAssetMenu(fileName = "PickupCollected", menuName = "Game/Events/Pickup Event Channel")]
    public sealed class PickupEventChannel : EventChannel<PickupConfig>
    {
    }
}
