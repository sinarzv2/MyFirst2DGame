using System.Collections;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The hero's knock-out in the real Main scene: knocked out in the air (a jump into an enemy's side),
// it comes down to the ground and sits there, not in the air.
namespace Tests.PlayMode
{
    public class PlayerDeathPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _body;
        private PlayerDeath _death;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;

            _player = GameObject.Find("Player");
            _body = _player.GetComponent<Rigidbody2D>();
            _death = _player.GetComponent<PlayerDeath>();
            Assert.IsNotNull(_death, "the Player has no PlayerDeath");
        }

        [UnityTest]
        public IEnumerator KnockedOutInTheAir_FallsToTheGround()
        {
            yield return new WaitForSeconds(0.5f);   // standing at the start
            var standing = _body.position;

            var up = standing + Vector2.up * 2f;
            _body.velocity = Vector2.zero;
            _body.position = up;
            _player.transform.position = up;
            _death.Die();

            // 2 units at the hero's gravity take under 0.4 s; the knock-out lasts 2.1 s.
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(_death.IsDying, "still knocked out");
            Assert.AreEqual(standing.y, _body.position.y, 0.03f, "it should have come down to the ground");
            Assert.AreEqual(standing.x, _body.position.x, 0.0001f, "straight down");
        }
    }
}
