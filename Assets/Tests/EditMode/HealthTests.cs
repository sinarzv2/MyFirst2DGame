using System;
using Hud;
using LevelTools;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEngine;

// The rules of hearts and lives (Health), the little rules of PlayerHealth and the HUD, and the HUD's
// icons.
namespace Tests.EditMode
{
    public class HealthTests
    {
        [Test]
        public void Start_AllLives_AllHearts()
        {
            var health = new Health(3, 3);
            Assert.AreEqual(3, health.Hearts);
            Assert.AreEqual(3, health.Lives);
            Assert.IsFalse(health.IsGameOver);
        }

        [Test]
        public void AnEnemy_CostsAHeart()
        {
            var health = new Health(3, 3);
            Assert.AreEqual(HealthResult.Hurt, health.TakeHit());
            Assert.AreEqual(2, health.Hearts);
            Assert.AreEqual(3, health.Lives, "a heart, not a life");
        }

        [Test]
        public void TheLastHeart_CostsALife_AndTheNextLifeStartsFull()
        {
            var health = new Health(3, 3);
            health.TakeHit();
            health.TakeHit();
            Assert.AreEqual(HealthResult.LifeLost, health.TakeHit());
            Assert.AreEqual(0, health.Hearts);
            Assert.AreEqual(2, health.Lives);

            health.Refill();
            Assert.AreEqual(3, health.Hearts, "back at the start: all the hearts of the new life");
        }

        [Test]
        public void APit_CostsAWholeLife_HoweverManyHeartsAreLeft()
        {
            var health = new Health(3, 3);
            health.TakeHit();   // 2 hearts left: the pit takes them both, and the life
            Assert.AreEqual(HealthResult.LifeLost, health.LoseLife());
            Assert.AreEqual(0, health.Hearts);
            Assert.AreEqual(2, health.Lives);
        }

        [Test]
        public void TheLastLife_IsGameOver_ByAPitOrByTheLastHeart()
        {
            var byPit = new Health(3, 1);
            Assert.AreEqual(HealthResult.GameOver, byPit.LoseLife());
            Assert.IsTrue(byPit.IsGameOver);

            var byHearts = new Health(1, 1);
            Assert.AreEqual(HealthResult.GameOver, byHearts.TakeHit());
            Assert.IsTrue(byHearts.IsGameOver);
            byHearts.Refill();
            Assert.AreEqual(0, byHearts.Hearts, "no refill after the game is over");
        }

        [Test]
        public void AfterTheGameIsOver_NothingMoreCanBeLost()
        {
            var health = new Health(3, 1);
            health.LoseLife();
            Assert.Throws<InvalidOperationException>(() => health.TakeHit());
            Assert.Throws<InvalidOperationException>(() => health.LoseLife());
        }

        // Knocked out (no hearts) until the next life starts: no hit counts meanwhile.
        [Test]
        public void WithNoHeartsLeft_AHitIsAMistake()
        {
            var health = new Health(1, 3);
            health.TakeHit();
            Assert.Throws<InvalidOperationException>(() => health.TakeHit());
        }

        [TestCase(0, 3)]
        [TestCase(3, 0)]
        public void ZeroHeartsOrLives_IsAMistake(int hearts, int lives)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(hearts, lives));
        }

        [TestCase(5f, 3f, false, ExpectedResult = 1f, TestName = "Knockback: enemy on the left, thrown right")]
        [TestCase(1f, 3f, false, ExpectedResult = -1f, TestName = "Knockback: enemy on the right, thrown left")]
        [TestCase(3f, 3f, false, ExpectedResult = -1f, TestName = "Knockback: level, facing right, thrown back left")]
        [TestCase(3f, 3f, true, ExpectedResult = 1f, TestName = "Knockback: level, facing left, thrown back right")]
        public float Knockback_IsAwayFromTheEnemy(float heroX, float enemyX, bool facingLeft)
        {
            return PlayerHealth.GetKnockbackDirection(heroX, enemyX, facingLeft);
        }

        [TestCase(0f, ExpectedResult = true, TestName = "Blink: shown at first")]
        [TestCase(0.15f, ExpectedResult = false, TestName = "Blink: hidden in the second interval")]
        [TestCase(0.25f, ExpectedResult = true, TestName = "Blink: shown again in the third")]
        public bool Blinking_TakesTurns(float t)
        {
            return PlayerHealth.IsShownWhileBlinking(t, 0.1f);
        }

        [TestCase(0, 2, ExpectedResult = true)]
        [TestCase(1, 2, ExpectedResult = true)]
        [TestCase(2, 2, ExpectedResult = false)]
        [TestCase(0, 0, ExpectedResult = false)]
        public bool HeartsRow_FillsFromTheLeft(int index, int hearts)
        {
            return HeartsView.IsFull(index, hearts);
        }

        // Tools > Hud > Build Hearts And Lives imports them as single sprites.
        [TestCase(HudMenu.HeartFullPath)]
        [TestCase(HudMenu.HeartEmptyPath)]
        [TestCase(HudMenu.HeroHeadPath)]
        public void HudIcons_AreSprites(string path)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), $"no icon at {path}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path), $"{path} is not a Sprite: run Tools > Hud > Build Hearts And Lives");
        }
    }
}
