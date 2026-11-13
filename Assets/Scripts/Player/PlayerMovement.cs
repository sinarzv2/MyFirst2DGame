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

        // The jump is designed by its height (units, soles above the ground) and the time to reach it
        // (seconds); gravity and the take-off speed are worked out from those two (see GetJumpGravity).
        // 3 units clears one ground tile (2 units) with room to spare and is a bit less than the hero
        // (3.7 units). The Hero_Jump and Hero_Fall states' Speed fit their clips to timeToApex
        // (the Edit Mode test JumpClips_FitTheJumpTime checks it).
        // Min: both are divided by, so the Inspector must not accept 0 (gravity would be infinite).
        [SerializeField, Min(0.1f)] private float jumpHeight = 3f;
        [SerializeField, Min(0.05f)] private float timeToApex = 0.4f;

        // Names of the Animator parameters this script drives. Hero.controller must
        // have them, with these types (an Edit Mode test checks it).
        public const string SpeedParameter = "Speed";           // Float
        public const string GroundedParameter = "Grounded";     // Bool
        public const string VelocityYParameter = "VelocityY";   // Float

        // The Animator finds parameters by a number (hash). Working it out once
        // is cheaper than passing the name every frame.
        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);
        private static readonly int GroundedHash = Animator.StringToHash(GroundedParameter);
        private static readonly int VelocityYHash = Animator.StringToHash(VelocityYParameter);

        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _body;

        // What counts as standing on the ground: touching something whose contact normal points up
        // (between 45 and 135 degrees, 90 = straight up). A wall the hero runs into is not ground.
        private ContactFilter2D _groundFilter;

        // The input read in Update, used by FixedUpdate.
        private float _horizontal;
        private bool _jumpRequested;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();
            _groundFilter.SetNormalAngle(45f, 135f);

            // Gravity Scale comes from the jump's design, not from the Inspector:
            // the gravity it needs divided by the project's gravity (9.81 down).
            _body.gravityScale = GetJumpGravity(jumpHeight, timeToApex) / -Physics2D.gravity.y;
        }

        public bool IsGrounded => _body.IsTouching(_groundFilter);

        public float JumpHeight => jumpHeight;

        // Jump at the next physics step, if the hero is on the ground then.
        // Public so a Play Mode test can jump without a keyboard.
        public void RequestJump()
        {
            _jumpRequested = true;
        }

        // Once per drawn frame: read the input and update what is drawn.
        // A key press lasts one frame, so it must be read here: FixedUpdate may not run in that frame.
        private void Update()
        {
            _horizontal = Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump"))
            {
                RequestJump();
            }

            _animator.SetFloat(SpeedHash, GetAnimatorSpeed(_horizontal));
            _animator.SetBool(GroundedHash, IsGrounded);
            _animator.SetFloat(VelocityYHash, _body.velocity.y);

            _spriteRenderer.flipX = FacesLeft(_horizontal, _spriteRenderer.flipX);
        }

        // Once per physics step (50 a second by default): move the body.
        // The physics engine moves it by its velocity and stops it at colliders;
        // changing transform.position directly would teleport it past them.
        private void FixedUpdate()
        {
            var velocity = GetVelocity(_horizontal, moveSpeed, _body.velocity);
            if (_jumpRequested)
            {
                // A jump pressed in the air is dropped, not kept for later.
                if (IsGrounded)
                {
                    velocity.y = GetJumpSpeed(jumpHeight, timeToApex);
                }

                _jumpRequested = false;
            }

            _body.velocity = velocity;
        }

        // The rules below don't read input or touch components,
        // so Edit Mode tests can call them directly.

        // Rising with speed v against gravity g, the body stops after v / g seconds, at height
        // v * v / (2 * g). For a chosen height h and time t that gives v = 2h / t and g = 2h / t².
        public static float GetJumpSpeed(float height, float timeToApex)
        {
            return 2f * height / timeToApex;
        }

        public static float GetJumpGravity(float height, float timeToApex)
        {
            return 2f * height / (timeToApex * timeToApex);
        }

        // The body's new velocity: the input sets the horizontal part;
        // the vertical part is left as it is, so gravity and jumps keep working.
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
