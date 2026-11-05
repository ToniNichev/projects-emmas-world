using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.Obstacles
{
    // Central timer for the obstacle course: CourseStartTrigger starts it,
    // CourseFinishButton stops it. Lives on the same canvas as the display
    // text it drives.
    public class ObstacleCourseTimer : MonoBehaviour
    {
        [SerializeField] private Text displayText;

        private float startTime;
        private bool running;
        private float finalTime;
        private bool hasFinished;

        // Let other course systems (checkpoints) react to a run starting or
        // ending without the timer needing to know about them.
        public event System.Action RunStarted;
        public event System.Action RunFinished;

        public void StartRun()
        {
            startTime = Time.time;
            running = true;
            hasFinished = false;
            RunStarted?.Invoke();
        }

        public void StopRun()
        {
            if (!running)
                return;

            finalTime = Time.time - startTime;
            running = false;
            hasFinished = true;
            RunFinished?.Invoke();
        }

        private void Update()
        {
            if (displayText == null)
                return;

            if (running)
                displayText.text = $"Course: {Time.time - startTime:0.0}s";
            else if (hasFinished)
                displayText.text = $"Finished: {finalTime:0.0}s";
        }
    }
}
