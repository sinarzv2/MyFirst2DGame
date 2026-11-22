using System.Collections;
using System.Linq;
using Enemies;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

// The enemy in the real Main scene, with real physics: it walks its ground and turns at the wall and
// at the drop, landing on it defeats it and bounces the Player, walking into it on the last heart
// knocks the Player out and then sends it back to the start, and it doesn't take coins.
namespace Tests.PlayMode
{
    public class EnemyPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _playerBody;
        private Enemy _enemy;
        private EnemyPatrol _patrol;
        private Rigidbody2D _enemyBody;
        private Tilemap _tilemap;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;

            _player = GameObject.Find("Player");
            _playerBody = _player.GetComponent<Rigidbody2D>();
            _tilemap = Object.FindObjectOfType<Tilemap>();
            Assert.AreEqual(LayerMask.NameToLayer("Ground"), _tilemap.gameObject.layer,
                "the Tilemap should be on the Ground layer: that is what the enemy walks on");
            _enemy = Object.FindObjectOfType<Enemy>();
            Assert.IsNotNull(_enemy, "no enemy in the Main scene: Tools > Level > Build From Layout places it");
            _patrol = _enemy.GetComponent<EnemyPatrol>();
            _enemyBody = _enemy.GetComponent<Rigidbody2D>();
        }

        private static void Teleport(Rigidbody2D body, Vector2 position)
        {
            body.velocity = Vector2.zero;
            body.position = position;
            body.transform.position = position;
        }

        // The enemy's ground: its surface, the wall at its left end (a higher block) and the drop at
        // its right end (an empty column), read from the Tilemap around the enemy's cell.
        private (float surface, float wallX, float dropX) EnemyGround()
        {
            var cell = _tilemap.WorldToCell(_enemy.transform.position + Vector3.up * 0.5f);
            var row = cell.y - 1;
            bool Floor(int x) => _tilemap.HasTile(new Vector3Int(x, row, 0)) && !_tilemap.HasTile(new Vector3Int(x, row + 1, 0));
            Assert.IsTrue(Floor(cell.x), "the enemy should stand on ground");
            var left = cell.x;
            while (Floor(left - 1))
            {
                left--;
            }

            var right = cell.x;
            while (Floor(right + 1))
            {
                right++;
            }

            Assert.IsTrue(_tilemap.HasTile(new Vector3Int(left - 1, row + 1, 0)), "a wall (a higher block) should end the enemy's ground on the left");
            Assert.IsFalse(_tilemap.HasTile(new Vector3Int(right + 1, row, 0)), "a drop should end the enemy's ground on the right");
            var surface = _tilemap.CellToWorld(new Vector3Int(cell.x, row + 1, 0)).y;
            return (surface, _tilemap.CellToWorld(new Vector3Int(left, row, 0)).x, _tilemap.CellToWorld(new Vector3Int(right + 1, row, 0)).x);
        }

        [UnityTest]
        public IEnumerator Enemy_TurnsAtTheWall_AndAtTheDrop_WithoutFalling()
        {
            var (surface, wallX, dropX) = EnemyGround();
            var lowest = float.PositiveInfinity;

            // Near the wall, walking left (as it starts): it must turn right.
            Assert.IsTrue(_patrol.FacingLeft, "the enemy starts walking left");
            Teleport(_enemyBody, new Vector2(wallX + 1.5f, surface + 0.05f));
            for (var t = 0f; t < 2f && _patrol.FacingLeft; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, _enemyBody.position.y);
            }

            Assert.IsFalse(_patrol.FacingLeft, "the enemy should turn at the wall");
            Assert.That(_enemyBody.position.x, Is.GreaterThan(wallX), "the enemy stays this side of the wall");

            // Near the drop, walking right: it must turn left before it falls.
            Teleport(_enemyBody, new Vector2(dropX - 1.5f, surface + 0.05f));
            for (var t = 0f; t < 2f && !_patrol.FacingLeft; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, _enemyBody.position.y);
            }

            Assert.IsTrue(_patrol.FacingLeft, "the enemy should turn at the drop");
            yield return new WaitForSeconds(0.5f);
            lowest = Mathf.Min(lowest, _enemyBody.position.y);
            Assert.That(lowest, Is.GreaterThan(surface - 0.3f), "the enemy should never fall off its ground");
            Assert.That(_enemyBody.position.x, Is.LessThan(dropX), "the enemy should walk back from the drop");
        }

        [UnityTest]
        public IEnumerator LandingOnTheEnemy_DefeatsIt_AndBouncesThePlayer()
        {
            yield return LandOnTheEnemy(0f);
        }

        // Both colliders are capsules: off the middle, the Player's round bottom first touches the
        // enemy's round shoulder, well below its top. That is still landing on it.
        [UnityTest]
        public IEnumerator LandingOnTheEnemysShoulder_AlsoDefeatsIt()
        {
            yield return LandOnTheEnemy(0.9f);
        }

        // Drops the Player from 1 unit above the enemy's top, offsetX units to the side of its middle.
        private IEnumerator LandOnTheEnemy(float offsetX)
        {
            _patrol.enabled = false;   // standing still, so the Player falls straight onto it
            _enemyBody.velocity = Vector2.zero;
            yield return new WaitForSeconds(0.3f);

            var enemyTop = _enemy.GetComponent<Collider2D>().bounds.max.y;
            var solesBelowPivot = _playerBody.position.y - _player.GetComponent<Collider2D>().bounds.min.y;
            Teleport(_playerBody, new Vector2(_enemyBody.position.x + offsetX, enemyTop + 1f + solesBelowPivot));

            for (var t = 0f; t < 1.5f && !_enemy.IsDefeated; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            // Defeated: out of the game at once (nothing touches it, it does not move), but still on
            // screen, playing its defeat.
            Assert.IsTrue(_enemy.IsDefeated, "landing on it should defeat it");
            var at = _enemy.transform.position;
            yield return null;   // two frames: the Animator has surely entered the new state
            yield return null;
            Assert.IsTrue(_enemy.gameObject.activeSelf, "the enemy should stay to play its defeat");
            Assert.IsFalse(_enemy.GetComponent<Collider2D>().enabled, "a defeated enemy should touch nothing");
            Assert.IsTrue(_enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(_enemy.DefeatState),
                $"the enemy should play its {_enemy.DefeatState} animation");

            var from = _playerBody.position.y;
            var top = from;
            for (var t = 0f; t < 0.8f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, _playerBody.position.y);
            }

            Assert.AreEqual(_enemy.StompBounceHeight, top - from, 0.25f, "the Player should bounce up by the enemy's bounce height");
            Assert.That(Vector2.Distance(_playerBody.position, _player.GetComponent<PlayerRespawn>().SpawnPoint), Is.GreaterThan(5f),
                "the Player should not have been sent back to the start");

            // The animation (1.1 s), lying still and the fade (0.4 s each): gone within 3 s, where it fell.
            for (var t = 0f; t < 3f && _enemy.gameObject.activeSelf; t += Time.deltaTime)
            {
                Assert.AreEqual(at, _enemy.transform.position, "a defeated enemy should stay where it was");
                yield return null;
            }

            Assert.IsFalse(_enemy.gameObject.activeSelf, "after its defeat the enemy should be gone");
        }

        // On its last heart (each hit before costs one: PlayerHealthPlayTests): knocked out where it
        // was hit (it stays there, out of the game, playing its knock-out), then sent back to the start.
        [UnityTest]
        public IEnumerator WalkingIntoTheEnemy_OnTheLastHeart_KnocksThePlayerOut_ThenSendsItBack_AndTheEnemyStays()
        {
            var (surface, _, _) = EnemyGround();
            var death = _player.GetComponent<PlayerDeath>();
            var health = _player.GetComponent<PlayerHealth>();
            Assert.IsNotNull(death, "the Player has no PlayerDeath");
            Assert.IsNotNull(health, "the Player has no PlayerHealth");
            while (health.Hearts > 1)
            {
                for (var t = 0f; t < 3f && health.IsInvulnerable; t += Time.deltaTime)
                {
                    yield return null;
                }

                health.Hit(_playerBody.position + Vector2.right);
            }

            for (var t = 0f; t < 3f && health.IsInvulnerable; t += Time.deltaTime)
            {
                yield return null;   // the blinking over: the enemy can hurt again
            }

            _patrol.enabled = false;
            _enemyBody.velocity = Vector2.zero;
            var movement = _player.GetComponent<PlayerMovement>();
            movement.enabled = false;   // we push it: the script would read the (untouched) keyboard
            var enemyX = _enemyBody.position.x;
            Teleport(_playerBody, new Vector2(enemyX - 3f, surface + 0.1f));
            yield return new WaitForSeconds(0.4f);

            for (var t = 0f; t < 2f && !death.IsDying; t += Time.fixedDeltaTime)
            {
                _playerBody.velocity = new Vector2(movement.MoveSpeed, _playerBody.velocity.y);
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(death.IsDying, "walking into the enemy on the last heart should knock the Player out");
            var at = _playerBody.position;
            yield return null;   // two frames: the Animator has surely entered the new state
            yield return null;
            Assert.IsFalse(_playerBody.simulated, "a knocked-out Player is out of the physics");
            Assert.IsFalse(movement.enabled, "a knocked-out Player takes no input");
            Assert.IsTrue(_player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(death.DeathState),
                $"the Player should play its {death.DeathState} animation");

            // The knock-out lasts 2.1 s: sent back within 3 s, from where it was hit. It may only come
            // down onto the ground (it was walking: a little at most).
            var sentBack = false;
            for (var t = 0f; t < 3f && !sentBack; t += Time.deltaTime)
            {
                sentBack = _playerBody.position.x < enemyX - 10f;
                if (!sentBack)
                {
                    Assert.AreEqual(at.x, _playerBody.position.x, 0.0001f, "a knocked-out Player stays where it was hit");
                    Assert.That(_playerBody.position.y, Is.InRange(surface - 0.05f, at.y + 0.0001f),
                        "a knocked-out Player only comes down onto the ground");
                }

                yield return null;
            }

            Assert.IsTrue(sentBack, $"the Player should be sent back to the start (it ended at {_playerBody.position})");
            Assert.IsFalse(death.IsDying, "back at the start, the knock-out is over");
            Assert.IsTrue(_playerBody.simulated && movement.enabled, "back at the start, the Player plays again");
            Assert.IsTrue(_enemy.gameObject.activeSelf && !_enemy.IsDefeated, "walking into it must not defeat it");
        }

        [UnityTest]
        public IEnumerator TheEnemy_DoesNotTakeCoins()
        {
            var coin = Object.FindObjectsOfType<Coin>().FirstOrDefault();
            Assert.IsNotNull(coin, "no coins in the Main scene");
            coin.transform.position = _enemy.GetComponent<Collider2D>().bounds.center;
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(coin.gameObject.activeSelf, "the coin should still be there");
            Assert.AreEqual(0, _player.GetComponent<CoinWallet>().Coins, "nobody's wallet got it");
        }
    }
}
