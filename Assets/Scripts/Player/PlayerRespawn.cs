using Cinemachine;
using UnityEngine;

namespace Player
{
    // Puts the Player back where it started (its position when the scene loaded), standing still.
    // Called by a KillZone when the Player falls out of the level.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRespawn : MonoBehaviour
    {
        // The camera that follows the Player (optional). A respawn is a cut, like a new shot in a film:
        // the camera forgets where it was, so next frame it is placed at the start at once, instead of
        // gliding there across the whole level with its damping (and the Confiner2D's own damping).
        [SerializeField] private CinemachineVirtualCamera followCamera;

        private Rigidbody2D _body;
        private Vector3 _spawnPoint;

        public Vector3 SpawnPoint => _spawnPoint;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _spawnPoint = transform.position;
        }

        public void Respawn()
        {
            // Move the physics body AND the drawn transform: with Interpolate on, moving only the
            // body would draw the Player sliding across the screen for one physics step.
            _body.velocity = Vector2.zero;
            _body.position = _spawnPoint;
            transform.position = _spawnPoint;

            // (OnTargetObjectWarped is for the other case: the whole world shifted and the camera
            // should carry on moving as before.)
            if (followCamera != null)
            {
                followCamera.PreviousStateIsValid = false;
            }
        }
    }
}
