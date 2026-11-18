using Sounds;
using UnityEngine;

namespace Enemies
{
    // An enemy's sounds. Footsteps come from Animation Events on its walk clip (Footstep, on the frames
    // a foot lands); the defeat sounds from Enemy's Defeated event.
    // Footsteps are background: quieter than the hero's, and heard only near the middle of the screen.
    // The volume and the left/right balance are worked out here, from the distance to the camera
    // across the screen: Unity's own 3D sound would also count the camera's 10 units of depth, so an
    // enemy in the middle of the screen would already sound far away.
    // The defeat sounds play on short-lived objects of their own (PlayClipAtPoint): the enemy is
    // hidden at once, and a hidden object's AudioSource stops.
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Enemy))]
    public class EnemySounds : MonoBehaviour
    {
        [SerializeField] private AudioClip[] footsteps;

        // Under the hero's footsteps (1): the enemy's walking is background.
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f;
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.05f;

        // Units from the camera's middle: full volume up to hearNear, silent from hearFar on. The
        // screen is about 18 units wide, so a footstep fades out a few units past its edge.
        [SerializeField, Min(0f)] private float hearNear = 4f;
        [SerializeField, Min(0f)] private float hearFar = 12f;

        // The hit (the same for every enemy) and this enemy's own voice, played together.
        [SerializeField] private AudioClip defeatHit;
        [SerializeField] private AudioClip defeatVoice;

        private AudioSource _source;
        private Enemy _enemy;
        private int _nextFootstep;

        // Raised with every sound played: tests listen to it.
        public event System.Action<AudioClip> Played;

        public AudioClip[] Footsteps => footsteps;
        public AudioClip DefeatHit => defeatHit;
        public AudioClip DefeatVoice => defeatVoice;
        public float HearFar => hearFar;

        private void Reset()
        {
            var source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // 2D: the volume and balance are set by Footstep
            }
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            _enemy.Defeated += PlayDefeat;
        }

        private void OnDisable()
        {
            _enemy.Defeated -= PlayDefeat;
        }

        // Called by the walk clip's Animation Events, which find it by its name.
        public void Footstep()
        {
            var listener = Camera.main;
            if (footsteps == null || footsteps.Length == 0 || listener == null)
            {
                return;
            }

            // Across the screen only (a Vector2 drops the depth).
            Vector2 offset = transform.position - listener.transform.position;
            var volume = SoundRules.GetVolume(offset.magnitude, hearNear, hearFar);
            if (volume <= 0f)
            {
                return;   // too far to hear: not played at all
            }

            var clip = footsteps[_nextFootstep];
            _nextFootstep = SoundRules.NextIndex(_nextFootstep, footsteps.Length);
            _source.pitch = SoundRules.GetPitch(pitchVariation, Random.value);
            _source.panStereo = SoundRules.GetPan(offset.x, hearFar);
            _source.PlayOneShot(clip, footstepVolume * volume);
            Played?.Invoke(clip);
        }

        // At the camera: full volume, in the middle. A defeat is always on screen, near the hero.
        private void PlayDefeat()
        {
            var listener = Camera.main;
            var at = listener != null ? listener.transform.position : transform.position;
            foreach (var clip in new[] { defeatHit, defeatVoice })
            {
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, at);
                    Played?.Invoke(clip);
                }
            }
        }
    }
}
