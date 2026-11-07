using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Persistent audio player on the GameRoot object: one looping music source and one source for sound effects.
    /// Volumes follow <see cref="AudioPreferences"/> (Settings sliders). Music keeps playing across scene changes.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const float MinRepeatSeconds = 0.05f; // avoid stacking the same sound many times in one frame

        private readonly Dictionary<SoundId, float> _lastPlayed = new Dictionary<SoundId, float>();
        private AudioSource _music;
        private AudioSource _sfx;
        private SoundLibrary _library;

        /// <summary>For tests and debugging: the last sound requested and how many have played.</summary>
        public SoundId? LastSound { get; private set; }
        public int PlayCount { get; private set; }
        public bool IsMusicPlaying => _music != null && _music.isPlaying;

        public void Initialize(SoundLibrary library)
        {
            _library = library;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            AudioPreferences.Changed += ApplyVolumes;
            ApplyVolumes();

            if (_library != null && _library.Music != null)
            {
                _music.clip = _library.Music;
                _music.Play();
            }
        }

        private void OnDestroy() => AudioPreferences.Changed -= ApplyVolumes;

        public void Play(SoundId id)
        {
            if (_library == null || !_library.TryGet(id, out var entry)) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < MinRepeatSeconds) return;
            _lastPlayed[id] = now;

            _sfx.PlayOneShot(entry.clip, entry.volume <= 0f ? 1f : entry.volume);
            LastSound = id;
            PlayCount++;
        }

        private void ApplyVolumes()
        {
            if (_music != null) _music.volume = AudioPreferences.MusicVolume * (_library != null ? _library.MusicVolume : 1f);
            if (_sfx != null) _sfx.volume = AudioPreferences.SfxVolume;
        }
    }
}
