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
        // Unity's built-in serializer cannot persist jagged arrays
        // (GameObject[][]/Renderer[][] silently fail to serialize -- the
        // field just never gets written to the scene file, so it comes
        // back null on load even though nothing errors at build time).
        // Wrapping each per-body array in a plain [Serializable] class
        // sidesteps that: an array of these wrapper objects serializes
        // fine, since it's a single-dimension array at the top level.
        // Public (not private) so SceneBootstrapper.cs -- which builds
        // these wrapper instances directly rather than through a public
        // API, since every avatar-building method here is editor-only
        // setup code -- can construct them.
        [System.Serializable]
        public class GameObjectGroup
        {
            public GameObject[] objects;
        }

        [System.Serializable]
        public class RendererGroup
        {
            public Renderer[] renderers;
        }

        // Each selectable body (MikeAlger, CartoonGirl, ...) is a fully
        // separate mesh/skeleton, so neither accessories nor color regions
        // can be shared across bodies -- a hat anchored to one body's Head
        // bone isn't parented under another body at all, and "shirt" on one
        // mesh has no counterpart renderer on a body with different
        // geometry. Every array below is therefore indexed by body first,
        // option second. A body with no separable region for a given slot
        // (e.g. ChibiGirl has no distinct shirt/skin/hair meshes at all)
        // just leaves that body's entry empty -- ApplyColor/SetActiveExclusive
        // no-op on an empty array, same as before this was per-body.
        [SerializeField] private GameObject[] bodies;
        [SerializeField] private GameObjectGroup[] hatsByBody;
        [SerializeField] private GameObjectGroup[] glassesByBody;
        [SerializeField] private GameObjectGroup[] backpacksByBody;
        [SerializeField] private RendererGroup[] shirtRenderersByBody;
        [SerializeField] private RendererGroup[] skinRenderersByBody;
        [SerializeField] private RendererGroup[] legsRenderersByBody;
        [SerializeField] private RendererGroup[] hairRenderersByBody;
        [SerializeField] private RendererGroup[] eyesRenderersByBody;
        [SerializeField] private RendererGroup[] lipsRenderersByBody;
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

        public void SetShirtColor(Color color) => ApplyColor(CurrentRenderers(shirtRenderersByBody), color);
        public void SetSkinColor(Color color) => ApplyColor(CurrentRenderers(skinRenderersByBody), color);
        public void SetLegsColor(Color color) => ApplyColor(CurrentRenderers(legsRenderersByBody), color);
        public void SetHairColor(Color color) => ApplyColor(CurrentRenderers(hairRenderersByBody), color);
        public void SetEyesColor(Color color) => ApplyColor(CurrentRenderers(eyesRenderersByBody), color);
        public void SetLipsColor(Color color) => ApplyColor(CurrentRenderers(lipsRenderersByBody), color);

        public void SetHat(int index) => SetActiveExclusive(CurrentObjects(hatsByBody), index);
        public void SetGlasses(int index) => SetActiveExclusive(CurrentObjects(glassesByBody), index);
        public void SetBackpack(int index) => SetActiveExclusive(CurrentObjects(backpacksByBody), index);

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

            SetActiveExclusive(CurrentObjects(hatsByBody), 0);
            SetActiveExclusive(CurrentObjects(glassesByBody), 0);
            SetActiveExclusive(CurrentObjects(backpacksByBody), 0);

            PlayerPrefs.SetInt(SelectedBodyPrefKey, index);
        }

        private GameObject[] CurrentObjects(GameObjectGroup[] byBody)
        {
            if (byBody == null || activeBodyIndex >= byBody.Length)
                return null;
            return byBody[activeBodyIndex]?.objects;
        }

        private Renderer[] CurrentRenderers(RendererGroup[] byBody)
        {
            if (byBody == null || activeBodyIndex >= byBody.Length)
                return null;
            return byBody[activeBodyIndex]?.renderers;
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
