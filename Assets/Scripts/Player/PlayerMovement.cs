using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Units per second, matched to the Run animation's feet so they don't slide:
    // feet move back 87 px/s / 46 PPU (Scale 1) = 1.9 units/s, and the Run state plays
    // 1.4x faster in the Animator: 1.9 * 1.4 = ~2.65. Recompute if PPU, Scale or Run Speed change.
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

        var direction = new Vector3(horizontal, 0f, 0f);

        transform.position += direction * (moveSpeed * Time.deltaTime);

        var speed = Mathf.Abs(horizontal);

        _animator.SetFloat("Speed", speed);

        if (horizontal != 0)
        {
            _spriteRenderer.flipX = horizontal < 0;
        }
    }
}
