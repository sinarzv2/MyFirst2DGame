using System.Collections;
using System.Linq;
using Hud;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        // A still coin (not the spin: a HUD stays calm) on the left of the counter, beside the text.
        [UnityTest]
        public IEnumerator CounterHasACoinIcon_OnItsLeft()
        {
            yield return null;   // a frame, for the Canvas to lay itself out
            var iconObject = GameObject.Find(IconName);
            Assert.IsNotNull(iconObject, $"no \"{IconName}\" in the Main scene");
            var icon = iconObject.GetComponent<Image>();
            Assert.IsTrue(icon != null, $"{IconName} has no UI Image");
            Assert.IsTrue(icon.sprite != null && icon.sprite.texture.name == "Coin", $"{IconName} should show a frame of the coin sheet");
            Assert.IsTrue(icon.preserveAspect, $"{IconName}: tick Preserve Aspect, or the coin is stretched to its box");
            Assert.IsNull(iconObject.GetComponent<Animator>(), $"{IconName} should be still");

            var label = Object.FindObjectOfType<CoinCounterView>().GetComponent<RectTransform>();
            var iconBox = Corners(icon.rectTransform);
            var labelBox = Corners(label);
            Assert.LessOrEqual(iconBox.xMax, labelBox.xMin + 1f, "the icon should end where the text begins, not overlap it");
            Assert.IsTrue(iconBox.center.y > labelBox.yMin && iconBox.center.y < labelBox.yMax, "the icon should be level with the text");
        }

        private const string IconName = "CoinIcon";

        // A UI element's box on the screen (a Screen Space - Overlay canvas: world units are pixels).
        private static Rect Corners(RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);   // bottom-left, top-left, top-right, bottom-right
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }
}
