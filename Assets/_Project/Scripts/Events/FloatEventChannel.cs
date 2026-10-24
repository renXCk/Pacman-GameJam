using UnityEngine;

namespace Game.Events
{
    // A channel that carries a float, used for the flashlight battery (0 to 1).
    [CreateAssetMenu(fileName = "FloatEvent", menuName = "Game/Events/Float Event Channel")]
    public sealed class FloatEventChannel : EventChannel<float>
    {
    }
}
