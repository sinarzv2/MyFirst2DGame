using UnityEngine;

namespace Enemies
{
    // Walks back and forth on its ground: turns at a wall and at the edge of a drop, so it never
    // falls off. What counts as ground or wall is anything on the Ground Layers (the level's Tilemap):
    // the Player, coins and other enemies are on other layers, and the rays below don't see them.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyPatrol : MonoBehaviour
    {
        // Units per second: well under the hero's 4, so it can be outrun and jumped.
        [SerializeField, Min(0f)] private float speed = 1.5f;
        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private bool startFacingLeft = true;
        // Which way the art is drawn. Each enemy's image may face either way; flipX turns it to
        // the way it walks.
        [SerializeField] private bool spriteFacesLeft;

        // How far ahead and below the rays look past the collider (units).
        private const float Probe = 0.1f;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private bool _facingLeft;

        public bool FacingLeft => _facingLeft;
        public float Speed => speed;
        public LayerMask GroundLayers => groundLayers;

        // Unity calls Reset when the component is added in the Editor: pick the Ground layer.
        private void Reset()
        {
            groundLayers = LayerMask.GetMask("Ground");
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _facingLeft = startFacingLeft;
        }

        private void FixedUpdate()
        {
            var box = _collider.bounds;
            var ahead = _facingLeft ? Vector2.left : Vector2.right;

            // Ground under its middle, a wall just ahead, ground just ahead of its front foot.
            var grounded = Physics2D.Raycast(new Vector2(box.center.x, box.min.y + Probe), Vector2.down, 2f * Probe, groundLayers);
            var wallAhead = Physics2D.Raycast(box.center, ahead, box.extents.x + Probe, groundLayers);
            var frontFoot = new Vector2(box.center.x + ahead.x * (box.extents.x + Probe), box.min.y + Probe);
            var groundAhead = Physics2D.Raycast(frontFoot, Vector2.down, 2f * Probe, groundLayers);

            if (ShouldTurn(grounded, wallAhead, groundAhead))
            {
                _facingLeft = !_facingLeft;
            }

            _body.velocity = GetVelocity(_facingLeft, speed, _body.velocity);
            _spriteRenderer.flipX = GetFlipX(_facingLeft, spriteFacesLeft);
        }

        // Rules, without physics or components: Edit Mode tests call them directly.

        // Turn at a wall; at a drop only while standing (in the air nothing is ahead, and it would
        // turn every step).
        public static bool ShouldTurn(bool grounded, bool wallAhead, bool groundAhead)
        {
            return wallAhead || (grounded && !groundAhead);
        }

        // Walk at speed the way it faces; leave the vertical part to gravity.
        public static Vector2 GetVelocity(bool facingLeft, float speed, Vector2 current)
        {
            return new Vector2(facingLeft ? -speed : speed, current.y);
        }

        // Mirror the art when it is drawn facing the other way from the way it walks.
        public static bool GetFlipX(bool facingLeft, bool spriteFacesLeft)
        {
            return facingLeft != spriteFacesLeft;
        }
    }
}
