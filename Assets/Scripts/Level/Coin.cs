using Player;
using UnityEngine;

namespace Level
{
    // A coin: whatever touches it and has a CoinWallet takes it. Its collider is a trigger (like the
    // KillZone's): it reports what enters it and stops nothing, so the Player runs through it.
    [RequireComponent(typeof(CircleCollider2D))]
    public class Coin : MonoBehaviour
    {
        [SerializeField, Min(1)] private int value = 1;

        private bool _taken;

        // Unity calls Reset when the component is added in the Editor: make the collider a trigger.
        private void Reset()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Taken once: if two colliders of the Player enter in the same physics step, both calls
            // come before the coin is gone.
            if (_taken)
            {
                return;
            }

            var body = other.attachedRigidbody;
            var wallet = body != null ? body.GetComponent<CoinWallet>() : null;
            if (wallet == null)
            {
                return;
            }

            _taken = true;
            wallet.Add(value);
            gameObject.SetActive(false);   // hidden, not destroyed: a level restart could bring it back
        }
    }
}
