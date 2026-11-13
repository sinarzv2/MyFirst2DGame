using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Checks assets and settings that are easy to get wrong by hand.
// These would have caught the problems found in the review (chapter 14 of the lessons).
namespace Tests.EditMode
{
    public class ProjectAssetTests
    {
        private const string HeroSpritesFolder = "Assets/Sprites/Hero";
        private const string AnimationsFolder = "Assets/Animations";
        private const string HeroControllerPath = "Assets/Animations/Hero/Hero.controller";
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        // Every Hero sheet must use the same Pixels Per Unit, or the hero changes size between animations.
        private const float HeroPixelsPerUnit = 46f;

        // In every Hero sheet the soles are 4 px above the bottom of the cell; that point is the pivot.
        private const float SolesAbovePixels = 4f;

        // The Run animation's planted shoe moves back 7.28 px for every frame of the sheet, on average
        // over a step (Ludo's run: 22.56 px a frame in the video, x 171/530 on the sheet; the speed
        // varies from frame to frame). Per frame, not per second, so a change to the clip's Samples
        // is noticed too.
        private const float RunFootPixelsPerFrame = 7.28f;

        // Built-in Unity assets (default material, default sprite...) use these GUIDs and have no file in the project.
        private static readonly string[] BuiltInGuids = { "0000000000000000e000000000000000", "0000000000000000f000000000000000" };

