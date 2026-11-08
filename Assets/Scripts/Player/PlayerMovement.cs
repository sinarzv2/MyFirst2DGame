using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Units per second. Matches the Run animation's feet: they slide back ~1.45 units/s,
    // and the Run state plays 1.4x faster in the Animator, so ~2 units/s (no foot sliding).
    [SerializeField] private float moveSpeed = 2f;

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
