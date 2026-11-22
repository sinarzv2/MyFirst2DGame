using Sounds;
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

        // Sent back to the start (after a knock-out, or after the pause in a pit): a puff of smoke, as
        // the hero comes back.
        [SerializeField] private AudioClip respawn;

        // Knocked out by an enemy: Ludo's sound for the knock-out clip, timed to its frames (the thud
        // as the hero lands, then the stars). The poof follows at the respawn.
        [SerializeField] private AudioClip death;

        // Hurt by an enemy, a heart less but still standing (thrown back, blinking).
        [SerializeField] private AudioClip hurt;

        // Fallen into a pit: a scream falling away, as long as PlayerHealth's pause before the
        // respawn (1 s); the poof follows it.
        [SerializeField] private AudioClip fall;

        // A coin picked up. Coins taken one within coinChainWindow seconds of the last (a row of
        // them, 0.5 s apart at running speed) climb a semitone each (SoundRules.GetChainPitch).
        [SerializeField] private AudioClip coin;
        [SerializeField, Min(0f)] private float coinChainWindow = 0.8f;

        // Each footstep plays a little higher or lower, at random (plus or minus this share of the
        // normal pitch): two recordings then sound like many different steps.
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.05f;

        private AudioSource _source;
        private PlayerMovement _movement;
        private PlayerRespawn _respawn;   // optional, as in PlayerMovement
        private PlayerDeath _death;       // optional
        private PlayerHealth _health;     // optional
        private CoinWallet _wallet;       // optional
        private int _nextFootstep;
        private int _coinsHeard;
        private int _coinChain;
        private float _lastCoinTime = float.NegativeInfinity;

        // Raised with every sound played: tests listen to it (and a debug overlay could).
        public event System.Action<AudioClip> Played;

        public AudioClip[] Footsteps => footsteps;
        public AudioClip Jump => jump;
        public AudioClip Land => land;
        public AudioClip Respawn => respawn;
        public AudioClip Death => death;
        public AudioClip Hurt => hurt;
        public AudioClip Fall => fall;
        public AudioClip Coin => coin;

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
            _respawn = GetComponent<PlayerRespawn>();
            _death = GetComponent<PlayerDeath>();
            _health = GetComponent<PlayerHealth>();
            _wallet = GetComponent<CoinWallet>();
            if (_wallet != null)
            {
                _coinsHeard = _wallet.Coins;
            }
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.Jumped += PlayJump;
                _movement.Landed += PlayLand;
            }

            if (_respawn != null)
            {
                _respawn.Respawned += PlayRespawn;
            }

            if (_death != null)
            {
                _death.Died += PlayDeath;
            }

            if (_health != null)
            {
                _health.Hurt += PlayHurt;
                _health.FellIn += PlayFall;
            }

            if (_wallet != null)
            {
                _wallet.CoinsChanged += OnCoinsChanged;
            }
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.Jumped -= PlayJump;
                _movement.Landed -= PlayLand;
            }

            if (_respawn != null)
            {
                _respawn.Respawned -= PlayRespawn;
            }

            if (_death != null)
            {
                _death.Died -= PlayDeath;
            }

            if (_health != null)
            {
                _health.Hurt -= PlayHurt;
                _health.FellIn -= PlayFall;
            }

            if (_wallet != null)
            {
                _wallet.CoinsChanged -= OnCoinsChanged;
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
            _nextFootstep = SoundRules.NextIndex(_nextFootstep, footsteps.Length);
            Play(clip, SoundRules.GetPitch(pitchVariation, Random.value));
        }

        private void PlayJump()
        {
            Play(jump, 1f);
        }

        private void PlayLand()
        {
            Play(land, 1f);
        }

        private void PlayRespawn()
        {
            Play(respawn, 1f);
        }

        private void PlayDeath()
        {
            Play(death, 1f);
        }

        private void PlayHurt()
        {
            Play(hurt, 1f);
        }

        private void PlayFall()
        {
            Play(fall, 1f);
        }

        // Only when the count goes up: spending coins (a shop, later) is not picking one up.
        private void OnCoinsChanged(int total)
        {
            var gained = total > _coinsHeard;
            _coinsHeard = total;
            if (!gained)
            {
                return;
            }

            _coinChain = SoundRules.NextChain(_coinChain, Time.time - _lastCoinTime, coinChainWindow);
            _lastCoinTime = Time.time;
            Play(coin, SoundRules.GetChainPitch(_coinChain));
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
    }
}
