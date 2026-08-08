using UnityEngine;
using UnityEngine.EventSystems;

namespace Sandbox.UI
{
    // Attach directly to the visible joystick background Image so its own
    // RectTransform doubles as the drag/touch hit area -- no separate
    // invisible touch zone needed at this control size.
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 55f;

        public Vector2 Value { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
            handle.anchoredPosition = clamped;
            Value = clamped / handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            handle.anchoredPosition = Vector2.zero;
            Value = Vector2.zero;
        }
    }
}
