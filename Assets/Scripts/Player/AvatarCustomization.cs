using UnityEngine;

namespace Sandbox.Player
{
    // Lets mirror swatch buttons recolor the player's own avatar. Renderers
    // switch to their own per-instance material the first time they're
    // recolored (Renderer.material, not sharedMaterial, auto-instances on
    // first access), so this never touches the shared material asset every
    // other avatar -- including remote players -- still uses.
    public class AvatarCustomization : MonoBehaviour
    {
        [SerializeField] private Renderer[] shirtRenderers;
        [SerializeField] private Renderer[] skinRenderers;
        [SerializeField] private Renderer[] legsRenderers;

        public void SetShirtColor(Color color) => Apply(shirtRenderers, color);
        public void SetSkinColor(Color color) => Apply(skinRenderers, color);
        public void SetLegsColor(Color color) => Apply(legsRenderers, color);

        private static void Apply(Renderer[] renderers, Color color)
        {
            if (renderers == null)
                return;

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    renderer.material.color = color;
            }
        }
    }
}
