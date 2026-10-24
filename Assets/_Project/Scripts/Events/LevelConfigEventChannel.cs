using Game.Core;
using UnityEngine;

namespace Game.Events
{
    // LevelStarted: raised by GameManager with the LevelConfig for the level about to begin,
    // before it raises GameStateChanged(Ready). Ghosts read their speeds and timers from it.
    // Proposed addition to the channel table in section 7.2.
    [CreateAssetMenu(fileName = "LevelStarted", menuName = "Game/Events/Level Config Event Channel")]
    public sealed class LevelConfigEventChannel : EventChannel<LevelConfig>
    {
    }
}
