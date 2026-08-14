using UnityEngine;

namespace Sandbox.Player
{
    // Shows the customization panel while the player is standing near the
    // mirror, hides it otherwise. Plain distance check each frame rather
    // than a trigger-collider callback -- same reasoning as ElderTentSpeech:
    // OnTriggerEnter only fires on a clean outside-to-inside crossing and
    // can silently miss a player who spawns already inside the volume.
    public class MirrorProximityUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private float radius = 2.5f;

        private Transform player;

        private void Awake()
        {
            ThirdPersonController controller = Object.FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
                player = controller.transform;

            if (panel != null)
                panel.SetActive(false);
        }

        private void Update()
        {
            if (player == null || panel == null)
                return;

            bool isNear = Vector3.Distance(transform.position, player.position) <= radius;
            if (panel.activeSelf != isNear)
                panel.SetActive(isNear);
        }
    }
}
