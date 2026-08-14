using UnityEngine;

namespace Sandbox.Environment
{
    public class FireFlicker : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 2.5f;
        [SerializeField] private float flickerAmount = 0.6f;
        [SerializeField] private float flickerSpeed = 6f;

        private Light fireLight;
        private float noiseOffset;

        private void Awake()
        {
            fireLight = GetComponent<Light>();
            // Distinct per-instance offset so multiple fires don't flicker in lockstep.
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (fireLight == null)
                return;

            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed);
            fireLight.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
        }
    }
}
