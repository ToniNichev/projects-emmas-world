using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // A trigger volume spanning the climbable range of a ladder. Doesn't
    // move the player itself -- climbing fundamentally means suspending
    // normal gravity/walking, which only ThirdPersonController's own
    // Update loop can arbitrate, so this just tells it when the player has
    // entered or left the ladder and lets it read the rail/range below.
    [RequireComponent(typeof(Collider))]
    public class Ladder : MonoBehaviour
    {
        [SerializeField] private float bottomY;
        [SerializeField] private float topY;

        // World-space XZ the player is pulled toward while climbing, so
        // they can't drift off the side mid-climb -- the ladder's own
        // position, not the trigger volume's extents.
        public Vector3 RailPosition => transform.position;
        public float BottomY => bottomY;
        public float TopY => topY;

        private void OnTriggerEnter(Collider other)
        {
            ThirdPersonController player = other.GetComponent<ThirdPersonController>();
            if (player != null)
                player.EnterLadder(this);
        }

        private void OnTriggerExit(Collider other)
        {
            ThirdPersonController player = other.GetComponent<ThirdPersonController>();
            if (player != null)
                player.ExitLadder(this);
        }
    }
}
