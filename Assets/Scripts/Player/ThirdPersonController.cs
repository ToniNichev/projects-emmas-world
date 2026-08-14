using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Audio;
using Sandbox.UI;

namespace Sandbox.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintSpeed = 10f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private float footstepInterval = 0.35f;
        [SerializeField] private VirtualJoystick moveJoystick;

        private CharacterController controller;
        private SoundEffects soundEffects;
        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private Vector2 moveInput;
        private bool sprintHeld;
        private bool jumpQueued;
        private Vector3 verticalVelocity;
        private float footstepTimer;

        // Combines keyboard/gamepad input with the on-screen movement
        // joystick (when present) so both work simultaneously without one
        // overriding the other.
        private Vector2 CombinedMoveInput => moveJoystick != null
            ? Vector2.ClampMagnitude(moveInput + moveJoystick.Value, 1f)
            : moveInput;

        public bool IsMoving => CombinedMoveInput.sqrMagnitude > 0.01f;
        public bool IsSprinting => sprintHeld;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            soundEffects = GetComponent<SoundEffects>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            InputActionMap map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            sprintAction = map.FindAction("Sprint", throwIfNotFound: true);
            jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            moveAction.performed += OnMove;
            moveAction.canceled += OnMove;
            sprintAction.performed += OnSprint;
            sprintAction.canceled += OnSprint;
            jumpAction.performed += OnJump;
            moveAction.Enable();
            sprintAction.Enable();
            jumpAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.performed -= OnMove;
            moveAction.canceled -= OnMove;
            sprintAction.performed -= OnSprint;
            sprintAction.canceled -= OnSprint;
            jumpAction.performed -= OnJump;
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            sprintHeld = context.ReadValueAsButton();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                TriggerJump();
        }

        // Public so the on-screen mobile jump button can call it directly
        // without needing to fake an InputAction.CallbackContext.
        public void TriggerJump()
        {
            if (controller.isGrounded)
                jumpQueued = true;
        }

        private void Update()
        {
            ApplyGravityAndJump();
            ApplyMovement();
            UpdateFootsteps();
        }

        private void ApplyGravityAndJump()
        {
            if (controller.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;

            if (jumpQueued)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpQueued = false;
                soundEffects?.PlayJump();
            }

            verticalVelocity.y += gravity * Time.deltaTime;
        }

        private void UpdateFootsteps()
        {
            // Gated on movement input rather than controller.isGrounded, which is
            // notoriously unreliable at rest even while standing on flat ground.
            if (CombinedMoveInput.sqrMagnitude < 0.01f)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                soundEffects?.PlayFootstep();
                footstepTimer = footstepInterval;
            }
        }

        private void ApplyMovement()
        {
            Vector2 combinedInput = CombinedMoveInput;
            Vector3 inputDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
            // CombinedMoveInput is already clamped to magnitude 1, so this is
            // how far the stick/keys are actually pushed, 0..1.
            float inputMagnitude = inputDir.magnitude;
            if (inputMagnitude < 0.0001f)
            {
                controller.Move(verticalVelocity * Time.deltaTime);
                return;
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // .normalized here is for DIRECTION only -- speed is scaled by
            // inputMagnitude separately below. Without that separation, any
            // non-zero input (even a stray value from a finger not landing
            // pixel-perfect on the joystick's center) snapped straight to
            // full speed, since normalizing the direction also discarded how
            // far the stick was actually pushed.
            Vector3 moveDir = (forward * inputDir.z + right * inputDir.x).normalized;
            float speed = (sprintHeld ? sprintSpeed : moveSpeed) * inputMagnitude;

            // Face the movement direction immediately (Roblox-style) rather
            // than easing into it -- a smoothed turn made the avatar visibly
            // spin in place before walking whenever it wasn't already facing
            // where the camera-relative input pointed.
            transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);

            Vector3 motion = moveDir * speed + verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
