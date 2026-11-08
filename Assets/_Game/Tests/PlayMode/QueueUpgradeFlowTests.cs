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
    /// <summary>Milestone 4 step 5: buying extra factory queue slots, kept across an app restart.</summary>
    public sealed class QueueUpgradeFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderQueueUpgradeTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator BuySlots_UntilMax_AndKeepThemAfterRestart()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);

            scene.Wallet.AddCoins(5_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string bakery = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsFalse(scene.Production.CanAddSlot(bakery), "no upgrades while under construction");
            Assert.IsTrue(scene.Construction.FinishNow(bakery));

            int baseSlots = bakeryDef.ProductionSlots;
            int max = content.MaxExtraQueueSlots;
            long expectedSpent = 0;
            long coins = scene.Wallet.Coins;
            for (int i = 0; i < max; i++)
            {
                long cost = scene.Production.NextSlotCost(bakery);
                Assert.AreEqual((long)content.QueueSlotUpgradeBaseCost * (i + 1), cost);
                Assert.IsTrue(scene.Production.TryAddSlot(bakery));
                expectedSpent += cost;
            }

            Assert.AreEqual(coins - expectedSpent, scene.Wallet.Coins);
            Assert.AreEqual(baseSlots + max, scene.Production.GetQueue(bakery).Slots);
            Assert.IsFalse(scene.Production.CanAddSlot(bakery), "maxed out");
            Assert.IsFalse(scene.Production.TryAddSlot(bakery));

            // All slots can be used.
            scene.Inventory.TryAdd("wheat", 2 * (baseSlots + max));
            for (int i = 0; i < baseSlots + max; i++)
                Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakery, bread));
            Assert.AreEqual(QueueResult.QueueFull, scene.Production.Enqueue(bakery, bread));

            // Restart: slots and queued jobs are kept.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.AreEqual(baseSlots + max, scene.Production.GetQueue(bakery).Slots);
            Assert.AreEqual(baseSlots + max, scene.Production.GetQueue(bakery).Jobs.Count);
            Assert.AreEqual(max, scene.Production.ExtraSlots(bakery));
        }

        [UnityTest]
        public IEnumerator NotEnoughCoins_Fails()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            GameRoot.Current.Context.Content.TryGetBuilding("bakery", out var bakeryDef);
            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string bakery = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(bakery));

            scene.Wallet.TrySpendCoins(scene.Wallet.Coins);
            Assert.IsFalse(scene.Production.CanAffordNextSlot(bakery));
            Assert.IsFalse(scene.Production.TryAddSlot(bakery));
            Assert.AreEqual(0, scene.Production.ExtraSlots(bakery));
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
