using System;
using System.IO;
using NUnit.Framework;
using Sounds;
using UnityEditor;
using UnityEngine;

// The level's music: when the loop takes over from the intro, how its files are imported, and that
// both joins (intro -> loop, loop -> loop) have no jump in the waveform (a jump is a click).
namespace Tests.EditMode
{
    public class MusicTests
    {
        private const string IntroPath = "Assets/Audio/Music/level_intro.wav";
        private const string LoopPath = "Assets/Audio/Music/level_loop.wav";

        [Test]
        public void Loop_StartsTheSampleTheIntroEnds()
        {
            // 545280 samples at 48 kHz = 11.36 s exactly.
            Assert.AreEqual(10.0 + 11.36, MusicPlayer.GetLoopStart(10.0, 545280, 48000), 1e-9);
        }

        [TestCase(IntroPath)]
        [TestCase(LoopPath)]
        public void MusicClip_IsImportedForStreaming(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.IsNotNull(importer, $"no sound at {path}");
            var settings = importer.defaultSampleSettings;
            Assert.AreEqual(AudioClipLoadType.Streaming, settings.loadType, "music streams from disk: it's too long to hold in memory");
            Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, "compressed: a WAV of music is megabytes");
            Assert.IsFalse(importer.forceToMono, "music stays stereo");
            var so = new SerializedObject(importer);
            Assert.IsFalse(so.FindProperty("m_Normalize").boolValue,
                "not normalized: each clip would get its own gain and the loudness would jump at the join");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.AreEqual(2, clip.channels);
        }

        [Test]
        public void MusicClips_AreAReasonableLength()
        {
            var intro = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroPath);
            var loop = AssetDatabase.LoadAssetAtPath<AudioClip>(LoopPath);
            Assert.That(intro.length, Is.InRange(2f, 20f), "an intro: a few bars");
            Assert.That(loop.length, Is.InRange(20f, 120f), "long enough not to tire, short enough to stay small");
            Assert.AreEqual(intro.frequency, loop.frequency, "one song: one sample rate");
        }

        // Where one clip's last sample meets the next one's first, the step is no bigger than the
        // music's own steps between two samples (4 times their mean, plus a little): no click.
        [TestCase(IntroPath, LoopPath, TestName = "Music: the intro runs into the loop without a click")]
        [TestCase(LoopPath, LoopPath, TestName = "Music: the loop runs into itself without a click")]
        public void Join_HasNoClick(string fromPath, string toPath)
        {
            var from = ReadWav(fromPath);
            var to = ReadWav(toPath);
            for (var c = 0; c < from.Length; c++)
            {
                var a = from[c];
                var typical = 0.0;
                for (var i = 1; i < a.Length; i++)
                {
                    typical += Math.Abs(a[i] - a[i - 1]);
                }

                typical /= a.Length - 1;
                var step = Math.Abs(to[c][0] - a[a.Length - 1]);
                Assert.That(step, Is.LessThanOrEqualTo(4 * typical + 0.01), $"channel {c}: step {step:F4} at the join, typical {typical:F4}");
            }
        }

        // 16-bit PCM WAV, read straight from the file (a streamed clip can't give its samples in the Editor).
        private static float[][] ReadWav(string assetPath)
        {
            var b = File.ReadAllBytes(Path.Combine(Application.dataPath, "..", assetPath));
            int channels = 0, bits = 0, dataAt = -1, dataSize = 0;
            for (var p = 12; p + 8 <= b.Length;)
            {
                var id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                var size = BitConverter.ToInt32(b, p + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, p + 10);
                    bits = BitConverter.ToInt16(b, p + 22);
                }
                else if (id == "data")
                {
                    dataAt = p + 8;
                    dataSize = size;
                    break;
                }

                p += 8 + size + (size & 1);
            }

            Assert.AreEqual(16, bits, $"{assetPath}: 16-bit PCM expected");
            var n = dataSize / (2 * channels);
            var samples = new float[channels][];
            for (var c = 0; c < channels; c++)
            {
                samples[c] = new float[n];
                for (var i = 0; i < n; i++)
                {
                    samples[c][i] = BitConverter.ToInt16(b, dataAt + (i * channels + c) * 2) / 32768f;
                }
            }

            return samples;
        }
    }
}
