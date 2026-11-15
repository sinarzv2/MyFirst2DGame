using System;
using UnityEngine;

namespace Player
{
    // How many coins the Player has. Coins add to it; whatever shows the count (CoinCounterView)
    // listens to CoinsChanged, so the wallet doesn't need to know about the screen (chapter 22-4).
    // The count stays through a respawn: falling costs the way back, not the coins.
    public class CoinWallet : MonoBehaviour
    {
        public int Coins { get; private set; }

        // Raised with the new total after every change.
        public event Action<int> CoinsChanged;

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "add at least one coin");
            }

            Coins += amount;
            CoinsChanged?.Invoke(Coins);
        }
    }
}
