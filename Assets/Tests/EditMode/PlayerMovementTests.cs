using NUnit.Framework;
using Player;
using UnityEngine;

// Unit tests for the movement rules in PlayerMovement.
// They run in Edit Mode: no Play button and no keyboard needed.
namespace Tests.EditMode
{
    public class PlayerMovementTests
    {
        [Test]
        public void GetVelocity_IsSpeed_InTheInputDirection()
        {
            Assert.AreEqual(2f, PlayerMovement.GetVelocity(1f, 2f, Vector2.zero).x, 1e-6f);
            Assert.AreEqual(-2f, PlayerMovement.GetVelocity(-1f, 2f, Vector2.zero).x, 1e-6f);
        }

        [Test]
        public void GetVelocity_StopsHorizontally_WithoutInput()
        {
            Assert.AreEqual(0f, PlayerMovement.GetVelocity(0f, 2.68f, new Vector2(2.68f, 0f)).x);
        }

        [Test]
        public void GetVelocity_KeepsTheVerticalVelocity()
        {
            // Falling (or, later, jumping) must not be cancelled by walking.
            Assert.AreEqual(-3f, PlayerMovement.GetVelocity(1f, 2.68f, new Vector2(0f, -3f)).y);
        }

        [Test]
        public void JumpSpeedAndGravity_ReachTheHeight_InTheTime()
        {
            var speed = PlayerMovement.GetJumpSpeed(3f, 0.4f);
            var gravity = PlayerMovement.GetJumpGravity(3f, 0.4f);

            // Rising with speed v against gravity g: stops after v / g seconds, at v² / 2g.
            Assert.AreEqual(3f, speed * speed / (2f * gravity), 1e-4f, "height of the top of the jump");
            Assert.AreEqual(0.4f, speed / gravity, 1e-5f, "time to the top of the jump");
        }

        [TestCase(1f, ExpectedResult = 1f)]
        [TestCase(-1f, ExpectedResult = 1f)]
        [TestCase(0f, ExpectedResult = 0f)]
        public float GetAnimatorSpeed_IsHowFast_NotWhichWay(float horizontal)
        {
            return PlayerMovement.GetAnimatorSpeed(horizontal);
        }

        [TestCase(-1f, false, ExpectedResult = true)]
        [TestCase(1f, true, ExpectedResult = false)]
        [TestCase(0f, true, ExpectedResult = true)]
        [TestCase(0f, false, ExpectedResult = false)]
        public bool FacesLeft_FollowsInput_AndKeepsDirectionWhenStanding(float horizontal, bool facingLeftNow)
        {
            return PlayerMovement.FacesLeft(horizontal, facingLeftNow);
        }
    }
}
