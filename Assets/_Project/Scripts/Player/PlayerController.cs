using Game.Core;
using Game.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    // Task A4: the first-person player. WASD / arrow keys / left stick walk, mouse / right stick look.
    // It moves only while the game is in the Playing state and not paused. The mouse lock is
    // CursorController's job, so nothing here touches the cursor. On every Ready state the player
    // goes back to the spawn point (the maze's player start), which is also what a catch does.
    //
    // Hierarchy it expects (built by Game > Player > Add Player To Game Scene):
    //   Player (CharacterController, this, TransformAnchorBinder)  turns left and right
    //     Head                                                      tilts up and down
    //       Player Camera
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        const float MaxPitch = 80f;
        const float StickLookDegreesPerSecond = 180f;

        // The capsule's feet are at the transform. A small lift keeps it from spawning inside the floor.
        const float SpawnLift = 0.05f;

        // A small constant push into the floor keeps isGrounded reliable while walking.
        const float GroundedPush = -2f;

        [SerializeField] PlayerConfig config;

        [Tooltip("The object the camera sits under. It tilts up and down.")]
        [SerializeField] Transform head;

        [Tooltip("Where the player starts every round. The maze moves this marker to the player start tile.")]
        [SerializeField] Transform spawnPoint;

        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;

        CharacterController controller;
        InputAction moveAction;
        InputAction lookAction;
        InputAction stickLookAction;
        GameState state = GameState.Boot;
        float pitch;
        float verticalSpeed;

        public GameState State => state;

        // Turn off to stop the controller reading the keyboard and mouse by itself, for example in a
        // cutscene or a test that drives Step directly. Step still works either way.
        public bool InputEnabled { get; set; } = true;
        public Transform Head => head;

        // Looking up is a negative pitch, like in the sandbox player.
        public float Pitch => pitch;

        // Input only counts while playing, and never while paused (pause sets the time scale to 0).
        public bool CanMove => state == GameState.Playing && Time.timeScale > 0f;

        CharacterController Controller
        {
            get
            {
                if (controller == null)
                    controller = GetComponent<CharacterController>();
                return controller;
            }
        }

        void OnEnable()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");

            stickLookAction = new InputAction("Stick Look", InputActionType.Value);
            stickLookAction.AddBinding("<Gamepad>/rightStick");

            moveAction.Enable();
            lookAction.Enable();
            stickLookAction.Enable();

            if (gameStateChanged != null)
                gameStateChanged.Raised += OnGameStateChanged;
        }

        void OnDisable()
        {
            if (gameStateChanged != null)
                gameStateChanged.Raised -= OnGameStateChanged;

            moveAction?.Dispose();
            lookAction?.Dispose();
            stickLookAction?.Dispose();
            moveAction = null;
            lookAction = null;
            stickLookAction = null;
        }

        void Start() => MoveToSpawn();

        void Update()
        {
            if (!InputEnabled || !Controller.enabled)
                return;

            if (!CanMove)
            {
                // Keep applying gravity while standing still.
                Apply(Vector3.zero);
                return;
            }

            Vector2 look = lookAction.ReadValue<Vector2>() * config.LookSensitivity
                           + stickLookAction.ReadValue<Vector2>() * (StickLookDegreesPerSecond * Time.deltaTime);
            Step(moveAction.ReadValue<Vector2>(), look);
        }

        // One frame of movement. Update calls it with the real input; tests call it with made-up input.
        //   moveInput: x = strafe right, y = walk forward, each -1 to 1.
        //   lookDegrees: x = turn right, y = look up.
        public void Step(Vector2 moveInput, Vector2 lookDegrees)
        {
            transform.Rotate(0f, lookDegrees.x, 0f);

            pitch = Mathf.Clamp(pitch - lookDegrees.y, -MaxPitch, MaxPitch);
            if (head != null)
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector3 move = transform.forward * moveInput.y + transform.right * moveInput.x;
            Apply(Vector3.ClampMagnitude(move, 1f) * config.MoveSpeed);
        }

        // Moves the capsule by planarVelocity (m/s, sideways) plus gravity, for one frame. Uses Move so
        // the distance is exactly speed times the frame time.
        void Apply(Vector3 planarVelocity)
        {
            CharacterController body = Controller;
            float dt = Time.deltaTime;

            verticalSpeed = body.isGrounded ? GroundedPush : verticalSpeed + Physics.gravity.y * dt;

            Vector3 velocity = planarVelocity;
            velocity.y = verticalSpeed;
            body.Move(velocity * dt);
        }

        // Back to the start position and facing, looking straight ahead.
        public void MoveToSpawn()
        {
            if (spawnPoint == null)
                return;

            CharacterController body = Controller;
            body.enabled = false;
            transform.SetPositionAndRotation(
                spawnPoint.position + Vector3.up * SpawnLift,
                Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f));
            pitch = 0f;
            verticalSpeed = 0f;
            if (head != null)
                head.localRotation = Quaternion.identity;
            body.enabled = true;
        }

        void OnGameStateChanged(GameState next)
        {
            state = next;

            // Ready comes at the start of every level and after every catch.
            if (next == GameState.Ready)
                MoveToSpawn();
        }
    }
}
