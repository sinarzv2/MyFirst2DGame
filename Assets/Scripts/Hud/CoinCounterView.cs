using Player;
using TMPro;
using UnityEngine;

// Hud: what is drawn over the game (heads-up display). Not "UI": that would clash with UnityEngine.UI.
namespace Hud
{
    // Shows the Player's coin count in a TextMeshPro text. Only shows: the count lives in CoinWallet
    // (model and view kept apart, so the counting can be tested without a screen).
    public class CoinCounterView : MonoBehaviour
    {
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private TMP_Text label;

        // What the label shows now, for tests (they needn't know about TextMeshPro).
        public string Shown => label != null ? label.text : null;

        private void OnEnable()
        {
            if (wallet == null || label == null)
            {
                Debug.LogError("CoinCounterView: set Wallet and Label in the Inspector", this);
                return;
            }

            wallet.CoinsChanged += Show;
            Show(wallet.Coins);   // right from the start, not only after the first coin
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.CoinsChanged -= Show;
            }
        }

        private void Show(int coins)
        {
            label.text = Format(coins);
        }

        // "× 12", next to the coin icon. × is the multiplication sign ×, not the letter x;
        // written as a code so the file's encoding can't spoil it.
        public static string Format(int coins) => $"× {coins}";
    }
}
