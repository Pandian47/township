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
    /// <summary>Milestone 7 step 3: fruit trees and the Juice Press end to end.</summary>
    public sealed class OrchardFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderOrchardTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator AppleTree_GrowsByItself_RegrowsAfterHarvest_AndIsSaved()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("apple_tree", out var treeDef);
            Assert.IsNotNull(treeDef);
            Assert.IsTrue(treeDef.IsFruitTree);
            var apple = treeDef.Fruit;
            Assert.AreEqual("apple", apple.Id);
            Assert.IsTrue(apple.GrowsOnTree);

            Assert.IsFalse(scene.Placement.BeginPlacement(treeDef), $"Apple Tree needs level {treeDef.UnlockLevel}");
            scene.Progress.AddXp(10_000);
            scene.Wallet.AddCoins(10_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(treeDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string tree = scene.Town.Buildings.First(b => b.DefinitionId == "apple_tree").InstanceId;

            Assert.AreEqual(PlotStage.Growing, scene.Farming.GetStage(tree), "a new tree starts growing apples by itself");
            Assert.AreEqual(PlantResult.NotAField, scene.Farming.Plant(tree, apple), "trees aren't planted");
            Assert.IsFalse(scene.Farming.TryHarvest(tree));

            _clock.Advance(apple.GrowDurationMs);
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(tree));
            int before = scene.Inventory.Count("apple");
            Assert.IsTrue(scene.Farming.TryHarvest(tree));
            Assert.AreEqual(before + apple.HarvestYield, scene.Inventory.Count("apple"));
            Assert.AreEqual(PlotStage.Growing, scene.Farming.GetStage(tree), "regrows straight away");

            // Restart halfway: still growing, ripe at the same time.
            _clock.Advance(apple.GrowDurationMs / 2);
            scene.SaveNow();
            Object.DestroyImmediate(GameRoot.Current.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneNames.MainMenu);
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Current == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(GameRoot.Current.Context.ContinueGame().Succeeded);
            yield return LoadTown();
            scene = Object.FindAnyObjectByType<TownSceneController>();
            Assert.AreEqual(PlotStage.Growing, scene.Farming.GetStage(tree));
            _clock.Advance(apple.GrowDurationMs / 2);
            Assert.AreEqual(PlotStage.Ready, scene.Farming.GetStage(tree));
        }

        [UnityTest]
        public IEnumerator FieldPicker_DoesNotOfferTreeFruit()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            var panel = Object.FindAnyObjectByType<FieldPanel>(FindObjectsInactive.Include);
            var buttons = panel.GetComponentsInChildren<CropButtonView>(true).Where(b => b.Crop != null).ToList();
            Assert.IsTrue(buttons.Any(b => b.Crop.Id == "wheat"));
            Assert.IsFalse(buttons.Any(b => b.Crop.GrowsOnTree), "apples and cherries grow on trees, not fields");
            Assert.IsTrue(content.Crops.Any(c => c.GrowsOnTree));
        }

        [UnityTest]
        public IEnumerator JuicePress_TurnsApplesIntoJuice()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("juice_press", out var pressDef);
            content.TryGetRecipe("press_apple_juice", out var recipe);
            Assert.IsNotNull(pressDef);
            Assert.IsNotNull(recipe);
            Assert.IsTrue(pressDef.Recipes.Contains(recipe));

            scene.Progress.AddXp(10_000);
            scene.Wallet.AddCoins(10_000);
            Assert.IsTrue(scene.Placement.BeginPlacement(pressDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string press = scene.Town.Buildings.First(b => b.DefinitionId == "juice_press").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(press));

            scene.Inventory.TryAdd("apple", recipe.Ingredients.Single().amount);
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(press, recipe));
            _clock.Advance(recipe.DurationMs);
            Assert.AreEqual(1, scene.Production.Collect(press));
            Assert.AreEqual(recipe.OutputAmount, scene.Inventory.Count("apple_juice"));
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
