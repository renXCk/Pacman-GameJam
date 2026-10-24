using Game.Core;
using Game.Events;
using UnityEngine;

namespace Game.Systems
{
    // Locks and hides the mouse only while actually playing, so the menus, the pause screen and
    // the game over screen can always be clicked. Driven by events, never checked per frame.
    public sealed class CursorController : MonoBehaviour
    {
        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] BoolEventChannel pauseChanged;

        GameState state = GameState.Boot;
        bool paused;

        void OnEnable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised += OnGameStateChanged;
            if (pauseChanged != null) pauseChanged.Raised += OnPauseChanged;
            Apply();
        }

        void OnDisable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (pauseChanged != null) pauseChanged.Raised -= OnPauseChanged;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGameStateChanged(GameState next)
        {
            state = next;
            Apply();
        }

        void OnPauseChanged(bool value)
        {
            paused = value;
            Apply();
        }

        void Apply()
        {
            bool locked = state == GameState.Playing && !paused;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
