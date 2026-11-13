using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Play Mode tests: Unity enters Play Mode, loads the real Main scene and runs real frames.
// A [UnityTest] is a coroutine: each "yield return null" lets Unity run one frame.
namespace Tests.PlayMode
{
    public class MainScenePlayTests
    {
        private GameObject _player;

        // Runs before every test: load the scene the game starts with.
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null; // the scene is loaded at the start of the next frame

            _player = GameObject.Find("Player");
            Assert.IsNotNull(_player, "no GameObject named Player in the Main scene");
        }

        [UnityTest]
        public IEnumerator Player_HasItsComponents_AndScaleOne()
        {
            Assert.IsNotNull(_player.GetComponent<SpriteRenderer>(), "Player has no SpriteRenderer");
            Assert.IsNotNull(_player.GetComponent<PlayerMovement>(), "Player has no PlayerMovement");

            var animator = _player.GetComponent<Animator>();
            Assert.IsNotNull(animator, "Player has no Animator");
            Assert.IsNotNull(animator.runtimeAnimatorController, "the Animator has no controller");

            // Sizes and speeds are set through Pixels Per Unit, not Scale (lesson 14-1).
            Assert.AreEqual(Vector3.one, _player.transform.localScale, "Player Scale must stay 1");

            // Physics: a body that gravity moves, that doesn't tip over, and that is drawn smoothly between steps.
            var body = _player.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "Player has no Rigidbody2D");
            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType, "Rigidbody2D Body Type");
            Assert.IsTrue(body.freezeRotation, "Rigidbody2D: Constraints > Freeze Rotation Z must be on");
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation, "Rigidbody2D Interpolate");
            Assert.IsNotNull(_player.GetComponent<CapsuleCollider2D>(), "Player has no CapsuleCollider2D");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_LandsOnTheGround_AndStaysThere()
        {
            var start = _player.transform.position.y;
            var body = _player.GetComponent<Rigidbody2D>();

            // One second of real physics with no input. Without a ground collider the player
            // would fall about 5 units in that time.
            yield return new WaitForSeconds(1f);

            Assert.AreEqual(start, _player.transform.position.y, 0.1f, "the Player should stand where it starts (on the grass, y = -2)");
            Assert.AreEqual(0f, body.velocity.y, 0.01f, "the Player should not be falling");
        }

        [UnityTest]
        public IEnumerator Player_JumpsToItsJumpHeight_AndLandsAgain()
        {
            var movement = _player.GetComponent<PlayerMovement>();
            var body = _player.GetComponent<Rigidbody2D>();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(movement.IsGrounded, "the Player should start on the ground");
            var ground = body.position.y;

            movement.RequestJump();
            var top = ground;
            var landed = false;
            for (var t = 0f; t < 2f && !landed; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, body.position.y);
                landed = t > 0.2f && movement.IsGrounded;
            }

            // Physics moves the body in steps (50 a second), so it stops a little short of the exact
            // height: about half a step of the take-off speed (13.3 x 0.02 / 2 = 0.13 units).
            Assert.AreEqual(movement.JumpHeight, top - ground, 0.25f, "height of the jump");
            Assert.IsTrue(landed, "the Player should be on the ground again within 2 seconds");
            Assert.AreEqual(ground, body.position.y, 0.05f, "the Player should land where it took off");
        }

        // The camera keeps the Player in view: centred on it horizontally at the start, and still
        // centred after the Player has moved 8 units to the right (given time to catch up).
        [UnityTest]
        public IEnumerator Camera_FollowsThePlayer()
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera, "no camera tagged MainCamera in the Main scene");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(_player.transform.position.x, camera.transform.position.x, 1.5f, "camera x at the start");

            // Move the Player without the keyboard: switch the script off and slide the body.
            _player.GetComponent<PlayerMovement>().enabled = false;
            var body = _player.GetComponent<Rigidbody2D>();
            for (var t = 0f; t < 1f; t += Time.fixedDeltaTime)
            {
                body.velocity = new Vector2(8f, body.velocity.y);
                yield return new WaitForFixedUpdate();
            }

            body.velocity = Vector2.zero;
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(_player.transform.position.x, camera.transform.position.x, 1.5f, "camera x after the Player moved 8 units");
        }

        // The Animator's transitions are made by hand, so check the whole chain of a jump on the spot.
        [UnityTest]
        public IEnumerator Animator_PlaysJumpFallLand_ThenIdleAgain()
        {
            var movement = _player.GetComponent<PlayerMovement>();
            var animator = _player.GetComponent<Animator>();
            var names = new[] { "Hero_Idle", "Hero_Run", "Hero_Jump", "Hero_Fall", "Hero_Land" };
            yield return new WaitForSeconds(0.3f);

            // Every state the Animator goes through, in order.
            var seen = new List<string>();
            void Record()
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                var current = names.FirstOrDefault(info.IsName) ?? "?";
                if (seen.Count == 0 || seen[seen.Count - 1] != current)
                {
                    seen.Add(current);
                }
            }

            Record();
            movement.RequestJump();
            for (var t = 0f; t < 3f && !(seen.Count > 1 && seen[seen.Count - 1] == "Hero_Idle"); t += Time.deltaTime)
            {
                yield return null;
                Record();
            }

            CollectionAssert.AreEqual(
                new[] { "Hero_Idle", "Hero_Jump", "Hero_Fall", "Hero_Land", "Hero_Idle" }, seen,
                "states during a jump: " + string.Join(" > ", seen));
        }

        [UnityTest]
        public IEnumerator Animator_SwitchesBetweenIdleAndRun_WhenSpeedChanges()
        {
            var animator = _player.GetComponent<Animator>();

            // Switch the script off, or every frame it would set Speed back to 0 from the (untouched) keyboard.
            _player.GetComponent<PlayerMovement>().enabled = false;

            yield return null;
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Hero_Idle"), "the game should start in Hero_Idle");

            animator.SetFloat(PlayerMovement.SpeedParameter, 1f);
            yield return null;
            yield return null;
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Hero_Run"), "Speed 1 should switch to Hero_Run");
            Assert.IsFalse(animator.IsInTransition(0), "the switch should be instant (Transition Duration 0)");

            animator.SetFloat(PlayerMovement.SpeedParameter, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Hero_Idle"), "Speed 0 should switch back to Hero_Idle");
        }
    }
}
