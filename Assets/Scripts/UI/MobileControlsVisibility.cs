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
            gameObject.SetActive(Touchscreen.current != null);
        }
    }
}
