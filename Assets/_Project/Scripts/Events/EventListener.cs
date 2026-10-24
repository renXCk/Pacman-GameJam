using UnityEngine;
using UnityEngine.Events;

namespace Game.Events
{
    // Inspector-wired listener: hears a channel and invokes a UnityEvent response.
    // Make a concrete subclass per payload type, for example GameStateEventListener.
    public abstract class EventListener<T, TChannel> : MonoBehaviour where TChannel : EventChannel<T>
    {
        [SerializeField] TChannel channel;
        [SerializeField] UnityEvent<T> response;

        void OnEnable()
        {
            if (channel != null)
                channel.Raised += Respond;
        }

        void OnDisable()
        {
            if (channel != null)
                channel.Raised -= Respond;
        }

        void Respond(T value)
        {
            // Follows the channel's own Log Raises switch, so one tick traces raiser and listeners.
            if (channel.LogRaises)
                Debug.Log($"[{name}] heard {channel.name}: {value}", this);

            response.Invoke(value);
        }
    }
}
