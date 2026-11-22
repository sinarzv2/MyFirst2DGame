using Player;
using UnityEngine;
using UnityEngine.UI;

namespace Hud
{
    // The hero's hearts as a row of images: the first `Hearts` full, the rest empty. Only shows: the
    // hearts live in PlayerHealth (model and view apart, as the coin counter).
    public class HeartsView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Image[] hearts;
        [SerializeField] private Sprite full;
        [SerializeField] private Sprite empty;

        // How many are shown full now, for tests.
        public int ShownFull { get; private set; }

        private void OnEnable()
        {
            if (health == null || hearts == null || hearts.Length == 0 || full == null || empty == null)
            {
                Debug.LogError("HeartsView: set Health, Hearts, Full and Empty in the Inspector", this);
                return;
            }

            if (hearts.Length != health.HeartsPerLife)
            {
                Debug.LogError($"HeartsView: {hearts.Length} images for {health.HeartsPerLife} hearts a life", this);
            }

            health.HeartsChanged += Show;
            Show(health.Hearts);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HeartsChanged -= Show;
            }
        }

        private void Show(int count)
        {
            for (var i = 0; i < hearts.Length; i++)
            {
                hearts[i].sprite = IsFull(i, count) ? full : empty;
            }

            ShownFull = Mathf.Clamp(count, 0, hearts.Length);
        }

        // Filled from the left: heart i (0 = leftmost) is full while there are more than i hearts.
        public static bool IsFull(int index, int hearts) => index < hearts;
    }
}
