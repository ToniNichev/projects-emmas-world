using UnityEngine;

namespace Sandbox.Player
{
    // Drives the real rigged model's Animator off ThirdPersonController's
    // existing movement state -- the same IsMoving/IsSprinting signals the
    // old procedural pivot-swing AvatarAnimator used, just feeding a real
    // Mecanim state machine (Idle/Walk/Run) instead of rotating limb
    // transforms by hand.
    public class HumanoidAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<ThirdPersonController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (animator == null || controller == null)
                return;

            animator.SetBool("IsMoving", controller.IsMoving);
            animator.SetBool("IsSprinting", controller.IsSprinting);
        }
    }
}
