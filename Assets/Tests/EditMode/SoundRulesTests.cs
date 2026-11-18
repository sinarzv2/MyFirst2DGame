using NUnit.Framework;
using Sounds;

// The rules every sound component uses: whose turn, how much the pitch varies, how loud and how far
// to the side a sound is heard.
namespace Tests.EditMode
{
    public class SoundRulesTests
    {
        [TestCase(0, 2, ExpectedResult = 1)]
        [TestCase(1, 2, ExpectedResult = 0)]
        [TestCase(3, 4, ExpectedResult = 0)]
        [TestCase(0, 1, ExpectedResult = 0)]
        public int Variations_TakeTurns(int index, int count)
        {
            return SoundRules.NextIndex(index, count);
        }

        [Test]
        public void Pitch_StaysWithinTheVariation()
        {
            Assert.AreEqual(0.95f, SoundRules.GetPitch(0.05f, 0f), 1e-5f);
            Assert.AreEqual(1.05f, SoundRules.GetPitch(0.05f, 1f), 1e-5f);
            Assert.AreEqual(1f, SoundRules.GetPitch(0.05f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, SoundRules.GetPitch(0f, 0.9f), 1e-5f, "no variation: always the normal pitch");
        }

        [TestCase(0f, ExpectedResult = 1f, TestName = "Volume: at the listener, full")]
        [TestCase(4f, ExpectedResult = 1f, TestName = "Volume: up to near, full")]
        [TestCase(8f, ExpectedResult = 0.5f, TestName = "Volume: halfway to far, half")]
        [TestCase(12f, ExpectedResult = 0f, TestName = "Volume: at far, silent")]
        [TestCase(40f, ExpectedResult = 0f, TestName = "Volume: beyond far, silent")]
        public float Volume_FadesFromNearToFar(float distance)
        {
            return SoundRules.GetVolume(distance, 4f, 12f);
        }

        [Test]
        public void Volume_WithNoFadeBetweenNearAndFar_IsOnOrOff()
        {
            Assert.AreEqual(1f, SoundRules.GetVolume(3f, 5f, 5f));
            Assert.AreEqual(0f, SoundRules.GetVolume(6f, 5f, 5f));
        }

        [Test]
        public void Pan_FollowsTheSide_ButNeverFully()
        {
            Assert.AreEqual(0f, SoundRules.GetPan(0f, 12f), "in the middle: both speakers");
            Assert.AreEqual(SoundRules.MaxPan / 2f, SoundRules.GetPan(6f, 12f), 1e-5f, "to the right: more on the right");
            Assert.AreEqual(-SoundRules.MaxPan, SoundRules.GetPan(-30f, 12f), 1e-5f, "far left: as far left as it goes");
            Assert.That(SoundRules.MaxPan, Is.LessThan(1f), "never into one ear only");
        }
    }
}
