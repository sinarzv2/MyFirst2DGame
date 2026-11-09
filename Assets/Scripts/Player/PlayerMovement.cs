using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Units per second, matched to the Run animation's feet so they don't slide:
    // feet move back 87 px/s / 46 PPU (Scale 1) = 1.9 units/s, and the Run state plays
    // 1.4x faster in the Animator: 1.9 * 1.4 = ~2.65. Recompute if PPU, Scale or Run Speed change.
    // The Edit Mode test MoveSpeed_MatchesRunFeet checks this.
    [SerializeField] private float moveSpeed = 2.65f;

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

        _animator.SetFloat("Speed", GetAnimatorSpeed(horizontal));

        _spriteRenderer.flipX = FacesLeft(horizontal, _spriteRenderer.flipX);
    }

    // The rules below don't read input or touch components, so Edit Mode tests can call them directly.

    // How far the player moves this frame.
    public static Vector3 GetDisplacement(float horizontal, float speed, float deltaTime)
    {
        return new Vector3(horizontal * speed * deltaTime, 0f, 0f);
    }

    // Value for the Animator's Speed parameter: 0 when standing, 1 when running in either direction.
    public static float GetAnimatorSpeed(float horizontal)
    {
        return Mathf.Abs(horizontal);
    }

    // Face left while moving left and right while moving right; keep the last direction when standing still.
    public static bool FacesLeft(float horizontal, bool facingLeftNow)
    {
        if (horizontal == 0f)
        {
            return facingLeftNow;
        }

        return horizontal < 0f;
    }
}
