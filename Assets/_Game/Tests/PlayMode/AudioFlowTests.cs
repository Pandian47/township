using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Game events reach the audio player (actual playback isn't asserted: batch mode has no audio device).</summary>
    public sealed class AudioFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderAudioTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator GameAudio_IsCreated_WithMusic()
        {
            yield return LoadTown();
            var audio = GameRoot.Current.Context.Audio;
            Assert.NotNull(audio);
            Assert.AreSame(audio, GameRoot.Current.GetComponent<GameAudio>(), "audio lives on the persistent GameRoot");
            Assert.NotNull(GameRoot.Current.Context.Content.SoundLibrary.Music);
        }

        [UnityTest]
        public IEnumerator Harvest_And_LevelUp_PlaySounds()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var audio = GameRoot.Current.Context.Audio;
            GameRoot.Current.Context.Content.TryGetCrop("wheat", out var wheat);
            var field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            scene.Farming.Plant(field, wheat);
            Assert.AreEqual(SoundId.Plant, audio.LastSound);

            scene.Farming.FinishNow(field);
            yield return new WaitForSecondsRealtime(0.1f); // past the anti-spam window
            scene.Farming.TryHarvest(field);
            // A rare collection find can play its own sound right after the harvest, so check the history.
            Assert.IsTrue(audio.PlayedWithin(SoundId.Harvest, 0.5f));

            scene.Progress.AddXp(1000);
            // Other sounds may follow in the same moment (e.g. the station opening at level 5), so check the history.
            Assert.IsTrue(audio.PlayedWithin(SoundId.LevelUp, 0.5f));
        }

        [UnityTest]
        public IEnumerator ButtonTap_PlaysUiSound()
        {
            yield return LoadTown();
            yield return new WaitForSecondsRealtime(0.7f); // ButtonSounds scans for buttons periodically
            var audio = GameRoot.Current.Context.Audio;
            int before = audio.PlayCount;

            GameObject.Find("BarnButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(SoundId.UiTap, audio.LastSound);
            Assert.Greater(audio.PlayCount, before);
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end)
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                if (c != null && c.IsReady) yield break;
                yield return null;
            }

            Assert.Fail("Town scene did not initialise");
        }
    }
}
