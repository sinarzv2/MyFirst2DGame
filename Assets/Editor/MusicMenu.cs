using Sounds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Adds Tools > Level > Build Music: a Music object with a MusicPlayer, given the level's
// intro and loop. Run it again to rebuild it (the old Music is replaced). One Undo step.
namespace LevelTools
{
    public static class MusicMenu
    {
        public const string IntroPath = "Assets/Audio/Music/level_intro.wav";
        public const string LoopPath = "Assets/Audio/Music/level_loop.wav";
        public const string ObjectName = "Music";

        private const string Title = "Music";

        [MenuItem("Tools/Level/Build Music")]
        private static void Build()
        {
            var intro = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroPath);
            var loop = AssetDatabase.LoadAssetAtPath<AudioClip>(LoopPath);
            if (intro == null || loop == null)
            {
                Debug.LogError($"Music: missing {IntroPath} or {LoopPath}");
                EditorUtility.DisplayDialog(Title, $"Missing {IntroPath} or {LoopPath}.", "OK");
                return;
            }

            var group = Undo.GetCurrentGroup();
            var old = GameObject.Find(ObjectName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, Title);
            var player = go.AddComponent<MusicPlayer>();
            var serialized = new SerializedObject(player);
            serialized.FindProperty("intro").objectReferenceValue = intro;
            serialized.FindProperty("loop").objectReferenceValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("Music: built. Save the scene to keep it.");
        }
    }
}
