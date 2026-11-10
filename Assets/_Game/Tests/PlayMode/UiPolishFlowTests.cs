using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TownBuilder.Tests
{
    /// <summary>Milestone 6 step 4: panel transitions, rewards flying to the top bar, pressed buttons.</summary>
    public sealed class UiPolishFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderUiPolishTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator OpeningAPanel_FadesAndPopsIn()
        {
            yield return LoadTown();
            GameObject.Find("BarnButton").GetComponent<Button>().onClick.Invoke();
            var root = GameObject.Find("BarnPanel").transform.Find("Root");
            Assert.IsTrue(root.gameObject.activeSelf);
            var transition = root.GetComponent<PanelTransition>();
            Assert.NotNull(transition, "every panel root has a transition");
            yield return null;
            Assert.IsTrue(transition.IsPlaying);
            Assert.Less(root.GetComponent<CanvasGroup>().alpha, 1f, "starts faded");

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(transition.IsPlaying);
            Assert.AreEqual(1f, root.GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.AreEqual(Vector3.one, root.Find("Window").localScale, "window back at full size");
        }

        [UnityTest]
        public IEnumerator EarningCoins_FliesIconsToTheTopBar_SpendingDoesNot()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var flyer = Object.FindAnyObjectByType<RewardFlyer>();
            Assert.NotNull(flyer);

            scene.Wallet.TrySpendCoins(100);
            Assert.AreEqual(0, flyer.ActiveIcons, "spending flies nothing");

            scene.Wallet.AddCoins(100);
            Assert.Greater(flyer.ActiveIcons, 0, "coins fly");
            scene.Progress.AddXp(5);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(0, flyer.ActiveIcons, "all arrived");
        }

        [UnityTest]
        public IEnumerator Buttons_GetThePressSquash_PillsDoNot()
        {
            yield return LoadTown();
            yield return new WaitForSecondsRealtime(0.7f); // ButtonSounds scans periodically
            Assert.NotNull(GameObject.Find("BuildButton").GetComponent<PressScale>());
            Assert.IsNull(GameObject.Find("BarnPill").GetComponent<PressScale>(), "top-bar pills pop instead");
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
