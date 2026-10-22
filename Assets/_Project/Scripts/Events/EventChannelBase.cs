using UnityEngine;

namespace Game.Events
{
    // Shared base for every event channel asset.
    public abstract class EventChannelBase : ScriptableObject
    {
        [TextArea(2, 4)]
        [Tooltip("Who raises this channel and who listens. Keep it in sync with the channel table in the spec.")]
        [SerializeField] string description;

        [Tooltip("Log every raise to the Console. For debugging only.")]
        [SerializeField] protected bool logRaises;

        public string Description => description;
    }
}
