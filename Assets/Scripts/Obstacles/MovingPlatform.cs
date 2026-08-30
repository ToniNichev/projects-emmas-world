using UnityEngine;

namespace Sandbox.Obstacles
{
    // Carries whoever's standing on it back and forth between two points by
    // temporarily re-parenting their transform under this one while they're
    // on it -- Unity's normal hierarchy then carries them for free however
    // this Update moves, every frame, automatically.
    //
    // An earlier version instead called CharacterController.Move directly
    // each frame with the platform's per-frame delta. That worked most of
    // the time but let the player slowly drift/slide off relative to the
    // platform: a second discrete Move() call composes imperfectly with
    // ThirdPersonController's own gravity-and-movement Move() call
    // happening the same frame, in whichever order Unity happens to run
    // the two scripts' Updates. Reparenting sidesteps the ordering problem
    // entirely since it's not an extra per-frame nudge to get exactly
    // right -- the child transform just *is* wherever the parent puts it.
    //
    // Deliberately lives on its own unscaled root GameObject rather than
    // the visually-scaled platform mesh directly (see
    // SceneBootstrapper.CreateMovingPlatform) -- CharacterController
    // explicitly doesn't support being parented under a non-uniformly
    // scaled transform, and this platform's visual is scaled non-uniformly
    // (flat and wide).
    [RequireComponent(typeof(Collider))]
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField] private Vector3 pointA;
        [SerializeField] private Vector3 pointB;
        [SerializeField] private float speed = 0.3f;

        private Transform rider;
        private Transform riderOriginalParent;

        private void Start()
        {
            transform.position = pointA;
        }

        private void Update()
        {
            float t = Mathf.PingPong(Time.time * speed, 1f);
            transform.position = Vector3.Lerp(pointA, pointB, t);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null)
                return;

            rider = other.transform;
            riderOriginalParent = rider.parent;
            rider.SetParent(transform, true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform != rider)
                return;

            rider.SetParent(riderOriginalParent, true);
            rider = null;
        }
    }
}
