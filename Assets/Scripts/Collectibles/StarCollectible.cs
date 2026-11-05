using UnityEngine;
using Sandbox.Player;

namespace Sandbox.Collectibles
{
    // A spinning, bobbing gold star. Walking into it collects it (see
    // StarCounter). The star shape is generated here at runtime, so there's
    // no model file to import.
    [RequireComponent(typeof(Collider))]
    public class StarCollectible : MonoBehaviour
    {
        private const float SpinDegreesPerSecond = 90f;
        private const float BobHeight = 0.15f;
        private const float BobSpeed = 2f;

        // Stable name saved in the player's progress, so it must never
        // change once a star is published.
        [SerializeField] private string starId;

        private static Mesh starMesh;
        private Vector3 basePosition;
        private float bobPhase;

        public string StarId => starId;

        private void Awake()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = GetStarMesh();

            basePosition = transform.position;
            // Spread the bobbing so nearby stars don't move in lockstep.
            bobPhase = basePosition.x * 0.7f + basePosition.z * 0.3f;
        }

        private void Update()
        {
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * BobSpeed + bobPhase) * BobHeight);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<ThirdPersonController>() != null && StarCounter.Instance != null)
                StarCounter.Instance.Collect(this);
        }

        // A puffy five-pointed star: a ring of alternating outer/inner
        // points, with a raised center vertex on each side.
        private static Mesh GetStarMesh()
        {
            if (starMesh != null)
                return starMesh;

            const int points = 5;
            const float outerRadius = 0.5f;
            const float innerRadius = 0.22f;
            const float puff = 0.14f;
            int rimCount = points * 2;

            var vertices = new Vector3[rimCount + 2];
            int front = rimCount;
            int back = rimCount + 1;
            for (int i = 0; i < rimCount; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outerRadius : innerRadius;
                vertices[i] = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            }
            vertices[front] = new Vector3(0f, 0f, -puff);
            vertices[back] = new Vector3(0f, 0f, puff);

            // Unity treats clockwise (as seen by the camera) as the front.
            // The rim runs counter-clockwise seen from -Z, so the -Z side
            // uses (center, next, current) and the +Z side the reverse.
            var triangles = new int[rimCount * 6];
            for (int i = 0; i < rimCount; i++)
            {
                int next = (i + 1) % rimCount;
                int t = i * 6;
                triangles[t] = front;
                triangles[t + 1] = next;
                triangles[t + 2] = i;
                triangles[t + 3] = back;
                triangles[t + 4] = i;
                triangles[t + 5] = next;
            }

            starMesh = new Mesh { name = "Star", vertices = vertices, triangles = triangles };
            starMesh.RecalculateNormals();
            starMesh.RecalculateBounds();
            return starMesh;
        }
    }
}
