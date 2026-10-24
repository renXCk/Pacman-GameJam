using Game.Events;
using UnityEngine;

namespace Game.Ghosts
{
    // GhostBanished: raised by GhostDirector, heard by score, audio and VFX.
    [CreateAssetMenu(fileName = "GhostBanished", menuName = "Game/Events/Ghost Banished Event Channel")]
    public sealed class GhostBanishedEventChannel : EventChannel<GhostBanishedInfo>
    {
    }
}
