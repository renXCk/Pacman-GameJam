using UnityEngine;

namespace Game.Core
{
    // Put this on the object an anchor should point at (for example the player root).
    public sealed class TransformAnchorBinder : MonoBehaviour
    {
        [SerializeField] TransformAnchor anchor;

        void OnEnable()
        {
            if (anchor != null)
                anchor.Set(transform);
        }

        void OnDisable()
        {
            if (anchor != null)
                anchor.Clear(transform);
        }
    }
}
