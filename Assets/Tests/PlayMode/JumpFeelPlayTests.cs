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
        private GameObject _platform;

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

        // The two buffer tests time the press from the Inspector's Jump Buffer and Coyote Time, not
        // from numbers written here: with other settings they still test what their names say.
        [UnityTest]
        public IEnumerator JumpBuffer_APressJustBeforeLanding_JumpsOnLanding()
        {
            // Pressed half the buffer before landing: inside the buffer, whatever its length.
            const float height = 1.5f;
            var pressAt = FallTime(height) - _movement.JumpBuffer / 2f;
            Assert.That(pressAt, Is.GreaterThan(_movement.CoyoteTime + 0.04f),
                "the press must come after the coyote time, or that (not the buffer) makes the jump: lift higher");
            Lift(height);
            yield return Wait(pressAt);

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
            // Pressed after the coyote time is over (a press right after leaving the ground is meant
            // to jump), and longer before landing than the buffer lasts.
            const float height = 3f;
            var pressAt = _movement.CoyoteTime + 0.06f;
            Assert.That(FallTime(height) - pressAt, Is.GreaterThan(_movement.JumpBuffer + 0.06f),
                "the press must come longer before landing than the buffer lasts: lift higher");
            Lift(height);
            yield return Wait(pressAt);
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

        // Standing is not "not moving up": on a platform that rises, the hero rises with it and must
        // still count as standing, and be able to jump. (The first IsStanding asked for no upward
        // speed: here the hero could never jump. The same happens walking up a rounded grass corner.)
        [UnityTest]
        public IEnumerator OnAPlatformMovingUp_TheHeroIsStanding_AndCanJump()
        {
            // Far from the level, to the left (RespawnTests builds its own objects far to the right).
            var far = new Vector2(-1000f, 0f);
            _platform = new GameObject("RisingPlatform");
            _platform.transform.position = far;
            _platform.AddComponent<BoxCollider2D>().size = new Vector2(6f, 1f);   // its top 0.5 above `far`
            var platformBody = _platform.AddComponent<Rigidbody2D>();
            platformBody.bodyType = RigidbodyType2D.Kinematic;                    // moved by its velocity only
            platformBody.velocity = Vector2.up;

            MoveTo(far + Vector2.up * 0.6f);
            yield return Wait(0.5f);
            Assert.That(_body.velocity.y, Is.GreaterThan(0.5f), "the hero should be riding the platform up");
            Assert.IsTrue(_movement.IsStanding, "riding a rising platform is standing");

            _movement.RequestJump();
            yield return Wait(0.04f);
            Assert.That(_body.velocity.y, Is.GreaterThan(_movement.JumpSpeed * 0.5f), "should have jumped off the platform");
        }

        // A press made just before falling into the kill zone must not jump at the start by itself.
        [UnityTest]
        public IEnumerator Respawn_ForgetsABufferedJump()
        {
            _movement.RequestJump();
            _body.GetComponent<PlayerRespawn>().Respawn();
            var fastestUp = 0f;
            for (var t = 0f; t < 0.3f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                fastestUp = Mathf.Max(fastestUp, _body.velocity.y);
            }

            Assert.That(fastestUp, Is.LessThan(1f), "the press from before the respawn should be forgotten");
        }

        // The scene stays loaded after the last test: leave nothing of ours in it.
        [TearDown]
        public void DestroyThePlatform()
        {
            if (_platform != null)
            {
                Object.Destroy(_platform);
            }
        }

        // Seconds to fall `height` units from standing still, with the Player's own gravity:
        // height = g t² / 2, so t = √(2 height / g).
        private float FallTime(float height)
        {
            var gravity = -Physics2D.gravity.y * _body.gravityScale;
            return Mathf.Sqrt(2f * height / gravity);
        }

        // Straight up from where the Player stands, standing still.
        private void Lift(float height)
        {
            MoveTo(_body.position + Vector2.up * height);
        }

        private void MoveTo(Vector2 to)
        {
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