        private static string[] HeroSheetPaths()
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { HeroSpritesFolder })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();
        }

        private static AnimatorController LoadHeroController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroControllerPath);
            Assert.IsNotNull(controller, HeroControllerPath + " not found");
            return controller;
        }

        [Test]
        public void HeroSheets_AllUseHeroPixelsPerUnit()
        {
            var paths = HeroSheetPaths();
            Assert.IsNotEmpty(paths, "no sprite sheets in " + HeroSpritesFolder);
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(HeroPixelsPerUnit, importer.spritePixelsPerUnit, 0.001f, path);
            }
        }

        [Test]
        public void HeroSprites_PivotIsOnTheSoles()
        {
            foreach (var path in HeroSheetPaths())
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.IsNotEmpty(sprites, path + " has no sprites. Is it sliced?");
                foreach (var sprite in sprites)
                {
                    Assert.AreEqual(sprite.rect.width / 2f, sprite.pivot.x, 0.01f, sprite.name + ": pivot x");
                    Assert.AreEqual(SolesAbovePixels, sprite.pivot.y, 0.01f, sprite.name + ": pivot y");
                }
            }
        }

        [Test]
        public void AnimationClips_HaveNoMissingSprites()
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationsFolder });
            Assert.IsNotEmpty(guids, "no clips in " + AnimationsFolder);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    {
                        // Unity's own != also catches "Missing" references, which a plain C# null check misses.
                        Assert.IsTrue(key.value != null, $"{path}: missing sprite at {key.time:0.###} s");
                    }
                }
            }
        }

        [Test]
        public void HeroController_EveryStateHasAClip_AndTransitionsAreInstant()
        {
            foreach (var layer in LoadHeroController().layers)
            {
                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;
                    Assert.IsTrue(state.motion != null, state.name + " has no clip");
                    foreach (var transition in state.transitions)
                    {
                        // Sprites can't be blended, so a transition must switch at once.
                        var target = transition.destinationState != null ? transition.destinationState.name : "Exit";
                        Assert.AreEqual(0f, transition.duration, 0.0001f, $"{state.name} -> {target}: Transition Duration");
                    }
                }

                // Transitions from Any State too (the jump and the fall start from any state).
                foreach (var transition in layer.stateMachine.anyStateTransitions)
                {
                    var target = transition.destinationState != null ? transition.destinationState.name : "?";
                    Assert.AreEqual(0f, transition.duration, 0.0001f, $"Any State -> {target}: Transition Duration");
                    Assert.IsFalse(transition.canTransitionToSelf, $"Any State -> {target}: Can Transition To Self (it would restart the clip every frame)");
                }
            }
        }

        // Idle and Run repeat; the jump's clips play once and hold their last frame (a long fall
        // stays in the falling pose, a landing doesn't start again).
        [TestCase("Hero_Idle", true)]
        [TestCase("Hero_Run", true)]
        [TestCase("Hero_Jump", false)]
        [TestCase("Hero_Fall", false)]
        [TestCase("Hero_Land", false)]
        public void HeroClips_LoopOnlyWhereTheyShould(string stateName, bool loops)
        {
            var state = LoadHeroController().layers[0].stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(s => s.name == stateName);
            Assert.IsNotNull(state, $"Hero.controller has no state named {stateName}");
            Assert.AreEqual(loops, ((AnimationClip)state.motion).isLooping, $"{stateName}: Loop Time of its clip");
        }

        // A transition that waits for its clip with no condition (Hero_Land -> Hero_Idle) must wait for
        // the whole clip: Exit Time 1. Unity suggests a smaller one when the transition is made
        // (0.615 here, once), which cut the landing off in its deepest crouch.
        [Test]
        public void HeroController_ExitTimeOnlyTransitions_WaitForTheWholeClip()
        {
            foreach (var child in LoadHeroController().layers[0].stateMachine.states)
            {
                foreach (var transition in child.state.transitions)
                {
                    if (!transition.hasExitTime || transition.conditions.Length > 0)
                    {
                        continue;
                    }

                    var target = transition.destinationState != null ? transition.destinationState.name : "Exit";
                    Assert.AreEqual(1f, transition.exitTime, 0.001f, $"{child.state.name} -> {target}: Exit Time");
                }
            }
        }

        // The jump's clips must last as long as the jump does: Hero_Jump plays while the hero rises,
        // Hero_Fall while it comes down again (from the top to the same ground: the same time).
        // A state's Speed stretches its clip: the clip lasts length / Speed seconds.
        [TestCase("Hero_Jump")]
        [TestCase("Hero_Fall")]
        public void JumpClips_FitTheJumpTime(string stateName)
        {
            var state = LoadHeroController().layers[0].stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(s => s.name == stateName);
            Assert.IsNotNull(state, $"Hero.controller has no state named {stateName}");
            var clip = (AnimationClip)state.motion;

            var gameObject = new GameObject("JumpTimeTest");
            float timeToApex;
            try
            {
                timeToApex = new SerializedObject(gameObject.AddComponent<PlayerMovement>()).FindProperty("timeToApex").floatValue;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            // The scene's value wins over the default (lesson 12-9), once the scene has saved one.
            var match = Regex.Match(File.ReadAllText(MainScenePath), @"^\s*timeToApex: ([-\d.eE]+)", RegexOptions.Multiline);
            if (match.Success)
            {
                timeToApex = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            Assert.AreEqual(timeToApex, clip.length / state.speed, timeToApex * 0.05f, $"{stateName}: clip length / state Speed (seconds)");
        }

        [Test]
        public void MoveSpeed_MatchesRunFeet()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(HeroSpritesFolder + "/Hero_Run.png");
            var runState = LoadHeroController().layers[0].stateMachine.states
                .Select(child => child.state)
                .First(state => state.name == "Hero_Run");
            var runClip = (AnimationClip)runState.motion;
            // px per frame * frames per second = px/s; / PPU = units/s; * the state's Speed.
            // Assumes the Player's Scale is 1 (chapter 14); the Play Mode test Player_HasItsComponents_AndScaleOne checks that.
            var feet = RunFootPixelsPerFrame * runClip.frameRate / importer.spritePixelsPerUnit * runState.speed;

            // The default in the code...
            var gameObject = new GameObject("MoveSpeedTest");
            try
            {
                var movement = gameObject.AddComponent<PlayerMovement>();
                var codeDefault = new SerializedObject(movement).FindProperty("moveSpeed").floatValue;
                Assert.AreEqual(feet, codeDefault, feet * 0.03f, "moveSpeed default in PlayerMovement.cs");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            // ...and the value saved in the scene, which wins over the default (lesson 12-9).
            var match = Regex.Match(File.ReadAllText(MainScenePath), @"^\s*moveSpeed: ([-\d.eE]+)", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "no PlayerMovement in " + MainScenePath);
            var sceneValue = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.AreEqual(feet, sceneValue, feet * 0.03f, "moveSpeed of the Player in " + MainScenePath);
        }

        [TestCase(PlayerMovement.SpeedParameter, AnimatorControllerParameterType.Float)]
        [TestCase(PlayerMovement.GroundedParameter, AnimatorControllerParameterType.Bool)]
        [TestCase(PlayerMovement.VelocityYParameter, AnimatorControllerParameterType.Float)]
        public void HeroController_HasTheParametersUsedByTheCode(string name, AnimatorControllerParameterType type)
        {
            var parameter = LoadHeroController().parameters.FirstOrDefault(p => p.name == name);
            Assert.IsNotNull(parameter, $"Hero.controller has no parameter named \"{name}\"");
            Assert.AreEqual(type, parameter.type, $"type of the parameter \"{name}\"");
        }

        [Test]
        public void Scenes_HaveNoMissingReferences()
        {
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();
            Assert.IsNotEmpty(scenePaths, "no scenes in Assets/Scenes");
            foreach (var scenePath in scenePaths)
            {
                // Every reference in a scene file looks like {fileID: <object in the file>, guid: <file>, type: <n>}.
                var references = Regex.Matches(File.ReadAllText(scenePath), @"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}");
                foreach (Match reference in references)
                {
                    var fileId = long.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture);
                    var guid = reference.Groups[2].Value;
                    if (BuiltInGuids.Contains(guid))
                    {
                        continue;
                    }

                    // The file must exist...
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    Assert.IsFalse(string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null,
                        $"{scenePath}: missing asset with guid {guid}");

                    // ...and so must the object inside it (for example one sprite of a sheet).
                    var found = AssetDatabase.LoadAllAssetsAtPath(path).Any(asset =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId) && localId == fileId);
                    Assert.IsTrue(found, $"{scenePath}: {path} has no object with fileID {fileId} (shown as Missing)");
                }
            }
        }

        [Test]
        public void BuildSettings_ScenesExist()
        {
            Assert.IsNotEmpty(EditorBuildSettings.scenes, "no scenes in Build Settings");
            foreach (var scene in EditorBuildSettings.scenes)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path), scene.path);
            }
        }
    }
}
