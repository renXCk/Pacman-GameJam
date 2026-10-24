using UnityEngine;
using UnityEngine.Events;

namespace Game.Events
{
    // Inspector-wired listener for a VoidEventChannel.
    public sealed class VoidEventListener : MonoBehaviour
    {
        [SerializeField] VoidEventChannel channel;
        [SerializeField] UnityEvent response;

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

        void Respond()
        {
            if (channel.LogRaises)
                Debug.Log($"[{name}] heard {channel.name}", this);

            response.Invoke();
        }
    }
}
