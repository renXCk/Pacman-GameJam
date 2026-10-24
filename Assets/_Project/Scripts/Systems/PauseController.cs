using Game.Core;
using Game.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Systems
{
    // Esc (or gamepad Start) pauses while Playing, using Time.timeScale = 0. That already freezes
    // the ghosts, so pause is not a GameState and the ghost code needs no change. The pause menu's
    // Resume button asks through PauseRequested; everyone hears the result on PauseChanged.
    public sealed class PauseController : MonoBehaviour
    {
        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [Tooltip("UI asks to pause (true) or resume (false), for example the Resume button.")]
        [SerializeField] BoolEventChannel pauseRequested;

        [Header("Raises")]
        [SerializeField] BoolEventChannel pauseChanged;

        // Built in code rather than in InputSystem_Actions so the shared actions asset stays
        // Dev A's, and pause works in any scene that has this component.
        InputAction pauseAction;
        GameState state = GameState.Boot;
        bool paused;

        public bool IsPaused => paused;

        void OnEnable()
        {
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
            pauseAction.performed += OnPausePressed;
            pauseAction.Enable();

            if (gameStateChanged != null) gameStateChanged.Raised += OnGameStateChanged;
            if (pauseRequested != null) pauseRequested.Raised += SetPaused;
        }

        void OnDisable()
        {
            if (gameStateChanged != null) gameStateChanged.Raised -= OnGameStateChanged;
            if (pauseRequested != null) pauseRequested.Raised -= SetPaused;

            pauseAction.performed -= OnPausePressed;
            pauseAction.Dispose();
            pauseAction = null;

            // Leaving the scene or stopping Play must never leave the game frozen. With domain
            // reload off, a stuck timeScale would even carry into the next Play session.
            paused = false;
            Time.timeScale = 1f;
        }

        void OnPausePressed(InputAction.CallbackContext context) => SetPaused(!paused);

        public void SetPaused(bool value)
        {
            if (value && state != GameState.Playing)
                return;
            if (value == paused)
                return;

            paused = value;
            Time.timeScale = paused ? 0f : 1f;

            if (pauseChanged != null)
                pauseChanged.Raise(paused);
        }

        // Caught, level complete, game over or menu: always resume first.
        void OnGameStateChanged(GameState next)
        {
            state = next;
            if (next != GameState.Playing)
                SetPaused(false);
        }
    }
}
