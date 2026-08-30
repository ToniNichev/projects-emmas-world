using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sandbox.Player;
using Sandbox.UI;

namespace Sandbox.Environment
{
    // Shows a short, kid-friendly line of "wisdom" when the player
    // deliberately talks to the elder -- a generic wise traveler, not tied
    // to any specific real culture or spiritual tradition.
    //
    // Plain distance check each frame rather than a trigger-collider
    // callback: OnTriggerEnter only fires on a clean outside-to-inside
    // crossing within a single physics step, so a player who spawns
    // already inside/near the volume (editor Play Focused, a loaded save,
    // etc.) would never trigger it at all. A per-frame check can't miss
    // that -- same pattern TeeterTotter already uses to find the player.
    public class ElderTentSpeech : MonoBehaviour
    {
        [SerializeField] private Text displayText;
        [SerializeField] private Button talkButton;
        [SerializeField] private Text talkButtonLabel;
        [SerializeField] private Transform elderVisual;
        [SerializeField] private float triggerRadius = 3f;
        [SerializeField] private float appearDuration = 0.6f;
        [SerializeField] private float holdDuration = 3.5f;
        [SerializeField] private float vanishDuration = 1.3f;
        [SerializeField] private float riseDistance = 40f;
        [SerializeField] private float turnSpeedDegreesPerSecond = 180f;
        [SerializeField] private float bowDuration = 0.7f;
        [SerializeField] private float bowAngle = 18f;

        private static readonly string[] Lines =
        {
            "The tallest mountain is climbed one step at a time.",
            "A kind word costs nothing and is worth everything.",
            "Even the smallest spark can light up the dark.",
            "Curiosity is the compass that guides every explorer.",
            "True treasure is the friends you make along the way.",
            "Rest when you're tired, but never stop dreaming.",
            "Every big journey starts with a small step.",
            "Listen twice as much as you speak.",
        };

        private Transform player;
        private Outline outline;
        private Vector2 basePosition;
        private bool wasInside;
        private float showStartTime = -1f;
        private float currentYaw;
        private float bowStartTime = -1f;

        private void Awake()
        {
            ThirdPersonController controller = Object.FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
                player = controller.transform;

            if (displayText != null)
            {
                basePosition = displayText.rectTransform.anchoredPosition;
                outline = displayText.GetComponent<Outline>();
            }

            if (talkButton != null)
            {
                talkButton.gameObject.SetActive(false);
                if (talkButtonLabel != null)
                    talkButtonLabel.text = MobileControlsVisibility.IsTouchDevice ? "Talk" : "Press E to talk";
            }

            if (elderVisual != null)
                currentYaw = elderVisual.localEulerAngles.y;
        }

        private void Update()
        {
            if (displayText == null)
                return;

            if (player != null)
            {
                bool isInside = Vector3.Distance(transform.position, player.position) <= triggerRadius;
                if (isInside != wasInside && talkButton != null)
                    talkButton.gameObject.SetActive(isInside);

                // Keyboard works as a shortcut on desktop; touch players use
                // the on-screen button instead (wired to TriggerSpeech in
                // SceneBootstrapper), since there's no key to press.
                if (isInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    TriggerSpeech();

                if (isInside)
                    TurnTowardPlayer();

                wasInside = isInside;
            }

            UpdateSpeechAnimation();
            ApplyElderRotation();
        }

        // Turns the elder to keep facing whichever way the player is
        // standing while they're in range -- makes him read as actually
        // paying attention to the visitor instead of a static prop that
        // happens to have floating text appear near it.
        private void TurnTowardPlayer()
        {
            Vector3 toPlayer = player.position - elderVisual.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
                return;

            float targetYaw = Quaternion.LookRotation(toPlayer).eulerAngles.y;
            currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        // Wired to the on-screen talk button's onClick, and reachable via
        // the E key directly above -- one shared entry point so both input
        // methods produce identical behavior.
        public void TriggerSpeech()
        {
            displayText.text = Lines[Random.Range(0, Lines.Length)];
            showStartTime = Time.time;
            bowStartTime = Time.time;
        }

        // Combines the continuously-updated facing yaw with a brief bow --
        // a sine arc so the tilt eases in and back out rather than
        // snapping, timed to roughly coincide with the wisdom line
        // appearing. ElderTentSpeech owns rotation entirely once triggered;
        // the Animator (Idle-only, see BuildWizardAnimatorController) never
        // touches the root transform, so there's nothing to fight over.
        private void ApplyElderRotation()
        {
            if (elderVisual == null)
                return;

            float bowPitch = 0f;
            if (bowStartTime >= 0f)
            {
                float t = (Time.time - bowStartTime) / bowDuration;
                if (t >= 1f)
                    bowStartTime = -1f;
                else
                    bowPitch = Mathf.Sin(t * Mathf.PI) * bowAngle;
            }

            elderVisual.localRotation = Quaternion.Euler(bowPitch, currentYaw, 0f);
        }

        private void UpdateSpeechAnimation()
        {
            if (showStartTime < 0f)
                return;

            float elapsed = Time.time - showStartTime;
            float totalDuration = appearDuration + holdDuration + vanishDuration;
            if (elapsed >= totalDuration)
            {
                displayText.text = string.Empty;
                showStartTime = -1f;
                return;
            }

            float scale, alpha, rise;
            if (elapsed < appearDuration)
            {
                // Overshoots past 1 then eases back -- a "pop" as it grows
                // into place rather than a flat linear scale-up.
                float t = elapsed / appearDuration;
                scale = Mathf.Lerp(0.4f, 1f, EaseOutBack(t));
                alpha = Mathf.Lerp(0f, 1f, t);
                rise = Mathf.Lerp(-riseDistance, 0f, t);
            }
            else if (elapsed < appearDuration + holdDuration)
            {
                scale = 1f;
                alpha = 1f;
                rise = 0f;
            }
            else
            {
                // Keeps growing further past full size while fading out and
                // drifting upward -- dissolving into mist rather than a
                // flat cut to nothing.
                float t = (elapsed - appearDuration - holdDuration) / vanishDuration;
                scale = Mathf.Lerp(1f, 1.5f, t);
                alpha = Mathf.Lerp(1f, 0f, t);
                rise = Mathf.Lerp(0f, riseDistance * 1.5f, t);
            }

            displayText.rectTransform.localScale = Vector3.one * scale;
            displayText.rectTransform.anchoredPosition = basePosition + new Vector2(0f, rise);

            Color textColor = displayText.color;
            textColor.a = alpha;
            displayText.color = textColor;

            if (outline != null)
            {
                Color outlineColor = outline.effectColor;
                outlineColor.a = alpha * 0.8f;
                outline.effectColor = outlineColor;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
