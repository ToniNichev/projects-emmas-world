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

            placeClip = ToneGenerator.CreateTone("Place", 880f, 0.08f, 0.4f);
            removeClip = ToneGenerator.CreateTone("Remove", 440f, 0.08f, 0.4f);
            footstepClip = ToneGenerator.CreateTone("Footstep", 150f, 0.06f, 0.2f);
            jumpClip = ToneGenerator.CreateTone("Jump", 660f, 0.12f, 0.35f);
        }

        public void PlayPlace() => source.PlayOneShot(placeClip);
        public void PlayRemove() => source.PlayOneShot(removeClip);
        public void PlayFootstep() => source.PlayOneShot(footstepClip);
        public void PlayJump() => source.PlayOneShot(jumpClip);
    }
}
