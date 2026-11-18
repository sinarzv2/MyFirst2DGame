using System.Linq;
using Enemies;
using LevelTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The enemy's defeat as set up in the project: the fade at its end, the goblin's defeat clip, the
// controller's state that Enemy plays, and the sound that goes with the clip.
namespace Tests.EditMode
{
    public class EnemyDefeatTests
    {
        private const string DeathClipPath = "Assets/Animations/Enemy/Goblin_Death.anim";
        private const string DeathSoundPath = "Assets/Audio/Enemy/Goblin/goblin_defeat.wav";

        [TestCase(0f, 0.4f, ExpectedResult = 1f, TestName = "Fade: at the start, fully seen")]
        [TestCase(0.2f, 0.4f, ExpectedResult = 0.5f, TestName = "Fade: halfway, half seen")]
        [TestCase(0.4f, 0.4f, ExpectedResult = 0f, TestName = "Fade: at the end, gone")]
        [TestCase(0f, 0f, ExpectedResult = 0f, TestName = "Fade: no fade time, gone at once")]
        public float Fade_GoesFromSeenToGone(float t, float duration)
        {
            return Enemy.GetFadeAlpha(t, duration);
        }

        // Plays once and stays on its last picture (the goblin lying still), about a second long:
        // long enough to read, short enough not to hold up the game.
        [Test]
        public void GoblinDeathClip_PlaysOnce()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
            Assert.IsNotNull(clip, $"no clip at {DeathClipPath}");
            Assert.IsFalse(clip.isLooping, "a defeat plays once");
            Assert.That(clip.length, Is.InRange(0.5f, 1.5f));
        }

        // Enemy plays the state by its name (Enemy.DefeatState): the prefab's controller must have a
        // state of that name, playing the death clip. Walk stays the state it starts in.
        [Test]
        public void EnemyController_HasTheDefeatState()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.EnemyPrefabPath);
            var enemy = prefab.GetComponent<Enemy>();
            var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(controller, "the Enemy prefab's Animator has no controller");

            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == enemy.DefeatState);
            Assert.IsNotNull(state, $"the controller {controller.name} has no state named {enemy.DefeatState}");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath), state.motion,
                $"the {enemy.DefeatState} state should play {DeathClipPath}");
            Assert.AreNotEqual(state, machine.defaultState, "the enemy starts walking, not defeated");
        }

        // Ludo made the sound for the clip's frames, and both start together: it must not last longer
        // than the clip, or it would go on after the picture has stopped.
        [Test]
        public void GoblinDeathSound_IsMono_AndNoLongerThanTheClip()
        {
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(DeathSoundPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
            Assert.IsNotNull(sound, $"no sound at {DeathSoundPath}");
            Assert.IsNotNull(clip, $"no clip at {DeathClipPath}");
            Assert.AreEqual(1, sound.channels, "a sound effect is mono");
            Assert.That(sound.length, Is.LessThanOrEqualTo(clip.length));
        }
    }
}
