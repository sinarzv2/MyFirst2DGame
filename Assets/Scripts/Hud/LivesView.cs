using Player;
using TMPro;
using UnityEngine;

namespace Hud
{
    // The lives left, as "× 3" beside the hero's head. Only shows: the lives live in PlayerHealth.
    public class LivesView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private TMP_Text label;

        public string Shown => label != null ? label.text : null;

        private void OnEnable()
        {
            if (health == null || label == null)
            {
                Debug.LogError("LivesView: set Health and Label in the Inspector", this);
                return;
            }

            health.LivesChanged += Show;
            Show(health.Lives);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.LivesChanged -= Show;
            }
        }

        // Written like the coin counter, so the two read alike.
        private void Show(int lives)
        {
            label.text = CoinCounterView.Format(lives);
        }
    }
}
