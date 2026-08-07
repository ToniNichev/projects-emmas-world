using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Sandbox.CameraControl
{
    public class OrbitCameraDragController : MonoBehaviour
    {
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private float sensitivity = 0.2f;

        private void Awake()
        {
            if (orbitalFollow == null)
                orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void Update()
        {
            if (orbitalFollow == null || Mouse.current == null || !Mouse.current.rightButton.isPressed)
                return;

            Vector2 delta = Mouse.current.delta.ReadValue();

            InputAxis horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Value = ApplyRange(horizontal.Value + delta.x * sensitivity, horizontal.Range, horizontal.Wrap);
            orbitalFollow.HorizontalAxis = horizontal;

            InputAxis vertical = orbitalFollow.VerticalAxis;
            vertical.Value = ApplyRange(vertical.Value - delta.y * sensitivity, vertical.Range, vertical.Wrap);
            orbitalFollow.VerticalAxis = vertical;
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
