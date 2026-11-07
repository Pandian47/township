using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TownBuilder.EditorTools
{
    /// <summary>
    /// Synthesises ORIGINAL placeholder sound effects and a music loop as WAV files (no third-party audio).
    /// Replace any PH_*.wav with a real sound of the same name to upgrade it; existing files are never overwritten.
    /// </summary>
    public static class PlaceholderAudioGenerator
    {
        public const string AudioFolder = "Assets/_Game/Audio";
        public const string LibraryPath = "Assets/_Game/ScriptableObjects/SoundLibrary.asset";
        private const int SfxRate = 44100;
        private const int MusicRate = 22050;

        public static SoundLibrary CreateLibrary()
        {
            PlaceholderArtGenerator.EnsureFolder(AudioFolder);

            var entries = new List<SoundEntry>
            {
                Entry(SoundId.UiTap, "PH_Sfx_ui_tap", UiTap, 0.6f),
                Entry(SoundId.Place, "PH_Sfx_place", Place, 0.9f),
                Entry(SoundId.Remove, "PH_Sfx_remove", Remove, 0.8f),
                Entry(SoundId.BuildComplete, "PH_Sfx_build_complete", BuildComplete, 0.8f),
                Entry(SoundId.Plant, "PH_Sfx_plant", Plant, 0.8f),
                Entry(SoundId.Harvest, "PH_Sfx_harvest", Harvest, 0.8f),
                Entry(SoundId.Collect, "PH_Sfx_collect", Collect, 0.8f),
                Entry(SoundId.Coins, "PH_Sfx_coins", Coins, 0.7f),
                Entry(SoundId.LevelUp, "PH_Sfx_level_up", LevelUp, 0.9f),
                Entry(SoundId.Error, "PH_Sfx_error", Error, 0.6f),
            };
            var music = CreateClip("PH_Music_town_loop", MusicLoop, MusicRate, isMusic: true);

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                library.EditorConfigure(entries, music);
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            return library;
        }

        private static SoundEntry Entry(SoundId id, string name, Func<float[]> synth, float volume) =>
            new SoundEntry { id = id, clip = CreateClip(name, synth, SfxRate, isMusic: false), volume = volume };

        // ------------------------------------------------------------------ Sounds (all procedurally generated)

        private static float[] UiTap()
        {
            var b = Buffer(0.035f, SfxRate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                b[i] = Mathf.Sin(2 * Mathf.PI * 1400 * t) * Mathf.Exp(-t * 140f) * 0.6f;
            }

            return b;
        }

        private static float[] Place()
        {
            var b = Buffer(0.22f, SfxRate);
            var rng = new System.Random(1);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                float freq = Mathf.Lerp(190f, 80f, t / 0.22f);
                phase += 2 * Mathf.PI * freq / SfxRate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);
                float knock = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 90f) * 0.5f;
                b[i] = (body + knock) * 0.8f;
            }

            return b;
        }

        private static float[] Remove()
        {
            var b = Buffer(0.25f, SfxRate);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                phase += 2 * Mathf.PI * Mathf.Lerp(500f, 150f, t / 0.25f) / SfxRate;
                b[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 10f) * 0.5f;
            }

            return b;
        }

        private static float[] BuildComplete() => Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.35f);

        private static float[] Plant()
        {
            var b = Buffer(0.12f, SfxRate);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                phase += 2 * Mathf.PI * Mathf.Lerp(380f, 820f, t / 0.12f) / SfxRate;
                b[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t / 0.12f) * 0.6f;
            }

            return b;
        }

        private static float[] Harvest()
        {
            var chirp = Arpeggio(new[] { 783.99f, 1046.5f }, 0.07f, 0.12f);
            var rng = new System.Random(7);
            for (int i = 0; i < chirp.Length; i++)
            {
                float t = i / (float)SfxRate;
                chirp[i] += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 25f) * 0.15f; // leafy rustle
            }

            return chirp;
        }

        private static float[] Collect()
        {
            var b = Buffer(0.45f, SfxRate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                float bell = Mathf.Sin(2 * Mathf.PI * 880 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 1320 * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * 2640 * t);
                b[i] = bell * Mathf.Exp(-t * 7f) * 0.35f;
            }

            return b;
        }

        private static float[] Coins() => Arpeggio(new[] { 1567.98f, 2093.0f }, 0.06f, 0.18f);

        private static float[] LevelUp()
        {
            // C major fanfare ending on a held chord.
            var notes = Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.1f, 0.2f);
            var chord = Buffer(0.6f, SfxRate);
            for (int i = 0; i < chord.Length; i++)
            {
                float t = i / (float)SfxRate;
                float env = Mathf.Min(1f, t * 30f) * Mathf.Exp(-t * 3.5f);
                chord[i] = (Tone(523.25f, t) + Tone(659.25f, t) + Tone(783.99f, t) + Tone(1046.5f, t)) * 0.16f * env;
            }

            var result = new float[notes.Length + chord.Length];
            Array.Copy(notes, result, notes.Length);
            Array.Copy(chord, 0, result, notes.Length, chord.Length);
            return result;
        }

        private static float[] Error()
        {
            var b = Buffer(0.32f, SfxRate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                bool on = t < 0.12f || (t > 0.17f && t < 0.29f);
                float square = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 196 * t));
                b[i] = on ? square * 0.18f * (1f - Mathf.Repeat(t, 0.17f) * 2f) : 0f;
            }

            return b;
        }

        /// <summary>Gentle 8-bar loop: soft chord pad (C - G - Am - F, twice) and a simple plucked melody.</summary>
        private static float[] MusicLoop()
        {
            const float bpm = 100f;
            float beat = 60f / bpm;
            int bars = 8;
            float length = bars * 4 * beat;
            var b = Buffer(length, MusicRate);

            var chords = new[]
            {
                new[] { 261.63f, 329.63f, 392.00f }, // C
                new[] { 246.94f, 293.66f, 392.00f }, // G
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 220.00f, 261.63f, 349.23f }, // F
            };
            // Melody: one note per beat (0 = rest), cheerful pentatonic phrases.
            var melody = new[]
            {
                659.25f, 783.99f, 880.00f, 783.99f,  587.33f, 659.25f, 783.99f, 0f,
                659.25f, 523.25f, 587.33f, 659.25f,  523.25f, 587.33f, 523.25f, 0f,
                783.99f, 880.00f, 1046.5f, 880.00f,  783.99f, 659.25f, 587.33f, 0f,
                659.25f, 783.99f, 659.25f, 587.33f,  523.25f, 0f, 523.25f, 0f,
            };

            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)MusicRate;
                int beatIndex = (int)(t / beat);
                int bar = beatIndex / 4;
                var chord = chords[bar % 4];
                float barT = t - bar * 4 * beat;

                // Pad: soft triangle chord with slow swell per bar.
                float padEnv = Mathf.Min(1f, barT * 2f) * (0.75f + 0.25f * Mathf.Cos(2 * Mathf.PI * barT / (4 * beat)));
                float pad = 0f;
                foreach (var f in chord) pad += Triangle(f * 0.5f, t);
                pad *= 0.06f * padEnv;

                // Melody pluck.
                float note = melody[beatIndex % melody.Length];
                float noteT = t - beatIndex * beat;
                float pluck = note > 0 ? Tone(note, noteT) * Mathf.Exp(-noteT * 5f) * 0.12f : 0f;

                // Bass on beats 1 and 3.
                float bassT = t - (beatIndex - beatIndex % 2) * beat;
                float bass = Mathf.Sin(2 * Mathf.PI * chord[0] * 0.25f * bassT) * Mathf.Exp(-bassT * 3f) * 0.12f;

                b[i] = pad + pluck + bass;
            }

            // Short fade at both ends so the loop point doesn't click.
            int fade = MusicRate / 50;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                b[i] *= k;
                b[b.Length - 1 - i] *= k;
            }

            return b;
        }

        // ------------------------------------------------------------------ Helpers

        private static float Tone(float freq, float t) =>
            Mathf.Sin(2 * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * freq * t);

        private static float Triangle(float freq, float t)
        {
            float x = Mathf.Repeat(t * freq, 1f);
            return 4f * Mathf.Abs(x - 0.5f) - 1f;
        }

        private static float[] Arpeggio(float[] freqs, float step, float tail)
        {
            var b = Buffer(step * freqs.Length + tail, SfxRate);
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = (int)(n * step * SfxRate);
                for (int i = start; i < b.Length; i++)
                {
                    float t = (i - start) / (float)SfxRate;
                    b[i] += Tone(freqs[n], t) * Mathf.Exp(-t * 9f) * 0.3f;
                }
            }

            return b;
        }

        private static float[] Buffer(float seconds, int rate) => new float[Mathf.CeilToInt(seconds * rate)];

        private static AudioClip CreateClip(string name, Func<float[]> synth, int rate, bool isMusic)
        {
            string assetPath = $"{AudioFolder}/{name}.wav";
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(fullPath))
            {
                WriteWav(fullPath, Normalize(synth()), rate);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isMusic ? 0.5f : 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static float[] Normalize(float[] samples)
        {
            float peak = 0f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0.9f)
                for (int i = 0; i < samples.Length; i++) samples[i] *= 0.9f / peak;
            return samples;
        }

        private static void WriteWav(string path, float[] samples, int rate)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);        // PCM
            w.Write((short)1);        // mono
            w.Write(rate);
            w.Write(rate * 2);        // byte rate
            w.Write((short)2);        // block align
            w.Write((short)16);       // bits per sample
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), short.MinValue, short.MaxValue));
        }
    }
}
