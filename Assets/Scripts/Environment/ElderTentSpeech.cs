using UnityEngine;
using UnityEngine.UI;
using Sandbox.Player;

namespace Sandbox.Environment
{
    // Shows a short, kid-friendly line of "wisdom" on screen when the
    // player gets close -- a generic wise traveler, not tied to any
    // specific real culture or spiritual tradition.
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
        [SerializeField] private float triggerRadius = 3f;
        [SerializeField] private float appearDuration = 0.6f;
        [SerializeField] private float holdDuration = 3.5f;
        [SerializeField] private float vanishDuration = 1.3f;
        [SerializeField] private float riseDistance = 40f;

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
        }

        private void Update()
        {
            if (displayText == null)
                return;

            if (player != null)
            {
                bool isInside = Vector3.Distance(transform.position, player.position) <= triggerRadius;
                if (isInside && !wasInside)
                {
                    displayText.text = Lines[Random.Range(0, Lines.Length)];
                    showStartTime = Time.time;
                }
                wasInside = isInside;
            }

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
