using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        // Units per second, matched to the Run animation's feet so they don't slide:
        // the planted shoe moves back 7.28 px a frame (on average over a step)
        // at 16.94 frames/s = 123.3 px/s, / 46 PPU (Scale 1) = 2.68 units/s,
        // with the Run state at Speed 1.
        // Recompute if PPU, Scale, the Run clip's Samples or Run Speed change.
        // The Edit Mode test MoveSpeed_MatchesRunFeet checks this.
        [SerializeField] private float moveSpeed = 2.68f;

        // Name of the Animator parameter this script drives. Hero.controller must
        // have a Float parameter with this name (an Edit Mode test checks it).
        public const string SpeedParameter = "Speed";

        // The Animator finds parameters by a number (hash). Working it out once
        // is cheaper than passing the name every frame.
        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _body;

        // The input read in Update, used by FixedUpdate.
        private float _horizontal;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();
        }

        // Once per drawn frame: read the input and update what is drawn.
        private void Update()
        {
            _horizontal = Input.GetAxisRaw("Horizontal");

            _animator.SetFloat(SpeedHash, GetAnimatorSpeed(_horizontal));

            _spriteRenderer.flipX = FacesLeft(_horizontal, _spriteRenderer.flipX);
        }

        // Once per physics step (50 a second by default): move the body.
        // The physics engine moves it by its velocity and stops it at colliders;
        // changing transform.position directly would teleport it past them.
        private void FixedUpdate()
        {
            _body.velocity = GetVelocity(_horizontal, moveSpeed, _body.velocity);
        }

        // The rules below don't read input or touch components,
        // so Edit Mode tests can call them directly.

        // The body's new velocity: the input sets the horizontal part;
        // the vertical part is left as it is, so gravity (and later jumps) keep working.
        public static Vector2 GetVelocity(float horizontal, float speed, Vector2 current)
        {
            return new Vector2(horizontal * speed, current.y);
        }

        // Value for the Animator's Speed parameter:
        // 0 when standing, 1 when running in either direction.
        public static float GetAnimatorSpeed(float horizontal)
        {
            return Mathf.Abs(horizontal);
        }

        // Face left while moving left and right while moving right;
        // keep the last direction when standing still.
        public static bool FacesLeft(float horizontal, bool facingLeftNow)
        {
            if (horizontal == 0f)
            {
                return facingLeftNow;
            }

            return horizontal < 0f;
        }
    }
}
