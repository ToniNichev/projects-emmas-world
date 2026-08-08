using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.UI
{
    // Image.alphaHitTestMinimumThreshold is intentionally not a
    // [SerializeField] in Unity's own UGUI source ("Not serialized until we
    // support read-enabled sprites better"), so setting it from editor
    // tooling has no effect once the scene is saved -- it silently resets to
    // 0 every time the scene loads. Applying it here at actual runtime
    // instead, where it works.
    [RequireComponent(typeof(Image))]
    public class AlphaHitTestSetter : MonoBehaviour
    {
        // Low, not 0.5 -- the corner-button sprite's solid areas are
        // deliberately translucent (alpha ~0.4, to match the joystick ring's
        // own translucent look), so a 0.5 threshold would reject every
        // click everywhere on the button, not just its transparent cutout.
        // Just needs to be comfortably above 0 (fully transparent gaps) and
        // below whatever alpha the visible shape actually renders at.
        [SerializeField] private float threshold = 0.05f;

        private void Awake()
        {
            GetComponent<Image>().alphaHitTestMinimumThreshold = threshold;
        }
    }
}
