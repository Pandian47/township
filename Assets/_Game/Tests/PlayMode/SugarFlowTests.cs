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
    /// <summary>Milestone 4 step 3: grow sugarcane, mill it into sugar.</summary>
    public sealed class SugarFlowTests
    {
        private string _saveDir;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            if (GameRoot.Current != null) Object.DestroyImmediate(GameRoot.Current.gameObject);
            _saveDir = Path.Combine(Path.GetTempPath(), "TownBuilderSugarTests", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator GrowSugarcane_MillIntoSugar()
        {
            yield return LoadTown();
            var scene = Object.FindAnyObjectByType<TownSceneController>();
            var content = GameRoot.Current.Context.Content;
            content.TryGetBuilding("sugar_mill", out var millDef);
            content.TryGetCrop("sugarcane", out var cane);
            content.TryGetRecipe("make_sugar", out var sugar);
            string field = scene.Town.Buildings.First(b => b.DefinitionId == "field").InstanceId;

            Assert.AreEqual(PlantResult.Locked, scene.Farming.Plant(field, cane), "sugarcane needs level 7");
            Assert.IsFalse(scene.Placement.BeginPlacement(millDef), "Sugar Mill needs level 7");

            scene.Progress.AddXp(10_000);
            Assert.GreaterOrEqual(scene.Progress.Level, millDef.UnlockLevel);
            scene.Wallet.AddCoins(10_000);

            Assert.AreEqual(PlantResult.Planted, scene.Farming.Plant(field, cane));
            _clock.Advance(cane.GrowDurationMs);
            Assert.IsTrue(scene.Farming.TryHarvest(field));
            Assert.AreEqual(cane.HarvestYield, scene.Inventory.Count("sugarcane"));

            Assert.IsTrue(scene.Placement.BeginPlacement(millDef));
            Assert.IsTrue(scene.Placement.Confirm());
            string mill = scene.Town.Buildings.First(b => b.DefinitionId == "sugar_mill").InstanceId;
            Assert.IsTrue(scene.Construction.FinishNow(mill));

            int needed = sugar.Ingredients.Single().amount;
            scene.Inventory.TryAdd("sugarcane", System.Math.Max(0, needed - scene.Inventory.Count("sugarcane")));
            Assert.AreEqual(QueueResult.Queued, scene.Production.Enqueue(mill, sugar));
            _clock.Advance(sugar.DurationMs);
            Assert.AreEqual(1, scene.Production.Collect(mill));
            Assert.AreEqual(sugar.OutputAmount, scene.Inventory.Count("sugar"));
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
