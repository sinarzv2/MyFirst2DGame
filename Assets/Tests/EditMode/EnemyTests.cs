using System.Linq;
using Enemies;
using LevelTools;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEngine;

// The enemy's rules (when to turn, what counts as landing on it, the bounce) and its prefab.
// No scene needed.
namespace Tests.EditMode
{
    public class EnemyTests
    {
        [TestCase(true, true, true, ExpectedResult = true, TestName = "Patrol: turns at a wall")]
        [TestCase(true, false, false, ExpectedResult = true, TestName = "Patrol: turns at a drop")]
        [TestCase(false, false, false, ExpectedResult = false, TestName = "Patrol: in the air, keeps going")]
        [TestCase(true, false, true, ExpectedResult = false, TestName = "Patrol: ground ahead, keeps going")]
        public bool Patrol_TurnsOnlyAtAWallOrADrop(bool grounded, bool wallAhead, bool groundAhead)
        {
            return EnemyPatrol.ShouldTurn(grounded, wallAhead, groundAhead);
        }

        [Test]
        public void Patrol_WalksTheWayItFaces_AndLeavesTheFallAlone()
        {
            Assert.AreEqual(new Vector2(-1.5f, -3f), EnemyPatrol.GetVelocity(true, 1.5f, new Vector2(0f, -3f)));
            Assert.AreEqual(new Vector2(1.5f, -3f), EnemyPatrol.GetVelocity(false, 1.5f, new Vector2(-1.5f, -3f)));
        }

        [TestCase(true, false, ExpectedResult = true, TestName = "Facing: art faces right, walking left, mirrored")]
        [TestCase(false, false, ExpectedResult = false, TestName = "Facing: art faces right, walking right, as drawn")]
        [TestCase(true, true, ExpectedResult = false, TestName = "Facing: art faces left, walking left, as drawn")]
        [TestCase(false, true, ExpectedResult = true, TestName = "Facing: art faces left, walking right, mirrored")]
        public bool Patrol_MirrorsTheArt_OnlyWhenItWalksTheOtherWay(bool facingLeft, bool spriteFacesLeft)
        {
            return EnemyPatrol.GetFlipX(facingLeft, spriteFacesLeft);
        }

        // The enemy stands from 0 to 2. Soles above its middle (1) land on it, its shoulder included;
        // soles below it (walking or jumping into its side) don't.
        [TestCase(2.05f, ExpectedResult = true, TestName = "Stomp: soles just above the top")]
        [TestCase(1.5f, ExpectedResult = true, TestName = "Stomp: soles on its shoulder")]
        [TestCase(1.0f, ExpectedResult = true, TestName = "Stomp: soles at its middle")]
        [TestCase(0.9f, ExpectedResult = false, TestName = "Stomp: soles below its middle")]
        [TestCase(0f, ExpectedResult = false, TestName = "Stomp: walking into its side")]
        public bool Stomp_IsLandingAboveItsMiddle(float playerBottom)
        {
            return Enemy.IsStomp(playerBottom, 0f, 2f);
        }

        // The bounce uses the jump's own gravity; for the jump's height it must give the jump's speed.
        [Test]
        public void LaunchSpeed_ForTheJumpHeight_IsTheJumpSpeed()
        {
            const float height = 3f, time = 0.45f;
            var gravity = PlayerMovement.GetJumpGravity(height, time);
            Assert.AreEqual(PlayerMovement.GetJumpSpeed(height, time), PlayerMovement.GetLaunchSpeed(height, gravity), 1e-4f);
            Assert.AreEqual(0f, PlayerMovement.GetLaunchSpeed(0f, gravity));
        }

        // The goblin's walk: its planted foot slides back this many sprite pixels a frame (measured on
        // the Ludo frames). The patrol speed must match it, or the feet skate on the ground.
        private const float WalkFootPixelsPerFrame = 2.03f;

        [Test]
        public void EnemyPrefab_WalksAtTheSpeedOfItsFeet()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.EnemyPrefabPath);
            var animator = prefab.GetComponent<Animator>();
            Assert.IsTrue(animator != null && animator.runtimeAnimatorController != null,
                "the prefab needs an Animator with the Goblin controller");
            var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Goblin_Walk");
            Assert.IsTrue(clip != null, "the controller should play Goblin_Walk");
            Assert.IsTrue(clip.isLooping, "the walk loops");
            var feetSpeed = WalkFootPixelsPerFrame * clip.frameRate / 46f;
            Assert.AreEqual(feetSpeed, prefab.GetComponent<EnemyPatrol>().Speed, 0.05f,
                "the patrol speed should be the feet's speed (pixels per frame * frames per second / PPU)");
        }

        // Build From Layout puts this prefab wherever the layout has a '@'.
        [Test]
        public void EnemyPrefab_IsSetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelLayoutMenu.EnemyPrefabPath);
            Assert.IsNotNull(prefab, $"no enemy prefab at {LevelLayoutMenu.EnemyPrefabPath}");
            Assert.IsTrue(prefab.GetComponent<Enemy>() != null, "the prefab has no Enemy component");
            var patrol = prefab.GetComponent<EnemyPatrol>();
            Assert.IsTrue(patrol != null, "the prefab has no EnemyPatrol component");

            var body = prefab.GetComponent<Rigidbody2D>();
            Assert.IsTrue(body != null, "the prefab has no Rigidbody2D");
            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType, "a dynamic body: gravity keeps it on the ground");
            Assert.IsTrue(body.freezeRotation, "Freeze Rotation Z: it must not tip over");
            var collider = prefab.GetComponent<Collider2D>();
            Assert.IsFalse(collider.isTrigger, "a solid collider: the Player lands on it, and it walks on the ground");

            var sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            Assert.IsTrue(sprite != null, "the prefab's SpriteRenderer has no sprite");
            Assert.AreEqual(46f, sprite.pixelsPerUnit, "Pixels Per Unit, as every sprite of the game");
            Assert.AreEqual(0f, sprite.pivot.y, 0.5f, "pivot at its feet: the level tool stands it on the ground");
            // You land on what you see: the collider's top is near the top of the art.
            var capsule = collider as CapsuleCollider2D;
            Assert.IsTrue(capsule != null, "a CapsuleCollider2D: round edges, it doesn't catch on tile seams");
            var colliderTop = capsule.offset.y + capsule.size.y / 2f;
            Assert.AreEqual(sprite.bounds.size.y, colliderTop, 0.25f, "the collider's top should be near the top of the sprite");
            Assert.AreEqual(0f, capsule.offset.x, "offset x 0: flipX mirrors the art about the pivot, not the collider");

            var ground = LayerMask.NameToLayer("Ground");
            Assert.AreNotEqual(-1, ground, "no layer named Ground (Project Settings > Tags and Layers)");
            Assert.AreNotEqual(0, patrol.GroundLayers.value & (1 << ground), "EnemyPatrol's Ground Layers should include Ground");
            Assert.AreNotEqual(ground, prefab.layer, "the enemy itself must not be on Ground: its rays would see itself");
        }
    }
}
