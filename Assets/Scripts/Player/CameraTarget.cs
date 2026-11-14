using UnityEngine;

namespace Player
{
    // The point the camera follows, instead of the Player itself. Across, it is always where the
    // Player is. Up and down, it holds the height of the last ground the Player stood on: a jump
    // doesn't move the camera, so the ground stays in view. It moves up when the Player lands on
    // higher ground, and down at once when the Player drops below that height (into a gap, or off
    // a ledge), so a fall is always seen. Most platformers' cameras work this way.
    // A GameObject of its own, not a child of the Player: a child would rise with every jump.
    public class CameraTarget : MonoBehaviour
    {
        [SerializeField] private PlayerMovement player;

        private PlayerRespawn _respawn;
        private float _height;

        private void Awake()
        {
            if (player == null)
            {
                Debug.LogError("CameraTarget: set Player in the Inspector", this);
                enabled = false;
                return;
            }

            _respawn = player.GetComponent<PlayerRespawn>();
            Snap();
        }

        // Told when the Player is put back at the start (Awake runs before OnEnable, so _respawn is
        // set by then). Without this the point would stay down in the gap the Player fell into:
        // back at the start the Player is above it and, until the next physics step, not yet
        // standing, which looks like a jump to GetHeight.
        private void OnEnable()
        {
            if (_respawn != null)
            {
                _respawn.Respawned += Snap;
            }
        }

        // Always undo a subscription: an event keeps the object it calls, even a destroyed one.
        private void OnDisable()
        {
            if (_respawn != null)
            {
                _respawn.Respawned -= Snap;
            }
        }

        // Update, not FixedUpdate: with Interpolate the Player is drawn at a smooth position every
        // frame, and the camera must follow that, not the 50-a-second physics steps.
        private void Update()
        {
            var p = player.transform.position;
            // IsStanding, not IsGrounded: for a moment after a take-off the hero still counts as on
            // the ground while it rises, and the point would rise with it (0.23 units, the test found).
            _height = GetHeight(_height, p.y, player.IsStanding);
            transform.position = new Vector3(p.x, _height, p.z);
        }

        // Straight to where the Player is, height and all.
        private void Snap()
        {
            var p = player.transform.position;
            _height = p.y;
            transform.position = p;
        }

        // held: the height held so far. Standing on ground: that ground's height. Below the held
        // height: follow down. Otherwise (in the air above it, jumping): keep holding it.
        public static float GetHeight(float held, float playerY, bool standing)
        {
            return standing || playerY < held ? playerY : held;
        }
    }
}
