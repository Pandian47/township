using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TownBuilder.Tests
{
    /// <summary>v0.39.0: the phone's back button closes the top window first, then asks to quit; Quit saves first.</summary>
    public sealed class BackButtonTests
    {
        private string _saveDir;
        private System.Action _realQuit;
        private int _quits;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderBackTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
            _realQuit = BackButtonHandler.QuitApplication;
            _quits = 0;
            BackButtonHandler.QuitApplication = () => _quits++;
        }

        [TearDown]
        public void TearDown()
        {
            BackButtonHandler.QuitApplication = _realQuit;
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator Town_BackClosesWindowsFirst_ThenAsksToQuit_QuitSaves()
        {
            SceneManager.LoadScene(SceneNames.Town);
            float end = Time.realtimeSinceStartup + 5f;
            TownSceneController scene = null;
            while (Time.realtimeSinceStartup < end && (scene == null || !scene.IsReady))
            {
                scene = Object.FindAnyObjectByType<TownSceneController>();
                yield return null;
            }

            var back = scene.Hud.BackButton;
            Assert.IsNotNull(back, "the town has a back-button handler");

            // Whatever is open at the start (e.g. the daily reward) closes first; then the quit dialog appears.
            for (int i = 0; i < 6 && !back.QuitDialogOpen; i++) back.HandleBack();
            Assert.IsTrue(back.QuitDialogOpen, "nothing open: back asks to quit");
            back.HandleBack();
            Assert.IsFalse(back.QuitDialogOpen, "back again = stay");

            // An open window closes before anything else.
            scene.Hud.MenuButton.onClick.Invoke();
            Assert.IsTrue(scene.Hud.SettingsPanel.IsOpen);
            back.HandleBack();
            Assert.IsFalse(scene.Hud.SettingsPanel.IsOpen, "back closes Settings");
            Assert.IsFalse(back.QuitDialogOpen, "and doesn't ask to quit yet");

            // Quit: saves, then closes the game.
            int saves = 0;
            var save = back.BeforeQuit;
            Assert.IsNotNull(save, "the town saves before quitting");
            back.BeforeQuit = () => { saves++; save(); };
            back.OpenQuitDialog();
            var quit = GameObject.Find("QuitDialog").transform.Find("Root/Window/Buttons/QuitButton").GetComponent<UnityEngine.UI.Button>();
            quit.onClick.Invoke();
            Assert.AreEqual(1, saves, "saved first");
            Assert.AreEqual(1, _quits, "then the game closes");
            Assert.IsFalse(back.QuitDialogOpen);
        }

        [UnityTest]
        public IEnumerator MainMenu_BackAsksToQuit_StayKeepsPlaying()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;
            yield return null;
            var back = Object.FindAnyObjectByType<BackButtonHandler>();
            Assert.IsNotNull(back, "the main menu has a back-button handler");
            back.HandleBack();
            Assert.IsTrue(back.QuitDialogOpen);
            GameObject.Find("QuitDialog").transform.Find("Root/Window/Buttons/StayButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(back.QuitDialogOpen, "Stay closes the dialog");
            Assert.AreEqual(0, _quits);
        }
    }
}
