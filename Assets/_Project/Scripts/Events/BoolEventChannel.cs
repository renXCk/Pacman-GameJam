using UnityEngine;

namespace Game.Events
{
    // A channel that carries a bool, used for pause.
    [CreateAssetMenu(fileName = "BoolEvent", menuName = "Game/Events/Bool Event Channel")]
    public sealed class BoolEventChannel : EventChannel<bool>
    {
    }
}
