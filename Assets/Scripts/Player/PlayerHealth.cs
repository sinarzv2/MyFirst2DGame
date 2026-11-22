using System.Collections;
using Enemies;
using UnityEngine;

namespace Player
{
    // The hero's hearts and lives in the game (the rules are in Health):
    //   an enemy     a heart less: thrown back, then blinking for a moment, when no enemy can hurt it
    //                (it passes through them, as in most platformers, so it isn't hit again at once)
    //   last heart   a life less: knocked out (PlayerDeath), then back at the start with full hearts
    //   a pit        a whole life at once: a scream as it falls away out of view, a short pause
    //                (the scream is heard out), then back at the start
    //   last life    game over: knocked out for good (or lost in the pit), then LevelRestart starts
    //                the level again
    // Hearts and lives change here only; the HUD and the sounds listen to the events.
    [RequireComponent(typeof(PlayerRespawn))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int heartsPerLife = 3;
        [SerializeField, Min(1)] private int lives = 3;

        // After a hit: thrown back this fast (units/s) and this high (units), with no control for
        // knockbackTime s; then blinking and unhurtable for invulnerableTime s in all.
        [SerializeField, Min(0f)] private float knockbackSpeed = 4f;
        [SerializeField, Min(0f)] private float knockbackHeight = 1f;
        [SerializeField, Min(0f)] private float knockbackTime = 0.3f;
        [SerializeField, Min(0f)] private float invulnerableTime = 1.5f;
        [SerializeField, Min(0.02f)] private float blinkInterval = 0.1f;

        // Fallen into a pit: held out of view this long (s) before the respawn, as long as the scream
        // (PlayerSounds), so the poof comes after it instead of on top of it.
        [SerializeField, Min(0f)] private float pitPause = 1f;

        private Health _health;
        private PlayerRespawn _respawn;
        private PlayerDeath _death;          // optional: without it a lost life respawns at once
        private PlayerMovement _movement;    // optional
        private Rigidbody2D _body;           // optional
        private Collider2D _collider;        // optional
        private SpriteRenderer _sprite;      // optional
        private Coroutine _invulnerable;

        // Made when first asked for, not in Awake: a HUD view may ask in its OnEnable, which can run
        // before this object's Awake.
        private Health Model => _health ??= new Health(heartsPerLife, lives);

        public int Hearts => Model.Hearts;
        public int Lives => Model.Lives;
        public int HeartsPerLife => Model.HeartsPerLife;
        public bool IsGameOver { get; private set; }
        public bool IsInvulnerable => _invulnerable != null;
        public bool IsInPit { get; private set; }   // fallen in, waiting out the pause
        public float PitPause => pitPause;

        public event System.Action<int> HeartsChanged;
        public event System.Action<int> LivesChanged;
        public event System.Action Hurt;       // a heart lost, still standing (its sound)
        public event System.Action FellIn;     // fallen into a pit (its sound)
        public event System.Action GameOver;

        private void Awake()
        {
            _respawn = GetComponent<PlayerRespawn>();
            _death = GetComponent<PlayerDeath>();
            _movement = GetComponent<PlayerMovement>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _sprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _respawn.Respawned += OnRespawned;
            if (_death != null)
            {
                _death.StayedDown += EndGame;
            }
        }

        private void OnDisable()
        {
            _respawn.Respawned -= OnRespawned;
            if (_death != null)
            {
                _death.StayedDown -= EndGame;
            }
        }

        // Hit by an enemy at `from` (its position: the hero is thrown away from it).
        public void Hit(Vector2 from)
        {
            if (IsGameOver || IsInvulnerable || IsInPit || (_death != null && _death.IsDying) || Hearts == 0)
            {
                return;
            }

            var result = Model.TakeHit();
            HeartsChanged?.Invoke(Hearts);
            if (result == HealthResult.Hurt)
            {
                Hurt?.Invoke();
                ThrowBack(from);
                _invulnerable = StartCoroutine(BeInvulnerable());
                return;
            }

            LivesChanged?.Invoke(Lives);
            KnockOut(getUp: result == HealthResult.LifeLost);
        }

        // Fallen into a pit (KillZone). The kill zone lies below what the camera can show, so the hero
        // is already out of view: it is held there (no input, out of the physics, or it would fall
        // forever) while the scream plays, then sent back to the start, or left there if the game is over.
        public void FellInPit()
        {
            if (IsGameOver || IsInPit)
            {
                return;
            }

            var result = Model.LoseLife();
            HeartsChanged?.Invoke(Hearts);
            LivesChanged?.Invoke(Lives);
            StopInvulnerable();
            Hold();
            FellIn?.Invoke();
            if (result == HealthResult.GameOver)
            {
                EndGame();
                return;
            }

            StartCoroutine(BackFromThePit());
        }

        private IEnumerator BackFromThePit()
        {
            IsInPit = true;
            yield return new WaitForSeconds(pitPause);
            IsInPit = false;
            if (_movement != null)
            {
                _movement.enabled = true;   // before the respawn: it listens to Respawned
            }

            _respawn.Respawn();
            if (_body != null)
            {
                _body.simulated = true;     // after it: back in the physics at the start, not in the kill zone
            }
        }

        // Held where it is: no input, and physics lets go of it.
        private void Hold()
        {
            if (_movement != null)
            {
                _movement.enabled = false;
            }

            if (_body != null)
            {
                _body.velocity = Vector2.zero;
                _body.simulated = false;
            }
        }

        private void KnockOut(bool getUp)
        {
            StopInvulnerable();
            if (_death != null)
            {
                _death.Die(getUp);
            }
            else if (getUp)
            {
                _respawn.Respawn();
            }
            else
            {
                EndGame();
            }
        }

        private void EndGame()
        {
            IsGameOver = true;
            GameOver?.Invoke();
        }

        // Back at the start with a new life: all its hearts, and no blinking left over.
        private void OnRespawned()
        {
            StopInvulnerable();
            Model.Refill();
            HeartsChanged?.Invoke(Hearts);
        }

        private void ThrowBack(Vector2 from)
        {
            if (_movement == null)
            {
                return;
            }

            _movement.Knockback(GetKnockbackDirection(transform.position.x, from.x, _sprite != null && _sprite.flipX) * knockbackSpeed,
                knockbackHeight, knockbackTime);
        }

        private IEnumerator BeInvulnerable()
        {
            SetEnemiesIgnored(true);
            for (var t = 0f; t < invulnerableTime; t += Time.deltaTime)
            {
                SetVisible(IsShownWhileBlinking(t, blinkInterval));
                yield return null;
            }

            EndInvulnerable();
        }

        // Cut short (knocked out, or back at the start).
        private void StopInvulnerable()
        {
            if (_invulnerable != null)
            {
                StopCoroutine(_invulnerable);
                EndInvulnerable();
            }
        }

        private void EndInvulnerable()
        {
            _invulnerable = null;
            SetEnemiesIgnored(false);
            SetVisible(true);
        }

        // Every enemy's collider and the hero's stop touching (or touch again). An enemy found later
        // (none appear during play yet) would still touch.
        private void SetEnemiesIgnored(bool ignored)
        {
            if (_collider == null)
            {
                return;
            }

            foreach (var enemy in FindObjectsOfType<Enemy>())
            {
                var other = enemy.GetComponent<Collider2D>();
                if (other != null)
                {
                    Physics2D.IgnoreCollision(_collider, other, ignored);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (_sprite != null)
            {
                _sprite.enabled = visible;
            }
        }

        // Rules, without components: Edit Mode tests call them directly.

        // Away from the enemy: +1 = to the right. Exactly level with it, back the way it faces.
        public static float GetKnockbackDirection(float heroX, float enemyX, bool facingLeft)
        {
            if (!Mathf.Approximately(heroX, enemyX))
            {
                return heroX > enemyX ? 1f : -1f;
            }

            return facingLeft ? 1f : -1f;
        }

        // Blinking: shown for one interval, hidden for the next, starting shown.
        public static bool IsShownWhileBlinking(float t, float interval)
        {
            return Mathf.FloorToInt(t / interval) % 2 == 0;
        }
    }
}
