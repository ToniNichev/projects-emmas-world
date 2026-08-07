using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintSpeed = 10f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController controller;
        private Vector2 moveInput;
        private bool sprintHeld;
        private bool jumpQueued;
        private Vector3 verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            sprintHeld = context.ReadValueAsButton();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed && controller.isGrounded)
                jumpQueued = true;
        }

        private void Update()
        {
            ApplyGravityAndJump();
            ApplyMovement();
        }

        private void ApplyGravityAndJump()
        {
            if (controller.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;

            if (jumpQueued)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpQueued = false;
            }

            verticalVelocity.y += gravity * Time.deltaTime;
        }

        private void ApplyMovement()
        {
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            if (inputDir.sqrMagnitude < 0.0001f)
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

            Vector3 moveDir = (forward * inputDir.z + right * inputDir.x).normalized;
            float speed = sprintHeld ? sprintSpeed : moveSpeed;

            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            Vector3 motion = moveDir * speed + verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
