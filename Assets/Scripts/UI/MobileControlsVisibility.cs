using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Sandbox.UI
{
    // Only touch devices get the on-screen joystick/buttons; keyboard and
    // mouse players don't need them cluttering the screen. Runtime-only
    // check -- the canvas must stay active in the saved scene so this Awake
    // actually gets a chance to run (a GameObject that starts disabled never
    // fires Awake until something else re-enables it).
    public class MobileControlsVisibility : MonoBehaviour
    {
        // Single source of truth for "treat this session as touch" --
        // BuildPlacer's aim-point logic reads this too, so the on-screen
        // crosshair/joysticks and the actual aiming behavior can never
        // disagree with each other.
        public static bool IsTouchDevice { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int EmmasWorld_IsTouchDevice();
#endif

        private void Awake()
        {
            IsTouchDevice = DetectTouchDevice();
            gameObject.SetActive(IsTouchDevice);
        }

        private static bool DetectTouchDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Touchscreen.current (New Input System) is only populated once a
            // touch event has actually fired, and Input.touchSupported
            // (legacy Input Manager) is unreliable in WebGL -- both gave
            // false positives/negatives in real testing. navigator.maxTouchPoints
            // via a small JS bridge reflects real hardware immediately.
            return EmmasWorld_IsTouchDevice() != 0;
#else
            return Touchscreen.current != null;
#endif
        }
    }
}
