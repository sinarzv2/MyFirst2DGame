using System.Linq;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEngine;

// The hero's sounds: what is a landing, the Run clip's footstep events and the sound files (the
// rules shared with the enemies' sounds are in SoundRulesTests).
namespace Tests.EditMode
{
    public class PlayerSoundsTests
    {
        private const string RunClipPath = "Assets/Animations/Hero/Hero_Run.anim";

        private static readonly string[] SoundPaths =
        {
            "Assets/Audio/Hero/hero_step_1.wav",
            "Assets/Audio/Hero/hero_step_2.wav",
            "Assets/Audio/Hero/hero_jump.wav",
            "Assets/Audio/Hero/hero_land.wav",
        };

        [TestCase(true, 0.5f, ExpectedResult = true, TestName = "Landing: standing after a jump")]
        [TestCase(true, 0.04f, ExpectedResult = false, TestName = "Landing: a bump is not a landing")]
        [TestCase(false, 0.5f, ExpectedResult = false, TestName = "Landing: still in the air")]
        [TestCase(true, 0f, ExpectedResult = false, TestName = "Landing: standing all along")]
        public bool Landing_IsStandingAfterTimeInTheAir(bool standing, float airTime)
        {
            return PlayerMovement.HasLanded(standing, airTime, PlayerMovement.MinAirTimeForLanding);
        }

        // The Run clip calls Footstep once per step: four steps (two strides) in its 36 frames, a
        // frame before each foot lands.
        [Test]
        public void HeroRunClip_CallsFootstep_OncePerStep()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath);
            Assert.IsNotNull(clip, $"no clip at {RunClipPath}");
            var events = AnimationUtility.GetAnimationEvents(clip);
            Assert.AreEqual(4, events.Length, "one footstep event per step: the Run clip has four");
            foreach (var e in events)
            {
                Assert.AreEqual(nameof(PlayerSounds.Footstep), e.functionName,
                    "an Animation Event calls a method by its name: the name must be PlayerSounds.Footstep's");
                Assert.That(e.time, Is.InRange(0f, clip.length));
            }

            // Spread over the cycle like the steps: each 7 to 11 frames after the one before.
            var total = Mathf.RoundToInt(clip.length * clip.frameRate);
            var frames = events.Select(e => Mathf.RoundToInt(e.time * clip.frameRate)).OrderBy(f => f).ToArray();
            for (var i = 0; i < frames.Length; i++)
            {
                var gap = (frames[(i + 1) % frames.Length] - frames[i] + total) % total;
                Assert.That(gap, Is.InRange(7, 11), $"steps at frames {string.Join(", ", frames)}");
            }
        }

        [Test]
        public void HeroSounds_AreShortMonoClips()
        {
            foreach (var path in SoundPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, $"no sound at {path}");
                Assert.AreEqual(1, clip.channels, $"{path}: a sound effect is mono");
                Assert.That(clip.length, Is.LessThan(0.35f),
                    $"{path}: shorter than the time between two running steps (0.357 s)");
            }

            const string respawnPath = "Assets/Audio/Hero/hero_respawn.wav";
            var respawn = AssetDatabase.LoadAssetAtPath<AudioClip>(respawnPath);
            Assert.IsNotNull(respawn, $"no sound at {respawnPath}");
            Assert.AreEqual(1, respawn.channels, $"{respawnPath}: a sound effect is mono");
            Assert.That(respawn.length, Is.LessThan(0.5f), $"{respawnPath}: short");
        }
    }
}
