using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        // Units per second. The Run animation's planted shoe moves back 7.28 px a frame (on average
        // over a step): at its 16.94 frames/s that is 123.3 px/s, / 46 PPU (Scale 1) = 2.68 units/s.
        // The hero runs faster than that, 4 units/s, so the Run state plays the clip faster to match:
        // Speed 4 / 2.68 = 1.49 (about 25 frames/s). The feet still don't slide.
        // Why 4: a jump must clear a one-cell gap (2 units, but about 2.9 between the rounded grass
        // corners). The distance covered in a jump is moveSpeed * 2 * timeToApex = 4 * 0.9 = 3.6 units;
        // at 2.68 it was 2.1, not enough. A two-cell gap (about 4.9) stays impossible.
        // Much faster and the jog-paced Run clip would look sped up (legs whirring): a faster hero
        // would want a sprint animation of its own. (5 was tried; the user preferred 4.)
        // Recompute if PPU, Scale, the Run clip's Samples or Run Speed change.
        // The Edit Mode test MoveSpeed_MatchesRunFeet checks this.
        [SerializeField] private float moveSpeed = 4f;

        // The jump is designed by its height (units, soles above the ground) and the time to reach it
        // (seconds); gravity and the take-off speed are worked out from those two (see GetJumpGravity).
        // 3 units clears one ground tile (2 units) with room to spare and is a bit less than the hero
        // (3.7 units). The Hero_Jump and Hero_Fall states' Speed fit their clips to timeToApex
        // (the Edit Mode test JumpClips_FitTheJumpTime checks it).
        // Min: both are divided by, so the Inspector must not accept 0 (gravity would be infinite).
        [SerializeField, Min(0.1f)] private float jumpHeight = 3f;
        [SerializeField, Min(0.05f)] private float timeToApex = 0.45f;

        // Forgiveness for presses a little off, as most platformers do (seconds; 0.1 = 5 physics steps).
        // Coyote time: a jump still works this long after running off a ledge.
        // Jump buffer: a press this long before landing jumps as soon as the hero lands.
        [SerializeField, Range(0f, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBuffer = 0.1f;

        // Letting go of the button while rising multiplies the rising speed by this. The height left
        // to climb goes with the speed squared, so letting go at take-off gives the shortest jump:
        // jumpHeight * 0.5² = 0.75 units. 1 = the jump is always full height.
        [SerializeField, Range(0f, 1f)] private float jumpCutFactor = 0.5f;

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
        private bool _jumpReleased;

        // Clocks, in seconds, counted in FixedUpdate. Infinity = long ago, or used up.
        private float _sinceGrounded = float.PositiveInfinity;
        private float _sinceJumpPressed = float.PositiveInfinity;

        // Rising from a jump that letting go of the button can still cut.
        private bool _rising;

        // From the step a jump starts until the body has really left the ground (see IsStanding).
        private bool _takingOff;

        private PlayerRespawn _respawn;   // optional: on the same GameObject

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();
            _respawn = GetComponent<PlayerRespawn>();
            _groundFilter.SetNormalAngle(45f, 135f);

            // Gravity Scale comes from the jump's design, not from the Inspector:
            // the gravity it needs divided by the project's gravity (9.81 down).
            _body.gravityScale = GetJumpGravity(jumpHeight, timeToApex) / -Physics2D.gravity.y;
        }

        // A respawn starts afresh (Awake runs before OnEnable, so _respawn is set by then).
        private void OnEnable()
        {
            if (_respawn != null)
            {
                _respawn.Respawned += ForgetJump;
            }
        }

        private void OnDisable()
        {
            if (_respawn != null)
            {
                _respawn.Respawned -= ForgetJump;
            }
        }

        // Without this, a press made just before falling into the kill zone is still in the jump
        // buffer when the hero is put back, and it jumps at the start by itself.
        private void ForgetJump()
        {
            _sinceJumpPressed = float.PositiveInfinity;
            _jumpReleased = false;
            _rising = false;
            _takingOff = false;
        }

        public bool IsGrounded => _body.IsTouching(_groundFilter);

        // On the ground and not taking off from it. The physics engine finds contacts at the start of
        // a step, before moving bodies: after the step in which a jump starts, the body is already
        // 0.27 units up but its contact with the ground is still listed until the next step. So
        // IsGrounded stays true for a moment while the hero rises; this doesn't.
        // (Not "on the ground and not moving up", the first version: a hero walking up a rounded
        // grass corner, or riding a platform that moves up, moves up too and is standing all the same.)
        public bool IsStanding => IsGrounded && !_takingOff;

        // Read-only views of the settings, for tests that move the hero as the script would.
        public float JumpHeight => jumpHeight;
        public float MoveSpeed => moveSpeed;
        public float JumpSpeed => GetJumpSpeed(jumpHeight, timeToApex);
        public float JumpCutFactor => jumpCutFactor;
        public float CoyoteTime => coyoteTime;
        public float JumpBuffer => jumpBuffer;

        // The jump button went down: jump as soon as the hero may (now, or on landing within the
        // buffer). Public, like ReleaseJump, so a Play Mode test can jump without a keyboard.
        public void RequestJump()
        {
            _sinceJumpPressed = 0f;
            _jumpReleased = false;
        }

        // The jump button came up: a jump still rising is cut short.
        public void ReleaseJump()
        {
            _jumpReleased = true;
        }

        // Once per drawn frame: read the input and update what is drawn.
        // A key press lasts one frame, so it must be read here: FixedUpdate may not run in that frame.
        // Up, not "not held": a test that calls RequestJump holds no key, and must get a full jump.
        private void Update()
        {
            _horizontal = Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump"))
            {
                RequestJump();
            }

            if (Input.GetButtonUp("Jump"))
            {
                ReleaseJump();
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

            // The take-off is over once the body has left the ground, or was stopped before it could.
            if (_takingOff && (!IsGrounded || velocity.y <= 0f))
            {
                _takingOff = false;
            }

            // Standing on the ground restarts the coyote clock. Not while taking off (see IsStanding):
            // that must not allow a second jump.
            if (IsStanding)
            {
                _sinceGrounded = 0f;
            }

            if (ShouldJump(_sinceGrounded, _sinceJumpPressed, coyoteTime, jumpBuffer))
            {
                velocity.y = GetJumpSpeed(jumpHeight, timeToApex);
                // Both used up: one press, one jump; and no second jump in the coyote time.
                _sinceGrounded = float.PositiveInfinity;
                _sinceJumpPressed = float.PositiveInfinity;
                _rising = true;
                _takingOff = true;
            }

            // Cut once, while rising. If the button was let go before a buffered jump started,
            // that jump is cut at once: a tap gives a short jump either way.
            if (_rising && _jumpReleased)
            {
                velocity.y = CutJumpSpeed(velocity.y, jumpCutFactor);
                _rising = false;
            }
            else if (_rising && velocity.y <= 0f)
            {
                _rising = false;   // past the top: letting go now changes nothing
            }

            _body.velocity = velocity;
            _sinceGrounded += Time.fixedDeltaTime;
            _sinceJumpPressed += Time.fixedDeltaTime;
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

        // Jump if the hero stood on the ground a moment ago (coyote time) and the button was pressed
        // a moment ago (jump buffer). Pressing on the ground is the case where both are 0.
        public static bool ShouldJump(float sinceGrounded, float sinceJumpPressed, float coyoteTime, float jumpBuffer)
        {
            return sinceGrounded <= coyoteTime && sinceJumpPressed <= jumpBuffer;
        }

        // Letting go of the button: a rise is slowed (so it ends lower), a fall is left alone.
        public static float CutJumpSpeed(float velocityY, float cutFactor)
        {
            return velocityY > 0f ? velocityY * cutFactor : velocityY;
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
