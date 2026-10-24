using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Sandbox
{
    // Throwaway first-person player for the ghost test maze. Dev A's real player replaces it.
    // Click to lock the mouse, click again to toggle the flashlight, Esc to free the mouse.
    [RequireComponent(typeof(CharacterController))]
    public sealed class SandboxPlayer : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Light flashlight;
        [SerializeField] float moveSpeed = 4.5f;
        [SerializeField] float lookSensitivity = 0.1f;

        CharacterController controller;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch;

        public bool InputEnabled { get; set; } = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Vector3 velocity = Vector3.zero;

            if (InputEnabled && keyboard != null && mouse != null)
            {
                HandleMouse(keyboard, mouse);
                velocity = ReadMove(keyboard) * moveSpeed;
            }

            // SimpleMove also applies gravity, so call it even when standing still.
            controller.SimpleMove(velocity);
        }

        public void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            pitch = 0f;
            head.localRotation = Quaternion.identity;
            controller.enabled = true;
        }

        void HandleMouse(Keyboard keyboard, Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (flashlight != null)
                {
                    flashlight.enabled = !flashlight.enabled;
                }
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector3 ReadMove(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

            Vector3 move = transform.forward * input.y + transform.right * input.x;
            return Vector3.ClampMagnitude(move, 1f);
        }
    }
}
