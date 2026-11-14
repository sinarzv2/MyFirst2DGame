using Level;
using NUnit.Framework;
using UnityEngine;

// LevelBounds.Compute alone: where the walls, the kill zone and the camera bounds go for a given
// box of tiles. No scene needed (as for PlayerMovement's rules, chapter 15).
namespace Tests.EditMode
{
    public class LevelBoundsTests
    {
        // The level as first drawn: tiles from x -8 to 20 and y -16 to 0, grass between -2 and 0.
        private static readonly Bounds Tiles = new Bounds(new Vector3(6f, -8f), new Vector3(28f, 16f));

        private static LevelBounds.Layout Fit(Bounds tiles) =>
            LevelBounds.Compute(tiles, -2f, 0f, wallThickness: 1f, cameraBelowGround: 7f, cameraAboveGround: 12f, killZoneBelowTiles: 3f, killZoneMargin: 6f);

        [Test]
        public void Walls_StandOutsideBothEnds_TouchingThem()
        {
            var layout = Fit(Tiles);
            Assert.AreEqual(-8f, layout.WallLeft.xMax, 1e-4f, "the left wall's inner side = the level's left end");
            Assert.AreEqual(1f, layout.WallLeft.width, 1e-4f, "wall thickness");
            Assert.AreEqual(20f, layout.WallRight.xMin, 1e-4f, "the right wall's inner side = the level's right end");
            Assert.That(layout.WallLeft.yMax, Is.GreaterThan(layout.Camera.yMax), "the walls reach above what the camera shows");
        }

        [Test]
        public void KillZone_LiesUnderEveryTile_AndReachesPastBothEnds()
        {
            var layout = Fit(Tiles);
            Assert.AreEqual(-19f, layout.KillZone.yMax, 1e-4f, "top = 3 under the lowest tile (-16)");
            Assert.AreEqual(-14f, layout.KillZone.xMin, 1e-4f, "left = 6 past the left end");
            Assert.AreEqual(26f, layout.KillZone.xMax, 1e-4f, "right = 6 past the right end");
            Assert.AreEqual(layout.KillZone.yMax, layout.WallLeft.yMin, 1e-4f, "the walls stand on the kill zone: nothing gets round them");
        }

        [Test]
        public void CameraBounds_SpanTheLevel_FromBelowTheLowestGroundToAboveTheHighest()
        {
            var camera = Fit(Tiles).Camera;
            Assert.AreEqual(Rect.MinMaxRect(-8f, -9f, 20f, 12f), camera, "the bounds the level was first drawn with");
        }

        [Test]
        public void ALongerLevel_MovesTheRightLimits_AndNothingElse()
        {
            var longer = new Bounds();
            longer.SetMinMax(Tiles.min, Tiles.max + new Vector3(40f, 0f));   // 20 more cells to the right
            var before = Fit(Tiles);
            var after = Fit(longer);

            Assert.AreEqual(60f, after.WallRight.xMin, 1e-4f, "right wall");
            Assert.AreEqual(60f, after.Camera.xMax, 1e-4f, "camera bounds' right side");
            Assert.AreEqual(66f, after.KillZone.xMax, 1e-4f, "kill zone's right end");
            Assert.AreEqual(before.WallLeft, after.WallLeft, "the left wall stays");
            Assert.AreEqual(before.Camera.xMin, after.Camera.xMin, 1e-4f, "the camera bounds' left side stays");
        }
    }
}
