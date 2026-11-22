using Hud;
using Level;
using Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Editor-only. Adds Tools > Hud > Build Hearts And Lives: puts the hearts, the lives and "GAME OVER"
// on the open scene's Canvas, copied from the coin icon and the coin counter (so they look alike),
// gives the Player its PlayerHealth and adds the LevelRestart, all wired up. Run it again to rebuild
// them (the old ones are replaced). One Undo step.
namespace LevelTools
{
    public static class HudMenu
    {
        public const string HeartFullPath = "Assets/Sprites/Hud/heart_full.png";
        public const string HeartEmptyPath = "Assets/Sprites/Hud/heart_empty.png";
        public const string HeroHeadPath = "Assets/Sprites/Hud/hero_head.png";

        // What it makes, by name (and finds again, to replace).
        private const string HeartsName = "Hearts", LivesIconName = "LivesIcon", LivesCounterName = "LivesCounter";
        private const string GameOverName = "GameOver", RestartName = "LevelRestart";

        // HUD layout, in the Canvas's reference pixels (1920 x 1080), like the coin row at the top left
        // (icon 50 x 50 at 40, -30). The hearts go under it; the lives at the top right.
        private const float Icon = 50f, HeartStep = 55f;
        private static readonly Vector2 HeartsAt = new Vector2(40f, -95f);
        private static readonly Vector2 LivesIconAt = new Vector2(-150f, -30f), LivesCounterAt = new Vector2(-40f, -30f);

        private const string Title = "Hud";

        [MenuItem("Tools/Hud/Build Hearts And Lives")]
        private static void Build()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            var respawn = Object.FindObjectOfType<PlayerRespawn>();
            var coinIcon = canvas != null ? canvas.transform.Find("CoinIcon") : null;
            var coinCounter = canvas != null ? canvas.transform.Find("CoinCounter") : null;
            if (canvas == null || respawn == null || coinIcon == null || coinCounter == null)
            {
                Fail("The open scene needs a Canvas with CoinIcon and CoinCounter on it, and the Player (with PlayerRespawn).");
                return;
            }

            var full = LoadIcon(HeartFullPath);
            var empty = LoadIcon(HeartEmptyPath);
            var head = LoadIcon(HeroHeadPath);
            if (full == null || empty == null || head == null)
            {
                Fail($"Missing icons: {HeartFullPath}, {HeartEmptyPath} and {HeroHeadPath} must all be there.");
                return;
            }

            var group = Undo.GetCurrentGroup();
            var player = respawn.gameObject;
            var health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = Undo.AddComponent<PlayerHealth>(player);
            }

            foreach (var name in new[] { HeartsName, LivesIconName, LivesCounterName, GameOverName })
            {
                var old = canvas.transform.Find(name);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
            }

            var oldRestart = Object.FindObjectOfType<LevelRestart>();
            if (oldRestart != null)
            {
                Undo.DestroyObjectImmediate(oldRestart.gameObject);
            }

            // The hearts: a row of copies of the coin icon, under the coins.
            var row = new GameObject(HeartsName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(row, Title);
            row.transform.SetParent(canvas.transform, false);
            Place((RectTransform)row.transform, new Vector2(0f, 1f), HeartsAt, new Vector2(HeartStep * health.HeartsPerLife, Icon));
            var images = new Image[health.HeartsPerLife];
            for (var i = 0; i < images.Length; i++)
            {
                var heart = Copy(coinIcon.gameObject, row.transform, $"Heart {i + 1}");
                Place((RectTransform)heart.transform, new Vector2(0f, 1f), new Vector2(i * HeartStep, 0f), new Vector2(Icon, Icon));
                images[i] = heart.GetComponent<Image>();
                images[i].sprite = full;
                images[i].preserveAspect = true;
            }

            var hearts = Undo.AddComponent<HeartsView>(row);
            Set(hearts, "health", health);
            Set(hearts, "full", full);
            Set(hearts, "empty", empty);
            var list = new SerializedObject(hearts);
            var array = list.FindProperty("hearts");
            array.arraySize = images.Length;
            for (var i = 0; i < images.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }

            list.ApplyModifiedProperties();

            // The lives: the hero's head and "× 3", at the top right.
            var livesIcon = Copy(coinIcon.gameObject, canvas.transform, LivesIconName);
            Place((RectTransform)livesIcon.transform, Vector2.one, LivesIconAt, new Vector2(Icon, Icon));
            livesIcon.GetComponent<Image>().sprite = head;
            livesIcon.GetComponent<Image>().preserveAspect = true;

            var livesCounter = CopyText(coinCounter.gameObject, canvas.transform, LivesCounterName);
            Place((RectTransform)livesCounter.transform, Vector2.one, LivesCounterAt, new Vector2(100f, Icon));
            var lives = Undo.AddComponent<LivesView>(livesCounter);
            Set(lives, "health", health);
            Set(lives, "label", livesCounter.GetComponent<TMP_Text>());

            // "GAME OVER" in the middle, hidden until then.
            var gameOver = CopyText(coinCounter.gameObject, canvas.transform, GameOverName);
            Place((RectTransform)gameOver.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 260f));
            var text = gameOver.GetComponent<TMP_Text>();
            text.text = "GAME OVER";
            text.fontSize = 160f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.enabled = false;
            var view = Undo.AddComponent<GameOverView>(gameOver);
            Set(view, "health", health);
            Set(view, "label", text);

            // The restart after a game over: not drawn, so not on the Canvas.
            var restartObject = new GameObject(RestartName);
            Undo.RegisterCreatedObjectUndo(restartObject, Title);
            var restart = Undo.AddComponent<LevelRestart>(restartObject);
            Set(restart, "health", health);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log($"Hud: built {images.Length} hearts, the lives and GAME OVER, and the LevelRestart. Save the scene to keep them.");
        }

        // The icon as a single Sprite, drawn as is (no compression, no mipmaps: a HUD icon is shown
        // at one size, sharp).
        private static Sprite LoadIcon(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                 importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject Copy(GameObject original, Transform parent, string name)
        {
            var copy = Object.Instantiate(original, parent, false);
            copy.name = name;
            Undo.RegisterCreatedObjectUndo(copy, Title);
            return copy;
        }

        // A copy of the coin counter's text without its CoinCounterView (it would show the coins).
        private static GameObject CopyText(GameObject original, Transform parent, string name)
        {
            var copy = Copy(original, parent, name);
            foreach (var counter in copy.GetComponents<CoinCounterView>())
            {
                Object.DestroyImmediate(counter);
            }

            return copy;
        }

        // Anchored at `anchor` of the Canvas (0,1 = top left, 1,1 = top right), by its same corner.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // A private [SerializeField] set from outside, as the Inspector does it.
        private static void Set(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void Fail(string message)
        {
            Debug.LogError("Hud: " + message);
            EditorUtility.DisplayDialog(Title, message, "OK");
        }
    }
}
