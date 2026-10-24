using UnityEngine;

namespace Game.Events
{
    // A channel that carries an int, used for score, lives, high score and level number.
    [CreateAssetMenu(fileName = "IntEvent", menuName = "Game/Events/Int Event Channel")]
    public sealed class IntEventChannel : EventChannel<int>
    {
    }
}
