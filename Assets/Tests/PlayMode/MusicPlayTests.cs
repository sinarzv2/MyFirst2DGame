using System.Collections;
using NUnit.Framework;
using Sounds;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The music in the real Main scene: the intro starts with the level, the loop is set to start the
// sample it ends, and the two sources are music sources (2D, below the sound effects, only the loop
// repeating).
namespace Tests.PlayMode
{
    public class MusicPlayTests
    {
        private MusicPlayer _music;
        private float _volume;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;   // checked, not heard
            SceneManager.LoadScene("Main");
            yield return null;
            _music = Object.FindObjectOfType<MusicPlayer>();
            Assert.IsNotNull(_music, "no MusicPlayer in the Main scene: run Tools > Level > Build Music and save the scene");
        }

        [TearDown]
        public void RestoreVolume()
        {
            AudioListener.volume = _volume;
        }

        [Test]
        public void Music_HasItsClips_OnTwoMusicSources()
        {
            Assert.IsNotNull(_music.Intro, "no intro");
            Assert.IsNotNull(_music.Loop, "no loop");
            Assert.AreSame(_music.Intro, _music.IntroSource.clip);
            Assert.AreSame(_music.Loop, _music.LoopSource.clip);
            Assert.IsFalse(_music.IntroSource.loop, "the intro plays once");
            Assert.IsTrue(_music.LoopSource.loop, "the loop repeats");
            foreach (var source in new[] { _music.IntroSource, _music.LoopSource })
            {
                Assert.AreEqual(0f, source.spatialBlend, "2D: the music sounds the same wherever the camera is");
                Assert.IsFalse(source.playOnAwake);
                Assert.AreEqual(_music.Volume, source.volume);
                Assert.That(source.volume, Is.LessThan(1f), "below the sound effects");
            }
        }

        [UnityTest]
        public IEnumerator Intro_PlaysFirst_AndTheLoopIsSetForTheSampleItEnds()
        {
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(_music.IntroSource.isPlaying, "the intro should be playing");
            Assert.That(_music.IntroSource.timeSamples, Is.GreaterThan(0), "the intro has started");
            Assert.AreEqual(0, _music.LoopSource.timeSamples, "the loop hasn't started yet");
            var intro = _music.Intro;
            Assert.AreEqual((double)intro.samples / intro.frequency, _music.LoopStart - _music.IntroStart, 1e-9,
                "the loop starts the very sample the intro ends");
        }
    }
}
