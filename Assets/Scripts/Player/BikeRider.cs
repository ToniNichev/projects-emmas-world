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
        // Riders stand on the pedals, so the avatar sits this much higher.
        private const float RiderLift = 0.45f;

        [SerializeField] private GameObject bikeVisual;
        [SerializeField] private Transform frontWheel;
        [SerializeField] private Transform rearWheel;
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

        public bool IsRiding => controller != null && controller.IsRiding;

        // A copy of the bike for showing on other players (MultiplayerManager).
        public GameObject BikeTemplate => bikeVisual;

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
                avatarRoot.localPosition = groundPoint + tilt * new Vector3(0f, 1f + RiderLift, 0f);
                avatarRoot.localRotation = tilt;
            }

            wheelAngle += speed / WheelRadius * Mathf.Rad2Deg * Time.deltaTime;
            Quaternion spin = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (frontWheel != null)
                frontWheel.localRotation = spin;
            if (rearWheel != null)
                rearWheel.localRotation = spin;
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
