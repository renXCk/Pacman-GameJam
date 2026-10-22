using Game.Core;
using UnityEngine;

namespace Game.Events
{
    // GameStateChanged: raised by GameManager, heard by UI, audio, player input and ghosts.
    [CreateAssetMenu(fileName = "GameStateChanged", menuName = "Game/Events/Game State Event Channel")]
    public sealed class GameStateEventChannel : EventChannel<GameState>
    {
    }
}
