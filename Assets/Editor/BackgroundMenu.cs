using Level;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Adds Tools > Level > Build Background: the sky, the clouds, the mountains and the forest
// behind everything, each a Parallax layer placed for the camera at the Player's start. Run it again to
// rebuild them (the old Background is replaced). One Undo step.
namespace LevelTools
{
    public static class BackgroundMenu
    {
        public const string Folder = "Assets/Sprites/Background/";
        public const string RootName = "Background";

        // Far to near: name, follow (share of the camera's movement), sorting order (behind the
        // Tilemap's 0), foot height above the Player's soles, drift (units a second).
        public static readonly Layer[] Layers =
        {
            new Layer("Sky", 1f, -100, 0f, 0f, false),
            new Layer("Clouds", 0.9f, -90, 0.5f, 0.15f, true),
            new Layer("Mountains", 0.8f, -80, -2.5f, 0f, true),
            new Layer("Forest", 0.5f, -70, -6f, 0f, true),
        };

        // The sky's gradient is 4 x 256 at 25.6 pixels a unit (10 units tall): stretched to cover any
        // screen shape (50 units wide) and a little more than the view's height.
        private static readonly Vector3 SkyScale = new Vector3(320f, 1.3f, 1f);

        // A repeating layer is drawn three strips wide: the camera stays within half a strip of the
        // middle one, so a strip and a half shows on each side of it.
        public const int Strips = 3;

        private const string Title = "Background";

        public readonly struct Layer
        {
            public readonly string Name;
            public readonly float Follow;
            public readonly int Order;
            public readonly float FootAboveSoles;
            public readonly float Drift;
            public readonly bool Repeat;

            public Layer(string name, float follow, int order, float footAboveSoles, float drift, bool repeat)
            {
                Name = name;
                Follow = follow;
                Order = order;
                FootAboveSoles = footAboveSoles;
                Drift = drift;
                Repeat = repeat;
            }
        }

        [MenuItem("Tools/Level/Build Background")]
        private static void Build()
        {
            var camera = Camera.main;
            var player = Object.FindObjectOfType<PlayerRespawn>();
            if (camera == null || player == null)
            {
                Fail("The open scene needs the Main Camera and the Player (with PlayerRespawn).");
                return;
            }

            var group = Undo.GetCurrentGroup();
            var old = GameObject.Find(RootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, Title);
            var soles = player.transform.position;
            var view = camera.transform.position;
            foreach (var layer in Layers)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + layer.Name + ".png");
                if (sprite == null)
                {
                    Undo.CollapseUndoOperations(group);
                    Undo.PerformUndo();
                    Fail($"Missing sprite: {Folder}{layer.Name}.png");
                    return;
                }

                var go = new GameObject(layer.Name);
                Undo.RegisterCreatedObjectUndo(go, Title);
                go.transform.SetParent(root.transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = layer.Order;
                if (layer.Repeat)
                {
                    // Tiled: the sprite repeated across the size, not stretched.
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.tileMode = SpriteTileMode.Continuous;
                    renderer.size = new Vector2(sprite.rect.width * Strips, sprite.rect.height) / sprite.pixelsPerUnit;
                    go.transform.position = new Vector3(soles.x, soles.y + layer.FootAboveSoles, 0f);
                }
                else
                {
                    go.transform.position = new Vector3(view.x, view.y, 0f);
                    go.transform.localScale = SkyScale;
                }

                var parallax = go.AddComponent<Parallax>();
                var serialized = new SerializedObject(parallax);
                serialized.FindProperty("followedCamera").objectReferenceValue = camera.transform;
                serialized.FindProperty("followX").floatValue = layer.Follow;
                serialized.FindProperty("followY").floatValue = layer.Follow;
                serialized.FindProperty("repeat").boolValue = layer.Repeat;
                serialized.FindProperty("driftSpeed").floatValue = layer.Drift;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Where nothing is drawn (never, if all is well), the camera clears to the sky's horizon
            // colour (the bottom of Sky.png) instead of Unity's default dark blue.
            Undo.RecordObject(camera, Title);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(200, 232, 250, 255);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"Background: built {Layers.Length} layers. Save the scene to keep them.");
        }

        private static void Fail(string message)
        {
            Debug.LogError("Background: " + message);
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
