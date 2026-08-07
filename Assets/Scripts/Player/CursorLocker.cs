using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox.Player
{
    public class CursorLocker : MonoBehaviour
    {
        private void OnEnable()
        {
            Lock();
        }

        private void OnDisable()
        {
            Unlock();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    Unlock();
                else
                    Lock();
            }
        }

        private static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
