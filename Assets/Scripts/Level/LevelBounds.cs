using Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Level
{
    // Fits everything that marks the level's limits to the tiles that are painted: the walls at both
    // ends, the kill zone under it all, and the camera's bounds. The level's shape is defined in one
    // place only, the Tilemap; a longer level needs only more tiles.
    // Runs when the scene starts. In the Editor the component's menu (the three dots) has
    // "Fit To Level", to see the result in the Scene view, and save it, before playing.
    public class LevelBounds : MonoBehaviour
    {
        [SerializeField] private Tilemap level;
        [SerializeField] private BoxCollider2D wallLeft;
        [SerializeField] private BoxCollider2D wallRight;
        [SerializeField] private BoxCollider2D killZone;
        [SerializeField] private PolygonCollider2D cameraBounds;
        [SerializeField] private CinemachineConfiner2D confiner;   // told when its shape changes

        [SerializeField, Min(0.1f)] private float wallThickness = 1f;
        // How far the camera may show below the lowest grass and above the highest.
        [SerializeField] private float cameraBelowGround = 7f;
        [SerializeField] private float cameraAboveGround = 12f;
        // The kill zone's top, this far under the lowest tile; it reaches this far past each end.
        [SerializeField] private float killZoneBelowTiles = 3f;
        [SerializeField] private float killZoneMargin = 6f;

        // Where each limit goes, in world units. Worked out from numbers only, so an Edit Mode test
        // can check it without a scene.
        public readonly struct Layout
        {
            public readonly Rect WallLeft, WallRight, KillZone, Camera;

            public Layout(Rect wallLeft, Rect wallRight, Rect killZone, Rect camera)
            {
                WallLeft = wallLeft;
                WallRight = wallRight;
                KillZone = killZone;
                Camera = camera;
            }
        }

        // tiles: the box around every painted tile. lowestGround / highestGround: the lowest and the
        // highest grass surface (the top of a column of tiles).
        public static Layout Compute(Bounds tiles, float lowestGround, float highestGround, float wallThickness,
            float cameraBelowGround, float cameraAboveGround, float killZoneBelowTiles, float killZoneMargin)
        {
            var camera = Rect.MinMaxRect(tiles.min.x, lowestGround - cameraBelowGround, tiles.max.x, highestGround + cameraAboveGround);
            var killTop = tiles.min.y - killZoneBelowTiles;
            var killZone = Rect.MinMaxRect(tiles.min.x - killZoneMargin, killTop - 2f, tiles.max.x + killZoneMargin, killTop);
            // The walls stand from the kill zone up to well above what the camera can show.
            var wallTop = camera.yMax + 5f;
            var wallLeft = Rect.MinMaxRect(tiles.min.x - wallThickness, killTop, tiles.min.x, wallTop);
            var wallRight = Rect.MinMaxRect(tiles.max.x, killTop, tiles.max.x + wallThickness, wallTop);
            return new Layout(wallLeft, wallRight, killZone, camera);
        }

        private void Awake()
        {
            Fit();
        }

        // Public so the level can be refitted after tiles change while playing (and for tests).
        public void Fit()
        {
            if (level == null || wallLeft == null || wallRight == null || killZone == null || cameraBounds == null)
            {
                Debug.LogError("LevelBounds: set Level, Wall Left, Wall Right, Kill Zone and Camera Bounds in the Inspector", this);
                return;
            }

            level.CompressBounds();   // leave out cells whose tiles were erased
            var local = level.localBounds;
            var tiles = new Bounds(level.transform.TransformPoint(local.center), local.size);
            GroundRange(out var lowest, out var highest);
            if (lowest > highest)
            {
                Debug.LogError("LevelBounds: the Level Tilemap has no tiles to fit to", this);
                return;
            }

            var layout = Compute(tiles, lowest, highest, wallThickness, cameraBelowGround, cameraAboveGround, killZoneBelowTiles, killZoneMargin);

            Place(wallLeft, layout.WallLeft);
            Place(wallRight, layout.WallRight);
            Place(killZone, layout.KillZone);

            // The polygon's points are in its own object's space.
            var c = layout.Camera;
            var t = cameraBounds.transform;
            cameraBounds.offset = Vector2.zero;
            cameraBounds.points = new Vector2[]
            {
                t.InverseTransformPoint(new Vector2(c.xMin, c.yMin)),
                t.InverseTransformPoint(new Vector2(c.xMax, c.yMin)),
                t.InverseTransformPoint(new Vector2(c.xMax, c.yMax)),
                t.InverseTransformPoint(new Vector2(c.xMin, c.yMax)),
            };
            if (confiner != null)
            {
                confiner.InvalidateCache();   // it keeps a copy of the shape: make it read the new one
            }
        }

        // The lowest and the highest grass: the top of each column of tiles.
        private void GroundRange(out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;
            var cells = level.cellBounds;
            for (var x = cells.xMin; x < cells.xMax; x++)
            {
                for (var y = cells.yMax - 1; y >= cells.yMin; y--)
                {
                    if (!level.HasTile(new Vector3Int(x, y, 0)))
                    {
                        continue;
                    }

                    var surface = level.CellToWorld(new Vector3Int(x, y + 1, 0)).y;
                    lowest = Mathf.Min(lowest, surface);
                    highest = Mathf.Max(highest, surface);
                    break;
                }
            }
        }

        private static void Place(BoxCollider2D box, Rect rect)
        {
            box.transform.position = rect.center;
            box.offset = Vector2.zero;
            box.size = rect.size;
        }

#if UNITY_EDITOR
        // Undo.RecordObjects first: the change can be undone, and Unity knows the scene changed.
        [ContextMenu("Fit To Level")]
        private void FitInEditor()
        {
            if (level == null || wallLeft == null || wallRight == null || killZone == null || cameraBounds == null)
            {
                Fit();   // logs which fields to set (the list below would fail on an empty one first)
                return;
            }

            UnityEditor.Undo.RecordObjects(new Object[]
            {
                level, wallLeft, wallLeft.transform, wallRight, wallRight.transform,
                killZone, killZone.transform, cameraBounds,
            }, "Fit To Level");
            Fit();
        }
#endif
    }
}
