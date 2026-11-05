using UnityEngine;
using Sandbox.Audio;
using Sandbox.Player;

namespace Sandbox.Obstacles
{
    // Puts the player back on the last checkpoint flag they touched when
    // they fall off the obstacle course, instead of making them climb the
    // whole tower again.
    //
    // The course stands on the ground, so falling off it never drops you
    // out of the world -- you just land at its base. "Fell off" here means:
    // a real fall (not a step down the stairs), landing inside the course
    // area, below the last checkpoint.
    public class CourseCheckpoints : MonoBehaviour
    {
        [SerializeField] private ObstacleCourseTimer timer;
        [SerializeField] private Vector3 courseCenter;
        [SerializeField] private float courseRadius = 10f;
        [SerializeField] private float minFallDistance = 3f;

        private CourseCheckpoint[] checkpoints;
        private CourseCheckpoint active;
        private ThirdPersonController player;
        private SoundEffects soundEffects;

        private void Start()
        {
            checkpoints = GetComponentsInChildren<CourseCheckpoint>();

            player = Object.FindFirstObjectByType<ThirdPersonController>();
            if (player != null)
            {
                player.Landed += OnPlayerLanded;
                soundEffects = player.GetComponent<SoundEffects>();
            }

            // Starting a new run (stepping onto the start platform) or
            // finishing one wipes progress, so every run starts fresh.
            if (timer != null)
            {
                timer.RunStarted += ClearAll;
                timer.RunFinished += ClearAll;
            }

            ClearAll();
        }

        private void OnDestroy()
        {
            if (player != null)
                player.Landed -= OnPlayerLanded;
            if (timer != null)
            {
                timer.RunStarted -= ClearAll;
                timer.RunFinished -= ClearAll;
            }
        }

        public void Reach(CourseCheckpoint checkpoint)
        {
            if (active != null && checkpoint.Order <= active.Order)
                return;

            active = checkpoint;
            checkpoint.SetReached(true);
            soundEffects?.PlayCheckpoint();
        }

        private void ClearAll()
        {
            active = null;
            if (checkpoints == null)
                return;
            foreach (CourseCheckpoint checkpoint in checkpoints)
                checkpoint.SetReached(false);
        }

        private void OnPlayerLanded(float fallDistance)
        {
            if (active == null || fallDistance < minFallDistance)
                return;

            Vector3 position = player.transform.position;
            Vector2 fromCenter = new Vector2(position.x - courseCenter.x, position.z - courseCenter.z);
            if (fromCenter.magnitude > courseRadius)
                return;

            // Landed on or above the checkpoint (e.g. a short hop between
            // upper steps) -- nothing was lost, so leave them be.
            if (position.y > active.RespawnPoint.y - 1f)
                return;

            player.TeleportTo(active.RespawnPoint, player.transform.rotation);
        }
    }
}
