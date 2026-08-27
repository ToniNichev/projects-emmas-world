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
        [SerializeField] private Renderer[] hairRenderers;
        [SerializeField] private Renderer[] eyesRenderers;
        [SerializeField] private Renderer[] lipsRenderers;

        // Each selectable body (MikeAlger, CartoonGirl, ...) is a fully
        // separate skeleton, so hats/glasses/backpacks can't be shared
        // across bodies -- a hat anchored to one body's Head bone isn't
        // parented under the other body at all. Every accessory array is
        // therefore indexed by body first, option second.
        [SerializeField] private GameObject[] bodies;
        [SerializeField] private GameObject[][] hatsByBody;
        [SerializeField] private GameObject[][] glassesByBody;
        [SerializeField] private GameObject[][] backpacksByBody;
        [SerializeField] private HumanoidAnimatorDriver animatorDriver;

        private const string SelectedBodyPrefKey = "Avatar.SelectedBody";

        private int activeBodyIndex;

        private void Awake()
        {
            if (bodies == null || bodies.Length == 0)
                return;

            int saved = PlayerPrefs.GetInt(SelectedBodyPrefKey, 0);
            SetBody(Mathf.Clamp(saved, 0, bodies.Length - 1));
        }

        public void SetShirtColor(Color color) => ApplyColor(shirtRenderers, color);
        public void SetSkinColor(Color color) => ApplyColor(skinRenderers, color);
        public void SetLegsColor(Color color) => ApplyColor(legsRenderers, color);
        public void SetHairColor(Color color) => ApplyColor(hairRenderers, color);
        public void SetEyesColor(Color color) => ApplyColor(eyesRenderers, color);
        public void SetLipsColor(Color color) => ApplyColor(lipsRenderers, color);

        public void SetHat(int index) => SetActiveExclusive(CurrentArray(hatsByBody), index);
        public void SetGlasses(int index) => SetActiveExclusive(CurrentArray(glassesByBody), index);
        public void SetBackpack(int index) => SetActiveExclusive(CurrentArray(backpacksByBody), index);

        // Switching body resets that body's accessories to "None" rather
        // than carrying the old body's hat/glasses/backpack selection over
        // -- the anchors are per-skeleton, so there's no sensible mapping
        // from "cap on MikeAlger" to "cap on CartoonGirl" to carry anyway.
        public void SetBody(int index)
        {
            if (bodies == null || index < 0 || index >= bodies.Length)
                return;

            SetActiveExclusive(bodies, index);
            activeBodyIndex = index;

            if (animatorDriver != null)
                animatorDriver.SetAnimator(bodies[index].GetComponentInChildren<Animator>());

            SetActiveExclusive(CurrentArray(hatsByBody), 0);
            SetActiveExclusive(CurrentArray(glassesByBody), 0);
            SetActiveExclusive(CurrentArray(backpacksByBody), 0);

            PlayerPrefs.SetInt(SelectedBodyPrefKey, index);
        }

        private GameObject[] CurrentArray(GameObject[][] byBody)
        {
            if (byBody == null || activeBodyIndex >= byBody.Length)
                return null;
            return byBody[activeBodyIndex];
        }

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
