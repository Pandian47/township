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
    /// <summary>Milestone 5 step 4: land expansion end to end.</summary>
    public sealed class LandFlowTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderLandTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NewTown_OwnsStartArea_WithSignsAroundIt()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var land = scene.Land;
            var content = GameRoot.Current.Context.Content;
            int startPlots = (content.LandStartMax.x - content.LandStartMin.x + 1) * (content.LandStartMax.y - content.LandStartMin.y + 1);
            Assert.AreEqual(startPlots, land.Owned.Count);
            Assert.IsTrue(scene.Town.Buildings.All(b => land.IsCellUnlocked(b.Origin)), "starter buildings stand on owned land");
            Assert.AreEqual(land.PlotsForSale().Count(), scene.LandController.SignCount, "one sign per plot for sale");
        }

        [UnityTest]
        public IEnumerator BuyingLand_NeedsLevelCoinsAndMaterials_ThenAllowsBuilding_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var land = scene.Land;
            var controller = scene.LandController;
            var content = GameRoot.Current.Context.Content;
            var plot = land.PlotsForSale().First();
            var tier = controller.TierFor(plot);
            var cell = land.PlotOrigin(plot);
            content.TryGetBuilding("cottage", out var cottage);

            Assert.AreEqual(PlacementResult.LockedLand, scene.Town.CanPlace(cell, cottage.FootprintSize));
            Assert.IsFalse(controller.TryBuy(plot), "level too low");

            scene.Progress.AddXp(10_000);
            scene.Wallet.AddCoins(10_000);
            Assert.IsFalse(controller.TryBuy(plot), "no materials yet");

            foreach (var m in tier.materials) scene.Inventory.TryAdd(m.material.Id, m.amount);
            long coins = scene.Wallet.Coins;
            Assert.IsTrue(controller.TryBuy(plot));
            Assert.AreEqual(coins - tier.coins, scene.Wallet.Coins);
            foreach (var m in tier.materials) Assert.AreEqual(0, scene.Inventory.Count(m.material.Id), $"{m.material.Id} spent");
            Assert.IsTrue(land.IsOwned(plot));
            Assert.AreEqual(PlacementResult.Valid, scene.Town.CanPlace(cell, cottage.FootprintSize), "can build on the new land");
            Assert.AreEqual(land.PlotsForSale().Count(), controller.SignCount, "signs updated");
            Assert.IsTrue(scene.Tasks.Board.Completed.Contains("guide_buy_land") ||
                          scene.Tasks.Board.Slots.Any(t => t != null && t.Spec.Id == "guide_buy_land" && t.IsComplete) ||
                          scene.Tasks.Board.Slots.All(t => t == null || t.Spec.Id != "guide_buy_land"),
                "buy-land task counted when it was on the board");

            // Restart: still owned.
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.IsTrue(scene.Land.IsOwned(plot));
            Assert.AreEqual(PlacementResult.Valid, scene.Town.CanPlace(cell, cottage.FootprintSize));
        }

        [UnityTest]
        public IEnumerator OldSave_WithBuildingOutsideStartArea_KeepsThatLand()
        {
            // A save from before land existed: a cottage near the map corner, no land data.
            var save = NewGameFactory.Create(0, 1500, 20);
            save.buildings.Add(new BuildingSaveData { instanceId = "old_cottage", definitionId = "cottage", x = 1, y = 1 });
            var store = new JsonSaveStore(_saveDir);
            store.Save(save);

            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();

            Assert.IsTrue(scene.Town.TryGet("old_cottage", out _), "the old building is still there");
            Assert.IsTrue(scene.Land.IsCellUnlocked(new GridCoord(1, 1)), "and its land is owned");
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
