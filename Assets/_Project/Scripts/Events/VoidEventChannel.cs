using System;
using UnityEngine;

namespace Game.Events
{
    // An event channel with no payload, for things like FlareCollected or PlayerCaught.
    [CreateAssetMenu(fileName = "NewVoidEvent", menuName = "Game/Events/Void Event Channel")]
    public sealed class VoidEventChannel : EventChannelBase
    {
        public event Action Raised;

        // Also available from the asset's context menu in the Inspector, for quick testing.
        [ContextMenu("Raise")]
        public void Raise()
        {
            if (logRaises)
                Debug.Log($"[{name}] raised", this);

            Raised?.Invoke();
        }
    }
}
