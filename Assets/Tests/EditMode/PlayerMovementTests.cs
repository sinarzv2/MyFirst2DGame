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

        // Seconds since the hero last stood on the ground, and since the jump button was pressed,
        // with 0.1 s of coyote time and of jump buffer.
        [TestCase(0f, 0f, ExpectedResult = true, TestName = "Jump: on the ground, pressed now")]
        [TestCase(0.08f, 0f, ExpectedResult = true, TestName = "Jump: just after leaving a ledge (coyote time)")]
        [TestCase(0.12f, 0f, ExpectedResult = false, TestName = "Jump: too long after leaving a ledge")]
        [TestCase(0f, 0.08f, ExpectedResult = true, TestName = "Jump: pressed just before landing (buffer)")]
        [TestCase(0f, 0.12f, ExpectedResult = false, TestName = "Jump: pressed too long before landing")]
        [TestCase(0f, float.PositiveInfinity, ExpectedResult = false, TestName = "Jump: on the ground, never pressed")]
        [TestCase(float.PositiveInfinity, 0f, ExpectedResult = false, TestName = "Jump: in the air, pressed now")]
        public bool ShouldJump_AllowsALittleLateness_BothWays(float sinceGrounded, float sinceJumpPressed)
        {
            return PlayerMovement.ShouldJump(sinceGrounded, sinceJumpPressed, coyoteTime: 0.1f, jumpBuffer: 0.1f);
        }

        [Test]
        public void CutJumpSpeed_SlowsARise_AndLeavesAFallAlone()
        {
            Assert.AreEqual(6f, PlayerMovement.CutJumpSpeed(12f, 0.5f), 1e-6f, "rising: cut");
            Assert.AreEqual(-5f, PlayerMovement.CutJumpSpeed(-5f, 0.5f), 1e-6f, "falling: untouched");
        }

        [Test]
        public void CutAtTakeOff_GivesTheShortestJump_HeightTimesCutSquared()
        {
            // The height reached is v² / 2g: cutting the speed v by 0.5 cuts the height by 0.5² = 0.25.
            var speed = PlayerMovement.CutJumpSpeed(PlayerMovement.GetJumpSpeed(3f, 0.45f), 0.5f);
            var gravity = PlayerMovement.GetJumpGravity(3f, 0.45f);
            Assert.AreEqual(3f * 0.5f * 0.5f, speed * speed / (2f * gravity), 1e-4f);
        }
    }
}
