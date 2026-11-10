using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Put on a Canvas: every Button under it (including ones cloned at runtime, like shop and recipe cards)
    /// plays the UI tap sound when clicked and squashes slightly while pressed. New buttons are picked up by a cheap periodic scan.
    /// </summary>
    public sealed class ButtonSounds : MonoBehaviour
    {
        [SerializeField] private float scanInterval = 0.5f;

        private readonly HashSet<Button> _hooked = new HashSet<Button>();
        private float _nextScan;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + scanInterval;

            foreach (var button in GetComponentsInChildren<Button>(includeInactive: true))
            {
                if (!_hooked.Add(button)) continue;
                button.onClick.AddListener(PlayTap);
                // Squash on press (not on top-bar pills, which pop when their value changes).
                if (!button.name.Contains("Pill") && button.GetComponent<PressScale>() == null) button.gameObject.AddComponent<PressScale>();
            }
        }

        private static void PlayTap()
        {
            var audio = GameRoot.Current != null ? GameRoot.Current.Context.Audio : null;
            if (audio != null) audio.Play(SoundId.UiTap);
        }
    }
}
