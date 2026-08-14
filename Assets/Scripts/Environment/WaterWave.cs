using UnityEngine;

namespace Sandbox.Environment
{
    // Displaces the water mesh's vertices with a sum of two overlapping sine
    // waves each frame -- real per-vertex motion instead of just a scrolling
    // texture is what keeps a lake from reading as a static, perfectly flat
    // disc. Requires a mesh with interior vertices to displace (a plain
    // Cylinder's cap is just a fan from rim to one center vertex); see
    // SceneBootstrapper.CreateWaterMesh for the radial grid this needs.
    [RequireComponent(typeof(MeshFilter))]
    public class WaterWave : MonoBehaviour
    {
        [SerializeField] private float waveHeight = 0.08f;
        [SerializeField] private float waveFrequency = 0.5f;
        [SerializeField] private float waveSpeed = 1f;

        private Mesh mesh;
        private Vector3[] baseVertices;
        private Vector3[] displacedVertices;

        private void Awake()
        {
            // .mesh (not sharedMesh) clones it per-instance on first access,
            // so this only ever mutates this lake's own copy, not the saved
            // asset on disk.
            mesh = GetComponent<MeshFilter>().mesh;
            baseVertices = mesh.vertices;
            displacedVertices = new Vector3[baseVertices.Length];
        }

        private void Update()
        {
            float t = Time.time * waveSpeed;
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float wave = Mathf.Sin((v.x + v.z) * waveFrequency + t)
                           + Mathf.Sin((v.x - v.z) * waveFrequency * 1.3f + t * 1.7f);
                displacedVertices[i] = new Vector3(v.x, wave * waveHeight, v.z);
            }

            mesh.vertices = displacedVertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
