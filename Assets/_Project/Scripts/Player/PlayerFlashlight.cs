using Game.Core;
using Game.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    // Task A6: the flashlight's switch and battery. F, the left mouse button or the gamepad's right
    // shoulder toggles it. While it is on and the game is Playing the battery drains; at empty it
    // goes out by itself and stays out until a battery pickup refills it. Dev D's
    // FlashlightController owns how the beam looks (dimming, flicker, ghost slowdown): it hears the
    // charge on BatteryChanged and never needs to know about this class.
    //
    // This class owns the numbers (they come from PlayerConfig); the Light's Enabled flag is the
    // on/off switch both classes read.
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        // The charge is announced only when it moved at least this much (a fraction of full),
        // so a draining battery is not a message every frame.
        const float AnnounceStep = 0.005f;

        [SerializeField] PlayerConfig config;

        [Tooltip("The flashlight's spot light. Its Enabled checkbox is the switch.")]
        [SerializeField] Light flashlight;

        [Header("Listens to")]
        [SerializeField] GameStateEventChannel gameStateChanged;
        [SerializeField] VoidEventChannel newGameStarted;

        [Header("Raises")]
        [Tooltip("The charge from 0 to 1. Heard by the HUD, audio and Dev D's FlashlightController.")]
        [SerializeField] FloatEventChannel batteryChanged;

        InputAction toggleAction;
        GameState state = GameState.Boot;
        float charge;
        float lastAnnounced = -1f;
        bool diedEmpty;
        bool initialised;

        // 0 (empty) to 1 (full).
        public float Fraction => config != null ? charge / config.FlashlightMaxBattery : 0f;
        public bool IsEmpty => config != null && Fraction <= config.BatteryEmptyThreshold;
        public bool IsOn => flashlight != null && flashlight.enabled;
        public bool CanToggle => state == GameState.Playing && Time.timeScale > 0f;

        void Awake() => Initialise();

        void Initialise()
        {
            if (initialised || config == null)
                return;

            initialised = true;
            charge = config.FlashlightMaxBattery;
        }

        void OnEnable()
        {
            Initialise();

            toggleAction = new InputAction("Flashlight", InputActionType.Button);
            toggleAction.AddBinding("<Keyboard>/f");
            toggleAction.AddBinding("<Mouse>/leftButton");
            toggleAction.AddBinding("<Gamepad>/rightShoulder");
            toggleAction.performed += OnTogglePressed;
            toggleAction.Enable();

            if (gameStateChanged != null)
                gameStateChanged.Raised += OnGameStateChanged;
            if (newGameStarted != null)
                newGameStarted.Raised += Refill;
        }

        void OnDisable()
        {
            if (gameStateChanged != null)
                gameStateChanged.Raised -= OnGameStateChanged;
            if (newGameStarted != null)
                newGameStarted.Raised -= Refill;

            if (toggleAction != null)
            {
                toggleAction.performed -= OnTogglePressed;
                toggleAction.Dispose();
                toggleAction = null;
            }
        }

        // The HUD and Dev D's flashlight hear the starting charge as soon as everything has woken up.
        void Start() => Announce(force: true);

        void Update() => Step(Time.deltaTime);

        // Drains the battery for one frame. Update calls it with the frame time; tests call it with any time.
        public void Step(float deltaTime)
        {
            if (state != GameState.Playing || !IsOn || config == null)
                return;

            charge = Mathf.Max(0f, charge - config.DrainPerSecond * deltaTime);

            if (IsEmpty)
            {
                diedEmpty = true;
                flashlight.enabled = false;
                Announce(force: true);
                return;
            }

            Announce(force: false);
        }

        // Switches the light. Returns the new state. A dead battery cannot be switched on.
        public bool Toggle() => SetOn(!IsOn);

        public bool SetOn(bool on)
        {
            if (flashlight == null)
                return false;

            if (on && IsEmpty)
                return false;

            flashlight.enabled = on;
            if (on)
                diedEmpty = false;
            return flashlight.enabled;
        }

        // A battery pickup. Phase 3's pickups call this on the player.
        public void Recharge() => Recharge(config != null ? config.RechargeAmount : 0f);

        public void Recharge(float amount)
        {
            if (config == null)
                return;

            charge = Mathf.Clamp(charge + amount, 0f, config.FlashlightMaxBattery);
            Announce(force: true);

            // A light that died of an empty battery comes back on by itself.
            if (diedEmpty && !IsEmpty)
                SetOn(true);
        }

        // Full battery, light on. A new game starts like this.
        public void Refill()
        {
            if (config == null)
                return;

            charge = config.FlashlightMaxBattery;
            Announce(force: true);
            SetOn(true);
        }

        void OnTogglePressed(InputAction.CallbackContext context)
        {
            if (CanToggle)
                Toggle();
        }

        void OnGameStateChanged(GameState next)
        {
            state = next;

            // Every round starts with the light on (if there is any battery) and the HUD in sync.
            if (next == GameState.Ready)
            {
                SetOn(true);
                Announce(force: true);
            }
        }

        void Announce(bool force)
        {
            if (batteryChanged == null)
                return;

            float fraction = Fraction;
            bool reachedEmpty = fraction <= 0f && lastAnnounced > 0f;
            if (!force && !reachedEmpty && Mathf.Abs(fraction - lastAnnounced) < AnnounceStep)
                return;

            lastAnnounced = fraction;
            batteryChanged.Raise(fraction);
        }
    }
}
