using System.Collections;
using UnityEngine;

namespace Player
{
    // Knocked out (walking into an enemy): the hero stops where it was hit, plays its knock-out once
    // (it falls and sits, dazed, stars circling its head), then PlayerRespawn puts it back at the start.
    // Meanwhile it is out of the game: no input moves it, and physics lets go of it, so an enemy
    // walking on can neither push it nor knock it out again. Without physics nothing holds it to the
    // ground either: hit in the air (a jump into the enemy's side), or lifted a little as the two round
    // colliders met, it would sit in the air. So it falls to the ground below it by itself (Settle).
    // Falling into a pit doesn't come here: the hero is already off screen (PlayerHealth.FellInPit).
    [RequireComponent(typeof(PlayerRespawn))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDeath : MonoBehaviour
    {
        // The Animator state that plays the knock-out, named like the controller's other states
        // (after its clip). Without it, the hero is sent back at once, as before.
        [SerializeField] private string deathState = "Hero_Death";

        // What it falls onto: the level's Tilemap is on this layer (enemies and coins are not).
        private const string GroundLayer = "Ground";

        // Looks this far down for the ground (units). Over a pit there is none: it stays where it is.
        private const float GroundSearch = 20f;

        private PlayerRespawn _respawn;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private Animator _animator;         // optional
        private PlayerMovement _movement;   // optional

        public bool IsDying { get; private set; }
        public string DeathState => deathState;

        // Raised as the knock-out starts, for its sound (and effects later). The respawn after it
        // raises PlayerRespawn.Respawned as always.
        public event System.Action Died;

        // Raised when a knock-out that doesn't get up (the last life) has played: the game is over.
        public event System.Action StayedDown;

        private void Awake()
        {
            _respawn = GetComponent<PlayerRespawn>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _animator = GetComponent<Animator>();
            _movement = GetComponent<PlayerMovement>();
        }

        // getUp false: the last life. It stays sitting, out of the game, and raises StayedDown.
        public void Die(bool getUp = true)
        {
            if (IsDying)
            {
                return;   // hit again while knocked out: nothing more happens
            }

            IsDying = true;
            if (_movement != null)
            {
                _movement.enabled = false;   // no input, and it stops driving the Animator
            }

            _body.velocity = Vector2.zero;
            var drop = HeightAboveGround();   // measured while the collider is still in the physics
            _body.simulated = false;
            Died?.Invoke();
            StartCoroutine(Settle(drop));
            StartCoroutine(PlayDeath(getUp));
        }

        // How far its feet (the collider's bottom) are above the ground right below them; 0 when there
        // is no ground below (over a pit) or no collider.
        private float HeightAboveGround()
        {
            if (_collider == null)
            {
                return 0f;
            }

            var feet = new Vector2(_body.position.x, _collider.bounds.min.y);
            var hit = Physics2D.Raycast(feet + Vector2.up * 0.1f, Vector2.down, GroundSearch, LayerMask.GetMask(GroundLayer));
            return hit.collider != null ? Mathf.Max(0f, feet.y - hit.point.y) : 0f;
        }

        // Falls `drop` units, as under its own gravity (speeding up). Two coroutines run side by side:
        // this one moves it, PlayDeath plays the pictures.
        private IEnumerator Settle(float drop)
        {
            var gravity = Physics2D.gravity.y * _body.gravityScale;   // negative: down
            var fallen = 0f;
            var speed = 0f;
            while (fallen < drop && IsDying)   // not past the respawn, which moves it itself
            {
                speed -= gravity * Time.deltaTime;
                var step = Mathf.Min(speed * Time.deltaTime, drop - fallen);
                fallen += step;
                var position = _body.position + Vector2.down * step;
                _body.position = position;
                transform.position = position;
                yield return null;
            }
        }

        private IEnumerator PlayDeath(bool getUp)
        {
            var state = Animator.StringToHash(deathState);
            if (_animator != null && _animator.HasState(0, state))
            {
                // The jump and the fall start from Any State when the parameters say "in the air":
                // hit in the air, they would take over from the knock-out at once. As standing still.
                _animator.SetFloat(PlayerMovement.SpeedParameter, 0f);
                _animator.SetBool(PlayerMovement.GroundedParameter, true);
                _animator.SetFloat(PlayerMovement.VelocityYParameter, 0f);
                _animator.Play(state);
                yield return null;   // the Animator enters the state at its next update
                yield return new WaitForSeconds(_animator.GetCurrentAnimatorStateInfo(0).length);
                if (getUp)
                {
                    _animator.Rebind();   // back to the controller's first state (standing), as at the start
                }
            }

            if (!getUp)
            {
                StayedDown?.Invoke();   // still knocked out (IsDying), sitting where it fell
                yield break;
            }

            _body.simulated = true;
            if (_movement != null)
            {
                _movement.enabled = true;   // before the respawn: it listens to Respawned
            }

            IsDying = false;
            _respawn.Respawn();
        }
    }
}
