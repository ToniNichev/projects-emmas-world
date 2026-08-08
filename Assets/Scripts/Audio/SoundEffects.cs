using UnityEngine;

namespace Sandbox.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEffects : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip placeClip;
        private AudioClip removeClip;
        private AudioClip footstepClip;
        private AudioClip jumpClip;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            // Softer than the original pass -- repeated build clicks and
            // footsteps at 0.4/0.2 got fatiguing quickly during real testing.
            placeClip = ToneGenerator.CreateTone("Place", 880f, 0.08f, 0.28f);
            removeClip = ToneGenerator.CreateTone("Remove", 440f, 0.08f, 0.28f);
            footstepClip = ToneGenerator.CreateTone("Footstep", 150f, 0.06f, 0.14f);
            jumpClip = ToneGenerator.CreateTone("Jump", 660f, 0.12f, 0.3f);
        }

        // Small per-play pitch variance so rapid repeats (placing several
        // blocks in a row, walking) don't sound like the exact same beep on
        // a loop -- jump is a one-off per press, so it stays fixed.
        public void PlayPlace() => PlayWithVariance(placeClip, 0.05f);
        public void PlayRemove() => PlayWithVariance(removeClip, 0.05f);
        public void PlayFootstep() => PlayWithVariance(footstepClip, 0.08f);
        public void PlayJump() => source.PlayOneShot(jumpClip);

        private void PlayWithVariance(AudioClip clip, float pitchVariance)
        {
            source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            source.PlayOneShot(clip);
        }
    }
}
