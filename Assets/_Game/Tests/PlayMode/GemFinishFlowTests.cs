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
    /// <summary>v0.29.1: gems finish growing crops and factory products (1 gem per minute left).</summary>
    public sealed class GemFinishFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderGemTests", System.Guid.NewGuid().ToString("N"));
            GameRoot.SaveDirectoryOverride = _saveDir;
            _clock = new ManualClock(1_700_000_000_000);
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
        public IEnumerator Crop_FinishWithGems_CostsOneGemPerMinuteLeft()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("corn", out var corn);
            scene.Progress.AddXp(200);
            var field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(field, corn));

            _clock.Advance(90_000); // 1.5 min of 5 gone: 3.5 left -> 4 gems
            int cost = scene.Farming.GemCostToFinish(field);
            Assert.AreEqual(TimeSkip.GemCost(corn.GrowDurationMs - 90_000), cost);
            Assert.AreEqual(4, cost);

            long gems = scene.Wallet.Gems;
            Assert.IsTrue(scene.Farming.FinishWithGems(field));
            Assert.AreEqual(gems - cost, scene.Wallet.Gems);
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(field));
            Assert.AreEqual(0, scene.Farming.GemCostToFinish(field), "nothing left to skip");
        }

        [UnityTest]
        public IEnumerator NotEnoughGems_KeepsGrowing()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetCrop("wheat", out var wheat);
            var field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            scene.Farming.Plant(field, wheat);
            scene.Wallet.TrySpendGems(scene.Wallet.Gems); // no gems left

            Assert.IsFalse(scene.Farming.FinishWithGems(field));
            Assert.AreEqual(PlotStage.Growing, scene.Farming.GetStage(field));
        }

        [UnityTest]
        public IEnumerator Factory_FinishWithGems_FinishesTheActiveProduct()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            scene.Wallet.AddCoins(10_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string bakery = scene.Town.Buildings.Last(b => b.DefinitionId == "bakery").InstanceId;
            scene.Construction.FinishNow(bakery);

            scene.Inventory.TryAdd("wheat", 10);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakery, bread));
            int cost = scene.Production.GemCostToFinish(bakery);
            Assert.AreEqual(TimeSkip.GemCost(bread.DurationMs), cost);

            long gems = scene.Wallet.Gems;
            Assert.IsTrue(scene.Production.FinishWithGems(bakery));
            Assert.AreEqual(gems - cost, scene.Wallet.Gems);
            Assert.AreEqual(1, scene.Production.Collect(bakery));
            Assert.AreEqual(0, scene.Production.GemCostToFinish(bakery), "idle factory: nothing to skip");
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
