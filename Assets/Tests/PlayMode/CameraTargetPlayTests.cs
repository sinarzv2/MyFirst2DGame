using System.Collections;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

// The camera's follow point in the real Main scene: a full jump from the highest ground must not
// lift the camera (the ground went off the bottom of the screen before CameraTarget), and a
// respawn must bring the point back with the Player.
namespace Tests.PlayMode
{
    public class CameraTargetPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _body;
        private PlayerMovement _movement;
        private CameraTarget _target;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            _player = GameObject.Find("Player");
            Assert.IsNotNull(_player, "no GameObject named Player in the Main scene");
            _body = _player.GetComponent<Rigidbody2D>();
            _movement = _player.GetComponent<PlayerMovement>();
            _target = Object.FindObjectOfType<CameraTarget>();
            Assert.IsNotNull(_target, "no CameraTarget in the Main scene");
        }

        [UnityTest]
        public IEnumerator Camera_StaysPut_WhileJumpingFromTheHighestGround()
        {
            // Stand on the highest ground, wherever it is (read from the Tilemap), and let the
            // camera settle there.
            var tilemap = Object.FindObjectOfType<Tilemap>();
            var ground = HighestGround(tilemap);
            MoveTo(ground + Vector2.up * 0.5f);
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(_movement.IsGrounded, "the Player should stand on the highest ground");

            var camera = Camera.main;
            var before = camera.transform.position.y;
            var highest = before;
            var groundLowestOnScreen = 1f;
            _movement.RequestJump();
            var landed = false;
            for (var t = 0f; t < 2f && !landed; t += Time.deltaTime)
            {
                yield return null;
                highest = Mathf.Max(highest, camera.transform.position.y);
                groundLowestOnScreen = Mathf.Min(groundLowestOnScreen, camera.WorldToViewportPoint(ground).y);
                landed = t > 0.2f && _movement.IsGrounded;
            }

            Assert.IsTrue(landed, "the Player should land again");
            Assert.AreEqual(before, highest, 0.15f, "the camera should not rise with a jump");
            Assert.That(groundLowestOnScreen, Is.GreaterThan(0f), "the ground should stay on screen (0 = bottom edge)");
        }

        [UnityTest]
        public IEnumerator Target_ComesBackWithTheRespawn()
        {
            var start = (Vector2)_player.transform.position;
            yield return new WaitForSeconds(0.3f);

            // Drop the Player into the kill zone, beside the level's left end (as in MainScenePlayTests).
            var killZone = Object.FindObjectOfType<KillZone>();
            var tilemap = Object.FindObjectOfType<Tilemap>();
            tilemap.CompressBounds();
            MoveTo(new Vector2(tilemap.localBounds.min.x - 3f, killZone.GetComponent<Collider2D>().bounds.max.y + 1f));
            for (var t = 0f; t < 2f && Vector2.Distance(_body.position, start) > 0.1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            // Checked straight after the respawn's physics step, before any frame's Update: only the
            // Respawned event can have moved the point back by then.
            Assert.AreEqual(start.y, _body.position.y, 0.1f, "the Player should be back at the start");
            Assert.AreEqual(start.y, _target.transform.position.y, 0.1f, "the camera's point should be back at the start's height");
            Assert.AreEqual(start.x, _target.transform.position.x, 0.1f, "the camera's point should be back at the start");
        }

        // The top of the highest ground, in the middle of its first run of columns: away from the
        // rounded grass at the block's ends.
        private static Vector2 HighestGround(Tilemap tilemap)
        {
            tilemap.CompressBounds();
            var cells = tilemap.cellBounds;
            var highest = float.MinValue;
            int first = 0, last = 0;
            for (var x = cells.xMin; x < cells.xMax; x++)
            {
                var top = float.MinValue;
                for (var y = cells.yMax - 1; y >= cells.yMin; y--)
                {
                    if (tilemap.HasTile(new Vector3Int(x, y, 0)))
                    {
                        top = tilemap.CellToWorld(new Vector3Int(x, y + 1, 0)).y;
                        break;
                    }
                }

                if (top > highest + 0.01f)
                {
                    highest = top;
                    first = last = x;
                }
                else if (Mathf.Abs(top - highest) <= 0.01f && last == x - 1)
                {
                    last = x;   // the same run goes on
                }
            }

            var left = tilemap.CellToWorld(new Vector3Int(first, 0, 0)).x;
            var right = tilemap.CellToWorld(new Vector3Int(last + 1, 0, 0)).x;
            return new Vector2((left + right) / 2f, highest);
        }

        private void MoveTo(Vector2 to)
        {
            _body.velocity = Vector2.zero;
            _body.position = to;
            _player.transform.position = to;
        }
    }
}
