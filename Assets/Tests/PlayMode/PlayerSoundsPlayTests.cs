using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The hero's sounds in the real Main scene: a jump and its landing play their sounds, running plays
// a footstep at each step (from the Run clip's Animation Events), the two footsteps take turns.
namespace Tests.PlayMode
{
    public class PlayerSoundsPlayTests
    {
        private GameObject _player;
        private PlayerMovement _movement;
        private PlayerSounds _sounds;
        private readonly List<AudioClip> _played = new List<AudioClip>();
        private float _volume;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;   // the sounds are checked, not heard
            SceneManager.LoadScene("Main");
            yield return null;

            _player = GameObject.Find("Player");
            _movement = _player.GetComponent<PlayerMovement>();
            _sounds = _player.GetComponent<PlayerSounds>();
            Assert.IsNotNull(_sounds, "the Player has no PlayerSounds");
            _played.Clear();
            _sounds.Played += _played.Add;
        }

        [TearDown]
        public void RestoreVolume()
        {
            AudioListener.volume = _volume;
        }

        [Test]
        public void Player_HasItsSounds()
        {
            Assert.That(_sounds.Footsteps, Has.Length.AtLeast(2), "at least two footsteps, to take turns");
            Assert.That(_sounds.Footsteps, Has.None.Null, "an empty footstep slot");
            Assert.IsNotNull(_sounds.Jump, "no jump sound");
            Assert.IsNotNull(_sounds.Land, "no landing sound");
            var source = _player.GetComponent<AudioSource>();
            Assert.IsFalse(source.playOnAwake, "the AudioSource must not play anything by itself at the start");
            Assert.AreEqual(0f, source.spatialBlend, "2D: the hero sounds the same wherever it is on screen");
        }

        [UnityTest]
        public IEnumerator Jumping_PlaysTheJumpSound_AndLandingTheLandSound()
        {
            for (var t = 0f; t < 1f && !_movement.IsStanding; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_movement.IsStanding, "the hero should stand at the start");
            _played.Clear();
            _movement.RequestJump();
            for (var t = 0f; t < 0.2f && !_played.Contains(_sounds.Jump); t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_played.Contains(_sounds.Jump), "the jump should play its sound at take-off");
            for (var t = 0f; t < 2f && !_played.Contains(_sounds.Land); t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_played.Contains(_sounds.Land), "landing should play its sound");
            Assert.AreEqual(new[] { _sounds.Jump, _sounds.Land }, _played.ToArray(), "one jump, one landing, nothing else");
        }

        [UnityTest]
        public IEnumerator Running_PlaysAFootstepAtEachStep()
        {
            // Run on the spot: drive the Animator as PlayerMovement would, without moving the body.
            _movement.enabled = false;
            var animator = _player.GetComponent<Animator>();
            animator.SetBool(PlayerMovement.GroundedParameter, true);
            animator.SetFloat(PlayerMovement.SpeedParameter, 1f);

            // The Run cycle (36 frames at 16.94 a second, played 1.49 times faster) lasts 1.43 s and
            // has four steps: in 1.5 s, four footsteps (a fifth only if the timing is off).
            yield return new WaitForSeconds(1.5f);

            Assert.That(_played.Count, Is.InRange(3, 5), "about one footstep every 0.36 s");
            Assert.That(_played, Is.All.Matches<AudioClip>(c => _sounds.Footsteps.Contains(c)), "only footsteps");
            for (var i = 1; i < _played.Count; i++)
            {
                Assert.AreNotEqual(_played[i - 1], _played[i], "the two footsteps should take turns");
            }
        }
    }
}
