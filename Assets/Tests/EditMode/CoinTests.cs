using System;
using System.Collections.Generic;
using Hud;
using Level;
using LevelTools;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The coin, the wallet that counts coins, and the counter's text. No scene needed.
namespace Tests.EditMode
{
    public class CoinTests
    {
        private GameObject _made;

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            if (_made != null)
            {
                Object.DestroyImmediate(_made);   // Edit Mode: Destroy would wait for a frame that never comes
            }
        }

        private CoinWallet MakeWallet()
        {
            _made = new GameObject("Wallet");
            return _made.AddComponent<CoinWallet>();
        }

        [Test]
        public void Wallet_AddsCoins_AndTellsWhoListens()
        {
            var wallet = MakeWallet();
            var heard = new List<int>();
            wallet.CoinsChanged += heard.Add;

            wallet.Add(1);
            wallet.Add(2);

            Assert.AreEqual(3, wallet.Coins);
            CollectionAssert.AreEqual(new[] { 1, 3 }, heard, "each change, with the new total");
        }

        [TestCase(0, TestName = "Wallet: refuses adding no coins")]
        [TestCase(-1, TestName = "Wallet: refuses taking coins away")]
        public void Wallet_RefusesNothingOrLess(int amount)
        {
            var wallet = MakeWallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(amount));
            Assert.AreEqual(0, wallet.Coins);
        }

        [TestCase(0, ExpectedResult = "x 0", TestName = "Counter: no coins")]
        [TestCase(12, ExpectedResult = "x 12", TestName = "Counter: twelve coins")]
        public string Counter_ShowsTheTotal(int coins)
        {
            return CoinCounterView.Format(coins);
        }

        // Build From Layout puts this prefab wherever the layout has a '$'.
        [Test]
        public void CoinPrefab_IsATriggerWithASprite_AtTheHeroDensity()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.CoinPrefabPath);
            Assert.IsNotNull(prefab, $"no coin prefab at {LevelLayoutMenu.CoinPrefabPath}");
            Assert.IsNotNull(prefab.GetComponent<Coin>(), "the prefab has no Coin component");
            var circle = prefab.GetComponent<CircleCollider2D>();
            Assert.IsTrue(circle != null && circle.isTrigger, "its CircleCollider2D must be a trigger: a coin is walked through, not stood on");
            // Not "?.": in the Editor a missing component is a fake null that "?." doesn't see (chapter 15).
            var renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsTrue(renderer != null, "the prefab has no SpriteRenderer");
            var sprite = renderer.sprite;
            Assert.IsNotNull(sprite, "the prefab's SpriteRenderer has no sprite");
            Assert.AreEqual(46f, sprite.pixelsPerUnit, "Pixels Per Unit, as every sprite of the game (chapter 18)");
        }
    }
}
