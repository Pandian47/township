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
    /// <summary>Developer-mode shortcuts (available in the Editor and test APKs).</summary>
    public sealed class DevToolsFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderDevTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator Shortcuts_UnlockEverything()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            Assert.IsTrue(DevTools.Enabled, "enabled in the Editor");
            Assert.NotNull(GameObject.Find("DevButton"), "DEV button in the HUD");

            long coins = scene.Wallet.Coins;
            DevTools.AddCurrency(scene);
            Assert.AreEqual(coins + 10_000, scene.Wallet.Coins);

            DevTools.LevelUp(scene);
            Assert.AreEqual(2, scene.Progress.Level);

            DevTools.UnlockAll(scene);
            Assert.GreaterOrEqual(scene.Progress.Level, DevTools.UnlockAllLevel);
            Assert.IsTrue(content.Buildings.All(b => scene.Progress.IsUnlocked(b.UnlockLevel)), "every building unlocked");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(scene.Train.IsOpen, "train station open");

            DevTools.FillBarn(scene, content);
            Assert.IsTrue(scene.Storage.Upgrades.IsMaxed);
            Assert.Greater(scene.Inventory.Count("planks"), 0, "materials too");
            Assert.Greater(scene.Inventory.Count("cake"), 0);

            // Plant a crop and queue bread, then finish everything.
            content.TryGetCrop("wheat", out var wheat);
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(field, wheat));
            DevTools.FinishAllTimers(scene);
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(field));

            DevTools.UnlockAllLand(scene);
            Assert.AreEqual(0, scene.Land.PlotsForSale().Count());
            Assert.AreEqual(scene.Land.Columns * scene.Land.Rows, scene.Land.Owned.Count);
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
