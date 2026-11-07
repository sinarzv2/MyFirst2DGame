using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

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
