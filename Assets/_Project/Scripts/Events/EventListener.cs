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

        void Respond(T value) => response.Invoke(value);
    }
}
