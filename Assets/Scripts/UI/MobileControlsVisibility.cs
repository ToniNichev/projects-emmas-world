using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox.UI
{
    // Only touch devices get the on-screen joystick/buttons; keyboard and
    // mouse players don't need them cluttering the screen. Runtime-only
    // check -- the canvas must stay active in the saved scene so this Awake
    // actually gets a chance to run (a GameObject that starts disabled never
    // fires Awake until something else re-enables it).
    public class MobileControlsVisibility : MonoBehaviour
    {
        private void Awake()
        {
            // Touchscreen.current is only populated once the New Input
            // System has actually seen a touch event -- on a fresh page load
            // (e.g. iPad Safari before the player has touched anything) it's
            // still null even though the device is touch-capable, which was
            // hiding the controls permanently before they could ever be
            // used. Input.touchSupported is the legacy Input Manager's
            // static browser/OS feature-detection query, available
            // immediately at startup with no prior touch required.
            gameObject.SetActive(Input.touchSupported || Touchscreen.current != null);
        }
    }
}
