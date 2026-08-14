using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Cinemachine;
using Sandbox.UI;

namespace Sandbox.CameraControl
{
    public class OrbitCameraDragController : MonoBehaviour
    {
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private float sensitivity = 0.2f;
        [SerializeField] private float scrollZoomSpeed = 1.5f;
        [SerializeField] private float keyZoomSpeed = 8f;
        [SerializeField] private float minRadius = 2f;
        [SerializeField] private float maxRadius = 20f;
        [SerializeField] private float zoomSmoothSpeed = 10f;
        [SerializeField] private VirtualJoystick lookJoystick;
        // Degrees/sec of camera rotation at full stick deflection. The
        // original 70 felt too sensitive in real testing; lowered along with
        // making the joystick itself bigger (more finger travel needed to
        // reach full deflection in the first place).
        [SerializeField] private float lookJoystickSpeed = 45f;
        // Radius units per pixel of pinch-distance change. Like the original
        // zoom multipliers, this is an initial guess -- likely needs a real
        // device tuning pass.
        [SerializeField] private float pinchZoomSpeed = 0.02f;

        // A single scroll-wheel notch can report a large raw delta in one
        // frame (especially discrete mouse wheels vs. trackpads), which used
        // to snap Radius straight to it and looked like an instant jump.
        // Instead, input only moves this target, and Radius eases toward it
        // every frame regardless of how big the input spike was.
        private float targetRadius;

        // Null whenever fewer than two non-UI fingers are down, so a fresh
        // pinch gesture doesn't compute a bogus delta against a stale
        // distance from a previous, unrelated gesture.
        private float? previousPinchDistance;

        private void Awake()
        {
            if (orbitalFollow == null)
                orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

            if (orbitalFollow != null)
                targetRadius = orbitalFollow.Radius;
        }

        private void Update()
        {
            if (orbitalFollow == null)
                return;

            UpdateOrbit();
            UpdateZoom();
            UpdatePinchZoom();
        }

        private void UpdateOrbit()
        {
            float horizontalDelta;
            float verticalDelta;

            // Free-look, Roblox-style: any mouse movement orbits the camera,
            // no button needs to be held.
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            if (mouseDelta.sqrMagnitude > 0.0001f)
            {
                horizontalDelta = mouseDelta.x * sensitivity;
                verticalDelta = -mouseDelta.y * sensitivity;
            }
            else if (lookJoystick != null && lookJoystick.Value.sqrMagnitude > 0.0001f)
            {
                // The joystick reports a held direction, not a per-frame
                // delta, so it needs to be scaled by its own speed and
                // deltaTime rather than the mouse-pixel-tuned sensitivity.
                horizontalDelta = lookJoystick.Value.x * lookJoystickSpeed * Time.deltaTime;
                verticalDelta = -lookJoystick.Value.y * lookJoystickSpeed * Time.deltaTime;
            }
            else
            {
                return;
            }

            InputAxis horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Value = ApplyRange(horizontal.Value + horizontalDelta, horizontal.Range, horizontal.Wrap);
            orbitalFollow.HorizontalAxis = horizontal;

            InputAxis vertical = orbitalFollow.VerticalAxis;
            vertical.Value = ApplyRange(vertical.Value + verticalDelta, vertical.Range, vertical.Wrap);
            orbitalFollow.VerticalAxis = vertical;
        }

        private void UpdateZoom()
        {
            float zoomDelta = 0f;

            // Scroll up (positive y) zooms in, matching Roblox's convention.
            if (Mouse.current != null)
                zoomDelta -= Mouse.current.scroll.ReadValue().y * scrollZoomSpeed * 0.03f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.equalsKey.isPressed || Keyboard.current.numpadPlusKey.isPressed)
                    zoomDelta -= keyZoomSpeed * Time.deltaTime;
                if (Keyboard.current.minusKey.isPressed || Keyboard.current.numpadMinusKey.isPressed)
                    zoomDelta += keyZoomSpeed * Time.deltaTime;
            }

            if (!Mathf.Approximately(zoomDelta, 0f))
                targetRadius = Mathf.Clamp(targetRadius + zoomDelta, minRadius, maxRadius);

            // Ease toward the target every frame -- this is what actually
            // smooths things out, independent of how big a single scroll
            // event's raw delta was.
            orbitalFollow.Radius = Mathf.Lerp(orbitalFollow.Radius, targetRadius, 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime));
        }

        // Two-finger pinch: spreading fingers apart zooms in (shrinks
        // Radius), pinching together zooms out, matching the universal
        // photo/map pinch convention. Touches over UI (joysticks/corner
        // buttons) don't count, so dragging the look joystick with one
        // finger doesn't accidentally start a pinch with a second.
        private void UpdatePinchZoom()
        {
            if (Touchscreen.current == null)
            {
                previousPinchDistance = null;
                return;
            }

            TouchControl first = null;
            TouchControl second = null;
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (!touch.isInProgress || IsTouchOverUI(touch))
                    continue;

                if (first == null)
                    first = touch;
                else if (second == null)
                {
                    second = touch;
                    break;
                }
            }

            if (first == null || second == null)
            {
                previousPinchDistance = null;
                return;
            }

            float distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());

            if (previousPinchDistance.HasValue)
            {
                float delta = distance - previousPinchDistance.Value;
                targetRadius = Mathf.Clamp(targetRadius - delta * pinchZoomSpeed, minRadius, maxRadius);
            }

            previousPinchDistance = distance;
        }

        private static bool IsTouchOverUI(TouchControl touch)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue());
        }

        private static float ApplyRange(float value, Vector2 range, bool wrap)
        {
            float span = range.y - range.x;
            if (span <= 0f)
                return value;

            if (!wrap)
                return Mathf.Clamp(value, range.x, range.y);

            value = (value - range.x) % span;
            if (value < 0f)
                value += span;
            return value + range.x;
        }
    }
}
