using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.Multiplayer
{
    // Floating speech-bubble text above a player avatar (local or remote).
    // Builds its own world-space canvas at runtime instead of expecting a
    // pre-wired scene/prefab reference -- avatars are spawned procedurally
    // (see MultiplayerManager.EnsureRemoteAvatar) and the local player exists
    // before any chat message is ever sent, so there's no Inspector slot to
    // hook this up to ahead of time. Purely ephemeral: nothing here is ever
    // persisted, matching the rest of Emma's World chat.
    public class ChatBubble : MonoBehaviour
    {
        // "wave"/"dance" typed in chat show as a big, colored callout
        // instead of the literal word -- both local and remote players run
        // this same match in Show(), since everyone receives the identical
        // chat text (see MultiplayerManager.OnRemoteChat), so there's one
        // place this needs to be taught a new emote rather than duplicating
        // it in the send and receive paths separately.
        //
        // Deliberately plain text, not a Unicode emoji glyph: the bundled
        // LegacyRuntime.ttf this UI already uses elsewhere isn't guaranteed
        // to include emoji, so a literal "👋" risks rendering as a blank
        // box. Also not a real body animation -- the only animation pack
        // the avatars have (Assets/Models/Imported/PersonAnimated.fbx) is a
        // shooter/action mocap set (Punch, Swing, ShootPistol/Rifle, Crouch
        // variants, Swim, Standing) with nothing that reads as a friendly
        // wave or a dance, and reusing "Punch" for a greeting would look
        // wrong.
        private static readonly string[] EmoteKeywords = { "wave", "dance" };
        private static readonly string[] EmoteLabels = { "WAVES!", "DANCES!" };
        private static readonly Color[] EmoteColors =
        {
            new Color(1f, 0.85f, 0.2f),
            new Color(1f, 0.4f, 0.8f),
        };
        private const int EmoteFontSize = 48;
        private const int NormalFontSize = 22;

        private const float AppearDuration = 0.15f;
        private const float HoldDuration = 3f;
        private const float VanishDuration = 0.5f;
        private const float HeightPadding = 0.3f;

        private Canvas bubbleCanvas;
        private Text label;
        private Outline outline;
        private float showStartTime = -1f;
        private Camera cam;

        private void Awake()
        {
            BuildBubble(ComputeHeight());
            cam = Camera.main;
        }

        // Sits just above whatever renderers make up this avatar rather than
        // a fixed height, so the same component works for both the local
        // player's model and the (differently proportioned) remote avatar.
        private float ComputeHeight()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 1.8f;

            float maxY = float.MinValue;
            foreach (Renderer r in renderers)
                maxY = Mathf.Max(maxY, r.bounds.max.y);
            return Mathf.Max(0.5f, maxY - transform.position.y + HeightPadding);
        }

        private void BuildBubble(float height)
        {
            GameObject canvasGo = new GameObject("ChatBubbleCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, height, 0f);
            canvasGo.transform.localScale = Vector3.one * 0.008f;

            bubbleCanvas = canvasGo.AddComponent<Canvas>();
            bubbleCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(280f, 70f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = NormalFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.text = string.Empty;

            outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            canvasGo.SetActive(false);
        }

        // Re-showing while already visible just restarts the timer on the
        // new text -- no queueing, a fresh message simply replaces whatever
        // was still fading out.
        public void Show(string message)
        {
            if (label == null)
                return;

            int emoteIndex = MatchEmoteIndex(message);
            bool isEmote = emoteIndex >= 0;

            label.text = isEmote ? EmoteLabels[emoteIndex] : message;
            label.fontSize = isEmote ? EmoteFontSize : NormalFontSize;

            // Only the RGB is set here -- LateUpdate below owns alpha for
            // the fade in/out and re-applies it every frame regardless, so
            // setting a full opaque color here would just be overwritten
            // next frame anyway.
            Color rgb = isEmote ? EmoteColors[emoteIndex] : Color.white;
            rgb.a = label.color.a;
            label.color = rgb;

            showStartTime = Time.time;
            bubbleCanvas.gameObject.SetActive(true);
        }

        // Whole-word match (same reasoning as ElderTentSpeech's greeting
        // check) so "wavelength" or "romance" don't false-trigger.
        private static int MatchEmoteIndex(string message)
        {
            string[] words = message.ToLowerInvariant().Split(
                new[] { ' ', '\t', '!', '.', '?', ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                for (int i = 0; i < EmoteKeywords.Length; i++)
                {
                    if (word == EmoteKeywords[i])
                        return i;
                }
            }
            return -1;
        }

        private void LateUpdate()
        {
            if (showStartTime < 0f)
                return;

            if (cam == null)
                cam = Camera.main;
            if (cam != null)
                bubbleCanvas.transform.rotation = cam.transform.rotation;

            float elapsed = Time.time - showStartTime;
            float total = AppearDuration + HoldDuration + VanishDuration;
            if (elapsed >= total)
            {
                showStartTime = -1f;
                bubbleCanvas.gameObject.SetActive(false);
                return;
            }

            float alpha;
            if (elapsed < AppearDuration)
                alpha = Mathf.Lerp(0f, 1f, elapsed / AppearDuration);
            else if (elapsed < AppearDuration + HoldDuration)
                alpha = 1f;
            else
                alpha = Mathf.Lerp(1f, 0f, (elapsed - AppearDuration - HoldDuration) / VanishDuration);

            Color textColor = label.color;
            textColor.a = alpha;
            label.color = textColor;

            Color outlineColor = outline.effectColor;
            outlineColor.a = alpha * 0.8f;
            outline.effectColor = outlineColor;
        }
    }
}
