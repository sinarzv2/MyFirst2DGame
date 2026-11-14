using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

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

        // Falling out of the level sends the Player back to where it started, standing still.
        [UnityTest]
        public IEnumerator Player_FallingIntoTheKillZone_RespawnsAtTheStart()
        {
            var killZone = Object.FindObjectOfType<KillZone>();
            Assert.IsNotNull(killZone, "no KillZone in the Main scene");
            Assert.IsNotNull(_player.GetComponent<PlayerRespawn>(), "Player has no PlayerRespawn");
            var body = _player.GetComponent<Rigidbody2D>();
            var start = (Vector2)_player.transform.position;
            yield return new WaitForFixedUpdate();

            // Put the Player just above the kill zone and let it fall in: beside the level's left end,
            // where no tile is in the way (under the level the blocks' fill may reach down that far).
            var tilemap = Object.FindObjectOfType<Tilemap>();
            tilemap.CompressBounds();
            var drop = new Vector2(tilemap.localBounds.min.x - 3f, killZone.GetComponent<Collider2D>().bounds.max.y + 1f);
            body.velocity = Vector2.zero;
            body.position = drop;
            _player.transform.position = drop;
            for (var t = 0f; t < 2f && Vector2.Distance(body.position, start) > 0.1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(start.x, body.position.x, 0.1f, "x after falling into the kill zone");
            Assert.AreEqual(start.y, body.position.y, 0.1f, "y after falling into the kill zone");
            Assert.AreEqual(0f, body.velocity.y, 1f, "the Player should come back standing, not still falling fast");
        }

        // The camera keeps the Player in view, at the start and at the far end of the level.
        // Not "centred on the Player": near an end of the level the camera stops at its bounds
        // (CinemachineConfiner2D), so the Player is off-centre there, as it should be. How far off
        // depends on the width of the Game view, so the test asks only that the Player be on screen.
        [UnityTest]
        public IEnumerator Camera_KeepsThePlayerInView()
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera, "no camera tagged MainCamera in the Main scene");
            yield return new WaitForSeconds(0.3f);
            AssertOnScreen(camera, "at the start");

            // Put the Player above the right end of the level (wherever that is: the Tilemap's
            // bounds), let it land and the camera catch up. A camera that didn't follow would be
            // left at the start, with this end off screen.
            var level = Object.FindObjectOfType<Tilemap>().localBounds;
            var body = _player.GetComponent<Rigidbody2D>();
            var target = new Vector2(level.max.x - 3f, level.max.y + 1f);
            body.velocity = Vector2.zero;
            body.position = target;
            _player.transform.position = target;
            yield return new WaitForSeconds(2.5f);
            AssertOnScreen(camera, "at the right end of the level");
        }

        // The Player's middle (1.7 units above its soles) inside the screen, away from the edges.
        private void AssertOnScreen(Camera camera, string when)
        {
            var onScreen = camera.WorldToViewportPoint(_player.transform.position + Vector3.up * 1.7f);
            Assert.That(onScreen.x, Is.InRange(0.05f, 0.95f), $"Player across the screen (0 = left, 1 = right) {when}");
            Assert.That(onScreen.y, Is.InRange(0.05f, 0.95f), $"Player up the screen (0 = bottom, 1 = top) {when}");
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
