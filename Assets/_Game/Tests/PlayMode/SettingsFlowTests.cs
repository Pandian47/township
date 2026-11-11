using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>Settings window (v0.35.0): opened from the town's menu button and the main menu; credits; privacy policy.</summary>
    public sealed class SettingsFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderSettingsTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator TownMenuButton_OpensSettings_WithCreditsAndMainMenu()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var settings = scene.Hud.SettingsPanel;
            Assert.IsNotNull(settings, "town has a settings window");
            Assert.IsFalse(settings.IsOpen);

            scene.Hud.MenuButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(settings.IsOpen, "the menu button opens Settings instead of leaving the town");
            Assert.AreEqual(SceneNames.Town, SceneManager.GetActiveScene().name);
            Assert.IsTrue(settings.MainMenuButton.gameObject.activeSelf, "town settings offer Main menu");

            settings.CreditsButton.onClick.Invoke();
            Assert.IsTrue(settings.CreditsOpen);
            StringAssert.Contains("Fredoka", settings.CreditsText);
            StringAssert.Contains("SIL OPEN FONT LICENSE Version 1.1", settings.CreditsText, "the font licence is shown in the app");
            StringAssert.Contains(Application.version, settings.CreditsText);
            StringAssert.DoesNotContain("{version}", settings.CreditsText);

            settings.Close();
            settings.Open();
            Assert.IsFalse(settings.CreditsOpen, "reopening settings starts on the main page");

            // No policy address yet: the button explains instead of opening an empty page.
            if (string.IsNullOrWhiteSpace(GameRoot.Current.Context.Content.AdsConfig.PrivacyPolicyUrl))
            {
                settings.PrivacyPolicyButton.onClick.Invoke();
                Assert.AreEqual(SettingsPanel.NoPrivacyUrlMessage, settings.Message);
            }

            settings.MainMenuButton.onClick.Invoke();
            float end = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name, "Main menu saves and leaves the town");
        }

        [UnityTest]
        public IEnumerator MainMenuSettings_HasNoMainMenuButton()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;
            yield return null;
            var settings = Object.FindAnyObjectByType<SettingsPanel>();
            Assert.IsNotNull(settings);
            settings.Open();
            Assert.IsTrue(settings.IsOpen);
            Assert.IsFalse(settings.MainMenuButton.gameObject.activeSelf, "already in the main menu");
            settings.CreditsButton.onClick.Invoke();
            StringAssert.Contains("Fredoka", settings.CreditsText);
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
