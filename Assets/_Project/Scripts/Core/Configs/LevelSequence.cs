using UnityEngine;

namespace Game.Core
{
    // The order levels are played in. GameManager asks for level 0, 1, 2, ... and each replay is
    // harder. Past the end, every further level replays the last (hardest) entry, the way the
    // original stops getting harder after a while.
    [CreateAssetMenu(fileName = "LevelSequence", menuName = "Game/Configs/Level Sequence")]
    public sealed class LevelSequence : ScriptableObject
    {
        [Tooltip("Levels in play order. After the last one, the last entry repeats.")]
        [SerializeField] LevelConfig[] levels;

        public int Count => levels == null ? 0 : levels.Length;

        // The config for a zero-based level index, clamped to the list. Null only if the list is empty.
        public LevelConfig Get(int index)
        {
            if (Count == 0)
                return null;

            return levels[Mathf.Clamp(index, 0, Count - 1)];
        }
    }
}
