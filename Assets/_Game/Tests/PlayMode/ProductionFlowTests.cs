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
    /// <summary>The v0.1 loop end to end: wheat -> bake bread -> collect -> sell, across an app restart.</summary>
    public sealed class ProductionFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderProdTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator BakeBread_CloseApp_Collect_Sell()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            Assert.IsTrue(bakeryDef.IsFactory, "Bakery should have recipes");

            // Build a bakery and get wheat.
            Assert.IsTrue(scene.Placement.BeginPlacement(bakeryDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string bakeryId = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(bakeryId), "finish construction so the bakery works");
            scene.Inventory.TryAdd("wheat", 4);

            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakeryId, bread));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakeryId, bread));
            Assert.AreEqual(0, scene.Inventory.Count("wheat"), "ingredients are taken when queued");
            Assert.AreEqual(QueueResult.MissingIngredients, scene.Production.Enqueue(bakeryId, bread));
            Assert.AreEqual(0, scene.Production.Collect(bakeryId), "nothing ready yet");

            // Close the app for 10 minutes.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            _clock.Advance(10 * 60 * 1000);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => GameRoot.Current != null);
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            SceneManager.LoadScene(SceneNames.Town);
            yield return WaitForTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.AreEqual(2, scene.Production.CountReady(bakeryId), "both loaves baked while closed");
            long xp = scene.Progress.Xp;
            Assert.AreEqual(2, scene.Production.Collect(bakeryId));
            Assert.AreEqual(2, scene.Inventory.Count("bread"));
            Assert.AreEqual(xp + 2 * bread.Xp, scene.Progress.Xp);

            long coins = scene.Wallet.Coins;
            Assert.AreEqual(SellResult.Sold, scene.Storage.SellAll("bread"));
            content.TryGetItem("bread", out var breadItem);
            Assert.AreEqual(coins + 2 * breadItem.SellPrice, scene.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator QueueFull_And_FinishNow()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            scene.Placement.BeginPlacement(bakeryDef);
            scene.Placement.Confirm();
            string bakeryId = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(bakeryId), "finish construction so the bakery works");
            scene.Inventory.TryAdd("wheat", 20);

            for (int i = 0; i < bakeryDef.ProductionSlots; i++)
                Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(bakeryId, bread));
            Assert.AreEqual(QueueResult.QueueFull, scene.Production.Enqueue(bakeryId, bread));

            Assert.IsTrue(scene.Production.FinishActiveNow(bakeryId));
            Assert.AreEqual(1, scene.Production.CountReady(bakeryId));
        }

        [UnityTest]
        public IEnumerator TappingFactoryWithReadyProducts_Collects()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("bakery", out var bakeryDef);
            content.TryGetRecipe("bake_bread", out var bread);
            scene.Placement.BeginPlacement(bakeryDef);
            scene.Placement.Confirm();
            string bakeryId = scene.Town.Buildings.First(b => b.DefinitionId == "bakery").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(bakeryId), "finish construction so the bakery works");
            scene.Inventory.TryAdd("wheat", 2);
            scene.Production.Enqueue(bakeryId, bread);
            _clock.Advance(bread.DurationMs);

            // Selecting goes through the tap interceptors: a ready factory collects instead.
            var intercept = typeof(PlacementSystem).GetMethod("InterceptTap",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            scene.Town.TryGet(bakeryId, out var bakeryBuilding);
            Assert.IsTrue((bool)intercept.Invoke(scene.Placement, new object[] { bakeryBuilding }));
            Assert.AreEqual(1, scene.Inventory.Count("bread"));
        }

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
            Assert.IsTrue(Object.FindAnyObjectByType<TownSceneController>().IsReady);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds = 5f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
