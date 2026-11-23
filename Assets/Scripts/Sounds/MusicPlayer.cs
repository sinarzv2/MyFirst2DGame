using UnityEngine;

namespace Sounds
{
    // The level's music: an intro played once, then a loop played for ever after it. One AudioSource
    // can't do "this once, then that for ever", so there are two, and the loop is started with
    // PlayScheduled at the very sample the intro ends. The time is the sound system's own clock
    // (AudioSettings.dspTime), not frames: a slow frame can't open a gap or an overlap at the join.
    // The two clips are cut from one song (anim/music_loop.js): the intro ends where the loop begins,
    // and the loop's end joins its own start, so the music never stops or clicks.
    // Its AudioSources can later go to a "Music" group of an Audio Mixer, apart from the sound
    // effects' "SFX" group, so a settings menu can turn each on and off.
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip intro;
        [SerializeField] private AudioClip loop;

        // Below the sound effects (they peak around -10 dBFS, the music's median is -19 dBFS):
        // 0.4 is about 8 dB down, so jumps, coins and the "ow" are heard over it.
        [SerializeField, Range(0f, 1f)] private float volume = 0.4f;

        // A moment for the sound system to get ready, so the intro's very start isn't cut.
        private const double StartDelay = 0.1;

        private AudioSource _introSource;
        private AudioSource _loopSource;

        public AudioClip Intro => intro;
        public AudioClip Loop => loop;
        public float Volume => volume;
        public AudioSource IntroSource => _introSource;
        public AudioSource LoopSource => _loopSource;

        // When (on the sound clock) the intro starts and the loop takes over; tests read them.
        public double IntroStart { get; private set; }
        public double LoopStart { get; private set; }

        private void Awake()
        {
            _introSource = MakeSource(intro, false);
            _loopSource = MakeSource(loop, true);
        }

        private void Start()
        {
            if (intro == null || loop == null)
            {
                Debug.LogError("MusicPlayer: set the intro and the loop in the Inspector", this);
                return;
            }

            IntroStart = AudioSettings.dspTime + StartDelay;
            LoopStart = GetLoopStart(IntroStart, intro.samples, intro.frequency);
            _introSource.PlayScheduled(IntroStart);
            _loopSource.PlayScheduled(LoopStart);
        }

        // A music source: heard the same wherever the camera is (2D), not started by itself.
        private AudioSource MakeSource(AudioClip clip, bool looping)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = looping;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        // Rules, without components: Edit Mode tests call them directly.

        // The intro's length counted in samples, not its rounded `length` in seconds: at 48000 samples
        // a second, a rounding of even a millisecond would be 48 samples of silence or overlap.
        public static double GetLoopStart(double introStart, int introSamples, int frequency)
        {
            return introStart + (double)introSamples / frequency;
        }
    }
}
