using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Ghosts
{
    // The ghosts currently in play. Each ghost adds itself on enable and removes itself on
    // disable, so the director and the flashlight beam never have to search the scene.
    [CreateAssetMenu(fileName = "GhostRuntimeSet", menuName = "Game/Runtime/Ghost Runtime Set")]
    public sealed class GhostRuntimeSet : ScriptableObject
    {
        [NonSerialized] readonly List<Ghost> items = new();

        public event Action<Ghost> Added;
        public event Action<Ghost> Removed;

        public IReadOnlyList<Ghost> Items => items;
        public int Count => items.Count;

        public void Add(Ghost ghost)
        {
            if (ghost == null || items.Contains(ghost))
                return;

            items.Add(ghost);
            Added?.Invoke(ghost);
        }

        public void Remove(Ghost ghost)
        {
            if (items.Remove(ghost))
                Removed?.Invoke(ghost);
        }

        public Ghost Find(GhostConfig config)
        {
            if (config == null)
                return null;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Config == config)
                    return items[i];
            }

            return null;
        }
    }
}
