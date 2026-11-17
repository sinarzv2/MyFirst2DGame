using Player;
using UnityEngine;

namespace Enemies
{
    // What happens when the Player touches an enemy: landing on top of it defeats it and bounces the
    // Player up; any other touch sends the Player back to the start. Defeat is public: weapons made
    // later (a sword, arrows) call it too, so stomping stays just one way of defeating an enemy.
    [RequireComponent(typeof(Collider2D))]
    public class Enemy : MonoBehaviour
    {
        // How high the Player bounces off a defeated enemy (units, soles above where it landed).
        [SerializeField, Min(0f)] private float stompBounceHeight = 1.5f;

        private Collider2D _collider;

        public bool Defeated { get; private set; }
        public float StompBounceHeight => stompBounceHeight;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Defeated)
            {
                return;
            }

            // The collider that touched may sit on a child; the Rigidbody's object is the Player.
            var body = collision.rigidbody;
            var player = body != null ? body.GetComponent<PlayerMovement>() : null;
            if (player == null)
            {
                return;   // the ground, a wall, another enemy
            }

            var box = _collider.bounds;
            if (IsStomp(collision.collider.bounds.min.y, box.min.y, box.max.y))
            {
                Defeat();
                player.Bounce(stompBounceHeight);
            }
            else
            {
                var respawn = body.GetComponent<PlayerRespawn>();
                if (respawn != null)
                {
                    respawn.Respawn();
                }
            }
        }

        // Hidden, not destroyed (as a coin): a level restart could bring it back.
        public void Defeat()
        {
            Defeated = true;
            gameObject.SetActive(false);
        }

        // Landing on it: the Player's soles are above its middle. Both are capsules, so a Player
        // landing off-centre first touches the enemy's round shoulder, well below its top; a Player
        // walking into its side has its soles at the enemy's feet.
        public static bool IsStomp(float playerBottom, float enemyBottom, float enemyTop)
        {
            return playerBottom >= (enemyBottom + enemyTop) / 2f;
        }
    }
}
