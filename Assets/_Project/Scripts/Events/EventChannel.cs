using System;
using UnityEngine;

namespace Game.Events
{
    // A ScriptableObject event channel that carries a value.
    // A system calls Raise(value); any system that cares subscribes to Raised.
    // The raiser never knows who is listening.
    public abstract class EventChannel<T> : EventChannelBase
    {
        public event Action<T> Raised;

        public void Raise(T value)
        {
            if (logRaises)
                Debug.Log($"[{name}] raised {value}", this);

            Raised?.Invoke(value);
        }
    }
}
