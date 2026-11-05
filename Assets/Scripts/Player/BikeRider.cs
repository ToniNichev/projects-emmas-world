using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sandbox.Audio;
using Sandbox.UI;

namespace Sandbox.Player
{
    // Getting on and off the bike, plus how the bike looks while riding
    // (spinning wheels, leaning into turns, nose up/down in the air). The
    // riding itself lives in ThirdPersonController.ApplyBikeMovement.
    //
    // Hop on at the bike stand near spawn (E or the on-screen button), hop
    // off anywhere the same way.
    [RequireComponent(typeof(ThirdPersonController))]
    public class BikeRider : MonoBehaviour
    {
        private const float WheelRadius = 0.35f;
        private const float MaxLeanDegrees = 22f;
        private const float MaxPitchDegrees = 20f;
        // Riders stand on the pedals, so the avatar sits this much higher
        // (on a full-size bike; scaled with the bike).
        private const float RiderLift = 0.45f;
        // The bike is sized to each avatar so the hands reach the grips.
        private const float MinBikeScale = 0.4f;
        private const float MaxBikeScale = 1.1f;
        // Grips sit this far below the shoulders, as a fraction of arm length.
        private const float GripDropOfArm = 0.4f;
        // Never ask for more than a slightly bent arm.
        private const float ComfortableReach = 0.92f;

        [SerializeField] private GameObject bikeVisual;
        [SerializeField] private Transform frontWheel;
        [SerializeField] private Transform rearWheel;
        // Where the hands go (see BikeHandsIK).
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform bikeStand;
        [SerializeField] private float mountRadius = 3f;

        private ThirdPersonController controller;
        private SoundEffects soundEffects;
        private GameObject buttonGo;
        private Text buttonLabel;
        private float lean;
        private float pitch;
        private float wheelAngle;
        private float bikeScale = 1f;
        private readonly System.Collections.Generic.Dictionary<Animator, float> scaleByBody =
            new System.Collections.Generic.Dictionary<Animator, float>();

        public bool IsRiding => controller != null && controller.IsRiding;

        // A copy of the bike for showing on other players (MultiplayerManager).
        public GameObject BikeTemplate => bikeVisual;
        public Transform LeftGrip => leftGrip;
        public Transform RightGrip => rightGrip;

        private bool NearStand => bikeStand != null
            && Vector3.Distance(transform.position, bikeStand.position) <= mountRadius;

        private void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            soundEffects = GetComponent<SoundEffects>();
            if (bikeVisual != null)
                bikeVisual.SetActive(false);
            BuildButton();
        }

        private void Update()
        {
            bool canToggle = IsRiding || NearStand;
            if (buttonGo != null && buttonGo.activeSelf != canToggle)
                buttonGo.SetActive(canToggle);
            if (buttonLabel != null)
                buttonLabel.text = IsRiding ? "Get off bike (E)" : "Ride bike (E)";

            if (canToggle && !ChatInputUI.IsTyping && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            if (IsRiding)
                Dismount();
            else if (NearStand)
                Mount();
        }

        private void Mount()
        {
            controller.SetRiding(true);
            if (bikeVisual != null)
                bikeVisual.SetActive(true);
            soundEffects?.PlayJump();
        }

        private void Dismount()
        {
            controller.SetRiding(false);
            if (bikeVisual != null)
                bikeVisual.SetActive(false);
            lean = 0f;
            pitch = 0f;
            if (avatarRoot != null)
            {
                avatarRoot.localPosition = Vector3.zero;
                avatarRoot.localRotation = Quaternion.identity;
            }
            soundEffects?.PlayJump();
        }

        private void LateUpdate()
        {
            if (!IsRiding)
                return;

            FitBikeToRider();

            float speed = controller.BikeSpeed;
            float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / controller.BikeMaxSpeed);

            // Lean into turns (more at speed), tip the nose with the jump arc.
            float targetLean = -controller.SteerInput * MaxLeanDegrees * speedFactor;
            float targetPitch = controller.IsGrounded
                ? 0f
                : Mathf.Clamp(-controller.VerticalSpeed * 2.5f, -MaxPitchDegrees, MaxPitchDegrees);
            float smoothing = 1f - Mathf.Exp(-8f * Time.deltaTime);
            lean = Mathf.Lerp(lean, targetLean, smoothing);
            pitch = Mathf.Lerp(pitch, targetPitch, smoothing);
            Quaternion tilt = Quaternion.Euler(pitch, 0f, lean);

            // Both the bike and the rider tip around the point where the
            // wheels touch the ground (1 unit below the player's center).
            Vector3 groundPoint = new Vector3(0f, -1f, 0f);
            if (bikeVisual != null)
            {
                bikeVisual.transform.localPosition = groundPoint;
                bikeVisual.transform.localRotation = tilt;
            }
            if (avatarRoot != null)
            {
                avatarRoot.localPosition = groundPoint + tilt * new Vector3(0f, 1f + RiderLift * bikeScale, 0f);
                avatarRoot.localRotation = tilt;
            }

            wheelAngle += speed / (WheelRadius * bikeScale) * Mathf.Rad2Deg * Time.deltaTime;
            Quaternion spin = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (frontWheel != null)
                frontWheel.localRotation = spin;
            if (rearWheel != null)
                rearWheel.localRotation = spin;
        }

