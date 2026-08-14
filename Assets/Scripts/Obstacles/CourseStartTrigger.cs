using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // Sits on the course's start platform -- stepping onto it (re)starts
    // the timer, so retries after falling off just mean walking back to
    // the start rather than needing a manual reset.
    [RequireComponent(typeof(Collider))]
    public class CourseStartTrigger : MonoBehaviour
    {
        [SerializeField] private ObstacleCourseTimer timer;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<ThirdPersonController>() != null)
                timer.StartRun();
        }
    }
}
