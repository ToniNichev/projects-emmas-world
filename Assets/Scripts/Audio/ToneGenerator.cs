using UnityEngine;

namespace Sandbox.Audio
{
    public static class ToneGenerator
    {
        private const int SampleRate = 44100;
        private const float AttackSeconds = 0.01f;

        public static AudioClip CreateTone(string name, float frequency, float duration, float volume)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * Envelope(t, duration);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Envelope(float t, float duration)
        {
            if (t < AttackSeconds)
                return t / AttackSeconds;

            const float decayStart = 0.3f;
            float decayStartTime = duration * decayStart;
            if (t > decayStartTime)
                return Mathf.Clamp01(1f - (t - decayStartTime) / (duration - decayStartTime));

            return 1f;
        }
    }
}
