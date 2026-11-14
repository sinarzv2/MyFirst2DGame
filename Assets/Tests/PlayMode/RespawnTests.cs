using System.Collections;
using System.Collections.Generic;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.TestTools;

// PlayerRespawn and KillZone on their own. Each test builds the few objects it needs, far away from
// any level: the Test Runner doesn't clear the scene between tests, so whatever scene an earlier test
// loaded (Main, with its ground around x = 0) is still there. (A first version built them at x = 0
// and they landed on the grass.) An error logged during a test, such as a NullReferenceException,
// also fails it.
namespace Tests.PlayMode
{
    public class RespawnTests
    {
        private static readonly Vector2 FarAway = new Vector2(1000f, 0f);

        // Everything a test made, destroyed after it (passed or failed): the scene stays loaded, and a
        // body left behind would go on falling and respawning through the next tests.
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            foreach (var gameObject in _made)
            {
                Object.Destroy(gameObject);
            }

            _made.Clear();
        }

        private GameObject Make(string name, Vector2 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            _made.Add(gameObject);
            return gameObject;
        }

        // A falling body that can respawn, with its spawn point at `spawn` (where it is when its
        // PlayerRespawn wakes up). Its collider can sit on a child object, as a sprite's often does.
        private (GameObject, Rigidbody2D, PlayerRespawn) MakeRespawner(Vector2 spawn, bool colliderOnChild = false)
        {
            var root = Make("Respawner", spawn);
            var body = root.AddComponent<Rigidbody2D>();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;   // as the Player's

            var holder = root;
            if (colliderOnChild)
            {
                holder = new GameObject("Collider");
                holder.transform.SetParent(root.transform, false);
            }

            holder.AddComponent<BoxCollider2D>();
            var respawn = root.AddComponent<PlayerRespawn>();   // in Play Mode its Awake runs right here
            return (root, body, respawn);
        }

        private void MakeKillZone(Vector2 centre, Vector2 size)
        {
            var zone = Make("KillZone", centre);
            zone.AddComponent<KillZone>();                      // adds its BoxCollider2D (RequireComponent)
            var box = zone.GetComponent<BoxCollider2D>();
            box.isTrigger = true;                               // KillZone.Reset does this in the Editor only
            box.size = size;
        }

        [UnityTest]
        public IEnumerator Respawn_PutsItBackAtItsSpawnPoint_StandingStill()
        {
            var spawn = FarAway + new Vector2(3f, 4f);
            var (root, body, respawn) = MakeRespawner(spawn);
            body.gravityScale = 0f;
            Assert.AreEqual((Vector3)spawn, respawn.SpawnPoint, "spawn point = where it was when it woke up");

            yield return new WaitForFixedUpdate();
            body.position = FarAway + new Vector2(10f, -5f);
            body.velocity = new Vector2(5f, -7f);
            yield return new WaitForFixedUpdate();

            respawn.Respawn();

            // Right away, not after the next physics step: the physics body, the drawn position, the speed.
            Assert.That(Vector2.Distance(body.position, spawn), Is.LessThan(0.001f), "the Rigidbody's position");
            Assert.That(Vector3.Distance(root.transform.position, spawn), Is.LessThan(0.001f), "the drawn position (Transform)");
            Assert.AreEqual(Vector2.zero, body.velocity, "velocity");
        }

        [UnityTest]
        public IEnumerator KillZone_RespawnsWhatFallsIn_EvenWithItsColliderOnAChild()
        {
            MakeKillZone(FarAway + new Vector2(0f, -10f), new Vector2(10f, 2f));      // its top at y -9
            var (_, body, _) = MakeRespawner(FarAway, colliderOnChild: true);

            // It falls from 0 to the zone (about 1.4 s), is put back at 0 and starts falling again:
            // wait for the moment it is back up after having been down there.
            var lowest = 0f;
            var respawned = false;
            for (var t = 0f; t < 3f && !respawned; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, body.position.y);
                respawned = lowest < -8f && body.position.y > -1f;
            }

            Assert.IsTrue(respawned, $"it should fall into the kill zone and be put back at its spawn point (lowest y {lowest:0.0})");
        }

        [UnityTest]
        public IEnumerator KillZone_LetsWhatCannotRespawnFallThrough()
        {
            MakeKillZone(FarAway + new Vector2(0f, -10f), new Vector2(10f, 2f));
            var crate = Make("Crate", FarAway);
            var body = crate.AddComponent<Rigidbody2D>();
            crate.AddComponent<BoxCollider2D>();

            // A trigger stops nothing: after 2.5 s of falling (about 30 units) it is far below the zone.
            yield return new WaitForSeconds(2.5f);
            Assert.That(body.position.y, Is.LessThan(-15f), "it should have fallen through the kill zone and on");
        }
    }
}
