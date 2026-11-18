using UnityEngine;

namespace Player
{
    // The hero's sounds. Footsteps come from Animation Events on the Run clip: it calls Footstep a
    // frame before each foot lands (the grass crush swells for about a frame before its loudest
    // part). The jump and the landing come from PlayerMovement's events, so they follow the physics,
    // not the pictures. Its AudioSource can later be sent to an "SFX" group of an Audio Mixer, so a
    // settings menu can turn sound effects and music on and off apart.
    [RequireComponent(typeof(AudioSource))]
    public class PlayerSounds : MonoBehaviour
    {
        // Played in turn (left foot, right foot).
        [SerializeField] private AudioClip[] footsteps;
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip land;

        // Each footstep plays a little higher or lower, at random (plus or minus this share of the
        // normal pitch): two recordings then sound like many different steps.
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.05f;

        private AudioSource _source;
        private PlayerMovement _movement;
        private int _nextFootstep;

        // Raised with every sound played: tests listen to it (and a debug overlay could).
        public event System.Action<AudioClip> Played;

        public AudioClip[] Footsteps => footsteps;
        public AudioClip Jump => jump;
        public AudioClip Land => land;

        // Unity calls Reset when the component is added in the Editor: a sound effect, heard the same
        // wherever the hero is on screen (2D), and nothing played on its own at the start.
        private void Reset()
        {
            var source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _movement = GetComponent<PlayerMovement>();
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.Jumped += PlayJump;
                _movement.Landed += PlayLand;
            }
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.Jumped -= PlayJump;
                _movement.Landed -= PlayLand;
            }
        }

        // Called by the Run clip's Animation Events, which find it by its name: renaming it breaks
        // them (an Edit Mode test checks the clip's events against this name).
        public void Footstep()
        {
            if (footsteps == null || footsteps.Length == 0)
            {
                return;
            }

            var clip = footsteps[_nextFootstep];
            _nextFootstep = NextIndex(_nextFootstep, footsteps.Length);
            Play(clip, GetPitch(pitchVariation, Random.value));
        }

        private void PlayJump()
        {
            Play(jump, 1f);
        }

        private void PlayLand()
        {
            Play(land, 1f);
        }

        // One AudioSource plays every sound (PlayOneShot lets them overlap). Its pitch is shared by
        // the sounds it is playing, so a footstep's pitch also bends the last moments of the sound
        // before it: they are a few hundredths of a second, too quiet to hear.
        private void Play(AudioClip clip, float pitch)
        {
            if (clip == null)
            {
                return;
            }

            _source.pitch = pitch;
            _source.PlayOneShot(clip);
            Played?.Invoke(clip);
        }

        // Rules, without components: Edit Mode tests call them directly.

        // The footstep after this one, back to the first after the last.
        public static int NextIndex(int index, int count)
        {
            return (index + 1) % count;
        }

        // t is a random number from 0 to 1: the pitch goes from 1 - variation to 1 + variation.
        public static float GetPitch(float variation, float t)
        {
            return 1f + variation * (2f * t - 1f);
        }
    }
}
