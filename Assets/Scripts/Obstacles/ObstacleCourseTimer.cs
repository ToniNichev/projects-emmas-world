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

        public void StartRun()
        {
            startTime = Time.time;
            running = true;
            hasFinished = false;
        }

        public void StopRun()
        {
            if (!running)
                return;

            finalTime = Time.time - startTime;
            running = false;
            hasFinished = true;
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
