using System;
using System.IO;
using System.Text;
using Game.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor automation tool for procedurally generating authentic retro-arcade horror WAV audio files
    /// and their corresponding AudioCue ScriptableObjects under Assets/_Project/.
    /// </summary>
    public static class AudioAssetGenerator
    {
        const string AudioFolder = "Assets/_Project/Art/Audio";
        const string CuesFolder = "Assets/_Project/ScriptableObjects/Audio";
        const int SampleRate = 44100;


        [InitializeOnLoadMethod]
        static void AutoGenerateIfMissing()
        {
            EnsureDirectories();
            string checkFile = Path.Combine(AudioFolder, "sfx_cell_waka.wav");
            if (!File.Exists(checkFile))
            {
                GenerateAll();
            }
        }

        [MenuItem("Game/Audio/Generate Audio Assets & Cues", priority = 60)]
        public static void GenerateAll()
        {
            EnsureDirectories();

            // 1. Generate procedural 16-bit PCM WAV files
            GenerateWavFiles();

            // Refresh to let Unity import the new WAVs as AudioClips
            AssetDatabase.Refresh();

            // 2. Generate and configure AudioCue ScriptableObjects
            GenerateAudioCues();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA>[Audio Asset Generator]</color> Successfully generated authentic retro-horror audio files and AudioCue assets!");
        }

        static void EnsureDirectories()
        {
            if (!Directory.Exists(AudioFolder)) Directory.CreateDirectory(AudioFolder);
            if (!Directory.Exists(CuesFolder)) Directory.CreateDirectory(CuesFolder);
        }

        static void GenerateWavFiles()
        {
            // 1. Ambient Drone (4s loopable sub-bass rumble)
            WriteWav(AudioFolder + "/ambient_drone.wav", GenerateTone(4.0f, t =>
            {
                float sub = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f;
                float harmonic = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.2f;
                float eerieBeat = Mathf.Sin(2f * Mathf.PI * 57.5f * t) * 0.25f; // 2.5Hz beat frequency
                float lfo = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                return (sub + harmonic + eerieBeat) * lfo * 0.6f;
            }));

            // 2. Scatter/Chase Siren (0.5s loop, rising/falling pitch sweep)
            WriteWav(AudioFolder + "/siren_chase.wav", GenerateTone(0.5f, t =>
            {
                float progress = t / 0.5f;
                float freq = Mathf.Lerp(220f, 440f, Mathf.PingPong(progress * 2f, 1f));
                float phase = 2f * Mathf.PI * freq * t;
                float wave = Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase);
                return wave * 0.45f;
            }));

            // 3. Frightened Siren (0.25s frantic warble)
            WriteWav(AudioFolder + "/siren_frightened.wav", GenerateTone(0.25f, t =>
            {
                float freq = 600f + 150f * Mathf.Sin(2f * Mathf.PI * 12f * t);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            }));

            // 4. Ready Jingle (Arcade start in eerie bell synth)
            WriteWav(AudioFolder + "/jingle_ready.wav", GenerateTone(2.4f, t =>
            {
                float[] notes = { 493.88f, 987.77f, 739.99f, 622.25f, 987.77f, 739.99f, 622.25f };
                float noteLen = 0.3f;
                int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                float localT = t % noteLen;
                float env = Mathf.Exp(-localT * 8f);
                float wave = Mathf.Sin(2f * Mathf.PI * notes[idx] * localT);
                return wave * env * 0.55f;
            }));

            // 5. Game Over Jingle (Descending sad arcade notes)
            WriteWav(AudioFolder + "/jingle_gameover.wav", GenerateTone(2.0f, t =>
            {
                float[] notes = { 261.63f, 207.65f, 174.61f, 130.81f };
                float noteLen = 0.45f;
                int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                float localT = t % noteLen;
                float env = Mathf.Exp(-localT * 4f);
                return Mathf.Sin(2f * Mathf.PI * notes[idx] * localT) * env * 0.6f;
            }));

            // 6. Cell Collect Waka (Crisp retro chomp)
            WriteWav(AudioFolder + "/sfx_cell_waka.wav", GenerateTone(0.12f, t =>
            {
                float progress = t / 0.12f;
                float freq = Mathf.Lerp(340f, 180f, progress);
                float env = Mathf.Sin(progress * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                return wave * env * 0.65f;
            }));

            // 7. Battery Collect (High-tech crystal chime)
            WriteWav(AudioFolder + "/sfx_battery.wav", GenerateTone(0.4f, t =>
            {
                float progress = t / 0.4f;
                float freq = Mathf.Lerp(523.25f, 1046.50f, progress);
                float env = Mathf.Exp(-progress * 5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
                return wave * env * 0.5f;
            }));

            // 8. Flare Ignite (Explosive flame whoosh)
            WriteWav(AudioFolder + "/sfx_flare_ignite.wav", GenerateTone(1.0f, t =>
            {
                float progress = t / 1.0f;
                float env = Mathf.Exp(-progress * 4f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.6f;
                float subBlast = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 40f, progress) * t) * 0.6f;
                return (noise + subBlast) * env * 0.7f;
            }));

            // 9. Ghost Banish (Supernatural electrical discharge)
            WriteWav(AudioFolder + "/sfx_ghost_banish.wav", GenerateTone(0.8f, t =>
            {
                float progress = t / 0.8f;
                float freq = Mathf.Lerp(880f, 180f, progress * progress);
                float env = Mathf.Exp(-progress * 4f);
                float zap = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Sin(2f * Mathf.PI * 45f * t);
                float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                return (zap + hiss) * env * 0.7f;
            }));

            // 10. Player Caught (Horror jumpscare stinger)
            WriteWav(AudioFolder + "/sfx_player_caught.wav", GenerateTone(1.2f, t =>
            {
                float progress = t / 1.2f;
                float env = Mathf.Exp(-progress * 3.5f);
                float dissonance = Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 932.33f * t); // Minor 2nd
                float impact = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f;
                return (dissonance * 0.4f + impact) * env * 0.8f;
            }));

            // 11. Level Complete (Victory arcade flourish)
            WriteWav(AudioFolder + "/sfx_level_complete.wav", GenerateTone(2.0f, t =>
            {
                float[] notes = { 261.63f, 329.63f, 392f, 523.25f, 659.25f, 783.99f, 1046.50f };
                float noteLen = 0.25f;
                int idx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
                float localT = t % noteLen;
                float env = Mathf.Exp(-localT * 3f);
                return Mathf.Sin(2f * Mathf.PI * notes[idx] * localT) * env * 0.6f;
            }));

            // 12. Extra Life (Arcade 2-tone chime)
            WriteWav(AudioFolder + "/sfx_extra_life.wav", GenerateTone(0.35f, t =>
            {
                float progress = t / 0.35f;
                float freq = progress < 0.5f ? 784f : 1046.50f;
                float env = Mathf.Exp(-(t % 0.175f) * 8f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }));
        }

        static float[] GenerateTone(float durationSeconds, Func<float, float> waveFunc)
        {
            int totalSamples = (int)(durationSeconds * SampleRate);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                samples[i] = Mathf.Clamp(waveFunc(t), -1f, 1f);
            }
            return samples;
        }

        static void WriteWav(string path, float[] samples)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int byteRate = SampleRate * 2;
                int dataChunkSize = samples.Length * 2;

                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataChunkSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)1); // Mono
                bw.Write(SampleRate);
                bw.Write(byteRate);
                bw.Write((short)2); // Block align
                bw.Write((short)16); // Bits per sample
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataChunkSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                    bw.Write(val);
                }
            }
        }

        static void GenerateAudioCues()
        {
            CreateOrUpdateCue("AudioCue_AmbientDrone", AudioFolder + "/ambient_drone.wav", loop: true, spatialBlend: 0f, volume: 0.85f);
            CreateOrUpdateCue("AudioCue_ScatterChaseSiren", AudioFolder + "/siren_chase.wav", loop: true, spatialBlend: 0f, volume: 0.7f);
            CreateOrUpdateCue("AudioCue_FrightenedSiren", AudioFolder + "/siren_frightened.wav", loop: true, spatialBlend: 0f, volume: 0.85f);
            CreateOrUpdateCue("AudioCue_ReadyJingle", AudioFolder + "/jingle_ready.wav", loop: false, spatialBlend: 0f, volume: 0.9f);
            CreateOrUpdateCue("AudioCue_GameOver", AudioFolder + "/jingle_gameover.wav", loop: false, spatialBlend: 0f, volume: 0.9f);
            CreateOrUpdateCue("AudioCue_CellCollect", AudioFolder + "/sfx_cell_waka.wav", loop: false, spatialBlend: 0f, volume: 0.8f, pitchMin: 0.95f, pitchMax: 1.05f);
            CreateOrUpdateCue("AudioCue_BatteryCollect", AudioFolder + "/sfx_battery.wav", loop: false, spatialBlend: 0f, volume: 0.9f);
            CreateOrUpdateCue("AudioCue_FlareIgnite", AudioFolder + "/sfx_flare_ignite.wav", loop: false, spatialBlend: 0f, volume: 1.0f);
            CreateOrUpdateCue("AudioCue_GhostBanished", AudioFolder + "/sfx_ghost_banish.wav", loop: false, spatialBlend: 1.0f, volume: 1.0f);
            CreateOrUpdateCue("AudioCue_PlayerCaught", AudioFolder + "/sfx_player_caught.wav", loop: false, spatialBlend: 0f, volume: 1.0f);
            CreateOrUpdateCue("AudioCue_LevelComplete", AudioFolder + "/sfx_level_complete.wav", loop: false, spatialBlend: 0f, volume: 0.9f);
            CreateOrUpdateCue("AudioCue_ExtraLife", AudioFolder + "/sfx_extra_life.wav", loop: false, spatialBlend: 0f, volume: 0.9f);
        }

        static void CreateOrUpdateCue(string cueName, string clipPath, bool loop, float spatialBlend, float volume, float pitchMin = 0.98f, float pitchMax = 1.02f)
        {
            string path = $"{CuesFolder}/{cueName}.asset";
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue == null)
            {
                cue = ScriptableObject.CreateInstance<AudioCue>();
                AssetDatabase.CreateAsset(cue, path);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            var so = new SerializedObject(cue);
            if (clip != null)
            {
                var clipsProp = so.FindProperty("clips");
                clipsProp.arraySize = 1;
                clipsProp.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            }

            so.FindProperty("loop").boolValue = loop;
            so.FindProperty("spatialBlend").floatValue = spatialBlend;
            so.FindProperty("volumeRange").vector2Value = new Vector2(volume * 0.95f, volume);
            so.FindProperty("pitchRange").vector2Value = new Vector2(pitchMin, pitchMax);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cue);
        }
    }
}
