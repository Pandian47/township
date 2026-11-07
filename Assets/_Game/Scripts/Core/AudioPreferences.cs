using System;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Device-level sound settings. Kept in PlayerPrefs (not in the town save) because they belong to the device,
    /// not to the player's progress, and must survive "New Game".
    /// </summary>
    public static class AudioPreferences
    {
        private const string MusicKey = "settings.musicVolume";
        private const string SfxKey = "settings.sfxVolume";

        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }
    }
}
