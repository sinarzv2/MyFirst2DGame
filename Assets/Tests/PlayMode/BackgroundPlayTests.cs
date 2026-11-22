using System.Collections;
using System.Linq;
using Enemies;
using Level;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

// The background in the Main scene: its layers are drawn behind the level, far ones follow the camera
// more than near ones, and wherever the camera goes (the start, the far end, down in a pit) the sky
// fills the view and every repeating layer reaches past both its edges. The clouds drift by themselves.
namespace Tests.PlayMode
{
    public class BackgroundPlayTests
    {
        private static readonly string[] FarToNear = { "Sky", "Clouds", "Mountains", "Forest" };

        private GameObject _player;
        private Rigidbody2D _body;
        private Tilemap _tilemap;
        private Transform _background;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;

            _player = GameObject.Find("Player");
            _body = _player.GetComponent<Rigidbody2D>();
            _tilemap = Object.FindObjectOfType<Tilemap>();
            _tilemap.CompressBounds();
            var root = GameObject.Find("Background");
            Assert.IsNotNull(root, "no Background in the Main scene: run Tools > Level > Build Background and save the scene");
            _background = root.transform;
            foreach (var enemy in Object.FindObjectsOfType<Enemy>())
            {
                enemy.gameObject.SetActive(false);   // nothing should send the Player back meanwhile
            }
        }

        private Transform Layer(string name)
        {
            var layer = _background.Find(name);
            Assert.IsNotNull(layer, $"no {name} layer under Background");
            return layer;
        }

        private void Teleport(Vector2 position)
        {
            _body.velocity = Vector2.zero;
            _body.position = position;
            _player.transform.position = position;
        }

        [Test]
        public void Layers_AreBehindTheLevel_FarOnesFirst_AndFollowTheCameraMore()
        {
            var tilemapOrder = _tilemap.GetComponent<TilemapRenderer>().sortingOrder;
            var layers = FarToNear.Select(Layer).ToArray();
            for (var i = 0; i < layers.Length; i++)
            {
                var renderer = layers[i].GetComponent<SpriteRenderer>();
                var parallax = layers[i].GetComponent<Parallax>();
                Assert.IsNotNull(parallax, $"{layers[i].name} has no Parallax");
                Assert.That(renderer.sortingOrder, Is.LessThan(tilemapOrder), $"{layers[i].name} is drawn behind the ground");
                if (i == 0)
                {
                    continue;
                }

                var nearer = layers[i];
                var farther = layers[i - 1];
                Assert.That(renderer.sortingOrder, Is.GreaterThan(farther.GetComponent<SpriteRenderer>().sortingOrder),
                    $"{nearer.name} is drawn over {farther.name}");
                Assert.That(parallax.FollowX, Is.LessThan(farther.GetComponent<Parallax>().FollowX),
                    $"{nearer.name} is nearer than {farther.name}: it follows the camera less");
            }
        }

        [UnityTest]
        public IEnumerator Background_FillsTheView_AtTheStart_AtTheFarEnd_AndInAPit()
        {
            var camera = Camera.main;
            yield return new WaitForSeconds(0.3f);
            AssertFilled(camera, "at the start");

            var level = _tilemap.localBounds;
            Teleport(new Vector2(level.max.x - 1f, level.max.y + 1f));
            yield return new WaitForSeconds(2.5f);
            AssertFilled(camera, "at the far end");

            // Held just above the kill zone, beside the level's left end: the camera as low as it goes.
            var zone = Object.FindObjectOfType<KillZone>().GetComponent<Collider2D>().bounds;
            _player.GetComponent<PlayerMovement>().enabled = false;
            _body.gravityScale = 0f;
            Teleport(new Vector2(level.min.x - 3f, zone.max.y + 1f));
            yield return new WaitForSeconds(2.5f);
            AssertFilled(camera, "with the Player down in a pit");
            var forest = Layer("Forest").GetComponent<SpriteRenderer>().bounds;
            Assert.That(forest.min.y, Is.LessThanOrEqualTo(ViewBottom(camera)),
                "down in a pit the forest still reaches below the view: no band of sky under it");
        }

        [UnityTest]
        public IEnumerator Clouds_DriftByThemselves()
        {
            yield return new WaitForSeconds(1f);   // the camera settled at the start
            var clouds = Layer("Clouds");
            var cameraAt = Camera.main.transform.position;
            var from = clouds.position.x;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(cameraAt, Camera.main.transform.position, "the camera should stand still meanwhile");
            // Leaving out a jump of a whole strip, should the repeat have happened meanwhile.
            var strip = clouds.GetComponent<Parallax>().StripWidth;
            var moved = Mathf.Repeat(clouds.position.x - from + strip / 2f, strip) - strip / 2f;
            Assert.That(moved, Is.InRange(0.05f, 0.5f), "the clouds should drift slowly to the right");
        }

        private static float ViewBottom(Camera camera) => camera.transform.position.y - camera.orthographicSize;

        // The sky covers the whole view; each repeating layer reaches past its left and right edges.
        private void AssertFilled(Camera camera, string when)
        {
            var centre = camera.transform.position;
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            var sky = Layer("Sky").GetComponent<SpriteRenderer>().bounds;
            Assert.That(sky.min.x, Is.LessThanOrEqualTo(centre.x - halfWidth), $"sky's left {when}");
            Assert.That(sky.max.x, Is.GreaterThanOrEqualTo(centre.x + halfWidth), $"sky's right {when}");
            Assert.That(sky.min.y, Is.LessThanOrEqualTo(centre.y - halfHeight), $"sky's bottom {when}");
            Assert.That(sky.max.y, Is.GreaterThanOrEqualTo(centre.y + halfHeight), $"sky's top {when}");
            foreach (var name in FarToNear.Skip(1))
            {
                var bounds = Layer(name).GetComponent<SpriteRenderer>().bounds;
                Assert.That(bounds.min.x, Is.LessThanOrEqualTo(centre.x - halfWidth), $"{name} reaches past the view's left {when}");
                Assert.That(bounds.max.x, Is.GreaterThanOrEqualTo(centre.x + halfWidth), $"{name} reaches past the view's right {when}");
            }
        }
    }
}
