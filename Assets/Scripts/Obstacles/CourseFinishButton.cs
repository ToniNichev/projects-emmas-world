using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // The red button at the top of the course -- walking into it stops the
    // timer and locks in the run's final time.
    [RequireComponent(typeof(Collider))]
    public class CourseFinishButton : MonoBehaviour
    {
        [SerializeField] private ObstacleCourseTimer timer;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<ThirdPersonController>() != null)
                timer.StopRun();
        }
    }
}
