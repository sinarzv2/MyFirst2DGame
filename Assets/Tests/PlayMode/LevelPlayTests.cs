using System.Collections;
using System.Collections.Generic;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

// The level in the Main scene: what keeps the Player in it (the kill zone, the walls at both ends),
// what keeps the camera in it (CameraBounds), and whether every gap and step can really be jumped
// with the hero's speed and jump. The level's size and shape are read from its Tilemap, so the tests
// keep working when the level is redrawn, and check every gap and step it then has.
namespace Tests.PlayMode
{
    public class LevelPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _body;
        private PlayerMovement _movement;
        private Tilemap _tilemap;
        private Bounds _level;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;

            _player = GameObject.Find("Player");
            Assert.IsNotNull(_player, "no GameObject named Player in the Main scene");
            _body = _player.GetComponent<Rigidbody2D>();
            _movement = _player.GetComponent<PlayerMovement>();
            _tilemap = Object.FindObjectOfType<Tilemap>();
            Assert.IsNotNull(_tilemap, "no Tilemap in the Main scene");
            _tilemap.CompressBounds();   // the bounds of the tiles there are (erased cells left out)
            _level = _tilemap.localBounds;
        }

        // Puts the Player at `position` at once (the physics body and the drawn transform).
        private void Teleport(Vector2 position)
        {
            _body.velocity = Vector2.zero;
            _body.position = position;
            _player.transform.position = position;
        }

        [UnityTest]
        public IEnumerator Level_GridCellsAreTheTilesSize()
        {
            // A tile is 92 px at 46 Pixels Per Unit = 2 units; with a smaller cell the tiles overlap.
            Assert.AreEqual(new Vector3(2f, 2f, 0f), Object.FindObjectOfType<Grid>().cellSize, "Grid Cell Size");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Level_KillZoneLiesUnderTheWholeLevel()
        {
            var zone = Object.FindObjectOfType<KillZone>();
            Assert.IsNotNull(zone, "no KillZone in the Main scene");
            var box = zone.GetComponent<BoxCollider2D>();
            Assert.IsTrue(box.isTrigger, "the KillZone's collider must be a trigger, or it is an invisible floor");
            Assert.That(box.bounds.min.x, Is.LessThanOrEqualTo(_level.min.x), "the KillZone must reach under the level's left end");
            Assert.That(box.bounds.max.x, Is.GreaterThanOrEqualTo(_level.max.x), "the KillZone must reach under the level's right end");
            Assert.That(box.bounds.max.y, Is.LessThan(_level.min.y), "the KillZone's top must be under the lowest tile");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Level_CameraBoundsFitTheLevel()
        {
            var bounds = GameObject.Find("CameraBounds");
            Assert.IsNotNull(bounds, "no GameObject named CameraBounds in the Main scene");
            var shape = bounds.GetComponent<PolygonCollider2D>();
            Assert.IsNotNull(shape, "CameraBounds has no PolygonCollider2D");
            Assert.IsTrue(shape.isTrigger, "CameraBounds' collider must be a trigger, or it pushes the Player out of the level");
            Assert.AreEqual(_level.min.x, shape.bounds.min.x, 0.5f, "CameraBounds' left side = the level's left end");
            Assert.AreEqual(_level.max.x, shape.bounds.max.x, 0.5f, "CameraBounds' right side = the level's right end");
            Assert.That(shape.bounds.size.y, Is.GreaterThanOrEqualTo(2f * Camera.main.orthographicSize), "CameraBounds must be at least a screen tall");
            yield return null;
        }

        // Paint a block 10 cells past the right end (a copy of the right end column, top tile down)
        // and refit: the right wall, the camera bounds and the kill zone must all follow it.
        [UnityTest]
        public IEnumerator LevelBounds_FollowsTheTilesWhenTheLevelGrows()
        {
            var fitter = Object.FindObjectOfType<LevelBounds>();
            Assert.IsNotNull(fitter, "no LevelBounds in the Main scene");
            var cells = _tilemap.cellBounds;
            var newColumn = cells.xMax + 9;
            for (var y = cells.yMin; y < cells.yMax; y++)
            {
                _tilemap.SetTile(new Vector3Int(newColumn, y, 0), _tilemap.GetTile(new Vector3Int(cells.xMax - 1, y, 0)));
            }

            fitter.Fit();
            yield return new WaitForFixedUpdate();

            var newEnd = _tilemap.CellToWorld(new Vector3Int(newColumn + 1, 0, 0)).x;
            var wall = GameObject.Find("WallRight").GetComponent<BoxCollider2D>().bounds;
            var camera = GameObject.Find("CameraBounds").GetComponent<PolygonCollider2D>().bounds;
            var zone = Object.FindObjectOfType<KillZone>().GetComponent<BoxCollider2D>().bounds;
            Assert.AreEqual(newEnd, wall.min.x, 0.01f, "the right wall should stand at the new right end");
            Assert.AreEqual(newEnd, camera.max.x, 0.01f, "the camera bounds should reach the new right end");
            Assert.That(zone.max.x, Is.GreaterThanOrEqualTo(newEnd), "the kill zone should reach under the new right end");
        }

        [UnityTest]
        public IEnumerator Player_CannotWalkOffTheLeftEnd()
        {
            yield return PushTowardsTheEnd(-1f);
        }

        [UnityTest]
        public IEnumerator Player_CannotWalkOffTheRightEnd()
        {
            yield return PushTowardsTheEnd(1f);
        }

        // Runs the Player into one end of the level for 2 s: a wall must stop it on the end block.
        // (Without a wall it falls off, and the kill zone puts it back at the start: so the test
        // watches how low it got, not only where it ends up.)
        private IEnumerator PushTowardsTheEnd(float direction)
        {
            if (direction > 0f)
            {
                Teleport(new Vector2(_level.max.x - 3f, _level.max.y + 1f));   // above the right end block
            }

            yield return new WaitForSeconds(1f);
            Assert.IsTrue(_movement.IsGrounded, "the Player should stand on the end block before the push");
            var ground = _body.position.y;

            _movement.enabled = false;   // we push it: the script would set the speed from the (untouched) keyboard
            var lowest = ground;
            for (var t = 0f; t < 2f; t += Time.fixedDeltaTime)
            {
                _body.velocity = new Vector2(direction * _movement.MoveSpeed, _body.velocity.y);
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, _body.position.y);
            }

            Assert.That(lowest, Is.GreaterThan(ground - 0.5f), "the Player fell off the end of the level");
            Assert.AreEqual(direction > 0f ? _level.max.x : _level.min.x, _body.position.x, 1.5f, "the Player should be stopped at the end of the level");
        }

        [UnityTest]
        public IEnumerator Camera_NeverShowsBeyondTheCameraBounds()
        {
            var camera = Camera.main;
            var bounds = GameObject.Find("CameraBounds").GetComponent<PolygonCollider2D>().bounds;

            yield return new WaitForSeconds(0.3f);
            AssertViewInside(camera, bounds, "at the start (the Player near the left end)");

            Teleport(new Vector2(_level.max.x - 1f, _level.max.y + 1f));
            yield return new WaitForSeconds(2.5f);
            AssertViewInside(camera, bounds, "at the right end");

            // Hold the Player down in a pit (in an empty column, just above the kill zone): the camera
            // must not follow it into the void. Checked once the camera has settled: the Confiner2D's
            // Damping lets the view cross the bounds for a moment while it catches up, by design.
            var pit = FirstEmptyColumn();
            if (pit != null)
            {
                var zone = Object.FindObjectOfType<KillZone>().GetComponent<Collider2D>().bounds;
                _movement.enabled = false;
                _body.gravityScale = 0f;
                Teleport(new Vector2(ColumnLeftX(pit.Value) + _tilemap.cellSize.x / 2f, zone.max.y + 1f));
                yield return new WaitForSeconds(2.5f);
                AssertViewInside(camera, bounds, "with the Player down in a pit");
            }
        }

        // An empty column between the level's ends (a gap), or null if the level has none.
        private int? FirstEmptyColumn()
        {
            foreach (var column in TopRows())
            {
                if (column.Value == null)
                {
                    return column.Key;
                }
            }

            return null;
        }

        // What the camera shows (an orthographic camera: Size = half the height) lies inside `bounds`.
        private static void AssertViewInside(Camera camera, Bounds bounds, string when)
        {
            var centre = camera.transform.position;
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            const float slack = 0.05f;
            Assert.That(centre.x - halfWidth, Is.GreaterThanOrEqualTo(bounds.min.x - slack), $"left edge of the view {when}");
            Assert.That(centre.x + halfWidth, Is.LessThanOrEqualTo(bounds.max.x + slack), $"right edge of the view {when}");
            Assert.That(centre.y - halfHeight, Is.GreaterThanOrEqualTo(bounds.min.y - slack), $"bottom edge of the view {when}");
            Assert.That(centre.y + halfHeight, Is.LessThanOrEqualTo(bounds.max.y + slack), $"top edge of the view {when}");
        }

        [UnityTest]
        public IEnumerator Respawn_CutsTheCameraBackToTheStart()
        {
            var camera = Camera.main;
            yield return new WaitForSeconds(0.3f);
            var startX = camera.transform.position.x;

            Teleport(new Vector2(_level.max.x - 1f, _level.max.y + 1f));
            yield return new WaitForSeconds(2.5f);
            Assert.That(camera.transform.position.x - startX, Is.GreaterThan(3f), "the camera should have followed the Player to the right end");

            _player.GetComponent<PlayerRespawn>().Respawn();
            yield return null;
            yield return null;
            Assert.AreEqual(startX, camera.transform.position.x, 0.5f, "camera x two frames after Respawn: it should cut back, not glide across the level");
        }

        // ---------- every gap and every step up in the level, jumped for real ----------

        // Top tile row of each column of the Tilemap, or null where the column is empty.
        private Dictionary<int, int?> TopRows()
        {
            var cells = _tilemap.cellBounds;
            var tops = new Dictionary<int, int?>();
            for (var x = cells.xMin; x < cells.xMax; x++)
            {
                tops[x] = null;
                for (var y = cells.yMax - 1; y >= cells.yMin && tops[x] == null; y--)
                {
                    if (_tilemap.HasTile(new Vector3Int(x, y, 0)))
                    {
                        tops[x] = y;
                    }
                }
            }

            return tops;
        }

        private float SurfaceY(int column, int topRow) => _tilemap.CellToWorld(new Vector3Int(column, topRow + 1, 0)).y;

        private float ColumnLeftX(int column) => _tilemap.CellToWorld(new Vector3Int(column, 0, 0)).x;

        [UnityTest]
        public IEnumerator EveryGapInTheLevel_CanBeJumped()
        {
            var tops = TopRows();
            var cells = _tilemap.cellBounds;
            var jumped = 0;
            for (var x = cells.xMin + 1; x < cells.xMax - 1; x++)
            {
                if (tops[x] != null || tops[x - 1] == null)
                {
                    continue;   // not the first empty column of a gap
                }

                var last = x;
                while (last + 1 < cells.xMax && tops[last + 1] == null)
                {
                    last++;
                }

                if (last + 1 >= cells.xMax)
                {
                    continue;   // no block after it: not a gap but the end
                }

                // From the block left of the gap to the block right of it.
                var from = new Vector2(ColumnLeftX(x), SurfaceY(x - 1, tops[x - 1].Value));
                var to = new Vector2(ColumnLeftX(last + 1), SurfaceY(last + 1, tops[last + 1].Value));
                yield return RunAndJump(from, to, $"the gap of {last - x + 1} cell(s) at x = {from.x}");
                jumped++;
            }

            if (jumped == 0)
            {
                Debug.Log("EveryGapInTheLevel_CanBeJumped: the level has no gaps to try");
            }
        }

        [UnityTest]
        public IEnumerator EveryStepUpInTheLevel_CanBeClimbed()
        {
            var tops = TopRows();
            var cells = _tilemap.cellBounds;
            var climbed = 0;
            for (var x = cells.xMin; x < cells.xMax - 1; x++)
            {
                if (tops[x] == null || tops[x + 1] == null || tops[x + 1] <= tops[x])
                {
                    continue;
                }

                var from = new Vector2(ColumnLeftX(x + 1), SurfaceY(x, tops[x].Value));
                var to = new Vector2(ColumnLeftX(x + 1), SurfaceY(x + 1, tops[x + 1].Value));
                yield return RunAndJump(from, to, $"the step of {tops[x + 1] - tops[x]} cell(s) up at x = {from.x}");
                climbed++;
            }

            if (climbed == 0)
            {
                Debug.Log("EveryStepUpInTheLevel_CanBeClimbed: the level has no steps up to try");
            }
        }

        // Stands the Player 0.8 units before the edge at `from` (x = the edge, y = its surface), then
        // runs right at the hero's speed and jumps with the hero's jump, as the keys would: it must
        // land on the far side, on the surface at `to`, without falling.
        private IEnumerator RunAndJump(Vector2 from, Vector2 to, string what)
        {
            _movement.enabled = false;   // we steer: the script would read the (untouched) keyboard
            Teleport(new Vector2(from.x - 0.8f, from.y + 0.1f));
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_movement.IsGrounded, $"the Player should stand before {what}");

            _body.velocity = new Vector2(_movement.MoveSpeed, _movement.JumpSpeed);
            var landed = false;
            for (var t = 0f; t < 2.5f && !landed; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                _body.velocity = new Vector2(_movement.MoveSpeed, _body.velocity.y);
                if (_body.position.y < Mathf.Min(from.y, to.y) - 1.5f)
                {
                    break;   // fell in
                }

                landed = t > 0.1f && _body.position.x > to.x + 0.3f && _movement.IsGrounded;
            }

            Assert.IsTrue(landed, $"the Player could not get across {what} (it ended at {_body.position})");
            Assert.AreEqual(to.y, _body.position.y, 0.2f, $"the Player should land on the far side of {what}");
        }
    }
}
