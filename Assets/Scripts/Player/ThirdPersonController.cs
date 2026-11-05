using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Audio;
using Sandbox.Obstacles;
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
        [SerializeField] private float climbSpeed = 3f;
        [SerializeField] private float ladderRailSnapSpeed = 8f;

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
        private Ladder currentLadder;
        private float lastGroundNormalY = 1f;

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

        // Lets a UI text field (chat input) take over the keyboard without
        // WASD also driving the character underneath it -- the on-screen
        // virtual joystick isn't gated here since a touch player can't drag
        // it and type at the same time anyway.
        public void SetInputEnabled(bool inputEnabled)
        {
            if (inputEnabled)
            {
                moveAction.Enable();
                sprintAction.Enable();
                jumpAction.Enable();
            }
            else
            {
                moveInput = Vector2.zero;
                sprintHeld = false;
                moveAction.Disable();
                sprintAction.Disable();
                jumpAction.Disable();
            }
        }

        // Public so the on-screen mobile jump button can call it directly
        // without needing to fake an InputAction.CallbackContext.
        public void TriggerJump()
        {
            // Jump doubles as "let go of the ladder" while climbing --
            // controller.isGrounded is generally false mid-climb anyway
            // (the player is hanging on a wall, not standing on ground), so
            // a normal jump wouldn't fire here regardless.
            if (currentLadder != null)
            {
                ExitLadder(currentLadder);
                return;
            }

            // Requiring the last touched surface to be close to flat
            // (normal.y > 0.7, roughly within 45 degrees of straight up)
            // closes off a known CharacterController quirk: repeatedly
            // jumping while pressed into a steep-but-not-vertical surface
            // (e.g. the elder tent's leaning wall panels) can make
            // isGrounded false-positive during the jump's own collision
            // resolution, letting a player "climb" the wall one jump at a
            // time. See OnControllerColliderHit below for where
            // lastGroundNormalY comes from.
            if (controller.isGrounded && lastGroundNormalY > 0.7f)
                jumpQueued = true;
        }

        // Unity calls this once per collider actually touched during the
        // most recent controller.Move() -- tracking it here is what lets
        // TriggerJump tell "standing on the ground" apart from "pressed
        // against a steep wall that isGrounded alone can't distinguish."
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            lastGroundNormalY = hit.normal.y;
        }

        // Called by Ladder.OnTriggerEnter -- entering the trigger volume is
        // enough to start climbing (no separate "grab on" input); with no
        // forward/back held the player just hangs in place, so grazing a
        // ladder while walking past isn't disruptive.
        public void EnterLadder(Ladder ladder)
        {
            currentLadder = ladder;
            verticalVelocity = Vector3.zero;
        }

        public void ExitLadder(Ladder ladder)
        {
            if (currentLadder == ladder)
                currentLadder = null;
        }

        private void Update()
        {
            if (currentLadder != null)
                ApplyLadderMovement();
            else
            {
                ApplyGravityAndJump();
                ApplyMovement();
            }

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

            float yBefore = transform.position.y;
            controller.Move(motion * Time.deltaTime);

            // CharacterController's own collide-and-slide can let simply
            // holding forward into a leaning (not fully vertical) wall
            // "climb" it -- no jumping needed. The horizontal push that the
            // wall blocks gets redirected to slide along the wall's own
            // surface instead, and that surface has an upward component
            // whenever it isn't perfectly vertical, exactly like walking up
            // a ramp. lastGroundNormalY (set by OnControllerColliderHit)
            // says the last surface actually touched was wall-steep rather
            // than ground; when that's true and we're airborne, strip out
            // any height gained beyond what verticalVelocity (gravity/jump)
            // alone would explain, so the wall genuinely blocks instead of
            // becoming a free ladder.
            if (!controller.isGrounded && lastGroundNormalY < 0.5f)
            {
                float expectedYChange = verticalVelocity.y * Time.deltaTime;
                float excessClimb = (transform.position.y - yBefore) - expectedYChange;
                if (excessClimb > 0f)
                    controller.Move(new Vector3(0f, -excessClimb, 0f));
            }
        }

        // No turning while climbing (matches the tank scheme's one-axis-
        // at-a-time feel) -- forward/back moves along world up/down instead
        // of walking, and the ladder's own XZ position is eased toward each
        // frame so the player can't drift off its side mid-climb. Gravity
        // is suspended entirely; ApplyGravityAndJump resumes it cleanly
        // once ExitLadder clears verticalVelocity's stale value away.
        private void ApplyLadderMovement()
        {
            float climbInput = CombinedMoveInput.y;
            float newY = Mathf.Clamp(
                transform.position.y + climbInput * climbSpeed * Time.deltaTime,
                currentLadder.BottomY,
                currentLadder.TopY);

            Vector3 rail = currentLadder.RailPosition;
            float snapT = Mathf.Clamp01(ladderRailSnapSpeed * Time.deltaTime);
            Vector3 delta = new Vector3(
                (rail.x - transform.position.x) * snapT,
                newY - transform.position.y,
                (rail.z - transform.position.z) * snapT);

            controller.Move(delta);
            verticalVelocity = Vector3.zero;
        }
    }
}
