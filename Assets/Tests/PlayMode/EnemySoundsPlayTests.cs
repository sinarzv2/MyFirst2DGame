using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The enemy's sounds in the real Main scene: defeating it plays the hit and its voice; its footsteps
// are heard only when the camera is near it.
namespace Tests.PlayMode
{
    public class EnemySoundsPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _playerBody;
        private Enemy _enemy;
        private Rigidbody2D _enemyBody;
        private EnemySounds _sounds;
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
            _playerBody = _player.GetComponent<Rigidbody2D>();
            _enemy = Object.FindObjectOfType<Enemy>();
            Assert.IsNotNull(_enemy, "no enemy in the Main scene");
            _enemyBody = _enemy.GetComponent<Rigidbody2D>();
            _sounds = _enemy.GetComponent<EnemySounds>();
            Assert.IsNotNull(_sounds, "the enemy has no EnemySounds");
            _enemy.GetComponent<EnemyPatrol>().enabled = false;   // standing still (its walk still plays)
            _enemyBody.velocity = Vector2.zero;
            _played.Clear();
            _sounds.Played += _played.Add;
        }

        [TearDown]
        public void RestoreVolume()
        {
            AudioListener.volume = _volume;
        }

        private void TeleportPlayer(Vector2 position)
        {
            _playerBody.velocity = Vector2.zero;
            _playerBody.position = position;
            _player.transform.position = position;
        }

        [UnityTest]
        public IEnumerator DefeatingTheEnemy_PlaysTheHitAndItsVoice()
        {
            yield return new WaitForSeconds(0.3f);
            var enemyTop = _enemy.GetComponent<Collider2D>().bounds.max.y;
            var solesBelowPivot = _playerBody.position.y - _player.GetComponent<Collider2D>().bounds.min.y;
            TeleportPlayer(new Vector2(_enemyBody.position.x, enemyTop + 1f + solesBelowPivot));
            for (var t = 0f; t < 1.5f && !_enemy.IsDefeated; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_enemy.IsDefeated, "the stomp should defeat the enemy");
            Assert.That(_played, Does.Contain(_sounds.DefeatHit), "the hit");
            Assert.That(_played, Does.Contain(_sounds.DefeatVoice), "the enemy's voice");
        }

        [UnityTest]
        public IEnumerator Footsteps_AreHeardOnlyNearTheCamera()
        {
            // At the start the camera is on the hero, at the other end of the level from the enemy.
            Assert.That(Vector2.Distance(Camera.main.transform.position, _enemy.transform.position), Is.GreaterThan(_sounds.HearFar));
            yield return new WaitForSeconds(1.5f);   // two steps of its walk
            Assert.That(_played, Is.Empty, "a footstep far off screen should not be played");

            // The hero next to it (3 units to its left), and the camera with the hero.
            TeleportPlayer(new Vector2(_enemyBody.position.x - 3f, _enemyBody.position.y + 0.1f));
            yield return new WaitForSeconds(2.5f);
            Assert.That(Vector2.Distance(Camera.main.transform.position, _enemy.transform.position), Is.LessThan(_sounds.HearFar),
                "the camera should have followed the hero to the enemy");
            _played.Clear();
            yield return new WaitForSeconds(1.5f);
            Assert.That(_played, Is.Not.Empty, "its footsteps should be heard near the camera");
            Assert.That(_played, Is.All.Matches<AudioClip>(c => _sounds.Footsteps.Contains(c)), "only footsteps");
        }
    }
}
