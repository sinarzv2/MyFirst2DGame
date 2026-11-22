using System.Collections;
using Enemies;
using Hud;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

// Hearts and lives in the real Main scene: an enemy costs a heart (thrown back, blinking), the last
// heart a life (knocked out, back with full hearts), a pit a whole life, the last life the game
// (GAME OVER, then the level starts again). The HUD shows it all.
namespace Tests.PlayMode
{
    public class PlayerHealthPlayTests
    {
        private GameObject _player;
        private Rigidbody2D _body;
        private PlayerHealth _health;
        private PlayerDeath _death;
        private SpriteRenderer _sprite;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            FindPlayer();
            Assert.IsNotNull(_health, "the Player has no PlayerHealth: run Tools > Hud > Build Hearts And Lives and save the scene");
        }

        private void FindPlayer()
        {
            _player = GameObject.Find("Player");
            _body = _player.GetComponent<Rigidbody2D>();
            _health = _player.GetComponent<PlayerHealth>();
            _death = _player.GetComponent<PlayerDeath>();
            _sprite = _player.GetComponent<SpriteRenderer>();
        }

        private static T Find<T>() where T : Object
        {
            var found = Object.FindObjectOfType<T>();
            Assert.IsNotNull(found, $"no {typeof(T).Name} in the Main scene: run Tools > Hud > Build Hearts And Lives");
            return found;
        }

        private void Teleport(Vector2 position)
        {
            _body.velocity = Vector2.zero;
            _body.position = position;
            _player.transform.position = position;
        }

        // Waits out the blinking after a hit (1.5 s), so the next hit counts.
        private IEnumerator UntilHurtable()
        {
            for (var t = 0f; t < 3f && _health.IsInvulnerable; t += Time.deltaTime)
            {
                yield return null;
            }

            Assert.IsFalse(_health.IsInvulnerable, "the blinking should be over within 3 s");
        }

