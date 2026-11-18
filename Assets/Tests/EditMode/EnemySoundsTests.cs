using System.Linq;
using Enemies;
using LevelTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The enemy's sounds as set up in the project: the goblin walk's footstep events, the prefab's sound
// component and the sound files.
namespace Tests.EditMode
{
    public class EnemySoundsTests
    {
        private const string WalkClipPath = "Assets/Animations/Enemy/Goblin_Walk.anim";

        private static readonly string[] SoundPaths =
        {
            "Assets/Audio/Enemy/stomp.wav",
            // goblin_defeat.wav goes with the defeat animation: EnemyDefeatTests checks it
            "Assets/Audio/Enemy/Goblin/goblin_step_1.wav",
            "Assets/Audio/Enemy/Goblin/goblin_step_2.wav",
            "Assets/Audio/Enemy/Goblin/goblin_step_3.wav",
            "Assets/Audio/Enemy/Goblin/goblin_step_4.wav",
        };

        // Four steps in the walk's 36 frames (two strides: each foot lands twice), at frames 6, 16, 25
        // and 33, about a frame before a foot lands (the back foot at 7 and 26, the front one at 17 and
        // 34). The back foot hardly lifts in the 3/4 view, easy to miss: counting the steps catches it.
        [Test]
        public void GoblinWalkClip_CallsFootstep_OncePerStep()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
            Assert.IsNotNull(clip, $"no clip at {WalkClipPath}");
            var events = AnimationUtility.GetAnimationEvents(clip);
            Assert.AreEqual(4, events.Length, "one footstep event per step: the walk has four");
            Assert.That(events.Select(e => e.functionName), Is.All.EqualTo(nameof(EnemySounds.Footstep)),
                "an Animation Event calls a method by its name: the name must be EnemySounds.Footstep's");

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
        public void EnemyPrefab_HasItsSounds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.EnemyPrefabPath);
            var sounds = prefab.GetComponent<EnemySounds>();
            Assert.IsTrue(sounds != null, "the Enemy prefab has no EnemySounds");
            Assert.That(sounds.Footsteps, Has.Length.AtLeast(2), "at least two footsteps, to take turns");
            Assert.That(sounds.Footsteps, Has.None.Null, "an empty footstep slot");
            Assert.IsTrue(sounds.DefeatHit != null, "no defeat hit (stomp.wav)");
            Assert.IsTrue(sounds.DefeatVoice != null, "no defeat voice (goblin_defeat.wav)");
            var source = prefab.GetComponent<AudioSource>();
            Assert.IsFalse(source.playOnAwake, "the AudioSource must not play anything by itself at the start");
            Assert.AreEqual(0f, source.spatialBlend, "2D: EnemySounds sets the volume and the balance itself");
        }

        [Test]
        public void EnemySounds_AreShortMonoClips()
        {
            foreach (var path in SoundPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, $"no sound at {path}");
                Assert.AreEqual(1, clip.channels, $"{path}: a sound effect is mono");
                Assert.That(clip.length, Is.LessThan(0.4f), $"{path}: short");
            }
        }
    }
}
