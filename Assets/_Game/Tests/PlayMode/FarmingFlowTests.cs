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
    /// <summary>Farming end to end in the real Town scene, with a manual clock to skip growth time.</summary>
    public sealed class FarmingFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderFarmTests", System.Guid.NewGuid().ToString("N"));
            _clock = new ManualClock(1_700_000_000_000);
            GameRoot.SaveDirectoryOverride = _saveDir;
            GameRoot.ClockOverride = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            GameRoot.SaveDirectoryOverride = null;
            GameRoot.ClockOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator NewTown_HasStarterFields_AndBarnCapacity()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;

            int fields = scene.Town.Buildings.Count(b => b.DefinitionId == "field");
            Assert.GreaterOrEqual(fields, 1, "New towns should start with fields");
            Assert.AreEqual(content.BaseStorageCapacity, scene.Inventory.Capacity);
        }

        [UnityTest]
        public IEnumerator PlantGrowHarvest_FillsBarn_GivesXp_AndSurvivesRestart()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);
            var fieldId = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(fieldId, wheat));
            Assert.AreEqual(PlotStage.Growing, scene.Farming.GetStage(fieldId));
            Assert.IsFalse(scene.Farming.TryHarvest(fieldId), "cannot harvest while growing");

            // Save mid-growth, "close the app" for 5 minutes, reopen.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            _clock.Advance(5 * 60 * 1000);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null);
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();

            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(fieldId), "crop kept growing while closed");

            long xpBefore = scene.Progress.Xp;
            Assert.IsTrue(scene.Farming.TryHarvest(fieldId));
            Assert.AreEqual(wheat.HarvestYield, scene.Inventory.Count("wheat"));
            Assert.AreEqual(xpBefore + wheat.HarvestXp, scene.Progress.Xp);
            Assert.AreEqual(PlotStage.Empty, scene.Farming.GetStage(fieldId));

            scene.SaveNow();
            var saved = GameRoot.Current.Context.SaveStore.Load().Data;
            Assert.AreEqual(wheat.HarvestYield, saved.inventory.Single(i => i.itemId == "wheat").amount);
            Assert.IsEmpty(saved.cropPlots);
        }

        [UnityTest]
        public IEnumerator FullBarn_BlocksHarvest_ButKeepsCrop()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetCrop("wheat", out var wheat);
            var fieldId = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            scene.Inventory.TryAdd("corn", scene.Inventory.FreeSpace); // fill the barn
            scene.Farming.Plant(fieldId, wheat);
            Assert.IsTrue(scene.Farming.FinishNow(fieldId));

            string message = null;
            scene.Farming.Notification += m => message = m;
            Assert.IsFalse(scene.Farming.TryHarvest(fieldId));
            StringAssert.Contains("Barn is full", message);
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(fieldId));
        }

        [UnityTest]
        public IEnumerator BuildingABarn_IncreasesCapacity()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("barn", out var barn);
            int before = scene.Inventory.Capacity;

            Assert.IsTrue(scene.Placement.BeginPlacement(barn));
            Assert.IsTrue(scene.Placement.Confirm());
            string barnId = scene.Town.Buildings.First(b => b.DefinitionId == "barn").InstanceId;
            Assert.AreEqual(before, scene.Inventory.Capacity, "no bonus while under construction");
            Assert.IsTrue(scene.Construction.FinishNow(barnId));
            Assert.AreEqual(before + barn.StorageCapacityBonus, scene.Inventory.Capacity);
        }

        [UnityTest]
        public IEnumerator RemovingAField_DropsItsCrop()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetCrop("wheat", out var wheat);
            var fieldId = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            scene.Farming.Plant(fieldId, wheat);

            scene.Placement.SelectBuilding(fieldId);
            Assert.IsTrue(scene.Placement.RemoveSelected());
            Assert.IsFalse(scene.Farming.Plots.TryGetCrop(fieldId, out _));
        }

        [UnityTest]
        public IEnumerator Barn_SellCrops_ForCoins_AndExpand_Persists()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);

            scene.Inventory.TryAdd("wheat", 10);
            long coins = scene.Wallet.Coins;
            Assert.AreEqual(SellResult.Sold, scene.Storage.Sell("wheat", 4));
            Assert.AreEqual(coins + 4 * wheat.SellPrice, scene.Wallet.Coins);
            Assert.AreEqual(SellResult.Sold, scene.Storage.SellAll("wheat"));
            Assert.AreEqual(0, scene.Inventory.Count("wheat"));

            int capacity = scene.Inventory.Capacity;
            Assert.IsTrue(scene.Storage.TryExpand());
            Assert.AreEqual(capacity + content.StorageExpansionCapacity, scene.Inventory.Capacity);

            // Restart: expansion level survives.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null);
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(1, scene.Storage.Upgrades.Level);
            Assert.AreEqual(capacity + content.StorageExpansionCapacity, scene.Inventory.Capacity);
        }

        [UnityTest]
        public IEnumerator BarnButton_OpensBarnScreen_WithItems()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            scene.Inventory.TryAdd("corn", 5);

            var barnButton = GameObject.Find("BarnButton").GetComponent<UnityEngine.UI.Button>();
            barnButton.onClick.Invoke();
            var panel = Object.FindAnyObjectByType<BarnPanel>();
            Assert.IsTrue(panel.IsOpen);

            var item = panel.GetComponentsInChildren<BarnItemView>(includeInactive: false).FirstOrDefault();
            Assert.NotNull(item, "Barn should show a card for corn");
            var texts = string.Join(" | ", item.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text));
            StringAssert.Contains("Corn", texts);
            StringAssert.Contains("x5", texts);
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator LoadTown()
        {
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
        }

        private static IEnumerator WaitForTown()
        {
            yield return WaitFor(() =>
            {
                var c = Object.FindAnyObjectByType<TownSceneController>();
                return c != null && c.IsReady;
            });
            Assert.IsTrue(Object.FindAnyObjectByType<TownSceneController>().IsReady, "Town scene did not initialise");
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds = 5f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
