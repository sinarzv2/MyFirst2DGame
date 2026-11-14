using Player;
using UnityEngine;

namespace Level
{
    // An invisible area under the level: whatever falls into it and can respawn, respawns.
    // Its collider is a trigger (it reports what enters it but stops nothing). Make it wide
    // enough to lie under every gap and both ends of the level.
    [RequireComponent(typeof(BoxCollider2D))]
    public class KillZone : MonoBehaviour
    {
        // Unity calls Reset when the component is added in the Editor: make the collider a trigger
        // right away, so it can't be forgotten.
        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // The collider that entered may sit on a child; the Rigidbody's object is the Player.
            var body = other.attachedRigidbody;
            var respawn = body != null ? body.GetComponent<PlayerRespawn>() : null;
            if (respawn != null)
            {
                respawn.Respawn();
            }
        }
    }
}
