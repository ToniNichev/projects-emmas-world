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
        [SerializeField] private float turnSpeed = 180f;
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

        // Only the forward/back axis counts as "moving" -- turning in place
        // (left/right with no forward/back held) shouldn't trigger footsteps
        // or arm/leg swing since the character isn't actually translating.
        public bool IsMoving => Mathf.Abs(CombinedMoveInput.y) > 0.01f;
        public bool IsSprinting => sprintHeld;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            soundEffects = GetComponent<SoundEffects>();

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
            if (!IsMoving)
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

        // Tank-style controls: left/right turns the character in place and
        // never moves it; forward/back always walks along whatever direction
        // it's currently facing and never changes its heading. The two axes
        // are independent, unlike the earlier camera-relative scheme where
        // any forward/back press snapped the character's facing to match
        // the camera.
        private void ApplyMovement()
        {
            Vector2 combinedInput = CombinedMoveInput;

            if (Mathf.Abs(combinedInput.x) > 0.0001f)
                transform.Rotate(Vector3.up, combinedInput.x * turnSpeed * Time.deltaTime);

            float forwardAmount = Mathf.Clamp(combinedInput.y, -1f, 1f);
            float speed = (sprintHeld ? sprintSpeed : moveSpeed) * Mathf.Abs(forwardAmount);
            Vector3 motion = transform.forward * forwardAmount * speed + verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
