using Game.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class AudioCueTests
    {
        [Test]
        public void AudioCueSelectsClipAndAvoidsImmediateRepeat()
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            var clipA = AudioClip.Create("TestA", 44100, 1, 44100, false);
            var clipB = AudioClip.Create("TestB", 44100, 1, 44100, false);

            // Use reflection to populate private serialized clips field
            var field = typeof(AudioCue).GetField("clips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(cue, new AudioClip[] { clipA, clipB });

            AudioClip first = cue.GetRandomClip();
            Assert.That(first, Is.Not.Null);

            // Second selection should alternate if 2 clips exist
            AudioClip second = cue.GetRandomClip();
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.EqualTo(first));

            Object.DestroyImmediate(clipA);
            Object.DestroyImmediate(clipB);
            Object.DestroyImmediate(cue);
        }

        [Test]
        public void AudioCueConfiguresAudioSourceProperties()
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            var clip = AudioClip.Create("TestClip", 44100, 1, 44100, false);

            var clipsField = typeof(AudioCue).GetField("clips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clipsField.SetValue(cue, new AudioClip[] { clip });

            var volumeField = typeof(AudioCue).GetField("volumeRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            volumeField.SetValue(cue, new Vector2(0.5f, 0.5f));

            var pitchField = typeof(AudioCue).GetField("pitchRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pitchField.SetValue(cue, new Vector2(1.2f, 1.2f));

            var go = new GameObject("TestAudioSource");
            var source = go.AddComponent<AudioSource>();

            cue.Play(source);

            Assert.That(source.clip, Is.EqualTo(clip));
            Assert.That(source.volume, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(source.pitch, Is.EqualTo(1.2f).Within(0.01f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(cue);
        }
    }
}
