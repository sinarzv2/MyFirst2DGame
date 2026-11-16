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

        [TestCase(0, ExpectedResult = "× 0", TestName = "Counter: no coins")]
        [TestCase(12, ExpectedResult = "× 12", TestName = "Counter: twelve coins")]
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

        // The prefab's Animator plays one looping clip, whose every key is a frame of the coin sheet.
        [Test]
        public void CoinPrefab_Spins_InALoop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.CoinPrefabPath);
            Assert.IsNotNull(prefab, $"no coin prefab at {LevelLayoutMenu.CoinPrefabPath}");
            var animator = prefab.GetComponent<Animator>();
            Assert.IsTrue(animator != null, "the coin prefab has no Animator");
            var controller = animator.runtimeAnimatorController;
            Assert.IsTrue(controller != null, "the coin's Animator has no Controller");
            Assert.AreEqual(1, controller.animationClips.Length, "one clip: the spin");

            var clip = controller.animationClips[0];
            Assert.IsTrue(clip.isLooping, $"{clip.name} must loop (Loop Time)");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length, "one curve: the SpriteRenderer's sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.Greater(keys.Length, 1, "a spin needs more than one frame");
            foreach (var key in keys)
            {
                var frame = key.value as Sprite;
                Assert.IsTrue(frame != null, $"{clip.name}: no sprite at {key.time:0.###} s");
                Assert.AreEqual("Coin", frame.texture.name, $"{frame.name} is not from the coin sheet");
                Assert.AreEqual(46f, frame.pixelsPerUnit, frame.name + ": Pixels Per Unit");
            }
        }
    }
}
