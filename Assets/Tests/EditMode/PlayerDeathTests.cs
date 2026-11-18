using System.Linq;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The hero's knock-out as set up in the project: its clip, the Hero controller's state that
// PlayerDeath plays, and the sound that goes with the clip.
namespace Tests.EditMode
{
    public class PlayerDeathTests
    {
        private const string DeathClipPath = "Assets/Animations/Hero/Hero_Death.anim";
        private const string DeathSoundPath = "Assets/Audio/Hero/hero_death.wav";
        private const string HeroControllerPath = "Assets/Animations/Hero/Hero.controller";

        // Plays once and stays on its last picture (sitting, dazed). The hero is out of the game
        // while it plays: long enough to see what happened, short enough not to bore.
        [Test]
        public void HeroDeathClip_PlaysOnce()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
            Assert.IsNotNull(clip, $"no clip at {DeathClipPath}");
            Assert.IsFalse(clip.isLooping, "a knock-out plays once");
            Assert.That(clip.length, Is.InRange(1f, 2.5f));
            Assert.IsEmpty(AnimationUtility.GetAnimationEvents(clip), "no events (the Run's footsteps must not come along)");
        }

        // PlayerDeath plays the state by its name: the controller must have a state of that name,
        // playing the knock-out clip. Standing stays the state it starts in.
        [Test]
        public void HeroController_HasTheDeathState()
        {
            var go = new GameObject("death");
            try
            {
                var stateName = go.AddComponent<PlayerDeath>().DeathState;
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroControllerPath);
                var machine = controller.layers[0].stateMachine;
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
                Assert.IsNotNull(state, $"Hero.controller has no state named {stateName}");
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath), state.motion,
                    $"the {stateName} state should play {DeathClipPath}");
                Assert.AreNotEqual(state, machine.defaultState, "the hero starts standing, not knocked out");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Ludo made the sound for the clip's frames, and both start together: it must not last longer
        // than the clip, or it would go on after the hero is back at the start.
        [Test]
        public void HeroDeathSound_IsMono_AndNoLongerThanTheClip()
        {
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(DeathSoundPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
            Assert.IsNotNull(sound, $"no sound at {DeathSoundPath}");
            Assert.IsNotNull(clip, $"no clip at {DeathClipPath}");
            Assert.AreEqual(1, sound.channels, "a sound effect is mono");
            Assert.That(sound.length, Is.LessThanOrEqualTo(clip.length + 0.01f));
        }
    }
}
