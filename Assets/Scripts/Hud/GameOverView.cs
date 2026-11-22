using Player;
using TMPro;
using UnityEngine;

namespace Hud
{
    // "GAME OVER" across the screen once the last life is lost; hidden until then. The text is
    // switched off, not its GameObject: a switched-off GameObject runs nothing, not even this.
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private TMP_Text label;

        public bool IsShown => label != null && label.enabled;

        private void OnEnable()
        {
            if (health == null || label == null)
            {
                Debug.LogError("GameOverView: set Health and Label in the Inspector", this);
                return;
            }

            label.enabled = health.IsGameOver;
            health.GameOver += Show;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.GameOver -= Show;
            }
        }

        private void Show()
        {
            label.enabled = true;
        }
    }
}
