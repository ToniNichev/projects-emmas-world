using UnityEngine;

namespace Sandbox.Player
{
    // Puts the rider's hands on the handlebar grips while riding, using
    // Unity's humanoid hand IK on top of whatever pose the animator plays.
    // Lives on each avatar body's Animator object (OnAnimatorIK is only
    // called there), and needs "IK Pass" on in the animator controller.
    [RequireComponent(typeof(Animator))]
    public class BikeHandsIK : MonoBehaviour
    {
        // Elbows bend outward and back, like a rider holding wide bars.
        private const float ElbowOut = 0.3f;
        private const float ElbowBack = 0.35f;

        private Animator animator;
        private BikeRider rider;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            rider = GetComponentInParent<BikeRider>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            bool holding = rider != null && rider.IsRiding && rider.LeftGrip != null && rider.RightGrip != null;
            SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, holding ? rider.LeftGrip : null, -1f);
            SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, holding ? rider.RightGrip : null, 1f);
        }

        private void SetHand(AvatarIKGoal hand, AvatarIKHint elbow, Transform grip, float side)
        {
            float weight = grip != null ? 1f : 0f;
            animator.SetIKPositionWeight(hand, weight);
            animator.SetIKHintPositionWeight(elbow, weight);
            if (grip == null)
                return;

            Transform bike = rider.transform;
            animator.SetIKPosition(hand, grip.position);
            animator.SetIKHintPosition(elbow, grip.position + bike.right * (side * ElbowOut) - bike.forward * ElbowBack);
        }
    }
}
