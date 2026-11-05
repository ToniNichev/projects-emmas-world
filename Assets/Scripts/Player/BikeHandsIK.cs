using UnityEngine;

namespace Sandbox.Player
{
    // Puts the rider's hands on the handlebar grips and feet on the pedals
    // while riding.
    //
    // Feet: foot IK pins each ankle just above its pedal as the cranks turn
    // (see BikeRider.UpdateCranks), with knees pushed forward and out.
    //
    // Hands, two steps each frame:
    //  1. OnAnimatorIK (humanoid hand IK) moves each wrist so the palm will
    //     sit on the grip, with elbows bent outward and back.
    //  2. LateUpdate, after the animator has written the pose, turns each
    //     hand palm-down with the fingers pointing forward over the bar and
    //     curls the fingers around it. The avatars' own animations have no
    //     gripping pose, so this is done directly on the bones.
    //
    // Everything is worked out from each avatar's own bones (finger and
    // thumb directions), so it works for every body without per-model
    // tuning. Lives on each body's Animator object (OnAnimatorIK is only
    // called there) and needs "IK Pass" on in the animator controller.
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(100)] // after BikeRider moves the avatar
    public class BikeHandsIK : MonoBehaviour
    {
        // Elbows bend outward and back, like a rider holding wide bars.
        private const float ElbowOut = 0.3f;
        private const float ElbowBack = 0.35f;
        // Fingers point forward and this much down over the bar.
        private const float WristDownDegrees = 25f;
        // How far along the hand (wrist to knuckles) the bar sits.
        private const float PalmFraction = 0.55f;
        private static readonly float[] CurlDegrees = { 60f, 75f, 50f }; // proximal, middle, tip
        // Knees point forward/up/out from the pedal, relative to bike size.
        private const float KneeForward = 0.5f;
        private const float KneeUp = 0.45f;
        private const float KneeOut = 0.12f;

        private static readonly HumanBodyBones[][] LeftFingers =
        {
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
        };
        private static readonly HumanBodyBones[][] RightFingers =
        {
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        private Animator animator;
        private BikeRider rider;
        private Transform avatarRoot;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            rider = GetComponentInParent<BikeRider>();
            avatarRoot = rider != null ? rider.transform.Find("Avatar") : null;
        }

        private bool Holding => rider != null && rider.IsRiding && rider.LeftGrip != null && rider.RightGrip != null;

        // Fingers forward and tipped down over the bar; back of the hand up.
        private Vector3 FingerDirection => Quaternion.AngleAxis(WristDownDegrees, rider.transform.right) * rider.transform.forward;
        private Vector3 BackOfHandDirection => Quaternion.AngleAxis(WristDownDegrees, rider.transform.right) * rider.transform.up;

        private void OnAnimatorIK(int layerIndex)
        {
            bool holding = Holding;
            SetWrist(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, LeftHandRig, holding ? rider.LeftGrip : null, -1f);
            SetWrist(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, RightHandRig, holding ? rider.RightGrip : null, 1f);

            bool pedaling = holding && rider.LeftPedal != null && rider.RightPedal != null;
            SetFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, pedaling ? rider.LeftPedal : null, -1f);
            SetFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, pedaling ? rider.RightPedal : null, 1f);
        }

        private void SetFoot(AvatarIKGoal goal, AvatarIKHint knee, Transform pedal, float side)
        {
            float weight = pedal != null ? 1f : 0f;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKHintPositionWeight(knee, weight);
            if (pedal == null)
                return;

            // The IK goal is the ankle, which sits above the sole. How far
            // above comes from the animated (crouched) pose before IK, in the
            // avatar's own space where the ground is at y = -1.
            float ankleHeight = 0.08f;
            if (avatarRoot != null)
                ankleHeight = Mathf.Max(0f, avatarRoot.InverseTransformPoint(animator.GetIKPosition(goal)).y + 1f);

            Transform bike = rider.transform;
            float size = pedal.lossyScale.y / Mathf.Max(0.0001f, bike.lossyScale.y); // bike scale
            animator.SetIKPosition(goal, pedal.position + bike.up * (ankleHeight + 0.015f * size));
            animator.SetIKHintPosition(knee, pedal.position
                + bike.forward * (KneeForward * size)
                + bike.up * (KneeUp * size)
                + bike.right * (side * KneeOut * size));
        }

        private void SetWrist(AvatarIKGoal goal, AvatarIKHint elbow, HandRig hand, Transform grip, float side)
        {
            float weight = grip != null ? 1f : 0f;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKHintPositionWeight(elbow, weight);
            if (grip == null)
                return;

            // Wrist sits back from the grip so the palm, not the wrist, is on the bar.
            Vector3 wrist = grip.position - FingerDirection * (hand.Length * PalmFraction);

            Transform bike = rider.transform;
            animator.SetIKPosition(goal, wrist);
            animator.SetIKHintPosition(elbow, grip.position + bike.right * (side * ElbowOut) - bike.forward * ElbowBack);
        }

        private void LateUpdate()
        {
            if (!Holding)
                return;
            GripHand(LeftHandRig, isRight: false);
            GripHand(RightHandRig, isRight: true);
        }

        private void GripHand(HandRig rig, bool isRight)
        {
            if (rig.Hand == null || rig.Knuckle == null || rig.Thumb == null)
                return;
            Transform hand = rig.Hand;

            // The hand's own directions, in its local space. With fingers
            // forward and palm down, a right thumb points left and a left
            // thumb right -- that's what tells palm from back of hand.
            Vector3 fingerLocal = hand.InverseTransformDirection(rig.Knuckle.position - hand.position).normalized;
            Vector3 thumbLocal = hand.InverseTransformDirection(rig.Thumb.position - hand.position).normalized;
            Vector3 palmLocal = isRight ? Vector3.Cross(fingerLocal, thumbLocal) : Vector3.Cross(thumbLocal, fingerLocal);
            if (palmLocal.sqrMagnitude < 1e-6f)
                return;

            Quaternion handFrame = Quaternion.LookRotation(fingerLocal, -palmLocal);
            Quaternion wanted = Quaternion.LookRotation(FingerDirection, BackOfHandDirection);
            hand.rotation = wanted * Quaternion.Inverse(handFrame);

            CurlFingers(rig.Fingers);
        }

        // Bends each finger toward the palm, around the bar.
        private void CurlFingers(Transform[][] fingers)
        {
            Vector3 forward = FingerDirection;
            Vector3 palm = -BackOfHandDirection;
            Vector3 axis = Vector3.Cross(BackOfHandDirection, forward).normalized;
            // Pick the rotation direction that moves fingertips toward the palm.
            if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * forward - forward, palm) < 0f)
                axis = -axis;

            foreach (Transform[] finger in fingers)
            {
                for (int i = 0; i < finger.Length && i < CurlDegrees.Length; i++)
                {
                    if (finger[i] != null)
                        finger[i].rotation = Quaternion.AngleAxis(CurlDegrees[i], axis) * finger[i].rotation;
                }
            }
        }

        // ----- Finding the hand's bones -----
        // Uses the humanoid finger mapping when the model has one. Some
        // models (the chibi girl) have finger bones that were never mapped,
        // so those are found by their standard Mixamo names instead
        // ("...HandIndex1", "...HandThumb1", ...).

        private class HandRig
        {
            public Transform Hand;
            public Transform Knuckle;
            public Transform Thumb;
            public Transform[][] Fingers;
            public float Length => Hand != null && Knuckle != null ? Vector3.Distance(Hand.position, Knuckle.position) : 0.08f;
        }

        private static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Pinky", "Little" };

        private HandRig leftHandRig;
        private HandRig rightHandRig;
        private HandRig LeftHandRig => leftHandRig ??= BuildHandRig(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftThumbProximal, LeftFingers);
        private HandRig RightHandRig => rightHandRig ??= BuildHandRig(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightThumbProximal, RightFingers);

        private HandRig BuildHandRig(HumanBodyBones handBone, HumanBodyBones middleBone, HumanBodyBones indexBone, HumanBodyBones thumbBone, HumanBodyBones[][] mappedFingers)
        {
            var rig = new HandRig { Hand = animator.GetBoneTransform(handBone) };
            if (rig.Hand == null)
            {
                rig.Fingers = new Transform[0][];
                return rig;
            }

            rig.Knuckle = animator.GetBoneTransform(middleBone) ?? animator.GetBoneTransform(indexBone);
            rig.Thumb = animator.GetBoneTransform(thumbBone);
            var fingers = new System.Collections.Generic.List<Transform[]>();
            foreach (HumanBodyBones[] chain in mappedFingers)
            {
                var bones = new Transform[chain.Length];
                bool any = false;
                for (int i = 0; i < chain.Length; i++)
                {
                    bones[i] = animator.GetBoneTransform(chain[i]);
                    any |= bones[i] != null;
                }
                if (any)
                    fingers.Add(bones);
            }

            if (rig.Knuckle == null || rig.Thumb == null || fingers.Count == 0)
            {
                Transform[] all = rig.Hand.GetComponentsInChildren<Transform>(true);
                Transform FindByName(string part)
                {
                    foreach (Transform t in all)
                    {
                        if (t.name.EndsWith(part, System.StringComparison.OrdinalIgnoreCase))
                            return t;
                    }
                    return null;
                }

                rig.Thumb ??= FindByName("Thumb1");
                if (fingers.Count == 0)
                {
                    foreach (string finger in FingerNames)
                    {
                        Transform first = FindByName(finger + "1");
                        if (first == null)
                            continue;
                        fingers.Add(new[] { first, FindByName(finger + "2"), FindByName(finger + "3") });
                    }
                }
                rig.Knuckle ??= FindByName("Middle1") ?? FindByName("Index1");
            }

            rig.Fingers = fingers.ToArray();
            return rig;
        }
    }
}
