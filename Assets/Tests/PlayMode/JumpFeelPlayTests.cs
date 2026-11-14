using System.Collections;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Coyote time, jump buffer and the short jump, with real physics in the Main scene.
// Leaving the ground is done by lifting the Player straight up from where it stands: for
// PlayerMovement that is the same as running off a ledge (touching the ground, then not), with
// no keyboard needed and no dependence on where the level's ledges are.
namespace Tests.PlayMode
{
    public class JumpFeelPlayTests
    {
        private PlayerMovement _movement;
        private Rigidbody2D _body;
        private float _ground;

        [UnitySetUp]
        public IEnumerator LoadMainScene_AndStand()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "no GameObject named Player in the Main scene");
            _movement = player.GetComponent<PlayerMovement>();
            _body = player.GetComponent<Rigidbody2D>();

            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(_movement.IsGrounded, "the Player should start on the ground");
            _ground = _body.position.y;
        }

        [UnityTest]
        public IEnumerator CoyoteTime_AJumpJustAfterLeavingTheGround_StillWorks()
        {
            Lift(4f);
            yield return Wait(_movement.CoyoteTime * 0.5f);
            _movement.RequestJump();
            yield return Wait(0.04f);
            Assert.That(_body.velocity.y, Is.GreaterThan(_movement.JumpSpeed * 0.5f), "should have jumped");
        }

        [UnityTest]
        public IEnumerator CoyoteTime_IsOver_ALittleLater()
        {
            Lift(4f);
            yield return Wait(_movement.CoyoteTime + 0.1f);
            _movement.RequestJump();
            yield return Wait(0.04f);
            Assert.That(_body.velocity.y, Is.LessThan(0f), "should still be falling: no jump in mid-air");
        }

        [UnityTest]
        public IEnumerator JumpBuffer_APressJustBeforeLanding_JumpsOnLanding()
        {
            // From 1.5 units up the Player falls at about 8.6 units/s near the end: 0.25 units above
            // the ground is about 0.03 s before landing, well inside the buffer.
            Lift(1.5f);
            for (var t = 0f; t < 1f && _body.position.y - _ground > 0.25f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(_movement.IsGrounded, Is.False, "the press must come before landing");
            _movement.RequestJump();
            var fastestUp = 0f;
            for (var t = 0f; t < 0.4f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                fastestUp = Mathf.Max(fastestUp, _body.velocity.y);
            }

            Assert.That(fastestUp, Is.GreaterThan(_movement.JumpSpeed * 0.5f), "should have jumped on landing");
        }

        [UnityTest]
        public IEnumerator JumpBuffer_APressLongBeforeLanding_IsForgotten()
        {
            // From 3 units up the fall takes about 0.45 s. The press comes after the coyote time is
            // over (a press right after leaving the ground is meant to jump), and still about 0.3 s
            // before landing, far more than the buffer.
            Lift(3f);
            yield return Wait(_movement.CoyoteTime + 0.06f);
            _movement.RequestJump();
            var fastestUp = 0f;
            for (var t = 0f; t < 1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                fastestUp = Mathf.Max(fastestUp, _body.velocity.y);
            }

            Assert.That(fastestUp, Is.LessThan(1f), "landed and stayed: the early press should not jump");
            Assert.IsTrue(_movement.IsGrounded, "should be standing again");
        }

        [UnityTest]
        public IEnumerator LettingGoAtOnce_GivesTheShortJump()
        {
            _movement.RequestJump();
            _movement.ReleaseJump();
            var top = _ground;
            for (var t = 0f; t < 1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, _body.position.y);
            }

            // The rise is cut in the same step it starts, so the jump is exactly the shortest one:
            // jumpHeight * cut² (0.75 units), less a little for the physics steps.
            var shortest = _movement.JumpHeight * _movement.JumpCutFactor * _movement.JumpCutFactor;
            Assert.AreEqual(shortest, top - _ground, 0.15f, "height of a tapped jump");
            Assert.IsTrue(_movement.IsGrounded, "should be standing again");
        }

        // Straight up from where the Player stands, standing still.
        private void Lift(float height)
        {
            var to = _body.position + Vector2.up * height;
            _body.velocity = Vector2.zero;
            _body.position = to;
            _body.transform.position = to;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
