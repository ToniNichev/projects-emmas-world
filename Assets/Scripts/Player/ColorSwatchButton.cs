using UnityEngine;

namespace Sandbox.Player
{
    // A single mirror swatch: stores its own color as a plain serialized
    // field rather than passing it as a UnityEvent argument, specifically
    // so Button.onClick can wire to a persistent, zero-argument listener --
    // UnityEventTools.AddPersistentListener (the only way a listener added
    // from an editor script survives being saved into the scene) binds a
    // method reference, not an arbitrary argument value like a Color.
    public class ColorSwatchButton : MonoBehaviour
    {
        public enum Target { Shirt, Skin, Legs }

        [SerializeField] private AvatarCustomization customization;
        [SerializeField] private Target target;
        [SerializeField] private Color color;

        public void Apply()
        {
            if (customization == null)
                return;

            switch (target)
            {
                case Target.Shirt:
                    customization.SetShirtColor(color);
                    break;
                case Target.Skin:
                    customization.SetSkinColor(color);
                    break;
                case Target.Legs:
                    customization.SetLegsColor(color);
                    break;
            }
        }
    }
}
