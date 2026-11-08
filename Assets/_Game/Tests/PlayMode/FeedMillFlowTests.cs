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
    /// <summary>Milestone 4 step 1: soybean + Feed Mill, end to end.</summary>
    public sealed class FeedMillFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderFeedMillTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator FeedMill_And_Soybean_AreLockedAtLevel1()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("feed_mill", out var mill);
            content.TryGetCrop("soybean", out var soybean);
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            Assert.IsFalse(scene.Placement.BeginPlacement(mill), "Feed Mill needs level 4");
            Assert.AreEqual(PlantResult.Locked, scene.Farming.Plant(field, soybean));
        }

        [UnityTest]
        public IEnumerator GrowSoybean_MakeBothFeeds_Collect()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("feed_mill", out var mill);
            content.TryGetCrop("soybean", out var soybean);
            content.TryGetRecipe("make_chicken_feed", out var chickenFeed);
            content.TryGetRecipe("make_cow_feed", out var cowFeed);

            // Reach level 5 (cow feed unlock) and have enough coins.
            scene.Progress.AddXp(10_000);
            Assert.GreaterOrEqual(scene.Progress.Level, 5);
            scene.Wallet.AddCoins(5_000);

            // Soybean grows and harvests like any crop.
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;
            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(field, soybean));
            _clock.Advance(soybean.GrowDurationMs);
            Assert.IsTrue(scene.Farming.TryHarvest(field));
            Assert.AreEqual(soybean.HarvestYield, scene.Inventory.Count("soybean"));

            // Build the Feed Mill.
            Assert.IsTrue(scene.Placement.BeginPlacement(mill));
            Assert.IsTrue(scene.Placement.Confirm());
            string millId = scene.Town.Buildings.First(b => b.DefinitionId == "feed_mill").InstanceId;
            Assert.AreEqual(QueueResult.NotAFactory, scene.Production.Enqueue(millId, chickenFeed), "not usable while under construction");
            Assert.IsTrue(scene.Construction.FinishNow(millId));

            scene.Inventory.TryAdd("wheat", 2);
            scene.Inventory.TryAdd("corn", 3); // 1 for chicken feed, 2 for cow feed; the harvested soybeans cover the rest
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(millId, chickenFeed));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(millId, cowFeed));
            Assert.AreEqual(0, scene.Inventory.Count("wheat"));
            Assert.AreEqual(0, scene.Inventory.Count("corn"));

            _clock.Advance(chickenFeed.DurationMs + cowFeed.DurationMs);
            Assert.AreEqual(2, scene.Production.Collect(millId));
            Assert.AreEqual(1, scene.Inventory.Count("chicken_feed"));
            Assert.AreEqual(1, scene.Inventory.Count("cow_feed"));

            // Feeds can be sold like any product.
            long coins = scene.Wallet.Coins;
            Assert.AreEqual(SellResult.Sold, scene.Storage.SellAll("cow_feed"));
            Assert.AreEqual(coins + cowFeed.Output.SellPrice, scene.Wallet.Coins);
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
