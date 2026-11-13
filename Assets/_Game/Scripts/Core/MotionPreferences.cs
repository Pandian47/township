using System;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// v0.41.0: device-level "Reduce motion" setting (Settings window). When on, windows open and close at once and UI
    /// flourishes (flying icons, pops, bounces, glows) are skipped. Kept in PlayerPrefs like <see cref="AudioPreferences"/>.
    /// </summary>
    public static class MotionPreferences
    {
        private const string ReduceKey = "settings.reduceMotion";

        public static event Action Changed;

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt(ReduceKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReduceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
