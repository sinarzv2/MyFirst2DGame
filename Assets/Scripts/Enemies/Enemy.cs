using System.Collections;
using Player;
using UnityEngine;

namespace Enemies
{
    // What happens when the Player touches an enemy: landing on top of it defeats it and bounces the
    // Player up; any other touch sends the Player back to the start. Defeat is public: weapons made
    // later (a sword, arrows) call it too, so stomping stays just one way of defeating an enemy.
    [RequireComponent(typeof(Collider2D))]
    public class Enemy : MonoBehaviour
    {
        // How high the Player bounces off a defeated enemy (units, soles above where it landed).
        [SerializeField, Min(0f)] private float stompBounceHeight = 1.5f;

        // The defeat animation: the Animator state of this name plays once (every enemy's controller
        // names it the same, so this code fits them all); then the enemy lies still for corpseTime
        // seconds and fades out over fadeTime before it is hidden. An enemy without such a state (one
        // with no defeat animation yet) is hidden at once.
        [SerializeField] private string defeatState = "Death";
        [SerializeField, Min(0f)] private float corpseTime = 0.4f;
        [SerializeField, Min(0f)] private float fadeTime = 0.4f;

        private Collider2D _collider;
        private Rigidbody2D _body;               // optional: an enemy that does not move has none
        private Animator _animator;              // optional
        private SpriteRenderer _spriteRenderer;  // optional

        public bool IsDefeated { get; private set; }
        public float StompBounceHeight => stompBounceHeight;
        public string DefeatState => defeatState;

        // Raised by Defeat, however the enemy was defeated (a stomp now, a sword or an arrow later):
        // sounds, effects and the patrol (which stops) listen to it.
        public event System.Action Defeated;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _body = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDefeated)
            {
                return;
            }

            // The collider that touched may sit on a child; the Rigidbody's object is the Player.
            var body = collision.rigidbody;
            var player = body != null ? body.GetComponent<PlayerMovement>() : null;
            if (player == null)
            {
                return;   // the ground, a wall, another enemy
            }

            var box = _collider.bounds;
            if (IsStomp(collision.collider.bounds.min.y, box.min.y, box.max.y))
            {
                Defeat();
                player.Bounce(stompBounceHeight);
            }
            else
            {
                var respawn = body.GetComponent<PlayerRespawn>();
                if (respawn != null)
                {
                    respawn.Respawn();
                }
            }
        }

        // Out of the game at once, gone from the screen after its defeat animation. Hidden, not
        // destroyed (as a coin): a level restart could bring it back.
        public void Defeat()
        {
            if (IsDefeated)
            {
                return;   // a second hit (a sword and a stomp together) changes nothing
            }

            IsDefeated = true;

            // Nothing touches it any more (the Player passes through the body, it hurts nobody), and
            // with no collider it must not fall through the ground: physics lets go of it.
            _collider.enabled = false;
            if (_body != null)
            {
                _body.simulated = false;
            }

            Defeated?.Invoke();
            StartCoroutine(PlayDefeat());
        }

        // A coroutine: Unity runs it a piece at a time, each `yield` waiting (a frame, or seconds)
        // before going on, so the steps of the defeat follow each other without a timer in Update.
        private IEnumerator PlayDefeat()
        {
            var state = Animator.StringToHash(defeatState);
            if (_animator != null && _animator.HasState(0, state))
            {
                _animator.Play(state);
                yield return null;   // the Animator enters the state at its next update

                yield return new WaitForSeconds(_animator.GetCurrentAnimatorStateInfo(0).length + corpseTime);
                if (_spriteRenderer != null)
                {
                    var color = _spriteRenderer.color;
                    for (var t = 0f; t < fadeTime; t += Time.deltaTime)
                    {
                        color.a = GetFadeAlpha(t, fadeTime);
                        _spriteRenderer.color = color;
                        yield return null;
                    }
                }
            }

            gameObject.SetActive(false);
        }

        // Opacity t seconds into a fade lasting `duration`: from fully seen (1) down to gone (0).
        public static float GetFadeAlpha(float t, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(1f - t / duration) : 0f;
        }

        // Landing on it: the Player's soles are above its middle. Both are capsules, so a Player
        // landing off-centre first touches the enemy's round shoulder, well below its top; a Player
        // walking into its side has its soles at the enemy's feet.
        public static bool IsStomp(float playerBottom, float enemyBottom, float enemyTop)
        {
            return playerBottom >= (enemyBottom + enemyTop) / 2f;
        }
    }
}
