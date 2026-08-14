using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // A seesaw plank: tilts toward whichever end the player is standing
    // closer to. Not physics-driven (CharacterController doesn't push
    // Rigidbodies the way a real seesaw would need) -- instead this reads
    // the player's position along the plank each frame and eases the tilt
    // toward a matching angle, which feels right without needing a full
    // physics rig.
    public class TeeterTotter : MonoBehaviour
    {
        [SerializeField] private float halfLength = 2f;
        [SerializeField] private float maxTiltDegrees = 22f;
        [SerializeField] private float tiltSpeed = 4f;

        private Transform player;
        private Quaternion baseRotation;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            ThirdPersonController controller = Object.FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
                player = controller.transform;
        }

        private void Update()
        {
            float targetTilt = 0f;
            if (player != null)
            {
                float localX = transform.InverseTransformPoint(player.position).x;
                float normalized = Mathf.Clamp(localX / halfLength, -1f, 1f);
                targetTilt = -normalized * maxTiltDegrees;
            }

            Quaternion targetRotation = baseRotation * Quaternion.Euler(0f, 0f, targetTilt);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, tiltSpeed * Time.deltaTime);
        }
    }
}
