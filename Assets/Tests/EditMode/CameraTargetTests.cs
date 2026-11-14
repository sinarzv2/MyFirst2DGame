using NUnit.Framework;
using Player;

// CameraTarget.GetHeight alone: the height the camera's follow point holds, from where the Player
// is. No scene needed.
namespace Tests.EditMode
{
    public class CameraTargetTests
    {
        // The follow point holds y 0 (the last ground) in every case.
        [TestCase(0f, true, ExpectedResult = 0f, TestName = "Height: standing on the same ground")]
        [TestCase(2f, true, ExpectedResult = 2f, TestName = "Height: landed on higher ground, follows up")]
        [TestCase(2.5f, false, ExpectedResult = 0f, TestName = "Height: jumping, holds the ground's height")]
        [TestCase(-1f, false, ExpectedResult = -1f, TestName = "Height: falling below the ground, follows down")]
        [TestCase(-2f, true, ExpectedResult = -2f, TestName = "Height: landed on lower ground")]
        public float GetHeight_HoldsTheLastGround_ExceptBelowIt(float playerY, bool grounded)
        {
            return CameraTarget.GetHeight(0f, playerY, grounded);
        }
    }
}