        // Dropped into the kill zone beside the level's left end; waits until it is back at the start
        // (or the game is over).
        private IEnumerator FallIntoThePit()
        {
            var start = (Vector2)_player.GetComponent<PlayerRespawn>().SpawnPoint;
            DropAboveThePit();
            yield return new WaitForFixedUpdate();
            for (var t = 0f; t < 2f && !_health.IsGameOver && Vector2.Distance(_body.position, start) > 0.1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        // Just above the kill zone, beside the level's left end (no tile in the way).
        private void DropAboveThePit()
        {
            var tilemap = Object.FindObjectOfType<Tilemap>();
            tilemap.CompressBounds();
            var zone = Object.FindObjectOfType<KillZone>().GetComponent<Collider2D>().bounds;
            Teleport(new Vector2(tilemap.localBounds.min.x - 3f, zone.max.y + 1f));
        }

        [UnityTest]
        public IEnumerator AtTheStart_ThreeLivesOfThreeHearts_OnTheHud()
        {
            yield return null;
            Assert.AreEqual(3, _health.Lives);
            Assert.AreEqual(3, _health.Hearts);
            Assert.AreEqual(3, Find<HeartsView>().ShownFull, "three full hearts");
            Assert.AreEqual(CoinCounterView.Format(3), Find<LivesView>().Shown);
            Assert.IsFalse(Find<GameOverView>().IsShown, "no GAME OVER yet");
            Find<LevelRestart>();
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheEnemy_CostsAHeart_ThrowsBack_AndBlinks()
        {
            var enemy = Object.FindObjectOfType<Enemy>();
            enemy.GetComponent<EnemyPatrol>().enabled = false;
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            enemyBody.velocity = Vector2.zero;
            var movement = _player.GetComponent<PlayerMovement>();
            movement.enabled = false;   // we push it: the script would read the (untouched) keyboard
            var enemyX = enemyBody.position.x;
            Teleport(new Vector2(enemyX - 3f, enemyBody.position.y + 0.1f));
            yield return new WaitForSeconds(0.4f);

            for (var t = 0f; t < 2f && _health.Hearts == 3; t += Time.fixedDeltaTime)
            {
                _body.velocity = new Vector2(movement.MoveSpeed, _body.velocity.y);
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(2, _health.Hearts, "walking into the enemy should cost a heart");
            Assert.AreEqual(3, _health.Lives);
            Assert.IsFalse(_death.IsDying, "a heart lost is not a knock-out");
            Assert.IsTrue(_health.IsInvulnerable, "blinking after the hit");
            movement.enabled = true;   // the knockback is PlayerMovement's: it must run

            var hitX = _body.position.x;
            var hidden = false;
            for (var t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                hidden |= !_sprite.enabled;
                yield return null;
            }

            Assert.That(_body.position.x, Is.LessThan(hitX - 0.5f), "thrown back, away from the enemy on its right");
            Assert.IsTrue(hidden, "the hero should blink");
            Assert.AreEqual(2, Find<HeartsView>().ShownFull, "the HUD shows two full hearts");

            yield return UntilHurtable();
            Assert.IsTrue(_sprite.enabled, "seen again after the blinking");
        }

        // While blinking the hero passes through enemies: one hit can't cost two hearts.
        [UnityTest]
        public IEnumerator WhileBlinking_AnEnemyCostsNothing()
        {
            var enemy = Object.FindObjectOfType<Enemy>();
            enemy.GetComponent<EnemyPatrol>().enabled = false;
            yield return null;
            _health.Hit(enemy.transform.position);
            Assert.AreEqual(2, _health.Hearts);

            _player.GetComponent<PlayerMovement>().enabled = false;
            Teleport(enemy.GetComponent<Rigidbody2D>().position + Vector2.up * 0.1f);   // right inside it
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(2, _health.Hearts, "no heart lost while blinking");
        }

        [UnityTest]
        public IEnumerator TheLastHeart_KnocksOut_ThenANewLifeWithFullHearts()
        {
            var from = _body.position + Vector2.right;   // an enemy just to the right
            for (var k = 0; k < 3; k++)
            {
                yield return UntilHurtable();
                _health.Hit(from);
            }

            Assert.AreEqual(0, _health.Hearts);
            Assert.AreEqual(2, _health.Lives, "the last heart costs a life");
            Assert.IsTrue(_death.IsDying, "knocked out");
            yield return null;
            yield return null;
            Assert.IsTrue(_player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName(_death.DeathState));

            for (var t = 0f; t < 3f && _death.IsDying; t += Time.deltaTime)
            {
                yield return null;
            }

            Assert.IsFalse(_death.IsDying, "up again within 3 s");
            Assert.AreEqual(3, _health.Hearts, "a new life: all its hearts");
            Assert.AreEqual(CoinCounterView.Format(2), Find<LivesView>().Shown);
            Assert.IsFalse(_health.IsGameOver);
        }

        [UnityTest]
        public IEnumerator APit_CostsAWholeLife_AndRefillsTheHearts()
        {
            yield return null;
            _health.Hit(_body.position + Vector2.right);
            Assert.AreEqual(2, _health.Hearts);

            yield return FallIntoThePit();
            Assert.AreEqual(2, _health.Lives, "the pit costs a life");
            Assert.AreEqual(3, _health.Hearts, "back at the start with all the hearts");
            Assert.AreEqual(3, Find<HeartsView>().ShownFull);
            Assert.AreEqual(CoinCounterView.Format(2), Find<LivesView>().Shown);
        }

        // In the pit: held where it fell in, below what the camera can show, for the pause (its scream
        // plays meanwhile: PlayerSoundsPlayTests), then back at the start, playing again.
        [UnityTest]
        public IEnumerator APit_HoldsTheHeroOutOfView_ForThePause_ThenSendsItBack()
        {
            yield return null;
            var start = (Vector2)_player.GetComponent<PlayerRespawn>().SpawnPoint;
            DropAboveThePit();
            for (var t = 0f; t < 2f && !_health.IsInPit; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_health.IsInPit, "falling into the kill zone should hold the hero in the pit");
            var fellAt = Time.time;
            var heldAt = _body.position;
            var view = GameObject.Find("CameraBounds").GetComponent<Collider2D>().bounds;
            Assert.That(_sprite.bounds.max.y, Is.LessThan(view.min.y), "held out of view: below all the camera can show");

            var movement = _player.GetComponent<PlayerMovement>();
            while (_health.IsInPit && Time.time - fellAt < 3f)
            {
                Assert.IsFalse(_body.simulated || movement.enabled, "held: out of the physics, no input");
                Assert.AreEqual(heldAt, _body.position, "held where it fell in");
                yield return null;
            }

            Assert.AreEqual(_health.PitPause, Time.time - fellAt, 0.15f, "held for the pause");
            Assert.AreEqual(start.x, _body.position.x, 0.1f, "then back at the start");
            Assert.AreEqual(start.y, _body.position.y, 0.1f, "then back at the start");
            Assert.IsTrue(_body.simulated && movement.enabled, "back at the start, the hero plays again");
        }

        [UnityTest]
        public IEnumerator TheLastLife_IsGameOver_ThenTheLevelStartsAgain()
        {
            yield return null;
            _player.GetComponent<CoinWallet>().Add(5);
            var oldPlayer = _player;
            for (var k = 0; k < 3; k++)
            {
                yield return FallIntoThePit();
            }

            Assert.IsTrue(_health.IsGameOver, "three pits: no lives left");
            Assert.AreEqual(0, _health.Lives);
            Assert.IsTrue(Find<GameOverView>().IsShown, "GAME OVER on screen");
            Assert.IsFalse(_body.simulated, "the hero no longer falls");

            var delay = Find<LevelRestart>().Delay;
            for (var t = 0f; t < delay + 2f && oldPlayer != null; t += Time.deltaTime)
            {
                yield return null;   // a destroyed Unity object compares equal to null
            }

            Assert.IsTrue(oldPlayer == null, "the level should have been loaded again");
            yield return null;
            FindPlayer();
            Assert.AreEqual(3, _health.Lives, "a new game: all the lives");
            Assert.AreEqual(3, _health.Hearts);
            Assert.AreEqual(0, _player.GetComponent<CoinWallet>().Coins, "and no coins");
            Assert.IsFalse(Find<GameOverView>().IsShown);
        }
    }
}
