using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

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

        private void Awake()
        {
            if (orbitalFollow == null)
                orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void Update()
        {
            if (orbitalFollow == null)
                return;

            UpdateOrbit();
            UpdateZoom();
        }

        private void UpdateOrbit()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.isPressed)
                return;

            Vector2 delta = Mouse.current.delta.ReadValue();

            InputAxis horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Value = ApplyRange(horizontal.Value + delta.x * sensitivity, horizontal.Range, horizontal.Wrap);
            orbitalFollow.HorizontalAxis = horizontal;

            InputAxis vertical = orbitalFollow.VerticalAxis;
            vertical.Value = ApplyRange(vertical.Value - delta.y * sensitivity, vertical.Range, vertical.Wrap);
            orbitalFollow.VerticalAxis = vertical;
        }

        private void UpdateZoom()
        {
            float zoomDelta = 0f;

            // Scroll up (positive y) zooms in, matching Roblox's convention.
            // The 0.03 multiplier (vs. an initial untested guess of 0.01) is
            // tuned for trackpad/Magic Mouse-style continuous small-delta
            // scrolling rather than discrete notch-wheel deltas -- confirmed
            // too slow to be usable in real testing at the lower value.
            if (Mouse.current != null)
                zoomDelta -= Mouse.current.scroll.ReadValue().y * scrollZoomSpeed * 0.03f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.equalsKey.isPressed || Keyboard.current.numpadPlusKey.isPressed)
                    zoomDelta -= keyZoomSpeed * Time.deltaTime;
                if (Keyboard.current.minusKey.isPressed || Keyboard.current.numpadMinusKey.isPressed)
                    zoomDelta += keyZoomSpeed * Time.deltaTime;
            }

            if (Mathf.Approximately(zoomDelta, 0f))
                return;

            orbitalFollow.Radius = Mathf.Clamp(orbitalFollow.Radius + zoomDelta, minRadius, maxRadius);
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
