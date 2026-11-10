using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerMovement : MonoBehaviour
    {
        // Units per second, matched to the Run animation's feet so they don't slide:
        // the planted shoe moves back 4 px a frame at 30 frames/s = 120 px/s,
        // / 46 PPU (Scale 1) = 2.6 units/s, with the Run state at Speed 1.
        // Recompute if PPU, Scale or Run Speed change. The Edit Mode test
        // MoveSpeed_MatchesRunFeet checks this.
        [SerializeField] private float moveSpeed = 2.6f;

        // Name of the Animator parameter this script drives. Hero.controller must
        // have a Float parameter with this name (an Edit Mode test checks it).
        public const string SpeedParameter = "Speed";

        // The Animator finds parameters by a number (hash). Working it out once
        // is cheaper than passing the name every frame.
        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

        private Animator _animator;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            var horizontal = Input.GetAxisRaw("Horizontal");

            transform.position += GetDisplacement(horizontal, moveSpeed, Time.deltaTime);

            _animator.SetFloat(SpeedHash, GetAnimatorSpeed(horizontal));

            _spriteRenderer.flipX = FacesLeft(horizontal, _spriteRenderer.flipX);
        }

        // The rules below don't read input or touch components,
        // so Edit Mode tests can call them directly.

        // How far the player moves this frame.
        public static Vector3 GetDisplacement(float horizontal, float speed, float deltaTime)
        {
            return new Vector3(horizontal * speed * deltaTime, 0f, 0f);
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
