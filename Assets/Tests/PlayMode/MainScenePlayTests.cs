using System.Collections;
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
