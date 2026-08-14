using UnityEngine;

namespace Sandbox.Player
{
    // Lets mirror swatch/toggle buttons customize the player's own avatar.
    // Color changes switch a renderer to its own per-instance material the
    // first time they're recolored (Renderer.material, not sharedMaterial,
    // auto-instances on first access), so this never touches the shared
    // material asset every other avatar -- including remote players --
    // still uses.
    public class AvatarCustomization : MonoBehaviour
    {
        [SerializeField] private Renderer[] shirtRenderers;
        [SerializeField] private Renderer[] skinRenderers;
        [SerializeField] private Renderer[] legsRenderers;

        // Index 0 in each array is the "None" option and is left as a null
        // entry rather than a real GameObject -- SetActiveExclusive skips
        // null entries when activating, so requesting index 0 just
        // deactivates every real option in the category.
        [SerializeField] private GameObject[] hats;
        [SerializeField] private GameObject[] glasses;
        [SerializeField] private GameObject[] backpacks;

        public void SetShirtColor(Color color) => ApplyColor(shirtRenderers, color);
        public void SetSkinColor(Color color) => ApplyColor(skinRenderers, color);
        public void SetLegsColor(Color color) => ApplyColor(legsRenderers, color);

        public void SetHat(int index) => SetActiveExclusive(hats, index);
        public void SetGlasses(int index) => SetActiveExclusive(glasses, index);
        public void SetBackpack(int index) => SetActiveExclusive(backpacks, index);

        private static void ApplyColor(Renderer[] renderers, Color color)
        {
            if (renderers == null)
                return;

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    renderer.material.color = color;
            }
        }

        private static void SetActiveExclusive(GameObject[] options, int index)
        {
            if (options == null)
                return;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                    options[i].SetActive(i == index);
            }
        }
    }
}
