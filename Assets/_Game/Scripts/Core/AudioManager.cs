using UnityEngine;

namespace TownBuilder
{
    /// <summary>Owns the music and SFX sources of a scene and applies the player's volume settings.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private void OnEnable()
        {
            AudioPreferences.Changed += Apply;
            Apply();
        }

        private void OnDisable() => AudioPreferences.Changed -= Apply;

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
        }

        private void Apply()
        {
            if (musicSource != null) musicSource.volume = AudioPreferences.MusicVolume;
            if (sfxSource != null) sfxSource.volume = AudioPreferences.SfxVolume;
        }
    }
}
