using System;
using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>Every sound the game can play. Code refers to sounds by id only; the clips live in <see cref="SoundLibrary"/>.</summary>
    public enum SoundId
    {
        UiTap,
        Place,
        Remove,
        BuildComplete,
        Plant,
        Harvest,
        Collect,
        Coins,
        LevelUp,
        Error,
    }

    [Serializable]
    public struct SoundEntry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    /// <summary>
    /// Maps sound ids to clips plus the music loop. Replace a clip (or the generated PH_*.wav file) to change a sound.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;

        public AudioClip Music => music;
        public float MusicVolume => musicVolume;
        public IReadOnlyList<SoundEntry> Sounds => sounds;

        public bool TryGet(SoundId id, out SoundEntry entry)
        {
            foreach (var s in sounds)
            {
                if (s.id == id && s.clip != null)
                {
                    entry = s;
                    return true;
                }
            }

            entry = default;
            return false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(List<SoundEntry> entries, AudioClip musicClip)
        {
            sounds = entries;
            music = musicClip;
        }
#endif
    }
}
