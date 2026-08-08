using UnityEngine;

namespace Sandbox.Environment
{
    public class WaterAnimator : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.05f, 0.03f);

        private Renderer waterRenderer;

        private void Awake()
        {
            waterRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (waterRenderer == null)
                return;

            // .material (not .sharedMaterial) clones it per-instance on first
            // access, so this scrolls only this lake's copy, not the shared
            // Water asset on disk.
            waterRenderer.material.mainTextureOffset = new Vector2(Time.time * scrollSpeed.x, Time.time * scrollSpeed.y);
        }
    }
}
