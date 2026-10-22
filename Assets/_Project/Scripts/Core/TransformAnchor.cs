using System;
using UnityEngine;

namespace Game.Core
{
    // A runtime pointer to one scene Transform, held in an asset so prefabs can reach it
    // without FindObjectOfType. The player sets it with a TransformAnchorBinder.
    [CreateAssetMenu(fileName = "TransformAnchor", menuName = "Game/Runtime/Transform Anchor")]
    public sealed class TransformAnchor : ScriptableObject
    {
        [NonSerialized] Transform value;

        public event Action<Transform> Changed;

        public Transform Value => value;
        public bool IsSet => value != null;

        public void Set(Transform transform)
        {
            if (value == transform)
                return;

            value = transform;
            Changed?.Invoke(value);
        }

        // Only clears the anchor if it still points at this transform.
        public void Clear(Transform transform)
        {
            if (value == transform)
                Set(null);
        }
    }
}
