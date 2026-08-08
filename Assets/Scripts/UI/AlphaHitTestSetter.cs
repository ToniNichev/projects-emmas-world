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
        [SerializeField] private float threshold = 0.5f;

        private void Awake()
        {
            GetComponent<Image>().alphaHitTestMinimumThreshold = threshold;
        }
    }
}
