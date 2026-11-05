using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sandbox.Multiplayer;
using Sandbox.Player;

namespace Sandbox.UI
{
    // A persistent chat bar, built entirely at runtime -- avatars and most
    // of this game's UI are procedurally spawned (see SceneBootstrapper),
    // so there's no baked scene/prefab slot to wire an Inspector reference
    // into. Always visible rather than an open/close toggle, matching the
    // ephemeral, no-history nature of Emma's World chat (see ChatBubble).
    // Reuses the scene's single existing EventSystem (built once, in
    // SceneBootstrapper.BuildEventSystem) rather than creating another one.
    public class ChatInputUI : MonoBehaviour
    {
        // True while the chat box has keyboard focus. Anything with its own
        // keyboard shortcuts (block undo/remove, elder talk, camera zoom)
        // checks this so typing a message doesn't also trigger them.
        public static bool IsTyping { get; private set; }

        private MultiplayerManager multiplayerManager;
        private ThirdPersonController playerController;
        private InputField inputField;
        private bool wasFocused;

        private void Awake()
        {
            multiplayerManager = GetComponent<MultiplayerManager>();
            playerController = Object.FindFirstObjectByType<ThirdPersonController>();
            BuildUI();
        }

        private void Update()
        {
            bool focused = inputField.isFocused;

            if (focused != wasFocused)
            {
                playerController?.SetInputEnabled(!focused);
                wasFocused = focused;
                IsTyping = focused;
            }
        }

        private void OnDisable()
        {
            if (wasFocused)
                playerController?.SetInputEnabled(true);
            IsTyping = false;
            wasFocused = false;
        }

        // Fires on Enter, Escape, and clicking away. Only Enter sends; all of
        // them drop focus so the game's keys work again right away.
        private void OnEndEdit(string _)
        {
            Keyboard keyboard = Keyboard.current;
            bool enterPressed = keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            if (enterPressed)
                TrySend();

            // When focus moved because something else got selected (e.g. the
            // Send button), the EventSystem is mid-selection and must not be
            // re-entered.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting
                && eventSystem.currentSelectedGameObject == inputField.gameObject)
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnSendClicked()
        {
            TrySend();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // Text stays in the box if the send was rejected (sending too fast),
        // so the player can just try again instead of retyping it.
        private void TrySend()
        {
            if (string.IsNullOrWhiteSpace(inputField.text))
                return;
            if (multiplayerManager.RequestSendChat(inputField.text))
                inputField.text = string.Empty;
        }

        // Top-left rather than bottom-left, which is where the mobile
        // virtual joystick lives (see SceneBootstrapper.BuildMobileControls)
        // -- putting chat there would mean the two constantly overlap.
        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("ChatInputCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject fieldGo = new GameObject("ChatInputField", typeof(RectTransform));
            fieldGo.transform.SetParent(canvasGo.transform, false);
            RectTransform fieldRect = fieldGo.GetComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0f, 1f);
            fieldRect.anchorMax = new Vector2(0f, 1f);
            fieldRect.pivot = new Vector2(0f, 1f);
            fieldRect.anchoredPosition = new Vector2(20f, -20f);
            fieldRect.sizeDelta = new Vector2(360f, 50f);

            Image fieldBackground = fieldGo.AddComponent<Image>();
            fieldBackground.color = new Color(0f, 0f, 0f, 0.5f);

            inputField = fieldGo.AddComponent<InputField>();
            inputField.characterLimit = MultiplayerManager.MaxChatLength;
            inputField.onEndEdit.AddListener(OnEndEdit);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(fieldGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);
            Text text = textGo.AddComponent<Text>();
            text.font = font;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            inputField.textComponent = text;

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(fieldGo.transform, false);
            RectTransform placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 6f);
            placeholderRect.offsetMax = new Vector2(-10f, -6f);
            Text placeholderText = placeholderGo.AddComponent<Text>();
            placeholderText.font = font;
            placeholderText.text = "Say something...";
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.5f);
            inputField.placeholder = placeholderText;

            GameObject buttonGo = new GameObject("SendButton", typeof(RectTransform));
            buttonGo.transform.SetParent(canvasGo.transform, false);
            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(390f, -20f);
            buttonRect.sizeDelta = new Vector2(90f, 50f);

            Image buttonBackground = buttonGo.AddComponent<Image>();
            buttonBackground.color = new Color(0.2f, 0.5f, 0.9f, 0.9f);
            Button sendButton = buttonGo.AddComponent<Button>();
            sendButton.onClick.AddListener(OnSendClicked);

            GameObject buttonTextGo = new GameObject("Text", typeof(RectTransform));
            buttonTextGo.transform.SetParent(buttonGo.transform, false);
            RectTransform buttonTextRect = buttonTextGo.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            Text buttonText = buttonTextGo.AddComponent<Text>();
            buttonText.font = font;
            buttonText.text = "Send";
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
        }
    }
}
