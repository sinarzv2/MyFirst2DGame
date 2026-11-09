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
            yield return null;
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