        private Animator ActiveBody()
        {
            if (avatarRoot == null)
                return null;
            foreach (Animator body in avatarRoot.GetComponentsInChildren<Animator>())
            {
                if (body.isActiveAndEnabled && body.isHuman)
                    return body;
            }
            return null;
        }

        // The three avatars are very different sizes (the girls are about
        // half as tall as the man), so the bike is scaled to whoever rides
        // it. Measured from the avatar's crouched riding pose, so it waits
        // for that pose to finish blending in, then remembers the answer
        // for that avatar. Switching avatars at the mirror while riding
        // just picks up the other avatar's size.
        private void FitBikeToRider()
        {
            Animator body = ActiveBody();
            if (body == null)
                return;

            if (!scaleByBody.TryGetValue(body, out float scale))
            {
                bool crouched = body.GetCurrentAnimatorStateInfo(0).IsName("Riding") && !body.IsInTransition(0);
                if (!crouched)
                    return;
                scale = MeasureBikeScale(body);
                scaleByBody[body] = scale;
            }

            if (!Mathf.Approximately(scale, bikeScale))
            {
                bikeScale = scale;
                if (bikeVisual != null)
                    bikeVisual.transform.localScale = Vector3.one * bikeScale;
            }
        }

        private float MeasureBikeScale(Animator body)
        {
            Transform upperArm = body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lowerArm = body.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            if (upperArm == null || lowerArm == null || hand == null || leftGrip == null || avatarRoot == null)
                return 1f;

            float armLength = Vector3.Distance(upperArm.position, lowerArm.position)
                + Vector3.Distance(lowerArm.position, hand.position);
            // In the avatar's own space (feet at y = -1), so it doesn't
            // matter where the avatar is right now or how it's leaning.
            Vector3 shoulder = avatarRoot.InverseTransformPoint(upperArm.position);
            Vector3 gripOnFullBike = leftGrip.localPosition;

            // On a bike of size s the shoulder rises by the pedal height
            // (RiderLift * s) and the grip sits at gripOnFullBike * s above
            // the ground. Solve for the size that puts the grip the wanted
            // distance below the shoulder...
            float shoulderHeight = shoulder.y + 1f;
            float scale = (shoulderHeight - GripDropOfArm * armLength) / (gripOnFullBike.y - RiderLift);
            scale = Mathf.Clamp(scale, MinBikeScale, MaxBikeScale);

            // ...then make sure the hand can actually get there.
            while (scale > MinBikeScale)
            {
                Vector3 shoulderOnBike = shoulder + new Vector3(0f, RiderLift * scale, 0f);
                Vector3 grip = new Vector3(0f, -1f, 0f) + gripOnFullBike * scale;
                if (Vector3.Distance(shoulderOnBike, grip) <= armLength * ComfortableReach)
                    break;
                scale -= 0.02f;
            }
            return scale;
        }

        // Built here at runtime, like the chat bar (see ChatInputUI). Bottom
        // center, left of the elder's Talk button spot.
        private void BuildButton()
        {
            GameObject canvasGo = new GameObject("BikeButtonCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            buttonGo = new GameObject("BikeButton", typeof(RectTransform));
            buttonGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 64f);
            rect.anchoredPosition = new Vector2(-330f, 40f);

            Image background = buttonGo.AddComponent<Image>();
            background.color = new Color(0.1f, 0.45f, 0.25f, 0.85f);
            Button button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                Toggle();
                // Don't leave the button selected, or Space/Enter would
                // keep "clicking" it.
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            });

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            buttonLabel = labelGo.AddComponent<Text>();
            buttonLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonLabel.fontSize = 26;
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            buttonLabel.color = Color.white;

            buttonGo.SetActive(false);
        }
    }
}
