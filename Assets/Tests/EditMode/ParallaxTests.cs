using System.IO;
using Level;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The parallax rules (how far a layer moves with the camera, how a strip repeats) and the background
// pictures: imported as repeating sprites, and really seamless where the strip's ends meet.
namespace Tests.EditMode
{
    public class ParallaxTests
    {
        private const string Folder = "Assets/Sprites/Background/";

        [TestCase(0f, ExpectedResult = 2f, TestName = "Parallax: follow 0 stays in the world")]
        [TestCase(1f, ExpectedResult = 12f, TestName = "Parallax: follow 1 moves with the camera")]
        [TestCase(0.5f, ExpectedResult = 7f, TestName = "Parallax: follow 0.5 moves half as far")]
        public float Layer_MovesByItsShareOfTheCamerasMovement(float follow)
        {
            // The camera moves 10 to the right; the layer started at 2.
            return Parallax.GetLayerX(10f, 0f, 2f, follow, 0f);
        }

        [Test]
        public void Layer_MovesUpAndDownByItsShare()
        {
            Assert.AreEqual(-2f + 4f * 0.8f, Parallax.GetLayerY(3f, -1f, -2f, 0.8f), 1e-5f);
        }

        // However far the camera goes, the strip's middle copy stays within half a strip of it, and only
        // whole strips are added (so the picture on screen is the same as without repeating).
        [TestCase(0f)]
        [TestCase(7.9f)]
        [TestCase(-30f)]
        [TestCase(250f)]
        public void RepeatingLayer_StaysWithinHalfAStripOfTheCamera(float cameraX)
        {
            const float strip = 16.7f, follow = 0.5f, start = 1f;
            var x = Parallax.GetLayerX(cameraX, 0f, start, follow, strip);
            var unwrapped = Parallax.GetLayerX(cameraX, 0f, start, follow, 0f);
            Assert.That(Mathf.Abs(x - cameraX), Is.LessThanOrEqualTo(strip / 2f + 1e-4f), "within half a strip of the camera");
            var strips = (x - unwrapped) / strip;
            Assert.AreEqual(Mathf.Round(strips), strips, 1e-4f, "moved by whole strips only");
        }

        [TestCase("Clouds", 120f, true)]
        [TestCase("Mountains", 80f, true)]
        [TestCase("Forest", 80f, true)]
        [TestCase("Sky", 25.6f, false)]
        public void BackgroundSprite_IsImportedForItsLayer(string name, float ppu, bool repeats)
        {
            var importer = AssetImporter.GetAtPath(Folder + name + ".png") as TextureImporter;
            Assert.IsNotNull(importer, $"no picture at {Folder}{name}.png");
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(ppu, importer.spritePixelsPerUnit, 1e-4f, "pixels per unit (the layer's size on screen)");
            Assert.IsFalse(importer.mipmapEnabled, "no mipmaps: a sprite is drawn at one size");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, "uncompressed: gradients band when compressed");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.AreEqual(SpriteMeshType.FullRect, settings.spriteMeshType, "Full Rect: a Tiled SpriteRenderer needs it");
            if (repeats)
            {
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU,
                    "wraps across: filtering at one end reads the other, which continues it");
                Assert.AreEqual(new Vector2(0.5f, 0f), settings.spritePivot, "pivot at the foot, in the middle");
            }
        }

        // Where the strip's last column meets its first (the repeat), the step is no bigger than between
        // two neighbouring columns anywhere else in it.
        [TestCase("Clouds")]
        [TestCase("Mountains")]
        [TestCase("Forest")]
        public void RepeatingStrip_IsSeamless(string name)
        {
            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath, "..", Folder + name + ".png"))));
            var pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height;
            float Step(int a, int b)
            {
                var sum = 0f;
                for (var y = 0; y < h; y++)
                {
                    Color32 p = pixels[y * w + a], q = pixels[y * w + b];
                    sum += Mathf.Abs(p.r * p.a - q.r * q.a) + Mathf.Abs(p.g * p.a - q.g * q.a) + Mathf.Abs(p.b * p.a - q.b * q.a);
                }

                return sum / (h * 255f);
            }

            var typical = 0f;
            for (var x = 0; x < w - 1; x++)
            {
                typical += Step(x, x + 1);
            }

            typical /= w - 1;
            Object.DestroyImmediate(texture);
            Assert.That(Step(w - 1, 0), Is.LessThanOrEqualTo(3f * typical + 1f),
                $"{name}: the step where the strip repeats should look like any other (typical {typical:F2})");
        }
    }
}
