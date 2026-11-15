using System.Collections;
using System.Linq;
using Hud;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Coins in the real Main scene: the Player takes one by touching it, once, and the counter on the
// screen shows the new total.
namespace Tests.PlayMode
{
    public class CoinPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TouchingACoin_TakesItOnce_AndTheCounterShowsIt()
        {
            var player = GameObject.Find("Player");
            var wallet = player.GetComponent<CoinWallet>();
            Assert.IsNotNull(wallet, "Player has no CoinWallet");
            var counter = Object.FindObjectOfType<CoinCounterView>();
            Assert.IsNotNull(counter, "no CoinCounterView in the Main scene");
            var coin = Object.FindObjectsOfType<Coin>().FirstOrDefault();
            Assert.IsNotNull(coin, "no coins in the Main scene: Tools > Level > Build From Layout places them");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(CoinCounterView.Format(0), counter.Shown, "the counter from the start");

            // Bring the coin to the standing Player's middle (1.7 units above its soles).
            coin.transform.position = player.transform.position + Vector3.up * 1.7f;
            for (var i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;   // a frame for the counter
            Assert.IsFalse(coin.gameObject.activeSelf, "the coin should be gone");
            Assert.AreEqual(1, wallet.Coins, "one coin in the wallet");
            Assert.AreEqual(CoinCounterView.Format(1), counter.Shown, "the counter should show it");

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, wallet.Coins, "taken once only");
        }
    }
}
