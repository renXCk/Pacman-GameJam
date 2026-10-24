using Game.Events;
using UnityEngine;

namespace Game.Ghosts
{
    // ModeChanged: raised by the ghost mode timer (GhostDirector), heard by ghosts and audio.
    [CreateAssetMenu(fileName = "ModeChanged", menuName = "Game/Events/Ghost Mode Event Channel")]
    public sealed class GhostModeEventChannel : EventChannel<GhostModeChange>
    {
    }
}
