using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Environment
{
    // A bigger, localized one-shot splash where the player actually enters
    // the water -- on top of the sparse ambient ripples LakeSplashes
    // already scatters randomly across the whole surface. Reuses that same
    // particle system via Emit() rather than spinning up a second one.
    [RequireComponent(typeof(Collider))]
    public class LakeSplashTrigger : MonoBehaviour
    {
        [SerializeField] private ParticleSystem splashSystem;
        [SerializeField] private float surfaceY;
        [SerializeField] private int burstCount = 12;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<ThirdPersonController>() == null)
                return;

            Vector3 position = other.transform.position;
            position.y = surfaceY + 0.02f;

            var emitParams = new ParticleSystem.EmitParams { position = position };
            splashSystem.Emit(emitParams, burstCount);
        }
    }
}
