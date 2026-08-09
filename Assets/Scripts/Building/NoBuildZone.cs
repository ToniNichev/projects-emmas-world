using UnityEngine;

namespace Sandbox.Building
{
    // Disables block placement/removal for whichever player is standing
    // inside this trigger volume -- used to keep the obstacle course honest
    // (no bridging past a hard jump) without needing a separate world.
    // CharacterController generates trigger callbacks on its own GameObject,
    // so other here is the player's own collider, same object as BuildPlacer.
    [RequireComponent(typeof(Collider))]
    public class NoBuildZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            BuildPlacer placer = other.GetComponent<BuildPlacer>();
            if (placer != null)
                placer.BuildingAllowed = false;
        }

        private void OnTriggerExit(Collider other)
        {
            BuildPlacer placer = other.GetComponent<BuildPlacer>();
            if (placer != null)
                placer.BuildingAllowed = true;
        }
    }
}
