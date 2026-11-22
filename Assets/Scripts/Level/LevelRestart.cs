using System.Collections;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Level
{
    // Game over: after `delay` seconds (the "GAME OVER" on screen, the hero sitting dazed), the level
    // starts again from scratch: its scene is loaded anew, so the coins, the enemies, the hearts and
    // the lives are all as at the start. Loading the scene again is simpler and safer than putting
    // every object back by hand: nothing can be forgotten.
    public class LevelRestart : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField, Min(0f)] private float delay = 3f;

        public float Delay => delay;

        private void OnEnable()
        {
            if (health == null)
            {
                Debug.LogError("LevelRestart: set Health in the Inspector", this);
                return;
            }

            health.GameOver += OnGameOver;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.GameOver -= OnGameOver;
            }
        }

        private void OnGameOver()
        {
            StartCoroutine(Restart());
        }

        private IEnumerator Restart()
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
