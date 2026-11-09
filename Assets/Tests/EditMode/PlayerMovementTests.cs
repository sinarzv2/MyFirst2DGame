using NUnit.Framework;

// Unit tests for the movement rules in PlayerMovement.
// They run in Edit Mode: no Play button and no keyboard needed.
public class PlayerMovementTests
{
    [Test]
    public void GetDisplacement_MovesSpeedTimesTime_InTheInputDirection()
    {
        var right = PlayerMovement.GetDisplacement(1f, 2f, 0.25f);
        var left = PlayerMovement.GetDisplacement(-1f, 2f, 0.25f);

        Assert.AreEqual(0.5f, right.x, 1e-6f);
        Assert.AreEqual(-0.5f, left.x, 1e-6f);
        Assert.AreEqual(0f, right.y);
        Assert.AreEqual(0f, right.z);
    }

    [Test]
    public void GetDisplacement_IsZero_WithoutInput()
    {
        Assert.AreEqual(0f, PlayerMovement.GetDisplacement(0f, 2.65f, 0.016f).magnitude);
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
