using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // One flag partway up the obstacle course. Touching it tells the
    // parent CourseCheckpoints manager the player got this far; the manager
    // decides what to do with that.
    [RequireComponent(typeof(Collider))]
    public class CourseCheckpoint : MonoBehaviour
    {
        private static readonly Color IdleColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color ReachedColor = new Color(0.2f, 0.85f, 0.3f);

        // Higher = further up the course. A lower checkpoint touched after a
        // higher one (e.g. walking back down) never replaces it.
        [SerializeField] private int order;
        [SerializeField] private Renderer flagRenderer;

        private CourseCheckpoints manager;

        public int Order => order;

        // The trigger sits where a player standing on the step would be, so
        // its own position is exactly where to put them back.
        public Vector3 RespawnPoint => transform.position;

        private void Awake()
        {
            manager = GetComponentInParent<CourseCheckpoints>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (manager != null && other.GetComponent<ThirdPersonController>() != null)
                manager.Reach(this);
        }

        public void SetReached(bool reached)
        {
            if (flagRenderer != null)
                flagRenderer.material.color = reached ? ReachedColor : IdleColor;
        }
    }
}
