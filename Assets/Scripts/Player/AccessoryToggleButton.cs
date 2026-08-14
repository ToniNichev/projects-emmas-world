using UnityEngine;

namespace Sandbox.Player
{
    // A single mirror accessory option (a hat style, a glasses color, a
    // backpack color, ...). Stores its own category/index as plain
    // serialized fields for the same reason ColorSwatchButton stores its
    // color that way: UnityEventTools.AddPersistentListener binds a
    // zero-argument method reference, not an arbitrary argument value.
    public class AccessoryToggleButton : MonoBehaviour
    {
        public enum Category { Hat, Glasses, Backpack }

        [SerializeField] private AvatarCustomization customization;
        [SerializeField] private Category category;
        [SerializeField] private int index;

        public void Apply()
        {
            if (customization == null)
                return;

            switch (category)
            {
                case Category.Hat:
                    customization.SetHat(index);
                    break;
                case Category.Glasses:
                    customization.SetGlasses(index);
                    break;
                case Category.Backpack:
                    customization.SetBackpack(index);
                    break;
            }
        }
    }
}
