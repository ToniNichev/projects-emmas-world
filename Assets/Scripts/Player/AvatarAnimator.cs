using UnityEngine;

namespace Sandbox.Player
{
    public class AvatarAnimator : MonoBehaviour
    {
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private float swingSpeed = 8f;
        [SerializeField] private float sprintSwingSpeed = 12f;
        [SerializeField] private float swingAmplitude = 35f;
        [SerializeField] private float returnSpeed = 8f;

        private Transform leftArmPivot;
        private Transform rightArmPivot;
        private Transform leftLegPivot;
        private Transform rightLegPivot;
        private float swingPhase;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<ThirdPersonController>();

            Transform avatarRoot = transform.Find("Avatar");
            if (avatarRoot == null)
                return;

            leftArmPivot = avatarRoot.Find("LeftArmPivot");
            rightArmPivot = avatarRoot.Find("RightArmPivot");
            leftLegPivot = avatarRoot.Find("LeftLegPivot");
            rightLegPivot = avatarRoot.Find("RightLegPivot");
        }

        private void Update()
        {
            bool isMoving = controller != null && controller.IsMoving;

            float targetAngle = 0f;
            if (isMoving)
            {
                float speed = controller.IsSprinting ? sprintSwingSpeed : swingSpeed;
                swingPhase += Time.deltaTime * speed;
                targetAngle = Mathf.Sin(swingPhase) * swingAmplitude;
            }
            else
            {
                swingPhase = 0f;
            }

            // Contralateral gait: left arm swings with right leg, and vice versa.
            SetSwing(leftArmPivot, targetAngle);
            SetSwing(rightArmPivot, -targetAngle);
            SetSwing(leftLegPivot, -targetAngle);
            SetSwing(rightLegPivot, targetAngle);
        }

        private void SetSwing(Transform pivot, float angle)
        {
            if (pivot == null)
                return;

            Quaternion target = Quaternion.Euler(angle, 0f, 0f);
            pivot.localRotation = Quaternion.Slerp(pivot.localRotation, target, returnSpeed * Time.deltaTime);
        }
    }
}
